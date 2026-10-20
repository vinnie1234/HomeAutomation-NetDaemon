using System.Text.Json.Serialization;

namespace Automation.Models.WhatsApp;

public record WhatsAppMessageKey
{
    /// <summary>The chat the message belongs to; for a 1:1 chat this is the other party, which is who a reply goes to.</summary>
    [JsonPropertyName("remoteJid")]
    public string RemoteJid { get; init; } = string.Empty;

    [JsonPropertyName("fromMe")]
    public bool FromMe { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}
