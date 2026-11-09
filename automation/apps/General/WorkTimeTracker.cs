using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Work;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps track of how long Vincent works, from the work laptop: every minute that the laptop is on within the working hours of a working
/// day (with the margin of <see cref="WorkLaptop"/>) is counted, at home, at the office (the zone Werk) or elsewhere. The time is kept per
/// day (<see cref="StorageKey"/>); the sensor <c>sensor.werktijd_vincent</c> has the figures for today, this week and this calendar month
/// and the days, weeks and months before, for the window on Vincent's page. The breaks are taken off per day, see
/// <see cref="WorkTimeReport"/>.
/// </summary>
[NetDaemonApp(Id = nameof(WorkTimeTracker))]
public class WorkTimeTracker : BaseApp
{
    public const string StorageKey = "WorkTime";
    public const string Sensor = "sensor.werktijd_vincent";

    /// <summary>The days kept in the storage.</summary>
    private const int KeepDays = 400;

    /// <summary>How long between two counts at most: a longer gap (NetDaemon was down) is not counted.</summary>
    private static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(90);

    private readonly IDataRepository _storage;
    private readonly IEntityManager _entityManager;
    private readonly Dictionary<string, WorkDay> _days;
    private DateTimeOffset? _lastCounted;
    private int _lastPublishedMinute = -1;

    public WorkTimeTracker(
        IHaContext ha,
        ILogger<WorkTimeTracker> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IDataRepository storage,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
        _entityManager = entityManager;
        _days = storage.Get<Dictionary<string, WorkDay>>(StorageKey) ?? [];

        _ = CreateSensorAsync();
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), Tick);
    }

    private async Task CreateSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(Sensor))
                await _entityManager.Create(Sensor, new EntityCreationOptions { Name = "Werktijd Vincent", Icon = "mdi:timer-outline", UnitOfMeasurement = "h" });
            Publish();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {Sensor}", Sensor);
        }
    }

    /// <summary>Where Vincent is: home, at the office, or elsewhere.</summary>
    private string Place()
    {
        if (Vincent.IsHome) return "home";
        var atOffice = HaContext.GetState("zone.werk")?.AttributesJson is { ValueKind: JsonValueKind.Object } a
                       && a.TryGetProperty("persons", out var persons) && persons.ValueKind == JsonValueKind.Array
                       && persons.EnumerateArray().Any(x => x.GetString() == "person.vincent_maarschalkerweerd");
        return atOffice ? "office" : "other";
    }

    private void Tick()
    {
        try
        {
            var now = Scheduler.NowNl();
            var counting = !VincentSchedule.IsDayOff(now.DayOfWeek) && WorkLaptop.IsOnline(HaContext) && InsideHours(now.DateTime);
            if (!counting)
            {
                // The edge of the end of a stretch: the figures are published once more
                if (_lastCounted != null) Publish();
                _lastCounted = null;
                return;
            }

            if (_lastCounted is { } last)
            {
                var gap = now - last;
                if (gap > TimeSpan.Zero && gap <= MaxGap) Add(now, gap.TotalSeconds);
                else if (gap > MaxGap) Add(now, 0);
            }
            else Add(now, 0);
            _lastCounted = now;

            // Every five minutes is enough for the dashboard; the history of Home Assistant would grow with every minute
            if (now.Minute % 5 == 0 || _lastPublishedMinute < 0) Publish();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not count the work time");
        }
    }

    /// <summary>Within the working hours of today, not further than the margin before the start or after the end.</summary>
    private bool InsideHours(DateTime now)
    {
        var (start, end) = Models.Persons.WorkHours.Today(HaContext, VincentSchedule, "vincent", now);
        return now >= start - WorkLaptop.Margin && now <= end + WorkLaptop.Margin;
    }

    private void Add(DateTimeOffset now, double seconds)
    {
        var key = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!_days.TryGetValue(key, out var day)) _days[key] = day = new WorkDay();
        switch (Place())
        {
            case "home": day.Home += seconds; break;
            case "office": day.Office += seconds; break;
            default: day.Other += seconds; break;
        }
        var time = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        day.First ??= time;
        day.Last = time;

        foreach (var old in _days.Keys.Where(k => string.CompareOrdinal(k, now.AddDays(-KeepDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) < 0).ToList())
            _days.Remove(old);
        _storage.Save(StorageKey, _days);
    }

    private static object Minutes(WorkTotals t) => new { b = t.Gross, p = t.WithBreak, n = t.Net, th = t.Home, ko = t.Office, ov = t.Other };

    private static object Row(WorkPeriod p) => new
    {
        k = p.Key, van = p.From, tot = p.To, b = p.Totals.Gross, p = p.Totals.WithBreak, n = p.Totals.Net,
        th = p.Totals.Home, ko = p.Totals.Office, ov = p.Totals.Other, first = p.First, last = p.Last
    };

    /// <summary>Writes the figures to the sensor: the state is the gross hours of today, the attributes have everything else.</summary>
    private void Publish()
    {
        var now = Scheduler.NowNl();
        var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var days = WorkTimeReport.Days(_days, 40);
        var weeks = WorkTimeReport.Weeks(_days, 14);
        var months = WorkTimeReport.Months(_days, 14);
        var todayTotals = days.FirstOrDefault(d => d.Key == today)?.Totals ?? WorkTotals.Zero;
        var thisWeek = weeks.FirstOrDefault(w => string.CompareOrdinal(w.From, today) <= 0 && string.CompareOrdinal(w.To, today) >= 0)?.Totals ?? WorkTotals.Zero;
        var thisMonth = months.FirstOrDefault(m => m.Key == today[..7])?.Totals ?? WorkTotals.Zero;

        _lastPublishedMinute = now.Minute;
        _entityManager.SetState(Sensor, Math.Round(todayTotals.Gross / 60.0, 1).ToString(CultureInfo.InvariantCulture), new
        {
            bijgewerkt = now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            vandaag = Minutes(todayTotals),
            week = Minutes(thisWeek),
            maand = Minutes(thisMonth),
            dagen = days.Select(Row),
            weken = weeks.Select(Row),
            maanden = months.Select(Row)
        });
    }
}
