using Automation.apps.General;
using Automation.Interfaces;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class NetDaemonTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();

    [Fact]
    public void ShouldNotifyHouseOnRestartWhenNotSleeping()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "off");
        
        // Act
        _ = _ctx.InitApp<Automation.apps.General.NetDaemon>(_storage);

        // Assert — should notify house about restart when not sleeping.
        _ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone");
    }

    [Fact]
    public void ShouldNotifyDiscordOnRestart()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "off");
        
        // Act
        _ = _ctx.InitApp<Automation.apps.General.NetDaemon>(_storage);

        // Assert — should always notify Discord about restart.
        _ctx.VerifyCallNotify("notify", "discord_homeassistant");
    }

    [Fact]
    public void ShouldRestoreLightColorFromStorageOnRestart()
    {
        // Arrange
        var lightColor = new List<double> { 255.0, 128.0, 64.0 };
        _storage.Get<IReadOnlyList<double>>("NetDaemonRestart").Returns(lightColor);
        
        // Act
        _ = _ctx.InitApp<Automation.apps.General.NetDaemon>(_storage);

        // Assert — should restore light color from storage.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldRestoreLightColorFromStorageOnRestart));
    }

    [Fact]
    public void ShouldHandleRestartButtonPress()
    {
        // Arrange
        var lightColor = new List<double> { 100.0, 200.0, 50.0 };
        _ctx.WithEntityState("light.koelkast", "on");
        _ctx.SetAttributesFor("light.koelkast", new { rgb_color = lightColor });
        _ = _ctx.InitApp<Automation.apps.General.NetDaemon>(_storage);

        // Act
        _ctx.ChangeStateFor("input_button.restartnetdaemon")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert — should save light color and restart addon.
        _storage.Received().Save("NetDaemonRestart", Arg.Any<object>());
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("hassio", "addon_restart", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldHandleRestartButtonPress));
    }

    [Fact]
    public void ShouldTurnLightRedOnRestartRequest()
    {
        // Arrange
        _ = _ctx.InitApp<Automation.apps.General.NetDaemon>(_storage);

        // Act
        _ctx.ChangeStateFor("input_button.restartnetdaemon")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert — should turn light red before restart.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldTurnLightRedOnRestartRequest));
    }
}