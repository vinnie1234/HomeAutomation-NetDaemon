namespace Automation.Interfaces;

/// <summary>
/// Interface for managing Home Assistant entities.
/// </summary>
public interface IEntityManager
{
    /// <summary>
    /// Creates an entity in Home Assistant.
    /// </summary>
    /// <param name="entityId">The entity ID to create.</param>
    /// <param name="options">Entity creation options.</param>
    Task Create(string entityId, EntityCreationOptions options);

    /// <summary>
    /// Sets the state of an entity.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="state">The state value.</param>
    /// <param name="attributes">Optional attributes.</param>
    void SetState(string entityId, object state, object? attributes = null);

    /// <summary>
    /// Checks if an entity exists.
    /// </summary>
    /// <param name="entityId">The entity ID to check.</param>
    /// <returns>True if the entity exists.</returns>
    bool EntityExists(string entityId);
}

/// <summary>
/// Options for creating entities.
/// </summary>
public record EntityCreationOptions
{
    public string Name { get; init; } = string.Empty;
    public string? DeviceClass { get; init; }
    public string? UnitOfMeasurement { get; init; }
    public string? Icon { get; init; }
}