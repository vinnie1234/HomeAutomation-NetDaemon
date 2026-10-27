using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Tells Home Assistant every minute that NetDaemon is alive, by setting <c>input_datetime.netdaemon_heartbeat</c> to the
/// current time. NetDaemon cannot report that it is down, so Home Assistant does the watching: the template sensor
/// <c>binary_sensor.netdaemon_online</c> (see <c>dashboard/ha/template.yaml</c>) turns off when the beat is missed for
/// a few minutes, and the dashboard then shows an alert with a button that restarts the add-on.
/// The beat goes over the same connection every app uses, so a lost connection to Home Assistant is noticed too.
/// </summary>
[NetDaemonApp(Id = nameof(NetDaemonHeartbeat))]
public class NetDaemonHeartbeat : BaseApp
{
    public const string HeartbeatId = "input_datetime.netdaemon_heartbeat";

    /// <summary>How often the beat is sent. The template sensor allows a few missed beats before it calls NetDaemon offline.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    public NetDaemonHeartbeat(
        IHaContext ha,
        ILogger<NetDaemonHeartbeat> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        Beat();
        Scheduler.SchedulePeriodic(Interval, Beat);
    }

    private void Beat()
    {
        try
        {
            HaContext.CallService("input_datetime", "set_datetime", ServiceTarget.FromEntity(HeartbeatId),
                new { timestamp = Scheduler.Now.ToUnixTimeSeconds() });
        }
        catch (Exception ex)
        {
            // Not connected right now: the next beat tries again, and Home Assistant notices the gap
            Logger.LogWarning(ex, "Could not send the heartbeat");
        }
    }
}
