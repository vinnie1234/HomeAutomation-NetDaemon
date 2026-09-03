using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.Kitchen;

/// <summary>
/// Starts Spotify on the kitchen speaker when motion is detected in the living room,
/// provided all guards are satisfied.
/// </summary>
[NetDaemonApp(Id = nameof(KitchenMusicOnMotion))]
public class KitchenMusicOnMotion : BaseApp
{
    private readonly ISpotcast _spotcast;
    private readonly AppConfig _config;

    /// <summary>Timestamp at which the kitchen speaker last stopped playing (playing → idle/off).</summary>
    private DateTimeOffset? _lastStoppedAt;

    /// <summary>
    /// Gets a value indicating whether Spotify is currently active on any device.
    /// </summary>
    private bool IsSpotifyPlaying =>
        Entities.MediaPlayer.SpotifyVincentMaarschalkerweerd.State == "playing";

    /// <summary>
    /// Gets a value indicating whether the TV is currently in use.
    /// </summary>
    private bool IsTvOn =>
        Entities.MediaPlayer.Tv.State is "on" or "playing" or "paused";

    /// <summary>
    /// Gets a value indicating whether the kitchen speaker is already playing.
    /// </summary>
    private bool IsKitchenAlreadyPlaying =>
        Entities.MediaPlayer.Nestmini9818.State == "playing";

    /// <summary>
    /// Gets a value indicating whether the music was manually stopped within the last 15 minutes.
    /// Uses <see cref="IScheduler.Now"/> so the test scheduler can control the clock.
    /// </summary>
    private bool WasRecentlyStopped =>
        _lastStoppedAt.HasValue &&
        Scheduler.Now - _lastStoppedAt.Value < TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets a value indicating whether all guards are satisfied and kitchen music may start.
    /// </summary>
    private bool IsHouseEmpty =>
        Entities.InputBoolean.Awayvincent.IsOn() &&
        Entities.InputBoolean.Awaycarleen.IsOn();

    private bool CanStartMusic =>
        Vincent.IsHome &&
        Entities.InputBoolean.Working.IsOff() &&
        !IsSpotifyPlaying &&
        !IsTvOn &&
        !IsNightMode &&
        !WasRecentlyStopped &&
        !IsKitchenAlreadyPlaying;

    /// <summary>
    /// Initializes a new instance of the <see cref="KitchenMusicOnMotion"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for timing operations.</param>
    /// <param name="spotcast">The Spotcast service used to start playback.</param>
    /// <param name="config">Application configuration containing the kitchen playlist URL.</param>
    public KitchenMusicOnMotion(
        IHaContext ha,
        ILogger<KitchenMusicOnMotion> logger,
        INotify notify,
        IScheduler scheduler,
        ISpotcast spotcast,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler)
    {
        _spotcast = spotcast;
        _config = config.Value;

        TrackKitchenSpeakerStopped();

        Entities.BinarySensor.Motionwoonkamer.WhenTurnsOn(_ => TryStartKitchenMusic());

        Entities.InputBoolean.Awayvincent.WhenTurnsOn(_ => { if (IsHouseEmpty) StopKitchenMusicIfPlaying(); });
        Entities.InputBoolean.Awaycarleen.WhenTurnsOn(_ => { if (IsHouseEmpty) StopKitchenMusicIfPlaying(); });

        Entities.MediaPlayer.Tv
            .StateChanges()
            .Where(x => x.New?.State is "on" or "playing" or "paused")
            .Subscribe(_ => StopKitchenMusicIfPlaying());
    }

    /// <summary>
    /// Records the time whenever the kitchen speaker transitions from playing to stopped/idle,
    /// so the 15-minute cooldown can be enforced on the next motion trigger.
    /// </summary>
    private void TrackKitchenSpeakerStopped()
    {
        Entities.MediaPlayer.Nestmini9818
            .StateChanges()
            .Where(x => x.Old?.State == "playing" && x.New?.State is "idle" or "off")
            .Subscribe(_ =>
            {
                Logger.LogDebug("Kitchen speaker stopped — starting 15-minute cooldown");
                _lastStoppedAt = Scheduler.Now;
            });
    }

    private void StopKitchenMusicIfPlaying()
    {
        if (!IsKitchenAlreadyPlaying) return;
        Logger.LogInformation("Stopping kitchen music");
        Services.MediaPlayer.MediaStop(ServiceTarget.FromEntity(Entities.MediaPlayer.Nestmini9818.EntityId));
    }

    /// <summary>
    /// Starts the kitchen playlist when all guards pass; logs the reason when they do not.
    /// </summary>
    private void TryStartKitchenMusic()
    {
        if (!CanStartMusic)
        {
            Logger.LogDebug(
                "Kitchen music skipped — VincentHome={VincentHome} Working={Working} " +
                "SpotifyPlaying={SpotifyPlaying} TvOn={TvOn} NightMode={NightMode} " +
                "RecentlyStopped={RecentlyStopped} AlreadyPlaying={AlreadyPlaying}",
                Vincent.IsHome,
                Entities.InputBoolean.Working.IsOn(),
                IsSpotifyPlaying,
                IsTvOn,
                IsNightMode,
                WasRecentlyStopped,
                IsKitchenAlreadyPlaying);
            return;
        }

        Logger.LogInformation("Starting kitchen music on nestmini9818");
        _spotcast.PlaySpotify(Entities.MediaPlayer.Nestmini9818, _config.SpotifyKeukenUrl);
    }
}
