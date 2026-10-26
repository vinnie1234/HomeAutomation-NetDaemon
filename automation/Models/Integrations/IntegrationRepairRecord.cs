namespace Automation.Models.Integrations;

/// <summary>
/// The stored reload history of a failing integration. It is removed as soon as the integration loads again,
/// so a later failure gets the full number of reload attempts again.
/// </summary>
public record IntegrationRepairRecord
{
    public string EntryId { get; init; } = string.Empty;
    public string Domain {  get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    /// <summary>How often the integration has been reloaded since it started failing.</summary>
    public int ReloadAttempts { get; init; }

    /// <summary>When the integration was last reloaded (UTC).</summary>
    public DateTimeOffset LastReloadAttempt { get; init; }

    /// <summary>Whether Discord has been told that reloading did not help.</summary>
    public bool Notified { get; init; }
}
