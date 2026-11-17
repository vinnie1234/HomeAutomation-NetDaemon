using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Fun;

/// <summary>
/// GTA VI comes out on 19 November 2026 at 00:00. With the switch "GTA 6 modus" on (the page of Vincent) the dashboard looks like Vice City and counts
/// down; at the moment it comes out Vincent gets a message on his phone.
/// </summary>
[NetDaemonApp(Id = nameof(GtaSix))]
public class GtaSix : BaseApp
{
    public const string Switch = "input_boolean.gta_6_modus";

    /// <summary>The moment GTA VI comes out, in Dutch time.</summary>
    public static readonly DateTime Release = new(2026, 11, 19, 0, 0, 0);

    public GtaSix(
        IHaContext ha,
        ILogger<GtaSix> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        // Cron has no year: it runs every 19 November, and only the day of the release counts
        Scheduler.ScheduleCronNl("0 0 19 11 *", ReleaseMoment);
    }

    private void ReleaseMoment()
    {
        var now = Scheduler.NowNl().DateTime;
        if (now.Year != Release.Year || HaContext.GetState(Switch)?.State != "on") return;

        Logger.LogInformation("GTA VI is out");
        Notify.NotifyPhoneVincent("🌴 GTA VI is uit!", "Het is zover: 19 november, 00:00. Veel plezier in Vice City!", true);
    }
}
