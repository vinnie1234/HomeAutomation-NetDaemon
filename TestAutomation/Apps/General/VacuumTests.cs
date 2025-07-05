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
    public void ShouldCleanBankWhenButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("input_button.vacuumcleanbank")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("vacuum", "send_command", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldCleanBankWhenButtonPressed));
    }

    [Fact]
    public void ShouldCleanLivingRoomWhenButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("input_button.vacuumcleanwoonkamer")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("vacuum", "send_command", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldCleanLivingRoomWhenButtonPressed));
    }

    [Fact]
    public void ShouldCleanBedroomWhenButtonPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("input_button.vacuumcleanslaapkamer")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("vacuum", "send_command", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldCleanBedroomWhenButtonPressed));
    }

    [Fact]
    public void ShouldCleanLitterBoxAreaAfterCatUsage()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "off")
            .WithEntityState("input_boolean.skipvaccumlitterbox", "off");
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("cleaning");

        // Assert — should trigger litter box area cleaning when not sleeping.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("vacuum", "send_command", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldCleanLitterBoxAreaAfterCatUsage));
    }

    [Fact]
    public void ShouldNotCleanWhenSleeping()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "on")
            .WithEntityState("input_boolean.skipvaccumlitterbox", "off");
        _ = _ctx.InitApp<Vacuum>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("cleaning");

        // Assert — should not clean when sleeping.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("vacuum", "send_command", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldNotCleanWhenSleeping));
    }
}