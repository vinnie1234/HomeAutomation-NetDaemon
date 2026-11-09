using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Text.Json;
using Automation.Core;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using Xunit;

namespace TestAutomation.Core;

public class ActivityTriggerTests
{
    private readonly IHaContext _inner = Substitute.For<IHaContext>();
    private readonly Subject<StateChange> _changes = new();
    private readonly Subject<Event> _eventBus = new();
    private readonly TestScheduler _clock = new();
    private readonly List<JsonElement> _events = [];

    public ActivityTriggerTests()
    {
        _inner.StateAllChanges().Returns(_changes);
        _inner.Events.Returns(_eventBus);
        _inner.When(x => x.SendEvent(ActivityLog.EventType, Arg.Any<object?>()))
            .Do(c => _events.Add(JsonSerializer.SerializeToElement(c.ArgAt<object?>(1))));
        _clock.AdvanceTo(new DateTime(2026, 11, 9, 20, 0, 0, DateTimeKind.Utc).Ticks);
    }

    private ActivityHaContext Ha() => new(_inner, "TestApp");

    private StateChange Change(string entityId, string? from, string to, object? attributes = null) =>
        new(new Entity(_inner, entityId),
            from == null ? null : new EntityState { EntityId = entityId, State = from },
            new EntityState { EntityId = entityId, State = to, AttributesJson = JsonSerializer.SerializeToElement(attributes ?? new { friendly_name = "Sensor" }) });

    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString()!;
    private static string[] Chain(JsonElement e) => e.GetProperty("causeChain").EnumerateArray().Select(x => x.GetString()!).ToArray();

    [Fact]
    public void WhatAnAppDoesWhileHandlingAMotionSensor_IsCausedByThatMotion()
    {
        var ha = Ha();
        ha.StateAllChanges().Subscribe(_ => ha.CallService("light", "turn_on", ServiceTarget.FromEntity("light.woonkamer")));

        _changes.OnNext(Change("binary_sensor.beweging_woonkamer", "off", "on", new { friendly_name = "Beweging Woonkamer", device_class = "motion" }));

        var e = _events.Should().ContainSingle().Subject;
        Text(e, "causeKind").Should().Be("entiteit");
        Text(e, "causeEntity").Should().Be("binary_sensor.beweging_woonkamer");
        Text(e, "cause").Should().Be("Beweging: Beweging Woonkamer");
    }

    [Fact]
    public void AnOrdinaryStateChange_IsDescribedWithTheOldAndTheNewState()
    {
        var ha = Ha();
        ha.StateAllChanges().Subscribe(_ => ha.CallService("light", "turn_off", ServiceTarget.FromEntity("light.x")));

        _changes.OnNext(Change("input_boolean.sleepingvincent", "off", "on", new { friendly_name = "SleepingVincent" }));

        Text(_events.Should().ContainSingle().Subject, "cause").Should().Be("SleepingVincent: uit → aan");
    }

    [Fact]
    public void AnEventLikeAButton_IsTheCause_AndTheHighVolumeEventsAreNot()
    {
        var ha = Ha();
        ha.Events.Subscribe(_ => ha.CallService("light", "toggle", ServiceTarget.FromEntity("light.x")));

        _eventBus.OnNext(new Event { EventType = "state_changed" });
        _eventBus.OnNext(new Event { EventType = "hue_event", DataElement = JsonSerializer.SerializeToElement(new { id = "knop_woonkamer" }) });

        _events.Should().HaveCount(2);
        Text(_events[0], "causeKind").Should().Be("geen");
        Text(_events[1], "cause").Should().Be("Hue-knop ingedrukt (knop_woonkamer)");
    }

    [Fact]
    public void WithoutATrigger_TheLogSaysNothingWasFound()
    {
        Ha().CallService("light", "turn_on", ServiceTarget.FromEntity("light.x"));

        var e = _events.Should().ContainSingle().Subject;
        Text(e, "causeKind").Should().Be("geen");
        Text(e, "cause").Should().StartWith("Geen aanleiding gevonden");
        Chain(e).Should().BeEmpty();
    }

    [Fact]
    public void TheTrigger_IsOnlyCurrentWhileTheStateChangeIsHandled()
    {
        var ha = Ha();
        ActivityTrigger? during = null;
        ha.StateAllChanges().Subscribe(_ => during = ActivityContext.Current);

        _changes.OnNext(Change("sensor.x", "1", "2"));

        during.Should().NotBeNull();
        ActivityContext.Current.Should().BeNull();
    }

