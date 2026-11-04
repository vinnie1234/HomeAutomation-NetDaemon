using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Models.Traffic;

namespace Automation.apps.General;

/// <summary>
/// Follows who is in the train, from the zones around the stations. Leaving a station zone and then, within a few minutes, being in the
/// passive zone on the track means the person sits in the train that left the station: that train is chosen as their trip
/// (<c>input_text.reis_keuze_{person}_{work|home}</c>). Not being in the zone on the track in time means they did not take the train:
/// the route is then no longer needed (<c>input_boolean.reis_niet_{person}_{work|home}</c>, which hides it on the dashboard).
/// </summary>
public partial class CommuteReport
{
    /// <summary>A station, the passive zone on the track next to it, and the way of the trip that starts there.</summary>
    private sealed record StationLink(string Station, string Track, CommuteDirection Direction);

    /// <summary>Nijmegen is where the trip home starts, De Klomp (Veenendaal) the trip to the office.</summary>
    private static readonly StationLink[] StationLinks =
    [
        new("zone.station_nijmegen", "zone.trein_nijmegen_arnhem", CommuteDirection.ToHome),
        new("zone.de_klomp", "zone.trein_de_klomp_arnhem", CommuteDirection.ToWork)
    ];

    /// <summary>Who arrives at this station for an appointment is there: the other routes of the person are no longer needed on the dashboard.</summary>
    private const string AppointmentStation = "zone.station_nijmegen";

    /// <summary>
    /// The zone on the track at Ede: whoever takes the bus to Ede and the train from there is in it when that train passes. The train is
    /// found by its stop at Ede (the passenger never was in a station zone).
    /// </summary>
    private const string EdeTrack = "zone.trein_ede_arnhem";

    /// <summary>How far back the trips of the person are remembered: a trip has gone from the sensor by the time the train is at Ede.</summary>
    private static readonly TimeSpan RememberTrips = TimeSpan.FromHours(3);

    /// <summary>How long after leaving the station the person has to be in the zone on the track.</summary>
    public static readonly TimeSpan InTrainWithin = TimeSpan.FromMinutes(5);

    /// <summary>How far the departure of a train may be from the moment the person left the station.</summary>
    private static readonly TimeSpan SameTrain = TimeSpan.FromMinutes(10);

    /// <summary>A person who left a station and has to show up in the zone on the track, with the trip they most likely took.</summary>
    private sealed record Departure(DateTimeOffset Left, TransitTrip? Trip);

    private readonly Dictionary<string, Dictionary<string, TransitTrip>> _knownTrips = [];
    private readonly HashSet<string> _onEdeTrack = [];
    private readonly Dictionary<string, Departure> _departures = [];
    private readonly Dictionary<string, IReadOnlyList<TransitTrip>> _tripsAtStation = [];
    private readonly HashSet<string> _atStation = [];
    private readonly Dictionary<string, string> _appointments = [];

