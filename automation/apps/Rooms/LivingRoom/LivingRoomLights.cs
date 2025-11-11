using System.Reactive.Concurrency;
using Automation.Configuration;

namespace Automation.apps.Rooms.LivingRoom;

[NetDaemonApp(Id = nameof(LivingRoomLights))]
public class LivingRoomLights : BaseApp
{
    private readonly AppConfiguration _config = new();
    /// <summary>
    /// Initializes a new instance of the <see cref="LivingRoomLights"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public LivingRoomLights(
        IHaContext ha,
        ILogger<LivingRoomLights> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        HaContext.Events.Where(x => x.EventType == "hue_event").Subscribe(x =>
        {
            var eventModel = x.DataElement?.ToObject<EventModel>();
            if (eventModel != null) TurnOnPlafond(eventModel);
        });

        Entities.BinarySensor.Motionwoonkamer.StateChanges().Subscribe(x =>
        {
            if (Entities.Light.Woonkamer.IsOn() && x.New.IsOff())
            {
                if (Vincent.IsSleeping)
                {
                    Entities.Light.Woonkamer.TurnOff();
                }
            }else if (Entities.Light.Woonkamer.IsOff() && x.New.IsOn())
            {
                if(!Vincent.IsSleeping)
                    LightExtension.SetLightSceneWoonkamer(Entities);
            }
        });

        Entities.InputSelect.Housemodeselect
            .StateChanges()
            .Where(_ => Entities.Light.HueFilamentBulb2.IsOn())
            .Subscribe(_ =>
            {
                LightExtension.TurnOnLightsWoonkamer(Entities, Scheduler);
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
            {
                LightExtension.TurnOnLightsWoonkamer(Entities, Scheduler);
            }
            else
            {
                LightExtension.TurnOffLightsWoonkamer(Entities, Scheduler);
            }
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
                LightExtension.TurnOnLightsWoonkamer(Entities, Scheduler);
            });
        });
    }
}