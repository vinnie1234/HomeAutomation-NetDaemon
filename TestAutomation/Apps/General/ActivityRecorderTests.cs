using System.Reactive.Subjects;
using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Core;
using Automation.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;
using EntityCreationOptions = Automation.Interfaces.EntityCreationOptions;

namespace TestAutomation.Apps.General;

public class ActivityRecorderTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();
    private readonly IMqttEntityManager _mqtt = Substitute.For<IMqttEntityManager>();
    private readonly Subject<Event> _events = new();
    private readonly List<string> _calls = [];

    public ActivityRecorderTests()
    {
        _mqtt.SetAttributesAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(c => { _calls.Add("attributes:" + JsonSerializer.SerializeToElement(c.ArgAt<object>(1)).GetProperty("id").GetString()); return Task.CompletedTask; });
        _mqtt.SetStateAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(c => { _calls.Add("state:" + c.ArgAt<string>(1)); return Task.CompletedTask; });
    }

    private void Start(bool sensorExists = false, ActivityBacklog? backlog = null)
    {
        _entityManager.EntityExists(ActivityRecorder.Sensor).Returns(sensorExists);
        _ctx.HaContext.Events.Returns(_events);
        // An own backlog: the one of ActivityLog is shared by every test that runs at the same time
        _ctx.InitApp<ActivityRecorder>(Options.Create(new AppConfig()), _entityManager, _mqtt, backlog ?? new ActivityBacklog());
    }

    private static JsonElement Entry(string id) =>
        JsonSerializer.SerializeToElement(new { id, app = "Alarm", kind = "actie", text = "Lamp: aan" });

    private void Fire(string id, string eventType = ActivityLog.EventType) =>
        _events.OnNext(new Event
        {
            EventType = eventType,
            DataElement = JsonSerializer.SerializeToElement(new { id, app = "Alarm", kind = "actie", text = "Lamp: aan" })
        });

    private async Task WaitFor(int calls)
    {
        for (var i = 0; i < 100 && _calls.Count < calls; i++) await Task.Delay(20);
    }

    [Fact]
    public async Task AnAction_BecomesTheAttributesFirst_AndThenTheStateWithTheSameId()
    {
        Start();

        Fire("a1");
        await WaitFor(2);

        _calls.Should().Equal("attributes:a1", "state:a1");
        await _mqtt.Received(1).SetStateAsync(ActivityRecorder.Sensor, "a1");
    }

    [Fact]
    public async Task ActionsThatComeTogether_AreWrittenOneByOne_InTheOrderTheyCame()
    {
        Start();

        Fire("a1"); Fire("a2"); Fire("a3");
        await WaitFor(6);

        _calls.Should().Equal("attributes:a1", "state:a1", "attributes:a2", "state:a2", "attributes:a3", "state:a3");
    }

    [Fact]
    public async Task WhatHappenedBeforeTheRecorderWasListening_IsWrittenToo_AndOnlyOnce()
    {
        var backlog = new ActivityBacklog();
        backlog.Add(Entry("early1"));
        backlog.Add(Entry("early2"));

        Start(backlog: backlog);
        Fire("early2");
        Fire("a1");
        await WaitFor(6);

        _calls.Should().Equal("attributes:early1", "state:early1", "attributes:early2", "state:early2", "attributes:a1", "state:a1");
    }

    [Fact]
    public void TheBacklog_IsHandedOverOnce_AndThenKeepsNothing()
    {
        var backlog = new ActivityBacklog();
        backlog.Add(Entry("x"));

        backlog.Drain().Should().HaveCount(1);
        backlog.Add(Entry("y"));

        backlog.Drain().Should().BeEmpty();
    }

    [Fact]
    public void TheBacklog_KeepsTheLast500()
    {
        var backlog = new ActivityBacklog();
        for (var i = 0; i < 520; i++) backlog.Add(Entry($"e{i}"));

        var kept = backlog.Drain();

        kept.Should().HaveCount(500);
        kept[0].GetProperty("id").GetString().Should().Be("e20");
    }

    [Fact]
    public async Task TheSensor_IsCreatedByNetDaemon_WhenItDoesNotExist()
    {
        Start(sensorExists: false);

        Fire("a1");
        await WaitFor(2);

        await _entityManager.Received(1).Create(ActivityRecorder.Sensor, Arg.Is<EntityCreationOptions>(o => o.Name == "NetDaemon activiteit"));
    }

    [Fact]
    public async Task AnExistingSensor_IsLeftAlone()
    {
        Start(sensorExists: true);

        Fire("a1");
        await WaitFor(2);

        await _entityManager.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<EntityCreationOptions>());
    }

    [Fact]
    public async Task OtherEvents_AreIgnored_AndAFailingWriteDoesNotStopTheNext()
    {
        Start();
        var first = true;
        _mqtt.SetAttributesAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(c =>
        {
            if (first) { first = false; throw new InvalidOperationException("mqtt down"); }
            _calls.Add("attributes:" + JsonSerializer.SerializeToElement(c.ArgAt<object>(1)).GetProperty("id").GetString());
            return Task.CompletedTask;
        });

        Fire("x", eventType: "something_else");
        Fire("a1");
        Fire("a2");
        await WaitFor(2);

        _calls.Should().Equal("attributes:a2", "state:a2");
    }
}
