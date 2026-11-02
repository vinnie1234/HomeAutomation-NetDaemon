using System.Globalization;
using Automation.Models.Traffic;

namespace Automation.Helpers;

/// <summary>
/// Turns the route of the ANWB File integration and the jams and speed cameras it reports into the message Vincent gets
/// before he leaves for the office or for home: the travel time, the jams on his route and how long they last, and the speed
/// cameras. Jams are matched to the route by road, by where they are and by the direction they go in; speed cameras have no
/// direction we can compare, so they are shown with the direction the ANWB gives.
/// </summary>
public static class CommuteMessage
{
    /// <summary>How far outside the straight line between start and end a jam or camera may be and still count as on the route, in degrees (~3 km).</summary>
    private const double Margin = 0.03;

    private static readonly CultureInfo Dutch = new("nl-NL");

    /// <summary>
    /// Which way a route goes, from its name: when "werk" (or "kantoor") comes before "thuis" (or "huis") it is the way home, when
    /// "thuis" comes first the way to work, whatever else the name says ("Werk => Huis", "Huis -> Werk", "ANWB Route Thuis - Kantoor").
    /// When the name has only one of them ("9292 Thuis -> Nijmegen", "Nijmegen -> Thuis"), the place named first in the name is where the
    /// route starts: "thuis" in the first half means the way to work, in the second half the way home, and the other way around for "werk".
    /// Null when the name does not tell.
    /// </summary>
    public static CommuteDirection? DirectionOfName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var work = System.Text.RegularExpressions.Regex.Match(name, "werk|kantoor", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var home = System.Text.RegularExpressions.Regex.Match(name, "thuis|huis", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (work.Success && home.Success) return work.Index < home.Index ? CommuteDirection.ToHome : CommuteDirection.ToWork;
        if (home.Success) return home.Index < name.Length / 2 ? CommuteDirection.ToWork : CommuteDirection.ToHome;
        if (work.Success) return work.Index < name.Length / 2 ? CommuteDirection.ToHome : CommuteDirection.ToWork;
        return null;
    }

    /// <summary>The jams on the route: on one of its roads, inside its area and going the same way.</summary>
    public static IReadOnlyList<TrafficIncident> JamsOnRoute(RouteSnapshot route, IEnumerable<TrafficIncident> jams) =>
        jams.Where(jam => OnRoad(route, jam) && (jam.Path.Count > 0 ? jam.Path.Any(p => InArea(route, p)) : InArea(route, jam.Position))
                          && SameDirection(route, jam))
            .OrderByDescending(jam => jam.DelayMin ?? 0)
            .ToList();

    /// <summary>The speed cameras on one of the roads of the route, inside its area.</summary>
    public static IReadOnlyList<TrafficIncident> RadarsOnRoute(RouteSnapshot route, IEnumerable<TrafficIncident> radars) =>
        radars.Where(radar => OnRoad(route, radar) && InArea(route, radar.Position)).ToList();

    /// <summary>The WhatsApp message for one commute.</summary>
    /// <param name="direction">Whether he goes to the office or home.</param>
    /// <param name="route">The fastest route.</param>
    /// <param name="jams">All jams, any road.</param>
    /// <param name="radars">All speed cameras, any road.</param>
    /// <param name="now">The current time.</param>
    /// <param name="target">When he has to be at the office (to work) or leaves it (home), if known.</param>
    public static string Build(CommuteDirection direction, RouteSnapshot route, IEnumerable<TrafficIncident> jams,
        IEnumerable<TrafficIncident> radars, DateTimeOffset now, CommuteTarget? target = null)
    {
        var lines = new List<string>
        {
            direction == CommuteDirection.ToWork ? "🚗 *Naar kantoor*" : "🏠 *Naar huis*"
        };

        var travel = $"⏱️ Reistijd: {Minutes(route.DurationMin)}";
        travel += route.DelayMin >= 1
            ? $" (normaal {Minutes(route.NormalMin)}, +{Minutes(route.DelayMin)} door het verkeer)"
            : " (geen vertraging)";
        lines.Add(travel);
        if (target is { } wanted)
        {
            // A time to be somewhere gets the advice when to leave for it; a time to leave needs none
            var place = direction == CommuteDirection.ToWork ? "op kantoor" : "thuis";
            lines.Add(wanted.IsArrival
                ? $"🎯 {char.ToUpper(place[0])}{place[1..]} om {Clock(wanted.Time)}: uiterlijk vertrekken om {Clock(wanted.Time - TimeSpan.FromMinutes(route.DurationMin))}"
                : $"🎯 Vertrek om {Clock(wanted.Time)}");
        }
        if (route.Arrival is { } arrival) lines.Add($"🕐 Aankomst rond {Clock(arrival)} als je nu vertrekt");
        if (route.Via.Count > 0) lines.Add($"🛣️ Via {string.Join(" · ", route.Via)}");

        var jamsOnRoute = JamsOnRoute(route, jams);
        if (jamsOnRoute.Count == 0)
        {
            lines.Add(route.DelayMin >= 3
                ? "🚦 Geen file gemeld op je route, maar het is wel drukker dan normaal"
                : "✅ Geen files op je route");
        }
        else
        {
            // The route is the fastest the ANWB found: when it has no delay while the jams on its roads have, it goes around them
            var jamDelay = jamsOnRoute.Sum(j => j.DelayMin ?? 0);
            var goesAround = jamDelay >= 5 && route.DelayMin < Math.Max(3, jamDelay * 0.25);
            lines.Add(goesAround
                ? $"ℹ️ Files op de wegen van je route ({jamsOnRoute.Count}), maar de route rijdt eromheen:"
                : $"⚠️ Files op je route ({jamsOnRoute.Count}):");
            lines.AddRange(jamsOnRoute.Take(4).Select(jam => $"• {Describe(jam, now)}"));
        }

        var radarsOnRoute = RadarsOnRoute(route, radars);
        if (radarsOnRoute.Count == 0)
            lines.Add("📸 Geen flitsers gemeld op je route");
        else
        {
            lines.Add("📸 Flitsers op je route (let op de richting):");
            lines.AddRange(radarsOnRoute.Take(6).Select(radar => $"• {DescribeRadar(radar)}"));
        }

        if (route.DelayMin >= 5 && route.Alternatives.MinBy(a => a.DurationMin) is { } alternative)
            lines.Add($"↪️ Alternatief via {string.Join(" · ", alternative.Via)}: {Minutes(alternative.DurationMin)} " +
                      $"({(alternative.DifferenceMin <= 0 ? "-" : "+")}{Minutes(Math.Abs(alternative.DifferenceMin))})");

        return string.Join("\n", lines);
    }


    /// <summary>The WhatsApp message for a public transport commute: the next trip with its lines, delay and cancellations, and the ones after it.</summary>
    /// <param name="direction">Whether he goes to the office or home.</param>
    /// <param name="route">The next trips.</param>
    /// <param name="target">When he has to be at the office (to work) or leaves it (home), if known.</param>
    public static string BuildTransit(CommuteDirection direction, TransitRoute route, CommuteTarget? target = null)
    {
        var lines = new List<string> { direction == CommuteDirection.ToWork ? "🚆 *Naar kantoor met het OV*" : "🚆 *Naar huis met het OV*" };
        if (target is { } wanted)
            lines.Add(wanted.IsArrival ? $"🎯 {(direction == CommuteDirection.ToWork ? "Op kantoor" : "Thuis")} om {Clock(wanted.Time)}" : $"🎯 Vertrek om {Clock(wanted.Time)}");
        if (route.Trips.Count == 0)
        {
            lines.Add("Geen reis gevonden.");
            return string.Join("\n", lines);
        }

        var next = route.Trips[0];
        lines.Add($"🕐 Eerstvolgende: vertrek {Clock(next.Departure)}, aankomst {Clock(next.Arrival)} ({Minutes(next.DurationMin)}" +
                  $"{(next.Transfers > 0 ? $", {next.Transfers} overstap{(next.Transfers == 1 ? "" : "pen")}" : ", zonder overstap")})");
        if (next.Legs.Count > 0) lines.Add($"{string.Join(" → ", next.Legs.Select(l => $"{ModeIcon(l.Mode)} {l.Line}"))}");
        lines.Add(next.Cancelled ? "❌ Deze reis vervalt (een deel is geschrapt)"
            : next.DelayMin >= 1 ? $"⚠️ {Minutes(next.DelayMin)} vertraging" : "✅ Op tijd");

        var later = route.Trips.Skip(1).Take(3).ToList();
        if (later.Count > 0)
            lines.Add("Daarna: " + string.Join(" · ", later.Select(t =>
                $"{Clock(t.Departure)}{(t.Cancelled ? " ❌" : t.DelayMin >= 1 ? $" (+{Math.Round(t.DelayMin)})" : "")} → {Clock(t.Arrival)}")));
        return string.Join("\n", lines);
    }

    private static string ModeIcon(string mode) => mode switch
    {
        "Bus" => "🚌",
        "Tram" => "🚋",
        "Metro" => "🚇",
        "Ferry" => "⛴️",
        _ => "🚆"
    };

    private static string Describe(TrafficIncident jam, DateTimeOffset now)
    {
        var text = $"{jam.Road} {jam.From} - {jam.To}";
        var details = new List<string>();
        if (jam.LengthKm is > 0) details.Add($"{jam.LengthKm.Value.ToString("0.#", Dutch)} km");
        if (jam.DelayMin is > 0) details.Add($"+{Minutes(jam.DelayMin.Value)}");
        if (jam.Start is { } start)
        {
            var age = now - start;
            details.Add(age.TotalMinutes >= 1
                ? $"al {(age.TotalMinutes >= 90 ? $"{Math.Round(age.TotalHours, 1).ToString("0.#", Dutch)} uur" : $"{Math.Round(age.TotalMinutes)} min")} (sinds {Clock(start)})"
                : $"sinds {Clock(start)}");
        }
        if (FirstSentence(jam.Reason) is { Length: > 0 } reason) details.Add(reason.TrimEnd('.').ToLowerInvariant());
        return details.Count == 0 ? text : $"{text}: {string.Join(", ", details)}";
    }

    private static string DescribeRadar(TrafficIncident radar)
    {
        var place = radar.From is { Length: > 0 } ? $"{radar.Road} bij {radar.From}" : radar.Road;
        var heading = radar.Direction?.Split(" - ").LastOrDefault();
        return heading is { Length: > 0 } ? $"{place}, richting {heading}" : place;
    }

    private static string? FirstSentence(string? text) => text?.Split('.', 2)[0].Trim();

    private static bool OnRoad(RouteSnapshot route, TrafficIncident incident) =>
        route.Via.Any(road => string.Equals(road, incident.Road, StringComparison.OrdinalIgnoreCase));

    private static bool InArea(RouteSnapshot route, GeoPoint point) =>
        point.Lat >= Math.Min(route.Origin.Lat, route.Destination.Lat) - Margin
        && point.Lat <= Math.Max(route.Origin.Lat, route.Destination.Lat) + Margin
        && point.Lon >= Math.Min(route.Origin.Lon, route.Destination.Lon) - Margin
        && point.Lon <= Math.Max(route.Origin.Lon, route.Destination.Lon) + Margin;

    /// <summary>A jam goes the same way as the route when its line points the same way as the line from start to end.</summary>
    private static bool SameDirection(RouteSnapshot route, TrafficIncident jam)
    {
        if (jam.Path.Count < 2) return true;
        var jamLat = jam.Path[^1].Lat - jam.Path[0].Lat;
        var jamLon = jam.Path[^1].Lon - jam.Path[0].Lon;
        var routeLat = route.Destination.Lat - route.Origin.Lat;
        var routeLon = route.Destination.Lon - route.Origin.Lon;
        return jamLat * routeLat + jamLon * routeLon > 0;
    }

    private static string Minutes(double minutes) => $"{Math.Round(minutes).ToString("0", Dutch)} min";

    private static string Clock(DateTimeOffset time) => Extensions.SchedulerExtensions.ToDutchTime(time).ToString("HH:mm", Dutch);
}
