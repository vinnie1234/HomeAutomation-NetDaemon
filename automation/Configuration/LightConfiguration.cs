namespace Automation.Configuration;

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
