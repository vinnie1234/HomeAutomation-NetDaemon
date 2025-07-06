using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models.DiscordNotificationModels;

public class Location
{
    public Location(string url)
    {
        Url = url;
    }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}