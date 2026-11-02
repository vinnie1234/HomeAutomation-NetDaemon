using System.Globalization;
using System.Reactive.Concurrency;
using System.Text.Json;
using System.Text.RegularExpressions;
using Automation.Configuration;
using Automation.Models.Persons;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Puts work days in the work schedule from the calendar, for everyone who switched <c>werk_uit_agenda_{person}</c> on (Carleen from the
/// start). An event called "C: werk" (Carleen) or "V: werk" (Vincent) makes that day Kantoor, with the times of the event when it has any;
/// capitals and spaces do not matter. At 23:50 the events of tomorrow are read; the button <c>sync_agenda_{person}</c> reads today and the
/// next six days right away. At 00:05 a day that was set this way and is over goes back to Vrij. A weekday that was not set from the
/// calendar is left alone.
/// </summary>
[NetDaemonApp(Id = nameof(WorkFromCalendar))]
public class WorkFromCalendar : BaseApp
{
    public const string StoreKey = "WorkFromCalendar";
    public const string CarleenCalendar = "calendar.carleen_vincent";

    private readonly IDataRepository _storage;

    /// <summary>The people whose work can come from the calendar: the helper name, the letter in the title of the event and the schedule.</summary>
    private readonly (string Person, char Letter, WorkSchedule Schedule)[] _people;

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
        _people = [("vincent", 'v', VincentSchedule), ("carleen", 'c', CarleenSchedule)];

        Scheduler.ScheduleCronNl("50 23 * * *", () => _ = SyncAsync(1, 1));
        Scheduler.ScheduleCronNl("05 0 * * *", ResetDaysThatAreOver);
        foreach (var (person, _, _) in _people)
        {
            var name = person;
            // An input_button's state is the time of the last press; creating it at startup gives "unknown", which is not a press
            new Entity(HaContext, $"input_button.sync_agenda_{name}").StateChanges().Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
                .Subscribe(press => _ = SyncAsync(0, 6, name));
        }
        // After a restart the nightly steps may have been missed; both are safe to repeat
        ResetDaysThatAreOver();
        _ = SyncAsync(1, 1);
    }

    /// <summary>Whether the title of an event says "C: werk" (or the letter of somebody else), with any capitals and spaces.</summary>
    public static bool IsWorkEvent(string? summary, char letter = 'c') =>
        summary != null && Regex.IsMatch(summary, $@"^\s*{letter}\s*:\s*werk\b", RegexOptions.IgnoreCase);

    /// <summary>Whether the person's work comes from the calendar: switched on, or (for Carleen) not switched off.</summary>
    private bool FromCalendar(string person)
    {
        var state = HaContext.GetState($"input_boolean.werk_uit_agenda_{person}")?.State;
        return state == "on" || (state == null && person == "carleen");
    }

    /// <summary>Reads the events of the days <paramref name="fromDay"/> to <paramref name="toDay"/> from today and sets those days to Kantoor.</summary>
    private async Task SyncAsync(int fromDay, int toDay, string? onlyPerson = null)
    {
        try
        {
            var today = Scheduler.NowNl().Date;
            var response = await HaContext.CallServiceWithResponseAsync("calendar", "get_events", ServiceTarget.FromEntity(CarleenCalendar),
                new Dictionary<string, object>
                {
                    ["start_date_time"] = today.AddDays(fromDay).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                    ["end_date_time"] = today.AddDays(toDay + 1).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
                });
            if (response is not { } root || !root.TryGetProperty(CarleenCalendar, out var calendar) || !calendar.TryGetProperty("events", out var events)) return;
            var items = events.EnumerateArray().ToList();

            foreach (var (person, letter, schedule) in _people)
            {
                if ((onlyPerson != null && onlyPerson != person) || !FromCalendar(person)) continue;
                for (var day = fromDay; day <= toDay; day++)
                    ApplyDay(person, letter, schedule, today.AddDays(day), items);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not read the work from the calendar");
        }
    }

    private void ApplyDay(string person, char letter, WorkSchedule schedule, DateTime date, List<JsonElement> items)
    {
        TimeSpan? start = null, end = null;
        var found = false;
        foreach (var item in items)
        {
            if (!IsWorkEvent(item.TryGetProperty("summary", out var s) ? s.GetString() : null, letter) || !StartsOn(item, date)) continue;
            found = true;
            if (TimeOf(item, "start") is { } begin && (start == null || begin < start)) start = begin;
            if (TimeOf(item, "end") is { } finish && (end == null || finish > end)) end = finish;
        }
        if (!found) return;

        var store = _storage.Get<WorkCalendarStore>(StoreKey) ?? new WorkCalendarStore();
        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        store.Entries.RemoveAll(e => e.Person == person && e.Date == dateText);
        store.Entries.Add(new WorkCalendarEntry { Person = person, Date = dateText, Start = Format(start), End = Format(end) });
        _storage.Save(StoreKey, store);

        var setting = schedule.WorkDaySetting(date.DayOfWeek);
        if (setting.State != WorkSchedule.Office)
        {
            HaContext.CallService("input_select", "select_option", ServiceTarget.FromEntity(setting.EntityId), new { option = WorkSchedule.Office });
            Logger.LogInformation("{Person} works on {Date} according to the calendar: {Day} is Kantoor", person, dateText, WorkSchedule.DayNames[date.DayOfWeek]);
        }
    }

    /// <summary>Whether an event starts on this date: an all-day event by its date, an event with a time by its date in Dutch time.</summary>
    private static bool StartsOn(JsonElement item, DateTime date)
    {
        if (!item.TryGetProperty("start", out var value) || value.GetString() is not { } text) return false;
        if (!text.Contains('T')) return text == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
               && Extensions.SchedulerExtensions.ToDutchTime(time).Date == date;
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
