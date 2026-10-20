using System.Text.Json.Serialization;

namespace Automation.Models.WhatsApp;

/// <summary>
/// A WhatsApp voice note. The file at <see cref="Url"/> is encrypted; <see cref="MediaKey"/> is needed to decrypt it.
/// </summary>
public record WhatsAppAudioMessage
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("mimetype")]
    public string Mimetype { get; init; } = string.Empty;

    /// <summary>Base64-encoded key used to decrypt the file at <see cref="Url"/>.</summary>
    [JsonPropertyName("mediaKey")]
    public string MediaKey { get; init; } = string.Empty;

    /// <summary>Whether this is a push-to-talk voice note, as opposed to a shared audio file.</summary>
    [JsonPropertyName("ptt")]
    public bool Ptt { get; init; }

    [JsonPropertyName("seconds")]
    public int Seconds { get; init; }
}
