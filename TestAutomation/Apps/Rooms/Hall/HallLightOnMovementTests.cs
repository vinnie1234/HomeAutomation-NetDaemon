using Automation.apps.Rooms.Hall;
using Automation.Models;
using TestAutomation.Helpers;
using Xunit;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using System.Text.Json;
using HomeAssistantGenerated;
using NetDaemon.Client.HomeAssistant.Model;

namespace TestAutomation.Apps.Rooms.Hall;

public class HallLightOnMovementTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    private void TriggerHueEvent(EventModel eventModel)
    {
        _ctx.HaContextMock.EventSubject.OnNext(new HassEvent
        {
            EventType = "hue_event",
            DataElement = JsonSerializer.SerializeToElement(eventModel)
        });
    }

    [Fact]
    public void ShouldEnableLightsHallWhenStartFriends()
    {
        // Arrange
        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "4339833970e35ff10c568a94b59e50dd",
            Type = "initial_press",
            Subtype = 4
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal"), 
                Arg.Any<object>());
        }, nameof(ShouldEnableLightsHallWhenStartFriends));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("switch", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "switch.bot29ff"), 
                Arg.Any<object>());
        }, nameof(ShouldEnableLightsHallWhenStartFriends));
    }

    [Fact]
    public void ShouldTurnOnLightWhenMotionDetectedAndNotSleeping()
    {
        // Arrange
        _ctx.WithEntityState("person.vincent_maarschalkerweerd", "home")
            .WithEntityState("input_boolean.sleeping", "off")
            .WithEntityState("input_boolean.disablelightautomationhall", "off");

        _ = _ctx.InitApp<HallLightOnMovement>();

        // Act
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");

        // Assert — should turn on both lights with full brightness when not sleeping
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal2"), 
                Arg.Is<LightTurnOnParameters>(p => p.BrightnessPct != null && Math.Abs((double)p.BrightnessPct - 100.0) < 0.01 && p.Transition != null && Math.Abs((double)p.Transition - 15.0) < 0.01));
        }, nameof(ShouldTurnOnLightWhenMotionDetectedAndNotSleeping));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOnLightWhenMotionDetectedAndNotSleeping));
    }

    [Fact]
    public void ShouldTurnOnLightWithLowBrightnessWhenMotionDetectedAndSleeping()
    {
        // Arrange
        _ctx.WithEntityState("person.vincent_maarschalkerweerd", "home")
            .WithEntityState("input_boolean.sleeping", "on")
            .WithEntityState("input_boolean.disablelightautomationhall", "off");

        _ = _ctx.InitApp<HallLightOnMovement>();

        // Act
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");

        // Assert — should turn on hal2 only with low brightness when sleeping
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal2"), 
                Arg.Is<LightTurnOnParameters>(p => p.BrightnessPct != null && Math.Abs((double)p.BrightnessPct - 5.0) < 0.01 && p.Transition != null && Math.Abs((double)p.Transition - 15.0) < 0.01));
        }, nameof(ShouldTurnOnLightWithLowBrightnessWhenMotionDetectedAndSleeping));
            
        // Should not turn on hal light when sleeping
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOnLightWithLowBrightnessWhenMotionDetectedAndSleeping));
    }

    [Fact]
    public void ShouldNotTurnOnLightWhenMotionDetectedButAutomationDisabled()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationhall", "on");

        _ = _ctx.InitApp<HallLightOnMovement>();

        // Act
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");

        // Assert — no lights should turn on when automation is disabled
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("light", "turn_on", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldNotTurnOnLightWhenMotionDetectedButAutomationDisabled));
    }

    [Fact]
    public void ShouldTurnOffLightsAfterMotionTimeoutWhenNotSleeping()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "off")
            .WithEntityState("input_number.halllightdaytime", 5.0)
            .WithEntityState("input_boolean.disablelightautomationhall", "off");

        _ = _ctx.InitApp<HallLightOnMovement>();

        // Act - motion turns off and stays off for specified time
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("on")
            .ToState("off");

        // Advance time by the timeout period (5 minutes)
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromMinutes(5).Ticks);

        // Assert — both lights should turn off
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffLightsAfterMotionTimeoutWhenNotSleeping));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal2"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffLightsAfterMotionTimeoutWhenNotSleeping));
    }

    [Fact]
    public void ShouldTurnOffLightsAfterMotionTimeoutWhenSleeping()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "on")
            .WithEntityState("input_number.halllightnighttime", 2.0)
            .WithEntityState("input_boolean.disablelightautomationhall", "off");

        _ = _ctx.InitApp<HallLightOnMovement>();

        // Act - motion turns off and stays off for specified time
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("on")
            .ToState("off");

        // Advance time by the night timeout period (2 minutes)
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromMinutes(2).Ticks);

        // Assert — both lights should turn off
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffLightsAfterMotionTimeoutWhenSleeping));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal2"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffLightsAfterMotionTimeoutWhenSleeping));
    }

    [Fact]
    public void ShouldNotTurnOffLightsIfAutomationDisabledDuringTimeout()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "off")
            .WithEntityState("input_number.halllightdaytime", 5.0)
            .WithEntityState("input_boolean.disablelightautomationhall", "off");

        _ = _ctx.InitApp<HallLightOnMovement>();

        // Act - motion turns off
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("on")
            .ToState("off");

        // Disable automation before timeout
        _ctx.ChangeStateFor("input_boolean.disablelightautomationhall")
            .FromState("off")
            .ToState("on");

        // Advance time by the timeout period
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromMinutes(5).Ticks);

        // Assert — lights should not turn off when automation is disabled
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("light", "turn_off", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldNotTurnOffLightsIfAutomationDisabledDuringTimeout));
    }

    [Fact]
    public void ShouldToggleAwayStateWhenHueButton1PressedAndVincentHome()
    {
        // Arrange
        
        _ctx.WithEntityState("person.vincent_maarschalkerweerd", "home")
            .WithEntityState("input_boolean.away", "off");

        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "4339833970e35ff10c568a94b59e50dd",
            Type = "initial_press",
            Subtype = 1
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert — should turn on away mode when Vincent is home
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_boolean", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_boolean.away"), 
                Arg.Any<object>());
        }, nameof(ShouldToggleAwayStateWhenHueButton1PressedAndVincentHome));
    }

    [Fact]
    public void ShouldToggleAwayStateWhenHueButton1PressedAndVincentAway()
    {
        // Arrange
        
        _ctx.WithEntityState("person.vincent_maarschalkerweerd", "not_home")
            .WithEntityState("input_boolean.away", "on");

        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "4339833970e35ff10c568a94b59e50dd",
            Type = "initial_press",
            Subtype = 1
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert — should turn off away mode when Vincent is not home
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_boolean", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_boolean.away"), 
                Arg.Any<object>());
        }, nameof(ShouldToggleAwayStateWhenHueButton1PressedAndVincentAway));
    }

    [Fact]
    public void ShouldIncreaseBrightnessWhenHueButton2Pressed()
    {
        // Arrange
        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "4339833970e35ff10c568a94b59e50dd",
            Type = "initial_press",
            Subtype = 2
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert — should increase brightness by 10%
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal2"), 
                Arg.Is<LightTurnOnParameters>(p => p.BrightnessStepPct != null && Math.Abs((double)p.BrightnessStepPct - 10.0) < 0.01));
        }, nameof(ShouldIncreaseBrightnessWhenHueButton2Pressed));
    }

    [Fact]
    public void ShouldDecreaseBrightnessWhenHueButton3Pressed()
    {
        // Arrange
        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "4339833970e35ff10c568a94b59e50dd",
            Type = "initial_press",
            Subtype = 3
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert — should decrease brightness by 10%
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal2"), 
                Arg.Is<LightTurnOnParameters>(p => p.BrightnessStepPct != null && Math.Abs((double)p.BrightnessStepPct - (-10.0)) < 0.01));
        }, nameof(ShouldDecreaseBrightnessWhenHueButton3Pressed));
    }

    [Fact]
    public void ShouldStartFriendsThemeWhenHueButton4Pressed()
    {
        // Arrange
        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "4339833970e35ff10c568a94b59e50dd",
            Type = "initial_press",
            Subtype = 4
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert — should set volume, turn on lights, turn off hal2, and play music
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("media_player", "volume_set", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "media_player.friends_speakers"), 
                Arg.Is<MediaPlayerVolumeSetParameters>(p => p.VolumeLevel != null && Math.Abs((double)p.VolumeLevel - 0.5) < 0.01));
        }, nameof(ShouldStartFriendsThemeWhenHueButton4Pressed));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal"), 
                Arg.Any<object>());
        }, nameof(ShouldStartFriendsThemeWhenHueButton4Pressed));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("switch", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "switch.bot29ff"), 
                Arg.Any<object>());
        }, nameof(ShouldStartFriendsThemeWhenHueButton4Pressed));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("light", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "light.hal2"), 
                Arg.Any<object>());
        }, nameof(ShouldStartFriendsThemeWhenHueButton4Pressed));
    }

    [Fact]
    public void ShouldIgnoreHueEventsFromOtherDevices()
    {
        // Arrange
        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "different_device_id",
            Type = "initial_press",
            Subtype = 1
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert — should not react to events from other devices
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService(Arg.Any<string>(), Arg.Any<string>(), 
                Arg.Any<ServiceTarget>(), Arg.Any<object>());
        }, nameof(ShouldIgnoreHueEventsFromOtherDevices));
    }

    [Fact]
    public void ShouldIgnoreNonInitialPressHueEvents()
    {
        // Arrange
        _ = _ctx.InitApp<HallLightOnMovement>();
        
        var eventModel = new EventModel
        {
            DeviceId = "4339833970e35ff10c568a94b59e50dd",
            Type = "release",
            Subtype = 1
        };

        // Act
        TriggerHueEvent(eventModel);

        // Assert — should not react to non-initial_press events
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService(Arg.Any<string>(), Arg.Any<string>(), 
                Arg.Any<ServiceTarget>(), Arg.Any<object>());
        }, nameof(ShouldIgnoreNonInitialPressHueEvents));
    }

    [Fact]
    public void ShouldTurnOnBotSwitchWhenHalLightIsOffAndNotSleeping()
    {
        // Arrange
        _ctx.WithEntityState("person.vincent_maarschalkerweerd", "home")
            .WithEntityState("input_boolean.sleeping", "off")
            .WithEntityState("input_boolean.disablelightautomationhall", "off")
            .WithEntityState("light.hal", "off");

        _ = _ctx.InitApp<HallLightOnMovement>();

        // Act
        _ctx.ChangeStateFor("binary_sensor.gang_motion")
            .FromState("off")
            .ToState("on");

        // Assert — should turn on bot switch when hal light is off
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("switch", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "switch.bot29ff"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOnBotSwitchWhenHalLightIsOffAndNotSleeping));
    }
}