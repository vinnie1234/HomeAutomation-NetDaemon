using Automation.apps.Rooms.LivingRoom;
using Automation.Models;
using NetDaemon.Client.HomeAssistant.Model;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using System.Text.Json;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms.LivingRoom;

public class LivingRoomLightsTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    private void TriggerHueEvent(string deviceId, string type, int subtype)
    {
        var eventModel = new EventModel
        {
            DeviceId = deviceId,
            Type = type,
            Subtype = subtype
        };
        
        _ctx.HaContextMock.EventSubject.OnNext(new HassEvent
        {
            EventType = "hue_event",
            DataElement = JsonSerializer.SerializeToElement(eventModel)
        });
    }

    [Fact]
    public void ShouldTurnOnLightsWhenHueFilamentBulbIsOffAndWallSwitchPressed()
    {
        // Arrange
        _ctx.WithEntityState("light.hue_filament_bulb_2", "off");
        _ = _ctx.InitApp<LivingRoomLights>();

        // Act
        TriggerHueEvent("b4784a8e43cc6f5aabfb6895f3a8dbac", "initial_press", 1);

        // Assert — should turn on living room lights when wall switch is pressed and lights are off.
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldTurnOffLightsWhenHueFilamentBulbIsOnAndWallSwitchPressed()
    {
        // Arrange
        _ctx.WithEntityState("light.hue_filament_bulb_2", "on");
        _ = _ctx.InitApp<LivingRoomLights>();

        // Act
        TriggerHueEvent("b4784a8e43cc6f5aabfb6895f3a8dbac", "initial_press", 1);

        // Assert — should turn off living room lights when wall switch is pressed and lights are on.
        _ctx.HaContext.Received().CallService("light", "turn_off", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldTriggerLightAutomationOnHouseModeChange()
    {
        // Arrange
        _ctx.WithEntityState("light.hue_filament_bulb_2", "on");
        _ = _ctx.InitApp<LivingRoomLights>();

        // Act
        _ctx.ChangeStateFor("input_select.housemodeselect")
            .FromState("Day")
            .ToState("Evening");

        // Assert — should trigger light automation when house mode changes and filament bulb is on.
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldNotTriggerLightAutomationWhenFilamentBulbIsOff()
    {
        // Arrange
        _ctx.WithEntityState("light.hue_filament_bulb_2", "off");
        _ = _ctx.InitApp<LivingRoomLights>();

        // Act
        _ctx.ChangeStateFor("input_select.housemodeselect")
            .FromState("Day")
            .ToState("Evening");

        // Assert — should not trigger light automation when filament bulb is off.
        _ctx.HaContext.DidNotReceive().CallService("light", "turn_on", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldFixLightColorsAfterFilamentBulbTurnsOn()
    {
        // Arrange
        _ = _ctx.InitApp<LivingRoomLights>();

        // Act
        _ctx.ChangeStateFor("light.hue_filament_bulb_2")
            .FromState("off")
            .ToState("on");

        // Assert — should fix light colors after filament bulb turns on by scheduling delayed action.
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }
}