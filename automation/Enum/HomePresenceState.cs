namespace Automation.Enum;

/// <summary>
/// Represents the different states of home presence for the AwayManager state machine.
/// This enum helps prevent race conditions by providing clear state transitions.
/// </summary>
public enum HomePresenceState
{
    /// <summary>
    /// User is away from home. Away automation is active.
    /// </summary>
    Away,

    /// <summary>
    /// User is returning home (away=false detected) but no motion detected yet.
    /// Waiting for motion sensor to trigger welcome home sequence.
    /// </summary>
    Returning,

    /// <summary>
    /// Welcome home sequence is currently executing.
    /// Prevents duplicate welcome home actions from multiple motion events.
    /// </summary>
    WelcomingHome,

    /// <summary>
    /// User is home and welcome sequence is completed.
    /// Normal home automation is active.
    /// </summary>
    Home
}