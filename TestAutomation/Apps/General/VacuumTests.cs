using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace TestAutomation.Apps.General;

public class VacuumTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldCleanKitchenWhenButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("input_button.vacuumcleankitchen")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        _ctx.HaContext.Received(1).CallService("vacuum", "send_command", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldCleanLivingRoomWhenButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("input_button.vacuumcleanlivingroom")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        _ctx.HaContext.Received(1).CallService("vacuum", "send_command", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldCleanBedroomWhenButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("input_button.vacuumcleanbedroom")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        _ctx.HaContext.Received(1).CallService("vacuum", "send_command", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldCleanLitterBoxAreaAfterCatUsage()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "off");
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("pet_into");

        // Assert — should trigger litter box area cleaning when not sleeping.
        _ctx.HaContext.Received().CallService("vacuum", "send_command", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldNotCleanWhenSleeping()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "on");
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("pet_into");

        // Assert — should not clean when sleeping.
        _ctx.HaContext.DidNotReceive().CallService("vacuum", "send_command", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }
}