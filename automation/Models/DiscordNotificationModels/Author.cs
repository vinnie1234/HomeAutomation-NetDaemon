using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models.DiscordNotificationModels;

// ReSharper disable once ClassNeverInstantiated.Global
public class Author
{
    public Author(string name, Uri url, Uri iconUrl)
    {
        Name = name;
        Url = url;
        IconUrl = iconUrl;
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("url")]
    public Uri Url { get; set; }

    [JsonPropertyName("icon_url")]
    public Uri IconUrl { get; set; }
}