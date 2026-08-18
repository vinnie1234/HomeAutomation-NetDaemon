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

/// <summary>
/// Configuration for the natural light rhythm (circadian lighting).
/// The sun elevation of <see cref="NightElevation"/> and below is treated as fully "night",
/// <see cref="DayElevation"/> and above as fully "day"; everything in between is interpolated.
/// </summary>
public class CircadianConfiguration
{
    /// <summary>Sun elevation (degrees) at which the warmest/dimmest values are used.</summary>
    public double NightElevation { get; set; } = -6;

    /// <summary>Sun elevation (degrees) at which the coolest/brightest values are used.</summary>
    public double DayElevation { get; set; } = 20;

    /// <summary>Warmest color temperature, used around and after sunset.</summary>
    public int MinColorTempKelvin { get; set; } = 2500;

    /// <summary>Coolest color temperature, used when the sun is high.</summary>
    public int MaxColorTempKelvin { get; set; } = 6000;

    /// <summary>Color temperatures are rounded to this step to avoid pointless updates.</summary>
    public int ColorTempStepKelvin { get; set; } = 50;

    /// <summary>Brightness used when the sun is at or below <see cref="NightElevation"/>.</summary>
    public int MinBrightnessPct { get; set; } = 50;

    /// <summary>Brightness used when the sun is at or above <see cref="DayElevation"/>.</summary>
    public int MaxBrightnessPct { get; set; } = 100;

    /// <summary>Brightness used when somebody is sleeping (night mode).</summary>
    public int NightModeBrightnessPct { get; set; } = 5;
}

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