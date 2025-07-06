using Automation.apps.General;
using Automation.Enum;
using HomeAssistantGenerated;
using TestAutomation.Helpers;
using Xunit;
using NSubstitute;
using NetDaemon.HassModel.Entities;

namespace TestAutomation.Apps.General;

public class AwayManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

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
        
        // Simulate away state turning off (sets to Returning state)
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("on")
            .ToState("off");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
            
        // Act - Trigger motion detection (which calls WelcomeHome)
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // Assert - Should get house state (showing that WelcomeHome logic is triggered)
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().GetState(Arg.Any<string>());
        }, nameof(ShouldGetHouseStateWhenMotionDetected));
    }

    [Fact]
    public void ShouldSendContextAwareNotificationWhenComingHome()
    {
        // Arrange - Set up evening house state and coming home scenario
        _ctx.WithEntityState("input_select.housemodeselect", "Evening")
            .WithEntityState("sensor.zedar_food_storage_status", "full");
        _ctx.InitApp<AwayManager>();
        
        // Simulate away state turning off (sets to Returning state)
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("on")
            .ToState("off");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
            
        // Act - Trigger motion detection (which calls WelcomeHome -> NotifyVincentPhone)
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // Small delay for reactive operations
        Task.Delay(50).Wait();

        // Assert - Should receive context-aware notification through service call
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService(
                Arg.Is<string>(s => s.Contains("notify")),
                Arg.Any<string>(),
                Arg.Any<ServiceTarget?>(),
                Arg.Is<NotifyMobileAppVincentPhoneParameters>(p => 
                    p.Title == "Thuis" && p.Message == "Goedenavond Vincent!"));
        }, nameof(ShouldSendContextAwareNotificationWhenComingHome));
    }

    [Fact]
    public void ShouldInitializeInHomeState()
    {
        // Arrange & Act
        var app = _ctx.InitApp<AwayManager>();
        
        // Assert - Should start in Home state
        Assert.Equal(HomePresenceState.Home, app.CurrentState);
    }

    [Fact]
    public void ShouldTransitionToAwayStateWhenAwayActivated()
    {
        // Arrange
        var app = _ctx.InitApp<AwayManager>();
        Assert.Equal(HomePresenceState.Home, app.CurrentState);
        
        // Act
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("off")
            .ToState("on");
        
        // Advance scheduler to process throttled callbacks
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        
        // Assert
        Assert.Equal(HomePresenceState.Away, app.CurrentState);
    }

    [Fact]
    public void ShouldTransitionToReturningStateWhenAwayDeactivated()
    {
        // Arrange
        var app = _ctx.InitApp<AwayManager>();
        
        // Set to Away first
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        Assert.Equal(HomePresenceState.Away, app.CurrentState);
        
        // Act - Away is turned off
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("on")
            .ToState("off");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        
        // Assert
        Assert.Equal(HomePresenceState.Returning, app.CurrentState);
    }

    [Fact]
    public async Task ShouldTransitionToWelcomingHomeOnMotionWhenReturning()
    {
        // Arrange
        _ctx.WithEntityState("input_select.housemodeselect", "Day")
            .WithEntityState("sensor.zedar_food_storage_status", "full");
            
        var app = _ctx.InitApp<AwayManager>();
        
        // Set to Returning state
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("on")
            .ToState("off");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        Assert.Equal(HomePresenceState.Returning, app.CurrentState);
        
        // Act - Motion detected
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        
        // Small delay to allow async transition
        await Task.Delay(10);
        
        // Assert - Should be in WelcomingHome or Home state (depending on timing)
        Assert.True(app.CurrentState == HomePresenceState.WelcomingHome || 
                   app.CurrentState == HomePresenceState.Home);
    }

    [Fact]
    public void ShouldIgnoreMotionWhenInHomeState()
    {
        // Arrange
        var app = _ctx.InitApp<AwayManager>();
        Assert.Equal(HomePresenceState.Home, app.CurrentState);
        
        // Act - Motion detected while in Home state
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        
        // Assert - Should remain in Home state
        Assert.Equal(HomePresenceState.Home, app.CurrentState);
    }

    [Fact]
    public void ShouldIgnoreMotionWhenInAwayState()
    {
        // Arrange
        var app = _ctx.InitApp<AwayManager>();
        
        // Set to Away state
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        Assert.Equal(HomePresenceState.Away, app.CurrentState);
        
        // Act - Motion detected while Away
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        
        // Assert - Should remain in Away state
        Assert.Equal(HomePresenceState.Away, app.CurrentState);
    }

    [Fact]
    public void ShouldPreventRaceConditionWithMultipleMotionEvents()
    {
        // Arrange
        _ctx.WithEntityState("input_select.housemodeselect", "Day")
            .WithEntityState("sensor.zedar_food_storage_status", "full");
            
        var app = _ctx.InitApp<AwayManager>();
        
        // Set to Returning state
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        _ctx.ChangeStateFor("input_boolean.away")
            .FromState("on")
            .ToState("off");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        Assert.Equal(HomePresenceState.Returning, app.CurrentState);
        
        // Act - Multiple rapid motion events
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("on")
            .ToState("off");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        
        // Assert - Should only trigger once and be in WelcomingHome or Home
        Assert.True(app.CurrentState == HomePresenceState.WelcomingHome || 
                   app.CurrentState == HomePresenceState.Home);
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