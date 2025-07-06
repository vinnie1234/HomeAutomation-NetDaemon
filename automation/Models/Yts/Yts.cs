using System.Text.Json.Serialization;

// ReSharper disable UnusedMember.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Automation.Models.Yts;

// ReSharper disable once ClassNeverInstantiated.Global
public class Yts
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;
    
    [JsonPropertyName("title_detail")]
    public TitleDetail TitleDetail { get; set; } = default!;
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = default!;
    
    [JsonPropertyName("summary_detail")]
    public SummaryDetail SummaryDetail { get; set; } = default!;
    
    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = default!;
    
    [JsonPropertyName("link")]
    public string Link { get; set; } = default!;
    
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;
    
    [JsonPropertyName("guidislink")]
    public bool GuidIsLink { get; set; } = default!;
    
    [JsonPropertyName("published")]
    public string Published { get; set; } = default!;
}