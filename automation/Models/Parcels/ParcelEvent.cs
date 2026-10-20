using System.Text.Json.Serialization;

namespace Automation.Models.Parcels;

/// <summary>
/// The data of a parcel event (<c>{integration}_parcel_registered</c>, <c>_parcel_status_changed</c> or
/// <c>_parcel_delivered</c>) of the PostNL, DHL NL, DPD and GLS integrations. They all send the same
/// carrier-agnostic parcel.
/// </summary>
public record ParcelEvent
{
    /// <summary>The carrier, e.g. <c>PostNL</c>, <c>DHL</c>, <c>DPD</c> or <c>GLS</c>.</summary>
    [JsonPropertyName("carrier")]
    public string? Carrier { get; init; }

    [JsonPropertyName("barcode")]
    public string? Barcode { get; init; }

    /// <summary>The web shop or person that sent the parcel.</summary>
    [JsonPropertyName("sender")]
    public string? Sender { get; init; }

    /// <summary>The normalised status: registered, in_transit, out_for_delivery, at_pickup_point, delivered, returning, problem or unknown.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>The status text of the carrier itself.</summary>
    [JsonPropertyName("raw_status")]
    public string? RawStatus { get; init; }

    /// <summary>Start of the expected delivery window (ISO 8601).</summary>
    [JsonPropertyName("planned_from")]
    public string? PlannedFrom { get; init; }

    /// <summary>End of the expected delivery window (ISO 8601).</summary>
    [JsonPropertyName("planned_to")]
    public string? PlannedTo { get; init; }

    /// <summary>The pickup point: a name, or for some carriers an object with a <c>name</c>.</summary>
    [JsonPropertyName("pickup_point")]
    public JsonElement? PickupPoint { get; init; }

    /// <summary>The track and trace page of the parcel.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
