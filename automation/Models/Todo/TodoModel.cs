using System.Text.Json.Serialization;

namespace Automation.Models.Todo;

public class TodoModel
{
    [JsonPropertyName("todo.dagelijks")]
    public required TodoDagelijks TodoDagelijks { get; init; }
}