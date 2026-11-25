using System.Text.Json;
using Automation.apps.Rooms.LivingRoom;
using FluentAssertions;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class SoundbarTests
{
    private static EntityState State(string entityId, string state, string? source = null) => new()
    {
        EntityId = entityId,
        State = state,
        AttributesJson = source == null ? null : JsonSerializer.SerializeToElement(new { source })
    };

    private static void Set(AppTestContext ctx, string entityId, string state, string? source = null)
        => ctx.HaContext.GetState(entityId).Returns(State(entityId, state, source));

    private static void Emit(AppTestContext ctx, string entityId, EntityState old, EntityState @new)
    {
        ctx.HaContext.GetState(entityId).Returns(@new);
        ctx.HaContextMock.StateChangeSubject.OnNext(new StateChange(new Entity(ctx.HaContext, entityId), old, @new));
        ctx.HaContextMock.ProcessPendingOperations();
    }

    private static void Wait(AppTestContext ctx, TimeSpan time)
    {
        ctx.AdvanceTimeBy(time.Ticks);
        ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(50);
    }

    /// <summary>The soundbar, TV and PS5 as they are before anything happens; the TV is off and the PS5 is not home.</summary>
    private static AppTestContext Setup(string soundbar = "on", string? soundbarSource = "digital", string disabled = "off")
    {
        var ctx = AppTestContext.NewWithScheduler();
        Set(ctx, "input_boolean.disablelightautomationlivingroom", disabled);
        Set(ctx, "media_player.soundbar", soundbar, soundbarSource);
        Set(ctx, "media_player.tv", "off");
        Set(ctx, "media_player.google_tv", "off");
        Set(ctx, "device_tracker.sony", "not_home");
        ctx.InitApp<Soundbar>();
        return ctx;
    }

    /// <summary>The soundbar turns on (by itself over HDMI-CEC, or because it was asked to) on the input it was left on.</summary>
    private static void SoundbarTurnsOn(AppTestContext ctx, string leftOn)
    {
        Emit(ctx, "media_player.soundbar", State("media_player.soundbar", "off"), State("media_player.soundbar", "on", leftOn));
        Wait(ctx, Soundbar.PowerOnDelay);
    }

    /// <summary>The TV turns on and its SmartThings entity reports an input.</summary>
    private static void TvTurnsOn(AppTestContext ctx, string tvSource)
    {
        Emit(ctx, "media_player.google_tv", State("media_player.google_tv", "off"), State("media_player.google_tv", "on", tvSource));
        Emit(ctx, "media_player.tv", State("media_player.tv", "off"), State("media_player.tv", "on"));
    }

    private static void Ps5TurnsOn(AppTestContext ctx)
        => Emit(ctx, "device_tracker.sony", State("device_tracker.sony", "not_home"), State("device_tracker.sony", "home"));

    private static void VerifySelected(AppTestContext ctx, string input) =>
        ctx.VerifyCallServiceWithData("media_player", "select_source", "soundbar", new MediaPlayerSelectSourceParameters { Source = input });

    [Fact]
    public void SoundbarTurnsOn_SetsTheVolumeOnce()
    {
        // Arrange
        var ctx = Setup(soundbar: "off", soundbarSource: null);

        // Act
        SoundbarTurnsOn(ctx, "digital");

        // Assert
        ctx.VerifyCallServiceWithData("media_player", "volume_set", "soundbar", new MediaPlayerVolumeSetParameters { VolumeLevel = Soundbar.StartVolume });
    }

    [Fact]
    public void TvAndPs5TurnOnAfterwards_DoNotSetTheVolumeAgain()
    {
        // Arrange - the soundbar is on and got its volume; the TV and the PS5 then come on (they used to set it again)
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        SoundbarTurnsOn(ctx, "digital");

        // Act
        TvTurnsOn(ctx, "hdmi3");
        Ps5TurnsOn(ctx);

        // Assert
        ctx.VerifyCallService("media_player", "volume_set", "soundbar", times: 1);
    }

    [Fact]
    public void SoundbarComesBackFromUnavailable_DoesNotResetTheVolume()
    {
        // Arrange - NetDaemon or Home Assistant restarts while someone is listening
        var ctx = Setup(soundbar: "unavailable", soundbarSource: null);

        // Act
        Emit(ctx, "media_player.soundbar", State("media_player.soundbar", "unavailable"), State("media_player.soundbar", "on", "hdmi1"));
        Wait(ctx, Soundbar.PowerOnDelay);

        // Assert
        ctx.VerifyNotCallService("media_player.volume_set");
        ctx.VerifyNotCallService("media_player.select_source");
    }

    [Fact]
    public void SoundbarWakesWithTheTv_OnItsOwnInput_SelectsTvArc()
    {
        // Arrange - the soundbar woke over HDMI-CEC, on HDMI 1 where gaming left it; a moment later the TV is reported as on
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        SoundbarTurnsOn(ctx, "hdmi1");

        // Act
        TvTurnsOn(ctx, "digital_tv");

        // Assert - at once, nothing was waited for
        VerifySelected(ctx, Soundbar.TvInput);
        ctx.VerifyCallService("media_player", "select_source", "soundbar", times: 1);
    }

    [Fact]
    public void SoundbarWakesWithTheTv_OnAnHdmiInput_SelectsTheExternalDevice()
    {
        // Arrange - the Google TV streamer or the PS5 woke the TV: it shows them on HDMI
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        SoundbarTurnsOn(ctx, "digital");

        // Act
        TvTurnsOn(ctx, "hdmi3");

        // Assert
        VerifySelected(ctx, Soundbar.ExternalInput);
    }

    [Fact]
    public void SoundbarWakesWithTheTv_OnTheRightInput_SelectsNothing()
    {
        // Arrange
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        SoundbarTurnsOn(ctx, "digital");

        // Act
        TvTurnsOn(ctx, "digital_tv");

        // Assert
        ctx.VerifyNotCallService("media_player.select_source");
        ctx.VerifyNotCallService("media_player.turn_on");
    }

    [Fact]
    public void TvTurnsOn_WithoutAnInputReported_DoesNothing()
    {
        // Arrange
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        SoundbarTurnsOn(ctx, "hdmi1");

        // Act
        Emit(ctx, "media_player.tv", State("media_player.tv", "off"), State("media_player.tv", "on"));

        // Assert
        ctx.VerifyNotCallService("media_player.select_source");
    }

    [Fact]
    public void SoundbarIsOnAlready_IsLeftAlone()
    {
        // Arrange - it was on before the TV came on, set by hand
        var ctx = Setup(soundbarSource: "hdmi1");

        // Act
        TvTurnsOn(ctx, "digital_tv");
        Ps5TurnsOn(ctx);

        // Assert
        ctx.VerifyNotCallService("media_player.select_source");
        ctx.VerifyNotCallService("media_player.turn_on");
        ctx.VerifyNotCallService("media_player.volume_set");
    }

    [Fact]
    public void SoundbarWasTurnedOnAWhileAgo_IsLeftAlone()
    {
        // Arrange - it came on, and some time later someone put it on the input they wanted
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        SoundbarTurnsOn(ctx, "digital");
        Wait(ctx, Soundbar.FreshPowerOn + TimeSpan.FromMinutes(1));

        // Act
        TvTurnsOn(ctx, "hdmi3");

        // Assert
        ctx.VerifyNotCallService("media_player.select_source");
    }

    [Fact]
    public void ChangedInput_OnlyCountsTheStateAndTheSource()
    {
        // The volume and the other attributes of the TV change all the time; a choice made in the SmartThings app must not be undone by that
        var tv = new EntityState { EntityId = "media_player.google_tv", State = "on", AttributesJson = JsonSerializer.SerializeToElement(new { source = "digital_tv", volume_level = 0.1 }) };
        var louder = new EntityState { EntityId = "media_player.google_tv", State = "on", AttributesJson = JsonSerializer.SerializeToElement(new { source = "digital_tv", volume_level = 0.2 }) };
        var hdmi = new EntityState { EntityId = "media_player.google_tv", State = "on", AttributesJson = JsonSerializer.SerializeToElement(new { source = "hdmi3", volume_level = 0.1 }) };
        var off = new EntityState { EntityId = "media_player.google_tv", State = "off" };

        Soundbar.ChangedInput(tv, louder).Should().BeFalse();
        Soundbar.ChangedInput(tv, hdmi).Should().BeTrue();
        Soundbar.ChangedInput(tv, off).Should().BeTrue();
        Soundbar.ChangedInput(off, tv).Should().BeTrue();
    }

    [Fact]
    public void Ps5TurnsOn_WithTheTvOff_SelectsTheExternalDevice()
    {
        // Arrange - the soundbar woke with the PS5, on the input of the TV
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        Set(ctx, "device_tracker.sony", "home");
        SoundbarTurnsOn(ctx, "digital");

        // Act
        Ps5TurnsOn(ctx);

        // Assert
        VerifySelected(ctx, Soundbar.ExternalInput);
    }

    [Fact]
    public void Ps5ReconnectsToWifi_WhileTheTvIsOnItsOwnInput_KeepsTheTvInput()
    {
        // Arrange - the PS5 drops off the network for half a minute while someone watches TV
        var ctx = Setup(soundbar: "off", soundbarSource: null);
        SoundbarTurnsOn(ctx, "digital");
        Set(ctx, "media_player.tv", "on");
        Set(ctx, "media_player.google_tv", "on", "digital_tv");
        Set(ctx, "device_tracker.sony", "home");

        // Act
        Ps5TurnsOn(ctx);

        // Assert
        ctx.VerifyNotCallService("media_player.select_source");
    }

    [Fact]
    public void SoundbarIsOff_IsTurnedOnFirstAndThenGetsTheInput()
    {
        // Arrange - HDMI-CEC did not wake the soundbar
        var ctx = Setup(soundbar: "off", soundbarSource: null);

        // Act
        TvTurnsOn(ctx, "hdmi3");

        // Assert - turned on once through SmartThings (not the cast device), no input yet
        ctx.VerifyCallService("media_player", "turn_on", "soundbar", times: 1);
        ctx.VerifyCallService("media_player", "turn_on", "av_soundbar", times: 0);
        ctx.VerifyNotCallService("media_player.select_source");

        // Act - it comes on
        SoundbarTurnsOn(ctx, "digital");

        // Assert
        VerifySelected(ctx, Soundbar.ExternalInput);
        ctx.VerifyCallService("media_player", "volume_set", "soundbar", times: 1);
    }

    [Fact]
    public void LightAutomationsDisabled_DoesNothing()
    {
        // Arrange
        var ctx = Setup(soundbar: "off", soundbarSource: null, disabled: "on");

        // Act
        SoundbarTurnsOn(ctx, "hdmi1");
        TvTurnsOn(ctx, "digital_tv");

        // Assert
        ctx.VerifyNotCallService("media_player.select_source");
        ctx.VerifyNotCallService("media_player.volume_set");
        ctx.VerifyNotCallService("media_player.turn_on");
    }
}
