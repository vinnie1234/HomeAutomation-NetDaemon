using System.Reactive.Concurrency;

namespace Automation.apps.Rooms.BedRoom;

[NetDaemonApp(Id = nameof(BedRoomLights))]
public class BedRoomLights : BaseApp
{
    public BedRoomLights(
        IHaContext ha, 
        ILogger<BedRoomLights> logger, 
        INotify notify, 
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        //todo still need to think about automation
    }
}