using System.Reactive.Concurrency;
using Automation.Helpers;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages the vacuum cleaner and its related actions.
/// </summary>
[NetDaemonApp(Id = nameof(Vacuum))]
public class Vacuum : BaseApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Vacuum"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public Vacuum(
        IHaContext ha,
        ILogger<Alarm> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
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
                if (Entities.InputBoolean.Sleeping.IsOff() && Entities.InputBoolean.Skipvaccumlitterbox.IsOff())
                {
                    Clean("Kattenbak");
                }
                else if(Entities.InputBoolean.Skipvaccumlitterbox.IsOff())
                {
                    Entities.InputBoolean.Sleeping
                        .StateChanges()
                        .Where(x => x.New.IsOff()).Subscribe(_ =>
                        {
                            if(Entities.InputBoolean.Skipvaccumlitterbox.IsOff())
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
        var zone = Collections.GetRoombaRooms().First(x => x.Key == cleanKey);

        Entities.Vacuum.Jaap.CallService("send_command",
            new
            {
                command = "start",
                @params = new
                {
                    pmap_id = ConfigManager.GetValueFromConfigNested("Roomba", "PmapId") ?? "",
                    regions = new[]
                    {
                        new
                        {
                            region_id = zone.Value.Item1,
                            type = zone.Value.Item2
                        }
                    }
                }
            });
    }
}