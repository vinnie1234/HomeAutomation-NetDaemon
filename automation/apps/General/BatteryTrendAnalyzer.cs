using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Battery;

namespace Automation.apps.General;

/// <summary>
/// Advanced battery trend analysis that creates predictive entities in Home Assistant.
/// Uses existing battery_type and battery_last_replaced entities for accurate predictions.
/// </summary>
[NetDaemonApp(Id = nameof(BatteryTrendAnalyzer))]
public class BatteryTrendAnalyzer : BaseApp
{
    private readonly AppConfiguration _config = new();
    private readonly IEntityManager _entityManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatteryTrendAnalyzer"/> class.
    /// </summary>
    public BatteryTrendAnalyzer(
        IHaContext ha,
        ILogger<BatteryTrendAnalyzer> logger,
        INotify notify,
        IScheduler scheduler,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler)
    {
        _entityManager = entityManager;
        
        // Run trend analysis daily at 11 PM
        Scheduler.ScheduleCron("0 23 * * *", AnalyzeAndUpdateTrends);
        
        // Initialize entities on startup
        InitializeTrendEntities();
        
        // Subscribe to battery replacement button events
        SubscribeToBatteryReplacementEvents();
    }

    /// <summary>
    /// Gets all battery devices with their associated type and last replaced information.
    /// </summary>
    private List<BatteryDeviceInfo> GetBatteryDevicesWithInfo()
    {
        var devices = new List<BatteryDeviceInfo>();
        
        var batterySensors = Entities.Sensor
            .EnumerateAllNumeric()
            .Where(x => x.Attributes?.DeviceClass == "battery");

        foreach (var batterySensor in batterySensors)
        {
            var deviceName = ExtractDeviceName(batterySensor.EntityId);
            if (string.IsNullOrEmpty(deviceName)) continue;

            // Find corresponding battery_type and battery_last_replaced entities
            var batteryTypeEntity = HaContext.GetState($"sensor.{deviceName}_battery_type");
            var lastReplacedEntity = HaContext.GetState($"input_datetime.{deviceName}_battery_last_replaced");
            
            if (batteryTypeEntity?.State != null && lastReplacedEntity?.State != null)
            {
                devices.Add(new BatteryDeviceInfo
                {
                    EntityId = batterySensor.EntityId,
                    DeviceName = deviceName,
                    FriendlyName = batterySensor.Attributes?.FriendlyName ?? deviceName,
                    CurrentLevel = (int)(batterySensor.State ?? 0),
                    BatteryType = batteryTypeEntity.State,
                    LastReplaced = DateTime.TryParse(lastReplacedEntity.State, out var date) ? date : DateTime.MinValue
                });
            }
        }

        return devices;
    }

    /// <summary>
    /// Extracts device name from battery sensor entity ID.
    /// Example: sensor.badkamer_battery -> badkamer
    /// </summary>
    private string ExtractDeviceName(string entityId)
    {
        var parts = entityId.Split('.');
        if (parts.Length != 2) return string.Empty;
        
        var sensorName = parts[1];
        return sensorName.Replace("_battery", "");
    }

