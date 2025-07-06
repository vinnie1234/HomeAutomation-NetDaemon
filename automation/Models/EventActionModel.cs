// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

using System.Text.Json.Serialization;

#pragma warning disable CS8618
namespace Automation.Models;

// ReSharper disable once ClassNeverInstantiated.Global
public class EventActionModel
{
    [JsonPropertyName("sticky")]
    public string Sticky { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("webhook_id")]
    public string WebhookId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("action_1_title")]
    public string Action1Title { get; set; }

    [JsonPropertyName("action_1_key")]
    public string Action1Key { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; }
}