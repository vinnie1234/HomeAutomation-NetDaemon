using System.Text.Json.Serialization;

namespace Automation.Models.WhatsApp;

public record WhatsAppMessageContent
{
    /// <summary>The text of a plain text message.</summary>
    [JsonPropertyName("conversation")]
    public string? Conversation { get; init; }

    [JsonPropertyName("audioMessage")]
    public WhatsAppAudioMessage? AudioMessage { get; init; }
}
