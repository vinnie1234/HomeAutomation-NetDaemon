using System.Text.Json.Serialization;

namespace Automation.Models.Integrations;

/// <summary>
/// An entity as returned by the Home Assistant <c>config/entity_registry/list</c> websocket command.
/// </summary>
public record EntityRegistryEntry
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; init; } = string.Empty;

    [JsonPropertyName("config_entry_id")]
    public string? ConfigEntryId { get; init; }

    /// <summary>Who disabled the entity, or <c>null</c> when it is enabled.</summary>
    [JsonPropertyName("disabled_by")]
    public string? DisabledBy { get; init; }
}
