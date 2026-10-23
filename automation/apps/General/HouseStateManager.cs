using System.Globalization;
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
        foreach (var schedule in new[] { VincentSchedule, CarleenSchedule })
        {
            // The times come from input_datetimes and are rescheduled when changed in Home Assistant
            Scheduler.RunDailyAt(schedule.WorkStart, DefaultWorkStart, () =>
            {
                if (!schedule.IsDayOff(Scheduler.NowNl().DayOfWeek)) schedule.Working.TurnOn();
            });
            Scheduler.RunDailyAt(schedule.WorkEnd, DefaultWorkEnd, () =>
            {
                if (!schedule.IsDayOff(Scheduler.NowNl().DayOfWeek)) schedule.Working.TurnOff();
            });
            schedule.Working.StateChanges().Subscribe(_ => UpdateWorking());
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