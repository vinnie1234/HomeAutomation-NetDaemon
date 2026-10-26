using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automation.Models.Helpers;

/// <summary>
/// A helper as Home Assistant returns it from <c>{domain}/list</c> and <c>{domain}/create</c>.
/// </summary>
public record HelperItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>The options of an input_select; null for other helpers.</summary>
    [JsonPropertyName("options")]
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>The other settings as Home Assistant stores them (<c>icon</c>, <c>initial</c>, <c>min</c>, ...).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? OtherSettings { get; init; }
}
