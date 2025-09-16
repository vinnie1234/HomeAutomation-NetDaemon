using System.Text.Json.Serialization;

namespace Automation.Models.Todo;

public class TodoModel
{
    [JsonPropertyName("todo.dagelijks")]
    public TodoDagelijks TodoDagelijks { get; set; }
}