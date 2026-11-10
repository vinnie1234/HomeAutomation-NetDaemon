using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.LivingRoom;

/// <summary>
/// Wakes Vincent's tablet in the living room (Galaxy Tab S6, Fully Kiosk Browser): on motion in the living room, at someone's
/// wake-up time, when someone comes home and on important alerts. Motion only turns the screen on; the others also open the
/// dashboard on Home. Not while everyone at
/// home sleeps and not while the TV is on. The screen goes off again when everyone goes to sleep and when the TV turns on.
/// <para>
/// Fully Kiosk does it through its HA integration: the screen switch turns the screen on and off, the start URL is Home,
/// and the screen off timer turns the screen off again when nobody touches it. Fully can unlock a swipe lock but not a
/// PIN or pattern, so the tablet needs no screen lock (or swipe) and Fully's kiosk mode instead.
/// </para>
/// </summary>
[NetDaemonApp(Id = nameof(TabletScreen))]
public class TabletScreen : BaseApp
{
    private const string ScreenOffTimer = "number.tab_s6_van_vincent_scherm_uit_timer";
    /// <summary>How long the screen stays on without anyone touching it, in seconds (the screen off timer of Fully).</summary>
    private const int AwakeTimeoutSeconds = 5 * 60;
    /// <summary>Motion keeps coming in; waking the tablet again within this time is not needed, it is still on.</summary>
    private static readonly TimeSpan WakeAgainAfter = TimeSpan.FromMinutes(2);

    private DateTimeOffset _lastWake = DateTimeOffset.MinValue;

    public TabletScreen(
        IHaContext ha,
        ILogger<TabletScreen> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        // Wake up
        Entities.BinarySensor.Motionwoonkamer.StateChanges()
            .Where(x => x.Old.IsOff() && x.New.IsOn())
            .Subscribe(_ => Wake("beweging in de woonkamer"));
        Entities.InputBoolean.Awayvincent.StateChanges().Where(x => x.Old.IsOn() && x.New.IsOff()).Subscribe(_ => Wake("Vincent komt thuis", force: true));
        Entities.InputBoolean.Awaycarleen.StateChanges().Where(x => x.Old.IsOn() && x.New.IsOff()).Subscribe(_ => Wake("Carleen komt thuis", force: true));
        WakeAtWakeUpTimes();
        WakeOnImportantAlerts();

        // Screen off
        Entities.MediaPlayer.Tv.StateChanges()
            .Where(x => !IsTvOnState(x.Old?.State) && IsTvOnState(x.New?.State))
            .Subscribe(_ => ScreenOff("de tv gaat aan"));
        Entities.InputBoolean.Sleepingvincent.StateChanges().Where(x => x.New.IsOn()).Subscribe(_ => ScreenOffIfAllAsleep());
        Entities.InputBoolean.Sleepingcarleen.StateChanges().Where(x => x.New.IsOn()).Subscribe(_ => ScreenOffIfAllAsleep());
    }

    private static bool IsTvOnState(string? state) => state is "on" or "playing" or "paused" or "idle";

    private bool IsTvOn => IsTvOnState(Entities.MediaPlayer.Tv.State);

    /// <summary>
    /// At the wake-up time of whoever has an office or home work day (from their work schedule), the tablet comes on too.
    /// This is the moment the alarm goes, so it also wakes while everyone still "sleeps".
    /// </summary>
    private void WakeAtWakeUpTimes()
    {
        foreach (var (schedule, name) in new[] { (VincentSchedule, "Vincent"), (CarleenSchedule, "Carleen") })
        {
            Scheduler.RunDailyAt(schedule.WakeUpOffice, Models.Persons.WorkSchedule.DefaultWakeUpOffice, () =>
            {
                if (schedule.IsOfficeDay(Scheduler.NowNl().DayOfWeek)) Wake($"wekker van {name}", force: true, evenWhenAsleep: true);
            });
            Scheduler.RunDailyAt(schedule.WakeUpHomeWork, new TimeSpan(7, 45, 0), () =>
            {
                if (schedule.IsHomeWorkDay(Scheduler.NowNl().DayOfWeek)) Wake($"wekker van {name}", force: true, evenWhenAsleep: true);
            });
        }
    }

