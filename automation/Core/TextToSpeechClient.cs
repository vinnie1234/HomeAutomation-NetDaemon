using System.Net.Http;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Assist;
using Microsoft.Extensions.Options;
using NetDaemon.Client;

namespace Automation.Core;

/// <summary>
/// Generates a WhatsApp-playable voice note for text, using Home Assistant Cloud's text-to-speech.
///
/// Home Assistant's TTS gives back an MP3. The WhatsApp add-on advertises that it can turn any audio file
/// into a voice note by setting <c>ptt: true</c>, but that turned out not to reliably produce a file mobile
/// WhatsApp will actually play (confirmed even with the add-on's own documented example file, not just
/// Home Assistant's MP3) - so this decodes the MP3 and re-encodes it into a proper Ogg Opus file itself
/// (<see cref="OpusAudioEncoder"/>), and serves that from NetDaemon's own small web server (<see cref="IWhatsAppVoiceNoteCache"/>
/// plus the <c>/whatsapp-tts/{id}</c> endpoint registered in Program.cs) for the add-on to download.
///
/// Resolving the Home Assistant TTS media URL itself goes through NetDaemon's own, already-connected
/// websocket (<see cref="IHomeAssistantRunner"/>) rather than a separate connection: it's a single
/// request/response command, so unlike the Assist pipeline run (which needs streaming events and binary
/// audio frames) it doesn't need one. An earlier attempt called the REST endpoint the Home Assistant
/// frontend uses for this (<c>/api/tts_get_url</c>) directly; that failed with 403 when NetDaemon runs as a
/// Home Assistant add-on, where it talks to Home Assistant through the Supervisor's proxy, which apparently
/// doesn't allow that REST call through.
/// </summary>
public class TextToSpeechClient(
    IHomeAssistantRunner runner,
    IHttpClientFactory httpClientFactory,
    IWhatsAppVoiceNoteCache voiceNoteCache,
    IOptions<AppConfig> appConfig)
    : ITextToSpeechClient
{
    /// <summary>The text-to-speech engine used for spoken WhatsApp replies.</summary>
    private const string Engine = "tts.home_assistant_cloud";

    public async Task<string> GetSpeechUrlAsync(string text, string language, CancellationToken cancellationToken)
    {
        var mp3Url = await ResolveMp3UrlAsync(text, language, cancellationToken);

        using var client = httpClientFactory.CreateClient(nameof(TextToSpeechClient));
        var mp3 = await client.GetByteArrayAsync(mp3Url, cancellationToken);

        var (pcm, sampleRate) = Mp3Decoder.Decode(mp3);
        var oggOpus = OpusAudioEncoder.EncodeToOggOpus(pcm, sampleRate);

        var id = voiceNoteCache.Add(oggOpus);
        return $"{appConfig.Value.Whatsapp.NetDaemonBaseUrl.TrimEnd('/')}/whatsapp-tts/{id}";
    }

    /// <summary>Asks Home Assistant to generate the speech, and returns the (Home Assistant-hosted) MP3 URL for it.</summary>
    private async Task<string> ResolveMp3UrlAsync(string text, string language, CancellationToken cancellationToken)
    {
        var connection = runner.CurrentConnection ?? throw new InvalidOperationException("Not connected to Home Assistant");

        var contentId = $"media-source://tts/{Engine}?message={Uri.EscapeDataString(text)}&language={Uri.EscapeDataString(language)}";
        var media = await connection.SendCommandAndReturnResponseAsync<ResolveMediaCommand, ResolvedMedia>(
            new ResolveMediaCommand { MediaContentId = contentId }, cancellationToken);

        if (string.IsNullOrEmpty(media?.Url))
            throw new InvalidOperationException("Home Assistant did not return a text-to-speech URL");

        return appConfig.Value.BaseUrlHomeAssistant.TrimEnd('/') + media.Url;
    }
}
