using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Persons;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps two entities per person up to date for their next office day: <c>input_datetime.kantoor_aankomst_{person}</c>, when they
/// have to be at the office (ten minutes before they start), and <c>input_datetime.kantoor_vertrek_{person}</c>, when they can leave
/// (ten minutes after they are done). They are the times of the calendar event of that day (see <see cref="WorkFromCalendar"/>) or else
/// the person's work hours (<c>werk_start_{person}</c> and <c>werk_eind_{person}</c>). A 9292 route can take its arrival or departure
/// time from these entities. Once the departure time of today has passed, the entities show the next office day.
/// </summary>
[NetDaemonApp(Id = nameof(OfficeTimes))]
public class OfficeTimes : BaseApp
{
    /// <summary>They are at the office this much before their start.</summary>
    public static readonly TimeSpan ArriveEarlier = TimeSpan.FromMinutes(10);

    /// <summary>They leave the office this much after their end.</summary>
    public static readonly TimeSpan LeaveLater = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan DefaultStart = new(8, 30, 0);
    private static readonly TimeSpan DefaultEnd = new(17, 0, 0);

    private readonly IDataRepository _storage;

    public OfficeTimes(
        IHaContext ha,
        ILogger<OfficeTimes> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository storage,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;

        Update();
        // The day moves on, the settings change and the calendar adds a day: every few minutes is quick enough
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(5), Update);
        foreach (var person in new[] { "vincent", "carleen" })
        {
            var schedule = person == "vincent" ? VincentSchedule : CarleenSchedule;
            foreach (var day in WorkSchedule.DayNames.Keys) schedule.WorkDaySetting(day).StateChanges().Subscribe(_ => Update());
            schedule.WorkStart.StateChanges().Subscribe(_ => Update());
            schedule.WorkEnd.StateChanges().Subscribe(_ => Update());
            schedule.DayOff.StateChanges().Subscribe(_ => Update());
        }
    }

    private void Update()
    {
        try
        {
            Update("vincent", VincentSchedule);
            Update("carleen", CarleenSchedule);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not update the office times");
        }
    }

    private void Update(string person, WorkSchedule schedule)
    {
        var now = Scheduler.NowNl();
        var store = _storage.Get<WorkCalendarStore>(WorkFromCalendar.StoreKey);
        for (var offset = 0; offset < 8; offset++)
        {
            var date = now.Date.AddDays(offset);
            if (!schedule.IsOfficeDay(date.DayOfWeek)) continue;

            var entry = store?.Entries.FirstOrDefault(e => e.Person == person && e.Date == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            var start = Extensions.SchedulerExtensions.ParseTimeOfDay(entry?.Start) ?? Extensions.SchedulerExtensions.ParseTimeOfDay(schedule.WorkStart.State) ?? DefaultStart;
            var end = Extensions.SchedulerExtensions.ParseTimeOfDay(entry?.End) ?? Extensions.SchedulerExtensions.ParseTimeOfDay(schedule.WorkEnd.State) ?? DefaultEnd;
            var leave = date + end + LeaveLater;
            if (offset == 0 && now.DateTime >= leave) continue;

            Set($"input_datetime.kantoor_aankomst_{person}", date + start - ArriveEarlier);
            Set($"input_datetime.kantoor_vertrek_{person}", leave);
            return;
        }
    }

    private void Set(string entityId, DateTime time)
    {
        var text = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (HaContext.GetState(entityId)?.State == text) return;
        HaContext.CallService("input_datetime", "set_datetime", ServiceTarget.FromEntity(entityId), new { datetime = text });
    }
}
