using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Enum;
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
    private static readonly TimeSpan DefaultDaytimeOffice = new(7, 15, 0);
    private readonly TimeSpan _startWorking = TimeSpan.Parse("08:30:00", new CultureInfo("nl-Nl"));
    private readonly TimeSpan _endWorking = TimeSpan.Parse("17:00:00", new CultureInfo("nl-Nl"));

    /// <summary>
    /// Initializes a new instance of the <see cref="HouseStateManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public HouseStateManager(
        IHaContext ha,
        ILogger<HouseStateManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
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
    /// Sets the working state based on the schedule.
    /// </summary>
    private void SetWorking()
    {
        Scheduler.RunDaily(_startWorking, () =>
        {
            if (IsOfficeDay(Entities, Scheduler.NowNl().DayOfWeek) ||
                IsHomeWorkDay(Entities, Scheduler.NowNl().DayOfWeek))
                Entities.InputBoolean.Working.TurnOn();
        });

        Scheduler.RunDaily(_endWorking, () =>
        {
            if (IsOfficeDay(Entities, Scheduler.NowNl().DayOfWeek) ||
                IsHomeWorkDay(Entities, Scheduler.NowNl().DayOfWeek))
                Entities.InputBoolean.Working.TurnOff();
        });
    }

    /// <summary>
    /// Sets the daytime state based on the schedule.
    /// </summary>
    private void SetDayTime()
    {
        // The times come from input_datetimes and are rescheduled when changed in Home Assistant
        Scheduler.RunDailyAt(Entities.InputDatetime.Daytimeoffice, DefaultDaytimeOffice, () =>
        {
            if (IsOfficeDay(Entities, Scheduler.NowNl().DayOfWeek))
                SetHouseState(HouseState.Day);
        });
        Scheduler.RunDailyAt(Entities.InputDatetime.Daytimehomework, DefaultDaytimeHomeWork, () =>
        {
            if (IsHomeWorkDay(Entities, Scheduler.NowNl().DayOfWeek))
                SetHouseState(HouseState.Day);
        });

        Scheduler.RunDailyAt(Entities.InputDatetime.Daytimeweekend, DefaultDaytimeWeekend, () =>
        {
            if (IsDayOff(Entities, Scheduler.NowNl().DayOfWeek))
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