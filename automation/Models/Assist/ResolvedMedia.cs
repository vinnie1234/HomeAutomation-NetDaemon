using System.Text.Json.Serialization;

namespace Automation.Models.Assist;

/// <summary>The result of a <see cref="ResolveMediaCommand"/>.</summary>
public record ResolvedMedia
{
    /// <summary>The URL the media can be downloaded from, relative to the Home Assistant base URL.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("mime_type")]
    public string MimeType { get; init; } = string.Empty;
}
