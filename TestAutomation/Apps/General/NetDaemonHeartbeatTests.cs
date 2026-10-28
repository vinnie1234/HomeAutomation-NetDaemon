using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Microsoft.Extensions.Options;
using FluentAssertions;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class NetDaemonHeartbeatTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private List<long> Beats() =>
        _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == "input_datetime" &&
                        (string)c.GetArguments()[1]! == "set_datetime" &&
                        ((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single() == NetDaemonHeartbeat.HeartbeatId)
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("timestamp").GetInt64())
            .ToList();

    [Fact]
    public void AtStartup_TheFirstBeatIsSentAtOnce_WithTheCurrentTime()
    {
        _ctx.InitApp<NetDaemonHeartbeat>();

        Beats().Should().Equal(_ctx.Scheduler.Now.ToUnixTimeSeconds());
    }

    [Fact]
    public void EveryMinute_AnotherBeatIsSent_WithTheTimeOfThatMoment()
    {
        _ctx.InitApp<NetDaemonHeartbeat>();

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(3).Ticks);

        var beats = Beats();
        beats.Should().HaveCount(4);
        beats.Zip(beats.Skip(1), (a, b) => b - a).Should().OnlyContain(seconds => seconds == 60);
    }

    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();

    private void StartWith(bool sensorExists)
    {
        var exists = sensorExists;
        _entityManager.EntityExists(NetDaemonHeartbeat.MqttBeatId).Returns(_ => exists);
        _entityManager.Create(NetDaemonHeartbeat.MqttBeatId, Arg.Any<EntityCreationOptions>()).Returns(_ =>
        {
            exists = true;
            return Task.CompletedTask;
        });
        _ctx.InitApp<NetDaemonHeartbeat>(Options.Create(new AppConfig()), _entityManager);
    }

    private List<string> MqttBeats() =>
        _entityManager.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IEntityManager.SetState) && (string)c.GetArguments()[0]! == NetDaemonHeartbeat.MqttBeatId)
            .Select(c => (string)c.GetArguments()[1]!)
            .ToList();

    [Fact]
    public void AtStartup_TheMqttSensorIsCreatedAsATimestamp_AndGetsItsFirstBeat()
    {
        StartWith(sensorExists: false);

        _entityManager.Received(1).Create(NetDaemonHeartbeat.MqttBeatId, Arg.Is<EntityCreationOptions>(o => o.DeviceClass == "timestamp"));
        MqttBeats().Should().HaveCount(1);
    }

    [Fact]
    public void WhenTheMqttSensorExists_ItIsNotCreatedAgain_AndEveryMinuteGetsABeat()
    {
        StartWith(sensorExists: true);

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(3).Ticks);

        _entityManager.DidNotReceiveWithAnyArgs().Create(null!, null!);
        var beats = MqttBeats();
        beats.Should().HaveCount(4);
        beats.Select(b => DateTimeOffset.Parse(b, System.Globalization.CultureInfo.InvariantCulture)).Zip(
                beats.Skip(1).Select(b => DateTimeOffset.Parse(b, System.Globalization.CultureInfo.InvariantCulture)), (a, b) => b - a)
            .Should().OnlyContain(gap => gap == TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void TheMqttBeat_IsAnIsoTimestampWithAnOffset_ThatHomeAssistantCanRead()
    {
        StartWith(sensorExists: true);

        MqttBeats().Single().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$");
    }

    [Fact]
    public void WhenTheConnectionFails_TheAppKeepsBeating()
    {
        _ctx.HaContext.When(h => h.CallService("input_datetime", "set_datetime", Arg.Any<ServiceTarget?>(), Arg.Any<object?>()))
            .Do(_ => throw new InvalidOperationException("not connected"));

        var act = () =>
        {
            _ctx.InitApp<NetDaemonHeartbeat>();
            _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(2).Ticks);
        };

        act.Should().NotThrow();
        _ctx.HaContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CallService").Should().Be(3);
    }
}
