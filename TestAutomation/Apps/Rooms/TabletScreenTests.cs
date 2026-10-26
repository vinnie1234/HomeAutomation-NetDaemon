using Automation.apps.Rooms.LivingRoom;
using FluentAssertions;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class TabletScreenTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    private TabletScreenTests Start(string tv = "off", string sleepingVincent = "off", string sleepingCarleen = "off")
    {
        SetState("input_boolean.awayvincent", "off");
        SetState("input_boolean.awaycarleen", "off");
        SetState("input_boolean.sleepingvincent", sleepingVincent);
        SetState("input_boolean.sleepingcarleen", sleepingCarleen);
        SetState("input_boolean.onvacation", "off");
        SetState("input_boolean.away", "off");
        SetState("media_player.tv", tv);
        _ctx.InitApp<TabletScreen>();
        return this;
    }

    private void Change(string entityId, string from, string to)
    {
        _ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    /// <summary>The service calls to the Fully Kiosk entities of the tablet, as "domain.service", in order.</summary>
    private List<string> Commands() =>
        _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" &&
                        (c.GetArguments()[2] as ServiceTarget)?.EntityIds?.Any(e => e.Contains("tab_s6_van_vincent")) == true)
            .Select(c => $"{c.GetArguments()[0]}.{c.GetArguments()[1]}")
            .ToList();

    [Fact]
    public void MotionInTheLivingRoom_TurnsTheScreenOn_AndOpensHome()
    {
        Start();

        Change("binary_sensor.motionwoonkamer", "off", "on");

        Commands().Should().Equal("number.set_value", "switch.turn_on", "button.press");
    }

    [Fact]
    public void Motion_WhileTheTvIsOn_DoesNothing()
    {
        Start(tv: "on");

        Change("binary_sensor.motionwoonkamer", "off", "on");

        Commands().Should().BeEmpty();
    }

    [Fact]
    public void Motion_WhileEveryoneSleeps_DoesNothing()
    {
        Start(sleepingVincent: "on", sleepingCarleen: "on");

        Change("binary_sensor.motionwoonkamer", "off", "on");

        Commands().Should().BeEmpty();
    }

    [Fact]
    public void Motion_WhileOnlyCarleenSleeps_TurnsTheScreenOn()
    {
        Start(sleepingCarleen: "on");

        Change("binary_sensor.motionwoonkamer", "off", "on");

        Commands().Should().Contain("switch.turn_on");
    }

    [Fact]
    public void MoreMotion_ShortlyAfter_DoesNotWakeItAgain()
    {
        Start();
        Change("binary_sensor.motionwoonkamer", "off", "on");
        Change("binary_sensor.motionwoonkamer", "on", "off");

        Change("binary_sensor.motionwoonkamer", "off", "on");

        Commands().Count(c => c == "switch.turn_on").Should().Be(1);
    }

    [Fact]
    public void SomeoneComesHome_TurnsTheScreenOn()
    {
        Start();

        Change("input_boolean.awaycarleen", "on", "off");

        Commands().Should().Contain("switch.turn_on");
    }

    [Fact]
    public void TvTurnsOn_TurnsTheScreenOff()
    {
        Start();

        Change("media_player.tv", "off", "on");

        Commands().Should().Equal("switch.turn_off");
    }

    [Fact]
    public void LastOneGoesToSleep_TurnsTheScreenOff_ButNotWhileTheOtherIsAwake()
    {
        Start(sleepingCarleen: "on");
        Change("input_boolean.sleepingcarleen", "off", "on");
        Commands().Should().BeEmpty();

        SetState("input_boolean.sleepingvincent", "on");
        Change("input_boolean.sleepingvincent", "off", "on");

        Commands().Should().Equal("switch.turn_off");
    }
}
