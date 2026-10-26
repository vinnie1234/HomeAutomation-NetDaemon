using System.IO;
using System.Net.WebSockets;
using Automation.Configuration;
using Automation.Models.Assist;
using Microsoft.Extensions.Options;

namespace Automation.Core;

/// <summary>
/// Drives a Home Assistant Assist pipeline run (speech-to-text followed by intent recognition) over its own
/// websocket connection. NetDaemon's own <c>IHomeAssistantConnection</c> only supports simple request/response
/// commands; a pipeline run is a streaming command (a request followed by a stream of "event" messages
/// sharing its id) that also needs raw binary audio frames, neither of which it exposes.
///
/// The connection is opened once and reused for every run, the same way NetDaemon's own connection is:
/// opening (and authenticating) a brand new connection per voice note turned out to get rejected with
/// HTTP 401 on the connect itself once Home Assistant's own connection had been open for a while, while
/// that one long-lived connection kept working fine throughout. A dead connection is replaced on the next run.
/// </summary>
public class AssistPipelineClient(ILogger<AssistPipelineClient> logger, IOptions<HomeAssistantConnectionOptions> options)
    : IAssistPipelineClient, IDisposable
{
    /// <summary>How long to wait for a whole pipeline run (connect if needed, stt, intent) before giving up.</summary>
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The largest chunk of PCM audio sent in a single binary websocket frame.</summary>
    private const int AudioChunkSize = 4096;

    /// <summary>Serializes runs: the connection is shared, and its request/response-by-id protocol isn't safe to interleave.</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ClientWebSocket? _socket;
    private int _nextId;

    public async Task<AssistPipelineResult> RunSpeechToIntentAsync(string pipelineName, byte[] pcm16, int sampleRate, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RunTimeout);
        var ct = timeout.Token;

        await _lock.WaitAsync(ct);
        try
        {
            var socket = await GetConnectedSocketAsync(ct);

            var pipelineId = await GetPipelineIdAsync(socket, pipelineName, ct);
            if (pipelineId is null)
                return new AssistPipelineResult { Success = false, Error = $"No Assist pipeline named '{pipelineName}' was found" };

            return await RunPipelineAsync(socket, pipelineId, pcm16, sampleRate, ct);
        }
        catch
        {
            // The connection may now be in a bad state (e.g. Home Assistant closed it); drop it so the
            // next run reconnects instead of repeatedly failing on the same dead socket.
            DisposeSocket();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Gets the shared connection, (re)connecting and authenticating it first if it isn't open.</summary>
    private async Task<ClientWebSocket> GetConnectedSocketAsync(CancellationToken ct)
    {
        if (_socket is { State: WebSocketState.Open }) return _socket;

        DisposeSocket();
        var socket = new ClientWebSocket();
        await ConnectAndAuthenticateAsync(socket, ct);
        _socket = socket;
        return socket;
    }

    /// <summary>Connects to the Home Assistant websocket API and authenticates with the configured long-lived token.</summary>
    private async Task ConnectAndAuthenticateAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var connection = options.Value;
        var scheme = connection.Ssl ? "wss" : "ws";
        var uri = new Uri($"{scheme}://{connection.Host}:{connection.EffectivePort}/{connection.EffectiveWebsocketPath}");

        await socket.ConnectAsync(uri, ct);
        await ReceiveJsonAsync(socket, ct); // auth_required, no action needed

        await SendJsonAsync(socket, new { type = "auth", access_token = connection.Token }, ct);
        var auth = await ReceiveJsonAsync(socket, ct);
        if (auth.RootElement.GetProperty("type").GetString() != "auth_ok")
            throw new InvalidOperationException("Home Assistant rejected the long-lived access token for the Assist pipeline connection");
    }

    /// <summary>Looks up a pipeline's id by name; pipeline ids are opaque and not safe to hardcode or cache across runs.</summary>
    private async Task<string?> GetPipelineIdAsync(ClientWebSocket socket, string pipelineName, CancellationToken ct)
    {
        await SendJsonAsync(socket, new { id = NextId(), type = "assist_pipeline/pipeline/list" }, ct);
        using var response = await ReceiveJsonAsync(socket, ct);

        foreach (var pipeline in response.RootElement.GetProperty("result").GetProperty("pipelines").EnumerateArray())
            if (string.Equals(pipeline.GetProperty("name").GetString(), pipelineName, StringComparison.OrdinalIgnoreCase))
                return pipeline.GetProperty("id").GetString();

        return null;
    }

    /// <summary>Starts the pipeline run, streams the audio, and collects the transcript and the assistant's reply.</summary>
    private async Task<AssistPipelineResult> RunPipelineAsync(ClientWebSocket socket, string pipelineId, byte[] pcm16, int sampleRate, CancellationToken ct)
    {
        var runCommandId = NextId();
        await SendJsonAsync(socket, new
        {
            id = runCommandId,
            type = "assist_pipeline/run",
            start_stage = "stt",
            end_stage = "intent",
            pipeline = pipelineId,
            input = new { sample_rate = sampleRate }
        }, ct);

        string? transcript = null;
        string? responseText = null;
        string? error = null;
        var audioSent = false;

        while (true)
        {
            using var message = await ReceiveJsonAsync(socket, ct);
            var root = message.RootElement;

            if (root.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                var commandError = root.TryGetProperty("error", out var errorElement) ? errorElement.GetProperty("message").GetString() : "unknown error";
                return new AssistPipelineResult { Success = false, Error = commandError };
            }

            if (!root.TryGetProperty("event", out var @event)) continue; // the initial {"type":"result","success":true} ack
            var eventType = @event.GetProperty("type").GetString();
            var data = @event.GetProperty("data");

            switch (eventType)
            {
                case "run-start" when !audioSent:
                    var handlerId = (byte)data.GetProperty("runner_data").GetProperty("stt_binary_handler_id").GetInt32();
                    await SendAudioAsync(socket, handlerId, pcm16, ct);
                    audioSent = true;
                    break;

                case "stt-end":
                    transcript = data.GetProperty("stt_output").GetProperty("text").GetString();
                    break;

                case "intent-end":
                    responseText = data.GetProperty("intent_output").GetProperty("response").GetProperty("speech")
                        .GetProperty("plain").GetProperty("speech").GetString();
                    break;

                case "error":
                    // Keep looping instead of returning here: Home Assistant still sends a final "run-end"
                    // for this id afterward, and leaving it unread would corrupt the next command's response
                    // on this (reused) connection.
                    error = data.GetProperty("message").GetString();
                    logger.LogWarning("Assist pipeline run reported an error: {Message}", error);
                    break;

                case "run-end":
                    return error is null
                        ? new AssistPipelineResult { Success = true, Transcript = transcript, ResponseText = responseText }
                        : new AssistPipelineResult { Success = false, Transcript = transcript, Error = error };
            }
        }
    }

    /// <summary>
    /// Streams PCM audio to Home Assistant as binary websocket frames prefixed with the handler id, then sends an
    /// empty frame to mark the end of the audio (see <c>assist_pipeline/websocket_api.py</c>'s binary handler protocol).
    /// </summary>
    private static async Task SendAudioAsync(ClientWebSocket socket, byte handlerId, byte[] pcm16, CancellationToken ct)
    {
        for (var offset = 0; offset < pcm16.Length; offset += AudioChunkSize)
        {
            var length = Math.Min(AudioChunkSize, pcm16.Length - offset);
            var frame = new byte[length + 1];
            frame[0] = handlerId;
            Buffer.BlockCopy(pcm16, offset, frame, 1, length);
            await socket.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct);
        }

        await socket.SendAsync(new[] { handlerId }, WebSocketMessageType.Binary, endOfMessage: true, ct);
    }

    private static async Task SendJsonAsync(ClientWebSocket socket, object message, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new InvalidOperationException("Home Assistant closed the Assist pipeline websocket connection");
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        stream.Position = 0;
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    /// <summary>Gets the next command id, unique for the lifetime of the (possibly long-lived, reused) connection.</summary>
    private int NextId() => Interlocked.Increment(ref _nextId);

    private void DisposeSocket()
    {
        _socket?.Dispose();
        _socket = null;
    }

    public void Dispose()
    {
        DisposeSocket();
        _lock.Dispose();
    }
}
