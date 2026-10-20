using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Integrations;

/// <summary>
/// Websocket command that lists all entities in the entity registry.
/// </summary>
public record GetEntityRegistryCommand : CommandMessage
{
    public GetEntityRegistryCommand() => Type = "config/entity_registry/list";
}
