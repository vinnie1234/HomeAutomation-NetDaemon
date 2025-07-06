using System.Text.Json.Serialization;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models.Twitter;

public class Meta
{
    [JsonPropertyName("result_count")]
    public int? ResultCount { get; set; }

    [JsonPropertyName("next_token")]
    public string? NextToken { get; set; }
}