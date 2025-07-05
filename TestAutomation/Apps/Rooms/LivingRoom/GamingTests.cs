using Automation.apps.Rooms.LivingRoom;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms.LivingRoom;

public class GamingTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldSetupGamingEnvironmentWhenSonyDeviceTurnsOn()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationlivingroom", "off");
        _ = _ctx.InitApp<Gaming>();

        // Act
        _ctx.ChangeStateFor("device_tracker.sony")
            .FromState("not_home")
            .ToState("home");

        // Assert — should turn on TV and soundbar for gaming.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("media_player", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
                Arg.Any<object>());
        }, nameof(ShouldSetupGamingEnvironmentWhenSonyDeviceTurnsOn));
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("media_player", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.av_soundbar")), 
                Arg.Any<object>());
        }, nameof(ShouldSetupGamingEnvironmentWhenSonyDeviceTurnsOn));
    }

    [Fact]
    public void ShouldSelectCorrectTvSourceWhenGaming()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationlivingroom", "off");
        _ = _ctx.InitApp<Gaming>();

        // Act
        _ctx.ChangeStateFor("device_tracker.sony")
            .FromState("not_home")
            .ToState("home");

        // Assert — should select HDMI2 source on TV.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("media_player", "select_source", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
                Arg.Any<object>());
        }, nameof(ShouldSelectCorrectTvSourceWhenGaming));
    }

    [Fact]
    public void ShouldTurnOffLivingRoomLightsWhenGaming()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationlivingroom", "off");
        _ = _ctx.InitApp<Gaming>();

        // Act
        _ctx.ChangeStateFor("device_tracker.sony")
            .FromState("not_home")
            .ToState("home");

        // Assert — should turn off living room lights for gaming ambiance.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.plafond_woonkamer")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffLivingRoomLightsWhenGaming));
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.plafond")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffLivingRoomLightsWhenGaming));
    }

    [Fact]
    public void ShouldSetTvVolumeWhenGaming()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationlivingroom", "off");
        _ = _ctx.InitApp<Gaming>();

        // Act
        _ctx.ChangeStateFor("device_tracker.sony")
            .FromState("not_home")
            .ToState("home");

        // Assert — should set TV volume to gaming level.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("media_player", "volume_set", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
                Arg.Any<object>());
        }, nameof(ShouldSetTvVolumeWhenGaming));
    }

    [Fact]
    public void ShouldNotSetupGamingWhenLightAutomationDisabled()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationlivingroom", "on");
        _ = _ctx.InitApp<Gaming>();

        // Act
        _ctx.ChangeStateFor("device_tracker.sony")
            .FromState("not_home")
            .ToState("home");

        // Assert — should not setup gaming when light automation is disabled.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("light", "turn_off", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldNotSetupGamingWhenLightAutomationDisabled));
    }
}