using System.Text.Json.Serialization;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models.DiscordNotificationModels;

public class Embed
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("color")]
    public long? Color { get; set; }

    [JsonPropertyName("author")]
    public Author? Author { get; set; }

    [JsonPropertyName("footer")]
    public Footer? Footer { get; set; }

    [JsonPropertyName("thumbnail")]
    public Location? Thumbnail { get; set; }

    [JsonPropertyName("image")]
    public Location? Image { get; set; }

    [JsonPropertyName("fields")]
    public Field[]? Fields { get; set; }
}