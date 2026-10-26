using System.Reactive.Subjects;
using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.Assist;
using Automation.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class WhatsAppAssistantTests : IDisposable
{
    private const string Vincent = "184829706502256@lid";
    private const string Carleen = "109508881846453@lid";
    private const string Stranger = "999999999999999@lid";
    private const string GeminiAgentId = "conversation.google_ai_conversation";
    private const string HomeAssistantCloudAgentId = "conversation.home_assistant";

    /// <summary>Marks the classification prompt <see cref="WhatsAppAssistant.IsForwardRequestAsync"/> sends, so it can be mocked separately from the normal conversation call.</summary>
    private const string ClassificationMarker = "Antwoord ALLEEN met het woord";

    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IWhatsAppMediaClient _mediaClient = Substitute.For<IWhatsAppMediaClient>();
    private readonly IAssistPipelineClient _assistPipeline = Substitute.For<IAssistPipelineClient>();
    private readonly ITextToSpeechClient _textToSpeech = Substitute.For<ITextToSpeechClient>();
    private readonly Subject<Event> _events = new();
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"WhatsAppAssistantTests_{Guid.NewGuid():N}");
    private readonly IDataRepository _storage;

    public WhatsAppAssistantTests()
    {
        Directory.CreateDirectory(_storagePath);
        _storage = new DataRepository(_storagePath, Substitute.For<ILogger>());
    }

    public void Dispose() => Directory.Delete(_storagePath, recursive: true);

    private WhatsAppAssistant CreateApp()
    {
        _ctx.HaContext.Events.Returns(_events);
        var config = Options.Create(new AppConfig { Whatsapp = new WhatsAppConfig { Vincent = Vincent, Carleen = Carleen } });
        return _ctx.InitApp<WhatsAppAssistant>(_mediaClient, _assistPipeline, _textToSpeech, _storage, config);
    }

    private void SendEvent(string remoteJid, bool fromMe, string type, object message) =>
        _events.OnNext(new Event
        {
            EventType = "new_whatsapp_message",
            DataElement = JsonSerializer.SerializeToElement(new
            {
                clientId = "default",
                type,
                key = new { remoteJid, fromMe, id = "ABC" },
                pushName = "Test",
                message
            })
        });

    /// <summary>Mocks a normal (non-classification) conversation.process reply for an agent.</summary>
    private void MockConversationResponse(string agentId, string speech, string responseType = "action_done") =>
        _ctx.HaContext.CallServiceWithResponseAsync("conversation", "process", null,
                Arg.Is<object>(d => GetProperty(d, "agent_id") == agentId && !IsClassificationPrompt(d)))
            .Returns(JsonSerializer.SerializeToElement(new { response = new { response_type = responseType, speech = new { plain = new { speech } } } }));

    private void MockConversationThrows(string agentId) =>
        _ctx.HaContext.CallServiceWithResponseAsync("conversation", "process", null,
                Arg.Is<object>(d => GetProperty(d, "agent_id") == agentId))
            .Returns(Task.FromException<JsonElement?>(new HttpRequestException("boom")));

    /// <summary>Mocks the "is this a forward request" classification call that precedes every message.</summary>
    private void MockClassification(string reply) =>
        _ctx.HaContext.CallServiceWithResponseAsync("conversation", "process", null,
                Arg.Is<object>(d => GetProperty(d, "agent_id") == GeminiAgentId && IsClassificationPrompt(d)))
            .Returns(JsonSerializer.SerializeToElement(new { response = new { response_type = "action_done", speech = new { plain = new { speech = reply } } } }));

    private static bool IsClassificationPrompt(object? data) => GetProperty(data, "text")?.Contains(ClassificationMarker) == true;

    [Fact]
    public void TextMessage_FromVincent_IsAnsweredByGemini()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationResponse(GeminiAgentId, "De woonkamerlamp staat aan");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe de woonkamerlamp aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(d, "to") == Vincent && GetProperty(GetValue(d, "body"), "text") == "De woonkamerlamp staat aan"));
        _ctx.HaContext.DidNotReceive().CallServiceWithResponseAsync("conversation", "process", null,
            Arg.Is<object>(d => GetProperty(d, "agent_id") == HomeAssistantCloudAgentId));
    }

    [Fact]
    public void TextMessage_FromCarleen_IsAlsoHandled()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationResponse(GeminiAgentId, "Ok");

        SendEvent(Carleen, fromMe: false, type: "conversation", message: new { conversation = "hoe laat is het" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null, Arg.Is<object>(d => GetProperty(d, "to") == Carleen));
    }

    [Fact]
    public void TextMessage_WhenGeminiThrows_FallsBackToHomeAssistantCloud()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationThrows(GeminiAgentId);
        MockConversationResponse(HomeAssistantCloudAgentId, "De woonkamerlamp staat aan");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe de woonkamerlamp aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "De woonkamerlamp staat aan"));
    }

    [Fact]
    public void TextMessage_WhenGeminiReportsAnError_FallsBackToHomeAssistantCloud()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationResponse(GeminiAgentId, "Er ging iets mis", responseType: "error");
        MockConversationResponse(HomeAssistantCloudAgentId, "De woonkamerlamp staat aan");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe de woonkamerlamp aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "De woonkamerlamp staat aan"));
    }

    [Fact]
    public void TextMessage_WhenGeminiJustDoesNotKnowTheAnswer_DoesNotFallBack()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        // A normal (non-error) response that just isn't helpful is still Gemini's real answer, not a failure
        MockConversationResponse(GeminiAgentId, "Sorry, ik snap het niet", responseType: "query_answer");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe iets onmogelijks" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "Sorry, ik snap het niet"));
        _ctx.HaContext.DidNotReceive().CallServiceWithResponseAsync("conversation", "process", null,
            Arg.Is<object>(d => GetProperty(d, "agent_id") == HomeAssistantCloudAgentId));
    }

    [Fact]
    public void TextMessage_WhenBothAgentsFail_RepliesThatNoAnswerWasReceived()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationThrows(GeminiAgentId);
        MockConversationThrows(HomeAssistantCloudAgentId);

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe de woonkamerlamp aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "Sorry, ik kreeg geen antwoord van de assistent."));
    }

    [Fact]
    public void TextMessage_FromAnUnknownSender_IsIgnored()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        SendEvent(Stranger, fromMe: false, type: "conversation", message: new { conversation = "doe de deur open" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.DidNotReceiveWithAnyArgs().CallServiceWithResponseAsync(null!, null!);
        _ctx.HaContext.DidNotReceive().CallService("whatsapp", "send_message", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void TextMessage_SentByTheAssistantItself_IsIgnored()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        // fromMe: true is the assistant's own reply echoed back; must not be treated as an incoming command
        SendEvent(Vincent, fromMe: true, type: "conversation", message: new { conversation = "De woonkamerlamp staat aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.DidNotReceiveWithAnyArgs().CallServiceWithResponseAsync(null!, null!);
    }

    [Fact]
    public void VoiceNote_FromVincent_IsTranscribedByGeminiAndAnsweredWithBoth()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        var mediaKey = new byte[32];
        var voiceNote = BuildEncryptedVoiceNote(mediaKey);
        _mediaClient.DownloadAsync("https://example.test/voice.enc", Arg.Any<CancellationToken>()).Returns(voiceNote);
        _assistPipeline.RunSpeechToIntentAsync("Gemini", Arg.Any<byte[]>(), 16000, Arg.Any<CancellationToken>())
            .Returns(new AssistPipelineResult { Success = true, Transcript = "doe de woonkamerlamp aan" });
        // The transcript is sent to the conversation agent itself (with history/forward handling), not the pipeline's own answer
        MockConversationResponse(GeminiAgentId, "De woonkamerlamp staat aan");

        SendEvent(Vincent, fromMe: false, type: "audioMessage", message: new
        {
            audioMessage = new
            {
                url = "https://example.test/voice.enc",
                mimetype = "audio/ogg; codecs=opus",
                mediaKey = Convert.ToBase64String(mediaKey),
                ptt = true,
                seconds = 2
            }
        });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(d, "to") == Vincent &&
                                 GetProperty(GetValue(d, "body"), "text") == "Jij zei: \"doe de woonkamerlamp aan\"\n\nDe woonkamerlamp staat aan"));
        // The decoded audio actually reached the pipeline at the expected sample rate, decrypted and decoded correctly
        _assistPipeline.Received(1).RunSpeechToIntentAsync("Gemini", Arg.Is<byte[]>(pcm => pcm.Length > 0), 16000, Arg.Any<CancellationToken>());
        _assistPipeline.DidNotReceive().RunSpeechToIntentAsync("Home Assistant Cloud", Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void VoiceNote_WhenGeminiPipelineFails_FallsBackToHomeAssistantCloud()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        var mediaKey = new byte[32];
        _mediaClient.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BuildEncryptedVoiceNote(mediaKey));
        _assistPipeline.RunSpeechToIntentAsync("Gemini", Arg.Any<byte[]>(), 16000, Arg.Any<CancellationToken>())
            .Returns(new AssistPipelineResult { Success = false, Error = "quota exceeded" });
        _assistPipeline.RunSpeechToIntentAsync("Home Assistant Cloud", Arg.Any<byte[]>(), 16000, Arg.Any<CancellationToken>())
            .Returns(new AssistPipelineResult { Success = true, Transcript = "hoe laat is het" });
        MockConversationResponse(GeminiAgentId, "Het is 14:00");

        SendEvent(Vincent, fromMe: false, type: "audioMessage", message: new
        {
            audioMessage = new { url = "https://example.test/voice.enc", mimetype = "audio/ogg", mediaKey = Convert.ToBase64String(mediaKey), ptt = true, seconds = 1 }
        });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "Jij zei: \"hoe laat is het\"\n\nHet is 14:00"));
    }

    [Fact]
    public void VoiceNote_WhenBothPipelinesFail_RepliesThatItCouldNotBeUnderstood()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        var mediaKey = new byte[32];
        _mediaClient.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BuildEncryptedVoiceNote(mediaKey));
        _assistPipeline.RunSpeechToIntentAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new AssistPipelineResult { Success = false, Error = "No text recognized" });

        SendEvent(Vincent, fromMe: false, type: "audioMessage", message: new
        {
            audioMessage = new { url = "https://example.test/voice.enc", mimetype = "audio/ogg", mediaKey = Convert.ToBase64String(mediaKey), ptt = true, seconds = 1 }
        });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "Ik kon je spraakbericht niet verstaan."));
    }

    [Fact]
    public void WhenDownloadingTheVoiceNoteThrows_RepliesWithAGenericError()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        _mediaClient.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<byte[]>(new HttpRequestException("boom")));

        SendEvent(Vincent, fromMe: false, type: "audioMessage", message: new
        {
            audioMessage = new { url = "https://example.test/voice.enc", mimetype = "audio/ogg", mediaKey = Convert.ToBase64String(new byte[32]), ptt = true, seconds = 1 }
        });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "Er is iets misgegaan bij het verwerken van je bericht."));
    }

    [Fact]
    public void UnknownMessageType_IsIgnored()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        SendEvent(Vincent, fromMe: false, type: "imageMessage", message: new { });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.DidNotReceive().CallService("whatsapp", "send_message", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void IncomingMessage_IsMarkedAsReadAndMakesTheConnectionAvailable()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationResponse(GeminiAgentId, "Ok");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "hoe laat is het" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "read_messages", null, Arg.Is<object>(d => IsReadMessagesForVincentsMessage(d)));
        _ctx.HaContext.Received(1).CallService("whatsapp", "send_presence_update", null,
            Arg.Is<object>(d => GetProperty(d, "clientId") == "default" && GetProperty(d, "type") == "available" && GetProperty(d, "to") == Vincent));
    }

    private static bool IsReadMessagesForVincentsMessage(object d)
    {
        var keys = GetValue(GetValue(d, "body"), "keys");
        return GetProperty(d, "clientId") == "default" && keys is { } k &&
               k.GetProperty("id").GetString() == "ABC" &&
               k.GetProperty("remoteJid").GetString() == Vincent &&
               k.GetProperty("fromMe").GetBoolean() == false;
    }

    [Fact]
    public void FollowUpQuestion_IncludesRecentHistoryAsContext()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationResponse(GeminiAgentId, "De woonkamerlamp staat aan");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe de woonkamerlamp aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        MockConversationResponse(GeminiAgentId, "De woonkamerlamp staat nu uit");
        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "wil je het nu weer uitzetten" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallServiceWithResponseAsync("conversation", "process", null,
            Arg.Is<object>(d => GetProperty(d, "agent_id") == GeminiAgentId &&
                                 GetProperty(d, "text")!.Contains("doe de woonkamerlamp aan") &&
                                 GetProperty(d, "text")!.Contains("De woonkamerlamp staat aan") &&
                                 GetProperty(d, "text")!.Contains("wil je het nu weer uitzetten")));
    }

    [Fact]
    public void FollowUpQuestion_WhenGeminiFails_FallsBackWithThePlainMessageInsteadOfTheHistoryPrefixedOne()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationResponse(GeminiAgentId, "De woonkamerlamp staat aan");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe de woonkamerlamp aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        // Gemini fails on the follow-up; the fallback is Home Assistant's built-in sentence-matching agent,
        // which cannot handle the "Eerdere berichten..." context prefix, so it must get the plain message
        MockConversationThrows(GeminiAgentId);
        MockConversationResponse(HomeAssistantCloudAgentId, "De woonkamerlamp staat nu uit");
        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "zet de woonkamerlamp weer uit" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallServiceWithResponseAsync("conversation", "process", null,
            Arg.Is<object>(d => GetProperty(d, "agent_id") == HomeAssistantCloudAgentId && GetProperty(d, "text") == "zet de woonkamerlamp weer uit"));
        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "De woonkamerlamp staat nu uit"));
    }

    [Fact]
    public void History_IsCappedAtTheLastFiveTurns()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        for (var i = 1; i <= 6; i++)
        {
            MockConversationResponse(GeminiAgentId, $"antwoord{i}");
            SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = $"bericht{i}" });
            _ctx.HaContextMock.ProcessPendingOperations();
        }

        MockConversationResponse(GeminiAgentId, "antwoord7");
        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "bericht7" });
        _ctx.HaContextMock.ProcessPendingOperations();

        // Only the last 5 turns (2-6) remain; the first one dropped out
        _ctx.HaContext.Received(1).CallServiceWithResponseAsync("conversation", "process", null,
            Arg.Is<object>(d => GetProperty(d, "agent_id") == GeminiAgentId &&
                                 GetProperty(d, "text")!.Contains("bericht7") &&
                                 GetProperty(d, "text")!.Contains("bericht2") &&
                                 !GetProperty(d, "text")!.Contains("bericht1")));
    }

    [Fact]
    public void ForwardRequest_SendsTheLastReplyToTheOtherPersonAndConfirms()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockConversationResponse(GeminiAgentId, "De woonkamerlamp staat aan");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "doe de woonkamerlamp aan" });
        _ctx.HaContextMock.ProcessPendingOperations();

        MockClassification("DOORSTUREN");
        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "stuur dit antwoord ook naar carleen" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(d, "to") == Carleen && GetProperty(GetValue(d, "body"), "text")!.Contains("De woonkamerlamp staat aan")));
        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(d, "to") == Vincent && GetProperty(GetValue(d, "body"), "text") == "Ik heb het laatste antwoord doorgestuurd naar Carleen."));
        // The forward request itself was never sent to the conversation agent as a normal question
        _ctx.HaContext.DidNotReceive().CallServiceWithResponseAsync("conversation", "process", null,
            Arg.Is<object>(d => GetProperty(d, "text") == "stuur dit antwoord ook naar carleen"));
    }

    [Fact]
    public void ForwardRequest_WithNoPriorReply_SaysThereIsNothingToForward()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();
        MockClassification("DOORSTUREN");

        SendEvent(Vincent, fromMe: false, type: "conversation", message: new { conversation = "stuur dat net ook naar carleen" });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(d, "to") == Vincent && GetProperty(GetValue(d, "body"), "text") == "Er is nog geen eerder antwoord dat ik kan doorsturen."));
        _ctx.HaContext.DidNotReceive().CallService("whatsapp", "send_message", null, Arg.Is<object>(d => GetProperty(d, "to") == Carleen));
    }

    [Fact]
    public void VoiceNote_AlsoSendsASpokenReply()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        var mediaKey = new byte[32];
        _mediaClient.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BuildEncryptedVoiceNote(mediaKey));
        _assistPipeline.RunSpeechToIntentAsync("Gemini", Arg.Any<byte[]>(), 16000, Arg.Any<CancellationToken>())
            .Returns(new AssistPipelineResult { Success = true, Transcript = "hoe laat is het" });
        MockConversationResponse(GeminiAgentId, "Het is 14:00");
        _textToSpeech.GetSpeechUrlAsync("Het is 14:00", "nl-NL", Arg.Any<CancellationToken>()).Returns("https://ha.example/tts_proxy/abc.mp3");

        SendEvent(Vincent, fromMe: false, type: "audioMessage", message: new
        {
            audioMessage = new { url = "https://example.test/voice.enc", mimetype = "audio/ogg", mediaKey = Convert.ToBase64String(mediaKey), ptt = true, seconds = 1 }
        });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(d, "to") == Vincent &&
                                 GetValue(GetValue(d, "body"), "audio")!.Value.GetProperty("url").GetString() == "https://ha.example/tts_proxy/abc.mp3" &&
                                 GetValue(d, "body")!.Value.GetProperty("ptt").GetBoolean()));
    }

    [Fact]
    public void VoiceNote_WhenTextToSpeechFails_StillSendsTheTextReply()
    {
        CreateApp();
        _ctx.HaContextMock.ProcessPendingOperations();

        var mediaKey = new byte[32];
        _mediaClient.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BuildEncryptedVoiceNote(mediaKey));
        _assistPipeline.RunSpeechToIntentAsync("Gemini", Arg.Any<byte[]>(), 16000, Arg.Any<CancellationToken>())
            .Returns(new AssistPipelineResult { Success = true, Transcript = "hoe laat is het" });
        MockConversationResponse(GeminiAgentId, "Het is 14:00");
        _textToSpeech.GetSpeechUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new HttpRequestException("boom")));

        SendEvent(Vincent, fromMe: false, type: "audioMessage", message: new
        {
            audioMessage = new { url = "https://example.test/voice.enc", mimetype = "audio/ogg", mediaKey = Convert.ToBase64String(mediaKey), ptt = true, seconds = 1 }
        });
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("whatsapp", "send_message", null,
            Arg.Is<object>(d => GetProperty(GetValue(d, "body"), "text") == "Jij zei: \"hoe laat is het\"\n\nHet is 14:00"));
        _ctx.HaContext.DidNotReceive().CallService("whatsapp", "send_message", null, Arg.Is<object>(d => HasAudioBody(d)));
    }

    private static bool HasAudioBody(object d) => GetValue(d, "body")!.Value.TryGetProperty("audio", out _);

    /// <summary>Builds a short, real Ogg Opus voice note, encrypted the same way WhatsApp encrypts media.</summary>
    private static byte[] BuildEncryptedVoiceNote(byte[] mediaKey)
    {
        var pcm = OggOpusTestFile.Silence(sampleRate: 16000, durationSeconds: 0.1);
        var ogg = OggOpusTestFile.Encode(pcm, sampleRate: 16000);
        return WhatsAppMediaTestEncryptor.Encrypt(ogg, mediaKey, Automation.Helpers.WhatsAppMediaDecryptor.MediaKind.Audio);
    }

    private static string? GetProperty(object? obj, string name)
    {
        var json = JsonSerializer.SerializeToElement(obj);
        return json.TryGetProperty(name, out var value) ? value.GetString() : null;
    }

    private static JsonElement? GetValue(object? obj, string name)
    {
        var json = JsonSerializer.SerializeToElement(obj);
        return json.TryGetProperty(name, out var value) ? value : null;
    }
}
