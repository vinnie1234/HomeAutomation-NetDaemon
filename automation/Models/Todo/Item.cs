using System.Text.Json.Serialization;

namespace Automation.Models.Todo;

public class Item
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }
}