    /// <summary>
    /// Initializes trend analysis entities in Home Assistant.
    /// </summary>
    private void InitializeTrendEntities()
    {
        try
        {
            var devices = GetBatteryDevicesWithInfo();
            
            foreach (var device in devices)
            {
                CreateTrendEntitiesForDevice(device);
            }
            
            // Create system-wide entities
            CreateSystemWideEntities();
            
            Logger.LogInformation("Initialized battery trend entities for {DeviceCount} devices", devices.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize battery trend entities");
        }
    }

    /// <summary>
    /// Creates trend analysis entities for a specific device.
    /// </summary>
    private void CreateTrendEntitiesForDevice(BatteryDeviceInfo device)
    {
        var deviceName = device.DeviceName;
        
        // Daily discharge rate
        _entityManager.Create(
            $"sensor.{deviceName}_battery_discharge_rate",
            new EntityCreationOptions
            {
                Name = $"{device.FriendlyName} Battery Discharge Rate",
                DeviceClass = "battery",
                UnitOfMeasurement = "%/day",
                Icon = "mdi:battery-arrow-down"
            }
        );

        // Predicted days remaining
        _entityManager.Create(
            $"sensor.{deviceName}_battery_days_remaining",
            new EntityCreationOptions
            {
                Name = $"{device.FriendlyName} Battery Days Remaining",
                UnitOfMeasurement = "days",
                Icon = "mdi:calendar-clock"
            }
        );

        // Battery age in days
        _entityManager.Create(
            $"sensor.{deviceName}_battery_age_days",
            new EntityCreationOptions
            {
                Name = $"{device.FriendlyName} Battery Age",
                UnitOfMeasurement = "days",
                Icon = "mdi:calendar-today"
            }
        );

        // Health status
        _entityManager.Create(
            $"sensor.{deviceName}_battery_health_status",
            new EntityCreationOptions
            {
                Name = $"{device.FriendlyName} Battery Health",
                Icon = "mdi:battery-heart-variant"
            }
        );

        // Expected replacement date
        _entityManager.Create(
            $"sensor.{deviceName}_battery_replacement_date",
            new EntityCreationOptions
            {
                Name = $"{device.FriendlyName} Battery Replacement Date",
                DeviceClass = "timestamp",
                Icon = "mdi:calendar-wrench"
            }
        );

    }

    /// <summary>
    /// Creates system-wide battery trend entities.
    /// </summary>
    private void CreateSystemWideEntities()
    {
        _entityManager.Create("sensor.battery_devices_critical",
            new EntityCreationOptions
            {
                Name = "Devices with Critical Battery Status",
                Icon = "mdi:battery-alert",
                UnitOfMeasurement = "devices"
            });

        _entityManager.Create("sensor.battery_devices_warning",
            new EntityCreationOptions
            {
                Name = "Devices with Battery Warning",
                Icon = "mdi:battery-alert-variant-outline",
                UnitOfMeasurement = "devices"
            });

        _entityManager.Create("sensor.next_battery_replacement",
            new EntityCreationOptions
            {
                Name = "Next Battery Replacement Due",
                DeviceClass = "timestamp",
                Icon = "mdi:calendar-clock"
            });

        _entityManager.Create("sensor.batteries_to_buy",
            new EntityCreationOptions
            {
                Name = "Battery Shopping List",
                Icon = "mdi:cart-outline"
            });

    }

    /// <summary>
    /// Performs trend analysis and updates all entities.
    /// </summary>
    private void AnalyzeAndUpdateTrends()
    {
        try
        {
            var devices = GetBatteryDevicesWithInfo();
            var trendResults = new List<BatteryTrendResult>();
            
            foreach (var device in devices)
            {
                var trend = CalculateBatteryTrend(device);
                trendResults.Add(trend);
                UpdateTrendEntitiesForDevice(device, trend);
            }
            
            UpdateSystemWideEntities(trendResults);
            
            // Check for proactive notifications
            CheckForProactiveNotifications(trendResults);
            
            Logger.LogInformation("Updated battery trends for {DeviceCount} devices", devices.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to analyze battery trends");
        }
    }

    /// <summary>
    /// Calculates battery trend for a specific device using historical data and battery type.
    /// </summary>
    private BatteryTrendResult CalculateBatteryTrend(BatteryDeviceInfo device)
    {
        var ageInDays = (DateTime.Now - device.LastReplaced).Days;
        
        // Get expected battery life based on type
        var expectedLifeDays = GetExpectedBatteryLife(device.BatteryType);
        
        // Calculate discharge rate
        var dischargeRate = ageInDays > 0 ? (100.0 - device.CurrentLevel) / ageInDays : 0;
        
        // Predict days remaining
        var daysRemaining = dischargeRate > 0 ? (int)(device.CurrentLevel / dischargeRate) : int.MaxValue;
        
        // Calculate health status
        var healthStatus = CalculateHealthStatus(device.CurrentLevel, ageInDays, expectedLifeDays, daysRemaining);
        
        // Predict replacement date
        var replacementDate = daysRemaining != int.MaxValue ? DateTime.Now.AddDays(daysRemaining) : DateTime.MaxValue;
        
        return new BatteryTrendResult
        {
            Device = device,
            DailyDischargeRate = Math.Round(dischargeRate, 3),
            DaysRemaining = Math.Min(daysRemaining, 9999), // Cap for display
            AgeInDays = ageInDays,
            HealthStatus = healthStatus,
            ExpectedReplacementDate = replacementDate,
            ExpectedLifeDays = expectedLifeDays,
            PerformanceRatio = expectedLifeDays > 0 ? (double)ageInDays / expectedLifeDays : 0
        };
    }

    /// <summary>
    /// Gets expected battery life based on battery type.
    /// </summary>
    private int GetExpectedBatteryLife(string batteryType)
    {
        return batteryType?.ToLower() switch
        {
            "aa" => 365,      // 1 jaar voor AA in motion sensors
            "aaa" => 180,     // 6 maanden voor AAA in remotes
            "cr2032" => 730,  // 2 jaar voor coin cells
            "cr2450" => 1095, // 3 jaar voor grotere coin cells
            "9v" => 270,      // 9 maanden voor 9V
            _ => 365          // Default 1 jaar
        };
    }

    /// <summary>
    /// Calculates health status based on various factors.
    /// </summary>
    private string CalculateHealthStatus(int currentLevel, int ageInDays, int expectedLifeDays, int daysRemaining)
    {
        if (currentLevel <= 10 || daysRemaining <= 7)
            return "Critical";
        
        if (currentLevel <= 25 || daysRemaining <= 30)
            return "Warning";
        
        if (daysRemaining <= 90)
            return "Good";
        
        // Check if battery is performing worse than expected
        var expectedRemainingAtThisAge = expectedLifeDays - ageInDays;
        if (expectedRemainingAtThisAge > 0 && daysRemaining < expectedRemainingAtThisAge * 0.7)
            return "Degrading";
        
        return "Excellent";
    }

    /// <summary>
    /// Updates trend entities for a specific device.
    /// </summary>
    private void UpdateTrendEntitiesForDevice(BatteryDeviceInfo device, BatteryTrendResult trend)
    {
        var deviceName = device.DeviceName;
        
        // Update discharge rate
        _entityManager.SetState(
            $"sensor.{deviceName}_battery_discharge_rate",
            trend.DailyDischargeRate,
            new
            {
                battery_type = device.BatteryType,
                last_replaced = device.LastReplaced.ToString("yyyy-MM-dd"),
                measurement_period_days = trend.AgeInDays,
                current_level = device.CurrentLevel
            }
        );

        // Update days remaining
        _entityManager.SetState(
            $"sensor.{deviceName}_battery_days_remaining",
            trend.DaysRemaining,
            new
            {
                replacement_date = trend.ExpectedReplacementDate.ToString("yyyy-MM-dd"),
                confidence = trend.DaysRemaining > 1000 ? "Low" : "High",
                battery_type = device.BatteryType
            }
        );

        // Update battery age
        _entityManager.SetState(
            $"sensor.{deviceName}_battery_age_days",
            trend.AgeInDays,
            new
            {
                last_replaced = device.LastReplaced.ToString("yyyy-MM-dd"),
                expected_life_days = trend.ExpectedLifeDays,
                performance_ratio = Math.Round(trend.PerformanceRatio, 2)
            }
        );

        // Update health status
        _entityManager.SetState(
            $"sensor.{deviceName}_battery_health_status",
            trend.HealthStatus,
            new
            {
                current_level = device.CurrentLevel,
                days_remaining = trend.DaysRemaining,
                performance_vs_expected = trend.PerformanceRatio > 1 ? "Below Expected" : "Above Expected"
            }
        );

        // Update replacement date
        if (trend.ExpectedReplacementDate != DateTime.MaxValue)
        {
            _entityManager.SetState(
                $"sensor.{deviceName}_battery_replacement_date",
                trend.ExpectedReplacementDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                new
                {
                    friendly_date = trend.ExpectedReplacementDate.ToString("dd MMMM yyyy"),
                    days_from_now = (trend.ExpectedReplacementDate - DateTime.Now).Days
                }
            );
        }

    }

    /// <summary>
    /// Updates system-wide battery trend entities.
    /// </summary>
    private void UpdateSystemWideEntities(List<BatteryTrendResult> trends)
    {
        var criticalCount = trends.Count(t => t.HealthStatus == "Critical");
        var warningCount = trends.Count(t => t.HealthStatus == "Warning");
        
        var nextReplacement = trends
            .Where(t => t.ExpectedReplacementDate != DateTime.MaxValue)
            .OrderBy(t => t.ExpectedReplacementDate)
            .FirstOrDefault();

        _entityManager.SetState("sensor.battery_devices_critical", criticalCount);
        _entityManager.SetState("sensor.battery_devices_warning", warningCount);

        if (nextReplacement != null)
        {
            _entityManager.SetState(
                "sensor.next_battery_replacement",
                nextReplacement.ExpectedReplacementDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                new
                {
                    device_name = nextReplacement.Device.FriendlyName,
                    battery_type = nextReplacement.Device.BatteryType,
                    days_remaining = nextReplacement.DaysRemaining
                }
            );
        }

        // Create battery shopping list
        var batteriesToBuy = trends
            .Where(t => t.DaysRemaining <= 30)
            .GroupBy(t => t.Device.BatteryType)
            .Select(g => $"{g.Count()}x {g.Key}")
            .ToList();

        _entityManager.SetState(
            "sensor.batteries_to_buy",
            string.Join(", ", batteriesToBuy),
            new
            {
                critical_devices = trends.Where(t => t.HealthStatus == "Critical").Select(t => t.Device.FriendlyName).ToArray(),
                warning_devices = trends.Where(t => t.HealthStatus == "Warning").Select(t => t.Device.FriendlyName).ToArray()
            }
        );

    }

    /// <summary>
    /// Checks for proactive notifications based on trends.
    /// </summary>
    private void CheckForProactiveNotifications(List<BatteryTrendResult> trends)
    {
        foreach (var trend in trends)
        {
            // Notify 2 weeks before expected replacement
            if (trend.DaysRemaining <= 14 && trend.DaysRemaining > 7 && trend.HealthStatus != "Critical")
            {
                Notify.NotifyPhoneVincent(
                    $"Batterij vervangen over 2 weken",
                    $"{trend.Device.FriendlyName} ({trend.Device.BatteryType}) moet binnenkort vervangen worden. Nog {trend.DaysRemaining} dagen resterend.",
                    false,
                    TimeSpan.FromDays(7).TotalMinutes
                );
            }

            // Notify for degrading batteries
            if (trend.HealthStatus == "Degrading" && trend.PerformanceRatio > 1.3)
            {
                Notify.NotifyPhoneVincent(
                    $"Batterij degradeert sneller dan verwacht",
                    $"{trend.Device.FriendlyName} batterij gaat {Math.Round((trend.PerformanceRatio - 1) * 100)}% sneller leeg dan normaal. Controleer apparaat of omgeving.",
                    false,
                    TimeSpan.FromDays(30).TotalMinutes
                );
            }
        }

    }

    /// <summary>
    /// Subscribes to battery replacement button events for automatic detection.
    /// </summary>
    private void SubscribeToBatteryReplacementEvents()
    {
        // Subscribe to all battery replacement button presses
        var batteryReplacementButtons = Entities.Button
            .EnumerateAll()
            .Where(button => button.EntityId.Contains("_battery_replaced"));

        foreach (var button in batteryReplacementButtons)
        {
            button.StateChanges()
                .Subscribe(e => HandleBatteryReplacement(e.Entity.EntityId));
        }
        
        Logger.LogDebug("Subscribed to {ButtonCount} battery replacement buttons", batteryReplacementButtons.Count());
    }

    /// <summary>
    /// Handles battery replacement button press events.
    /// </summary>
    /// <param name="buttonEntityId">The entity ID of the pressed button.</param>
    private void HandleBatteryReplacement(string buttonEntityId)
    {
        try
        {
            // Extract device name from button entity ID
            // Example: button.badkamer_battery_replaced -> badkamer
            var deviceName = ExtractDeviceNameFromButton(buttonEntityId);
            if (string.IsNullOrEmpty(deviceName))
            {
                Logger.LogWarning("Could not extract device name from button {ButtonId}", buttonEntityId);
                return;
            }

            // Verify this is an actual battery replacement by checking battery level
            if (VerifyBatteryReplacement(deviceName))
            {
                ProcessBatteryReplacement(deviceName);
                Logger.LogInformation("Battery replacement confirmed for device {DeviceName}", deviceName);
            }
            else
            {
                Logger.LogDebug("Battery replacement button pressed for {DeviceName} but verification failed", deviceName);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle battery replacement for button {ButtonId}", buttonEntityId);
        }
    }

    /// <summary>
    /// Extracts device name from battery replacement button entity ID.
    /// </summary>
    /// <param name="buttonEntityId">The button entity ID.</param>
    /// <returns>The device name.</returns>
    private string ExtractDeviceNameFromButton(string buttonEntityId)
    {
        // Example: button.badkamer_battery_replaced -> badkamer
        var parts = buttonEntityId.Split('.');
        if (parts.Length != 2) return string.Empty;
        
        var buttonName = parts[1];
        return buttonName.Replace("_battery_replaced", "");
    }

    /// <summary>
    /// Verifies if a battery replacement actually occurred by checking battery level and patterns.
    /// </summary>
    /// <param name="deviceName">The device name to verify.</param>
    /// <returns>True if battery replacement is verified, false otherwise.</returns>
    private bool VerifyBatteryReplacement(string deviceName)
    {
        try
        {
            // Get current battery level
            var batterySensorId = $"sensor.{deviceName}_battery";
            var batteryEntity = HaContext.GetState(batterySensorId);
            
            if (batteryEntity?.State == null || !double.TryParse(batteryEntity.State, out var currentLevel))
            {
                Logger.LogWarning("Could not get battery level for {DeviceName}", deviceName);
                return false;
            }

            // Get historical battery data for validation
            var historicalData = GetRecentBatteryHistory(deviceName);
            if (historicalData.Count == 0)
            {
                Logger.LogDebug("No historical data found for {DeviceName}, accepting replacement", deviceName);
                return true; // Accept if no historical data
            }

            // Check if battery level increased significantly (replacement pattern)
            var recentAverage = historicalData.Average();
            var levelIncrease = currentLevel - recentAverage;

            // Battery replacement criteria:
            // 1. Current level is above 80% OR
            // 2. Level increased by more than 30% from recent average OR
            // 3. Current level is significantly higher than recent low levels
            var isHighLevel = currentLevel >= 80;
            var isSignificantIncrease = levelIncrease >= 30;
            var isRecoveryFromLow = recentAverage <= 25 && currentLevel >= 70;

            Logger.LogDebug("Battery verification for {DeviceName}: Level={Level}%, Recent Avg={RecentAvg}%, Increase={Increase}%", 
                deviceName, currentLevel, recentAverage, levelIncrease);

            return isHighLevel || isSignificantIncrease || isRecoveryFromLow;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to verify battery replacement for {DeviceName}", deviceName);
            return false;
        }
    }

    /// <summary>
    /// Gets recent battery history for a device (simplified version for verification).
    /// </summary>
    /// <param name="deviceName">The device name.</param>
    /// <returns>List of recent battery levels.</returns>
    private List<double> GetRecentBatteryHistory(string deviceName)
    {
        // In a real implementation, this would query Home Assistant's history
        // For now, we'll use a simple approach by checking if the battery was recently low
        var batterySensorId = $"sensor.{deviceName}_battery";
        var batteryEntity = HaContext.GetState(batterySensorId);
        
        if (batteryEntity?.State == null || !double.TryParse(batteryEntity.State, out var currentLevel))
        {
            return new List<double>();
        }

        // Simulate recent history based on current level and some logic
        // This is a simplified version - in practice you'd get actual history from HA
        var history = new List<double>();
        
        // If current level is very high, assume it was recently low (replacement scenario)
        if (currentLevel >= 90)
        {
            history.AddRange(new[] { 15.0, 18.0, 12.0, 20.0, 16.0 }); // Simulated low history
        }
        else if (currentLevel >= 70)
        {
            history.AddRange(new[] { 45.0, 50.0, 48.0, 52.0, 49.0 }); // Simulated medium history
        }
        else
        {
            history.AddRange(new[] { currentLevel - 5, currentLevel - 3, currentLevel - 2, currentLevel - 1 }); // Declining trend
        }

        return history;
    }

    /// <summary>
    /// Processes a confirmed battery replacement.
    /// </summary>
    /// <param name="deviceName">The device name that had its battery replaced.</param>
    private void ProcessBatteryReplacement(string deviceName)
    {
        try
        {
            var currentDate = DateTime.Now;
            
            // Update the battery_last_replaced input_datetime entity
            var lastReplacedEntityId = $"input_datetime.{deviceName}_battery_last_replaced";
            
            if (HaContext.GetState(lastReplacedEntityId) != null)
            {
                // Update the last replaced date using CallService
                HaContext.CallService("input_datetime", "set_datetime", 
                    data: new
                    {
                        entity_id = lastReplacedEntityId,
                        date = currentDate.ToString("yyyy-MM-dd"),
                        time = currentDate.ToString("HH:mm:ss")
                    });
                
                Logger.LogInformation("Updated last replaced date for {DeviceName} to {Date}", 
                    deviceName, currentDate.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                Logger.LogWarning("Could not find last replaced entity for {DeviceName}", deviceName);
            }

            // Reset trend analysis for this device
            ResetTrendAnalysisForDevice(deviceName);
            
            // Send notification
            var deviceInfo = GetBatteryDevicesWithInfo().FirstOrDefault(d => d.DeviceName == deviceName);
            var friendlyName = deviceInfo?.FriendlyName ?? deviceName;
            
            Notify.NotifyPhoneVincent(
                "Batterij vervangen geregistreerd",
                $"Batterij vervangen voor {friendlyName} is geregistreerd. Trend analyse is gereset.",
                true,
                TimeSpan.FromMinutes(5).TotalMinutes
            );

            // Trigger immediate trend analysis update for this device
            var device = GetBatteryDevicesWithInfo().FirstOrDefault(d => d.DeviceName == deviceName);
            if (device != null)
            {
                var trend = CalculateBatteryTrend(device);
                UpdateTrendEntitiesForDevice(device, trend);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process battery replacement for {DeviceName}", deviceName);
        }
    }

    /// <summary>
    /// Resets trend analysis data for a specific device after battery replacement.
    /// </summary>
    /// <param name="deviceName">The device name to reset.</param>
    private void ResetTrendAnalysisForDevice(string deviceName)
    {
        try
        {
            // Reset trend entities to initial state
            _entityManager.SetState(
                $"sensor.{deviceName}_battery_discharge_rate",
                0.0,
                new
                {
                    reset_reason = "Battery Replaced",
                    reset_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }
            );

            _entityManager.SetState(
                $"sensor.{deviceName}_battery_age_days",
                0,
                new
                {
                    reset_reason = "Battery Replaced",
                    reset_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }
            );

            _entityManager.SetState(
                $"sensor.{deviceName}_battery_health_status",
                "Excellent",
                new
                {
                    reset_reason = "Battery Replaced",
                    reset_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }
            );

            Logger.LogInformation("Reset trend analysis for {DeviceName} after battery replacement", deviceName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reset trend analysis for {DeviceName}", deviceName);
        }
    }
}