using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Helpers;

/// <summary>
/// Websocket command that lists the helpers of one domain, e.g. <c>input_boolean/list</c>.
/// </summary>
public record ListHelpersCommand : CommandMessage
{
    public ListHelpersCommand(string domain) => Type = $"{domain}/list";
}
