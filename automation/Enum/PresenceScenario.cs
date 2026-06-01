namespace Automation.Enum;

/// <summary>
/// Represents the combined presence of Vincent and Carleen.
/// Used by the <see cref="Automation.apps.General.PresenceManager"/> to decide which
/// actions to execute when presence changes.
/// </summary>
public enum PresenceScenario
{
    /// <summary>
    /// Both Vincent and Carleen are home.
    /// </summary>
    BothHome,

    /// <summary>
    /// Vincent is away, Carleen is still home.
    /// </summary>
    VincentAwayOnly,

    /// <summary>
    /// Carleen is away, Vincent is still home.
    /// </summary>
    CarleenAwayOnly,

    /// <summary>
    /// Both Vincent and Carleen are away. This drives the house-wide "away" boolean.
    /// </summary>
    BothAway
}
