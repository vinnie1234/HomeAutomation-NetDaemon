using System.Globalization;
using Automation.Core;
using Automation.Models.Traffic;

namespace Automation.apps.General;

/// <summary>
/// Whoever leaves the place of work keeps the route home as it was at that moment: the ANWB recalculates its route sensors all the time
/// (the arrival time is always "now plus the travel time"), so on the way the dashboard would show a travel time and an arrival that
/// keep changing. Until the person leaves the zone of the work the sensors are live; at that moment their travel time, delay and the
/// arrival (the moment of leaving plus the travel time) are written to <c>input_text.reis_vast_{person}</c>, which the dashboard shows
/// instead of the live values for the rest of the day. Coming back to the work clears it.
/// </summary>
public partial class CommuteReport
{
    /// <summary>The zone of the place of work.</summary>
    public const string WorkZone = "zone.werk";

    private readonly HashSet<string> _atWork = [];

    /// <summary>The helper that holds the route as it was when the person left work.</summary>
    public static string FrozenHelper(string person) => $"input_text.reis_vast_{person}";

    private void SetUpDepartureTracking()
    {
        // Who is at work already (also after a restart) has not just arrived there
        foreach (var commuter in _commuters)
            if (InZone(WorkZone, commuter.PersonId)) _atWork.Add(commuter.Id);
        new Entity(HaContext, WorkZone).StateChanges().Subscribe(_ => FollowWorkZone());
    }

    private void FollowWorkZone()
    {
        try
        {
            foreach (var commuter in _commuters)
            {
                if (InZone(WorkZone, commuter.PersonId))
                {
                    // Back at work: the route home is live again
                    if (_atWork.Add(commuter.Id) && !string.IsNullOrEmpty(FrozenText(commuter))) SetFrozen(commuter, "");
                }
                else if (_atWork.Remove(commuter.Id))
                {
                    FreezeRouteHome(commuter);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not follow the zone of the work");
        }
    }

    private void FreezeRouteHome(Commuter commuter)
    {
        if (FindRoutePrefix(commuter.PersonId, CommuteDirection.ToHome) is not { } prefix || ReadRoute(prefix) is not { } route)
        {
            Logger.LogInformation("{Person} left work, but there is no route home to keep", commuter.Id);
            return;
        }

        var left = Scheduler.Now;
        var frozen = new FrozenRoute(Scheduler.NowNl().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), left,
            Math.Round(route.DurationMin, 1), Math.Round(route.NormalMin, 1), Math.Round(route.DelayMin, 1), left.AddMinutes(route.DurationMin));
        ActivityContext.Note($"{commuter.Id} verliet de zone van het werk: de route naar huis blijft zoals hij toen was");
        SetFrozen(commuter, JsonSerializer.Serialize(frozen));
        Logger.LogInformation("{Person} left work: the route home stays at {Duration} min, arrival {Arrival}", commuter.Id, frozen.Duration, frozen.Arrival);
    }

    /// <summary>The route home as it was when the person left work, if that was today.</summary>
    private FrozenRoute? FrozenToday(Commuter commuter)
    {
        var text = FrozenText(commuter);
        if (string.IsNullOrEmpty(text) || text is "unknown" or "unavailable") return null;
        try
        {
            var frozen = JsonSerializer.Deserialize<FrozenRoute>(text);
            return frozen?.Date == Scheduler.NowNl().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ? frozen : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string? FrozenText(Commuter commuter) => HaContext.GetState(FrozenHelper(commuter.Id))?.State;

    private void SetFrozen(Commuter commuter, string value) =>
        HaContext.CallService("input_text", "set_value", ServiceTarget.FromEntity(FrozenHelper(commuter.Id)), new { value });

    /// <summary>The route home at the moment of leaving. The names are short because an input_text holds 255 characters.</summary>
    private sealed record FrozenRoute(
        [property: System.Text.Json.Serialization.JsonPropertyName("date")] string Date,
        [property: System.Text.Json.Serialization.JsonPropertyName("left")] DateTimeOffset Left,
        [property: System.Text.Json.Serialization.JsonPropertyName("duration")] double Duration,
        [property: System.Text.Json.Serialization.JsonPropertyName("normal")] double Normal,
        [property: System.Text.Json.Serialization.JsonPropertyName("delay")] double Delay,
        [property: System.Text.Json.Serialization.JsonPropertyName("arrival")] DateTimeOffset Arrival);
}
