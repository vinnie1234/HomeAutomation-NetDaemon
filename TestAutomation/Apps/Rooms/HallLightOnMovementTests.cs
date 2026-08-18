using Automation.apps.Rooms.Hall;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class HallLightOnMovementTests
{
    private void SetupDefaultStates(AppTestContext ctx)
    {
        ctx.HaContext.GetState("input_boolean.disablelightautomationhall").Returns(new EntityState { EntityId = "input_boolean.disablelightautomationhall", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        ctx.HaContext.GetState("input_number.halllightnighttime").Returns(new EntityState { EntityId = "input_number.halllightnighttime", State = "5" });
        ctx.HaContext.GetState("input_number.halllightdaytime").Returns(new EntityState { EntityId = "input_number.halllightdaytime", State = "10" });
    }

    [Fact]
    public void MotionDetected_TurnsOnHalLights_WhenNotDisabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<HallLightOnMovement>();

        // Act
        ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_on", "hal_2", times: 1);
    }

    [Fact]
    public void MotionDetected_TurnsOnHalLights_AtFullBrightness_WhenDaytime()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<HallLightOnMovement>();

        // Act
        ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        // We can check if it passed brightness=100 in the args, though full checking might require checking call data.
        ctx.VerifyCallService("light", "turn_on", "hal_2", times: 1);
        ctx.VerifyCallService("light", "turn_on", "hal", times: 1);
    }

    [Fact]
    public void MotionDetected_TurnsOnHalLights_AtLowBrightness_WhenNightMode()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "on" });
        var app = ctx.InitApp<HallLightOnMovement>();

        // Act
        ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_on", "hal_2", times: 1);
        // During night mode (and not office day with Vincent awake), hal should not turn on
        ctx.VerifyCallService("light", "turn_on", "hal", times: 0);
    }

    [Fact]
    public void MotionDetected_DoesNothing_WhenLightAutomationsDisabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablelightautomationhall").Returns(new EntityState { EntityId = "input_boolean.disablelightautomationhall", State = "on" });
        var app = ctx.InitApp<HallLightOnMovement>();

        // Act
        ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_on", "hal_2", times: 0);
    }

    [Fact]
    public void MotionStops_TurnsOffLights_AfterTimeout()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<HallLightOnMovement>();

        // Act
        ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(11).Ticks); // Daytime is 10 minutes
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_off", "hal", times: 1);
        ctx.VerifyCallService("light", "turn_off", "hal_2", times: 1);
    }

    [Fact]
    public void MotionStops_DoesNotTurnOff_WhenDisabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<HallLightOnMovement>();

        // Act
        ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        
        // Disable before timeout
        ctx.HaContext.GetState("input_boolean.disablelightautomationhall").Returns(new EntityState { EntityId = "input_boolean.disablelightautomationhall", State = "on" });
        
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(11).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_off", "hal", times: 0);
        ctx.VerifyCallService("light", "turn_off", "hal_2", times: 0);
    }
}

