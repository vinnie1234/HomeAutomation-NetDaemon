using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models.DiscordNotificationModels;

// ReSharper disable once ClassNeverInstantiated.Global
public class Footer
{
    public Footer(string text, Uri iconUrl)
    {
        Text = text;
        IconUrl = iconUrl;
    }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("icon_url")]
    public Uri IconUrl { get; set; }
}