using Automation.Helpers;
using Automation.Models.Traffic;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Helpers;

public class CommuteMessageTests
{
    // Home (east) to the office (west) along the A12, like the real route
    private static readonly GeoPoint Home = new(52.028, 5.528);
    private static readonly GeoPoint Office = new(52.063, 5.088);
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 5, 50, 0, TimeSpan.Zero);

    private static RouteSnapshot Route(double duration = 31, double delay = 0, params RouteAlternative[] alternatives) =>
        new(duration, duration - delay, delay, Now.AddMinutes(duration), ["A12"], Home, Office, alternatives);

    private static TrafficIncident Jam(string road = "A12", bool towardsOffice = true, double lat = 52.05, double lon = 5.3, double delay = 8) =>
        new(road, "Arnhem - Utrecht", "Veenendaal", "Ede", "Stilstaand verkeer. Door een ongeluk.", 4.2, delay, Now.AddMinutes(-30),
            new GeoPoint(lat, lon), towardsOffice ? [new GeoPoint(lat, lon + 0.02), new GeoPoint(lat, lon - 0.02)] : [new GeoPoint(lat, lon - 0.02), new GeoPoint(lat, lon + 0.02)]);

    private static TrafficIncident Radar(string road = "A12", double lat = 52.05, double lon = 5.43) =>
        new(road, "Utrecht - Arnhem", "Maarsbergen", "Veenendaal-West", "Bij hectometerpaal 83.2.", null, null, null, new GeoPoint(lat, lon), []);

    [Fact]
    public void NoTraffic_SaysSo_AndHasTheTravelTime()
    {
        var text = CommuteMessage.Build(CommuteDirection.ToWork, Route(), [], [], Now);

        text.Should().Contain("Naar kantoor").And.Contain("Reistijd: 31 min (geen vertraging)").And.Contain("Via A12")
            .And.Contain("Geen files op je route").And.Contain("Geen flitsers gemeld");
    }

    [Fact]
    public void ToHome_HasItsOwnTitle()
    {
        CommuteMessage.Build(CommuteDirection.ToHome, Route(), [], [], Now).Should().Contain("Naar huis");
    }

    [Fact]
    public void Delay_ShowsNormalTimeAndTheExtraMinutes()
    {
        var text = CommuteMessage.Build(CommuteDirection.ToWork, Route(duration: 45, delay: 14), [], [], Now);

        text.Should().Contain("Reistijd: 45 min (normaal 31 min, +14 min door het verkeer)");
    }

    [Fact]
    public void Delay_WithoutAJamOnTheRoute_SaysItIsBusy()
    {
        CommuteMessage.Build(CommuteDirection.ToWork, Route(delay: 6), [], [], Now).Should().Contain("drukker dan normaal");
    }

    [Fact]
    public void JamOnTheRoute_IsListedWithLengthDelayAndHowLongItHasBeenThere()
    {
        var text = CommuteMessage.Build(CommuteDirection.ToWork, Route(delay: 8), [Jam()], [], Now);

        text.Should().Contain("Files op je route (1)").And.Contain("A12 Veenendaal - Ede: 4,2 km, +8 min, al 30 min").And.Contain("stilstaand verkeer");
    }

    [Fact]
    public void JamInTheOtherDirection_IsLeftOut()
    {
        var text = CommuteMessage.Build(CommuteDirection.ToWork, Route(), [Jam(towardsOffice: false)], [], Now);

        text.Should().Contain("Geen files op je route");
    }

    [Fact]
    public void JamOnAnotherRoadOrFarAway_IsLeftOut()
    {
        var jams = new[] { Jam(road: "A2"), Jam(lat: 51.7, lon: 5.3), Jam(lon: 6.5) };

        CommuteMessage.Build(CommuteDirection.ToWork, Route(), jams, [], Now).Should().Contain("Geen files op je route");
    }

    [Fact]
    public void BackHome_ASameDirectionJamIsTheOtherWayAround()
    {
        var route = new RouteSnapshot(30, 30, 0, null, ["A12"], Office, Home, []);

        CommuteMessage.JamsOnRoute(route, [Jam(towardsOffice: true)]).Should().BeEmpty();
        CommuteMessage.JamsOnRoute(route, [Jam(towardsOffice: false)]).Should().HaveCount(1);
    }

    [Fact]
    public void LongJams_AreSortedByDelayAndShowHours()
    {
        var jams = new[] { Jam(delay: 3), Jam(delay: 20) with { Start = Now.AddHours(-2), From = "Lunetten" } };

        var text = CommuteMessage.Build(CommuteDirection.ToWork, Route(delay: 23), jams, [], Now);

        text.IndexOf("Lunetten", StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("Veenendaal", StringComparison.Ordinal));
        text.Should().Contain("al 2 uur");
    }

    [Fact]
    public void SpeedCameraOnTheRoute_IsListedWithTheDirection()
    {
        var text = CommuteMessage.Build(CommuteDirection.ToWork, Route(), [], [Radar(), Radar(road: "A28"), Radar(lat: 50.9)], Now);

        text.Should().Contain("Flitsers op je route").And.Contain("A12 bij Maarsbergen, richting Arnhem");
        text.Split("Maarsbergen").Length.Should().Be(2, "only the camera on the route counts");
    }

    [Fact]
    public void BigDelay_SuggestsTheFastestAlternative()
    {
        var route = Route(duration: 60, delay: 25, new RouteAlternative(["N224", "A28"], 51, 20.4), new RouteAlternative(["A27"], 49, 12));

        CommuteMessage.Build(CommuteDirection.ToWork, route, [], [], Now).Should().Contain("Alternatief via A27: 49 min (+12 min)");
    }

    [Fact]
    public void SmallDelay_HasNoAlternative()
    {
        CommuteMessage.Build(CommuteDirection.ToWork, Route(delay: 2, alternatives: new RouteAlternative(["A27"], 50, 19)), [], [], Now)
            .Should().NotContain("Alternatief");
    }

    [Theory]
    [InlineData("ANWB Route Werk => Huis Route", CommuteDirection.ToHome)]
    [InlineData("ANWB Route Huis => Werk Route", CommuteDirection.ToWork)]
    [InlineData("Thuis -> Kantoor", CommuteDirection.ToWork)]
    [InlineData("Route van kantoor naar thuis (A12) Carleen", CommuteDirection.ToHome)]
    [InlineData("werk thuis", CommuteDirection.ToHome)]
    [InlineData("THUIS WERK", CommuteDirection.ToWork)]
    public void DirectionOfName_FollowsTheOrderOfWorkAndHome(string name, CommuteDirection expected)
    {
        CommuteMessage.DirectionOfName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("ANWB Route Boodschappen")]
    [InlineData("9292 Thuis -> Nijmegen (Carleen)")]
    [InlineData("9292 Nijmegen -> Thuis (Carleen)")]
    [InlineData("Route naar werk")]
    [InlineData("Werk naar Utrecht")]
    [InlineData("")]
    [InlineData(null)]
    public void DirectionOfName_IsNull_WhenTheNameDoesNotTell(string? name)
    {
        CommuteMessage.DirectionOfName(name).Should().BeNull();
    }

    [Fact]
    public void JamOnTheRoadsOfTheRoute_WithoutDelayOnTheRoute_SaysTheRouteGoesAround()
    {
        // 2 November: a 24 minute jam on the A12, while the fastest route (via the N226) had no delay at all
        var jam = Jam(delay: 24);

        var text = CommuteMessage.Build(CommuteDirection.ToHome, Route(delay: 0), [jam], [], Now);

        text.Should().Contain("maar de route rijdt eromheen").And.Contain("+24 min");
    }

    [Fact]
    public void JamOnTheRoute_WithAMatchingDelay_IsAWarning()
    {
        var text = CommuteMessage.Build(CommuteDirection.ToWork, Route(delay: 20), [Jam(delay: 24)], [], Now);

        text.Should().Contain("⚠️ Files op je route").And.NotContain("rijdt eromheen");
    }

    private static TransitTrip Trip(int departMinutes, int duration, double delay = 0, bool cancelled = false, params (string Mode, string Line)[] legs) =>
        new(Now.AddMinutes(departMinutes), Now.AddMinutes(departMinutes + duration), duration, delay, 2,
            legs.Select(l => new TransitLeg(l.Mode, l.Line)).ToList(), cancelled);

    [Fact]
    public void Transit_ShowsTheNextTripWithLinesAndTheOnesAfterIt()
    {
        var route = new TransitRoute("Veenendaal", "Nijmegen", [
            Trip(63, 69, 0, false, ("Bus", "505"), ("FastTrain", "3255"), ("FastTrain", "3157")),
            Trip(65, 84, 2), Trip(74, 83, 0, true), Trip(86, 76)]);

        var text = CommuteMessage.BuildTransit(CommuteDirection.ToWork, route);

        text.Should().Contain("Naar kantoor met het OV").And.Contain("vertrek 07:53").And.Contain("(69 min, 2 overstappen)")
            .And.Contain("🚌 505 → 🚆 Intercity → 🚆 Intercity").And.Contain("✅ Op tijd")
            .And.Contain("Daarna: 07:55 (+2) → 09:19 · 08:04 ❌ → 09:27 · 08:16 → 09:32");
    }

    [Fact]
    public void Transit_NextTripDelayedOrCancelled_SaysSo()
    {
        BuildTransitText(Trip(10, 60, 4)).Should().Contain("⚠️ 4 min vertraging");
        BuildTransitText(Trip(10, 60, 0, true)).Should().Contain("❌ Deze reis vervalt");
    }

    [Fact]
    public void Transit_WithoutTrips_SaysSo()
    {
        CommuteMessage.BuildTransit(CommuteDirection.ToHome, new TransitRoute(null, null, [])).Should().Contain("Naar huis met het OV").And.Contain("Geen reis gevonden");
    }

    private static string BuildTransitText(TransitTrip trip) =>
        CommuteMessage.BuildTransit(CommuteDirection.ToWork, new TransitRoute("A", "B", [trip]));
}
