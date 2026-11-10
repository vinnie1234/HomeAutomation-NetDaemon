using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps an eye on the wallet of Vincent (a tracker in Google Find Hub, entities <c>*_portemonnee_van_vincent_2*</c>):
/// <list type="bullet">
/// <item>Vincent leaves home while the wallet stays: a few minutes after he left the wallet is asked where it is, and when it was seen
/// at home after he left, he gets a message ("Portemonnee vergeten?"). A wallet that was not seen since he left, is not reported:
/// it can be in his pocket.</item>
/// <item>The wallet leaves home while Vincent is home: a message to Vincent, but only when it has been away for half an hour
/// (<see cref="NotHomeFor"/>); a wallet that is back sooner is nothing to report.</item>
/// <item>Vincent comes home (or the wallet leaves while he is home) and the wallet is not at home: the tracker often lags, so the wallet
/// is asked where it is, a minute later again, and when it is still not at home the alert on the dashboard shows
/// (<see cref="AlertHelper"/>), as soon as the wallet has been away for half an hour (<see cref="NotHomeFor"/>) and Vincent is home.</item>
/// <item>The tracker warns that an unknown tracker follows him (unwanted tracking): a message.</item>
/// </list>
/// Google calls a place that has a label ("Huis") a semantic location: the tracker has no coordinates then and its state is the name of
/// the label, so "at home" is the state home or a name that starts with Huis or Thuis.
/// </summary>
[NetDaemonApp(Id = nameof(WalletMonitor))]
public class WalletMonitor : BaseApp
{
    private const string Prefix = "portemonnee_van_vincent_2";

    public const string Tracker = "device_tracker." + Prefix;
    public const string UnwantedTracking = "binary_sensor." + Prefix + "_ongewenste_trackingmodus";
    public const string LocateButton = "button." + Prefix + "_nu_lokaliseren";
    public const string SoundButton = "button." + Prefix + "_geluid_afspelen";

    /// <summary>On when the wallet is still not at home after it was asked twice where it is: the dashboard shows its alert then.</summary>
    public const string AlertHelper = "input_boolean.portemonnee_melding";

    /// <summary>How long between asking the wallet where it is, and checking whether it answered.</summary>
    public static readonly TimeSpan RescanAfter = TimeSpan.FromMinutes(1);

    /// <summary>Changes with every search, so a search that was overtaken stops.</summary>
    private int _searchId;

    /// <summary>How long after Vincent left the wallet is asked where it is, and how long after that the answer is read.</summary>
    public static readonly TimeSpan AskAfter = TimeSpan.FromMinutes(4);
    public static readonly TimeSpan ReadAfter = TimeSpan.FromMinutes(7);

    /// <summary>How long the wallet has to be away from home, while Vincent is home, before the alert and the message come.</summary>
    public static readonly TimeSpan NotHomeFor = TimeSpan.FromMinutes(30);

    /// <summary>Since when the wallet is not at home (null while it is, or when that is not known).</summary>
    private DateTimeOffset? _awaySince;

    public WalletMonitor(
        IHaContext ha,
        ILogger<WalletMonitor> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        Entities.InputBoolean.Awayvincent.WhenTurnsOn(_ =>
        {
            StopSearch();
            VincentLeft(Scheduler.Now);
        });
        Entities.InputBoolean.Awayvincent.WhenTurnsOff(_ => StartSearch());

        // After a restart: since when the wallet is away, as far as Home Assistant knows
        if (HaContext.GetState(Tracker) is { State: { } current } known && !IsAtHome(current) && current is not ("unknown" or "unavailable"))
            _awaySince = known.LastChanged ?? Scheduler.Now;

        Entities.DeviceTracker.PortemonneeVanVincent2.StateChanges()
            .Where(x => IsAtHome(x.Old?.State) && !IsAtHome(x.New?.State) && x.New?.State is not (null or "unknown" or "unavailable"))
            .Subscribe(_ =>
            {
                _awaySince = Scheduler.Now;
                StartSearch(leftWhileVincentIsHome: true);
            });

        // Found: the alert goes away
        Entities.DeviceTracker.PortemonneeVanVincent2.StateChanges().Where(x => IsAtHome(x.New?.State)).Subscribe(_ =>
        {
            _awaySince = null;
            StopSearch();
        });

        Entities.BinarySensor.PortemonneeVanVincent2OngewensteTrackingmodus.StateChanges()
            .Where(x => x.New?.State == "on" && x.Old?.State != "on")
            .Subscribe(_ => Notify.NotifyPhoneVincent("Portemonnee: ongewenste tracking", "De portemonnee-tracker meldt dat een onbekende tracker je volgt", true, 60));
    }

