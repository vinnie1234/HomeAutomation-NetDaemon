using System.Globalization;
using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Automation.Configuration;
using Automation.Models.Persons;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Puts Carleen's work days in her work schedule from the calendar. At 23:50 the events of tomorrow are read: one called
/// "C: werk" (capitals and spaces do not matter) makes her day tomorrow Kantoor, with the times of the event when it has any.
/// At 00:05 a day that was set this way and is over goes back to Vrij. A weekday that was not set from the calendar is left alone.
/// </summary>
[NetDaemonApp(Id = nameof(WorkFromCalendar))]
public partial class WorkFromCalendar : BaseApp
{
    public const string StoreKey = "WorkFromCalendar";
    public const string CarleenCalendar = "calendar.carleen_vincent";

    private readonly IDataRepository _storage;

    /// <summary>"C: werk", with any capitals and spaces around the colon.</summary>
    [GeneratedRegex(@"^\s*c\s*:\s*werk\b", RegexOptions.IgnoreCase)]
    private static partial Regex WorkEventPattern();

    public WorkFromCalendar(
        IHaContext ha,
        ILogger<WorkFromCalendar> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository storage,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;

        Scheduler.ScheduleCronNl("50 23 * * *", () => _ = CheckTomorrowAsync());
        Scheduler.ScheduleCronNl("05 0 * * *", ResetDaysThatAreOver);
        // After a restart the nightly steps may have been missed; both are safe to repeat
        ResetDaysThatAreOver();
        _ = CheckTomorrowAsync();
    }

    /// <summary>Whether the title of an event says "C: werk".</summary>
    public static bool IsWorkEvent(string? summary) => summary != null && WorkEventPattern().IsMatch(summary);

    private async Task CheckTomorrowAsync()
    {
        try
        {
            var tomorrow = Scheduler.NowNl().Date.AddDays(1);
            var response = await HaContext.CallServiceWithResponseAsync("calendar", "get_events", ServiceTarget.FromEntity(CarleenCalendar),
                new Dictionary<string, object>
                {
                    ["start_date_time"] = tomorrow.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                    ["end_date_time"] = tomorrow.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
                });
            if (response is not { } root || !root.TryGetProperty(CarleenCalendar, out var calendar) || !calendar.TryGetProperty("events", out var events)) return;

            TimeSpan? start = null, end = null;
            var found = false;
            foreach (var item in events.EnumerateArray())
            {
                if (!IsWorkEvent(item.TryGetProperty("summary", out var s) ? s.GetString() : null)) continue;
                found = true;
                if (TimeOf(item, "start") is { } from && (start == null || from < start)) start = from;
                if (TimeOf(item, "end") is { } to && (end == null || to > end)) end = to;
            }
            if (!found) return;

            var store = _storage.Get<WorkCalendarStore>(StoreKey) ?? new WorkCalendarStore();
            var date = tomorrow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            store.Entries.RemoveAll(e => e.Person == "carleen" && e.Date == date);
            store.Entries.Add(new WorkCalendarEntry { Person = "carleen", Date = date, Start = Format(start), End = Format(end) });
            _storage.Save(StoreKey, store);

            var setting = CarleenSchedule.WorkDaySetting(tomorrow.DayOfWeek);
            if (setting.State != WorkSchedule.Office)
            {
                HaContext.CallService("input_select", "select_option", ServiceTarget.FromEntity(setting.EntityId), new { option = WorkSchedule.Office });
                Logger.LogInformation("Carleen works tomorrow ({Date}) according to the calendar: {Day} is Kantoor", date, WorkSchedule.DayNames[tomorrow.DayOfWeek]);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not read tomorrow's work from the calendar");
        }
    }

    /// <summary>The time of day of the start or end of an event, in Dutch time; null for an all-day event (a date without a time).</summary>
    private static TimeSpan? TimeOf(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.GetString() is not { } text || !text.Contains('T')) return null;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? Extensions.SchedulerExtensions.ToDutchTime(time).TimeOfDay
            : null;
    }

    private static string? Format(TimeSpan? time) => time?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private void ResetDaysThatAreOver()
    {
        try
        {
            var store = _storage.Get<WorkCalendarStore>(StoreKey);
            if (store == null) return;
            var today = Scheduler.NowNl().Date;
            var over = store.Entries.Where(e => DateTime.TryParse(e.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d < today).ToList();
            foreach (var entry in over)
            {
                var day = DateTime.Parse(entry.Date, CultureInfo.InvariantCulture).DayOfWeek;
                var schedule = entry.Person == "carleen" ? CarleenSchedule : VincentSchedule;
                var setting = schedule.WorkDaySetting(day);
                if (setting.State == WorkSchedule.Office)
                    HaContext.CallService("input_select", "select_option", ServiceTarget.FromEntity(setting.EntityId), new { option = WorkSchedule.Off });
                store.Entries.Remove(entry);
            }
            if (over.Count > 0) _storage.Save(StoreKey, store);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not set the work days from the calendar back to Vrij");
        }
    }
}
