using MQTTnet.Internal;
using NetDaemon.Extensions.MqttEntityManager;
using EntityCreationOptions = Automation.Interfaces.EntityCreationOptions;

namespace Automation.Core;

/// <summary>
/// Service for managing Home Assistant entities using MQTT Entity Manager.
/// </summary>
public class EntityManager(IMqttEntityManager mqttEntityManager, IHaContext haContext, ILogger<EntityManager> logger) : IEntityManager
{
    /// <summary>
    /// Creates an entity in Home Assistant using MQTT Entity Manager.
    /// </summary>
    public async Task Create(string entityId, EntityCreationOptions options)
    {
        try
        {
            // Check if entity already exists
            if (EntityExists(entityId))
            {
                logger.LogDebug("Entity {EntityId} already exists, skipping creation", entityId);
                return;
            }

            // Create sensor entity using MQTT Entity Manager
            var creationOptions = new NetDaemon.Extensions.MqttEntityManager.EntityCreationOptions
            {
                Name = options.Name,
                DeviceClass = options.DeviceClass
            };
            
            // Create attributes object for additional properties
            var attributes = new
            {
                unit_of_measurement = options.UnitOfMeasurement,
                icon = options.Icon
            };

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await mqttEntityManager.CreateAsync(entityId, creationOptions, attributes)
                    .WaitAsync(cts.Token);
                logger.LogInformation("Created entity {EntityId} with name '{Name}'", entityId, options.Name);
            }
            catch (TimeoutException)
            {
                logger.LogWarning("MQTT entity creation timed out for {EntityId}, but entity may still be created", entityId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create entity {EntityId} via MQTT", entityId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create entity {EntityId}", entityId);
            // Don't throw - allow app to continue
        }
    }

    /// <summary>
    /// Sets the state of an entity using MQTT Entity Manager.
    /// </summary>
    public void SetState(string entityId, object state, object? attributes = null)
    {
        try
        {
            // Use async method in fire-and-forget manner
            _ = Task.Run(async () =>
            {
                try
                {
                    await mqttEntityManager.SetStateAsync(entityId, state.ToString()!);
                    if (attributes != null)
                    {
                        await mqttEntityManager.SetAttributesAsync(entityId, attributes);
                    }
                    logger.LogDebug("Set state for {EntityId} to {State}", entityId, state);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to set state for entity {EntityId} via MQTT", entityId);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set state for entity {EntityId}", entityId);
            // Don't throw - allow app to continue
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