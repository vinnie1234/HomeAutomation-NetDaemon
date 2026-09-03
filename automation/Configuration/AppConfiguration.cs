namespace Automation.Configuration;

/// <summary>
/// Central configuration for automation apps.
/// </summary>
public class AppConfiguration
{
    public BatteryConfiguration Battery { get; set; } = new();
    public LightConfiguration Lights { get; set; } = new();
    public TimingConfiguration Timing { get; set; } = new();
    public CircadianConfiguration Circadian { get; set; } = new();
    public PresenceConfiguration Presence { get; set; } = new();
}
