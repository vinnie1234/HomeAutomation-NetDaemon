using System.Text.Json.Serialization;
using NetDaemon.Client.HomeAssistant.Model;

namespace Automation.Models.Traffic;

/// <summary>Websocket command of the 9292 OV integration: the legs, stops and platforms of one journey.</summary>
public record GetJourneyCommand : CommandMessage
{
    public GetJourneyCommand(string journeyId, string? routeId)
    {
        Type = "ov_9292/journey";
        JourneyId = journeyId;
        RouteId = routeId;
    }

    [JsonPropertyName("journey_id")]
    public string JourneyId { get; init; }

    [JsonPropertyName("route_id")]
    public string? RouteId { get; init; }
}

/// <summary>One journey with all its legs, as the 9292 OV integration gives it.</summary>
public record JourneyDetail
{
    [JsonPropertyName("legs")]
    public List<JourneyLeg> Legs { get; init; } = [];
}

/// <summary>A part of a journey: walking, or a ride on a bus, tram, metro or train.</summary>
public record JourneyLeg
{
    /// <summary>Walking, Bus, Tram, Metro, SlowTrain (sprinter), FastTrain (intercity), ...</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("line")]
    public string? Line { get; init; }

    /// <summary>Where the vehicle goes.</summary>
    [JsonPropertyName("destination")]
    public string? Destination { get; init; }

    [JsonPropertyName("from")]
    public JourneyStop? From { get; init; }

    [JsonPropertyName("to")]
    public JourneyStop? To { get; init; }

    /// <summary>The stops in between.</summary>
    [JsonPropertyName("stops")]
    public List<JourneyStop> Stops { get; init; } = [];

    [JsonPropertyName("distance_m")]
    public double? DistanceM { get; init; }

    [JsonPropertyName("disruptions")]
    public IReadOnlyList<string> Disruptions { get; init; } = [];

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; init; }
}

/// <summary>A stop, station or address on a leg.</summary>
public record JourneyStop
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The planned time.</summary>
    [JsonPropertyName("time")]
    public string? Time { get; init; }

    /// <summary>The real-time time.</summary>
    [JsonPropertyName("real")]
    public string? Real { get; init; }

    [JsonPropertyName("platform")]
    public string? Platform { get; init; }
}
