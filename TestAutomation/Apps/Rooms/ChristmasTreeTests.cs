using Automation.apps.Rooms.LivingRoom;
using HomeAssistantGenerated;
using Automation.Configuration;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class ChristmasTreeTests
{
    private const string Playlist = "https://open.spotify.com/playlist/kerst";

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void Start(string playlist = Playlist) =>
        _ctx.InitApp<ChristmasTree>(_ctx.Spotcast, Options.Create(new AppConfig { SpotifyKerstUrl = playlist }));

    private void Change(string entityId, string from, string to)
    {
        _ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    [Fact]
    public void WhenVincentGetsUp_TheTreeGoesOn()
    {
        Start();

        Change("input_boolean.sleepingvincent", "on", "off");

        _ctx.VerifyCallService("light", "turn_on", "festavia_string_lights_1", times: 1);
    }

    [Fact]
    public void WhenVincentGoesToSleep_TheTreeGoesOff()
    {
        Start();

        Change("input_boolean.sleepingvincent", "off", "on");

        _ctx.VerifyCallService("light", "turn_off", "festavia_string_lights_1", times: 1);
    }

    [Fact]
    public void TheMusicButton_PlaysTheChristmasPlaylistOnTheSoundbar()
    {
        Start();

        Change(ChristmasTree.MusicButton, "unknown", "2026-11-09T10:00:00+00:00");

        _ctx.Spotcast.Received(1).PlaySpotify(Arg.Is<MediaPlayerEntity>(m => m.EntityId == ChristmasTree.Soundbar), Playlist);
    }

    [Fact]
    public void TheMusicButton_WithoutAPlaylistInTheConfig_PlaysNothing()
    {
        Start(playlist: "");

        Change(ChristmasTree.MusicButton, "unknown", "2026-11-09T10:00:00+00:00");

        _ctx.Spotcast.DidNotReceiveWithAnyArgs().PlaySpotify(null!, null!);
    }
}
