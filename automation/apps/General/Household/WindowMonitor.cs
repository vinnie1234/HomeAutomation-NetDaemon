using System.Globalization;
using System.Reactive.Concurrency;
using System.Text.Json;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Ventilation;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Household;

/// <summary>
/// Keeps track of which windows are open and for how long, advises whether the windows can be opened, and warns by phone when an open
/// window is a risk. There are no window sensors: someone presses <c>input_boolean.raam_{window}</c> on the dashboard when a window is opened
/// and again when it is closed (on = open; since when is the <c>last_changed</c> of that helper, which survives a restart).
/// </summary>
/// <remarks>
/// Published for the dashboard: <c>sensor.raam_advies</c> (the advice of <see cref="WindowAdvice"/> with its reasons),
/// <c>sensor.raam_open_aantal</c> (the open windows and since when) and <c>sensor.raam_open_vandaag_minuten</c> (today's minutes
/// open, per window, with the longest opening and the last days). The phone gets a message once per opening for each risk: rain, wind or a
/// warning, the dark, and a window that has been open too long. The risks are the reasons the advice gives to keep the windows closed
/// (<see cref="WindowAdvice.Risks"/>).
/// </remarks>
[NetDaemonApp(Id = nameof(WindowMonitor))]
public class WindowMonitor : BaseApp
{
    /// <summary>The advice (state: the verdict in words; attributes: key, reasons, minutes and the water in the air).</summary>
    public const string AdviceSensor = "sensor.raam_advies";

    /// <summary>The number of open windows (attributes: which ones and since when).</summary>
    public const string OpenSensor = "sensor.raam_open_aantal";

    /// <summary>The minutes the windows were open today (attributes: per window, the longest and the last days).</summary>
    public const string TodaySensor = "sensor.raam_open_vandaag_minuten";

    /// <summary>The windows: the key (in the id of the helper) and the name. Two in the living room, one in the bedroom.</summary>
    public static readonly IReadOnlyList<(string Key, string Name)> WindowList =
        [("woonkamer_tv", "Woonkamer bij de tv"), ("woonkamer_tafel", "Woonkamer bij de tafel"), ("slaapkamer", "Slaapkamer")];

    /// <summary>A window open this long is a risk.</summary>
    public static readonly TimeSpan LongOpen = TimeSpan.FromHours(2);

    /// <summary>A window open this long is a risk when it is cold outside (the house cools down).</summary>
    public static readonly TimeSpan LongOpenCold = TimeSpan.FromMinutes(30);

    /// <summary>The days that are kept.</summary>
    public const int DaysKept = 8;

    private const string StorageKey = "WindowsDays";
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    private static readonly string[] WatchedIds =
    [
        "sensor.weer_thuis_temperatuur", "sensor.weer_thuis_luchtvochtigheid", "sensor.weer_thuis_windstoten",
        "binary_sensor.weer_thuis_neerslag_verwacht", "binary_sensor.weer_thuis_waarschuwing_actief", "sun.sun",
        "sensor.luchtontvochtiger_temperatuur", "sensor.luchtontvochtiger_luchtvochtigheid", "humidifier.luchtontvochtiger"
    ];

