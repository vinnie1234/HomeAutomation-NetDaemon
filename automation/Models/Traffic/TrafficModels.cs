namespace Automation.Models.Traffic;

/// <summary>Which of the two commutes a route is.</summary>
public enum CommuteDirection
{
    /// <summary>From home to the office.</summary>
    ToWork,

    /// <summary>From the office back home.</summary>
    ToHome
}

/// <summary>A point on the map.</summary>
/// <param name="Lat">Latitude in degrees.</param>
/// <param name="Lon">Longitude in degrees.</param>
public readonly record struct GeoPoint(double Lat, double Lon);

/// <summary>An alternative route the ANWB offers next to the fastest one.</summary>
/// <param name="Via">The motorways and roads it follows.</param>
/// <param name="DurationMin">Its travel time in minutes.</param>
/// <param name="DifferenceMin">How many minutes it takes longer than the fastest route.</param>
public record RouteAlternative(IReadOnlyList<string> Via, double DurationMin, double DifferenceMin);

/// <summary>The fastest route of the ANWB File integration, as it is right now.</summary>
/// <param name="DurationMin">The travel time in minutes, including the delay.</param>
/// <param name="NormalMin">The travel time in minutes without any traffic.</param>
/// <param name="DelayMin">The delay in minutes caused by traffic.</param>
/// <param name="Arrival">When you arrive when you leave now, if known.</param>
/// <param name="Via">The motorways and roads the route follows.</param>
/// <param name="Origin">Where the route starts.</param>
/// <param name="Destination">Where the route ends.</param>
/// <param name="Alternatives">Other routes.</param>
public record RouteSnapshot(
    double DurationMin,
    double NormalMin,
    double DelayMin,
    DateTimeOffset? Arrival,
    IReadOnlyList<string> Via,
    GeoPoint Origin,
    GeoPoint Destination,
    IReadOnlyList<RouteAlternative> Alternatives);

/// <summary>A jam or a speed camera reported by the ANWB File integration.</summary>
/// <param name="Road">The road, such as "A12".</param>
/// <param name="Direction">The carriageway, such as "Utrecht - Arnhem" (heading for Arnhem).</param>
/// <param name="From">Where it starts.</param>
/// <param name="To">Where it ends.</param>
/// <param name="Reason">What the ANWB says about it.</param>
/// <param name="LengthKm">The length of a jam in kilometres.</param>
/// <param name="DelayMin">The delay of a jam in minutes.</param>
/// <param name="Start">Since when a jam is there.</param>
/// <param name="Position">Where it is.</param>
/// <param name="Path">The line a jam follows, in driving order; empty for a speed camera.</param>
public record TrafficIncident(
    string Road,
    string? Direction,
    string? From,
    string? To,
    string? Reason,
    double? LengthKm,
    double? DelayMin,
    DateTimeOffset? Start,
    GeoPoint Position,
    IReadOnlyList<GeoPoint> Path);
