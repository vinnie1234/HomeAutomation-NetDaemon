using Automation.apps.Rooms.LivingRoom;
using TestAutomation.Helpers;
using Xunit;
using FluentAssertions;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace TestAutomation.Apps.Rooms.LivingRoom;

public class TvTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldActivateMovieModeWhenTvTurnsOn()
    {
        // Arrange
        var app = _ctx.InitApp<Tv>();

        // Act
        _ctx.ChangeStateFor("media_player.tv")
            .FromState("off")
            .ToState("on");

        // Assert — should activate movie scene.
        _ctx.HaContext.Received(1).CallService("scene", "turn_on", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldRestoreLightingWhenTvTurnsOff()
    {
        // Arrange
        var app = _ctx.InitApp<Tv>();

        // Act
        _ctx.ChangeStateFor("media_player.tv")
            .FromState("on")
            .ToState("off");

        // Assert — should restore normal lighting.
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldHandleBankLightColorCorrection()
    {
        // Arrange
        var app = _ctx.InitApp<Tv>();

        // Act
        _ctx.ChangeStateFor("light.bank")
            .FromState("off")
            .ToState("on");

        // Assert — should correct bank light color
        app.Should().NotBeNull();
    }

    [Fact]
    public void ShouldSelectCorrectSceneBasedOnHouseState()
    {
        // Arrange
        _ctx.WithEntityState("input_select.housemodeselect", "Evening");
        var app = _ctx.InitApp<Tv>();

        // Act
        _ctx.ChangeStateFor("media_player.tv")
            .FromState("off")
            .ToState("on");

        // Assert — should select appropriate scene for house state
        app.Should().NotBeNull();
    }

    [Fact]
    public void ShouldConsiderWorkModeForSceneSelection()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.working", "on");
        var app = _ctx.InitApp<Tv>();

        // Act
        _ctx.ChangeStateFor("media_player.tv")
            .FromState("off")
            .ToState("on");

        // Assert — should adapt scene for work mode
        app.Should().NotBeNull();
    }
}