    [Fact]
    public void ATimerThatRunsOut_IsCausedByTheTimeItWasSet()
    {
        var scheduler = new ActivityScheduler(_clock);
        var ha = Ha();
        scheduler.Schedule(TimeSpan.FromSeconds(30), () => ha.CallService("light", "turn_off", ServiceTarget.FromEntity("light.x")));

        _clock.AdvanceBy(TimeSpan.FromSeconds(31).Ticks);

        var e = _events.Should().ContainSingle().Subject;
        Text(e, "causeKind").Should().Be("tijd");
        Text(e, "cause").Should().Be("Timer van 30 s afgelopen");
    }

    [Fact]
    public void ASchedule_AtAClockTime_SaysWhichTime()
    {
        var scheduler = new ActivityScheduler(_clock);
        var ha = Ha();
        // 22:00 UTC is 23:00 Dutch time in November
        scheduler.Schedule(new DateTimeOffset(2026, 11, 9, 22, 0, 0, TimeSpan.Zero), () => ha.CallService("light", "turn_off", ServiceTarget.FromEntity("light.x")));

        _clock.AdvanceTo(new DateTime(2026, 11, 9, 22, 0, 1, DateTimeKind.Utc).Ticks);

        Text(_events.Should().ContainSingle().Subject, "cause").Should().Be("Tijd: gepland om 23:00:00");
    }

    [Fact]
    public void ATimerStartedByMotion_KeepsTheMotionAsTheFirstStepOfTheChain()
    {
        var scheduler = new ActivityScheduler(_clock);
        var ha = Ha();
        ha.StateAllChanges().Subscribe(_ =>
            scheduler.Schedule(TimeSpan.FromMinutes(5), () => ha.CallService("light", "turn_off", ServiceTarget.FromEntity("light.woonkamer"))));

        _changes.OnNext(Change("binary_sensor.beweging_woonkamer", "on", "off", new { friendly_name = "Beweging Woonkamer", device_class = "motion" }));
        _clock.AdvanceBy(TimeSpan.FromMinutes(6).Ticks);

        var e = _events.Should().ContainSingle().Subject;
        Chain(e).Should().Equal("Geen beweging meer: Beweging Woonkamer", "5 min later");
        Text(e, "cause").Should().Be("Geen beweging meer: Beweging Woonkamer › 5 min later");
        Text(e, "causeKind").Should().Be("entiteit");
        Text(e, "causeEntity").Should().Be("binary_sensor.beweging_woonkamer");
    }

    [Fact]
    public async Task TheCause_AlsoFollowsARealTimerOnAnotherThread()
    {
        // The real scheduler runs its timers on other threads: the cause has to be taken along there too
        var scheduler = new ActivityScheduler(Scheduler.Default);
        var ha = Ha();
        var done = new TaskCompletionSource();
        ha.StateAllChanges().Subscribe(_ => scheduler.Schedule(TimeSpan.FromMilliseconds(60), () =>
        {
            ha.CallService("light", "turn_off", ServiceTarget.FromEntity("light.x"));
            done.SetResult();
        }));

        _changes.OnNext(Change("binary_sensor.beweging", "on", "off", new { friendly_name = "Beweging", device_class = "motion" }));
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Text(_events.Should().ContainSingle().Subject, "cause").Should().Be("Geen beweging meer: Beweging › 1 s later");
    }

    [Fact]
    public void ARepeatingTimer_DoesNotGrowAChain()
    {
        var scheduler = new ActivityScheduler(_clock);
        var ha = Ha();
        void Tick() => scheduler.Schedule(TimeSpan.FromMinutes(1), () =>
        {
            ha.CallService("light", "toggle", ServiceTarget.FromEntity("light.x"));
            Tick();
        });
        Tick();

        _clock.AdvanceBy(TimeSpan.FromMinutes(4).Ticks + 1);

        _events.Should().HaveCount(4);
        _events.Select(Chain).Should().OnlyContain(c => c.Length == 1);
    }

    [Fact]
    public void TheDataOfTheCall_GoesWithTheAction()
    {
        Ha().CallService("light", "turn_on", ServiceTarget.FromEntity("light.x"), new { brightness_pct = 40, transition = 2 });

        Text(_events.Should().ContainSingle().Subject, "data").Should().Be("{\"brightness_pct\":40,\"transition\":2}");
    }
}
