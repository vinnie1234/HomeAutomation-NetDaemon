using System.Text.Json.Serialization;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedMember.Global

namespace Automation.Models.Yts;

// ReSharper disable once ClassNeverInstantiated.Global
public class Link
{
    [JsonPropertyName("rel")]
    public string Rel { get; set; } = default!;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;
    
    [JsonPropertyName("href")]
    public string Href { get; set; } = default!;
    
    [JsonPropertyName("length")]
    public string Length { get; set; } = default!;
}