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
        Action<StateChange<T, EntityState<TAttributes>>> observer, int throttleInSeconds = 0)
        where TAttributes : class
        where T : Entity<T, EntityState<TAttributes>, TAttributes>
    {
        entity.StateChanges().Throttle(TimeSpan.FromSeconds(throttleInSeconds))
            .Where(c => c.Old?.IsOff() == true && (c.New?.IsOn() ?? false))
            .Subscribe(observer);
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
        Action<StateChange<T, EntityState<TAttributes>>> observer, int throttleInSeconds = 0)
        where TAttributes : class
        where T : Entity<T, EntityState<TAttributes>, TAttributes>
    {
        entity.StateChanges().Throttle(TimeSpan.FromSeconds(throttleInSeconds))
            .Where(c => c.Old?.IsOn() == true && (c.New?.IsOff() ?? false))
            .Subscribe(observer);
    }
}