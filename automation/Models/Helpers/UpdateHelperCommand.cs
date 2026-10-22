using System.Text.Json.Serialization;
using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Helpers;

/// <summary>
/// Websocket command that changes settings of an existing helper, e.g. <c>input_select/update</c> with the new options.
/// Home Assistant expects the id as <c>{domain}_id</c>; settings that are not sent stay as they are.
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
