using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Automation.Models;
using Automation.Models.DiscordNotificationModels;
using EntityCreationOptions = Automation.Interfaces.EntityCreationOptions;

namespace Automation.apps.General.Pets;

/// <summary>
/// Feeding Pixel. A feeding is only done when the feeder itself says so: after the command the feeder has to report that it is feeding
/// (<c>sensor.zedar_a600_feed_state</c> becomes "feeding", <c>binary_sensor.zedar_a600_voeden</c> goes on) within
/// <see cref="FeedConfirmTimeout"/>. Only then the counters and the "last fed" moments are updated. When it does not report that, nothing
/// was dispensed and everybody is told right away (both phones, with a button to feed what was missed and a tap that opens the
/// dashboard, the alert on Home, and a red cross at the feeding time). There is no automatic second try: feeding twice by accident is also harmful.
/// What was missed is kept (<see cref="MissedStorageKey"/>) until it is fed afterwards with "Alsnog voeren", which gives exactly that amount.
/// </summary>
public partial class Cat
{
    /// <summary>The missed feedings: the state is the amount of portions missed, the attributes list the moments.</summary>
    public const string MissedSensor = "sensor.pixel_gemiste_voeding";

    /// <summary>Feeds what was missed.</summary>
    public const string CatchUpButton = "input_button.pixel_alsnog_voeren";

    public const string MissedStorageKey = "PixelMissedFeeds";

    /// <summary>Where a tap on the notification goes (the page of Pixel, with the feeding moments).</summary>
    public const string PixelPage = "/dashboard-huis/pixel";

    /// <summary>How long the feeder has to confirm a feeding. It reports within seconds; the commands of other devices took up to 20 seconds to time out.</summary>
    public static readonly TimeSpan FeedConfirmTimeout = TimeSpan.FromSeconds(40);

    /// <summary>A feeding that was asked for but not confirmed by the feeder.</summary>
    /// <param name="Date">The day, "yyyy-MM-dd".</param>
    /// <param name="Time">The feeding time on the dashboard ("12:00") or the time it was asked for.</param>
    /// <param name="Amount">The portions that were not dispensed.</param>
    /// <param name="Reason">Automatic, manual or the early feed.</param>
    public sealed record MissedFeed(string Date, string Time, int Amount, string Reason);

    private List<MissedFeed> _missed = [];
    private bool _catchUpBusy;

    private void SetUpFeeding()
    {
        _missed = _storage.Get<List<MissedFeed>>(MissedStorageKey) ?? [];
        _ = CreateMissedSensorAsync();
        new Entity(HaContext, CatchUpButton).StateChanges()
            .Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
            .Subscribe(_ => FeedMissed());
    }

