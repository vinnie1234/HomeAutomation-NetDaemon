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

    /// <summary>How long after leaving the station the person has to be in the zone on the track.</summary>
    public static readonly TimeSpan InTrainWithin = TimeSpan.FromMinutes(5);

    /// <summary>How far the departure of a train may be from the moment the person left the station.</summary>
    private static readonly TimeSpan SameTrain = TimeSpan.FromMinutes(10);

    /// <summary>A person who left a station and has to show up in the zone on the track, with the trip they most likely took.</summary>
    private sealed record Departure(DateTimeOffset Left, TransitTrip? Trip);

    private readonly Dictionary<string, Departure> _departures = [];
    private readonly Dictionary<string, IReadOnlyList<TransitTrip>> _tripsAtStation = [];
    private readonly HashSet<string> _atStation = [];
    private readonly Dictionary<string, string> _appointments = [];

    private void SetUpTrainTracking()
    {
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
                if (HaContext.GetState($"input_boolean.afspraak_aangekomen_{commuter.Id}")?.State == "on")
                    HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity($"input_boolean.afspraak_aangekomen_{commuter.Id}"));
        });
    }

    private void FollowTrains()
    {
        try
        {
            var now = Scheduler.Now;
            foreach (var commuter in _commuters) FollowAppointment(commuter);
            foreach (var commuter in _commuters)
                foreach (var link in StationLinks)
                {
                    var key = $"{commuter.Id}|{link.Station}";
                    if (InZone(link.Station, commuter.PersonId))
                    {
                        // At the station: remember the trips (the one they take has gone from the sensor by the time they are on the track)
                        _atStation.Add(key);
                        _departures.Remove(key);
                        _tripsAtStation[key] = AllTrips(commuter, link.Direction);
                        SetRouteNotNeeded(commuter, link.Direction, false);
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
    /// With an appointment (<c>input_text.afspraak_{person}</c>) the other routes of the person are on Home until they arrive at the station
    /// in Nijmegen: <c>input_boolean.afspraak_aangekomen_{person}</c> goes on then, and off again for the next appointment.
    /// </summary>
    private void FollowAppointment(Commuter commuter)
    {
        var appointment = HaContext.GetState($"input_text.afspraak_{commuter.Id}")?.State ?? "";
        var arrivedId = $"input_boolean.afspraak_aangekomen_{commuter.Id}";
        var arrived = HaContext.GetState(arrivedId)?.State == "on";
        if (_appointments.GetValueOrDefault(commuter.Id) != appointment)
        {
            _appointments[commuter.Id] = appointment;
            if (arrived) HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity(arrivedId));
            return;
        }
        if (!arrived && AppointmentIsNear(appointment) && InZone(AppointmentStation, commuter.PersonId))
            HaContext.CallService("input_boolean", "turn_on", ServiceTarget.FromEntity(arrivedId));
    }

    /// <summary>Whether the appointment ("start|end|title") begins within six hours (the dashboard shows the other routes from then) and is not over.</summary>
    private bool AppointmentIsNear(string appointment)
    {
        var parts = appointment.Split('|');
        if (parts.Length < 2
            || !DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) return false;
        var now = Scheduler.NowNl();
        return start - now <= TimeSpan.FromHours(6) && now < end;
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
