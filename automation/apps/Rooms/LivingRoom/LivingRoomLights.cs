using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Helpers;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.LivingRoom;

[NetDaemonApp(Id = nameof(LivingRoomLights))]
public class LivingRoomLights : BaseApp
{
    private readonly AppConfiguration _config;
    /// <summary>
    /// Initializes a new instance of the <see cref="LivingRoomLights"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="livingRoomPresenceService">The service that tracks whether the living room is occupied.</param>
    public LivingRoomLights(
        IHaContext ha,
        ILogger<LivingRoomLights> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfiguration> config,
        ILivingRoomPresenceService livingRoomPresenceService)
        : base(ha, logger, notify, scheduler)
    {
        _config = config.Value;
        HaContext.Events.Where(x => x.EventType == "hue_event").Subscribe(x =>
        {
            var eventModel = x.DataElement?.ToObject<EventModel>();
            if (eventModel != null) TurnOnPlafond(eventModel);
        });

        livingRoomPresenceService.OccupiedChanged.Subscribe(isOccupied =>
        {
            if (Entities.Light.Woonkamer.IsOn() && !isOccupied)
            {
                if (IsVincentNightMode) Entities.Light.Woonkamer.TurnOff();
            }else if (Entities.Light.Woonkamer.IsOff() && isOccupied)
            {
                if (!IsVincentNightMode)
                    LightExtension.SetLightSceneWoonkamer(Entities);
            }
        });

        Entities.InputSelect.Housemodeselect
            .StateChanges()
            .Where(_ => Entities.Light.HueFilamentBulb2.IsOn())
            .Subscribe(_ =>
            {
                LightExtension.SetLightSceneWoonkamer(Entities);
            });

        FixLightsDifferentColorWhenTurnOn();
    }

    /// <summary>
    /// Turns on or off the living room lights based on the event model.
    /// </summary>
    /// <param name="eventModel">The event model containing the switch event data.</param>
    private void TurnOnPlafond(EventModel eventModel)
    {
        var hueWallLivingRoomId = _config.Lights.DeviceIds["HueWallLivingRoom"];

        if (eventModel is { DeviceId: { } deviceId, Type: "initial_press" } && deviceId == hueWallLivingRoomId)
        {
            if (Entities.Light.HueFilamentBulb2.IsOff())
                LightExtension.SetLightSceneWoonkamer(Entities);
            else
                Entities.Light.Woonkamer.TurnOff(transition: _config.Lights.DefaultTransitionSeconds);
        }
    }

    /// <summary>
    /// Fixes the issue of lights turning on with different colors by ensuring they are set correctly after turning on.
    /// </summary>
    private void FixLightsDifferentColorWhenTurnOn()
    {
        Entities.Light.HueFilamentBulb2.WhenTurnsOn(_ =>
        {
            Scheduler.Schedule(TimeSpan.FromSeconds(10), () =>
            {
                if (Entities.Light.HueFilamentBulb2.IsOn())
                    LightExtension.SetLightSceneWoonkamer(Entities);
            });
        });
    }
}