    private async Task CreateMissedSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(MissedSensor))
                await _entityManager.CreateAsync(MissedSensor, new EntityCreationOptions { Name = "Pixel gemiste voeding", Icon = "mdi:food-drumstick-off" });
            PublishMissed();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {Sensor}", MissedSensor);
        }
    }

    private int MissedTotal => _missed.Sum(m => m.Amount);

    private void PublishMissed() =>
        _entityManager.SetState(MissedSensor, MissedTotal, new
        {
            momenten = _missed.Select(m => new { datum = m.Date, tijd = m.Time, aantal = m.Amount, reden = m.Reason }),
            bijgewerkt = Scheduler.NowNl().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)
        });

    private void SaveMissed() => _storage.Save(MissedStorageKey, _missed);

    /// <summary>
    /// Asks the feeder for <paramref name="amount"/> portions and waits for the feeder to say it is feeding. Exactly one of the two actions
    /// runs, once: <paramref name="onConfirmed"/> when it reported feeding, <paramref name="onFailed"/> when the command failed or the report did not come in time.
    /// </summary>
    private void Feed(int amount, Action onConfirmed, Action onFailed)
    {
        if (amount <= 0)
        {
            Logger.LogWarning("Not feeding: the amount is {Amount}", amount);
            return;
        }

        var decided = 0;
        var watch = new CompositeDisposable();

        void Decide(bool confirmed)
        {
            if (Interlocked.Exchange(ref decided, 1) == 1) return;
            watch.Dispose();
            try
            {
                if (confirmed) onConfirmed();
                else onFailed();
            }
            catch (Exception ex)
            {
                // Rx would swallow it: a feeding result that is not processed must at least show in the log
                Logger.LogError(ex, "Processing the result of a feeding ({Confirmed}) failed", confirmed);
            }
        }

        watch.Add(Entities.Sensor.ZedarA600FeedState.StateChanges().Where(x => x.New?.State == "feeding").Subscribe(_ => Decide(true)));
        watch.Add(Entities.BinarySensor.ZedarA600Voeden.StateChanges().Where(x => x.New?.State == "on").Subscribe(_ => Decide(true)));
        watch.Add(Scheduler.Schedule(FeedConfirmTimeout, () => Decide(false)));

        try
        {
            Services.Number.SetValue(ServiceTarget.FromEntity(Entities.Number.ZedarA600Voeden.EntityId), amount.ToString(CultureInfo.InvariantCulture));
            Logger.LogInformation("Asked the feeder for {Amount} portions, waiting for it to confirm", amount);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Asking the feeder for {Amount} portions failed", amount);
            Decide(false);
        }
    }

    /// <summary>The counters of what Pixel got. <paramref name="today"/> is the part that belongs to today (the rest was missed on an earlier day).</summary>
    private void CountFed(int total, int today)
    {
        if (today > 0)
            Entities.InputNumber.Pixeltotalamountfeedday.SetValue(Convert.ToInt32((Entities.InputNumber.Pixeltotalamountfeedday.State ?? 0) + today));
        Entities.InputNumber.Pixeltotalamountfeedalltime.SetValue(
            Convert.ToInt32((Entities.InputNumber.Pixeltotalamountfeedalltime.State ?? 0) + total));
    }

    private static string TimeLabel(string? state) =>
        state is { Length: >= 5 } ? state[..5] : "";

    private string NowLabel() => Scheduler.NowNl().ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A scheduled feeding (automatic).</summary>
    private void FeedScheduled(int amount, string time) =>
        Feed(amount,
            onConfirmed: () =>
            {
                CountFed(amount, amount);
                Entities.InputNumber.Pixellastamountautomationfeed.SetValue(amount);
                Entities.InputDatetime.Pixellastautomatedfeed.SetDatetime(new InputDatetimeSetDatetimeParameters { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            },
            onFailed: () => RecordMissed(time, amount, "automatisch"));

    /// <summary>A feeding by hand (button), or the next one early.</summary>
    private void FeedByHand(int amount, string time, string reason, Action? whenFailed = null) =>
        Feed(amount,
            onConfirmed: () =>
            {
                CountFed(amount, amount);
                Entities.InputNumber.Pixellastamountmanualfeed.SetValue(amount);
                Entities.InputDatetime.Pixellastmanualfeed.SetDatetime(new InputDatetimeSetDatetimeParameters { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            },
            onFailed: () =>
            {
                whenFailed?.Invoke();
                RecordMissed(time, amount, reason);
            });

    private void RecordMissed(string time, int amount, string reason)
    {
        _missed.Add(new MissedFeed(Scheduler.NowNl().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), time, amount, reason));
        SaveMissed();
        PublishMissed();
        Logger.LogError("Pixel was NOT fed: {Amount} portions at {Time} ({Reason}); {Total} portions missed in all", amount, time, reason, MissedTotal);
        Core.ActivityContext.Note($"De voeder bevestigde de voeding van {time} niet binnen {FeedConfirmTimeout.TotalSeconds:0} seconden");

        var message = $"De voeding van {time} ({Portions(amount)}) is mislukt: de voeder heeft niets uitgegeven. " +
                      (MissedTotal > amount ? $"In totaal gemist: {Portions(MissedTotal)}. " : "") + "Tik om naar de pagina van Pixel te gaan.";
        NotifyNotFed("Pixel is NIET gevoerd", message);
        Notify.NotifyDiscord($"🐱❌ **Pixel is NIET gevoerd** ({time}, {Portions(amount)})", [_config.Discord.Pixel]);
    }

    private void NotifyNotFed(string title, string message) =>
        Notify.NotifyPhoneVincentCarleen(title, message, true,
            action: [new ActionModel("PIXEL_ALSNOG", $"Alsnog voeren ({Portions(MissedTotal)})", func: FeedMissed)],
            vibrationPattern: "100, 1000, 100, 1000, 100", clickAction: PixelPage);

    private static string Portions(int amount) => $"{amount} {(amount == 1 ? "portie" : "porties")}";

    /// <summary>
    /// "Alsnog voeren": feeds everything that was missed in one go, exactly that amount. When the feeder confirms it the cross turns green
    /// (the moments are no longer missed); when not, everybody is told again and what was missed stays.
    /// </summary>
    private void FeedMissed()
    {
        var entries = _missed.ToList();
        var total = entries.Sum(m => m.Amount);
        if (total <= 0)
        {
            Logger.LogInformation("Feeding what was missed: nothing was missed");
            return;
        }
        if (_catchUpBusy)
        {
            Logger.LogInformation("Feeding what was missed is already busy");
            return;
        }

        _catchUpBusy = true;
        Core.ActivityContext.Note($"Alsnog voeren: {Portions(total)} gemist");
        Feed(total,
            onConfirmed: () =>
            {
                _catchUpBusy = false;
                var today = Scheduler.NowNl().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                CountFed(total, entries.Where(e => e.Date == today).Sum(e => e.Amount));
                _missed.RemoveAll(entries.Contains);
                SaveMissed();
                PublishMissed();
                Entities.InputNumber.Pixellastamountmanualfeed.SetValue(total);
                Entities.InputDatetime.Pixellastmanualfeed.SetDatetime(new InputDatetimeSetDatetimeParameters { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                Logger.LogInformation("Pixel was fed afterwards: {Total} portions", total);
                Notify.NotifyPhoneVincentCarleen("Pixel is alsnog gevoerd", $"De voeder heeft {Portions(total)} uitgegeven.", true);
            },
            onFailed: () =>
            {
                _catchUpBusy = false;
                Logger.LogError("Feeding what was missed ({Total} portions) failed too", total);
                NotifyNotFed("Alsnog voeren is OOK mislukt", $"De voeder gaf de {Portions(total)} niet uit. Pixel heeft nog steeds niet gegeten: geef hem zelf te eten of controleer de voeder.");
            });
    }
}
