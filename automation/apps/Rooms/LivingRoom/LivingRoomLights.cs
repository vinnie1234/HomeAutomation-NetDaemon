using System.Threading;

namespace Automation.apps.Rooms.LivingRoom;

[NetDaemonApp(Id = nameof(LivingRoomLights))]
// ReSharper disable once UnusedType.Global
public class LivingRoomLights : BaseApp
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public LivingRoomLights(IHaContext ha, ILogger<LivingRoomLights> logger, INotify notify)
        : base(ha, logger, notify)
    {
        HaContext.Events.Where(x => x.EventType == "hue_event").Subscribe(x =>
        {
            var eventModel = x.DataElement?.ToObject<EventModel>();
            if (eventModel != null) TurnOnPlafond(eventModel);
        });
    }

    // ReSharper disable once IdentifierTypo
    private void TurnOnPlafond(EventModel eventModel)
    {
        if (eventModel is { DeviceId: "b4784a8e43cc6f5aabfb6895f3a8dbac", Type: "initial_press" })
        {
            if (Entities.Light.HueFilamentBulb2.IsOff())
            {
                Entities.Light.HueFilamentBulb2.TurnOn(brightnessPct: 100, colorTemp: 222);
                Entities.Light.HueFilamentBulb2
                    .StateChanges()
                    .Where(x => x.Old.IsOff())
                    .Throttle(TimeSpan.FromMilliseconds(50))
                    .Subscribe(_ => { Entities.Light.PlafondWoonkamer.TurnOn(brightnessPct: 100, colorTemp: 222); });
                Entities.Light.PlafondWoonkamer
                    .StateChanges()
                    .Where(x => x.Old.IsOff())
                    .Throttle(TimeSpan.FromMilliseconds(50))
                    .Subscribe(_ => { Entities.Light.HueFilamentBulb1.TurnOn(brightnessPct: 100, colorTemp: 222); });
                
                Thread.Sleep(TimeSpan.FromMilliseconds(150));
                //backup when PlafondWoonkamer was already on
                Entities.Light.HueFilamentBulb1.TurnOn(brightnessPct: 100, colorTemp: 222);
            }
            else
            {
                Entities.Light.HueFilamentBulb1.TurnOff();
                Entities.Light.HueFilamentBulb1
                    .StateChanges()
                    .Where(x => x.Old.IsOn())
                    .Throttle(TimeSpan.FromMilliseconds(50))
                    .Subscribe(_ => { Entities.Light.PlafondWoonkamer.TurnOff(); });
                Entities.Light.PlafondWoonkamer
                    .StateChanges()
                    .Where(x => x.Old.IsOn())
                    .Throttle(TimeSpan.FromMilliseconds(50))
                    .Subscribe(_ => { Entities.Light.HueFilamentBulb2.TurnOff(); });
                
                Thread.Sleep(TimeSpan.FromMilliseconds(150));
                //backup when PlafondWoonkamer was already off
                Entities.Light.HueFilamentBulb2.TurnOff();
            }
        }
    }
}