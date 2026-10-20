using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages holiday states and notifications.
/// </summary>
[NetDaemonApp(Id = nameof(HolidayManager))]
public class HolidayManager : BaseApp
{
    /// <summary>Whether the holiday was turned on by the calendar check (and may be turned off by it).</summary>
    private const string HolidaySetByCalendarKey = "HolidaySetByCalendar";

    private readonly IDataRepository _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="HolidayManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="storage">The data repository, remembering whether the calendar turned the holiday on.</param>
    public HolidayManager(
        IHaContext ha,
        ILogger<HolidayManager> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository storage,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
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
        if (GetNextAlarmStatus() != "set") return;

        var attributesJson = Entities.Sensor.HubVincentAlarms.EntityState?.AttributesJson;
        if (attributesJson?.TryGetProperty("alarms", out var alarmsProp) != true) return;

        var alarmList = alarmsProp.EnumerateArray()
            .Select(o => o.Deserialize<AlarmStateModel>())
            .ToList();

        var firstAlarm = alarmList.Where(x => x?.Status == "set").MinBy(x => x?.LocalTime);
        Notify.NotifyPhoneVincent("WEKKER UITZETTEN",
            $"Je moet je wekker nog uit zetten voor {firstAlarm?.LocalTime ?? ""}", true);

        Logger.LogDebug("Send reminder for disable alarm");
    }

    /// <summary>
    /// Ends the holiday state and sends a reminder to enable alarms.
    /// </summary>
    private void SetEndHoliday()
    {
        if (GetNextAlarmStatus() != "inactive") return;

        Notify.NotifyPhoneVincent("WEKKER AANZETTEN", "Helaas moet je je wekker nog aanzetten :(", true);
        Logger.LogDebug("Send reminder for enable alarm");
    }

    /// <summary>
    /// Reads the alarm hub's "next_alarm_status" attribute directly from the raw attributes JSON, since it's
    /// only present on the device when an alarm is actually set and therefore isn't part of the generated
    /// <see cref="SensorAttributes"/> model.
    /// </summary>
    private string? GetNextAlarmStatus() =>
        Entities.Sensor.HubVincentAlarms.EntityState?.AttributesJson?.TryGetProperty("next_alarm_status", out var status) == true
            ? status.GetString()
            : null;

    /// <summary>
    /// Checks the calendar for holidays and updates the holiday state accordingly.
    /// Both the event title (<c>message</c>) and its <c>description</c> of the current event are inspected.
    /// When the calendar turned the holiday on, it also turns it off again once the day off is over;
    /// a holiday turned on by hand is left alone.
    /// </summary>
    private void CheckCalenderForHoliday()
    {
        // At 00:05 rather than 00:00: an all-day event only turns the calendar on just after midnight
        Scheduler.ScheduleCronNl("05 00 * * *", () =>
        {
            var calendar = Entities.Calendar.VincentmaarschalkerweerdGmailCom;

            // The attributes also describe the next upcoming event, so only an event that is active now counts
            var isDayOff = calendar.State == "on" && IsDayOffEvent(calendar.EntityState?.AttributesJson);
            var setByCalendar = _storage.Get<string>(HolidaySetByCalendarKey) == bool.TrueString;

            if (isDayOff)
            {
                if (Entities.InputBoolean.Holliday.IsOn()) return;

                Entities.InputBoolean.Holliday.TurnOn();
                _storage.Save(HolidaySetByCalendarKey, bool.TrueString);
            }
            else if (setByCalendar)
            {
                Entities.InputBoolean.Holliday.TurnOff();
                _storage.Save(HolidaySetByCalendarKey, bool.FalseString);
            }
        });
    }

    /// <summary>
    /// Whether the calendar event is a day off: its title or description contains the word "vrij" or "vakantie".
    /// Whole words only, so e.g. "vrijdagborrel" doesn't count.
    /// </summary>
    private static bool IsDayOffEvent(JsonElement? attributesJson)
    {
        var text = $"{GetCalendarAttribute(attributesJson, "message")} {GetCalendarAttribute(attributesJson, "description")}";
        return Regex.IsMatch(text, @"\b(vrij|vakantie)\b", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Reads a string attribute from the calendar's raw attributes JSON; these are only present while an event is active.
    /// </summary>
    private static string? GetCalendarAttribute(JsonElement? attributesJson, string name) =>
        attributesJson?.TryGetProperty(name, out var prop) == true ? prop.GetString() : null;
}