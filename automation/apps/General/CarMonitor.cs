using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps an eye on the Peugeot 2008 (Stellantis integration, entities <c>*_buiten_peugeot_2008_*</c>):
/// <list type="bullet">
/// <item>Reminders to Vincent: the tank is almost empty (less than 100 km left), the service is near (30 days or 1000 km).</item>
/// <item>At 22:00, with the car at home: not locked, the alarm off, or a door or the trunk open, with a button in the message to lock it.
/// A door or the trunk that stays open for ten minutes with the engine off is reported too.</item>
/// <item>Security, to both phones: the car leaves the zone Thuis while both of us are home, or a door opens, the car is
/// unlocked or its alarm is switched off at night while everyone at home sleeps.</item>
/// </list>
/// </summary>
[NetDaemonApp(Id = nameof(CarMonitor))]
public class CarMonitor : BaseApp
{
    private const string Prefix = "buiten_peugeot_2008_";

    public const string Tracker = "device_tracker." + Prefix + "voertuig";
    /// <summary>Device class lock: on means the doors are unlocked.</summary>
    public const string Unlocked = "binary_sensor." + Prefix + "deuren";
    /// <summary>Device class running: on means the alarm of the car is switched on.</summary>
    public const string Alarm = "binary_sensor." + Prefix + "alarm";
    public const string Engine = "binary_sensor." + Prefix + "motor";
    public const string LockButton = "button." + Prefix + "deuren_vergrendelen";
    public const string FuelRange = "sensor." + Prefix + "brandstof_bereik";
    public const string FuelPercent = "sensor." + Prefix + "brandstof";
    public const string DaysToService = "sensor." + Prefix + "days_before_maintenance";
    public const string KmToService = "sensor." + Prefix + "mileage_before_maintenance";

    /// <summary>The doors and the trunk (device class door: on means open), with the name used in the messages.</summary>
    public static readonly IReadOnlyDictionary<string, string> Openings = new Dictionary<string, string>
    {
        ["binary_sensor." + Prefix + "driver_door"] = "bestuurdersdeur",
        ["binary_sensor." + Prefix + "passenger_door"] = "bijrijdersdeur",
        ["binary_sensor." + Prefix + "rear_left_door"] = "linker achterdeur",
        ["binary_sensor." + Prefix + "rear_right_door"] = "rechter achterdeur",
        ["binary_sensor." + Prefix + "trunk"] = "kofferbak"
    };

    private const double LowFuelKm = 100;
    private const double ServiceDays = 30;
    private const double ServiceKm = 1000;
    private static readonly TimeSpan LeftOpenAfter = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LeavingCheckDelay = TimeSpan.FromMinutes(2);

    public CarMonitor(
        IHaContext ha,
        ILogger<CarMonitor> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        // Reminders
        new Entity(HaContext, FuelRange).StateChanges().Subscribe(_ => CheckFuel());
        new Entity(HaContext, DaysToService).StateChanges().Subscribe(_ => CheckService());
        new Entity(HaContext, KmToService).StateChanges().Subscribe(_ => CheckService());
        Scheduler.ScheduleCronNl("00 09 * * *", () =>
        {
            CheckFuel();
            CheckService();
        });

        // Locked and closed
        Scheduler.ScheduleCronNl("00 22 * * *", EveningCheck);
        foreach (var (id, name) in Openings)
            new Entity(HaContext, id).StateChanges()
                .Where(x => x.New?.State == "on" && x.Old?.State != "on")
                .Subscribe(_ =>
                {
                    CheckAtNight($"Auto: {name} open", $"De {name} van de auto gaat open terwijl iedereen slaapt");
                    Scheduler.Schedule(LeftOpenAfter, () => CheckStillOpen(id, name));
                });
        new Entity(HaContext, Unlocked).StateChanges()
            .Where(x => x.New?.State == "on" && x.Old?.State == "off")
            .Subscribe(_ => CheckAtNight("Auto: ontgrendeld", "De auto is ontgrendeld terwijl iedereen slaapt"));
        new Entity(HaContext, Alarm).StateChanges()
            .Where(x => x.New?.State == "off" && x.Old?.State == "on")
            .Subscribe(_ => CheckAtNight("Auto: alarm uitgeschakeld", "Het alarm van de auto is uitgeschakeld terwijl iedereen slaapt"));

        // Security
        new Entity(HaContext, Tracker).StateChanges()
            .Where(x => x.Old?.State == "home" && x.New?.State != "home")
            .Subscribe(_ => Scheduler.Schedule(LeavingCheckDelay, CheckLeftWhileWeAreHome));
    }

    private string? StateOf(string entityId) => HaContext.GetState(entityId)?.State;

    private static double? Number(string? state) =>
        double.TryParse(state, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private bool AtHome => StateOf(Tracker) == "home";

    private void CheckFuel()
    {
        if (Number(StateOf(FuelRange)) is not { } km || km >= LowFuelKm) return;
        var percent = Number(StateOf(FuelPercent));
        Notify.NotifyPhoneVincent("Tanken", $"De tank van de auto is bijna leeg: nog {km:0} km{(percent is { } p ? $" ({p:0}%)" : "")}", false, 12 * 60);
    }

    private void CheckService()
    {
        var days = Number(StateOf(DaysToService));
        var km = Number(StateOf(KmToService));
        if (!(days <= ServiceDays) && !(km <= ServiceKm)) return;

        var parts = new List<string>();
        if (days is { } d) parts.Add($"{d:0} dagen");
        if (km is { } k) parts.Add($"{k:0} km");
        Notify.NotifyPhoneVincent("Onderhoudsbeurt auto", $"De auto moet over {string.Join(" of ", parts)} naar de garage", false, 7 * 24 * 60);
    }

    /// <summary>What is not closed and locked: "niet op slot", "kofferbak open", ...</summary>
    private List<string> Problems()
    {
        var problems = new List<string>();
        if (StateOf(Unlocked) == "on") problems.Add("niet op slot");
        if (StateOf(Alarm) == "off") problems.Add("alarm staat uit");
        problems.AddRange(Openings.Where(o => StateOf(o.Key) == "on").Select(o => $"{o.Value} open"));
        return problems;
    }

    private void EveningCheck()
    {
        if (!AtHome) return;
        var problems = Problems();
        if (problems.Count == 0) return;

        Notify.NotifyPeopleHome("De auto is niet dicht", $"De auto: {string.Join(", ", problems)}", false, 60,
            [new ActionModel("LOCK_CAR", "🔒 Vergrendel", func: LockCar)]);
    }

    private void LockCar() => HaContext.CallService("button", "press", ServiceTarget.FromEntity(LockButton));

    private void CheckStillOpen(string id, string name)
    {
        if (StateOf(id) != "on" || StateOf(Engine) == "on") return;
        Notify.NotifyPhoneVincent($"De auto: {name} staat open", $"De {name} van de auto staat al {LeftOpenAfter.TotalMinutes:0} minuten open", false, 60);
    }

    private void CheckAtNight(string title, string message)
    {
        if (!AtHome || !AllAsleep) return;
        Notify.NotifyPhoneVincentCarleen(title, message, true, 10);
    }

    private void CheckLeftWhileWeAreHome()
    {
        // Only when it is still away after a moment: whoever drives it away is no longer home by then either
        if (AtHome || !Vincent.IsHome || !Carleen.IsHome) return;
        Notify.NotifyPhoneVincentCarleen("De auto is weggereden", "De auto heeft de zone Thuis verlaten terwijl jullie allebei thuis zijn", true, 10);
    }
}
