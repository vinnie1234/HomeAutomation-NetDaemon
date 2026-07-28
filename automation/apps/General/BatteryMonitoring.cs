using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that monitors battery levels and sends notifications when the battery is low.
/// </summary>
[NetDaemonApp(Id = nameof(BatteryMonitoring))]
public class BatteryMonitoring : BaseApp
{
    private readonly AppConfiguration _appConfiguration;
    private readonly AppConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatteryMonitoring"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public BatteryMonitoring(
        IHaContext ha, 
        ILogger<BatteryMonitoring> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IOptions<AppConfiguration> appConfiguration)
        : base(ha, logger, notify, scheduler)
    {
        _config = config.Value;
        _appConfiguration = appConfiguration.Value;

        var batterySensors = Entities.Sensor.
            EnumerateAllNumeric().Where(x => x.Attributes?.DeviceClass == "battery");
        
        Console.WriteLine($"Found {batterySensors.Count()} battery sensors");
        foreach (var battySensor in batterySensors)
        {
            battySensor
                .StateChanges()
                .WhenStateIsFor(x => x?.State <= _appConfiguration.Battery.WarningLevel, _appConfiguration.Battery.CheckInterval, Scheduler)
                .Subscribe(x => SendNotification(battySensor.Attributes?.FriendlyName ?? "", x.Entity.State ?? 0));

            battySensor
                .StateChanges()
                .Where(x => x.Entity.State is 100)
                .Subscribe(_ => Notify.ResetNotificationHistoryForNotificationTitle(battySensor.Attributes?.FriendlyName ?? ""));
        }
    }

    /// <summary>
    /// Sends a notification when the battery level is low.
    /// </summary>
    /// <param name="name">The name of the device with the low battery.</param>
    /// <param name="batterPrc">The current battery percentage.</param>
    private void SendNotification(string name, double batterPrc)
    {
        Logger.LogDebug("Batterij bijna leeg van {Name}. De batterij is nu op {BatterPrc}", name, batterPrc);
        Notify.NotifyPhoneVincent(
            $"Batterij bijna leeg van {name}",
            $"Het is tijd om de batterij op te laden van {name}. De batterij is nu op {batterPrc}%",
            false,
            TimeSpan.FromDays(7).Minutes,
            [
                new ActionModel(action: "URI", title: "Ga naar batterij checks",
                    uri: _config.BaseUrlHomeAssistant + "/status-huis")
            ]);
    }
}
