using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models;

public record ActionModel
{
    [JsonPropertyName("action")]
    // ReSharper disable once PropertyCanBeMadeInitOnly.Global
    public string Action { get; set; } = default!;
    
    [JsonPropertyName("title")]
    public string Title { get; } = default!;
    
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonIgnore]
    public Action? Func;

    public ActionModel(string action, string title, string? uri = null, Action? func = null)
    {
        Action = action;
        Title = title;
        Uri = uri;
        Func = func;
    }
}