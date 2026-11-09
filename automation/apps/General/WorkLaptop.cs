using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Vincent's work laptop (entities <c>*.werk_laptop_*</c>; they are unavailable while the laptop is off or not at home). Everything here
/// is about the laptop at home:
/// <list type="bullet">
/// <item>Working time: when the laptop starts earlier than the working hours of today, Vincent works from then on; when it stays on or
/// goes off later or earlier than the end, he stops then. Never further away from the hours than <see cref="Margin"/>, so a laptop that
/// is left on all night is not hours of work. This is for every working day: also an office day that starts at home (a traffic jam) counts
/// for as long as he is home. The energy of working at home (<see cref="HomeWorkEnergyTracker"/>) follows it.</item>
/// <item>The laptop starts on a working day: the desk light goes on, like when the PC starts (<see cref="PcManager"/>).</item>
/// <item>Vincent leaves: the laptop is locked. Vincent goes to sleep: it is shut down.</item>
/// <item>At the end of the working day, while the laptop is on: a reminder to close it.</item>
/// <item>The battery is low and the laptop is not charging: a message.</item>
/// </list>
/// </summary>
[NetDaemonApp(Id = nameof(WorkLaptop))]
public class WorkLaptop : BaseApp
{
    public const string Uptime = "sensor.werk_laptop_uptime";
    public const string Battery = "sensor.werk_laptop_battery";
    public const string PluggedIn = "binary_sensor.werk_laptop_plugged_in";
    public const string LockButton = "button.werk_laptop_lock";
    public const string ShutdownButton = "button.werk_laptop_shutdown";

    /// <summary>How far from the working hours the laptop can move the start and the end.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(60);

    private const double LowBattery = 20;
    private const double BatteryRecovered = 30;

    private string _workModeDay = "";
    private string _reminderDay = "";
    private string _extendedDay = "";
    private bool _batteryWarned;

