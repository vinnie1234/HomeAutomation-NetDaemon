using System.Text.Json.Serialization;

namespace Automation.Models.Todo;

public class TodoDagelijks
{
    [JsonPropertyName("items")]
    public List<Item>? Items { get; set; }
}