using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Enum;
using Newtonsoft.Json;
using static Automation.Globals;

namespace Automation.apps.General;

[NetDaemonApp(Id = nameof(HouseStateManager))]
public class HouseStateManager : BaseApp
{
    private TimeSpan _nighttimeWeekdays;
    private TimeSpan _nighttimeWeekends;
    private TimeSpan _daytimeWeekend;
    private TimeSpan _daytimeHomeWork;
    private TimeSpan _daytimeOffice;
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
        SetTimes();
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
        SetHouseState(Entities.Sun.Sun.State == "below_horizon" ? HouseState.Evening : HouseState.Day);
    }

    /// <summary>
    /// Sets the times for various house states.
    /// </summary>
    private void SetTimes()
    {
        _nighttimeWeekdays = TimeSpan.Parse(Entities.InputDatetime.Nighttimeweekdays.State ?? "00:00:00", new CultureInfo("nl-Nl"));
        _nighttimeWeekends = TimeSpan.Parse(Entities.InputDatetime.Nighttimeweekends.State ?? "00:30:00", new CultureInfo("nl-Nl"));
        _daytimeWeekend = TimeSpan.Parse(Entities.InputDatetime.Daytimeweekend.State ?? "10:00:00", new CultureInfo("nl-Nl"));
        _daytimeHomeWork = TimeSpan.Parse(Entities.InputDatetime.Daytimehomework.State ?? "08:15:00", new CultureInfo("nl-Nl"));
        _daytimeOffice = TimeSpan.Parse(Entities.InputDatetime.Daytimeoffice.State ?? "07:15:00", new CultureInfo("nl-Nl"));
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
        Scheduler.ScheduleCron("00 00 * * *", () =>
        {
            var alarmsHubJson = JsonConvert.SerializeObject(Entities.Sensor.HubVincentAlarms.Attributes?.Alarms);
            var alarmsHub = JsonConvert.DeserializeObject<List<AlarmStateModel>>(alarmsHubJson);
            var alarmToday =
                alarmsHub?.Find(alarmStateModel =>
                    !string.IsNullOrEmpty(alarmStateModel.LocalTime) &&
                    DateTimeOffset.Parse(alarmStateModel.LocalTime, new CultureInfo("nl-Nl")).Date == DateTimeOffset.Now.Date);

            if (alarmToday is { LocalTime: not null })
                Scheduler.Schedule(DateTimeOffset.Parse(alarmToday.LocalTime, new CultureInfo("nl-Nl")), () =>
                {
                    Logger.LogDebug("Setting schedular for {Time}. Sleeping off from alarm",
                        alarmToday.LocalTime);
                    Entities.InputBoolean.Sleeping.TurnOff();
                });
        });
    }

    /// <summary>
    /// Sets the working state based on the schedule.
    /// </summary>
    private void SetWorking()
    {
        Scheduler.RunDaily(_startWorking, () =>
        {
            if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek) ||
                IsHomeWorkDay(Entities, DateTimeOffset.Now.DayOfWeek) &&
                Entities.InputBoolean.Holliday.IsOff())
                Entities.InputBoolean.Working.TurnOn();
        });

        Scheduler.RunDaily(_endWorking, () =>
        {
            if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek) ||
                IsHomeWorkDay(Entities, DateTimeOffset.Now.DayOfWeek) &&
                Entities.InputBoolean.Holliday.IsOff())
                Entities.InputBoolean.Working.TurnOff();
        });
    }

    /// <summary>
    /// Sets the daytime state based on the schedule.
    /// </summary>
    private void SetDayTime()
    {
        Scheduler.RunDaily(_daytimeOffice, () =>
        {
            if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek) &&
                Entities.InputBoolean.Holliday.IsOff())
                SetHouseState(HouseState.Day);
        });
        Scheduler.RunDaily(_daytimeHomeWork, () =>
        {
            if (IsHomeWorkDay(Entities, DateTimeOffset.Now.DayOfWeek) &&
                Entities.InputBoolean.Holliday.IsOff())
                SetHouseState(HouseState.Day);
        });

        Scheduler.RunDaily(_daytimeWeekend, () =>
        {
            if (WeekendDays.Contains(DateTimeOffset.Now.DayOfWeek) ||
                Entities.InputBoolean.Holliday.IsOn())
                SetHouseState(HouseState.Day);
        });
    }

    /// <summary>
    /// Sets the nighttime state based on the schedule.
    /// </summary>
    private void SetNightTime()
    {
        Entities.InputBoolean.Sleeping.WhenTurnsOn(_ => SetHouseState(HouseState.Night));

        Scheduler.RunDaily(_nighttimeWeekdays, () =>
        {
            if (WeekdayNightDays.Contains(DateTimeOffset.Now.DayOfWeek))
                SetHouseState(HouseState.Night);
        });

        Scheduler.RunDaily(_nighttimeWeekends, () =>
        {
            if (WeekendNightDays.Contains(DateTimeOffset.Now.DayOfWeek))
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
            .Where(change => change.Entity.State == "below_horizon")
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
            .Where(change => change.Entity.State == "above_horizon")
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