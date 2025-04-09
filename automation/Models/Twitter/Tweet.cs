using System.Text.Json.Serialization;

namespace Automation.Models.Twitter;

public class Tweet
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}