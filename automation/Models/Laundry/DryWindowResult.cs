namespace Automation.Models.Laundry;

/// <summary>
/// Result of the dry window calculation.
/// </summary>
/// <param name="IsDryNow">True when there are at least 3 consecutive dry hours starting now.</param>
/// <param name="DryUntil">The first moment rain is expected, or null when it stays dry all day.</param>
/// <param name="DryUntilDescription">Human-readable description of how long it stays dry.</param>
/// <param name="NextWindowTime">Start of the next dry window between 09:00–19:00, if any.</param>
public record DryWindowResult(
    bool IsDryNow,
    DateTime? DryUntil,
    string? DryUntilDescription,
    DateTime? NextWindowTime);
