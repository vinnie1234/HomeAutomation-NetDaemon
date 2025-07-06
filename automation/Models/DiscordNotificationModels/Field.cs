using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models.DiscordNotificationModels;

public class Field
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("value")]
    public string Value { get; set; } = default!;

    [JsonPropertyName("inline")]
    // ReSharper disable once UnusedMember.Global
    public bool? Inline { get; set; }
}