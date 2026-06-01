using System.Reactive.Concurrency;
using Automation.Enum;
using static Automation.Globals;

namespace Automation.apps.General;

/// <summary>
/// Manages per-person presence for Vincent and Carleen and derives the house-wide
/// <c>input_boolean.away</c> state from it.
///
/// Responsibilities:
/// <list type="bullet">
/// <item>Sets <c>input_boolean.awayvincent</c> / <c>input_boolean.awaycarleen</c> based on
/// <c>person.*</c> state and Vincent's phone distance (auto-away).</item>
/// <item>Derives <c>input_boolean.away</c> = awayvincent AND awaycarleen (both away).</item>
/// <item>Executes per-scenario actions (see <see cref="PresenceScenario"/>).</item>
/// </list>
///
/// The welcome-home sequence remains in <see cref="AwayManager"/>, driven off the derived
/// <c>away</c> boolean.
/// </summary>
[NetDaemonApp(Id = nameof(PresenceManager))]
public class PresenceManager : BaseApp
{
    private IDisposable? _carleenWakeUpSchedule;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresenceManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public PresenceManager(
        IHaContext ha,
        ILogger<PresenceManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        TrackPersonPresence();
        AutoAway();
        DeriveHouseAway();
        HandlePerPersonTransitions();
    }

    /// <summary>
    /// Turns the per-person away booleans off when a person arrives home and on when they leave.
    /// </summary>
    private void TrackPersonPresence()
    {
        Entities.Person.VincentMaarschalkerweerd
            .StateChanges()
            .Where(x => x.New?.State == "home" && Entities.InputBoolean.Awayvincent.IsOn())
            .Subscribe(_ => Entities.InputBoolean.Awayvincent.TurnOff());

        Entities.Person.Carleen
            .StateChanges()
            .Where(x => x.New?.State == "home" && Entities.InputBoolean.Awaycarleen.IsOn())
            .Subscribe(_ => Entities.InputBoolean.Awaycarleen.TurnOff());

        Entities.Person.Carleen
            .StateChanges()
            .Where(x => x.Old?.State == "home" &&
                        x.New?.State != "home" &&
                        Entities.InputBoolean.Awaycarleen.IsOff())
            .Subscribe(_ => Entities.InputBoolean.Awaycarleen.TurnOn());
    }

    /// <summary>
    /// Automatically marks Vincent as away based on his phone distance and direction of travel.
    /// </summary>
    private void AutoAway()
    {
        Entities.Sensor.ThuisSmS938bDistance.StateChanges()
            .WhenStateIsFor(x => x?.State > 300, TimeSpan.FromMinutes(5), Scheduler)
            .Subscribe(_ =>
            {
                if (Vincent.DirectionOfTravel is "away_from" or "stationary" &&
                    Entities.InputBoolean.Awayvincent.IsOff() &&
                    Entities.Zone.Boodschappen.IsOff())
                    Entities.InputBoolean.Awayvincent.TurnOn();
            });
    }

    /// <summary>
    /// Derives the house-wide <c>away</c> boolean: on when both people are away, off otherwise.
    /// </summary>
    private void DeriveHouseAway()
    {
        Entities.InputBoolean.Awayvincent.StateChanges().Subscribe(_ => UpdateHouseAway());
        Entities.InputBoolean.Awaycarleen.StateChanges().Subscribe(_ => UpdateHouseAway());
    }

    /// <summary>
    /// Recomputes <c>input_boolean.away</c> from the two per-person booleans.
    /// </summary>
    private void UpdateHouseAway()
    {
        var bothAway = Entities.InputBoolean.Awayvincent.IsOn() && Entities.InputBoolean.Awaycarleen.IsOn();

        if (bothAway && Entities.InputBoolean.Away.IsOff())
            Entities.InputBoolean.Away.TurnOn();
        else if (!bothAway && Entities.InputBoolean.Away.IsOn())
            Entities.InputBoolean.Away.TurnOff();
    }

    /// <summary>
    /// Wires up per-person away transitions to their scenario actions.
    /// </summary>
    private void HandlePerPersonTransitions()
    {
        // Vincent leaving: notify him and (if Carleen is home sleeping) schedule her wake-up.
        Entities.InputBoolean.Awayvincent.WhenTurnsOn(_ => OnVincentLeaves());

        // Both away: turn everything off. The departure notification was already sent above.
        Entities.InputBoolean.Away.WhenTurnsOn(_ => OnBothAway());
    }

    /// <summary>
    /// Actions when Vincent leaves home. Sends a context-aware notification and, when Carleen is
    /// home and sleeping, schedules her wake-up at 09:00.
    /// </summary>
    private void OnVincentLeaves()
    {
        Logger.LogInformation("Presence: Vincent left ({Scenario})", GetScenario());

        if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek)
            && DateTimeOffset.Now.Hour < 9
            && Entities.InputBoolean.Holliday.IsOff())
            Notify.NotifyPhoneVincent("Werkse Vincent", "Succes op kantoor :)", false, 5);
        else
            Notify.NotifyPhoneVincent("Tot ziens", "Je laat je huis weer alleen :(", false, 5);

        if (Carleen.IsHome && Carleen.IsSleeping)
            ScheduleCarleenWakeUp();
    }

    /// <summary>
    /// Actions when both people are away: turn off all lights and entertainment.
    /// </summary>
    private void OnBothAway()
    {
        Logger.LogInformation("Presence: both away — turning everything off");
        Entities.Light.TurnAllOff();
        Entities.MediaPlayer.Tv.TurnOff();
        Entities.MediaPlayer.AvSoundbar.TurnOff();
    }

    /// <summary>
    /// Schedules Carleen's sleeping boolean to turn off at 09:00. Cancels any previous schedule.
    /// </summary>
    private void ScheduleCarleenWakeUp()
    {
        _carleenWakeUpSchedule?.Dispose();

        var now = DateTimeOffset.Now;
        var wakeUpTime = now.Date.AddHours(9);
        if (wakeUpTime <= now)
            wakeUpTime = wakeUpTime.AddDays(1);

        var delay = wakeUpTime - now;
        _carleenWakeUpSchedule = Scheduler.Schedule(delay, () =>
        {
            if (Carleen.IsHome && Carleen.IsSleeping)
            {
                Logger.LogInformation("Scheduled 09:00 wake-up: setting Carleen sleeping off");
                Entities.InputBoolean.Sleepingcarleen.TurnOff();
            }
        });

        Logger.LogInformation("Scheduled Carleen wake-up at 09:00 (in {Delay})", delay);
    }

    /// <summary>
    /// Determines the current presence scenario from the per-person away booleans.
    /// </summary>
    /// <returns>The current <see cref="PresenceScenario"/>.</returns>
    private PresenceScenario GetScenario()
    {
        var vincentAway = Entities.InputBoolean.Awayvincent.IsOn();
        var carleenAway = Entities.InputBoolean.Awaycarleen.IsOn();

        return (vincentAway, carleenAway) switch
        {
            (false, false) => PresenceScenario.BothHome,
            (true, false) => PresenceScenario.VincentAwayOnly,
            (false, true) => PresenceScenario.CarleenAwayOnly,
            (true, true) => PresenceScenario.BothAway
        };
    }
}
