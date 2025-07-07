namespace Automation.Models.Battery;

/// <summary>
/// Result of battery trend analysis.
/// </summary>
public record BatteryTrendResult
{
    public BatteryDeviceInfo Device { get; init; } = new();
    public double DailyDischargeRate { get; init; }
    public int DaysRemaining { get; init; }
    public int AgeInDays { get; init; }
    public string HealthStatus { get; init; } = string.Empty;
    public DateTime ExpectedReplacementDate { get; init; }
    public int ExpectedLifeDays { get; init; }
    public double PerformanceRatio { get; init; }
}