using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Traffic;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Sends Vincent a WhatsApp message with the traffic on his commute: at 06:50 on an office day the trip to the office, at
/// 17:15 the trip home. The message has the travel time, the jams on the route and how long they last, and the speed cameras
/// (see <see cref="CommuteMessage"/>). It reads the route sensors and the jams and speed cameras of the ANWB File integration.
/// </summary>
[NetDaemonApp(Id = nameof(CommuteReport))]
public class CommuteReport : BaseApp
{
    /// <summary>When the message about the trip to the office is sent.</summary>
    public const string MorningCron = "50 6 * * *";

    /// <summary>When the message about the trip home is sent.</summary>
    public const string EveningCron = "15 17 * * *";

    private static readonly string[] GeoSource = ["anwb_file"];

    public CommuteReport(
        IHaContext ha,
        ILogger<CommuteReport> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        Scheduler.ScheduleCronNl(MorningCron, () => Send(CommuteDirection.ToWork));
        Scheduler.ScheduleCronNl(EveningCron, () => Send(CommuteDirection.ToHome));
    }

    private void Send(CommuteDirection direction)
    {
        try
        {
            if (!VincentSchedule.IsOfficeDay(Scheduler.NowNl().DayOfWeek)) return;
            // Already home on his way back: nothing to warn about
            if (direction == CommuteDirection.ToHome && Vincent.IsHome) return;
            if (string.IsNullOrEmpty(Vincent.Whatsapp))
            {
                Logger.LogWarning("No WhatsApp id for Vincent, the commute message is not sent");
                return;
            }

            var text = ReadRoute(direction) is { } route
                ? CommuteMessage.Build(direction, route, Incidents("jams"), Incidents("radars"), Scheduler.Now)
                : "⚠️ Ik kan de verkeersinformatie nu niet ophalen (de ANWB-route is niet beschikbaar).";
            Services.Whatsapp.SendMessage(clientId: "default", to: Vincent.Whatsapp, body: new { text });
            Logger.LogInformation("Sent the {Direction} commute message", direction);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not send the {Direction} commute message", direction);
        }
    }

    /// <summary>The fastest route, or null while its sensors have no value.</summary>
    private RouteSnapshot? ReadRoute(CommuteDirection direction)
    {
        var prefix = CommuteMessage.RoutePrefix(direction);
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
