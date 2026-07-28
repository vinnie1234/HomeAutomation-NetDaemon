using System.Reactive.Subjects;
using System.Text.Json;
using Automation.apps.Rooms.BathRoom;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Interfaces;
using Automation.Models;
using HomeAssistantGenerated;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class BathRoomLightsTests
{
    private void SetupDefaultStates(AppTestContext ctx)
    {
        ctx.HaContext.GetState("input_boolean.disablelightautomationbathroom").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.douchen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { State = "Day" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_number.bathroomlightnighttime").Returns(new EntityState { State = "2" });
        ctx.HaContext.GetState("input_number.bathroomlightdaytime").Returns(new EntityState { State = "5" });
        ctx.HaContext.GetState("binary_sensor.badkamer_motion").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("sensor.smart_series_4000_97ae_toothbrush_state").Returns(new EntityState { State = "idle" });
        
        // For IsOfficeDay
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.guest").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.officedaymonday").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.officedaytuesday").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.officedaywednesday").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.officedaythursday").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.officedayfriday").Returns(new EntityState { State = "off" });
    }

    [Fact]
    public void MotionOn_TurnsOnLights_WhenAutomationEnabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig { SpotifyRadioNlUrl = "spotify:radio" });

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("binary_sensor.badkamer_motion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_on", "plafond_badkamer");
        ctx.VerifyCallService("light", "turn_on", "badkamer_spiegel");
    }

    [Fact]
    public void MotionOn_DoesNothing_WhenAutomationDisabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablelightautomationbathroom").Returns(new EntityState { State = "on" });
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("binary_sensor.badkamer_motion").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("light.turn_on");
    }

    [Fact]
    public void MotionOff_TurnsOffLights_DuringDaytime()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act - motion goes off and stays off for 5 minutes (bathroomlightdaytime)
        ctx.ChangeStateFor("binary_sensor.badkamer_motion").FromState("on").ToState("off");
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(5).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_off", "plafond_badkamer");
        ctx.VerifyCallService("light", "turn_off", "badkamer_spiegel");
    }

    [Fact]
    public void MotionOff_TurnsOffLights_DuringNighttime()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { State = "Night" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "on" }); // IsNightMode = true
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act - motion goes off and stays off for 2 minutes (bathroomlightnighttime)
        ctx.ChangeStateFor("binary_sensor.badkamer_motion").FromState("on").ToState("off");
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(2).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_off", "plafond_badkamer");
        ctx.VerifyCallService("light", "turn_off", "badkamer_spiegel");
    }

    [Fact]
    public void Douchen_TurnsOn_ActivatesDouchingAutomation()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig { SpotifyRadioNlUrl = "spotify:radio" });

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("input_boolean.douchen").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("media_player", "volume_set", "googlehome0351");
        spotcast.Received(1).PlaySpotify(Arg.Any<MediaPlayerEntity>(), "spotify:radio");
        ctx.VerifyCallService("light", "turn_on", "badkamer_spiegel");
        ctx.VerifyCallService("light", "turn_on", "plafond_badkamer");
        ctx.VerifyCallService("cover", "close_cover", "rollerblind_0003");
        ctx.VerifyCallServiceWithData("tts", "cloud_say", null, new TtsCloudSayParameters { EntityId = "media_player.hele_huis", Message = "Tijd om te douchen" }); 
    }
    
    [Fact]
    public void Douchen_TurnsOn_DuringNightMode_DoesNotPlayMusic()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());
        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");
        ctx.ChangeStateFor("input_boolean.douchen").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
        ctx.VerifyCallService("light", "turn_on", "badkamer_spiegel");
    }

    [Fact]
    public void Douchen_TurnsOff_CompletesDouchingSequence()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("input_boolean.douchen").FromState("on").ToState("off");
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(3).Ticks); // To trigger lights turning off
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("cover", "open_cover", "rollerblind_0003");
        ctx.VerifyCallService("light", "turn_on", "plafond");
        ctx.VerifyCallService("media_player", "media_pause", "googlehome0351");
        ctx.VerifyCallServiceWithData("tts", "cloud_say", null, new TtsCloudSayParameters { EntityId = "media_player.hele_huis", Message = "Klaar met douchen" });
        
        // After 3 minutes
        ctx.VerifyCallService("light", "turn_off", "badkamer_spiegel");
        ctx.VerifyCallService("light", "turn_off", "plafond_badkamer");
    }

    [Fact]
    public void Douchen_RemainsOnFor1Hour_StopsMediaAndTurnsOff()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.douchen").Returns(new EntityState { State = "on" }); // Remains on
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("input_boolean.douchen").FromState("off").ToState("on"); // Start douching
        ctx.AdvanceTimeBy(TimeSpan.FromHours(1).Ticks); // 1 hour passes
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("media_player", "media_stop", "googlehome0351");
        ctx.VerifyCallService("input_boolean", "turn_off", "douchen");
        // We know it notifies but let's just assume we hit the logic.
    }

    [Fact]
    public void Toothbrush_TurnsOn_PlaysMusic_WhenNotNightModeAndNotDouching()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig { SpotifyRadioNlUrl = "spotify:radio" });

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("sensor.smart_series_4000_97ae_toothbrush_state").FromState("idle").ToState("running");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("media_player", "volume_set", "googlehome0351");
        spotcast.Received(1).PlaySpotify(Arg.Any<MediaPlayerEntity>(), "spotify:radio");
        ctx.VerifyCallService("media_player", "media_play", "googlehome0351");
    }

    [Fact]
    public void Toothbrush_TurnsOff_StopsMusicAndTurnsOnBedroomLight_After30Seconds()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        ctx.ChangeStateFor("sensor.smart_series_4000_97ae_toothbrush_state").FromState("running").ToState("idle");
        ctx.AdvanceTimeBy(TimeSpan.FromSeconds(30).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("media_player", "media_stop", "googlehome0351");
        ctx.VerifyCallService("light", "turn_on", "slaapkamer");
    }

    [Fact]
    public void HueSwitch_Button1_TurnsOffLights()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var spotcast = Substitute.For<ISpotcast>();
        var config = Options.Create(new AppConfig());

        var app = ctx.InitApp<BathRoomLights>(spotcast, config);

        // Act
        var eventSubject = new Subject<Event>();
        ctx.HaContextMock.HaContext.Events.Returns(eventSubject);

        var eventModel = new EventModel { DeviceId = "3dcab87acc97379282b359fdf3557a52", Type = "initial_press", Subtype = 1 };
        var jsonElement = JsonSerializer.SerializeToElement(eventModel);
        
        // Let's re-init because Events subscription happens in constructor
        var app2 = ctx.InitApp<BathRoomLights>(spotcast, config);
        
        eventSubject.OnNext(new NetDaemon.HassModel.Event
        {
            EventType = "hue_event",
            DataElement = jsonElement
        });

        // Assert
        ctx.VerifyCallService("light", "turn_off", "badkamer_spiegel");
        ctx.VerifyCallService("light", "turn_off", "plafond_badkamer");
    }
}




