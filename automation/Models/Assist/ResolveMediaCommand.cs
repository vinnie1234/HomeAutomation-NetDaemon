using System.Text.Json.Serialization;
using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Assist;

/// <summary>
/// Websocket command that resolves a <c>media-source://</c> content ID (e.g. a TTS phrase) to a playable URL.
/// </summary>
public record ResolveMediaCommand : CommandMessage
{
    public ResolveMediaCommand() => Type = "media_source/resolve_media";

    [JsonPropertyName("media_content_id")]
    public string MediaContentId { get; init; } = string.Empty;
}
