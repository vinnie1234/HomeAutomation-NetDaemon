using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.Traffic;
using Automation.Models.WhatsApp;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetDaemon.Client;
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
    private readonly List<Entity> _entities = [];
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();
    private readonly IHomeAssistantRunner _runner = Substitute.For<IHomeAssistantRunner>();
    private readonly IHomeAssistantConnection _connection = Substitute.For<IHomeAssistantConnection>();

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
        _entities.Clear();
        var entities = _entities;
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
        _runner.CurrentConnection.Returns(_connection);
        _ctx.InitApp<CommuteReport>(Options.Create(new AppConfig { Whatsapp = new WhatsAppConfig { Vincent = whatsapp, Carleen = CarleenWhatsapp } }), _storage, _runner);
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
    public void ANinesTwoNineTwoRouteOfCarleen_SendsHerTheTrips()
    {
        Start("Vrij", configure: () =>
        {
            SetState("input_select.werkdag_carleen_donderdag", "Kantoor");
            SetState("input_boolean.vrij_carleen", "off");
            SetState("input_boolean.awaycarleen", "off");
            SetState("input_select.vervoer_carleen_donderdag", "OV");
            SetState("sensor.9292_thuis_nijmegen_carleen_lijnen", "505 · 3255", new
            {
                friendly_name = "9292 Thuis -> Nijmegen (Carleen) Lijnen",
                person = "person.carleen",
                from = "Veenendaal",
                to = "Nijmegen",
                upcoming = new[]
                {
                    new
                    {
                        departure_actual = "2026-10-29T07:10:00+00:00", arrival = "2026-10-29T08:19:00+00:00", duration_min = 69, delay_min = 0, transfers = 2,
                        legs = new[] { new { mode = "Bus", line = "505", cancelled = false }, new { mode = "FastTrain", line = "3255", cancelled = false } },
                        cancelled = false
                    }
                }
            });
            _entities.Add(new Entity(_ctx.HaContext, "sensor.9292_thuis_nijmegen_carleen_lijnen"));
        });

        AdvanceTo(7, 51);

        var messages = Messages();
        messages.Should().Contain(m => m.To == CarleenWhatsapp && m.Text.Contains("Naar kantoor met het OV") && m.Text.Contains("vertrek 08:10") && m.Text.Contains("🚌 505"));
        messages.Where(m => m.To == CarleenWhatsapp).Should().ContainSingle("only the way of the day (OV) is sent, not the car route as well");
        messages.Single(m => m.To == CarleenWhatsapp).Text.Should().NotContain("Reistijd");
    }

    [Fact]
    public void TheCarIsTheDefault_AndTheMessageHasTheOfficeTime()
    {
        Start("Kantoor", configure: () => SetState("input_datetime.kantoor_aankomst_vincent", "2026-10-29 08:20:00"));

        AdvanceTo(7, 51);

        // 31 minutes of driving to be there at 08:20: leave by 07:49
        Messages().Should().ContainSingle().Which.Text.Should().Contain("Op kantoor om 08:20: uiterlijk vertrekken om 07:49");
    }

    [Fact]
    public void WithATimeToBeAtTheOffice_TheMessageComesTenMinutesBeforeTheDepartureThatGetsYouThere()
    {
        Start("Kantoor", configure: () => SetState("input_datetime.kantoor_aankomst_vincent", "2026-10-29 08:20:00"));

        // Be there at 08:20 with 31 minutes of driving: leave at 07:49, so the message comes at 07:39 and not at the fixed 07:50
        AdvanceTo(7, 38);
        Messages().Should().BeEmpty();
        AdvanceTo(7, 40);
        Messages().Should().ContainSingle();
        AdvanceTo(8, 5);
        Messages().Should().ContainSingle("it comes once");
    }

    [Fact]
    public void WithATimeToLeave_TheMessageComesTenMinutesBeforeIt()
    {
        Start("Kantoor", configure: () =>
        {
            SetState("input_boolean.werk_uit_agenda_vincent", "off");
            SetState("input_select.reis_ochtend_soort_vincent", "Van huis vertrekken");
            SetState("input_datetime.thuis_vertrek_vincent", "2026-10-29 08:30:00");
        });

        AdvanceTo(8, 19);
        Messages().Should().BeEmpty();
        AdvanceTo(8, 21);
        Messages().Should().ContainSingle();
    }

    [Fact]
    public void AMessageForSomebodyWhoHasLeftAlready_IsNotSent()
    {
        Start("Kantoor", vincentHome: false, configure: () => SetState("input_datetime.kantoor_aankomst_vincent", "2026-10-29 08:20:00"));

        AdvanceTo(7, 45);

        Messages().Where(m => m.Text.Contains("Naar kantoor")).Should().BeEmpty();
    }

    [Fact]
    public void ATimeToLeaveHome_GetsNoAdvice()
    {
        Start("Kantoor", configure: () =>
        {
            SetState("input_boolean.werk_uit_agenda_vincent", "off");
            SetState("input_select.reis_ochtend_soort_vincent", "Van huis vertrekken");
            SetState("input_datetime.thuis_vertrek_vincent", "2026-10-29 08:00:00");
        });

        AdvanceTo(7, 51);

        // The message came at 07:50, ten minutes before the time to leave
        var text = Messages().Should().ContainSingle().Which.Text;
        text.Should().Contain("🎯 Vertrek om 08:00").And.NotContain("uiterlijk vertrekken");
    }

    [Fact]
    public void ATimeToBeHome_GetsTheAdviceWhenToLeaveTheOffice()
    {
        Start("Kantoor", vincentHome: false, configure: () =>
        {
            SetState("input_boolean.werk_uit_agenda_vincent", "off");
            SetState("input_select.reis_avond_soort_vincent", "Thuis zijn");
            SetState("input_datetime.thuis_aankomst_vincent", "2026-10-29 18:00:00");
        });

        AdvanceTo(17, 20);

        // 31 minutes of driving to be home at 18:00: leave the office by 17:29 (the message came at 17:19)
        Messages().Single(m => m.Text.Contains("Naar huis")).Text.Should().Contain("Thuis om 18:00: uiterlijk vertrekken om 17:29");
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

    /// <summary>Carleen's OV route with the id of the first trip, the way she goes to the office on Thursday.</summary>
    private void CarleenGoesByTrain()
    {
        SetState("input_select.werkdag_carleen_donderdag", "Kantoor");
        SetState("input_boolean.vrij_carleen", "off");
        SetState("input_boolean.awaycarleen", "off");
        SetState("input_select.vervoer_carleen_donderdag", "OV");
        SetState("sensor.9292_thuis_nijmegen_carleen_lijnen", "505 · 3255", new
        {
            friendly_name = "9292 Thuis -> Nijmegen (Carleen) Lijnen",
            person = "person.carleen",
            route_id = "route-1",
            upcoming = new[]
            {
                new
                {
                    id = "journey-1", departure_actual = "2026-10-29T07:10:00+00:00", arrival = "2026-10-29T08:19:00+00:00", duration_min = 69, delay_min = 0,
                    transfers = 1, legs = Array.Empty<object>(), cancelled = false
                }
            }
        });
        _entities.Add(new Entity(_ctx.HaContext, "sensor.9292_thuis_nijmegen_carleen_lijnen"));
        _connection.SendCommandAndReturnResponseAsync<GetJourneyCommand, JourneyDetail>(Arg.Is<GetJourneyCommand>(c => c.JourneyId == "journey-1" && c.RouteId == "route-1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<JourneyDetail?>(new JourneyDetail
            {
                Legs =
                [
                    new JourneyLeg { Mode = "Walking", DistanceM = 450, From = new JourneyStop { Name = "Thuis", Time = "2026-10-29T07:05:00+00:00" }, To = new JourneyStop { Name = "Veenendaal Centrum", Time = "2026-10-29T07:10:00+00:00" } },
                    new JourneyLeg
                    {
                        Mode = "Bus", Line = "505", Destination = "Ede",
                        From = new JourneyStop { Name = "Veenendaal Centrum", Time = "2026-10-29T07:10:00+00:00", Platform = "B" },
                        Stops = [new JourneyStop { Name = "Veenendaal Centrum", Time = "2026-10-29T07:10:00+00:00" }, new JourneyStop { Name = "Veenendaal-De Klomp", Time = "2026-10-29T07:20:00+00:00" }],
                        To = new JourneyStop { Name = "Ede-Wageningen", Time = "2026-10-29T07:30:00+00:00", Real = "2026-10-29T07:32:00+00:00" }
                    },
                    new JourneyLeg
                    {
                        Mode = "FastTrain", Line = "3255", Destination = "Nijmegen",
                        From = new JourneyStop { Name = "Ede-Wageningen", Time = "2026-10-29T07:40:00+00:00", Platform = "4" },
                        To = new JourneyStop { Name = "Nijmegen", Time = "2026-10-29T08:19:00+00:00", Platform = "2" }
                    }
                ]
            }));
    }

    [Fact]
    public void ByTrain_TheMessageHasEveryStep_AndTellsThatItCanBeForwarded()
    {
        Start("Vrij", configure: CarleenGoesByTrain);

        AdvanceTo(7, 51);

        var text = Messages().Single(m => m.To == CarleenWhatsapp).Text;
        text.Should().Contain("🚶 08:05 Loop naar Veenendaal Centrum (450 m)")
            .And.Contain("*Bus 505* richting Ede").And.Contain("vanaf Veenendaal Centrum, perron B")
            .And.Contain("• 08:20 Veenendaal-De Klomp")
            .And.Contain("➜ 08:32 (+2) Ede-Wageningen")
            .And.Contain("🔁 Overstappen op Ede-Wageningen: 8 min")
            .And.Contain("*Intercity* richting Nijmegen").And.Contain("vanaf Ede-Wageningen, spoor 4")
            .And.Contain("🏁 Aankomst 09:19 Nijmegen")
            .And.Contain("Antwoord *Stuur door* om dit naar Vincent te sturen");
    }

    [Fact]
    public void ByTrain_WithoutTheJourneyDetails_StillHasTheShortMessageAndTheHint()
    {
        Start("Vrij", configure: () =>
        {
            CarleenGoesByTrain();
            _connection.SendCommandAndReturnResponseAsync<GetJourneyCommand, JourneyDetail>(Arg.Any<GetJourneyCommand>(), Arg.Any<CancellationToken>())
                .Returns<Task<JourneyDetail?>>(_ => throw new InvalidOperationException("no answer"));
        });

        AdvanceTo(7, 51);

        var text = Messages().Single(m => m.To == CarleenWhatsapp).Text;
        text.Should().Contain("Naar kantoor met het OV").And.Contain("Stuur door");
    }

    [Fact]
    public void TheTravelMessage_IsTheLastReplyOfTheConversation_SoStuurDoorForwardsIt()
    {
        Start("Vrij", configure: CarleenGoesByTrain);

        AdvanceTo(7, 51);

        _storage.Received().Save(WhatsAppAssistant.HistoryStorageKey, Arg.Is<Dictionary<string, List<WhatsAppConversationTurn>>>(h =>
            h.ContainsKey(CarleenWhatsapp) && h[CarleenWhatsapp].Last().AssistantReply.Contains("Naar kantoor met het OV")));
    }

    [Fact]
    public void TheForwardButton_SendsTheOtherPersonTheExpectedTimes()
    {
        Start("Kantoor", configure: () => SetState("input_datetime.kantoor_aankomst_vincent", "2026-10-29 08:20:00"));
        _ctx.HaContext.ClearReceivedCalls();

        _ctx.ChangeStateFor("input_button.reis_doorsturen_vincent").FromState("unknown").ToState("2026-10-29T06:30:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(100);

        var message = Messages().Should().ContainSingle().Which;
        message.To.Should().Be(CarleenWhatsapp);
        message.Text.Should().Contain("Doorgestuurd door Vincent: dit zijn de tijden die verwacht worden voor Vincent").And.Contain("Op kantoor om 08:20");
    }

    [Fact]
    public void TheForwardButton_OfCarleen_GoesToVincent()
    {
        Start("Vrij", configure: CarleenGoesByTrain);
        _ctx.HaContext.ClearReceivedCalls();

        _ctx.ChangeStateFor("input_button.reis_doorsturen_carleen").FromState("unknown").ToState("2026-10-29T06:30:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(100);

        var message = Messages().Should().ContainSingle().Which;
        message.To.Should().Be(VincentWhatsapp);
        message.Text.Should().Contain("Doorgestuurd door Carleen").And.NotContain("Antwoord *Stuur door*");
    }
}
