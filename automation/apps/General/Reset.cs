using System.Reactive.Concurrency;

namespace Automation.apps.General;

[NetDaemonApp(Id = nameof(Reset))]
public class Reset : BaseApp
{
    private readonly IDataRepository _storage;

    private List<LightStateModel>? LightEntitiesStates { get; set; }

    public Reset(
        IHaContext ha,
        ILogger<Reset> logger,
        IDataRepository storage,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        _storage = storage;
        LightEntitiesStates = new List<LightStateModel>();

        if (Entities.InputBoolean.Disablereset.IsOff())
        {
            ResetAlarm();
            ResetLights();
        }
    }

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

    private static void TurnOnReset(LightStateModel oldStateLight, LightEntity light)
    {
        if ((light.Attributes?.SupportedColorModes ?? Array.Empty<string>()).Any(x => x == "xy"))
        {
            if (oldStateLight.RgbColors != null)
            {
                //cause the codegen changed the input from Object to IReadOnlyCollection<int> but leaves the output to IReadOnlyList<double> I need to translate the value;
                IReadOnlyCollection<int> lightColorInInt = new[]
                {
                    (int)oldStateLight.RgbColors[0], (int)oldStateLight.RgbColors[1], (int)oldStateLight.RgbColors[2]
                };
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