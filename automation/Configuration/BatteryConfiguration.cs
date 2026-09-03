namespace Automation.Configuration;

/// <summary>
/// Configuration for battery monitoring.
/// </summary>
public class BatteryConfiguration
{
    public int WarningLevel { get; set; } = 20;
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(10);
}
