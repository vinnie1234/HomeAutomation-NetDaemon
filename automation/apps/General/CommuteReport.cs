using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Persons;
using Automation.Models.Traffic;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Sends everyone who has a route in the ANWB File integration a WhatsApp message with the traffic on their commute, on their
/// office days: the trip to the office in the morning, the trip home in the afternoon. The message has the travel time, the jams
/// on the route and how long they last, and the speed cameras (see <see cref="CommuteMessage"/>).
/// <para>
/// A route belongs to a person (the <c>person</c> attribute of its sensors) and goes the way its name says (see
/// <see cref="CommuteMessage.DirectionOfName"/>), so a new route for somebody works without changing anything here. Each person
/// has helpers to switch the message on or off and to set the two times: <c>input_boolean.verkeer_bericht_{person}</c>,
/// <c>input_datetime.verkeer_heen_{person}</c> and <c>input_datetime.verkeer_terug_{person}</c>.
/// </para>
/// </summary>
[NetDaemonApp(Id = nameof(CommuteReport))]
public class CommuteReport : BaseApp
{
    /// <summary>When the message about the trip to the office is sent while the helper has no time.</summary>
    public static readonly TimeSpan DefaultMorning = new(7, 50, 0);

    /// <summary>When the message about the trip home is sent while the helper has no time.</summary>
    public static readonly TimeSpan DefaultEvening = new(17, 15, 0);

    /// <summary>Pressing this button sends Vincent both messages right away, to see what they look like.</summary>
    public const string TestButton = "input_button.verkeertestbericht";

    private static readonly string[] GeoSource = ["anwb_file"];

    /// <summary>One person who can get the message.</summary>
    /// <param name="Id">The name as used in the helper ids: <c>vincent</c> or <c>carleen</c>.</param>
    /// <param name="PersonId">The person entity, which the routes name as their owner.</param>
    /// <param name="Person">Whether the person is home.</param>
    /// <param name="Schedule">Their work schedule.</param>
    /// <param name="Whatsapp">Their WhatsApp id.</param>
    private sealed record Commuter(string Id, string PersonId, IPerson Person, WorkSchedule Schedule, string Whatsapp);

    public CommuteReport(
        IHaContext ha,
        ILogger<CommuteReport> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        Commuter[] commuters =
        [
            new("vincent", Entities.Person.VincentMaarschalkerweerd.EntityId, Vincent, VincentSchedule, Vincent.Whatsapp),
            new("carleen", Entities.Person.Carleen.EntityId, Carleen, CarleenSchedule, Carleen.Whatsapp)
        ];

        foreach (var commuter in commuters)
        {
            // The time is a helper, so a change in the dashboard applies without a restart
            Scheduler.RunDailyAt(new InputDatetimeEntity(HaContext, $"input_datetime.verkeer_heen_{commuter.Id}"), DefaultMorning,
                () => Send(commuter, CommuteDirection.ToWork));
            Scheduler.RunDailyAt(new InputDatetimeEntity(HaContext, $"input_datetime.verkeer_terug_{commuter.Id}"), DefaultEvening,
                () => Send(commuter, CommuteDirection.ToHome));
        }

        // A test: the button sends Vincent both messages now, on any day
        // (an input_button's state is the time of the last press; creating it at startup gives "unknown", which is not a press)
        new Entity(HaContext, TestButton).StateChanges().Where(x => DateTimeOffset.TryParse(x.New?.State, out _)).Subscribe(_ =>
        {
            Send(commuters[0], CommuteDirection.ToWork, test: true);
            Send(commuters[0], CommuteDirection.ToHome, test: true);
        });
    }

