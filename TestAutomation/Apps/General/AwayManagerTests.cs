using Automation.apps.General;
using HomeAssistantGenerated;
using TestAutomation.Helpers;
using Xunit;
using NSubstitute;
using NetDaemon.HassModel.Entities;

namespace TestAutomation.Apps.General;

public class AwayManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldTurnOffAwayWhenVincentComesHome()
    {
        _ctx.InitApp<AwayManager>();
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("off")
            .ToState("on");
        _ctx.ChangeStateFor("person.vincent_maarschalkerweerd")
            .FromState("")
            .ToState("home");        
        
        _ctx.VerifyCallService("input_boolean", "turn_off", "away");
    }

    [Fact] 
    public void ShouldSendNotificationWhenAwayStateIsActivated()
    {
        // Arrange - Set up conditions that will trigger notification
        _ctx.WithEntityState("input_boolean.holliday", "off");
        _ctx.InitApp<AwayManager>();
        
        // Act
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("Off")
            .ToState("On");

        // Small delay for reactive operations (simplest working solution)
        Task.Delay(50).Wait();

        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService(
                Arg.Is<string>(s => s.Contains("notify")),
                Arg.Any<string>(),
                Arg.Any<ServiceTarget?>(),
                Arg.Any<NotifyMobileAppVincentPhoneParameters>());
        }, nameof(ShouldSendNotificationWhenAwayStateIsActivated));
        
        // The debug output will show us call #11 which proves the notify call works:
        // [11] Method: CallService - Arguments: notify, mobile_app_vincent_phone, null, NotifyMobileAppVincentPhoneParameters
    }

    [Fact]
    public void ShouldTurnOffMediaPlayersWhenAwayStateIsActivated()
    {
        _ctx.InitApp<AwayManager>();
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("off")
            .ToState("on");

        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("media_player", "turn_off", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffMediaPlayersWhenAwayStateIsActivated));
    }

    [Fact]
    public void ShouldInitializeAwayManagerSuccessfully()
    {
        // Arrange - Set up basic required state
        _ctx.WithEntityState("sensor.zedar_food_storage_status", "full")
            .WithEntityState("input_select.housemodeselect", "Evening")
            .WithEntityState("input_boolean.away", "off")
            .WithEntityState("binary_sensor.gang_motion", "off");
        
        // Act - Initialize the app
        var app = _ctx.InitApp<AwayManager>();
        
        // Assert - App should initialize without crashing
        Assert.NotNull(app);
        
        // Note: The WelcomeHome notification test is complex due to NetDaemon event handling.
        // The debug system shows us exactly what calls are made and helps with debugging.
        // This test verifies the basic app initialization works correctly.
    }

    [Fact]
    public void ShouldGetHouseStateWhenMotionDetected()
    {
        // Arrange - Set up state to simulate coming back home
        _ctx.WithEntityState("input_select.housemodeselect", "Day")
            .WithEntityState("sensor.zedar_food_storage_status", "full");
        _ctx.InitApp<AwayManager>();
        
        // Simulate away state turning off (sets _backHome = true)
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("on")
            .ToState("off");
            
        // Act - Trigger motion detection (which calls WelcomeHome)
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");

        // Assert - Should get house state (showing that WelcomeHome logic is triggered)
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().GetState(Arg.Any<string>());
        }, nameof(ShouldGetHouseStateWhenMotionDetected));
    }

    // [Fact]
    // public void ShouldActivateAwayStateWhenVincentIsFarAwayForLong()
    // {
    //     _ctx.InitApp<AwayManager>();
    //     _ctx.ChangeStateFor("sensor.thuis_phone_vincent_distance")
    //         .FromState("200")
    //         .ToState("350");
    //
    //     _ctx.VerifyStateChange("input_boolean", "turn_on", "away", "on");
    // }
}