namespace Automation.Configuration;

/// <summary>
/// Central configuration for automation apps.
/// </summary>
public class AppConfiguration
{
    public BatteryConfiguration Battery { get; set; } = new();
    public LightConfiguration Lights { get; set; } = new();
    public TimingConfiguration Timing { get; set; } = new();
}

/// <summary>
/// Configuration for battery monitoring.
/// </summary>
public class BatteryConfiguration
{
    public int WarningLevel { get; set; } = 20;
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(10);
}

/// <summary>
/// Configuration for light management.
/// </summary>
public class LightConfiguration
{
    public Dictionary<string, string> DeviceIds { get; set; } = new()
    {
        ["HueWallLivingRoom"] = "b4784a8e43cc6f5aabfb6895f3a8dbac"
    };
    
    public int DefaultTransitionSeconds { get; set; } = 5;
    public TimeSpan StateChangeThrottleMs { get; set; } = TimeSpan.FromMilliseconds(50);
    public TimeSpan DelayBetweenLights { get; set; } = TimeSpan.FromMilliseconds(200);
}

/// <summary>
/// Configuration for timing delays used throughout the application.
/// </summary>
public class TimingConfiguration
{
    public TimeSpan WelcomeHomeDelay { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan NewYearMusicDelay { get; set; } = TimeSpan.FromSeconds(49);
    public TimeSpan ShortDelay { get; set; } = TimeSpan.FromSeconds(0.5);
}