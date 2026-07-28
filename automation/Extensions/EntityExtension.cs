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
    public static void WhenTurnsOn<T, TAttributes>(this Entity<T, EntityState<TAttributes>, TAttributes> entity,
        Action<StateChange<T, EntityState<TAttributes>>> observer, int throttleInSeconds = 0, IScheduler? scheduler = null)
        where TAttributes : class
        where T : Entity<T, EntityState<TAttributes>, TAttributes>
    {
        var changes = entity.StateChanges()
            .Where(c => c.Old?.IsOff() == true
                     && (c.New?.IsOn() ?? false)
                     && c.Old?.State is not (null or "unknown" or "unavailable"));
                     
        if (throttleInSeconds > 0)
            changes = changes.Throttle(TimeSpan.FromSeconds(throttleInSeconds), scheduler ?? DefaultScheduler.Instance);
            
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
    public static void WhenTurnsOff<T, TAttributes>(this Entity<T, EntityState<TAttributes>, TAttributes> entity,
        Action<StateChange<T, EntityState<TAttributes>>> observer, int throttleInSeconds = 0, IScheduler? scheduler = null)
        where TAttributes : class
        where T : Entity<T, EntityState<TAttributes>, TAttributes>
    {
        var changes = entity.StateChanges()
            .Where(c => c.Old?.IsOn() == true
                     && (c.New?.IsOff() ?? false)
                     && c.Old?.State is not (null or "unknown" or "unavailable"));
                     
        if (throttleInSeconds > 0)
            changes = changes.Throttle(TimeSpan.FromSeconds(throttleInSeconds), scheduler ?? DefaultScheduler.Instance);
            
        changes.Subscribe(observer);
    }
}