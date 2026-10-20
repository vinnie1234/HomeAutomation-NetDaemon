using System.Text.Json.Serialization;

namespace Automation.Models.WhatsApp;

/// <summary>
/// The data of a Home Assistant <c>new_whatsapp_message</c> event.
/// </summary>
public record WhatsAppMessageEvent
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>The message kind, e.g. <c>conversation</c> for text or <c>audioMessage</c> for a voice note.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("key")]
    public WhatsAppMessageKey Key { get; init; } = new();

    [JsonPropertyName("pushName")]
    public string? PushName { get; init; }

    [JsonPropertyName("message")]
    public WhatsAppMessageContent Message { get; init; } = new();
}
