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
/// <item>The wallet leaves home while Vincent is home: a message to Vincent.</item>
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

    /// <summary>How long after Vincent left the wallet is asked where it is, and how long after that the answer is read.</summary>
    public static readonly TimeSpan AskAfter = TimeSpan.FromMinutes(4);
    public static readonly TimeSpan ReadAfter = TimeSpan.FromMinutes(7);

    private static readonly TimeSpan LeavingCheckDelay = TimeSpan.FromMinutes(2);

    public WalletMonitor(
        IHaContext ha,
        ILogger<WalletMonitor> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        Entities.InputBoolean.Awayvincent.WhenTurnsOn(_ => VincentLeft(Scheduler.Now));

        new Entity(HaContext, Tracker).StateChanges()
            .Where(x => IsAtHome(x.Old?.State) && !IsAtHome(x.New?.State) && x.New?.State is not (null or "unknown" or "unavailable"))
            .Subscribe(_ => Scheduler.Schedule(LeavingCheckDelay, CheckLeftWhileVincentIsHome));

        new Entity(HaContext, UnwantedTracking).StateChanges()
            .Where(x => x.New?.State == "on" && x.Old?.State != "on")
            .Subscribe(_ => Notify.NotifyPhoneVincent("Portemonnee: ongewenste tracking", "De portemonnee-tracker meldt dat een onbekende tracker je volgt", true, 60));
    }

    /// <summary>Whether a tracker state means home: the zone Thuis, or the Google label of the house ("Huis Vincent").</summary>
    public static bool IsAtHome(string? state) =>
        state is not null && (state.Equals("home", StringComparison.OrdinalIgnoreCase)
                              || state.StartsWith("Huis", StringComparison.OrdinalIgnoreCase)
                              || state.StartsWith("Thuis", StringComparison.OrdinalIgnoreCase));

    private string? StateOf(string entityId) => HaContext.GetState(entityId)?.State;

    /// <summary>When the wallet was last seen, by the tracker attribute <c>last_seen</c>.</summary>
    private DateTimeOffset? LastSeen() =>
        HaContext.GetState(Tracker)?.AttributesJson is { ValueKind: JsonValueKind.Object } a
        && a.TryGetProperty("last_seen", out var seen) && seen.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(seen.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private void VincentLeft(DateTimeOffset left)
    {
        // The wallet only knows where it is when another device has seen it: ask it to look, then read what it says
        Scheduler.Schedule(AskAfter, () => HaContext.CallService("button", "press", ServiceTarget.FromEntity(LocateButton)));
        Scheduler.Schedule(ReadAfter, () => CheckLeftWithoutWallet(left));
    }

    private void CheckLeftWithoutWallet(DateTimeOffset left)
    {
        // Still away, the wallet at home, and seen there after he had left: it really is lying at home
        if (!Entities.InputBoolean.Awayvincent.IsOn() || !IsAtHome(StateOf(Tracker))) return;
        if (LastSeen() is not { } seen || seen < left + TimeSpan.FromMinutes(2)) return;
        var at = Extensions.SchedulerExtensions.ToDutchTime(seen).ToString("HH:mm", CultureInfo.InvariantCulture);
        Notify.NotifyPhoneVincent("Portemonnee vergeten?", $"Je portemonnee ligt nog thuis (gezien om {at})", false, 60,
            [new ActionModel("WALLET_SOUND", "🔔 Laat piepen", func: () => HaContext.CallService("button", "press", ServiceTarget.FromEntity(SoundButton)))]);
    }

    private void CheckLeftWhileVincentIsHome()
    {
        // Only when it is still away after a moment, and while Vincent is home to notice
        if (IsAtHome(StateOf(Tracker)) || !Vincent.IsHome) return;
        Notify.NotifyPhoneVincent("Portemonnee weg", $"Je portemonnee is van huis weggegaan: {StateOf(Tracker)}", false, 60);
    }
}
