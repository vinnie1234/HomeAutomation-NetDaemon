using System.Text.Json.Serialization;

namespace Automation.Models.Parcels;

/// <summary>
/// The data of the <c>postnl_letter_announced</c> event: PostNL announced a letter, with a scan of the envelope.
/// </summary>
public record LetterEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The title PostNL gives the letter, e.g. <c>Brief 17 oktober</c>.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>The day the letter is delivered (ISO 8601 date).</summary>
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    /// <summary>The scan of the envelope.</summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }
}
