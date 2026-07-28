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
    private static AppTestContext ArrangeWithLightOn()
    {
        var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("light.woonkamer").Returns(
            new EntityState { EntityId = "light.woonkamer", State = "on" });
        return ctx;
    }

    [Fact]
    public void LivingRoomLight_StaysOn_WhenVincentAwakeButCarleenSleeping()
    {
        var ctx = ArrangeWithLightOn();

        // Carleen home and sleeping; Vincent awake (sleepingvincent never turned on → IsSleeping=false)
        ctx.InitApp<LivingRoomLights>(Options.Create(new AppConfiguration()));
        ctx.ChangeStateFor("input_boolean.awaycarleen").FromState("on").ToState("off"); // Carleen home
        ctx.ChangeStateFor("input_boolean.sleepingcarleen").FromState("off").ToState("on"); // Carleen sleeping
        ctx.HaContextMock.ProcessPendingOperations();

        // Motion stops in the living room
        ctx.ChangeStateFor("binary_sensor.motionwoonkamer").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        // Living room light must NOT be turned off — Vincent is awake
        ctx.VerifyCallService("light", "turn_off", "woonkamer", times: 0);
    }

    [Fact]
    public void LivingRoomLight_TurnsOff_WhenVincentSleeping()
    {
        var ctx = ArrangeWithLightOn();

        ctx.InitApp<LivingRoomLights>(Options.Create(new AppConfiguration()));
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on"); // Vincent sleeping
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.ChangeStateFor("binary_sensor.motionwoonkamer").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("light", "turn_off", "woonkamer");
    }
}


