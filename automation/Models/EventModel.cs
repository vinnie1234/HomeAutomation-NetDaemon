// ReSharper disable UnusedAutoPropertyAccessor.Global

using System.Text.Json.Serialization;

#pragma warning disable CS8618
namespace Automation.Models;

// ReSharper disable once ClassNeverInstantiated.Global
public class EventModel
{
    // ReSharper disable once UnusedMember.Global
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; }

    // ReSharper disable once UnusedMember.Global
    [JsonPropertyName("unique_id")]
    public string UniqueId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("subtype")]
    public int Subtype { get; set; }    
}