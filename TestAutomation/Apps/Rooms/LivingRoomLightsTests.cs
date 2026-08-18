using Automation.Configuration;
using Automation.apps.Rooms.LivingRoom;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class LivingRoomLightsTests
{
    private static readonly TimeSpan GracePeriod = new PresenceConfiguration().LivingRoomGracePeriod;

    /// <summary>
    /// Arranges a living room that is in use: the light is on and the motion sensor sees somebody,
    /// so the presence service starts out as occupied.
    /// </summary>
    private static AppTestContext ArrangeWithLightOn(string tvState = "off")
    {
        var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("light.woonkamer").Returns(
            new EntityState { EntityId = "light.woonkamer", State = "on" });
        ctx.WithEntityState("binary_sensor.motionwoonkamer", "on")
            .WithEntityState("media_player.tv", tvState);
        return ctx;
    }

    private static void MotionStops(AppTestContext ctx)
    {
        ctx.ChangeStateFor("binary_sensor.motionwoonkamer").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
    }

    [Fact]
    public void LivingRoomLight_StaysOn_WhenVincentAwakeButCarleenSleeping()
    {
        using var ctx = ArrangeWithLightOn();

        // Carleen home and sleeping; Vincent awake (sleepingvincent never turned on → IsSleeping=false)
        ctx.InitApp<LivingRoomLights>(Options.Create(new AppConfiguration()));
        ctx.ChangeStateFor("input_boolean.awaycarleen").FromState("on").ToState("off"); // Carleen home
        ctx.ChangeStateFor("input_boolean.sleepingcarleen").FromState("off").ToState("on"); // Carleen sleeping
        ctx.HaContextMock.ProcessPendingOperations();

        // Motion stops in the living room and the room goes empty
        MotionStops(ctx);
        ctx.AdvanceTimeBy(GracePeriod.Ticks);

        // Living room light must NOT be turned off — Vincent is awake
        ctx.VerifyCallService("light", "turn_off", "woonkamer", times: 0);
    }

    [Fact]
    public void LivingRoomLight_TurnsOff_WhenVincentSleeping()
    {
        using var ctx = ArrangeWithLightOn();

        ctx.InitApp<LivingRoomLights>(Options.Create(new AppConfiguration()));
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on"); // Vincent sleeping
        ctx.HaContextMock.ProcessPendingOperations();

        MotionStops(ctx);
        ctx.AdvanceTimeBy(GracePeriod.Ticks);

        ctx.VerifyCallService("light", "turn_off", "woonkamer");
    }

    [Fact]
    public void LivingRoomLight_StaysOn_UntilTheGracePeriodHasElapsed()
    {
        using var ctx = ArrangeWithLightOn();

        ctx.InitApp<LivingRoomLights>(Options.Create(new AppConfiguration()));
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        MotionStops(ctx);
        ctx.AdvanceTimeBy(GracePeriod.Ticks - TimeSpan.FromMinutes(1).Ticks);

        ctx.VerifyCallService("light", "turn_off", "woonkamer", times: 0);
    }

    /// <summary>
    /// Sitting still while watching TV must not switch the lights off, even though the motion
    /// sensor no longer sees anything.
    /// </summary>
    [Fact]
    public void LivingRoomLight_StaysOn_WhenMotionStops_ButTvIsStillPlaying()
    {
        using var ctx = ArrangeWithLightOn(tvState: "playing");

        ctx.InitApp<LivingRoomLights>(Options.Create(new AppConfiguration()));
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        MotionStops(ctx);
        ctx.AdvanceTimeBy(GracePeriod.Ticks * 2);

        ctx.VerifyCallService("light", "turn_off", "woonkamer", times: 0);
    }
}