    private void Send(Commuter commuter, CommuteDirection direction, bool test = false)
    {
        try
        {
            if (!test)
            {
                // Switched off by the person, or not an office day
                if (HaContext.GetState($"input_boolean.verkeer_bericht_{commuter.Id}")?.State == "off") return;
                if (!commuter.Schedule.IsOfficeDay(Scheduler.NowNl().DayOfWeek)) return;
                // Already home on the way back: nothing to warn about
                if (direction == CommuteDirection.ToHome && commuter.Person.IsHome) return;
            }

            var carPrefix = FindRoutePrefix(commuter.PersonId, direction);
            var transitPrefix = FindTransitPrefix(commuter.PersonId, direction);
            // The way of the day (car or public transport, set per weekday) decides which route it is; the other one only when that route does not exist
            if (commuter.Schedule.Transport(Scheduler.NowNl().DayOfWeek) == WorkSchedule.PublicTransport)
            {
                if (transitPrefix != null) carPrefix = null;
            }
            else if (carPrefix != null) transitPrefix = null;
            var target = TargetTime(commuter, direction);
            if (carPrefix == null && transitPrefix == null)
            {
                // Somebody without a route in this direction gets nothing, just like somebody without a WhatsApp id
                Logger.LogInformation("{Person} has no {Direction} route, no commute message", commuter.Id, direction);
                return;
            }
            if (string.IsNullOrEmpty(commuter.Whatsapp))
            {
                Logger.LogWarning("No WhatsApp id for {Person}, the commute message is not sent", commuter.Id);
                return;
            }

            var parts = new List<string>();
            if (carPrefix != null)
                parts.Add(ReadRoute(carPrefix) is { } route
                    ? CommuteMessage.Build(direction, route, Incidents("jams"), Incidents("radars"), Scheduler.Now, target)
                    : "⚠️ Ik kan de verkeersinformatie nu niet ophalen (de ANWB-route is niet beschikbaar).");
            if (transitPrefix != null)
                parts.Add(ReadTransit(transitPrefix) is { } transit
                    ? CommuteMessage.BuildTransit(direction, transit, target)
                    : "⚠️ Ik kan de OV-reis nu niet ophalen (9292 is niet beschikbaar).");
            var text = (test ? "🧪 Test\n" : "") + string.Join("\n\n", parts);
            Services.Whatsapp.SendMessage(clientId: "default", to: commuter.Whatsapp, body: new { text });
            Logger.LogInformation("Sent the {Direction} commute message to {Person}", direction, commuter.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not send the {Direction} commute message to {Person}", direction, commuter.Id);
        }
    }

    /// <summary>
    /// The entity id prefix of the route of this person in this direction. The ANWB File integration gives every route an owner
    /// (the <c>person</c> attribute of its sensors); which way it goes follows from its name (<see cref="CommuteMessage.DirectionOfName"/>),
    /// or else from whether it starts or ends at home.
    /// </summary>
    private string? FindRoutePrefix(string personId, CommuteDirection direction)
    {
        var homeAttributes = HaContext.GetState("zone.home")?.AttributesJson;
        var homePoint = homeAttributes is { ValueKind: JsonValueKind.Object } home
            ? new GeoPoint(Number(home, "latitude") ?? 0, Number(home, "longitude") ?? 0)
            : default;

        foreach (var entity in HaContext.GetAllEntities().Where(e => e.EntityId.StartsWith("sensor.anwb_route_", StringComparison.Ordinal)
                                                                   && e.EntityId.EndsWith("_route", StringComparison.Ordinal)))
        {
            if (entity.EntityState?.AttributesJson is not { ValueKind: JsonValueKind.Object } a || Text(a, "person") != personId) continue;
            var name = Text(a, "friendly_name") ?? entity.EntityId;
            var routeDirection = CommuteMessage.DirectionOfName(name)
                                 ?? (Near(Point(a, "origin"), homePoint) ? CommuteDirection.ToWork
                                     : Near(Point(a, "destination"), homePoint) ? CommuteDirection.ToHome : null);
            if (routeDirection == direction) return entity.EntityId[..^"_route".Length];
        }
        return null;
    }

    /// <summary>
    /// The entity id prefix of the public transport route (9292 OV) of this person in this direction: the sensors named
    /// <c>sensor.9292_..._lijnen</c> with this person as owner, going the way their name says.
    /// </summary>
    private string? FindTransitPrefix(string personId, CommuteDirection direction)
    {
        foreach (var entity in HaContext.GetAllEntities().Where(e => e.EntityId.StartsWith("sensor.9292_", StringComparison.Ordinal)
                                                                   && e.EntityId.EndsWith("_lijnen", StringComparison.Ordinal)))
        {
            if (entity.EntityState?.AttributesJson is not { ValueKind: JsonValueKind.Object } a || Text(a, "person") != personId) continue;
            if (CommuteMessage.DirectionOfName(Text(a, "friendly_name") ?? entity.EntityId) == direction)
                return entity.EntityId[..^"_lijnen".Length];
        }
        return null;
    }

    /// <summary>The coming trips of a public transport route, or null while its sensor has no value.</summary>
    private TransitRoute? ReadTransit(string prefix)
    {
        var lines = HaContext.GetState($"{prefix}_lijnen");
        if (lines?.State is null or "unavailable" or "unknown" || lines.AttributesJson is not { ValueKind: JsonValueKind.Object } a) return null;

        var trips = new List<TransitTrip>();
        if (a.TryGetProperty("upcoming", out var upcoming) && upcoming.ValueKind == JsonValueKind.Array)
            foreach (var trip in upcoming.EnumerateArray())
            {
                if (!DateTimeOffset.TryParse(Text(trip, "departure_actual"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var departure)
                    || !DateTimeOffset.TryParse(Text(trip, "arrival"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrival)) continue;
                var legs = trip.TryGetProperty("legs", out var l) && l.ValueKind == JsonValueKind.Array
                    ? l.EnumerateArray().Where(x => Text(x, "line") is { Length: > 0 }).Select(x => new TransitLeg(Text(x, "mode") ?? "", Text(x, "line")!)).ToList()
                    : [];
                trips.Add(new TransitTrip(departure, arrival, Number(trip, "duration_min") ?? 0, Number(trip, "delay_min") ?? 0,
                    (int)(Number(trip, "transfers") ?? 0), legs, trip.TryGetProperty("cancelled", out var c) && c.ValueKind == JsonValueKind.True));
            }
        // Trips that have left already are no use; the integration refreshes, but not at the second
        trips = trips.Where(t => t.Departure >= Scheduler.Now.AddMinutes(-1)).ToList();
        return new TransitRoute(Text(a, "from"), Text(a, "to"), trips);
    }

    /// <summary>
    /// When the person has to be at the office or leaves it today, from <see cref="OfficeTimes"/>; null when that is not today.
    /// </summary>
    private DateTimeOffset? TargetTime(Commuter commuter, CommuteDirection direction)
    {
        var id = $"input_datetime.{(direction == CommuteDirection.ToWork ? "kantoor_aankomst" : "kantoor_vertrek")}_{commuter.Id}";
        if (!DateTime.TryParse(HaContext.GetState(id)?.State, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            || local.Date != Scheduler.NowNl().Date) return null;
        return new DateTimeOffset(local, Extensions.SchedulerExtensions.DutchTimeZone.GetUtcOffset(local));
    }

    private static bool Near(GeoPoint a, GeoPoint b) => Math.Abs(a.Lat - b.Lat) < 0.01 && Math.Abs(a.Lon - b.Lon) < 0.01;

    /// <summary>The fastest route, or null while its sensors have no value.</summary>
    private RouteSnapshot? ReadRoute(string prefix)
    {
        var duration = HaContext.GetState($"{prefix}_reistijd");
        var route = HaContext.GetState($"{prefix}_route");
        if (!double.TryParse(duration?.State, NumberStyles.Float, CultureInfo.InvariantCulture, out var durationMin)
            || route?.AttributesJson is not { ValueKind: JsonValueKind.Object } attributes)
            return null;

        var normal = Number(duration.AttributesJson, "normal_duration_min") ?? durationMin;
        var delay = double.TryParse(HaContext.GetState($"{prefix}_vertraging")?.State, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d : Math.Max(0, durationMin - normal);
        DateTimeOffset? arrival = DateTimeOffset.TryParse(HaContext.GetState($"{prefix}_aankomsttijd")?.State, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var a) ? a : null;

        return new RouteSnapshot(durationMin, normal, delay, arrival, Strings(attributes, "via"), Point(attributes, "origin"),
            Point(attributes, "destination"), Alternatives(attributes));
    }

    /// <summary>The jams or speed cameras of the ANWB File integration.</summary>
    private List<TrafficIncident> Incidents(string category)
    {
        var incidents = new List<TrafficIncident>();
        foreach (var entity in HaContext.GetAllEntities().Where(e => e.EntityId.StartsWith("geo_location.", StringComparison.Ordinal)))
        {
            if (entity.EntityState?.AttributesJson is not { ValueKind: JsonValueKind.Object } a
                || !GeoSource.Contains(Text(a, "source")) || Text(a, "category") != category
                || Text(a, "road") is not { Length: > 0 } road) continue;

            var path = a.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.Array
                ? p.EnumerateArray().Where(x => x.GetArrayLength() >= 2).Select(x => new GeoPoint(x[0].GetDouble(), x[1].GetDouble())).ToList()
                : [];
            incidents.Add(new TrafficIncident(road, Text(a, "direction"), Text(a, "from"), Text(a, "to"), Text(a, "reason"),
                Number(a, "length_km"), Number(a, "delay_min"),
                DateTimeOffset.TryParse(Text(a, "start"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ? start : null,
                new GeoPoint(Number(a, "latitude") ?? 0, Number(a, "longitude") ?? 0), path));
        }
        return incidents;
    }

    private static List<RouteAlternative> Alternatives(JsonElement attributes) =>
        attributes.TryGetProperty("alternatives", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(x => new RouteAlternative(Strings(x, "via"), Number(x, "duration_min") ?? 0, Number(x, "difference_min") ?? 0)).ToList()
            : [];

    private static List<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : [];

    private static GeoPoint Point(JsonElement element, string name) =>
        element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array && p.GetArrayLength() >= 2
            ? new GeoPoint(p[0].GetDouble(), p[1].GetDouble())
            : default;

    private static string? Text(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Number(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}
