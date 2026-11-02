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

/// <summary>One public transport trip of the 9292 OV integration.</summary>
/// <param name="Departure">When it leaves (actual).</param>
/// <param name="Arrival">When it arrives.</param>
/// <param name="DurationMin">Its travel time in minutes.</param>
/// <param name="DelayMin">Its delay in minutes.</param>
/// <param name="Transfers">How many times you change.</param>
/// <param name="Legs">The parts of the trip with a line: the mode (Bus, FastTrain, ...) and the line number.</param>
/// <param name="Cancelled">Whether a part of the trip is cancelled.</param>
public record TransitTrip(
    DateTimeOffset Departure,
    DateTimeOffset Arrival,
    double DurationMin,
    double DelayMin,
    int Transfers,
    IReadOnlyList<TransitLeg> Legs,
    bool Cancelled);

/// <summary>A part of a trip with a line.</summary>
/// <param name="Mode">The kind of vehicle: Bus, Tram, Metro, SlowTrain, FastTrain, ...</param>
/// <param name="Line">The line number.</param>
public record TransitLeg(string Mode, string Line);

/// <summary>The next trips of a public transport route of the 9292 OV integration, the first being the next one.</summary>
/// <param name="From">Where it starts.</param>
/// <param name="To">Where it ends.</param>
/// <param name="Trips">The coming trips, soonest first.</param>
public record TransitRoute(string? From, string? To, IReadOnlyList<TransitTrip> Trips);

/// <summary>The time somebody wants to be at the office or home (an arrival), or leaves (a departure).</summary>
/// <param name="Time">The time.</param>
/// <param name="IsArrival">True for a time to be somewhere (the advice is when to leave for it), false for a time to leave (no advice needed).</param>
public record CommuteTarget(DateTimeOffset Time, bool IsArrival);
