using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class PcManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldTurnOnLightsWhenStartPcButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<PcManager>();

        // Act
        _ctx.ChangeStateFor("input_button.start_pc")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert — should turn on bureau and plafond lights.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.bureau")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOnLightsWhenStartPcButtonPressed));
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.plafond")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOnLightsWhenStartPcButtonPressed));
    }

    [Fact]
    public void ShouldTurnOffNachtkastjeWhenStartPcButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<PcManager>();

        // Act
        _ctx.ChangeStateFor("input_button.start_pc")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert — should turn off nachtkastje light.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.nachtkastje")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffNachtkastjeWhenStartPcButtonPressed));
    }

    [Fact]
    public void ShouldTurnOffTvWhenStartPcButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<PcManager>();

        // Act
        _ctx.ChangeStateFor("input_button.start_pc")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert — should turn off TV.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("media_player", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffTvWhenStartPcButtonPressed));
    }

    [Fact]
    public void ShouldTurnOnLightsWhenPcLastStartedSensorChanges()
    {
        // Arrange
        _ = _ctx.InitApp<PcManager>();

        // Act
        _ctx.ChangeStateFor("sensor.vincent_pc_laatst_opgestart")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert — should turn on bureau and plafond lights.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.bureau")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOnLightsWhenPcLastStartedSensorChanges));
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.plafond")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOnLightsWhenPcLastStartedSensorChanges));
    }

    [Fact]
    public void ShouldTurnOffDevicesWhenPcLastStartedSensorChanges()
    {
        // Arrange
        _ = _ctx.InitApp<PcManager>();

        // Act
        _ctx.ChangeStateFor("sensor.vincent_pc_laatst_opgestart")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert — should turn off nachtkastje light and TV.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.nachtkastje")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffDevicesWhenPcLastStartedSensorChanges));
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("media_player", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffDevicesWhenPcLastStartedSensorChanges));
    }
}