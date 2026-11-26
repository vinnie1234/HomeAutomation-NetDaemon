namespace Automation.Models.Ventilation;

/// <summary>
/// How long the windows were open on one day (Dutch time), as the windows app keeps it.
/// </summary>
public sealed record WindowDay
{
    /// <summary>Gets the day, as yyyy-MM-dd.</summary>
    public string Date { get; init; } = "";

    /// <summary>Gets the minutes a window was open that day, per window (the key of the window).</summary>
    public Dictionary<string, double> Minutes { get; init; } = [];

    /// <summary>Gets the longest single opening that ended that day, in minutes.</summary>
    public double Longest { get; init; }
}
