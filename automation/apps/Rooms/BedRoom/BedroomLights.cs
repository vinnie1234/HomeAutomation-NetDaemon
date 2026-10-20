using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.BedRoom;

[NetDaemonApp(Id = nameof(BedRoomLights))]
public class BedRoomLights : BaseApp
{
    public BedRoomLights(
        IHaContext ha,
        ILogger<BedRoomLights> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        //todo still need to think about automation
    }
}