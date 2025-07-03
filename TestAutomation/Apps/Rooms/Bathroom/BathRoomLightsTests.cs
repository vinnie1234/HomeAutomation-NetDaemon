using Automation.apps.Rooms.BathRoom;
using Automation.Models;
using NetDaemon.Client.HomeAssistant.Model;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using System.Text.Json;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms.Bathroom;

public class BathRoomLightsTests
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
    public void ShouldTurnOnLightsWhenMotionDetected()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationbathroom", "off");
        _ = _ctx.InitApp<BathRoomLights>();

        // Act
        _ctx.ChangeStateFor("binary_sensor.badkamer_motion")
            .FromState("off")
            .ToState("on");

        // Assert — should turn on bathroom lights when motion is detected.
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.plafond_badkamer")), 
            Arg.Any<object>());
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.badkamer_spiegel")), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldNotTurnOnLightsWhenAutomationDisabled()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationbathroom", "on");
        _ = _ctx.InitApp<BathRoomLights>();

        // Act
        _ctx.ChangeStateFor("binary_sensor.badkamer_motion")
            .FromState("off")
            .ToState("on");

        // Assert — should not turn on lights when automation is disabled.
        _ctx.HaContext.DidNotReceive().CallService("light", "turn_on", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldStartShowerAutomationWhenDoubchenTurnsOn()
    {
        // Arrange
        _ = _ctx.InitApp<BathRoomLights>();

        // Act
        _ctx.ChangeStateFor("input_boolean.douchen")
            .FromState("off")
            .ToState("on");

        // Assert — should start shower automation with media and lights.
        _ctx.HaContext.Received().CallService("media_player", "volume_set", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.googlehome_0351")), 
            Arg.Any<object>());
        _ctx.HaContext.Received().CallService("light", "turn_on", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("light.badkamer_spiegel")), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldStopShowerAutomationWhenDoubchenTurnsOff()
    {
        // Arrange
        _ = _ctx.InitApp<BathRoomLights>();

        // Act
        _ctx.ChangeStateFor("input_boolean.douchen")
            .FromState("on")
            .ToState("off");

        // Assert — should stop shower automation and open cover.
        _ctx.HaContext.Received().CallService("cover", "open_cover", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("cover.rollerblind_0003")), 
            Arg.Any<object>());
        _ctx.HaContext.Received().CallService("media_player", "media_pause", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.googlehome_0351")), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldStartMusicWhenToothbrushBecomesActive()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.douchen", "off");
        _ = _ctx.InitApp<BathRoomLights>();

        // Act
        _ctx.ChangeStateFor("sensor.smart_series_400_097ae_toothbrush_state")
            .FromState("idle")
            .ToState("running");

        // Assert — should start music when toothbrush becomes active.
        _ctx.HaContext.Received().CallService("media_player", "volume_set", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.googlehome_0351")), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldToggleShowerModeOnHueButtonPress()
    {
        // Arrange
        _ = _ctx.InitApp<BathRoomLights>();

        // Act
        TriggerHueEvent("3dcab87acc97379282b359fdf3557a52", "initial_press", 4);

        // Assert — should toggle shower mode on button 4 press.
        _ctx.HaContext.Received().CallService("input_boolean", Arg.Any<string>(), 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("input_boolean.douchen")), 
            Arg.Any<object>());
    }
}