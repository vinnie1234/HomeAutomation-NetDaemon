using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that saves the state of lights and alarms, so <see cref="Reset"/> can restore
/// the lights after a restart.
/// </summary>
[NetDaemonApp(Id = nameof(SaveInState))]
public class SaveInState : BaseApp, IAsyncInitializable
{
    /// <summary>Saves once the lights have been unchanged for this long, instead of on every single change.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(10);

    private readonly IDataRepository _storage;

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
        IDataRepository storage,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Keep the snapshot up to date while running, so Reset restores the state from just before a restart.
        // Not at startup: that would overwrite the snapshot before Reset has used it.
        HaContext.StateChanges()
            .Where(change => change.Entity.EntityId.StartsWith("light."))
            .Throttle(SaveDelay, Scheduler)
            .Subscribe(_ => SaveStates());

        _storage.Save("AlarmState", SetAlarmState());

        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves the current state of the lights to storage.
    /// </summary>
    private void SaveStates()
    {
        var lightStates = _storage.Get<List<LightStateModel>>("LightState") ?? [];

        foreach (var property in Entities.Light.GetType().GetProperties())
            if (property.GetValue(Entities.Light, null) is LightEntity light)
                SetLightEntityState(lightStates, light);

        _storage.Save("LightState", lightStates);

        Logger.LogDebug("Saved the light states");
    }

    /// <summary>
    /// Sets the state of active alarms and returns a list of alarm states.
    /// </summary>
    /// <returns>A list of active alarm states.</returns>
    private List<AlarmStateModel?> SetAlarmState()
    {
        // var activeAlarmsHub = new List<AlarmStateModel?>();
        // var activeAlarmsHubJson = Entities.Sensor.HubVincentAlarms.Attributes?.Alarms;
        // if (activeAlarmsHubJson != null)
        //     activeAlarmsHub
        //         .AddRange(activeAlarmsHubJson.Cast<JsonElement>().Select(o => o.Deserialize<AlarmStateModel>()));
        //
        // foreach (var alarmState in activeAlarmsHub.Where(alarmState => alarmState != null))
        //     if (alarmState != null)
        //         alarmState.EntityId = Entities.Sensor.HubVincentAlarms.EntityId;
        //
        // return activeAlarmsHub;

        return new List<AlarmStateModel?>();
    }

    /// <summary>
    /// Sets the state of a light entity in the list of light states.
    /// A light without a real state (unavailable/unknown, e.g. while Home Assistant restarts) keeps its
    /// previous entry, otherwise Reset would turn it off.
    /// </summary>
    /// <param name="lightStates">The light states to update.</param>
    /// <param name="entity">The light entity to set the state for.</param>
    private static void SetLightEntityState(List<LightStateModel> lightStates, LightEntity entity)
    {
        if (entity.State is not ("on" or "off")) return;

        lightStates.RemoveAll(lightStateModel => lightStateModel.EntityId == entity.EntityId);

        lightStates.Add(new LightStateModel(entityId: entity.EntityId, rgbColors: entity.Attributes?.RgbColor,
            brightness: entity.Attributes?.Brightness, colorTempKelvin: entity.Attributes?.ColorTempKelvin,
            isOn: entity.IsOn(), supportedColorModes: entity.Attributes?.SupportedColorModes));
    }
}
