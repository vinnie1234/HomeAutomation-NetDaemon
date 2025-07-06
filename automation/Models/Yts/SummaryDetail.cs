using System.Text.Json.Serialization;

// ReSharper disable UnusedMember.Global

namespace Automation.Models.Yts;

// ReSharper disable once ClassNeverInstantiated.Global
public class SummaryDetail
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;
    
    [JsonPropertyName("language")]
    public object Language { get; set; } = default!;
    
    [JsonPropertyName("base")]
    public string Base { get; set; } = default!;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = default!;
}