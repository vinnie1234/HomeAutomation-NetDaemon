using System.Reactive.Concurrency;

namespace Automation.Extensions;

/// <summary>
/// Provides extension methods for entities.
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    /// Subscribes to state changes when the entity turns on.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <typeparam name="TAttributes">The type of the entity's attributes.</typeparam>
    /// <param name="entity">The entity to observe.</param>
    /// <param name="observer">The action to perform when the entity turns on.</param>
    /// <param name="throttleInSeconds">The throttle duration in seconds.</param>
    /// <param name="scheduler">The scheduler for the throttle; the default scheduler when null.</param>
    public static void WhenTurnsOn<T, TAttributes>(this Entity<T, EntityState<TAttributes>, TAttributes> entity,
        Action<StateChange<T, EntityState<TAttributes>>> observer, int throttleInSeconds = 0, IScheduler? scheduler = null)
        where TAttributes : class
        where T : Entity<T, EntityState<TAttributes>, TAttributes>
    {
        var changes = entity.StateChanges()
            .Where(c => c.Old?.IsOff() == true
                     && (c.New?.IsOn() ?? false)
                     && c.Old?.State is not (null or "unknown" or "unavailable"));
                     
        // After the waiting time, only act when the entity is still on (it may have turned off again meanwhile)
        if (throttleInSeconds > 0)
            changes = changes
                .Throttle(TimeSpan.FromSeconds(throttleInSeconds), scheduler ?? DefaultScheduler.Instance)
                .Where(_ => entity.IsOn());

        changes.Subscribe(observer);
    }

    /// <summary>
    /// Subscribes to state changes when the entity turns off.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <typeparam name="TAttributes">The type of the entity's attributes.</typeparam>
    /// <param name="entity">The entity to observe.</param>
    /// <param name="observer">The action to perform when the entity turns off.</param>
    /// <param name="throttleInSeconds">The throttle duration in seconds.</param>
    /// <param name="scheduler">The scheduler for the throttle; the default scheduler when null.</param>
    public static void WhenTurnsOff<T, TAttributes>(this Entity<T, EntityState<TAttributes>, TAttributes> entity,
        Action<StateChange<T, EntityState<TAttributes>>> observer, int throttleInSeconds = 0, IScheduler? scheduler = null)
        where TAttributes : class
        where T : Entity<T, EntityState<TAttributes>, TAttributes>
    {
        var changes = entity.StateChanges()
            .Where(c => c.Old?.IsOn() == true
                     && (c.New?.IsOff() ?? false)
                     && c.Old?.State is not (null or "unknown" or "unavailable"));
                     
        // After the waiting time, only act when the entity is still off (it may have turned on again meanwhile)
        if (throttleInSeconds > 0)
            changes = changes
                .Throttle(TimeSpan.FromSeconds(throttleInSeconds), scheduler ?? DefaultScheduler.Instance)
                .Where(_ => entity.IsOff());

        changes.Subscribe(observer);
    }

    /// <summary>
    /// Emits a state change once the entity has been off for the given duration. Unlike NetDaemon's
    /// <c>WhenStateIsFor</c>, the duration is determined each time the entity turns off, so changes to the
    /// settings it is based on (e.g. an input_number in Home Assistant) apply without restarting.
    /// </summary>
    /// <typeparam name="T">The type of the state changes.</typeparam>
    /// <param name="changes">The state changes of the entity.</param>
    /// <param name="duration">Determines how long the entity must stay off.</param>
    /// <param name="scheduler">The scheduler for timing.</param>
    public static IObservable<T> WhenOffFor<T>(this IObservable<T> changes, Func<TimeSpan> duration, IScheduler scheduler)
        where T : StateChange =>
        changes
            .Select(change => change.New.IsOff()
                ? Observable.Timer(duration(), scheduler).Select(_ => change)
                : Observable.Empty<T>())
            .Switch();
}