using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps an eye on the Bosch eBike (Bosch eBike integration, entities <c>*_bfe_ah1336572_*</c>):
/// <list type="bullet">
/// <item>The battery is full again (charging stops at 95% or more): a message to Vincent, so the charger can go.</item>
/// <item>The bike leaves the zone Thuis while Vincent is home: a message to Vincent (to both of us when we are both home), as a
/// check against theft. Only when it is still away after a moment, so a jumping GPS position does not count.</item>
/// </list>
/// </summary>
[NetDaemonApp(Id = nameof(BikeMonitor))]
public class BikeMonitor : BaseApp
{
    private const string Prefix = "bfe_ah1336572_";

    public const string Tracker = "device_tracker." + Prefix + "location";
    public const string BatteryLevel = "sensor." + Prefix + "battery_level";
    public const string Charging = "binary_sensor." + Prefix + "battery_charging";
    public const string Range = "sensor." + Prefix + "battery_reachable_max_range";

    /// <summary>From this percentage on, a battery that stops charging is full.</summary>
    public const double FullBattery = 95;

    private static readonly TimeSpan LeavingCheckDelay = TimeSpan.FromMinutes(2);

    public BikeMonitor(
        IHaContext ha,
        ILogger<BikeMonitor> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        Entities.BinarySensor.BfeAh1336572BatteryCharging.StateChanges()
            .Where(x => x.Old?.State == "on" && x.New?.State == "off")
            .Subscribe(_ => CheckFull());

        Entities.DeviceTracker.BfeAh1336572Location.StateChanges()
            .Where(x => x.Old?.State == "home" && x.New?.State != "home")
            .Subscribe(_ => Scheduler.Schedule(LeavingCheckDelay, CheckLeftWhileWeAreHome));
    }

    private string? StateOf(string entityId) => HaContext.GetState(entityId)?.State;

    private static double? Number(string? state) =>
        double.TryParse(state, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private void CheckFull()
    {
        if (Number(StateOf(BatteryLevel)) is not { } level || level < FullBattery) return;
        Notify.NotifyPhoneVincent("Fiets: accu vol", $"De accu van de fiets is vol ({level:0}%), de oplader kan eraf", false, 60);
    }

    private void CheckLeftWhileWeAreHome()
    {
        // Only when it is still away after a moment, and while somebody is home to notice
        if (StateOf(Tracker) == "home" || !Vincent.IsHome) return;
        const string message = "De fiets heeft de zone Thuis verlaten terwijl er iemand thuis is";
        if (Carleen.IsHome) Notify.NotifyPhoneVincentCarleen("De fiets is weg", message, true, 10);
        else Notify.NotifyPhoneVincent("De fiets is weg", message, true, 10);
    }
}
