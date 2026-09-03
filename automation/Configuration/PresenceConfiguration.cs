namespace Automation.Configuration;

/// <summary>
/// Configuration for the probabilistic room presence detection.
/// </summary>
public class PresenceConfiguration
{
    /// <summary>
    /// How long a room keeps counting as occupied after the last presence anchor
    /// (motion, TV, ...) dropped away.
    /// </summary>
    public TimeSpan LivingRoomGracePeriod { get; set; } = TimeSpan.FromMinutes(15);
}
