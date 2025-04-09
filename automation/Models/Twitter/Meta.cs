using System.Text.Json.Serialization;

namespace Automation.Models.Twitter;

public class Meta
{
    [JsonPropertyName("result_count")]
    public int? ResultCount { get; set; }

    [JsonPropertyName("next_token")]
    public string? NextToken { get; set; } // Als er meerdere pagina's zijn
}