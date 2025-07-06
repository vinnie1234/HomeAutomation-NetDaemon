using System.Text.Json.Serialization;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable once ClassNeverInstantiated.Global

namespace Automation.Models;

public class AlarmStateModel
{
    [JsonPropertyName("alarm_id")]
    public string? AlarmId { get; set; }

    [JsonPropertyName("fire_time")]
    public int? FireTime { get; set; }

    [JsonPropertyName("local_time")]
    public string? LocalTime { get; set; }

    [JsonPropertyName("local_time_iso")]
    public DateTime? LocalTimeIso { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("label")]
    public object? Label { get; set; }

    [JsonPropertyName("recurrence")]
    public List<int>? RecurrenceDays { get; set; }

    [JsonPropertyName(nameof(EntityId))]
    public string? EntityId { get; set; }
}