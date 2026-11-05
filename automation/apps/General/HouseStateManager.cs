using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Enum;
using Microsoft.Extensions.Options;
using static Automation.Globals;

namespace Automation.apps.General;

[NetDaemonApp(Id = nameof(HouseStateManager))]
public class HouseStateManager : BaseApp
{
    // Used while the corresponding input_datetime has no valid time
    private static readonly TimeSpan DefaultNighttimeWeekdays = new(0, 0, 0);
    private static readonly TimeSpan DefaultNighttimeWeekends = new(0, 30, 0);
    private static readonly TimeSpan DefaultDaytimeWeekend = new(10, 0, 0);
    private static readonly TimeSpan DefaultDaytimeHomeWork = new(8, 15, 0);
    private static readonly TimeSpan DefaultDaytimeOffice = Models.Persons.WorkSchedule.DefaultWakeUpOffice;
    private static readonly TimeSpan DefaultWorkStart = new(8, 30, 0);
    private static readonly TimeSpan DefaultWorkEnd = new(17, 0, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="HouseStateManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="config">The application settings.</param>
    public HouseStateManager(
        IHaContext ha,
        ILogger<HouseStateManager> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        SetSleepingOffFromAlarm();
        SetDayTime();
        SetEveningWhenSunIsDown();
        SetNightTime();
        SetMorningWhenSunIsUp();
        InitHouseStateSceneManagement();
        SetCurrentStates();
        SetWorking();
    }

    /// <summary>
    /// Sets the current house state based on the sun's position.
    /// </summary>
    private void SetCurrentStates()
    {
        var sunState = Entities.Sun.Sun.State;
        if (sunState is not (null or "unknown" or "unavailable"))
            SetHouseState(sunState == "below_horizon" ? HouseState.Evening : HouseState.Day);
    }

    /// <summary>
    /// Initializes the house state scene management.
    /// </summary>
    private void InitHouseStateSceneManagement()
    {
        Entities.Scene.Woonkamerday.StateChanges().Subscribe(_ => SetHouseState(HouseState.Day));
        Entities.Scene.Woonkamerevening.StateChanges().Subscribe(_ => SetHouseState(HouseState.Evening));
        Entities.Scene.Woonkamernight.StateChanges().Subscribe(_ => SetHouseState(HouseState.Night));
        Entities.Scene.Woonkamermorning.StateChanges().Subscribe(_ => SetHouseState(HouseState.Morning));
    }

    /// <summary>
    /// Sets the sleeping state off based on the alarm.
    /// </summary>
    private void SetSleepingOffFromAlarm()
    {
        // Scheduler.ScheduleCron("00 00 * * *", () =>
        // {
        //     var alarmsHubJson = JsonConvert.SerializeObject(Entities.Sensor.HubVincentAlarms.Attributes?.Alarms);
        //     var alarmsHub = JsonConvert.DeserializeObject<List<AlarmStateModel>>(alarmsHubJson);
        //     var alarmToday =
        //         alarmsHub?.Find(alarmStateModel =>
        //             !string.IsNullOrEmpty(alarmStateModel.LocalTime) &&
        //             DateTimeOffset.Parse(alarmStateModel.LocalTime, new CultureInfo("nl-Nl")).Date == DateTimeOffset.Now.Date);
        //
        //     if (alarmToday is { LocalTime: not null })
        //         Scheduler.Schedule(DateTimeOffset.Parse(alarmToday.LocalTime, new CultureInfo("nl-Nl")), () =>
        //         {
        //             Logger.LogDebug("Setting schedular for {Time}. Sleeping off from alarm",
        //                 alarmToday.LocalTime);
        //             Entities.InputBoolean.Sleeping.TurnOff();
        //         });
        // });
    }

    /// <summary>
    /// Sets whether Vincent and Carleen work, each with their own working hours on their office and home work days,
    /// and keeps the shared working state in line: it is on as long as Vincent or Carleen works.
    /// </summary>
    private void SetWorking()
    {
        foreach (var (person, schedule) in new[] { ("vincent", VincentSchedule), ("carleen", CarleenSchedule) })
        {
            // The times come from input_datetimes and are rescheduled when changed in Home Assistant. When the work of today comes
            // from the calendar (its own hours), those hours decide instead (see CheckCalendarWorkHours).
            Scheduler.RunDailyAt(schedule.WorkStart, DefaultWorkStart, () =>
            {
                if (!schedule.IsDayOff(Scheduler.NowNl().DayOfWeek) && CalendarWorkHours(person) == null) schedule.Working.TurnOn();
            });
            Scheduler.RunDailyAt(schedule.WorkEnd, DefaultWorkEnd, () =>
            {
                if (!schedule.IsDayOff(Scheduler.NowNl().DayOfWeek) && CalendarWorkHours(person) == null) schedule.Working.TurnOff();
            });
            schedule.Working.StateChanges().Subscribe(_ => UpdateWorking());
        }
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), CheckCalendarWorkHours);
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), CheckAwayFromWork);
        ReconcileCalendarWork();
    }

    /// <summary>After a start (or a deployment) the working state of today follows the hours from the calendar, in case the edge was missed.</summary>
    private void ReconcileCalendarWork()
    {
        var now = Scheduler.NowNl().DateTime;
        foreach (var (person, schedule) in new[] { ("vincent", VincentSchedule), ("carleen", CarleenSchedule) })
        {
            if (CalendarWorkHours(person) is not { } hours || schedule.IsDayOff(now.DayOfWeek)) continue;
            var shouldWork = now >= hours.Start && now < hours.End;
            if (shouldWork && schedule.Working.IsOff()) schedule.Working.TurnOn();
            else if (!shouldWork && schedule.Working.IsOn()) schedule.Working.TurnOff();
        }
    }

    /// <summary>
    /// The working hours of today when they come from the calendar: from the time to be at the office plus the margin before it, up to the
    /// time to leave minus the margin after it (see <see cref="OfficeTimes"/>). Null when the work does not come from the calendar or today
    /// has no hours from it.
    /// </summary>
    private (DateTime Start, DateTime End)? CalendarWorkHours(string person)
    {
        if (HaContext.GetState($"input_boolean.werk_uit_agenda_{person}")?.State != "on") return null;
        var today = Scheduler.NowNl().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        DateTime? Read(string entity)
        {
            var state = HaContext.GetState($"input_datetime.{entity}_{person}")?.State;
            return state != null && state.StartsWith(today, StringComparison.Ordinal)
                   && DateTime.TryParse(state, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time) ? time : null;
        }
        double Minutes(string helper) =>
            double.TryParse(HaContext.GetState($"input_number.{helper}_{person}")?.State, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 10;

        if (Read("kantoor_aankomst") is not { } arrive || Read("kantoor_vertrek") is not { } leave) return null;
        return (arrive.AddMinutes(Minutes("reis_marge_voor")), leave.AddMinutes(-Minutes("reis_marge_na")));
    }

    /// <summary>How long somebody can be away from the place of work on an office day before they are no longer working.</summary>
    public static readonly TimeSpan AwayFromWorkAfter = TimeSpan.FromHours(1);

    private readonly Dictionary<string, DateTimeOffset> _lastAtWork = [];
    private readonly Dictionary<string, bool> _wasAtWork = [];

    /// <summary>
    /// Whoever has to be at the office today and has been away from the place of work (the zone Werk) for more than an hour, no longer
    /// works; coming back to the place of work before the end of the working hours starts the work again.
    /// </summary>
    private void CheckAwayFromWork()
    {
        try
        {
            var now = Scheduler.NowNl();
            foreach (var (person, personId, zone, schedule) in new[]
                     {
                         ("vincent", "person.vincent_maarschalkerweerd", "zone.werk", VincentSchedule),
                         ("carleen", "person.carleen", "zone.werk_carleen", CarleenSchedule)
                     })
            {
                var atWork = HaContext.GetState(zone)?.AttributesJson is { ValueKind: JsonValueKind.Object } a
                             && a.TryGetProperty("persons", out var persons) && persons.ValueKind == JsonValueKind.Array
                             && persons.EnumerateArray().Any(x => x.GetString() == personId);
                var arrived = atWork && _wasAtWork.TryGetValue(person, out var before) && !before;
                _wasAtWork[person] = atWork;
                if (atWork) _lastAtWork[person] = now;
                if (!schedule.IsOfficeDay(now.DayOfWeek)) continue;

                if (arrived && schedule.Working.IsOff() && now.DateTime < EndOfWork(person, schedule, now.DateTime)) schedule.Working.TurnOn();
                else if (!atWork && schedule.Working.IsOn() && _lastAtWork.TryGetValue(person, out var last)
                         && last.Date == now.Date && now - last > AwayFromWorkAfter)
                    schedule.Working.TurnOff();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not check whether somebody is away from work");
        }
    }

    /// <summary>The end of the working hours of today: from the calendar, or else the person's own.</summary>
    private DateTime EndOfWork(string person, Models.Persons.WorkSchedule schedule, DateTime now) =>
        _calendarHours.TryGetValue(person, out var hours) && hours.Date == now.Date
            ? hours.End
            : now.Date + (Extensions.SchedulerExtensions.ParseTimeOfDay(schedule.WorkEnd.State) ?? DefaultWorkEnd);

    /// <summary>The hours of today from the calendar as they were last read, per person: the entities move on to the next office day as soon as the end has passed.</summary>
    private readonly Dictionary<string, (DateTime Date, DateTime Start, DateTime End)> _calendarHours = [];

    /// <summary>The edges (start and end) that were handled already, per person and day, so a late tick still counts once and a manual switch afterwards stays.</summary>
    private readonly HashSet<string> _handledEdges = [];

    /// <summary>
    /// Every minute: from the start of the hours from the calendar the person works, from their end the person stops. Each happens once
    /// a day, also when the minute of the edge was missed (the entities of the day may have moved on to the next day by then).
    /// </summary>
    private void CheckCalendarWorkHours()
    {
        try
        {
            var now = Scheduler.NowNl().DateTime;
            _handledEdges.RemoveWhere(key => !key.Contains(now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
            foreach (var (person, schedule) in new[] { ("vincent", VincentSchedule), ("carleen", CarleenSchedule) })
            {
                if (CalendarWorkHours(person) is { } read) _calendarHours[person] = (now.Date, read.Start, read.End);
                if (!_calendarHours.TryGetValue(person, out var hours) || hours.Date != now.Date || schedule.IsDayOff(now.DayOfWeek)) continue;

                var day = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                if (now >= hours.Start && now < hours.End && _handledEdges.Add($"{person}|{day}|start") && schedule.Working.IsOff()) schedule.Working.TurnOn();
                if (now >= hours.End && _handledEdges.Add($"{person}|{day}|end") && schedule.Working.IsOn()) schedule.Working.TurnOff();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not check the working hours from the calendar");
        }
    }

    /// <summary>
    /// Turns the shared working state on while Vincent or Carleen works, and off when neither does.
    /// </summary>
    private void UpdateWorking()
    {
        var anyoneWorking = VincentSchedule.Working.IsOn() || CarleenSchedule.Working.IsOn();
        if (anyoneWorking && Entities.InputBoolean.Working.IsOff()) Entities.InputBoolean.Working.TurnOn();
        else if (!anyoneWorking && Entities.InputBoolean.Working.IsOn()) Entities.InputBoolean.Working.TurnOff();
    }

    /// <summary>
    /// Sets the daytime state based on the schedules: at someone's wake-up time for their office or home work day, so
    /// whoever gets up first wakes the house, and at the weekend time when both have a day off.
    /// </summary>
    private void SetDayTime()
    {
        // The times come from input_datetimes and are rescheduled when changed in Home Assistant
        foreach (var schedule in new[] { VincentSchedule, CarleenSchedule })
        {
            Scheduler.RunDailyAt(schedule.WakeUpOffice, DefaultDaytimeOffice, () =>
            {
                if (schedule.IsOfficeDay(Scheduler.NowNl().DayOfWeek))
                    SetHouseState(HouseState.Day);
            });
            Scheduler.RunDailyAt(schedule.WakeUpHomeWork, DefaultDaytimeHomeWork, () =>
            {
                if (schedule.IsHomeWorkDay(Scheduler.NowNl().DayOfWeek))
                    SetHouseState(HouseState.Day);
            });
        }

        Scheduler.RunDailyAt(Entities.InputDatetime.Daytimeweekend, DefaultDaytimeWeekend, () =>
        {
            var today = Scheduler.NowNl().DayOfWeek;
            if (VincentSchedule.IsDayOff(today) && CarleenSchedule.IsDayOff(today))
                SetHouseState(HouseState.Day);
        });
    }

    /// <summary>
    /// Sets the nighttime state based on the schedule.
    /// </summary>
    private void SetNightTime()
    {
        Entities.InputBoolean.Sleepingvincent.WhenTurnsOn(_ => SetHouseState(HouseState.Night));
        Entities.InputBoolean.Sleepingcarleen.WhenTurnsOn(_ => SetHouseState(HouseState.Night));

        Scheduler.RunDailyAt(Entities.InputDatetime.Nighttimeweekdays, DefaultNighttimeWeekdays, () =>
        {
            if (WeekdayNightDays.Contains(Scheduler.NowNl().DayOfWeek))
                SetHouseState(HouseState.Night);
        });

        Scheduler.RunDailyAt(Entities.InputDatetime.Nighttimeweekends, DefaultNighttimeWeekends, () =>
        {
            if (WeekendNightDays.Contains(Scheduler.NowNl().DayOfWeek))
                SetHouseState(HouseState.Night);
        });
    }

    /// <summary>
    /// Sets the evening state when the sun is down.
    /// </summary>
    private void SetEveningWhenSunIsDown()
    {
        Entities.Sun.Sun
            .StateChanges()
            .Where(change => change.Entity.State == "below_horizon"
                          && change.Old?.State is not (null or "unknown" or "unavailable"))
            // Someone already went to sleep before sunset (e.g. in summer): stay in Night
            .Where(_ => GetHouseState(Entities) != HouseState.Night)
            .Subscribe(_ =>
            {
                Logger.LogDebug("Setting current house state to {State}", HouseState.Evening);
                SetHouseState(HouseState.Evening);
            });
    }

    /// <summary>
    /// Sets the morning state when the sun is up.
    /// </summary>
    private void SetMorningWhenSunIsUp()
    {
        Entities.Sun.Sun
            .StateChanges()
            .Where(change => change.Entity.State == "above_horizon"
                          && change.Old?.State is not (null or "unknown" or "unavailable"))
            // The scheduled daytime may already have set Day before sunrise (e.g. office days in winter)
            .Where(_ => GetHouseState(Entities) != HouseState.Day)
            .Subscribe(_ =>
            {
                Logger.LogDebug("Setting current house state to {State}", HouseState.Morning);
                SetHouseState(HouseState.Morning);
            });
    }

    /// <summary>
    /// Sets the house state to the specified state and updates the Home Assistant InputSelect.
    /// </summary>
    /// <param name="state">The state to set.</param>
    private void SetHouseState(HouseState state)
    {
        Logger.LogDebug("Setting current house state to {State}", state);
        var selectState = state
            switch
            {
                HouseState.Morning => "Morning",
                HouseState.Day     => "Day",
                HouseState.Evening => "Evening",
                HouseState.Night   => "Night",
                _                      => throw new ArgumentException("Not supported", nameof(state))
            };
        Entities.InputSelect.Housemodeselect.SelectOption(option: selectState);
    }
}