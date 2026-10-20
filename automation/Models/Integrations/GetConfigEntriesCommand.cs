using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Integrations;

/// <summary>
/// Websocket command that lists all integrations (config entries).
/// </summary>
public record GetConfigEntriesCommand : CommandMessage
{
    public GetConfigEntriesCommand() => Type = "config_entries/get";
}
