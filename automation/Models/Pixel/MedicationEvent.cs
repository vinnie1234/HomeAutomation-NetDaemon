using System.Text.Json.Serialization;

namespace Automation.Models.Pixel;

/// <summary>
/// One line in Pixel's medication history: a dose that was given, or a change of the stock (e.g. a new strip of pills).
/// These are kept forever.
/// </summary>
public record MedicationEvent
{
    /// <summary>When it happened (Dutch time).</summary>
    [JsonPropertyName("tijd")]
    public DateTimeOffset Time { get; init; }

    /// <summary>The medicine: <c>prednison</c> or <c>antibraak</c>.</summary>
    [JsonPropertyName("medicijn")]
    public string Medicine { get; init; } = string.Empty;

    /// <summary><c>gegeven</c> for a dose, <c>voorraad</c> for a change of the stock.</summary>
    [JsonPropertyName("soort")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>How much the stock changed, in pills (negative for a dose, e.g. -0.25).</summary>
    [JsonPropertyName("hoeveelheid")]
    public double Amount { get; init; }

    /// <summary>The stock afterwards, in pills.</summary>
    [JsonPropertyName("voorraad")]
    public double Stock { get; init; }
}
