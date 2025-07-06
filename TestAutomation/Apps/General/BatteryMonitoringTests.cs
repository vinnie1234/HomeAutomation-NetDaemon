using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace TestAutomation.Apps.General;

public class BatteryMonitoringTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldSendNotificationWhenBatteryIsLow()
    {
        // Arrange
        _ = _ctx.InitApp<BatteryMonitoring>();

        // Simulate a battery sensor with low battery (15%)
        _ctx.ChangeStateFor("sensor.test_battery")
            .FromState("50")
            .ToState("15");

        // Let 10 hours pass to trigger the notification
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(10).Ticks);

        // Assert — should send notification for low battery.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("notify", Arg.Any<string>(), 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldSendNotificationWhenBatteryIsLow));
    }

    [Fact]
    public void ShouldNotSendNotificationForHighBattery()
    {
        // Arrange
        _ = _ctx.InitApp<BatteryMonitoring>();

        // Act - battery is above 20% threshold
        _ctx.ChangeStateFor("sensor.test_battery")
            .FromState("15")
            .ToState("80");

        // Assert — should not send notification for high battery.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("notify", Arg.Any<string>(), 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldNotSendNotificationForHighBattery));
    }

    [Fact]
    public void ShouldThrottleNotificationsForSameDevice()
    {
        // Arrange
        _ = _ctx.InitApp<BatteryMonitoring>();

        // Act - trigger low battery notification
        _ctx.ChangeStateFor("sensor.test_battery")
            .FromState("50")
            .ToState("15");

        _ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(10).Ticks);

        // Act again - should not send another notification within 7 days
        _ctx.ChangeStateFor("sensor.test_battery")
            .FromState("20")
            .ToState("10");

        _ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(10).Ticks);

        // Assert — should throttle notifications within 7 days.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("notify", Arg.Any<string>(), 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldThrottleNotificationsForSameDevice));
    }

    [Fact]
    public void ShouldResetThrottlingWhenBatteryIsFullyCharged()
    {
        // Arrange
        _ = _ctx.InitApp<BatteryMonitoring>();

        // Act - trigger low battery, then charge to 100%
        _ctx.ChangeStateFor("sensor.test_battery")
            .FromState("50")
            .ToState("15");

        _ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(10).Ticks);

        // Charge to 100%
        _ctx.ChangeStateFor("sensor.test_battery")
            .FromState("15")
            .ToState("100");

        // Drop to low again
        _ctx.ChangeStateFor("sensor.test_battery")
            .FromState("100")
            .ToState("15");

        _ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(10).Ticks);

        // Assert — should send notification again after full charge reset.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(2).CallService("notify", Arg.Any<string>(), 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldResetThrottlingWhenBatteryIsFullyCharged));
    }
}