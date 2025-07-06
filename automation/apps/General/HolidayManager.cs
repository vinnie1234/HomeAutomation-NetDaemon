using System.Reactive.Concurrency;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages holiday states and notifications.
/// </summary>
[NetDaemonApp(Id = nameof(HolidayManager))]
public class HolidayManager : BaseApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HolidayManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public HolidayManager(
        IHaContext ha,
        ILogger<HolidayManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        HolidayChangeStateHandler();
        CheckCalenderForHoliday();
    }

    /// <summary>
    /// Handles state changes for the holiday input boolean.
    /// </summary>
    private void HolidayChangeStateHandler()
    {
        Entities
            .InputBoolean
            .Holliday
            .StateChanges()
            .Where(x =>
                x.Entity.IsOn())
            .Subscribe(_ => SetHoliday());

        Entities
            .InputBoolean
            .Holliday
            .StateChanges()
            .Where(x =>
                x.Entity.IsOff())
            .Subscribe(_ => SetEndHoliday());
    }

    /// <summary>
    /// Sets the holiday state and sends a reminder to disable alarms.
    /// </summary>
    private void SetHoliday()
    {
        if (Entities.Sensor.HubVincentAlarms.Attributes is { NextAlarmStatus: "set", Alarms: not null })
        {
            var alarmList = new List<AlarmStateModel?>();
            var jsonList = Entities.Sensor.HubVincentAlarms.Attributes.Alarms;

            if (jsonList != null)
                alarmList.AddRange(
                    jsonList.Cast<JsonElement>()
                        .Select(o => o.Deserialize<AlarmStateModel>()));

            var firstAlarm = alarmList.Where(x => x?.Status == "set").MinBy(x => x?.LocalTime);
            Notify.NotifyPhoneVincent("WEKKER UITZETTEN",
                $"Je moet je wekker nog uit zetten voor {firstAlarm?.LocalTime ?? ""}", true);

            Logger.LogDebug("Send reminder for disable alarm");
        }
    }

    /// <summary>
    /// Ends the holiday state and sends a reminder to enable alarms.
    /// </summary>
    private void SetEndHoliday()
    {
        if (Entities.Sensor.HubVincentAlarms.Attributes?.NextAlarmStatus == "inactive")
        {
            Notify.NotifyPhoneVincent("WEKKER AANZETTEN", "Helaas moet je je wekker nog aanzetten :(", true);
            Logger.LogDebug("Send reminder for enable alarm");
        }
    }

    /// <summary>
    /// Checks the calendar for holidays and updates the holiday state accordingly.
    /// </summary>
    private void CheckCalenderForHoliday()
    {
        Scheduler.ScheduleCron("00 00 * * *", () =>
        {
            var description = Entities.Calendar.VincentmaarschalkerweerdGmailCom.Attributes?.Description?.ToLower();
            if (description?.Contains("vrij") == true || description?.Contains("vakantie") == true)
                Entities.InputBoolean.Holliday.TurnOn();
        });
    }
}