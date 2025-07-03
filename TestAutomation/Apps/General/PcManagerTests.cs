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
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.bureau")), 
            Arg.Any<object>());
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.plafond")), 
            Arg.Any<object>());
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
        _ctx.HaContext.Received().CallService("light", "turn_off", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.nachtkastje")), 
            Arg.Any<object>());
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
        _ctx.HaContext.Received().CallService("media_player", "turn_off", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
            Arg.Any<object>());
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
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.bureau")), 
            Arg.Any<object>());
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.plafond")), 
            Arg.Any<object>());
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
        _ctx.HaContext.Received().CallService("light", "turn_off", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.nachtkastje")), 
            Arg.Any<object>());
        _ctx.HaContext.Received().CallService("media_player", "turn_off", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
            Arg.Any<object>());
    }
}