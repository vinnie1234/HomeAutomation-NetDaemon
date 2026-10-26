using System.Text.Json.Serialization;
using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Helpers;

/// <summary>
/// Websocket command that changes settings of an existing helper, e.g. <c>input_select/update</c> with the new options.
/// Home Assistant expects the id as <c>{domain}_id</c> and replaces all settings: what is not sent (an icon, ...) is gone.
/// </summary>
public record UpdateHelperCommand : CommandMessage
{
    public UpdateHelperCommand(string domain, string id, IReadOnlyDictionary<string, object> settings)
    {
        Type = $"{domain}/update";
        Fields = new Dictionary<string, object>(settings) { [$"{domain}_id"] = id };
    }

    [JsonExtensionData]
    public Dictionary<string, object> Fields { get; init; }
}
