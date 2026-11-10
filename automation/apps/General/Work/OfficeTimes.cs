using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Persons;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Work;

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
    /// <summary>They are at the office this much before their start, while <c>reis_marge_voor_{person}</c> has no value.</summary>
    public static readonly TimeSpan ArriveEarlier = TimeSpan.FromMinutes(10);

    /// <summary>They leave the office this much after their end, while <c>reis_marge_na_{person}</c> has no value.</summary>
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
            foreach (var id in new[] { $"input_number.reis_marge_voor_{person}", $"input_number.reis_marge_na_{person}", $"input_boolean.werk_uit_agenda_{person}", $"input_datetime.reis_ochtend_tijd_{person}", $"input_select.reis_ochtend_soort_{person}",
                         $"input_datetime.reis_avond_tijd_{person}", $"input_select.reis_avond_soort_{person}" })
                new Entity(HaContext, id).StateChanges().Subscribe(_ => Update());
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
        var fromCalendar = HaContext.GetState($"input_boolean.werk_uit_agenda_{person}")?.State == "on";
        for (var offset = 0; offset < 8; offset++)
        {
            var date = now.Date.AddDays(offset);
            if (!schedule.IsOfficeDay(date.DayOfWeek)) continue;

            // The work hours: of the calendar event of that day, or else the person's own (also the basis when the times are not set yet)
            var entry = fromCalendar ? store?.Entries.FirstOrDefault(e => e.Person == person && e.Date == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : null;
            var start = Extensions.SchedulerExtensions.ParseTimeOfDay(entry?.Start) ?? Extensions.SchedulerExtensions.ParseTimeOfDay(schedule.WorkStart.State) ?? DefaultStart;
            var end = Extensions.SchedulerExtensions.ParseTimeOfDay(entry?.End) ?? Extensions.SchedulerExtensions.ParseTimeOfDay(schedule.WorkEnd.State) ?? DefaultEnd;

            // Taken from the calendar: ten minutes before the start and ten minutes after the end. Otherwise the times he or she set.
            var arriveAtOffice = date + start - Margin(person, "voor", ArriveEarlier);
            var leaveOffice = date + end + Margin(person, "na", LeaveLater);
            DateTime? leaveHome = null, arriveHome = null;
            if (!fromCalendar)
            {
                var morning = Extensions.SchedulerExtensions.ParseTimeOfDay(HaContext.GetState($"input_datetime.reis_ochtend_tijd_{person}")?.State);
                var evening = Extensions.SchedulerExtensions.ParseTimeOfDay(HaContext.GetState($"input_datetime.reis_avond_tijd_{person}")?.State);
                if (morning != null)
                {
                    if (HaContext.GetState($"input_select.reis_ochtend_soort_{person}")?.State == OfficeTravel.MorningDepart) leaveHome = date + morning;
                    else arriveAtOffice = date + morning.Value;
                }
                if (evening != null)
                {
                    if (HaContext.GetState($"input_select.reis_avond_soort_{person}")?.State == OfficeTravel.EveningArrive) arriveHome = date + evening;
                    else leaveOffice = date + evening.Value;
                }
            }

            // Once the evening time has passed, the next office day is the one to show
            var done = arriveHome ?? leaveOffice;
            if (offset == 0 && now.DateTime >= done) continue;

            Set($"input_datetime.kantoor_aankomst_{person}", arriveAtOffice);
            Set($"input_datetime.kantoor_vertrek_{person}", leaveOffice);
            if (leaveHome != null) Set($"input_datetime.thuis_vertrek_{person}", leaveHome.Value);
            if (arriveHome != null) Set($"input_datetime.thuis_aankomst_{person}", arriveHome.Value);
            return;
        }
    }

    /// <summary>The margin the person set (in minutes), or the default.</summary>
    private TimeSpan Margin(string person, string which, TimeSpan fallback) =>
        double.TryParse(HaContext.GetState($"input_number.reis_marge_{which}_{person}")?.State, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) && minutes >= 0
            ? TimeSpan.FromMinutes(minutes)
            : fallback;

    private void Set(string entityId, DateTime time)
    {
        var text = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (HaContext.GetState(entityId)?.State == text) return;
        HaContext.CallService("input_datetime", "set_datetime", ServiceTarget.FromEntity(entityId), new { datetime = text });
    }
}
