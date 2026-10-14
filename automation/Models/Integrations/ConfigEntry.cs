using System.Text.Json.Serialization;
using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Integrations;

/// <summary>
/// An integration (config entry) as returned by the Home Assistant <c>config_entries/get</c> websocket command.
/// </summary>
public record ConfigEntry
{
    /// <summary>The config entry states that mean the integration failed to load.</summary>
    private static readonly string[] FailedStates = ["setup_error", "setup_retry", "migration_error", "failed_unload"];

    [JsonPropertyName("entry_id")]
    public string EntryId { get; init; } = string.Empty;

    [JsonPropertyName("domain")]
    public string Domain { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>The load state, e.g. <c>loaded</c>, <c>setup_retry</c> or <c>setup_error</c>.</summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>The error Home Assistant gave for the current state, if any.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>Who disabled the integration, or <c>null</c> when it is enabled.</summary>
    [JsonPropertyName("disabled_by")]
    public string? DisabledBy { get; init; }

    /// <summary>Gets whether the integration is enabled but failed to load.</summary>
    [JsonIgnore]
    public bool HasFailed => DisabledBy is null && FailedStates.Contains(State);
}

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

/// <summary>
/// Websocket command that lists all integrations (config entries).
/// </summary>
public record GetConfigEntriesCommand : CommandMessage
{
    public GetConfigEntriesCommand() => Type = "config_entries/get";
}

/// <summary>
/// Websocket command that lists all entities in the entity registry.
/// </summary>
public record GetEntityRegistryCommand : CommandMessage
{
    public GetEntityRegistryCommand() => Type = "config/entity_registry/list";
}
