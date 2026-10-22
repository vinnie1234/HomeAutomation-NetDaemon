using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Pixel;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps track of Pixel's medicines, prednison (a quarter pill every evening) and antibraak (half a pill when needed).
/// The "gegeven" buttons take the dose off the stock; every dose and every change of the stock is saved forever.
/// <para>
/// The prednison of a day counts as given when a dose was given between 12:00 that day and 12:00 the next day, so a late
/// dose after midnight still belongs to the evening before. From the reminder time (<c>input_datetime.pixel_prednison_tijd</c>,
/// 20:00) the status is <c>nodig</c> until it is given; when a day passed without a dose the status is <c>vergeten</c> until
/// the next dose. Status, stock and history are published as <c>sensor.pixel_medicijnen</c> for the dashboard.
/// </para>
/// <para>
/// An hour after the reminder time Vincent and Carleen get a phone notification when the prednison is still needed, and at
/// 08:00 when it was forgotten; both have a "Gegeven" button. <c>input_button.pixel_medicijn_ongedaan</c> undoes the last
/// dose with a correction line: the dose stays in the history but no longer counts.
/// </para>
/// </summary>
[NetDaemonApp(Id = nameof(PixelMedication))]
public class PixelMedication : BaseApp
{
    public const string SensorId = "sensor.pixel_medicijnen";
    public const string Prednison = "prednison";
    public const string Antibraak = "antibraak";

    private const string HistoryKey = "PixelMedicationHistory";
    private const string TrackingSinceKey = "PixelMedicationSince";
    /// <summary>A stock change by hand is saved once the field has not changed for this long, so typing or clicking makes one line.</summary>
    private static readonly TimeSpan StockSettleTime = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan DefaultReminder = new(20, 0, 0);
    private static readonly TimeSpan DayStartsAt = new(12, 0, 0);
    private static readonly TimeSpan PhoneReminderDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan ForgottenNotificationAt = new(8, 0, 0);
    private const string UndoButton = "input_button.pixel_medicijn_ongedaan";

    /// <summary>Per medicine: the stock helper, the button, the dose in pills and the stock below which it is almost gone.</summary>
    private static readonly IReadOnlyDictionary<string, (string Stock, string Button, double Dose, double Low)> Medicines =
        new Dictionary<string, (string, string, double, double)>
        {
            [Prednison] = ("input_number.pixel_prednison_voorraad", "input_button.pixel_prednison_gegeven", 0.25, 2),
            [Antibraak] = ("input_number.pixel_antibraak_voorraad", "input_button.pixel_antibraak_gegeven", 0.5, 1.5)
        };

    private readonly IDataRepository _storage;
    private readonly IEntityManager _entityManager;
    private readonly Lock _lock = new();
    private readonly List<MedicationEvent> _history;
    private readonly DateTimeOffset _since;

    /// <summary>The stock the app set itself, so a change made by hand can be told apart and logged as a stock change.</summary>
    private readonly Dictionary<string, double> _expectedStock = [];

    /// <summary>Per medicine: the stock before a change by hand started, and the timer that saves it once it settles.</summary>
    private readonly Dictionary<string, (double Before, IDisposable Timer)> _pendingStock = [];

    public PixelMedication(
        IHaContext ha,
        ILogger<PixelMedication> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IDataRepository storage,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
        _entityManager = entityManager;
        _history = _storage.Get<List<MedicationEvent>>(HistoryKey) ?? [];

        // From when there is something to check: no "forgotten" for the days before the app ran
        var since = _storage.Get<string>(TrackingSinceKey);
        if (since == null || !DateTimeOffset.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.None, out _since))
        {
            _since = Scheduler.NowNl();
            _storage.Save(TrackingSinceKey, _since.ToString("O"));
        }

        foreach (var (medicine, m) in Medicines)
        {
            new Entity(HaContext, m.Button).StateChanges()
                .Where(x => x.Old?.State is not (null or "unavailable") && x.New?.State is not (null or "unknown" or "unavailable"))
                .Subscribe(_ => Give(medicine));
            new Entity(HaContext, m.Stock).StateChanges()
                .Subscribe(x => OnStockChanged(medicine, x.Old?.State, x.New?.State));
        }

        new Entity(HaContext, UndoButton).StateChanges()
            .Where(x => x.Old?.State is not (null or "unavailable") && x.New?.State is not (null or "unknown" or "unavailable"))
            .Subscribe(_ => UndoLastDose());

