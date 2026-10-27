using System.Text.Json;
using Automation.apps.General;
using FluentAssertions;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
