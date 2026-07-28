using System.Reactive.Concurrency;

using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages the vacuum cleaner and its related actions.
/// </summary>
[NetDaemonApp(Id = nameof(Vacuum))]
public class Vacuum : BaseApp
{
    private readonly AppConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vacuum"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public Vacuum(
        IHaContext ha,
        ILogger<Vacuum> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler)
    {
        _config = config.Value;
        CleanLitterBoxAfterUse();
        StartFromButton();
    }

    /// <summary>
    /// Subscribes to state changes of input buttons to start the vacuum cleaner.
    /// </summary>
    private void StartFromButton()
    {
        var buttons = new Dictionary<InputButtonEntity, string>
        {
            { Entities.InputButton.Vacuumcleankattenbak, "Kattenbak" },
            { Entities.InputButton.Vacuumcleanbank, "Bank" },
            { Entities.InputButton.Vacuumcleangang, "Gang" },
            { Entities.InputButton.Vacuumcleanslaapkamer, "Slaapkamer" },
            { Entities.InputButton.Vacuumcleanwoonkamer, "Woonkamer" }
        };

        foreach (var button in buttons)
        {
            button.Key.StateChanges().Subscribe(_ =>
            {
                Clean(button.Value);
            });
        }
    }

    /// <summary>
    /// Subscribes to state changes of the litter box sensor to start cleaning after use.
    /// </summary>
    private void CleanLitterBoxAfterUse()
    {
        Entities.Sensor.PetsnowyLitterboxStatus
            .StateChanges()
            .Where(x => x.New?.State == "cleaning")
            .Subscribe(_ =>
            {
                if(Entities.InputBoolean.Disablereset.IsOn()) return;
                
                if (!IsNightMode && Entities.InputBoolean.Skipvaccumlitterbox.IsOff())
                {
                    Clean("Kattenbak");
                }
                else if (Entities.InputBoolean.Skipvaccumlitterbox.IsOff())
                {
                    // Wait until nobody is sleeping anymore before cleaning
                    Entities.InputBoolean.Sleepingvincent
                        .StateChanges()
                        .Where(x => x.New.IsOff())
                        .Subscribe(_ =>
                        {
                            if (!IsNightMode && Entities.InputBoolean.Skipvaccumlitterbox.IsOff())
                                Clean("Kattenbak");
                        });
                    Entities.InputBoolean.Sleepingcarleen
                        .StateChanges()
                        .Where(x => x.New.IsOff())
                        .Subscribe(_ =>
                        {
                            if (!IsNightMode && Entities.InputBoolean.Skipvaccumlitterbox.IsOff())
                                Clean("Kattenbak");
                        });
                }
                
                Entities.InputBoolean.Skipvaccumlitterbox.TurnOff();
            });
    }

    /// <summary>
    /// Sends a command to the vacuum cleaner to start cleaning a specified zone.
    /// </summary>
    /// <param name="cleanKey">The key representing the zone to clean.</param>
    private void Clean(string cleanKey)
    {
        if (!_config.Roomba.Rooms.TryGetValue(cleanKey, out var zone))
        {
            Logger.LogError("Roomba room {Room} not found in configuration.", cleanKey);
            return;
        }

        Entities.Vacuum.Jaap.CallService("send_command",
            new
            {
                command = "start",
                @params = new
                {
                    pmap_id = _config.Roomba.PmapId,
                    regions = new[]
                    {
                        new
                        {
                            region_id = zone.Id,
                            type = zone.Type
                        }
                    }
                }
            });
    }
}