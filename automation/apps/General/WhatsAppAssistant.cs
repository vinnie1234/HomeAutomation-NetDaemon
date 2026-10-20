using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Assist;
using Automation.Models.WhatsApp;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Lets Vincent and Carleen control the house, and ask questions, over WhatsApp. A text message is sent
/// straight to a conversation agent; a voice note is first transcribed and then sent to a conversation
/// agent, using an Assist pipeline (its speech-to-text and conversation agent together). Gemini is tried
/// first; if it fails (an error, not just an answer it couldn't give), the Home Assistant Cloud agent is
/// used as a fallback.
///
/// Every incoming message is marked as read and makes the WhatsApp connection show as online. The last
/// <see cref="MaxHistoryTurns"/> exchanges per sender are kept, so follow-up questions ("wat vroeg ik net",
/// "zet 'm nu weer uit") have context. A message can also ask to forward the last reply to the other
/// person; a reply to a voice note is sent back as both text (with the transcript) and a voice note.
/// </summary>
[NetDaemonApp(Id = nameof(WhatsAppAssistant))]
public class WhatsAppAssistant : BaseApp
{
    /// <summary>The conversation agent, and the Assist pipeline for voice notes, tried first.</summary>
    private const string PrimaryAgentId = "conversation.google_ai_conversation";
    private const string PrimaryPipelineName = "Gemini";

    /// <summary>The conversation agent, and the Assist pipeline for voice notes, used when the primary one fails.</summary>
    private const string FallbackAgentId = "conversation.home_assistant";
    private const string FallbackPipelineName = "Home Assistant Cloud";

    private const string Language = "nl";

    /// <summary>The locale for text-to-speech specifically: the bare "nl" <see cref="Language"/> isn't accepted there.</summary>
    private const string TtsLanguage = "nl-NL";

    /// <summary>The sample rate PCM audio is decoded to before it is sent to Assist for speech-to-text.</summary>
    private const int SpeechToTextSampleRate = 16000;

    private const string NoResponseText = "Sorry, ik kreeg geen antwoord van de assistent.";

    /// <summary>The storage key of the per-sender conversation history.</summary>
    public const string HistoryStorageKey = "WhatsAppAssistantHistory";

    /// <summary>How many exchanges (user message + reply) are kept per sender, for context on follow-up questions.</summary>
    public const int MaxHistoryTurns = 5;

    /// <summary>The word the classification prompt in <see cref="IsForwardRequestAsync"/> answers with for a forward request.</summary>
    private const string ForwardKeyword = "DOORSTUREN";

    private readonly IWhatsAppMediaClient _mediaClient;
    private readonly IAssistPipelineClient _assistPipeline;
    private readonly ITextToSpeechClient _textToSpeech;
    private readonly IDataRepository _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhatsAppAssistant"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="mediaClient">Downloads voice notes from WhatsApp.</param>
    /// <param name="assistPipeline">Runs voice notes through Assist's speech-to-text and conversation agent.</param>
    /// <param name="textToSpeech">Generates the voice note sent back when the person sent a voice note.</param>
    /// <param name="storage">The data repository holding the conversation history.</param>
    /// <param name="config">The app configuration.</param>
    public WhatsAppAssistant(
        IHaContext ha,
        ILogger<WhatsAppAssistant> logger,
        INotify notify,
        IScheduler scheduler,
        IWhatsAppMediaClient mediaClient,
        IAssistPipelineClient assistPipeline,
        ITextToSpeechClient textToSpeech,
        IDataRepository storage,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _mediaClient = mediaClient;
        _assistPipeline = assistPipeline;
        _textToSpeech = textToSpeech;
        _storage = storage;

        // Only Vincent and Carleen may use the assistant
        var allowedSenders = new[] { Vincent.Whatsapp, Carleen.Whatsapp };
        HaContext.Events.Filter<WhatsAppMessageEvent>("new_whatsapp_message")
            .Where(e => e.Data is { Key.FromMe: false } data && allowedSenders.Contains(data.Key.RemoteJid))
            .Subscribe(e => _ = HandleMessageAsync(e.Data!));
    }