    /// <summary>Whether a tracker state means home: the zone Thuis, or the Google label of the house ("Huis Vincent").</summary>
    public static bool IsAtHome(string? state) =>
        state is not null && (state.Equals("home", StringComparison.OrdinalIgnoreCase)
                              || state.StartsWith("Huis", StringComparison.OrdinalIgnoreCase)
                              || state.StartsWith("Thuis", StringComparison.OrdinalIgnoreCase));

    /// <summary>Vincent is home and the wallet is known to be somewhere else.</summary>
    private bool IsMissing() =>
        !Entities.InputBoolean.Awayvincent.IsOn() && StateOf(Tracker) is { } state && state is not ("unknown" or "unavailable") && !IsAtHome(state);

    private void SetAlert(bool on)
    {
        var alert = new InputBooleanEntity(HaContext, AlertHelper);
        if (on) alert.TurnOn();
        else alert.TurnOff();
    }

    private void Locate() => Entities.Button.PortemonneeVanVincent2NuLokaliseren.Press();

    private void StopSearch()
    {
        _searchId++;
        SetAlert(false);
    }

    /// <summary>
    /// The wallet is asked where it is right away and after a minute again; when it is still not at home a minute after the second time,
    /// the alert shows, once the wallet has been away for <see cref="NotHomeFor"/>. The tracker lags behind, so the first answer is often too old.
    /// </summary>
    /// <param name="leftWhileVincentIsHome">The wallet left while Vincent is home: he also gets a message when the alert comes.</param>
    private void StartSearch(bool leftWhileVincentIsHome = false)
    {
        var id = ++_searchId;
        SetAlert(false);
        if (!IsMissing()) return;

        Locate();
        Scheduler.Schedule(RescanAfter, () =>
        {
            if (id != _searchId || !IsMissing()) return;
            Locate();
            Scheduler.Schedule(RescanAfter, () => AlertWhenAwayLongEnough(id, leftWhileVincentIsHome));
        });
    }

    /// <summary>The alert (and the message) come as soon as the wallet has been away for <see cref="NotHomeFor"/>; sooner it may still turn up.</summary>
    private void AlertWhenAwayLongEnough(int id, bool message)
    {
        if (id != _searchId || !IsMissing()) return;
        var wait = NotHomeFor - (Scheduler.Now - (_awaySince ?? Scheduler.Now));
        if (wait > TimeSpan.Zero)
        {
            Scheduler.Schedule(wait, () => AlertWhenAwayLongEnough(id, message));
            return;
        }

        Core.ActivityContext.Note($"De portemonnee is al {NotHomeFor.TotalMinutes:0} minuten niet thuis en Vincent is thuis");
        SetAlert(true);
        if (message) Notify.NotifyPhoneVincent("Portemonnee weg", $"Je portemonnee is al een half uur van huis weg: {StateOf(Tracker)}", false, 60);
    }

    private string? StateOf(string entityId) => HaContext.GetState(entityId)?.State;

    /// <summary>When the wallet was last seen, by the tracker attribute <c>last_seen</c>.</summary>
    private DateTimeOffset? LastSeen() =>
        HaContext.GetState(Tracker)?.AttributesJson is { ValueKind: JsonValueKind.Object } a
        && a.TryGetProperty("last_seen", out var seen) && seen.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(seen.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private void VincentLeft(DateTimeOffset left)
    {
        // The wallet only knows where it is when another device has seen it: ask it to look, then read what it says
        Scheduler.Schedule(AskAfter, () => Entities.Button.PortemonneeVanVincent2NuLokaliseren.Press());
        Scheduler.Schedule(ReadAfter, () => CheckLeftWithoutWallet(left));
    }

    private void CheckLeftWithoutWallet(DateTimeOffset left)
    {
        // Still away, the wallet at home, and seen there after he had left: it really is lying at home
        if (!Entities.InputBoolean.Awayvincent.IsOn() || !IsAtHome(StateOf(Tracker))) return;
        if (LastSeen() is not { } seen || seen < left + TimeSpan.FromMinutes(2)) return;
        var at = Extensions.SchedulerExtensions.ToDutchTime(seen).ToString("HH:mm", CultureInfo.InvariantCulture);
        Notify.NotifyPhoneVincent("Portemonnee vergeten?", $"Je portemonnee ligt nog thuis (gezien om {at})", false, 60,
            [new ActionModel("WALLET_SOUND", "🔔 Laat piepen", func: () => Entities.Button.PortemonneeVanVincent2GeluidAfspelen.Press())]);
    }
}