    /// <summary>The alerts that also get a card on Home: when one starts, the tablet comes on to show it.</summary>
    private void WakeOnImportantAlerts()
    {
        Entities.Sensor.PixelMedicijnen.StateChanges()
            .Where(x => x.New?.State is "nodig" or "vergeten" && x.Old?.State != x.New?.State)
            .Subscribe(x => Wake($"Pixel: prednison {x.New?.State}", force: true));
        Entities.Sensor.HoogStroomverbruik.StateChanges()
            .Where(x => x.New?.State == "on" && x.Old?.State != "on")
            .Subscribe(_ => Wake("hoog stroomverbruik", force: true));
        Entities.BinarySensor.WeerThuisWaarschuwingActief.StateChanges()
            .Where(x => x.Old.IsOff() && x.New.IsOn())
            .Subscribe(_ => Wake("weerwaarschuwing", force: true));
        Entities.Sensor.AnwbElectricityAllInPriceCurrent.StateChanges()
            .Where(x => !IsExtremePrice(x.Old?.State) && IsExtremePrice(x.New?.State))
            .Subscribe(_ => Wake("extreme stroomprijs", force: true));
        Entities.Sensor.BadkamerWasmachineMachinestatus.StateChanges()
            .Where(x => x.Old?.State?.ToLower() is "active" && x.New?.State?.ToLower() is "idle")
            .Subscribe(_ => Wake("de wasmachine is klaar", force: true));
        Entities.BinarySensor.WeerThuisNeerslagVerwacht.StateChanges()
            .Where(x => x.Old.IsOff() && x.New.IsOn() && Entities.InputBoolean.Laundryhangingoutside.IsOn())
            .Subscribe(_ => Wake("regen terwijl de was buiten hangt", force: true));
    }

    /// <summary>Below € 0 or above € 0,55 per kWh, the same limits as the price alert on Home.</summary>
    private static bool IsExtremePrice(double? price) => price is < 0 or > 0.55;

    /// <summary>
    /// Turns the screen on, and with <paramref name="force"/> opens Home. Not while the TV is on, and not while everyone sleeps (unless it is the alarm).
    /// </summary>
    /// <param name="reason">Why, for the log.</param>
    /// <param name="force">Also when the tablet was woken a moment ago (an alert should always show).</param>
    /// <param name="evenWhenAsleep">The alarm: it is the moment to wake up.</param>
    private void Wake(string reason, bool force = false, bool evenWhenAsleep = false)
    {
        if (IsTvOn || (AllAsleep && !evenWhenAsleep) || IsHouseUnattended) return;
        var now = Scheduler.Now;
        if (!force && now - _lastWake < WakeAgainAfter) return;
        _lastWake = now;

        // Motion only turns the screen on: the dashboard is still where it was, so loading the start URL every time somebody walks by
        // is not needed (and reloaded the whole dashboard under their fingers). Only an alert (force) opens Home, so it is in view.
        Logger.LogInformation("Tablet aan: {Reason}", reason);
        // The timer is set once: it stays at this value, so setting it at every wake-up was the same call again and again
        if (!double.TryParse(HaContext.GetState(ScreenOffTimer)?.State, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var timer)
            || Math.Abs(timer - AwakeTimeoutSeconds) > 0.5)
            Entities.Number.TabS6VanVincentSchermUitTimer.SetValue(new NumberSetValueParameters { Value = AwakeTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        Entities.Switch.TabS6VanVincentScherm.TurnOn();
        if (force) Entities.Button.TabS6VanVincentStartUrlLaden.Press();
    }

    private void ScreenOffIfAllAsleep()
    {
        if (AllAsleep) ScreenOff("iedereen slaapt");
    }

    private void ScreenOff(string reason)
    {
        Logger.LogInformation("Tablet uit: {Reason}", reason);
        _lastWake = DateTimeOffset.MinValue;
        Entities.Switch.TabS6VanVincentScherm.TurnOff();
    }
}