    private void SetUpTrainTracking()
    {
        // Who is at a station already (also after a restart) has not just arrived there
        foreach (var commuter in _commuters)
            foreach (var link in StationLinks)
                if (InZone(link.Station, commuter.PersonId)) _atStation.Add($"{commuter.Id}|{link.Station}");
        foreach (var link in StationLinks)
        {
            new Entity(HaContext, link.Station).StateChanges().Subscribe(_ => FollowTrains());
            new Entity(HaContext, link.Track).StateChanges().Subscribe(_ => FollowTrains());
        }
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), FollowTrains);
        // Every night the routes are needed again
        Scheduler.ScheduleCronNl("0 4 * * *", () =>
        {
            foreach (var commuter in _commuters)
                foreach (var direction in new[] { CommuteDirection.ToWork, CommuteDirection.ToHome })
                    SetRouteNotNeeded(commuter, direction, false);
            foreach (var commuter in _commuters)
                foreach (var helper in new[] { "afspraak_route", "afspraak_aangekomen" })
                    if (HaContext.GetState($"input_boolean.{helper}_{commuter.Id}")?.State == "on")
                        HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity($"input_boolean.{helper}_{commuter.Id}"));
        });
    }

    private void FollowTrains()
    {
        try
        {
            var now = Scheduler.Now;
            foreach (var commuter in _commuters)
            {
                FollowAppointment(commuter);
                FollowEde(commuter, now);
            }
            foreach (var commuter in _commuters)
                foreach (var link in StationLinks)
                {
                    var key = $"{commuter.Id}|{link.Station}";
                    if (InZone(link.Station, commuter.PersonId))
                    {
                        // At the station: remember the trips (the one they take has gone from the sensor by the time they are on the track)
                        var justArrived = _atStation.Add(key);
                        _departures.Remove(key);
                        _tripsAtStation[key] = AllTrips(commuter, link.Direction);
                        // Only on arriving: someone who stays in the zone (an office next to the station) keeps the choice "does not come home"
                        if (justArrived) SetRouteNotNeeded(commuter, link.Direction, false);
                    }
                    else if (_atStation.Remove(key) && IsTimeForTrip(link.Direction))
                    {
                        _departures[key] = new Departure(now, ClosestTrip(_tripsAtStation.GetValueOrDefault(key), now));
                    }

                    if (!_departures.TryGetValue(key, out var departure)) continue;
                    if (InZone(link.Track, commuter.PersonId))
                    {
                        _departures.Remove(key);
                        if (departure.Trip != null) ChooseTrip(commuter, link.Direction, departure.Trip);
                        SetRouteNotNeeded(commuter, link.Direction, false);
                        Logger.LogInformation("{Person} sits in the train of {Departure}", commuter.Id, departure.Trip?.Departure);
                    }
                    else if (now - departure.Left > InTrainWithin)
                    {
                        _departures.Remove(key);
                        SetRouteNotNeeded(commuter, link.Direction, true);
                        Logger.LogInformation("{Person} did not get into the train within {Minutes} minutes, the {Direction} route is not needed", commuter.Id, InTrainWithin.TotalMinutes, link.Direction);
                    }
                }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not follow the trains");
        }
    }

    /// <summary>
    /// Remembers the trips to the office, and when the person comes into the zone on the track at Ede in the morning without a train chosen yet
    /// (<c>input_text.reis_keuze_{person}_work</c>), chooses the trip whose train was at Ede about now.
    /// </summary>
    private void FollowEde(Commuter commuter, DateTimeOffset now)
    {
        var known = _knownTrips.TryGetValue(commuter.Id, out var existing) ? existing : _knownTrips[commuter.Id] = [];
        foreach (var trip in AllTrips(commuter, CommuteDirection.ToWork).Where(t => t.Id != null)) known[trip.Id!] = trip;
        foreach (var old in known.Where(k => k.Value.Departure < now - RememberTrips).Select(k => k.Key).ToList()) known.Remove(old);

        if (!InZone(EdeTrack, commuter.PersonId))
        {
            _onEdeTrack.Remove(commuter.Id);
            return;
        }
        if (!_onEdeTrack.Add(commuter.Id) || !IsTimeForTrip(CommuteDirection.ToWork) || ChosenDeparture(commuter, CommuteDirection.ToWork) != null) return;
        var candidates = known.Values.Where(t => t.Departure <= now && !t.Cancelled).ToList();
        if (candidates.Count > 0) _ = ChooseTripAtEdeAsync(commuter, candidates, now);
    }

    /// <summary>Asks for the details of the trips that have left and chooses the one with a train that was at Ede within a few minutes of now.</summary>
    private async Task ChooseTripAtEdeAsync(Commuter commuter, List<TransitTrip> candidates, DateTimeOffset now)
    {
        try
        {
            var routeId = FindTransitPrefix(commuter.PersonId, CommuteDirection.ToWork) is { } prefix
                && HaContext.GetState($"{prefix}_lijnen")?.AttributesJson is { ValueKind: JsonValueKind.Object } a ? Text(a, "route_id") : null;
            TransitTrip? best = null;
            var bestGap = TimeSpan.MaxValue;
            foreach (var trip in candidates)
            {
                var detail = await GetJourneyAsync(trip.Id!, routeId);
                foreach (var leg in detail?.Legs ?? [])
                {
                    if (leg.Mode is null or "Walking" or "Bus" or "Tram" or "Metro" or "Ferry") continue;
                    foreach (var stop in new[] { leg.From }.Concat(leg.Stops).Concat([leg.To]))
                    {
                        if (stop?.Name?.Contains("Ede", StringComparison.OrdinalIgnoreCase) != true
                            || !DateTimeOffset.TryParse(stop.Real ?? stop.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) continue;
                        var gap = (at - now).Duration();
                        if (gap <= SameTrain && gap < bestGap) (best, bestGap) = (trip, gap);
                    }
                }
            }
            if (best == null) return;
            ChooseTrip(commuter, CommuteDirection.ToWork, best);
            Logger.LogInformation("{Person} sits in the train from Ede of {Departure}", commuter.Id, best.Departure);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not find the train of {Person} at Ede", commuter.Id);
        }
    }

    /// <summary>
    /// With an appointment (<c>input_text.afspraak_{person}</c>) the other routes of the person come on Home six hours before it, but only
    /// when the person is at home then (<c>input_boolean.afspraak_route_{person}</c> goes on). Once on they stay, also when the person has left
    /// home, until the person arrives at the station in Nijmegen (<c>input_boolean.afspraak_aangekomen_{person}</c>) or the appointment is over.
    /// Both go off again for the next appointment.
    /// </summary>
    private void FollowAppointment(Commuter commuter)
    {
        var appointment = HaContext.GetState($"input_text.afspraak_{commuter.Id}")?.State ?? "";
        var shownId = $"input_boolean.afspraak_route_{commuter.Id}";
        var arrivedId = $"input_boolean.afspraak_aangekomen_{commuter.Id}";
        var shown = HaContext.GetState(shownId)?.State == "on";
        var arrived = HaContext.GetState(arrivedId)?.State == "on";
        // A new appointment starts afresh; after a restart the one that was there is still the same, so what was on stays on
        if (_appointments.TryGetValue(commuter.Id, out var known) && known != appointment)
        {
            if (shown) HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity(shownId));
            if (arrived) HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity(arrivedId));
            _appointments[commuter.Id] = appointment;
            return;
        }
        _appointments[commuter.Id] = appointment;
        if (!AppointmentIsNear(appointment)) return;
        if (!shown && commuter.Person.IsHome)
            HaContext.CallService("input_boolean", "turn_on", ServiceTarget.FromEntity(shownId));
        if (!arrived && InZone(AppointmentStation, commuter.PersonId))
            HaContext.CallService("input_boolean", "turn_on", ServiceTarget.FromEntity(arrivedId));
    }

    /// <summary>Whether the appointment ("start|end|title") begins within six hours (the dashboard shows the other routes from then) and is not over.</summary>
    private bool AppointmentIsNear(string appointment)
    {
        var parts = appointment.Split('|');
        if (parts.Length < 2
            || !DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) return false;
        // The times are Dutch clock times: not the clock of the machine this runs on
        DateTimeOffset InDutchTime(DateTime clock) => new(clock, Extensions.SchedulerExtensions.DutchTimeZone.GetUtcOffset(clock));
        var now = Scheduler.NowNl();
        return InDutchTime(start) - now <= TimeSpan.FromHours(6) && now < InDutchTime(end);
    }

    /// <summary>The trip to the office starts in the morning, the trip home in the afternoon: leaving a station at the other time of day (arriving) is no departure.</summary>
    private bool IsTimeForTrip(CommuteDirection direction) =>
        (Scheduler.NowNl().Hour < 12) == (direction == CommuteDirection.ToWork);

    private bool InZone(string zone, string personId) =>
        HaContext.GetState(zone)?.AttributesJson is { ValueKind: JsonValueKind.Object } a
        && a.TryGetProperty("persons", out var persons) && persons.ValueKind == JsonValueKind.Array
        && persons.EnumerateArray().Any(p => p.GetString() == personId);

    /// <summary>Every trip of the public transport route of this person in this direction, including the ones before the time to plan around.</summary>
    private IReadOnlyList<TransitTrip> AllTrips(Commuter commuter, CommuteDirection direction)
    {
        var prefix = FindTransitPrefix(commuter.PersonId, direction);
        if (prefix == null || HaContext.GetState($"{prefix}_lijnen")?.AttributesJson is not { ValueKind: JsonValueKind.Object } a) return [];
        return TransitTrips(a, "earlier").Concat(TransitTrips(a, "upcoming")).ToList();
    }

    private static TransitTrip? ClosestTrip(IReadOnlyList<TransitTrip>? trips, DateTimeOffset left) =>
        trips?.Where(t => (t.Departure - left).Duration() <= SameTrain).MinBy(t => (t.Departure - left).Duration());

    private void ChooseTrip(Commuter commuter, CommuteDirection direction, TransitTrip trip) =>
        HaContext.CallService("input_text", "set_value", ServiceTarget.FromEntity($"input_text.reis_keuze_{commuter.Id}_{Side(direction)}"),
            new { value = PlannedStamp(trip) });

    private void SetRouteNotNeeded(Commuter commuter, CommuteDirection direction, bool notNeeded)
    {
        var entityId = $"input_boolean.reis_niet_{commuter.Id}_{Side(direction)}";
        if ((HaContext.GetState(entityId)?.State == "on") == notNeeded) return;
        HaContext.CallService("input_boolean", notNeeded ? "turn_on" : "turn_off", ServiceTarget.FromEntity(entityId));
    }

    private static string Side(CommuteDirection direction) => direction == CommuteDirection.ToWork ? "work" : "home";
}
