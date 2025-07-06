using System.Text.Json.Serialization;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models.Twitter;

public class TweetResponse
{
    [JsonPropertyName("data")]
    public List<Tweet>? Data { get; set; }

    [JsonPropertyName("meta")]
    public Meta? Meta { get; set; }
}