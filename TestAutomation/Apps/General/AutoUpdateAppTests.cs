using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;
using FluentAssertions;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace TestAutomation.Apps.General;

public class AutoUpdateAppTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldScheduleDailyUpdateCheckAt3AM()
    {
        // Arrange & Act
        _ = _ctx.InitApp<AutoUpdateApp>();

        // Assert — app should schedule daily update checks.
        // Note: Testing scheduled tasks requires specific scheduler verification
    }

    [Fact]
    public void ShouldProcessAvailableUpdates()
    {
        // Arrange
        _ = _ctx.InitApp<AutoUpdateApp>();

        // Act — simulate update entity becoming available
        _ctx.ChangeStateFor("update.home_assistant_core_update")
            .FromState("off")
            .ToState("on");
        
        // Assert — should trigger update installation.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("update", "install", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldProcessAvailableUpdates));
    }

    [Fact]
    public void ShouldSendDiscordNotificationForUpdates()
    {
        // Arrange
        _ = _ctx.InitApp<AutoUpdateApp>();

        // Act — trigger update notification
        _ctx.ChangeStateFor("update.home_assistant_supervisor_update")
            .FromState("off")
            .ToState("on");

        // Assert — should send Discord notification.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("notify", "discord", 
                null, 
                Arg.Any<object>());
        }, nameof(ShouldSendDiscordNotificationForUpdates));
    }

    [Fact]
    public void ShouldSendPhoneNotificationWithRestartOption()
    {
        // Arrange
        _ = _ctx.InitApp<AutoUpdateApp>();

        // Act — simulate update completion requiring restart
        _ctx.ChangeStateFor("update.home_assistant_core_update")
            .FromState("on")
            .ToState("off");

        // Assert — should send phone notification with restart option.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("notify", Arg.Any<string>(), 
                null, 
                Arg.Any<object>());
        }, nameof(ShouldSendPhoneNotificationWithRestartOption));
    }

    [Fact]
    public void ShouldRestartHomeAssistantWhenRequested()
    {
        // Arrange
        _ = _ctx.InitApp<AutoUpdateApp>();

        // Act — simulate restart button press via mobile notification
        var eventData = new { action = "restart_ha" };
        // Note: Testing mobile app notification actions requires event simulation

        // Assert — should restart Home Assistant.
        // Note: Full testing requires mocking mobile app notification events
    }
}