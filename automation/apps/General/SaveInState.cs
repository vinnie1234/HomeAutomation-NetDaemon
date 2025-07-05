using System.Reactive.Concurrency;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that saves the state of lights and alarms.
/// </summary>
[NetDaemonApp(Id = nameof(SaveInState))]
public class SaveInState : BaseApp, IAsyncInitializable
{
    private readonly IDataRepository _storage;
    private List<LightStateModel> LightEntitiesStates { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveInState"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="storage">The data repository for storing and retrieving data.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public SaveInState(
        IHaContext ha,
        ILogger<SaveInState> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository storage)
        : base(ha, logger, notify, scheduler)
    {
        _storage = storage;
        LightEntitiesStates = new List<LightStateModel>();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await SetInitialStates();
    }

    /// <summary>
    /// Sets the initial states of lights and alarms and saves them to storage.
    /// </summary>
    private async Task SetInitialStates()
    {
        var properties = Entities.Light.GetType().GetProperties();

        foreach (var property in properties)
        {
            var light = property.GetValue(Entities.Light, null);
            if (light != null)
                SetLightEntityState((LightEntity)light);
        }

        await _storage.SaveAsync("LightState", LightEntitiesStates);

        await _storage.SaveAsync("AlarmState", SetAlarmState());

        Logger.LogDebug("Save state");
    }

    /// <summary>
    /// Sets the state of active alarms and returns a list of alarm states.
    /// </summary>
    /// <returns>A list of active alarm states.</returns>
    private List<AlarmStateModel?> SetAlarmState()
    {
        var activeAlarmsHub = new List<AlarmStateModel?>();
        var activeAlarmsHubJson = Entities.Sensor.HubVincentAlarms.Attributes?.Alarms;
        if (activeAlarmsHubJson != null)
            activeAlarmsHub
                .AddRange(activeAlarmsHubJson.Cast<JsonElement>().Select(o => o.Deserialize<AlarmStateModel>()));

        foreach (var alarmState in activeAlarmsHub.Where(alarmState => alarmState != null))
            if (alarmState != null)
                alarmState.EntityId = Entities.Sensor.HubVincentAlarms.EntityId;

        return activeAlarmsHub;
    }

    /// <summary>
    /// Sets the state of a light entity and adds it to the list of light states.
    /// </summary>
    /// <param name="entity">The light entity to set the state for.</param>
    private void SetLightEntityState(LightEntity entity)
    {
        var oldEntity = LightEntitiesStates
            .Find(lightStateModel => lightStateModel.EntityId == entity.EntityId);
        if (oldEntity != null) LightEntitiesStates.Remove(oldEntity);

        LightEntitiesStates.Add(new LightStateModel(entityId: entity.EntityId, rgbColors: entity.Attributes?.RgbColor,
            brightness: entity.Attributes?.Brightness, colorTemp: (JsonElement?)entity.Attributes?.ColorTemp, isOn: entity.IsOn(),
            supportedColorModes: entity.Attributes?.SupportedColorModes));
    }
}