    private async Task HandleMessageAsync(WhatsAppMessageEvent message)
    {
        try
        {
            AcknowledgeMessage(message);

            switch (message)
            {
                case { Type: "conversation", Message.Conversation: { } text } when !string.IsNullOrWhiteSpace(text):
                    await ProcessUserMessageAsync(message.Key.RemoteJid, text, isVoice: false);
                    break;

                case { Type: "audioMessage", Message.AudioMessage: { } audio }:
                    await HandleAudioMessageAsync(message.Key.RemoteJid, audio);
                    break;

                default:
                    Logger.LogDebug("Ignoring WhatsApp message of type {Type} from {Sender}: not text or a voice note", message.Type, message.Key.RemoteJid);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling a WhatsApp message from {Sender}", message.Key.RemoteJid);
            Reply(message.Key.RemoteJid, "Er is iets misgegaan bij het verwerken van je bericht.");
        }
    }

    /// <summary>Marks the message as read and makes the WhatsApp connection show as online to the sender.</summary>
    private void AcknowledgeMessage(WhatsAppMessageEvent message)
    {
        Services.Whatsapp.ReadMessages(clientId: message.ClientId, body: new
        {
            keys = new { id = message.Key.Id, remoteJid = message.Key.RemoteJid, fromMe = message.Key.FromMe }
        });
        Services.Whatsapp.SendPresenceUpdate(clientId: message.ClientId, @type: "available", to: message.Key.RemoteJid);
    }

    /// <summary>Decrypts and transcribes a voice note, then processes the transcript like a typed message.</summary>
    private async Task HandleAudioMessageAsync(string sender, WhatsAppAudioMessage audio)
    {
        var encrypted = await _mediaClient.DownloadAsync(audio.Url, CancellationToken.None);
        var ogg = WhatsAppMediaDecryptor.Decrypt(encrypted, audio.MediaKey, WhatsAppMediaDecryptor.MediaKind.Audio);
        var decoded = OpusAudioDecoder.DecodeToPcm16(ogg, SpeechToTextSampleRate);
        if (decoded.SkippedPacketCount > 0)
            Logger.LogWarning("Skipped {Skipped} of {Total} audio packets while decoding a voice note from {Sender}: they could not be decoded",
                decoded.SkippedPacketCount, decoded.AudioPacketCount, sender);
        var pcm = decoded.Pcm;

        var result = await TryRunPipelineAsync(PrimaryPipelineName, pcm);
        if (result is null)
        {
            Logger.LogWarning("The primary Assist pipeline {Pipeline} failed; falling back to {FallbackPipeline}", PrimaryPipelineName, FallbackPipelineName);
            result = await TryRunPipelineAsync(FallbackPipelineName, pcm);
        }

        if (result is null || string.IsNullOrWhiteSpace(result.Transcript))
        {
            Reply(sender, "Ik kon je spraakbericht niet verstaan.");
            return;
        }

        await ProcessUserMessageAsync(sender, result.Transcript, isVoice: true);
    }

    /// <summary>
    /// Handles one user message, whether typed or transcribed from a voice note: forwards the last reply
    /// if that is what was asked, otherwise asks the conversation agent (with recent history for context)
    /// and replies, saving the exchange in that history. A voice note also gets a spoken reply.
    /// </summary>
    private async Task ProcessUserMessageAsync(string sender, string userText, bool isVoice)
    {
        var history = GetHistory(sender);

        if (await TryForwardLastReplyAsync(sender, userText, history)) return;

        var contextualText = BuildContextualText(userText, history);
        var speech = await TryProcessTextAsync(contextualText, PrimaryAgentId);
        if (speech is null)
        {
            Logger.LogWarning("The primary conversation agent {AgentId} failed; falling back to {FallbackAgentId}", PrimaryAgentId, FallbackAgentId);

            // The fallback is Home Assistant's built-in agent: a sentence matcher, not an LLM. It cannot make
            // sense of the "Eerdere berichten..." context prefix (that would just make it fail to match any
            // intent), so it gets the plain message instead; it also has no conversation memory of its own.
            speech = await TryProcessTextAsync(userText, FallbackAgentId);
        }

        var answer = speech ?? NoResponseText;
        AddTurn(sender, userText, answer);

        if (isVoice)
        {
            Reply(sender, $"Jij zei: \"{userText}\"\n\n{answer}");
            await ReplyWithVoiceAsync(sender, answer);
        }
        else
        {
            Reply(sender, answer);
        }
    }

    /// <summary>
    /// Checks (via the conversation agent, since the wording isn't fixed) whether the message asks to
    /// forward the last reply to the other person, and does so if it does.
    /// </summary>
    /// <returns><c>true</c> when the message was handled as a forward request.</returns>
    private async Task<bool> TryForwardLastReplyAsync(string sender, string userText, IReadOnlyList<WhatsAppConversationTurn> history)
    {
        if (!await IsForwardRequestAsync(userText)) return false;

        var lastReply = history.Count > 0 ? history[^1].AssistantReply : null;
        if (lastReply is null)
        {
            Reply(sender, "Er is nog geen eerder antwoord dat ik kan doorsturen.");
            return true;
        }

        var (senderName, otherName, otherJid) = GetPersons(sender);
        Services.Whatsapp.SendMessage(clientId: "default", to: otherJid, body: new { text = $"Doorgestuurd van {senderName}:\n\n{lastReply}" });
        Reply(sender, $"Ik heb het laatste antwoord doorgestuurd naar {otherName}.");
        return true;
    }

    /// <summary>Asks the conversation agent to classify whether a message is a request to forward the last reply.</summary>
    private async Task<bool> IsForwardRequestAsync(string userText)
    {
        var prompt =
            $"Antwoord ALLEEN met het woord {ForwardKeyword} als het volgende bericht vraagt om het vorige " +
            "antwoord door te sturen of door te geven aan de partner. Antwoord in alle andere gevallen ALLEEN " +
            $"met het woord NORMAAL. Bericht: \"{userText}\"";

        var speech = await TryProcessTextAsync(prompt, PrimaryAgentId);
        return speech?.Contains(ForwardKeyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Gets the sender's own name, and the other person's name and WhatsApp ID.</summary>
    private (string SenderName, string OtherName, string OtherJid) GetPersons(string sender) =>
        sender == Vincent.Whatsapp ? ("Vincent", "Carleen", Carleen.Whatsapp) : ("Carleen", "Vincent", Vincent.Whatsapp);

    /// <summary>Prepends the recent conversation history, if any, so the agent has context for follow-up questions.</summary>
    private static string BuildContextualText(string userText, IReadOnlyList<WhatsAppConversationTurn> history)
    {
        if (history.Count == 0) return userText;

        var transcript = string.Join("\n", history.SelectMany(turn => new[] { $"Jij: {turn.UserMessage}", $"Assistent: {turn.AssistantReply}" }));
        return $"Eerdere berichten in dit gesprek:\n{transcript}\n\nNieuw bericht van jou: {userText}";
    }

    /// <summary>
    /// Sends text to a conversation agent.
    /// </summary>
    /// <returns>The agent's reply, or <c>null</c> when the call failed or the agent itself reported an error
    /// (as opposed to a normal answer that just didn't help, which is not treated as a failure).</returns>
    private async Task<string?> TryProcessTextAsync(string text, string agentId)
    {
        try
        {
            var response = await Services.Conversation.ProcessAsync(text, language: Language, agentId: agentId);
            return ExtractResult(response) is (var speech, false) ? speech : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Conversation agent {AgentId} threw an exception", agentId);
            return null;
        }
    }

    /// <summary>Runs a voice note through an Assist pipeline.</summary>
    /// <returns>The result, or <c>null</c> when the pipeline failed (an exception, or an unsuccessful run).</returns>
    private async Task<AssistPipelineResult?> TryRunPipelineAsync(string pipelineName, byte[] pcm)
    {
        try
        {
            var result = await _assistPipeline.RunSpeechToIntentAsync(pipelineName, pcm, SpeechToTextSampleRate, CancellationToken.None);
            if (!result.Success)
                Logger.LogWarning("Assist pipeline {Pipeline} did not succeed: {Error}", pipelineName, result.Error);
            return result.Success ? result : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Assist pipeline {Pipeline} threw an exception", pipelineName);
            return null;
        }
    }

    /// <summary>Generates a spoken version of the reply and sends it as a WhatsApp voice note.</summary>
    private async Task ReplyWithVoiceAsync(string sender, string answer)
    {
        try
        {
            var url = await _textToSpeech.GetSpeechUrlAsync(answer, TtsLanguage, CancellationToken.None);
            Services.Whatsapp.SendMessage(clientId: "default", to: sender, body: new { audio = new { url }, ptt = true });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not generate a spoken reply for {Sender}", sender);
        }
    }

    private void Reply(string to, string text) =>
        Services.Whatsapp.SendMessage(clientId: "default", to: to, body: new { text });

    /// <summary>Extracts the plain-text reply from a <c>conversation.process</c> response, and whether the agent reported an error.</summary>
    private static (string? Speech, bool IsError) ExtractResult(JsonElement? response)
    {
        if (response is not { } element) return (null, true);
        var result = element.GetProperty("response");
        var isError = result.GetProperty("response_type").GetString() == "error";
        var speech = result.GetProperty("speech").GetProperty("plain").GetProperty("speech").GetString();
        return (speech, isError);
    }

    private List<WhatsAppConversationTurn> GetHistory(string sender) =>
        GetAllHistory().GetValueOrDefault(sender) ?? [];

    /// <summary>Appends an exchange to a sender's history, trimmed to the last <see cref="MaxHistoryTurns"/>.</summary>
    private void AddTurn(string sender, string userMessage, string assistantReply)
    {
        var all = GetAllHistory();
        var history = all.GetValueOrDefault(sender) ?? [];

        history.Add(new WhatsAppConversationTurn { UserMessage = userMessage, AssistantReply = assistantReply });
        if (history.Count > MaxHistoryTurns) history.RemoveAt(0);

        all[sender] = history;
        _storage.Save(HistoryStorageKey, all);
    }

    private Dictionary<string, List<WhatsAppConversationTurn>> GetAllHistory() =>
        _storage.Get<Dictionary<string, List<WhatsAppConversationTurn>>>(HistoryStorageKey) ?? [];
}
