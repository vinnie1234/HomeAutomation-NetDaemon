using System.Text.Json.Serialization;
using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Helpers;

/// <summary>
/// Websocket command that creates a helper, e.g. <c>input_boolean/create</c>. The settings are sent as fields next to
/// the name, the same way the Home Assistant UI does it.
/// </summary>
public record CreateHelperCommand : CommandMessage
{
    public CreateHelperCommand(HelperDefinition helper)
    {
        Type = $"{helper.Domain}/create";
        Name = helper.Name;
        Settings = helper.Settings.ToDictionary(x => x.Key, x => x.Value);
    }

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object> Settings { get; init; }
}