        // The status changes at the reminder time and when a new day begins
        var reminderTime = new InputDatetimeEntity(HaContext, "input_datetime.pixel_prednison_tijd");
        Scheduler.RunDailyAt(reminderTime, DefaultReminder, Publish);
        Scheduler.RunDailyAt(reminderTime, DefaultReminder, () => Scheduler.Schedule(PhoneReminderDelay, RemindOnPhone));
        Scheduler.RunDaily(ForgottenNotificationAt, RemindOnPhone);
        Scheduler.RunDaily(new TimeSpan(0, 0, 30), Publish);
        Scheduler.RunDaily(DayStartsAt.Add(TimeSpan.FromSeconds(30)), Publish);

        _ = CreateSensorAsync();
    }

    private async Task CreateSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(SensorId))
                await _entityManager.Create(SensorId, new EntityCreationOptions { Name = "Pixel medicijnen", Icon = "mdi:pill" });
            Publish();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {SensorId}", SensorId);
        }
    }

    private double Stock(string medicine) => ReadNumber(HaContext.GetState(Medicines[medicine].Stock)?.State) ?? 0;

    private static double? ReadNumber(string? state) =>
        double.TryParse(state, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Takes a dose off the stock and saves it.</summary>
    private void Give(string medicine)
    {
        lock (_lock)
        {
            var m = Medicines[medicine];
            var stock = Math.Max(0, Stock(medicine) - m.Dose);
            _expectedStock[medicine] = stock;
            HaContext.CallService("input_number", "set_value", ServiceTarget.FromEntity(m.Stock), new { value = stock });

            Add(new MedicationEvent { Time = Scheduler.NowNl(), Medicine = medicine, Kind = "gegeven", Amount = -m.Dose, Stock = stock });
            Logger.LogInformation("Pixel kreeg {Dose} {Medicine}, nog {Stock}", m.Dose, medicine, stock);
        }
    }

    /// <summary>
    /// Saves a change of the stock that was made by hand (e.g. a new strip), not by a dose. Changes in quick succession
    /// (typing, or clicking the arrows) become one line once the stock has not changed for <see cref="StockSettleTime"/>.
    /// </summary>
    private void OnStockChanged(string medicine, string? oldState, string? newState)
    {
        lock (_lock)
        {
            if (ReadNumber(oldState) is not { } before || ReadNumber(newState) is not { } after || before == after) return;
            if (_expectedStock.TryGetValue(medicine, out var expected) && Math.Abs(expected - after) < 0.001)
            {
                _expectedStock.Remove(medicine);
                return;
            }

            if (_pendingStock.Remove(medicine, out var pending))
            {
                pending.Timer.Dispose();
                before = pending.Before;
            }
            var timer = Scheduler.Schedule(StockSettleTime, () => SaveStockChange(medicine));
            _pendingStock[medicine] = (before, timer);
        }
    }

    private void SaveStockChange(string medicine)
    {
        lock (_lock)
        {
            if (!_pendingStock.Remove(medicine, out var pending)) return;
            var after = Stock(medicine);
            if (Math.Abs(after - pending.Before) < 0.001) return;

            Add(new MedicationEvent { Time = Scheduler.NowNl(), Medicine = medicine, Kind = "voorraad", Amount = after - pending.Before, Stock = after });
            Logger.LogInformation("Voorraad {Medicine} aangepast van {Before} naar {After}", medicine, pending.Before, after);
        }
    }

    private void Add(MedicationEvent medicationEvent)
    {
        _history.Add(medicationEvent);
        _storage.Save(HistoryKey, _history);
        Publish();
    }

    /// <summary>
    /// The prednison status: <c>gegeven</c> (today's dose is in), <c>wacht</c> (not yet, before the reminder time), <c>nodig</c>
    /// (after the reminder time and not given yet) or <c>vergeten</c> (a whole day passed without a dose, until the next one).
    /// </summary>
    public string PrednisonStatus()
    {
        var now = Scheduler.NowNl();
        var lastDose = Doses(Prednison).Select(x => x.Time).DefaultIfEmpty(DateTimeOffset.MinValue).Max();

        // A dose day runs from 12:00 to 12:00 the next day, so before 12:00 it is still the day before
        var day = now.TimeOfDay < DayStartsAt ? now.Date.AddDays(-1) : now.Date;
        DateTimeOffset Start(DateTime date)
        {
            var local = date.Date + DayStartsAt;
            return new DateTimeOffset(local, Automation.Extensions.SchedulerExtensions.DutchTimeZone.GetUtcOffset(local));
        }
        bool Given(DateTime date) => lastDose >= Start(date);
        bool Tracked(DateTime date) => Start(date) >= _since;

        // The day before was missed entirely: forgotten until the next dose
        var previous = day.AddDays(-1);
        if (Tracked(previous) && !Given(previous)) return "vergeten";

        if (Given(day)) return "gegeven";

        // Past midnight and still nothing for the evening before: forgotten
        if (now.Date > day && Tracked(day)) return "vergeten";

        return now.TimeOfDay >= ReminderTime() ? "nodig" : "wacht";
    }

    private TimeSpan ReminderTime() =>
        TimeSpan.TryParse(HaContext.GetState("input_datetime.pixel_prednison_tijd")?.State, CultureInfo.InvariantCulture, out var time)
            ? time
            : DefaultReminder;

    /// <summary>The doses of a medicine (or of all when null) that still count: not undone by a correction.</summary>
    private IEnumerable<MedicationEvent> Doses(string? medicine) =>
        _history.Where(x => x.Kind == "gegeven" && (medicine == null || x.Medicine == medicine) && !IsCorrected(x));

    private bool IsCorrected(MedicationEvent dose) =>
        _history.Any(x => x.Kind == "correctie" && x.Medicine == dose.Medicine && x.Corrects == dose.Time);

    /// <summary>Undoes the last dose that still counts: the pill goes back on the stock and a correction line is saved.</summary>
    private void UndoLastDose()
    {
        lock (_lock)
        {
            if (Doses(null).LastOrDefault() is not { } dose)
            {
                Logger.LogInformation("Nothing to undo in Pixel's medication");
                return;
            }

            // Exactly what that dose took off, also if the dose was different then
            var m = Medicines[dose.Medicine];
            var amount = -dose.Amount;
            var stock = Stock(dose.Medicine) + amount;
            _expectedStock[dose.Medicine] = stock;
            HaContext.CallService("input_number", "set_value", ServiceTarget.FromEntity(m.Stock), new { value = stock });

            Add(new MedicationEvent
            {
                Time = Scheduler.NowNl(), Medicine = dose.Medicine, Kind = "correctie", Amount = amount, Stock = stock, Corrects = dose.Time
            });
            Logger.LogInformation("Gift {Medicine} van {Time} ongedaan gemaakt, nog {Stock}", dose.Medicine, dose.Time, stock);
        }
    }

    /// <summary>A phone notification for Vincent and Carleen when the prednison is still needed or was forgotten.</summary>
    private void RemindOnPhone()
    {
        var status = PrednisonStatus();
        if (status is not ("nodig" or "vergeten")) return;

        var forgotten = status == "vergeten";
        Notify.NotifyPhoneVincentCarleen(
            forgotten ? "Prednison vergeten!" : "Pixel heeft zijn prednison nog niet gehad",
            forgotten ? "Pixel heeft gisteren geen prednison gekregen. Geef hem zijn kwart pil." : "Vergeet zijn kwart pil prednison niet.",
            true,
            action: [new ActionModel(action: "PIXEL_PREDNISON_GEGEVEN", title: "✅ Gegeven", func: () => Give(Prednison))],
            channel: "Pixel");
        Logger.LogInformation("Pixel prednison: herinnering op de telefoon ({Status})", status);
    }

    private void Publish()
    {
        lock (_lock)
        {
            var status = PrednisonStatus();
            var last = (string medicine) => Doses(medicine).LastOrDefault()?.Time.ToString("yyyy-MM-ddTHH:mm:sszzz");
            var undoable = Doses(null).LastOrDefault();
            var prednison = Stock(Prednison);
            var antibraak = Stock(Antibraak);

            _entityManager.SetState(SensorId, status, new
            {
                prednison_voorraad = prednison,
                antibraak_voorraad = antibraak,
                prednison_bijna_op = prednison < Medicines[Prednison].Low,
                antibraak_bijna_op = antibraak < Medicines[Antibraak].Low,
                prednison_dagen_over = Math.Floor(prednison / Medicines[Prednison].Dose),
                laatste_prednison = last(Prednison),
                laatste_antibraak = last(Antibraak),
                herinnering = ReminderTime().ToString(@"hh\:mm"),
                ongedaan_maken = undoable == null ? null : new
                {
                    tijd = undoable.Time.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    medicijn = undoable.Medicine,
                    hoeveelheid = undoable.Amount
                },
                geschiedenis = Enumerable.Reverse(_history).Select(x => new
                {
                    tijd = x.Time.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    medicijn = x.Medicine,
                    soort = x.Kind,
                    hoeveelheid = x.Amount,
                    voorraad = x.Stock,
                    gecorrigeerd = x.Kind == "gegeven" && IsCorrected(x),
                    corrigeert = x.Corrects?.ToString("yyyy-MM-ddTHH:mm:sszzz")
                }).ToList()
            });
        }
    }
}
