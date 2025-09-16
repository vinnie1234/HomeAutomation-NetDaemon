using System.Text.Json.Serialization;

namespace Automation.Models.Todo;

public class Item
{
    [JsonPropertyName("summary")]
    public required string Summary { get; set; }

    [JsonPropertyName("uid")]
    public required string Uid { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }
}