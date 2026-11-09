using System.Text.Json;
using Automation.Core;
using FluentAssertions;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using Xunit;

namespace TestAutomation.Core;

public class ActivityLogTests
{
    private readonly IHaContext _inner = Substitute.For<IHaContext>();
    private readonly List<JsonElement> _events = [];

    public ActivityLogTests() =>
        _inner.When(x => x.SendEvent(ActivityLog.EventType, Arg.Any<object?>()))
            .Do(c => _events.Add(JsonSerializer.SerializeToElement(c.ArgAt<object?>(1))));

    private ActivityHaContext Wrap(string app = "Alarm", IReadOnlyDictionary<string, string>? people = null) => new(_inner, app, people);

    private void SetName(string entityId, string friendlyName) =>
        _inner.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = "off",
            AttributesJson = JsonSerializer.SerializeToElement(new { friendly_name = friendlyName })
        });

    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    [Fact]
    public void AServiceCall_IsPassedOn_AndWrittenToTheLogWithTheApp()
    {
        SetName("light.woonkamer_plafond", "Plafond Woonkamer");

        Wrap("LivingRoomLights").CallService("light", "turn_on", ServiceTarget.FromEntity("light.woonkamer_plafond"), new { brightness_pct = 80 });

        _inner.Received(1).CallService("light", "turn_on", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
        var e = _events.Should().ContainSingle().Subject;
        Text(e, "app").Should().Be("LivingRoomLights");
        Text(e, "kind").Should().Be("actie");
        Text(e, "service").Should().Be("light.turn_on");
        Text(e, "entity").Should().Be("light.woonkamer_plafond");
        Text(e, "text").Should().Be("Plafond Woonkamer: aan (helderheid 80%)");
        Text(e, "room").Should().Be("Woonkamer");
        Text(e, "id").Should().NotBeNullOrEmpty();
        DateTimeOffset.TryParse(Text(e, "time"), out _).Should().BeTrue();
    }

    [Fact]
    public void AnEntityOfPersonOrRoom_IsTaggedWithThemForTheFilters()
    {
        Wrap("Sleep").CallService("input_boolean", "turn_on", ServiceTarget.FromEntity("input_boolean.sleepingcarleen"));
        Wrap("Kitchen").CallService("media_player", "media_play", ServiceTarget.FromEntity("media_player.nestmini9818"));

        Text(_events[0], "person").Should().Be("Carleen");
        Text(_events[0], "room").Should().BeEmpty();
        Text(_events[1], "room").Should().Be("Keuken");
        Text(_events[1], "person").Should().BeEmpty();
    }

    [Fact]
    public void AMessageToAKnownPerson_CountsForThatPerson()
    {
        var people = new Dictionary<string, string> { ["111@lid"] = "vincent", ["222@lid"] = "carleen" };

        Wrap("CommuteReport", people).CallService("whatsapp", "send_message", null, new { clientId = "default", to = "222@lid", body = new { text = "hoi" } });

        var e = _events.Should().ContainSingle().Subject;
        Text(e, "person").Should().Be("Carleen");
        Text(e, "text").Should().Be("whatsapp: bericht verstuurd");
    }

    [Fact]
    public void ACallWithManyLights_IsOneEntryThatListsAFewAndCountsTheRest()
    {
        var entities = new[] { "light.a", "light.b", "light.c", "light.d", "light.e" };

        Wrap().CallService("light", "turn_off", ServiceTarget.FromEntities(entities));

        Text(_events.Should().ContainSingle().Subject, "text").Should().Be("light.a, light.b en 3 andere: uit");
    }

    [Fact]
    public void TheEntityInTheDataOfACall_IsUsedWhenThereIsNoTarget()
    {
        Wrap().CallService("input_boolean", "turn_on", null, new { entity_id = "input_boolean.away" });

        Text(_events.Should().ContainSingle().Subject, "entity").Should().Be("input_boolean.away");
    }

    [Theory]
    [InlineData("input_datetime", "set_datetime", "input_datetime.netdaemon_heartbeat")]
    [InlineData("input_boolean", "turn_on", "input_boolean.netdaemon_net_daemon_heartbeat")]
    [InlineData("logbook", "log", "input_text.x")]
    public void Heartbeats_AndTheLogItself_AreNotWrittenDown(string domain, string service, string entity)
    {
        Wrap().CallService(domain, service, ServiceTarget.FromEntity(entity));

        _events.Should().BeEmpty();
        _inner.Received(1).CallService(domain, service, Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void AFailingLog_NeverBreaksTheCall()
    {
        _inner.When(x => x.SendEvent(Arg.Any<string>(), Arg.Any<object?>())).Do(_ => throw new InvalidOperationException("no connection"));

        var act = () => Wrap().CallService("light", "turn_on", ServiceTarget.FromEntity("light.x"));

        act.Should().NotThrow();
        _inner.Received(1).CallService("light", "turn_on", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public async Task ACallWithAResponse_IsWrittenDownAfterTheAnswer()
    {
        _inner.CallServiceWithResponseAsync("todo", "get_items", Arg.Any<ServiceTarget?>(), Arg.Any<object?>())
            .Returns(Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(new { ok = true })));

        var answer = await Wrap("GroceryList").CallServiceWithResponseAsync("todo", "get_items", ServiceTarget.FromEntity("todo.boodschappen"));

        answer.Should().NotBeNull();
        Text(_events.Should().ContainSingle().Subject, "app").Should().Be("GroceryList");
    }

    [Fact]
    public void AStatusNetDaemonSets_IsWrittenDownAsStatus()
    {
        ActivityLog.Status(_inner, "Entiteiten", "sensor.batteries_to_buy", "3 batterijen");

        var e = _events.Should().ContainSingle().Subject;
        Text(e, "kind").Should().Be("status");
        Text(e, "entity").Should().Be("sensor.batteries_to_buy");
        Text(e, "text").Should().Contain("3 batterijen");
    }

    [Theory]
    [InlineData("light.badkamer_spiegel", "Badkamer")]
    [InlineData("light.hal_2", "Gang")]
    [InlineData("light.plafondslaapkamer", "Slaapkamer")]
    [InlineData("media_player.tv", "Woonkamer")]
    [InlineData("sensor.last_boot", "")]
    public void TheRoomFollowsFromTheWordsInTheEntity(string entity, string expected) =>
        string.Join(", ", ActivityLog.RoomsOf([entity])).Should().Be(expected);
}
