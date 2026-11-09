using System.Globalization;
using System.Text.RegularExpressions;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Persons;
using Automation.Models.WhatsApp;
using Automation.Models.Traffic;
using Microsoft.Extensions.Options;
using NetDaemon.Client;

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
public partial class CommuteReport : BaseApp
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

    private readonly Commuter[] _commuters;
    private readonly IDataRepository _storage;
    private readonly IHomeAssistantRunner _runner;
    private readonly AppConfig _config;

    /// <summary>The messages already sent (or tried) for a person, direction and date, so a message comes once per trip.</summary>
    private readonly HashSet<string> _sent = [];

    /// <summary>The work laptop is switched off at the office from this long before the time to leave: that is the moment of the message about the way home.</summary>
    private static readonly TimeSpan LaptopEarliest = TimeSpan.FromHours(2);

    /// <summary>A message about the way home that was sent because of the laptop makes the one of the fixed time within this time unnecessary.</summary>
    private static readonly TimeSpan LaptopOverlap = TimeSpan.FromMinutes(60);

    private DateTimeOffset? _laptopSentAt;

    /// <summary>How long before leaving the message comes.</summary>
    private static readonly TimeSpan SendBefore = TimeSpan.FromMinutes(10);

    /// <summary>How long after its moment a message can still come (a restart or a late update), after that it is too late to be of use.</summary>
    private static readonly TimeSpan TooLate = TimeSpan.FromMinutes(30);

    public CommuteReport(
        IHaContext ha,
        ILogger<CommuteReport> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IDataRepository storage,
        IHomeAssistantRunner runner)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
        _runner = runner;
        _config = config.Value;
        _commuters =
        [
            new("vincent", Entities.Person.VincentMaarschalkerweerd.EntityId, Vincent, VincentSchedule, Vincent.Whatsapp),
            new("carleen", Entities.Person.Carleen.EntityId, Carleen, CarleenSchedule, Carleen.Whatsapp)
        ];

        foreach (var commuter in _commuters)
        {
            // The fixed times are for the days without a time to be somewhere or to leave; otherwise the message comes ten minutes
            // before leaving (see SendWhenItIsTime). The time is a helper, so a change in the dashboard applies without a restart.
            Scheduler.RunDailyAt(new InputDatetimeEntity(HaContext, $"input_datetime.verkeer_heen_{commuter.Id}"), DefaultMorning,
                () => { if (TargetTime(commuter, CommuteDirection.ToWork) == null) _ = Send(commuter, CommuteDirection.ToWork); });
            Scheduler.RunDailyAt(new InputDatetimeEntity(HaContext, $"input_datetime.verkeer_terug_{commuter.Id}"), DefaultEvening,
                () => { if (TargetTime(commuter, CommuteDirection.ToHome) == null && !SentByLaptopRecently(commuter, CommuteDirection.ToHome)) _ = Send(commuter, CommuteDirection.ToHome); });
        }
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), SendWhenItIsTime);
        SetUpTrainTracking();

        // The work laptop goes off while Vincent is at the office: the message about the way home comes now, besides the fixed time
        new Entity(HaContext, WorkLaptop.Uptime).StateChanges()
            .Where(x => WorkLaptop.IsAvailable(x.Old?.State) && !WorkLaptop.IsAvailable(x.New?.State))
            .Subscribe(_ => LaptopSwitchedOff());

        // A test: the button sends Vincent both messages now, on any day
        // (an input_button's state is the time of the last press; creating it at startup gives "unknown", which is not a press)
        new Entity(HaContext, TestButton).StateChanges().Where(x => DateTimeOffset.TryParse(x.New?.State, out _)).Subscribe(press =>
        {
            _ = Send(_commuters[0], CommuteDirection.ToWork, test: true);
            _ = Send(_commuters[0], CommuteDirection.ToHome, test: true);
        });

        // "Stuur door" on the dashboard: sends the other person what is expected for this person's trip (the trip to the office while at home,
        // otherwise the one back home)
        foreach (var commuter in _commuters)
        {
            var sender = commuter;
            new Entity(HaContext, $"input_button.reis_doorsturen_{sender.Id}").StateChanges().Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
                .Subscribe(press => _ = Send(sender, sender.Person.IsHome ? CommuteDirection.ToWork : CommuteDirection.ToHome, forward: true,
                    otherRoute: HaContext.GetState($"input_text.reis_doorsturen_route_{sender.Id}")?.State));

            // "Stuur naar ..." for the person themself: the same message, but to their own WhatsApp
            new Entity(HaContext, $"input_button.reis_doorsturen_zelf_{sender.Id}").StateChanges().Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
                .Subscribe(press => _ = Send(sender, sender.Person.IsHome ? CommuteDirection.ToWork : CommuteDirection.ToHome, toSelf: true,
                    otherRoute: HaContext.GetState($"input_text.reis_doorsturen_route_{sender.Id}")?.State));
        }
    }

    /// <summary>
    /// Every minute: with a time to leave the message comes ten minutes before it; with a time to be somewhere ten minutes before the
    /// departure that gets one there on time (the route of now: the travel time of the car, or the departure of the trip public transport finds).
    /// </summary>
    private void SendWhenItIsTime()
    {
        try
        {
            var now = Scheduler.Now;
            var today = Scheduler.NowNl().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _sent.RemoveWhere(key => !key.EndsWith(today, StringComparison.Ordinal));
            foreach (var commuter in _commuters)
                foreach (var direction in new[] { CommuteDirection.ToWork, CommuteDirection.ToHome })
                {
                    if (TargetTime(commuter, direction) is not { } target) continue;
                    var key = $"{commuter.Id}|{direction}|{today}";
                    if (_sent.Contains(key) || HaContext.GetState($"input_boolean.reis_niet_{commuter.Id}_{Side(direction)}")?.State == "on") continue;

                    var sendAt = LeaveBy(commuter, direction, target) - SendBefore;
                    if (now < sendAt || now >= sendAt + TooLate) continue;
                    _sent.Add(key);
                    if (SentByLaptopRecently(commuter, direction)) continue;
                    _ = Send(commuter, direction);
                }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not check whether a commute message is due");
        }
    }

    private bool SentByLaptopRecently(Commuter commuter, CommuteDirection direction) =>
        commuter.Id == "vincent" && direction == CommuteDirection.ToHome && _laptopSentAt is { } sent && Scheduler.Now - sent < LaptopOverlap;

    /// <summary>
    /// The work laptop was switched off. At the office on an office day, from two hours before the time to leave, that means he is about to
    /// leave: the message about the way home is sent now. (A laptop that goes off in the morning or at lunch changes nothing.)
    /// </summary>
    private void LaptopSwitchedOff()
    {
        try
        {
            var vincent = _commuters.First(c => c.Id == "vincent");
            var now = Scheduler.Now;
            if (!vincent.Schedule.IsOfficeDay(Scheduler.NowNl().DayOfWeek) || !IsAtOffice(vincent)) return;

            var leaveAt = TargetTime(vincent, CommuteDirection.ToHome) is { } target ? LeaveBy(vincent, CommuteDirection.ToHome, target) : FixedEveningToday(vincent);
            if (now < leaveAt - LaptopEarliest) return;

            Logger.LogInformation("The work laptop went off at the office: the message about the way home");
            _laptopSentAt = now;
            _ = Send(vincent, CommuteDirection.ToHome);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not send the message about the way home after the laptop went off");
        }
    }

    /// <summary>Whether the person is in the zone of the place of work.</summary>
    private bool IsAtOffice(Commuter commuter) =>
        HaContext.GetState("zone.werk")?.AttributesJson is { ValueKind: JsonValueKind.Object } a
        && a.TryGetProperty("persons", out var persons) && persons.ValueKind == JsonValueKind.Array
        && persons.EnumerateArray().Any(x => x.GetString() == commuter.PersonId);

    /// <summary>The fixed time of the message about the way home, today (the helper of the person, otherwise the default).</summary>
    private DateTimeOffset FixedEveningToday(Commuter commuter)
    {
        var time = Extensions.SchedulerExtensions.ParseTimeOfDay(HaContext.GetState($"input_datetime.verkeer_terug_{commuter.Id}")?.State) ?? DefaultEvening;
        var local = Scheduler.NowNl().Date + time;
        return new DateTimeOffset(local, Extensions.SchedulerExtensions.DutchTimeZone.GetUtcOffset(local));
    }

    /// <summary>When to leave: the time to leave itself, or the time to be somewhere minus the travel time of now (45 minutes when it is not known).</summary>
    private DateTimeOffset LeaveBy(Commuter commuter, CommuteDirection direction, CommuteTarget target)
    {
        if (!target.IsArrival) return target.Time;
        var (car, transit) = ChooseRoutes(commuter, direction);
        if (transit != null && ReadTransit(transit, commuter, direction) is { Trips.Count: > 0 } trips) return trips.Trips[0].Departure;
        if (car != null && ReadRoute(car) is { } route) return target.Time - TimeSpan.FromMinutes(route.DurationMin);
        return target.Time - TimeSpan.FromMinutes(45);
    }

    /// <summary>
    /// The route of the day in this direction: the car route or the public transport route, the way of the day (set per weekday)
    /// decides; the other one only when that route does not exist.
    /// </summary>
    private (string? Car, string? Transit) ChooseRoutes(Commuter commuter, CommuteDirection direction)
    {
        var car = FindRoutePrefix(commuter.PersonId, direction);
        var transit = FindTransitPrefix(commuter.PersonId, direction);
        if (commuter.Schedule.TransportToday(Scheduler.NowNl().DayOfWeek) == WorkSchedule.PublicTransport)
        {
            if (transit != null) car = null;
        }
        else if (car != null) transit = null;
        return (car, transit);
    }

    /// <param name="commuter">Whose trip it is.</param>
    /// <param name="direction">To the office or home.</param>
    /// <param name="test">A test message, whatever day it is.</param>
    /// <param name="forward">The message goes to the other person, as the expected times of this person (the "Stuur door" button).</param>
    /// <param name="toSelf">The message goes to this person themself (the "Stuur naar ..." button), worded like the forwarded one.</param>
    /// <param name="otherRoute">The sensor of a route that is neither to the office nor home, which is forwarded instead of the trip of the day.</param>
    private async Task Send(Commuter commuter, CommuteDirection direction, bool test = false, bool forward = false, string? otherRoute = null, bool toSelf = false)
    {
        try
        {
            if (!test && !forward && !toSelf)
            {
                // Switched off by the person, or not an office day
                if (HaContext.GetState($"input_boolean.verkeer_bericht_{commuter.Id}")?.State == "off") return;
                // The route is not needed today (they did not get into the train)
                if (HaContext.GetState($"input_boolean.reis_niet_{commuter.Id}_{Side(direction)}")?.State == "on") return;
                if (!commuter.Schedule.IsOfficeDay(Scheduler.NowNl().DayOfWeek)) return;
                // Already home on the way back, or already gone on the way there: nothing to warn about
                if (direction == CommuteDirection.ToHome && commuter.Person.IsHome) return;
                if (direction == CommuteDirection.ToWork && !commuter.Person.IsHome) return;
            }

            string? title = null;
            var (carPrefix, transitPrefix) = otherRoute is { Length: > 0 } ? OtherRoute(commuter, otherRoute, out title) : ChooseRoutes(commuter, direction);
            var target = title != null ? null : TargetTime(commuter, direction);
            var side = title != null ? "other" : Side(direction);
            if (carPrefix == null && transitPrefix == null)
            {
                // Somebody without a route in this direction gets nothing, just like somebody without a WhatsApp id
                Logger.LogInformation("{Person} has no {Direction} route, no commute message", commuter.Id, direction);
                return;
            }
            var recipient = forward ? _commuters.First(c => c.Id != commuter.Id).Whatsapp : commuter.Whatsapp;
            if (string.IsNullOrEmpty(recipient))
            {
                Logger.LogWarning("No WhatsApp id for {Person}, the commute message is not sent", forward ? "the other person" : commuter.Id);
                return;
            }

            var parts = new List<string>();
            if (carPrefix != null)
                parts.Add(ReadRoute(carPrefix) is { } route
                    ? CommuteMessage.Build(direction, route, Incidents("jams"), Incidents("radars"), Scheduler.Now, target, title)
                    : "⚠️ Ik kan de verkeersinformatie nu niet ophalen (de ANWB-route is niet beschikbaar).");
            if (carPrefix != null) parts[^1] += DashboardLink(commuter, side, "car");
            var otherName = commuter.Id == "vincent" ? "Carleen" : "Vincent";
            if (transitPrefix != null)
            {
                var transit = ReadTransit(transitPrefix, commuter, direction);
                // With the id of the first trip the integration tells every step; without it (or when asking fails) the short version
                var journey = transit is { Trips.Count: > 0 } ? await GetJourneyAsync(transit) : null;
                parts.Add(transit == null ? "⚠️ Ik kan de OV-reis nu niet ophalen (9292 is niet beschikbaar)."
                    : journey != null ? CommuteMessage.BuildJourney(direction, transit, journey, target, forward || toSelf ? null : otherName, title)
                    : CommuteMessage.BuildTransit(direction, transit, target, title) + (forward || toSelf ? "" : $"\n↪️ Antwoord *Stuur door* om dit naar {otherName} te sturen"));
                parts[^1] += DashboardLink(commuter, side, "ov");
            }
            var name = commuter.Id == "vincent" ? "Vincent" : "Carleen";
            var text = (test ? "🧪 Test\n" : "") + (forward ? $"↪️ Doorgestuurd door {name}: dit zijn de tijden die verwacht worden voor {name}\n\n" : toSelf ? $"↪️ Dit zijn de tijden die verwacht worden voor {name}\n\n" : "") + string.Join("\n\n", parts);
            Services.Whatsapp.SendMessage(clientId: "default", to: recipient, body: new { text });
            // The message is also the last reply in the WhatsApp conversation, so "Stuur door" forwards it to the other person
            if (transitPrefix != null && !forward && !toSelf) RememberAsLastReply(commuter.Whatsapp, text);
            Logger.LogInformation("Sent the {Direction} commute message to {Person}", direction, commuter.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not send the {Direction} commute message to {Person}", direction, commuter.Id);
        }
    }

    /// <summary>
    /// A link that opens the dashboard with the details of this trip (the card reads <c>reis=person-direction-kind</c> and opens its window).
    /// Nothing when the address of Home Assistant is not known.
    /// </summary>
    private string DashboardLink(Commuter commuter, string side, string kind) =>
        string.IsNullOrEmpty(_config.BaseUrlHomeAssistant)
            ? ""
            : $"\n🔗 Alle details: {_config.BaseUrlHomeAssistant.TrimEnd('/')}/dashboard-huis/home?reis={commuter.Id}-{side}-{kind}";

    /// <summary>
    /// The route that was asked for by its sensor (a route that is neither to the office nor home), as the prefix of a car or a public
    /// transport route of this person, with its name as the title of the message. Nothing when it is not a route of this person.
    /// </summary>
    private (string? Car, string? Transit) OtherRoute(Commuter commuter, string entityId, out string? title)
    {
        title = null;
        var state = HaContext.GetState(entityId);
        if (state?.AttributesJson is not { ValueKind: JsonValueKind.Object } a || Text(a, "person") != commuter.PersonId) return (null, null);
        var name = Text(a, "friendly_name") ?? entityId;
        title = Regex.Replace(Regex.Replace(Regex.Replace(name, @"^(9292|ANWB Route)\s+", "", RegexOptions.IgnoreCase), @"\s*\([^)]*\)\s*", " "), @"\s+(Lijnen|Route)\s*$", "", RegexOptions.IgnoreCase).Trim();
        if (entityId.StartsWith("sensor.9292_", StringComparison.Ordinal) && entityId.EndsWith("_lijnen", StringComparison.Ordinal)) return (null, entityId[..^"_lijnen".Length]);
        if (entityId.StartsWith("sensor.anwb_route_", StringComparison.Ordinal) && entityId.EndsWith("_route", StringComparison.Ordinal)) return (entityId[..^"_route".Length], null);
        return (null, null);
    }

    /// <summary>
    /// The entity id prefix of the route of this person in this direction. The ANWB File integration gives every route an owner
    /// (the <c>person</c> attribute of its sensors); which way it goes follows from its name (<see cref="CommuteMessage.DirectionOfName"/>).
    /// </summary>
    private string? FindRoutePrefix(string personId, CommuteDirection direction)
    {
        foreach (var entity in HaContext.GetAllEntities().Where(e => e.EntityId.StartsWith("sensor.anwb_route_", StringComparison.Ordinal)
                                                                   && e.EntityId.EndsWith("_route", StringComparison.Ordinal)))
        {
            if (entity.EntityState?.AttributesJson is not { ValueKind: JsonValueKind.Object } a || Text(a, "person") != personId) continue;
            var name = Text(a, "friendly_name") ?? entity.EntityId;
            var routeDirection = CommuteMessage.DirectionOfName(name);
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

    /// <summary>The details of the first trip (every step with its stops and platforms), or null when the integration has no id for it or does not answer.</summary>
    private Task<JourneyDetail?> GetJourneyAsync(TransitRoute route) =>
        route.Trips[0].Id is { Length: > 0 } id ? GetJourneyAsync(id, route.RouteId) : Task.FromResult<JourneyDetail?>(null);

    /// <summary>The details of one journey (every step with its stops and platforms), or null when the integration does not answer.</summary>
    private async Task<JourneyDetail?> GetJourneyAsync(string id, string? routeId)
    {
        if (_runner.CurrentConnection is not { } connection) return null;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var detail = await connection.SendCommandAndReturnResponseAsync<GetJourneyCommand, JourneyDetail>(new GetJourneyCommand(id, routeId), timeout.Token);
            return detail is { Legs.Count: > 0 } ? detail : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not get the details of the 9292 journey");
            return null;
        }
    }

    /// <summary>Adds the message to the WhatsApp conversation as the last reply, which is what "Stuur door" forwards.</summary>
    private void RememberAsLastReply(string whatsapp, string text)
    {
        var all = _storage.Get<Dictionary<string, List<WhatsAppConversationTurn>>>(WhatsAppAssistant.HistoryStorageKey) ?? [];
        var history = all.GetValueOrDefault(whatsapp) ?? [];
        history.Add(new WhatsAppConversationTurn { UserMessage = "(reisbericht)", AssistantReply = text });
        if (history.Count > WhatsAppAssistant.MaxHistoryTurns) history.RemoveAt(0);
        all[whatsapp] = history;
        _storage.Save(WhatsAppAssistant.HistoryStorageKey, all);
    }

    /// <summary>
    /// The coming trips of a public transport route, or null while its sensor has no value. The trip the person chose in the dashboard
    /// (<c>input_text.reis_keuze_{person}_{work|home}</c>, the departure by the timetable of today) comes first, whether it is one of the
    /// earlier trips or a later one.
    /// </summary>
    private TransitRoute? ReadTransit(string prefix, Commuter commuter, CommuteDirection direction)
    {
        var lines = HaContext.GetState($"{prefix}_lijnen");
        if (lines?.State is null or "unavailable" or "unknown" || lines.AttributesJson is not { ValueKind: JsonValueKind.Object } a) return null;

        // Trips that have left already are no use; the integration refreshes, but not at the second
        List<TransitTrip> Read(string name) => TransitTrips(a, name).Where(t => t.Departure >= Scheduler.Now.AddMinutes(-1)).ToList();
        var trips = Read("upcoming");
        var chosen = ChosenDeparture(commuter, direction) is { } stamp
            ? Read("earlier").Concat(trips).FirstOrDefault(t => PlannedStamp(t) == stamp)
            : null;
        if (chosen != null) trips = trips.Where(t => t != chosen).Prepend(chosen).ToList();
        return new TransitRoute(Text(a, "from"), Text(a, "to"), trips, Text(a, "route_id"));
    }

    private static IEnumerable<TransitTrip> TransitTrips(JsonElement attributes, string name)
    {
        if (!attributes.TryGetProperty(name, out var list) || list.ValueKind != JsonValueKind.Array) yield break;
        foreach (var trip in list.EnumerateArray())
        {
            if (!DateTimeOffset.TryParse(Text(trip, "departure_actual"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var departure)
                || !DateTimeOffset.TryParse(Text(trip, "arrival"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrival)) continue;
            var legs = trip.TryGetProperty("legs", out var l) && l.ValueKind == JsonValueKind.Array
                ? l.EnumerateArray().Where(x => Text(x, "line") is { Length: > 0 }).Select(x => new TransitLeg(Text(x, "mode") ?? "", Text(x, "line")!)).ToList()
                : [];
            yield return new TransitTrip(departure, arrival, Number(trip, "duration_min") ?? 0, Number(trip, "delay_min") ?? 0,
                (int)(Number(trip, "transfers") ?? 0), legs, trip.TryGetProperty("cancelled", out var c) && c.ValueKind == JsonValueKind.True, Text(trip, "id"));
        }
    }

    /// <summary>The departure by the timetable ("yyyy-MM-dd HH:mm", Dutch time) of the trip chosen for today, or null.</summary>
    private string? ChosenDeparture(Commuter commuter, CommuteDirection direction)
    {
        var value = HaContext.GetState($"input_text.reis_keuze_{commuter.Id}_{(direction == CommuteDirection.ToWork ? "work" : "home")}")?.State;
        return value is { Length: > 0 } && value.StartsWith(Scheduler.NowNl().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal) ? value : null;
    }

    private static string PlannedStamp(TransitTrip trip) =>
        TimeZoneInfo.ConvertTime(trip.Departure - TimeSpan.FromMinutes(trip.DelayMin), Extensions.SchedulerExtensions.DutchTimeZone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// The time the person wants to be somewhere or leaves, for today (from <see cref="OfficeTimes"/>): from the calendar always the time to
    /// be at the office and the time to leave it; set by hand it can also be the time to leave home and the time to be home.
    /// Null when that is not for today.
    /// </summary>
    private CommuteTarget? TargetTime(Commuter commuter, CommuteDirection direction)
    {
        var fromCalendar = HaContext.GetState($"input_boolean.werk_uit_agenda_{commuter.Id}")?.State == "on";
        var kind = HaContext.GetState($"input_select.{(direction == CommuteDirection.ToWork ? "reis_ochtend_soort" : "reis_avond_soort")}_{commuter.Id}")?.State;
        var departure = !fromCalendar && direction == CommuteDirection.ToWork && kind == OfficeTravel.MorningDepart;
        var arrivalHome = !fromCalendar && direction == CommuteDirection.ToHome && kind == OfficeTravel.EveningArrive;
        var entity = direction == CommuteDirection.ToWork ? (departure ? "thuis_vertrek" : "kantoor_aankomst") : (arrivalHome ? "thuis_aankomst" : "kantoor_vertrek");
        if (!DateTime.TryParse(HaContext.GetState($"input_datetime.{entity}_{commuter.Id}")?.State, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            || local.Date != Scheduler.NowNl().Date) return null;
        var time = new DateTimeOffset(local, Extensions.SchedulerExtensions.DutchTimeZone.GetUtcOffset(local));
        // To work, a time at the office is an arrival and a time to leave home is a departure; back home it is the other way around
        return new CommuteTarget(time, IsArrival: direction == CommuteDirection.ToWork ? !departure : arrivalHome);
    }

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
