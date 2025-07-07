namespace Automation.Core;

/// <summary>
/// Service for managing Home Assistant entities.
/// </summary>
public class EntityManager(IHaContext haContext, ILogger<EntityManager> logger) : IEntityManager
{
    /// <summary>
    /// Creates an entity in Home Assistant using input helpers.
    /// For now, this is a simple implementation that logs the creation intent.
    /// In the future, this could be extended to actually create entities via API.
    /// </summary>
    public void Create(string entityId, EntityCreationOptions options)
    {
        try
        {
            // Check if entity already exists
            if (EntityExists(entityId))
            {
                logger.LogDebug("Entity {EntityId} already exists, skipping creation", entityId);
                return;
            }

            // Create the entity by setting initial state with attributes
            var initialState = "unknown";
            var entityAttributes = new
            {
                friendly_name = options.Name,
                device_class = options.DeviceClass,
                unit_of_measurement = options.UnitOfMeasurement,
                icon = options.Icon
            };

            var serviceData = new
            {
                entity_id = entityId,
                state = initialState,
                attributes = entityAttributes
            };

            haContext.CallService("homeassistant", "set_state", data: serviceData);
            logger.LogInformation("Created entity {EntityId} with name '{Name}'", entityId, options.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create entity {EntityId}", entityId);
            throw;
        }
    }

    /// <summary>
    /// Sets the state of an entity by calling the appropriate service.
    /// </summary>
    public void SetState(string entityId, object state, object? attributes = null)
    {
        try
        {
            // Use the set_state service to update entity state
            var serviceData = new
            {
                entity_id = entityId,
                state = state.ToString(),
                attributes = attributes
            };

            haContext.CallService("homeassistant", "set_state", data: serviceData);
            
            logger.LogDebug("Set state for {EntityId} to {State}", entityId, state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set state for entity {EntityId}", entityId);
            throw;
        }
    }

    /// <summary>
    /// Checks if an entity exists by trying to get its state.
    /// </summary>
    public bool EntityExists(string entityId)
    {
        try
        {
            var state = haContext.GetState(entityId);
            return state != null;
        }
        catch
        {
            return false;
        }
    }
}