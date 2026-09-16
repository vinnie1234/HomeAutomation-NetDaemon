using System.Text.Json.Serialization;

namespace Automation.Models.Laundry;

/// <summary>
/// Represents a single entry in the hourly weather forecast.
/// </summary>
public record ForecastEntry
{
    [JsonPropertyName("datetime")]
    public DateTime DateTime { get; init; }

    [JsonPropertyName("condition")]
    public string? Condition { get; init; }

    [JsonPropertyName("precipitation")]
    public double? Precipitation { get; init; }

    [JsonPropertyName("precipitation_probability")]
    public double? PrecipitationProbability { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }
}