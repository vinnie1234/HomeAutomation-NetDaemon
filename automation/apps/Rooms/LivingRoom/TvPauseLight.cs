using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Text.Json;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.LivingRoom;

/// <summary>
/// Pausing a film on the living room TV puts the pendant lamp on, playing it again puts the lamp out. This works for Jellyfin (the player of
/// every Jellyfin user that plays on "Tv in Woonkamer", whoever is logged in) and for other apps that report playing and paused on the
/// TV itself (the Cast entity of the TV). A pause is only counted after <see cref="PauseDelay"/>: skipping and buffering pause for a moment.
/// Playing always puts the lamp out.
/// </summary>
[NetDaemonApp(Id = nameof(TvPauseLight))]
public class TvPauseLight : BaseApp
{
    /// <summary>The name Jellyfin gives the TV in the living room.</summary>
    public const string JellyfinDevice = "Tv in Woonkamer";

    public const string CastEntity = "media_player.tv_in_woonkamer_2";

    /// <summary>The Jellyfin players of the users (JellyHA).</summary>
    public static readonly string[] JellyfinPlayers =
    [
        "media_player.jellyha_user_vincent", "media_player.jellyha_user_dewin", "media_player.jellyha_user_falco",
        "media_player.jellyha_user_roelof", "media_player.jellyha_user_zoe"
    ];

    /// <summary>How long a film has to stay paused before the lamp goes on.</summary>
    public static readonly TimeSpan PauseDelay = TimeSpan.FromSeconds(2);

    /// <summary>Dim and warm: bright enough to find the remote, not to ruin the film.</summary>
    public const int Brightness = 35;

    public const int Kelvin = 2700;

    private readonly SerialDisposable _pending = new();

    public TvPauseLight(
        IHaContext ha,
        ILogger<TvPauseLight> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        foreach (var player in JellyfinPlayers)
            new Entity(HaContext, player).StateChanges()
                .Where(x => OnTheLivingRoomTv(x.New?.AttributesJson))
                .Subscribe(x => Changed(x.New?.State));

        new Entity(HaContext, CastEntity).StateChanges().Subscribe(x => Changed(x.New?.State));
    }

    private static bool OnTheLivingRoomTv(JsonElement? attributes) =>
        attributes is { ValueKind: JsonValueKind.Object } a &&
        a.TryGetProperty("device_name", out var name) && name.ValueKind == JsonValueKind.String &&
        string.Equals(name.GetString(), JellyfinDevice, StringComparison.OrdinalIgnoreCase);

    private void Changed(string? state)
    {
        switch (state)
        {
            case "paused":
                _pending.Disposable = Scheduler.Schedule(PauseDelay, Paused);
                break;
            case "playing":
                _pending.Disposable = Disposable.Empty;
                Playing();
                break;
        }
    }

    private bool MayChangeLights =>
        Entities.InputBoolean.Disablelightautomationlivingroom.IsOff() &&
        Entities.MediaPlayer.Tv.State is "on" or "playing" or "paused" or "idle" &&
        !IsSleepMode && Entities.InputBoolean.Away.IsOff();

    private void Paused()
    {
        if (!MayChangeLights) return;
        if (Entities.Light.Hanglamp.IsOn())
        {
            Logger.LogDebug("The film is paused, the pendant lamp is already on");
            return;
        }

        Logger.LogInformation("The film is paused: the pendant lamp goes on");
        Entities.Light.Hanglamp.TurnOn(brightnessPct: Brightness, colorTempKelvin: Kelvin);
    }

    private void Playing()
    {
        Logger.LogInformation("The film plays again: the pendant lamp goes out");
        Entities.Light.Hanglamp.TurnOff();
    }
}