    private readonly IDataRepository _storage;
    private readonly IEntityManager _entityManager;
    private readonly Lock _lock = new();
    private readonly List<WindowDay> _days;
    private readonly HashSet<string> _warned = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowMonitor"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="config">The application settings.</param>
    /// <param name="storage">The data repository.</param>
    /// <param name="entityManager">The entity manager that publishes the sensors.</param>
    public WindowMonitor(
        IHaContext ha,
        ILogger<WindowMonitor> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IDataRepository storage,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
        _entityManager = entityManager;
        _days = _storage.Get<List<WindowDay>>(StorageKey) ?? [];

        foreach (var (key, name) in WindowList)
        {
            new InputBooleanEntity(HaContext, Helper(key)).StateChanges()
                .Where(x => x.Old?.State is "on" or "off" && x.New?.State is "on" or "off")
                .Subscribe(x => Toggled(key, name, x.Old!, x.New!));
        }

        // The weather and the air change slowly and in steps: look again a moment after the last change, and every minute for the clock
        HaContext.StateAllChanges()
            .Where(x => WatchedIds.Contains(x.Entity.EntityId))
            .Throttle(Settle, Scheduler)
            .Subscribe(_ => Evaluate());
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), Evaluate);

        _ = CreateSensorsAsync();
    }

    /// <summary>
    /// Gets the id of the helper of a window.
    /// </summary>
    /// <param name="key">The key of the window.</param>
    /// <returns>The entity id.</returns>
    public static string Helper(string key) => $"input_boolean.raam_{key}";

    private async Task CreateSensorsAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(AdviceSensor))
                await _entityManager.CreateAsync(AdviceSensor, new EntityCreationOptions { Name = "Raamadvies", Icon = "mdi:window-open-variant" });
            if (!_entityManager.EntityExists(OpenSensor))
                await _entityManager.CreateAsync(OpenSensor, new EntityCreationOptions { Name = "Ramen open", Icon = "mdi:window-open" });
            if (!_entityManager.EntityExists(TodaySensor))
                await _entityManager.CreateAsync(TodaySensor, new EntityCreationOptions { Name = "Ramen open vandaag", Icon = "mdi:timer-outline", UnitOfMeasurement = "min" });
            Evaluate();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create the window sensors");
        }
    }

    /// <summary>
    /// A window was opened or closed. When it is closed, the time it was open is added to the day(s).
    /// </summary>
    private void Toggled(string key, string name, EntityState old, EntityState @new)
    {
        try
        {
            if (@new.State == "on")
            {
                _warned.RemoveWhere(w => w.StartsWith(key + ":", StringComparison.Ordinal));
                Logger.LogInformation("Window {Window} opened", name);
            }
            else if (old.State == "on")
            {
                var opened = Since(old) ?? Scheduler.Now;
                lock (_lock)
                    Credit(key, opened, Scheduler.Now);
                Logger.LogInformation("Window {Window} closed after {Minutes:0} minutes", name, (Scheduler.Now - opened).TotalMinutes);
            }

            Evaluate();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling the window {Window}", name);
        }
    }

    /// <summary>
    /// Works out the advice and the numbers, publishes them and sends the phone messages that are due.
    /// </summary>
    private void Evaluate()
    {
        try
        {
            var now = Scheduler.Now;
            var open = WindowList.Where(r => HaContext.GetState(Helper(r.Key))?.State == "on")
                .Select(r => (r.Key, r.Name, Since: Since(HaContext.GetState(Helper(r.Key))) ?? now)).ToList();
            var conditions = Conditions();
            var advice = WindowAdvice.Advise(conditions);

            Publish(advice, open, now);
            WarnAboutRisks(open, conditions, now);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error working out the window advice");
        }
    }

    private WindowConditions Conditions()
    {
        var target = HaContext.GetState("humidifier.luchtontvochtiger") is { State: "on" or "off" } humidifier
            ? Number(Attribute(humidifier, "humidity"))
            : null;
        return new WindowConditions(
            OutdoorTemperature: Number(HaContext.GetState("sensor.weer_thuis_temperatuur")?.State),
            OutdoorHumidity: Number(HaContext.GetState("sensor.weer_thuis_luchtvochtigheid")?.State),
            GustSpeed: Number(HaContext.GetState("sensor.weer_thuis_windstoten")?.State),
            RainExpected: HaContext.GetState("binary_sensor.weer_thuis_neerslag_verwacht")?.State == "on",
            WarningActive: HaContext.GetState("binary_sensor.weer_thuis_waarschuwing_actief")?.State == "on",
            IsDark: HaContext.GetState("sun.sun")?.State == "below_horizon",
            IndoorTemperature: Number(HaContext.GetState("sensor.luchtontvochtiger_temperatuur")?.State),
            IndoorHumidity: Number(HaContext.GetState("sensor.luchtontvochtiger_luchtvochtigheid")?.State),
            TargetHumidity: target);
    }

    private void Publish(WindowAdviceResult advice, List<(string Key, string Name, DateTimeOffset Since)> open, DateTimeOffset now)
    {
        _entityManager.SetState(AdviceSensor, advice.Title, new
        {
            sleutel = advice.Key,
            redenen = advice.Reasons,
            minuten = advice.Minutes,
            buiten_absoluut = advice.OutdoorAbsoluteHumidity is { } o ? Math.Round(o, 1) : (double?)null,
            binnen_absoluut = advice.IndoorAbsoluteHumidity is { } i ? Math.Round(i, 1) : (double?)null
        });

        _entityManager.SetState(OpenSensor, open.Count, new
        {
            ramen = open.Select(w => new
            {
                sleutel = w.Key,
                raam = w.Name,
                sinds = w.Since.ToString("o", CultureInfo.InvariantCulture),
                minuten = Math.Round((now - w.Since).TotalMinutes)
            })
        });

        var today = DayKey(now);
        Dictionary<string, double> perRoom;
        double longest;
        List<object> week;
        lock (_lock)
        {
            var stored = _days.FirstOrDefault(d => d.Date == today);
            perRoom = WindowList.ToDictionary(r => r.Key, r => stored?.Minutes.GetValueOrDefault(r.Key) ?? 0);
            longest = stored?.Longest ?? 0;
            week = _days.Where(d => d.Date != today).OrderByDescending(d => d.Date).Take(7)
                .Select(d => (object)new { datum = d.Date, minuten = Math.Round(d.Minutes.Values.Sum()), langste = Math.Round(d.Longest) }).ToList();
        }

        // A window that is open now counts too, from the start of today
        var midnight = DayStart(now);
        foreach (var w in open)
            perRoom[w.Key] += (now - (w.Since > midnight ? w.Since : midnight)).TotalMinutes;
        longest = Math.Max(longest, open.Select(w => (now - w.Since).TotalMinutes).DefaultIfEmpty(0).Max());

        _entityManager.SetState(TodaySensor, Math.Round(perRoom.Values.Sum()), new
        {
            per_raam = perRoom.ToDictionary(p => p.Key, p => Math.Round(p.Value)),
            langste_minuten = Math.Round(longest),
            week
        });
    }

    /// <summary>
    /// Sends a message for each risk an open window runs, once per opening.
    /// </summary>
    private void WarnAboutRisks(List<(string Key, string Name, DateTimeOffset Since)> open, WindowConditions c, DateTimeOffset now)
    {
        var cold = c.OutdoorTemperature is <= WindowAdvice.ColdLimit;
        foreach (var (key, name, since) in open)
        {
            var openFor = now - since;
            var minutes = Math.Round(openFor.TotalMinutes);

            // The reasons the advice gives to keep the windows closed, and a window that has been open too long
            var risks = WindowAdvice.Risks(c).Select(r => (r.Kind, Text: char.ToLowerInvariant(r.Reason[0]) + r.Reason[1..])).ToList();
            if (openFor >= (cold ? LongOpenCold : LongOpen))
                risks.Add(("lang", cold ? $"het staat al {minutes} minuten open en het is koud buiten" : $"het staat al {minutes} minuten open"));

            foreach (var (kind, text) in risks)
                if (_warned.Add($"{key}:{kind}"))
                    Notify.NotifyPeopleHome("Raam open", $"{name} staat open en {text}. Doe het raam dicht en druk het op het dashboard af.", true);
        }
    }

    /// <summary>
    /// Adds the time a window was open to the days it was open on, split at midnight (Dutch time).
    /// </summary>
    private void Credit(string key, DateTimeOffset from, DateTimeOffset to)
    {
        var duration = (to - from).TotalMinutes;
        var start = from;
        while (start < to)
        {
            var next = DayStart(start).AddDays(1);
            var end = next < to ? next : to;
            AddToDay(DayKey(start), key, (end - start).TotalMinutes, 0);
            start = end;
        }

        // The longest opening counts on the day it ended
        AddToDay(DayKey(to), key, 0, duration);
        var oldest = DayKey(to.AddDays(-DaysKept));
        _days.RemoveAll(d => string.CompareOrdinal(d.Date, oldest) <= 0);
        _storage.Save(StorageKey, _days);
    }

    private void AddToDay(string date, string key, double minutes, double longest)
    {
        var index = _days.FindIndex(d => d.Date == date);
        var day = index >= 0 ? _days[index] : new WindowDay { Date = date };
        var perRoom = new Dictionary<string, double>(day.Minutes);
        perRoom[key] = perRoom.GetValueOrDefault(key) + minutes;
        var updated = day with { Minutes = perRoom, Longest = Math.Max(day.Longest, longest) };
        if (index >= 0)
            _days[index] = updated;
        else
            _days.Add(updated);
    }

    /// <summary>The start of the Dutch day of a moment.</summary>
    private static DateTimeOffset DayStart(DateTimeOffset moment)
    {
        var midnight = Automation.Extensions.SchedulerExtensions.ToDutchTime(moment).Date;
        return new DateTimeOffset(midnight, Automation.Extensions.SchedulerExtensions.DutchTimeZone.GetUtcOffset(midnight));
    }

    private static string DayKey(DateTimeOffset moment) => Automation.Extensions.SchedulerExtensions.ToDutchTime(moment).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTimeOffset? Since(EntityState? state) => state?.LastChanged is { } changed ? changed : null;

    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string? Attribute(EntityState state, string name) =>
        state.AttributesJson is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()
            : null;
}
