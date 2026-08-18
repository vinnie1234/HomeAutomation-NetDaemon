using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.Helpers;

/// <summary>
/// Probabilistic presence detection for the living room. Instead of trusting a single motion
/// sensor, presence is derived from several "anchors" (motion, TV). As long as one anchor is
/// active the room counts as occupied; once the last one drops away the room stays occupied
/// for a grace period, so sitting still while gaming or watching TV never turns the lights off.
/// </summary>
public interface ILivingRoomPresenceService
{
    /// <summary>Gets a value indicating whether the living room is currently considered occupied.</summary>
    bool IsOccupied { get; }

    /// <summary>Emits whenever the occupancy state changes. Does not replay the current state.</summary>
    IObservable<bool> OccupiedChanged { get; }
}

/// <inheritdoc cref="ILivingRoomPresenceService"/>
public class LivingRoomPresenceService : ILivingRoomPresenceService, IDisposable
{
    /// <summary>
    /// Media player states that mean somebody is using the TV. "idle" and "standby" are
    /// deliberately excluded: the TV reports those while nobody is watching.
    /// </summary>
    private static readonly string[] ActiveTvStates = ["on", "playing", "paused", "buffering"];

    private readonly Entities _entities;
    private readonly ILogger<LivingRoomPresenceService> _logger;
    private readonly BehaviorSubject<bool> _isOccupiedSubject;
    private readonly IDisposable _subscription;

    /// <inheritdoc />
    public bool IsOccupied => _isOccupiedSubject.Value;

    /// <inheritdoc />
    public IObservable<bool> OccupiedChanged => _isOccupiedSubject.DistinctUntilChanged().Skip(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="LivingRoomPresenceService"/> class.
    /// </summary>
    /// <param name="haContext">The Home Assistant context, used to observe the presence anchors.</param>
    /// <param name="scheduler">The scheduler that times the grace period.</param>
    /// <param name="config">The application configuration holding the grace period.</param>
    /// <param name="logger">The logger instance.</param>
    public LivingRoomPresenceService(
        IHaContext haContext,
        IScheduler scheduler,
        IOptions<AppConfiguration> config,
        ILogger<LivingRoomPresenceService> logger)
    {
        _entities = new Entities(haContext);
        _logger = logger;

        var gracePeriod = config.Value.Presence.LivingRoomGracePeriod;
        _isOccupiedSubject = new BehaviorSubject<bool>(ReadInitialAnchorState());

        var anchorChanges = Observable.Merge(
                _entities.BinarySensor.Motionwoonkamer.StateChanges().Select(_ => Unit.Default),
                _entities.MediaPlayer.Tv.StateChanges().Select(_ => Unit.Default))
            .Select(_ => HasActiveAnchor())
            .DistinctUntilChanged();

        // While an anchor is active the room is occupied right away. When the last anchor drops
        // away the "not occupied" signal is delayed; a new anchor within the grace period cancels
        // it because Switch() unsubscribes from the pending timer.
        _subscription = anchorChanges
            .Select(anchorActive => anchorActive
                ? Observable.Return(true)
                : Observable.Return(false).Delay(gracePeriod, scheduler))
            .Switch()
            .DistinctUntilChanged()
            .Subscribe(OnOccupancyChanged);
    }

    private void OnOccupancyChanged(bool isOccupied)
    {
        if (isOccupied == _isOccupiedSubject.Value) return;

        _logger.LogDebug("Living room presence changed to {State}", isOccupied ? "occupied" : "empty");
        _isOccupiedSubject.OnNext(isOccupied);
    }

    /// <summary>
    /// Reads the anchors for the initial occupancy value, tolerating a Home Assistant connection
    /// that is not ready yet.
    /// </summary>
    /// <remarks>
    /// Entity state is unavailable until NetDaemon finished its initial connection, and this
    /// service can be constructed before that happens (for example when the container is resolved
    /// eagerly at startup). A failed read simply means "not occupied yet": the first motion or TV
    /// change establishes the real state a moment later.
    /// </remarks>
    private bool ReadInitialAnchorState()
    {
        try
        {
            return HasActiveAnchor();
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogDebug(exception, "Home Assistant state is not available yet, assuming the living room is empty");
            return false;
        }
    }

    /// <summary>
    /// Determines whether at least one presence anchor is currently active.
    /// </summary>
    /// <remarks>
    /// The living room lights are intentionally not used as an anchor: the light automation
    /// itself switches them on when the room is occupied, which would keep the room occupied
    /// forever and break the automatic turn off.
    /// </remarks>
    private bool HasActiveAnchor() => IsMotionDetected() || IsTvActive();

    private bool IsMotionDetected() => _entities.BinarySensor.Motionwoonkamer.IsOn();

    private bool IsTvActive() =>
        _entities.MediaPlayer.Tv.State is { } state
        && ActiveTvStates.Contains(state, StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        _subscription.Dispose();
        _isOccupiedSubject.Dispose();
        GC.SuppressFinalize(this);
    }
}
