namespace Automation.Models.Battery;

/// <summary>
/// Information about a battery-powered device.
/// </summary>
public record BatteryDeviceInfo
{
    public string EntityId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public int CurrentLevel { get; init; }
    public string BatteryType { get; init; } = string.Empty;
    public DateTime LastReplaced { get; init; }
}