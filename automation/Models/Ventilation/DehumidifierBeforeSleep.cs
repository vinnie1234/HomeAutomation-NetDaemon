namespace Automation.Models.Ventilation;

/// <summary>
/// What the dehumidifier was set to before the night, to give back in the morning (the sleep mode of the device sets its own target
/// humidity). An empty mode means the app did not change anything.
/// </summary>
public sealed record DehumidifierBeforeSleep
{
    /// <summary>Gets the mode before the night (empty: nothing was changed).</summary>
    public string Mode { get; init; } = "";

    /// <summary>Gets the target humidity before the night in %, or null when it was not known.</summary>
    public double? Humidity { get; init; }
}
