using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Automation.Models;

public class RecordNotifyData
{
    public RecordNotifyData(string? priority, int? ttl, string? tag, string? color, string? sticky)
    {
        Priority = priority;
        Ttl = ttl;
        Tag = tag;
        Color = color;
        Sticky = sticky;
    }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("sticky")]
    public string? Sticky { get; set; }

    [JsonPropertyName("tts_text")]
    public string? TtsText { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("vibrationPattern")]
    public string? VibrationPattern { get; set; }

    [JsonPropertyName("ledColor")]
    public string? LedColor { get; set; }

    [JsonPropertyName("actions")]
    public List<ActionModel>? Actions { get; set; }
}