using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class CommuteReportTests
{
    private const string VincentWhatsapp = "111@lid";
    private const string CarleenWhatsapp = "222@lid";

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void SetState(string entityId, string state, object? attributes = null) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = state,
            AttributesJson = attributes == null ? null : JsonSerializer.SerializeToElement(attributes)
        });

    /// <summary>Starts the app on a Thursday (2026-10-29) at 00:00 Dutch time, with a normal route and the given work day setting.</summary>
    private void Start(string thursday, bool vincentHome = true, string whatsapp = VincentWhatsapp, Action? configure = null)
    {
        SetState("input_select.werkdag_vincent_donderdag", thursday);
        SetState("input_boolean.vrij_vincent", "off");
        SetState("input_boolean.onvacation", "off");
        SetState("input_boolean.awayvincent", vincentHome ? "off" : "on");
        SetState("zone.home", "0", new { latitude = 52.028, longitude = 5.528 });
        var entities = new List<Entity>();
        foreach (var (prefix, from, to) in new (string, double[], double[])[] { ("sensor.anwb_route_huis_werk", [52.028, 5.528], [52.063, 5.088]),
                     ("sensor.anwb_route_werk_huis", [52.063, 5.088], [52.028, 5.528]) })
        {
            SetState($"{prefix}_reistijd", "31", new { normal_duration_min = 31 });
            SetState($"{prefix}_vertraging", "0");
            SetState($"{prefix}_aankomsttijd", "2026-10-29T06:21:00+00:00");
            SetState($"{prefix}_route", "A12", new { person = "person.vincent_maarschalkerweerd", via = new[] { "A12" }, origin = from, destination = to, alternatives = Array.Empty<object>() });
            entities.Add(new Entity(_ctx.HaContext, $"{prefix}_route"));
        }
        // Carleen's routes: the name says which way they go, even when the places are the other way around
        foreach (var (prefix, name) in new[] { ("sensor.anwb_route_carleen_heen", "ANWB Route Huis => Werk Carleen Route"), ("sensor.anwb_route_carleen_terug", "ANWB Route Werk => Huis Carleen Route") })
        {
            SetState($"{prefix}_reistijd", "25", new { normal_duration_min = 25 });
            SetState($"{prefix}_vertraging", "0");
            SetState($"{prefix}_aankomsttijd", "2026-10-29T06:21:00+00:00");
            SetState($"{prefix}_route", "A27", new { friendly_name = name, person = "person.carleen", via = new[] { "A27" }, origin = new[] { 52.2, 5.2 }, destination = new[] { 52.2, 5.2 }, alternatives = Array.Empty<object>() });
            entities.Add(new Entity(_ctx.HaContext, $"{prefix}_route"));
        }
        configure?.Invoke();
        _ctx.HaContext.GetAllEntities().Returns(entities);
        _ctx.SetCurrentTime(new DateTime(2026, 10, 28, 23, 0, 0, DateTimeKind.Utc));
        _ctx.InitApp<CommuteReport>(Options.Create(new AppConfig { Whatsapp = new WhatsAppConfig { Vincent = whatsapp, Carleen = CarleenWhatsapp } }));
    }

    private List<(string To, string Text)> Messages() =>
        _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == "whatsapp")
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]))
            .Select(d => (d.GetProperty("to").GetString()!, d.GetProperty("body").GetProperty("text").GetString()!))
            .ToList();

    private void AdvanceTo(int hour, int minute)
    {
        // 2026-10-29 is in winter time (UTC+1)
        _ctx.AdvanceTimeTo(new DateTime(2026, 10, 29, hour - 1, minute, 0, DateTimeKind.Utc).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    [Fact]
    public void OfficeDay_At0750_SendsTheTripToTheOffice()
    {
        Start("Kantoor");

        AdvanceTo(7, 51);

        var messages = Messages();
        messages.Should().ContainSingle();
        messages[0].To.Should().Be(VincentWhatsapp);
        messages[0].Text.Should().Contain("Naar kantoor").And.Contain("Reistijd: 31 min");
    }

    [Fact]
    public void OfficeDay_At1715_SendsTheTripHome_WhenStillAway()
    {
        Start("Kantoor", vincentHome: false);

        AdvanceTo(17, 16);

        Messages().Where(m => m.Text.Contains("Naar huis")).Should().ContainSingle();
    }

    [Fact]
    public void OfficeDay_At1715_SendsNothing_WhenAlreadyHome()
    {
        Start("Kantoor");

        AdvanceTo(17, 16);

        Messages().Should().NotContain(m => m.Text.Contains("Naar huis"));
    }

    [Theory]
    [InlineData("Thuis")]
    [InlineData("Vrij")]
    public void NoOfficeDay_SendsNothing(string setting)
    {
        Start(setting);

        AdvanceTo(17, 16);

        Messages().Should().BeEmpty();
    }

    [Fact]
    public void TestButton_SendsBothMessages_EvenOnAFreeDay()
    {
        Start("Vrij");

        _ctx.ChangeStateFor(CommuteReport.TestButton).FromState("2026-10-28T10:00:00").ToState("2026-10-29T10:00:00");
        _ctx.HaContextMock.ProcessPendingOperations();

        var messages = Messages();
        messages.Should().HaveCount(2);
        messages.Select(m => m.Text).Should().Contain(t => t.Contains("Naar kantoor")).And.Contain(t => t.Contains("Naar huis"));
        messages.Should().OnlyContain(m => m.Text.StartsWith("🧪 Test"));
    }

    [Fact]
    public void ARouteOfCarleen_SendsHerTheMessage_AndNotVincent()
    {
        Start("Vrij", configure: () =>
        {
            SetState("input_select.werkdag_carleen_donderdag", "Kantoor");
            SetState("input_boolean.vrij_carleen", "off");
            SetState("input_boolean.awaycarleen", "off");
        });

        AdvanceTo(7, 51);

        var messages = Messages();
        messages.Should().ContainSingle().Which.To.Should().Be(CarleenWhatsapp);
        messages[0].Text.Should().Contain("Naar kantoor").And.Contain("Reistijd: 25 min").And.Contain("Via A27");
    }

    [Fact]
    public void MessageSwitchedOff_SendsNothing()
    {
        Start("Kantoor", configure: () => SetState("input_boolean.verkeer_bericht_vincent", "off"));

        AdvanceTo(7, 51);

        Messages().Should().BeEmpty();
    }

    [Fact]
    public void TimeFromTheHelper_DecidesWhenTheMessageComes()
    {
        Start("Kantoor", configure: () => SetState("input_datetime.verkeer_heen_vincent", "08:05:00"));

        AdvanceTo(7, 51);
        Messages().Should().BeEmpty("the helper says 08:05");

        AdvanceTo(8, 6);
        Messages().Should().ContainSingle().Which.Text.Should().Contain("Naar kantoor");
    }

    [Fact]
    public void RouteUnavailable_SaysSo()
    {
        Start("Kantoor");
        SetState("sensor.anwb_route_huis_werk_reistijd", "unavailable");

        AdvanceTo(7, 51);

        Messages().Should().ContainSingle().Which.Text.Should().Contain("niet beschikbaar");
    }

    [Fact]
    public void NoWhatsappId_SendsNothing()
    {
        Start("Kantoor", whatsapp: "");

        AdvanceTo(7, 51);

        Messages().Should().BeEmpty();
    }
}