    public WorkLaptop(
        IHaContext ha,
        ILogger<WorkLaptop> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        new Entity(HaContext, Uptime).StateChanges()
            .Where(x => IsAvailable(x.Old?.State) != IsAvailable(x.New?.State))
            .Subscribe(x =>
            {
                if (IsAvailable(x.New?.State)) LaptopStarted();
                else LaptopStopped();
            });

        Entities.InputBoolean.Awayvincent.StateChanges().Where(x => x.Old.IsOff() && x.New.IsOn()).Subscribe(_ => VincentLeft());
        Entities.InputBoolean.Sleepingvincent.StateChanges().Where(x => x.Old.IsOff() && x.New.IsOn()).Subscribe(_ => VincentSleeps());

        new Entity(HaContext, Battery).StateChanges().Subscribe(_ => CheckBattery());
        new Entity(HaContext, PluggedIn).StateChanges().Subscribe(_ => CheckBattery());

        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), CheckWork);
    }

    // ---- the laptop and the working hours

    /// <summary>Whether a state of the uptime sensor means the laptop is there: it is unavailable while the laptop is off or not at home.</summary>
    public static bool IsAvailable(string? state) => state is not (null or "" or "unavailable" or "unknown");

    public static bool IsOnline(IHaContext ha) => IsAvailable(ha.GetState(Uptime)?.State);

    /// <summary>
    /// Whether the laptop keeps Vincent working past the end of his hours: it is on, he is home, and the end is not further away than
    /// <see cref="Margin"/>. <see cref="HouseStateManager"/> asks this before it stops the work at the end.
    /// </summary>
    public static bool KeepsWorking(IHaContext ha, string person, bool personHome, DateTime now, DateTime end) =>
        person == "vincent" && personHome && IsOnline(ha) && now < end + Margin && now.TimeOfDay < Models.Persons.WorkHours.LatestEnd;

    /// <summary>The working hours of today: from the calendar when the work comes from it, otherwise the hours of the person.</summary>
    private (DateTime Start, DateTime End) HoursToday(DateTime now) =>
        Models.Persons.WorkHours.Today(HaContext, VincentSchedule, "vincent", now);

    private void CheckWork()
    {
        try
        {
            var now = Scheduler.NowNl().DateTime;
            var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (VincentSchedule.IsDayOff(now.DayOfWeek)) return;
            var (start, end) = HoursToday(now);
            var athome = Vincent.IsHome && IsOnline(HaContext);

            // Starting earlier: the laptop is on at home before the hours begin, but not further back than the margin
            if (athome && VincentSchedule.Working.IsOff() && now >= start - Margin && now < end)
            {
                Logger.LogInformation("Vincent works from {Now}: the laptop is on (the hours start at {Start})", now, start);
                VincentSchedule.Working.TurnOn();
            }

            // Stopping later: the end passed, and the laptop keeps him working until it goes off or the margin is over
            if (now >= end)
            {
                if (_extendedDay != today && VincentSchedule.Working.IsOn() && athome) _extendedDay = today;
                if (_extendedDay == today && VincentSchedule.Working.IsOn() && !KeepsWorking(HaContext, "vincent", Vincent.IsHome, now, end))
                {
                    Logger.LogInformation("Vincent stops working: the laptop is off or the margin after the end is over");
                    VincentSchedule.Working.TurnOff();
                }

                // The end of the day: a reminder to close the laptop
                if (athome && _reminderDay != today && now < end + Margin && now.TimeOfDay < Models.Persons.WorkHours.LatestEnd)
                {
                    _reminderDay = today;
                    Notify.NotifyPhoneVincent("Werklaptop", "Het is tijd om af te sluiten", false, 60,
                    [
                        new ActionModel("LAPTOP_LOCK", "🔒 Vergrendel", func: () => HaContext.CallService("button", "press", ServiceTarget.FromEntity(LockButton))),
                        new ActionModel("LAPTOP_SHUTDOWN", "⏻ Afsluiten", func: () => HaContext.CallService("button", "press", ServiceTarget.FromEntity(ShutdownButton)))
                    ]);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not check the work laptop");
        }
    }

    private void LaptopStarted()
    {
        var now = Scheduler.NowNl().DateTime;
        var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        CheckWork();

        // The laptop starts on a working day while Vincent is home and awake: the desk light, once a day
        if (VincentSchedule.IsDayOff(now.DayOfWeek) || !Vincent.IsHome || Vincent.IsSleeping || _workModeDay == today) return;
        _workModeDay = today;
        Logger.LogInformation("The work laptop started: work mode");
        Entities.Light.Bureau.TurnOn();
        Entities.Light.Nachtkastje.TurnOff();
        Entities.Light.Plafondslaapkamer.TurnOn();
        if (Entities.MediaPlayer.Tv.IsOn()) Entities.MediaPlayer.Tv.TurnOff();
    }

    /// <summary>The laptop went off: within the margin before the end that is the end of the work, earlier than the hours say.</summary>
    private void LaptopStopped()
    {
        var now = Scheduler.NowNl().DateTime;
        if (VincentSchedule.IsDayOff(now.DayOfWeek) || !Vincent.IsHome || VincentSchedule.Working.IsOff()) return;
        var (_, end) = HoursToday(now);
        if (now >= end - Margin && now < end)
        {
            Logger.LogInformation("Vincent stops working: the laptop went off before the end ({End})", end);
            VincentSchedule.Working.TurnOff();
        }
    }

    // ---- leaving, sleeping, battery

    private void VincentLeft()
    {
        if (!IsOnline(HaContext)) return;
        Logger.LogInformation("Vincent left: locking the work laptop");
        HaContext.CallService("button", "press", ServiceTarget.FromEntity(LockButton));
    }

    private void VincentSleeps()
    {
        if (!IsOnline(HaContext)) return;
        Logger.LogInformation("Vincent goes to sleep: shutting down the work laptop");
        HaContext.CallService("button", "press", ServiceTarget.FromEntity(ShutdownButton));
    }

    private void CheckBattery()
    {
        var level = double.TryParse(HaContext.GetState(Battery)?.State, NumberStyles.Float, CultureInfo.InvariantCulture, out var l) ? (double?)l : null;
        var charging = HaContext.GetState(PluggedIn)?.State == "on";
        if (charging || level >= BatteryRecovered)
        {
            _batteryWarned = false;
            return;
        }
        if (level is < LowBattery && HaContext.GetState(PluggedIn)?.State == "off" && !_batteryWarned)
        {
            _batteryWarned = true;
            Notify.NotifyPhoneVincent("Werklaptop", $"De batterij van de werklaptop is bijna leeg ({Math.Round(level.Value)}%): aan de lader", false, 60);
        }
    }
}
