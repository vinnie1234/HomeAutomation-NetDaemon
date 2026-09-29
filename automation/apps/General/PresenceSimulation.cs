using System.Reactive.Concurrency;
using Automation.Helpers;

namespace Automation.apps.General;

/// <summary>
/// Simulates presence against burglars while the house is unattended (on vacation, nobody home and
/// no house sitter). Every evening the living room lights turn on shortly after sunset and the bedroom
/// light later in the evening, each at a somewhat random time, and turn off again at a random time.
/// </summary>
[NetDaemonApp(Id = nameof(PresenceSimulation))]
public class PresenceSimulation : BaseApp
{
    /// <summary>Living room lights turn on up to this long after sunset.</summary>
    private static readonly TimeSpan MaxDelayAfterSunset = TimeSpan.FromMinutes(30);

    /// <summary>Earliest time of day the living room lights turn off; the actual time is up to an hour later.</summary>
    private static readonly TimeSpan EarliestLivingRoomOff = new(22, 30, 0);

    /// <summary>The living room lights are on for at least this long, also when the sun sets late in summer.</summary>
    private static readonly TimeSpan MinLivingRoomOnDuration = TimeSpan.FromHours(1);

    private readonly Random _random = Random.Shared;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresenceSimulation"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for timed tasks.</param>
    public PresenceSimulation(
        IHaContext ha,
        ILogger<PresenceSimulation> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        Entities.Sun.Sun
            .StateChanges()
            .Where(change => change.Entity.State == "below_horizon"
                          && change.Old?.State is not (null or "unknown" or "unavailable"))
            .Subscribe(_ => ScheduleEvening());
    }

    /// <summary>
    /// Schedules tonight's simulated evening, starting at sunset.
    /// </summary>
    private void ScheduleEvening()
    {
        if (!IsHouseUnattended) return;

        var now = Scheduler.Now.LocalDateTime;
        var livingRoomOn = now + RandomBetween(TimeSpan.Zero, MaxDelayAfterSunset);

        var livingRoomOff = now.Date + EarliestLivingRoomOff + RandomBetween(TimeSpan.Zero, TimeSpan.FromHours(1));
        if (livingRoomOff < livingRoomOn + MinLivingRoomOnDuration)
            livingRoomOff = livingRoomOn + MinLivingRoomOnDuration + RandomBetween(TimeSpan.Zero, TimeSpan.FromMinutes(30));

        // Like going to bed: the bedroom light goes on a little before the living room goes dark
        var bedroomOn = livingRoomOff - RandomBetween(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10));
        var bedroomOff = livingRoomOff + RandomBetween(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(40));

        Logger.LogInformation(
            "Presence simulation tonight: living room {LivingRoomOn:HH:mm}-{LivingRoomOff:HH:mm}, bedroom {BedroomOn:HH:mm}-{BedroomOff:HH:mm}",
            livingRoomOn, livingRoomOff, bedroomOn, bedroomOff);

        ScheduleAt(livingRoomOn, () => LightExtension.SetLightSceneWoonkamer(Entities));
        ScheduleAt(bedroomOn, () => Entities.Light.Slaapkamer.TurnOn(brightnessPct: 60));
        ScheduleAt(livingRoomOff, () => Entities.Light.Woonkamer.TurnOff());
        ScheduleAt(bedroomOff, () => Entities.Light.Slaapkamer.TurnOff());
    }

    /// <summary>
    /// Schedules a light action, which only runs when the house is still unattended by then
    /// (so nothing is touched once someone is home again).
    /// </summary>
    private void ScheduleAt(DateTime time, Action action)
    {
        Scheduler.Schedule(time - Scheduler.Now.LocalDateTime, () =>
        {
            if (IsHouseUnattended) action();
        });
    }

    private TimeSpan RandomBetween(TimeSpan min, TimeSpan max) =>
        min + TimeSpan.FromSeconds(_random.NextDouble() * (max - min).TotalSeconds);
}
