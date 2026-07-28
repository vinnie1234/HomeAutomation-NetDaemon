using Automation.apps.General;
using Automation.Configuration;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

/// <summary>
/// Tests for the AwayManager state machine app.
/// Note: ExecuteWelcomeHomeSequenceAsync runs via Task.Run, making async verification challenging.
/// Tests focus on state transitions and synchronous behavior verification.
/// </summary>
public class AwayManagerTests
{
    private static AppTestContext SetupContext(string houseMode = "Day")
    {
        var ctx = AppTestContext.NewWithScheduler();
        
        ctx.HaContext.GetState("input_boolean.away").Returns(new EntityState { EntityId = "input_boolean.away", State = "off" });
        ctx.HaContext.GetState("binary_sensor.gangmotion").Returns(new EntityState { EntityId = "binary_sensor.gangmotion", State = "off" });
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = houseMode });
        
        ctx.HaContext.GetState("person.vincentmaarschalkerweerd").Returns(new EntityState { EntityId = "person.vincentmaarschalkerweerd", State = "home" });
        ctx.HaContext.GetState("person.carleen").Returns(new EntityState { EntityId = "person.carleen", State = "home" });
        ctx.HaContext.GetState("device_tracker.carleenmobiel").Returns(new EntityState { EntityId = "device_tracker.carleenmobiel", State = "home" });
        
        ctx.HaContext.GetState("sensor.zedarfoodstoragestatus").Returns(new EntityState { EntityId = "sensor.zedarfoodstoragestatus", State = "full" });
        
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        
        return ctx;
    }

    private static AwayManager CreateApp(AppTestContext ctx)
    {
        return ctx.InitApp<AwayManager>(Options.Create(new AppConfiguration()));
    }

    #region State Machine - Away Trigger

    [Fact]
    public void Away_TransitionsToAway_WhenAwayTurnsOn()
    {
        // Arrange
        var ctx = SetupContext();
        CreateApp(ctx);

        // Act — turn away on
        ctx.ChangeStateFor("input_boolean.away").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        
        // Assert — motion in Away state should not trigger any scene (stays in Away)
        ctx.ChangeStateFor("binary_sensor.gangmotion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(300);
        
        // No scene should be called because motion in Away doesn't trigger welcome home
        ctx.VerifyNotCallService("scene.turn_on");
        ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");
    }

    [Fact]
    public void MotionDetected_DoesNothing_WhenInHomeState()
    {
        // Arrange
        var ctx = SetupContext();
        CreateApp(ctx);
        
        // Act — motion while in Home state
        ctx.ChangeStateFor("binary_sensor.gangmotion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(300);

        // Assert — nothing happens
        ctx.VerifyNotCallService("scene.turn_on");
        ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");
    }

    [Fact]
    public void MotionDetected_DoesNothing_WhenInAwayState()
    {
        // Arrange
        var ctx = SetupContext();
        CreateApp(ctx);

        // Go to Away
        ctx.ChangeStateFor("input_boolean.away").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        
        // Act — motion while Away
        ctx.ChangeStateFor("binary_sensor.gangmotion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(300);

        // Assert
        ctx.VerifyNotCallService("scene.turn_on");
    }

    [Fact]
    public void InvalidTransition_IsRejected_HomeToReturning()
    {
        // Arrange — start from Home
        var ctx = SetupContext();
        CreateApp(ctx);
        
        // Try to trigger Returning by turning away off while already off
        // This should be rejected since Home -> Returning is invalid
        ctx.ChangeStateFor("input_boolean.away").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        
        ctx.ChangeStateFor("binary_sensor.gangmotion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(300);

        // Assert — no welcome home
        ctx.VerifyNotCallService("scene.turn_on");
    }

    #endregion

    [Fact]
    public void WelcomeHome_ExecuteSequence_CallsServices()
    {
        // Arrange
        var ctx = SetupContext(houseMode: "Day");
        var app = CreateApp(ctx);

        // Act - Call ExecuteWelcomeHomeSequenceAsync via reflection
        var method = typeof(AwayManager).GetMethod("ExecuteWelcomeHomeSequenceAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method.Invoke(app, null);
        task.Wait();

        // Let the immediate actions run
        ctx.HaContextMock.ProcessPendingOperations();

        // Advance time for the scheduled NotifyHouse call
        ctx.AdvanceTimeBy(TimeSpan.FromSeconds(20).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        var calls = ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService")
            .ToList();
        
        Assert.True(calls.Count > 0, $"Expected CallService calls after welcome home sequence, but got {calls.Count}. Total calls: {ctx.HaContext.ReceivedCalls().Count()}");
    }

    [Fact]
    public void FullStateMachineFlow_NoWelcomeInAwayState()
    {
        // Arrange
        var ctx = SetupContext();
        CreateApp(ctx);

        // Step 1: Home -> Away
        ctx.ChangeStateFor("input_boolean.away").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Step 2: Motion in Away — no CallService
        ctx.ChangeStateFor("binary_sensor.gangmotion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(300);
        
        var callsAfterAwayMotion = ctx.HaContext.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == "CallService");
        Assert.Equal(0, callsAfterAwayMotion);
    }
}

