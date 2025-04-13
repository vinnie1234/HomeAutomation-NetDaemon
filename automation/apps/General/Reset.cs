using System.Reactive.Concurrency;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that handles the reset logic for alarms and lights.
/// </summary>
[NetDaemonApp(Id = nameof(Reset))]
public class Reset : BaseApp
{
    private readonly IDataRepository _storage;

    /// <summary>
    /// Gets or sets the list of light entity states.
    /// </summary>
    private List<LightStateModel>? LightEntitiesStates { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reset"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="storage">The data repository for storing and retrieving data.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public Reset(
        IHaContext ha,
        ILogger<Reset> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository storage)
        : base(ha, logger, notify, scheduler)
    {
        _storage = storage;
        LightEntitiesStates = [];

        if (Entities.InputBoolean.Disablereset.IsOff())
        {
            ResetAlarm();
            ResetLights();
        }
    }

    /// <summary>
    /// Resets the alarms by comparing the current active alarms with the stored alarms.
    /// </summary>
    private void ResetAlarm()
    {
        //todo TTP steps!
        var oldAlarms = _storage.Get<List<AlarmStateModel?>>("LightState");
        if (oldAlarms == null) return;

        var activeAlarmsHub = new List<AlarmStateModel?>();
        var activeAlarmsHubJson = Entities.Sensor.HubVincentAlarms.Attributes?.Alarms;
        if (activeAlarmsHubJson != null)
            activeAlarmsHub.AddRange(activeAlarmsHubJson.Cast<JsonElement>()
                .Select(o => o.Deserialize<AlarmStateModel>()));

        foreach (var alarm in activeAlarmsHub
                     .Where(alarm => alarm?.Status == "set")
                     .Where(alarm => oldAlarms
                         .TrueForAll(alarmStateModel => alarmStateModel?.AlarmId != alarm?.AlarmId)))
            if (alarm is { EntityId: not null, AlarmId: not null })
            {
                Notify.NotifyHouse("deleteAlarm", $"Alarm van {alarm.LocalTime} word verwijderd", true);
                Services.GoogleHome.DeleteAlarm(alarm.EntityId, alarm.AlarmId);
            }
    }

    /// <summary>
    /// Resets the lights to their previous states.
    /// </summary>
    private void ResetLights()
    {
        //todo TTP steps!
        LightEntitiesStates = _storage.Get<List<LightStateModel>>("LightState");

        Logger.LogDebug("Get {Count} light states", LightEntitiesStates?.Count);

        var properties = Entities.Light.GetType().GetProperties();

        foreach (var property in properties)
        {
            var light = (LightEntity)property.GetValue(Entities.Light, null)!;

            var oldStateLight = LightEntitiesStates?
                .Find(lightStateModel => lightStateModel.EntityId == light.EntityId);

            ActualResetLight(oldStateLight, light);
        }
    }

    /// <summary>
    /// Resets a specific light to its previous state.
    /// </summary>
    /// <param name="oldStateLight">The previous state of the light.</param>
    /// <param name="light">The light entity to reset.</param>
    private static void ActualResetLight(LightStateModel? oldStateLight, LightEntity light)
    {
        if (oldStateLight != null)
            switch (oldStateLight.IsOn)
            {
                case false:
                    light.TurnOff();
                    break;
                case true:
                    TurnOnReset(oldStateLight, light);
                    break;
            }
    }

    /// <summary>
    /// Turns on a light and sets its properties based on the previous state.
    /// </summary>
    /// <param name="oldStateLight">The previous state of the light.</param>
    /// <param name="light">The light entity to turn on.</param>
    private static void TurnOnReset(LightStateModel oldStateLight, LightEntity light)
    {
        if ((light.Attributes?.SupportedColorModes ?? Array.Empty<string>()).Any(x => x == "xy"))
        {
            if (oldStateLight.RgbColors != null)
            {
                // Translate the value from IReadOnlyList<double> to IReadOnlyCollection<int>
                IReadOnlyCollection<int> lightColorInInt =
                [
                    (int)oldStateLight.RgbColors[0], (int)oldStateLight.RgbColors[1], (int)oldStateLight.RgbColors[2]
                ];
                light.TurnOn(
                    rgbColor: lightColorInInt,
                    brightness: Convert.ToInt64(oldStateLight.Brightness)
                );
            }
        }
        else
        {
            if (light.Attributes == null ||
                (light.Attributes?.SupportedColorModes ??
                 Array.Empty<string>())
                .Any(x => x == "onoff"))
                light.TurnOn();
            else
                light.TurnOn(
                    colorTemp: oldStateLight.ColorTemp,
                    brightness: Convert.ToInt64(oldStateLight.Brightness)
                );
        }
    }
}