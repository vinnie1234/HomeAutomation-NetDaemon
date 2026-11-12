using Automation.apps;
using Automation.apps.General.Assistants;
using NetDaemon.HassModel.Entities;
using NetDaemon.HassModel;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class MusicFollowMeTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void State(string entityId, string state, string? source = null)
    {
        System.Text.Json.JsonElement? attributes = source == null ? null : System.Text.Json.JsonDocument.Parse($"{{\"source\":\"{source}\"}}").RootElement;
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state, AttributesJson = attributes });
    }

    private void Start(bool followOn = true, bool laptopOn = true, bool musicAssistant = false, bool sleeping = false, bool away = false,
        bool carleenHome = false)
    {
        State(MusicFollowMe.FollowSwitch, followOn ? "on" : "off");
        State(MusicFollowMe.LaptopSwitch, laptopOn ? "on" : "off");
        State(Spotcast.UseMusicAssistant, musicAssistant ? "on" : "off");
        State("input_boolean.sleepingvincent", sleeping ? "on" : "off");
        State("input_boolean.sleepingcarleen", "off");
        State("input_boolean.away", away ? "on" : "off");
        State("input_boolean.awayvincent", "off");
        State("input_boolean.awaycarleen", carleenHome ? "off" : "on");
        State("input_boolean.datenight", "off");
        State("media_player.tv", "off");
        _ctx.InitApp<MusicFollowMe>();
    }

    private void Change(string entityId, string from, string to)
    {
        _ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    [Fact]
    public void MotionInTheBathroom_WhileTheMusicPlaysInTheLivingRoom_MovesTheMusicWithSpotifyConnect()
    {
        Start();
        State("media_player.woonkamer", "playing");

        Change("binary_sensor.badkamer_beweging", "off", "on");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 1);
    }

    [Fact]
    public void MotionInTheBathroom_WithMusicAssistant_MovesTheQueue()
    {
        Start(musicAssistant: true);
        State("media_player.woonkamer_woonkamer", "playing");

        Change("binary_sensor.badkamer_beweging", "off", "on");

        _ctx.HaContext.Received(1).CallService("music_assistant", "transfer_queue",
            Arg.Is<ServiceTarget>(t => t.EntityIds != null && t.EntityIds.Contains("media_player.badkamer_badkamer_speaker")), Arg.Any<object?>());
    }

    [Fact]
    public void Motion_WhenNothingPlays_DoesNothing()
    {
        Start();

        Change("binary_sensor.badkamer_beweging", "off", "on");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 0);
    }

    [Fact]
    public void Motion_WhenTheSwitchIsOff_DoesNothing()
    {
        Start(followOn: false);
        State("media_player.woonkamer", "playing");

        Change("binary_sensor.badkamer_beweging", "off", "on");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 0);
    }

    [Fact]
    public void Motion_WhileSomeoneSleeps_DoesNothing()
    {
        Start(sleeping: true);
        State("media_player.woonkamer", "playing");

        Change("binary_sensor.badkamer_beweging", "off", "on");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 0);
    }

    [Fact]
    public void Motion_WhenNobodyIsHome_DoesNothing()
    {
        Start(away: true);
        State("media_player.woonkamer", "playing");

        Change("binary_sensor.badkamer_beweging", "off", "on");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 0);
    }

    [Fact]
    public void WhenTheLaptopLocks_AndTheMusicPlaysOnIt_TheMusicGoesOnInTheKitchen()
    {
        Start();
        State(MusicFollowMe.SpotifyState, "playing", MusicFollowMe.LaptopDevice);

        Change("binary_sensor.werk_laptop_session_locked", "off", "on");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 1);
    }

    [Fact]
    public void WhenTheLaptopLocks_AndTheMusicPlaysElsewhere_NothingHappens()
    {
        Start();
        State(MusicFollowMe.SpotifyState, "playing", "Woonkamer");

        Change("binary_sensor.werk_laptop_session_locked", "off", "on");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 0);
    }

    [Fact]
    public void WhenTheLaptopUnlocks_TheMusicGoesBackToTheLaptop()
    {
        Start();
        State(MusicFollowMe.SpotifyState, "playing", MusicFollowMe.LaptopDevice);
        Change("binary_sensor.werk_laptop_session_locked", "off", "on");
        State("media_player.nestmini9818", "playing");

        Change("binary_sensor.werk_laptop_session_locked", "on", "off");

        _ctx.VerifyCallService("media_player", "select_source", "spotifyplus_vincent_maarschalkerweerd", times: 2);
    }

    [Fact]
    public void WhenCarleenComesHome_TheMusicNoLongerFollows()
    {
        Start();
        State("input_boolean.awaycarleen", "off");

        Change("input_boolean.awaycarleen", "on", "off");

        _ctx.VerifyCallService("input_boolean", "turn_off", "muziek_volgt_mij", times: 1);
    }
}

public class SpotcastRoutingTests
{
    [Theory]
    [InlineData("https://open.spotify.com/playlist/1HLh1SsPFrBOQLta0y2FFl?si=d658e2e608e54c58", "spotify://playlist/1HLh1SsPFrBOQLta0y2FFl")]
    [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", "spotify://playlist/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("library://playlist/26", "library://playlist/26")]
    public void ASpotifyLink_BecomesAMusicAssistantUri(string link, string expected) =>
        Assert.Equal(expected, Spotcast.ToMusicAssistantUri(link));

    [Fact]
    public void ASpotifyLink_WithAnInstance_UsesThatAccount() =>
        Assert.Equal("spotify--abc123://playlist/XYZ", Spotcast.ToMusicAssistantUri("https://open.spotify.com/playlist/XYZ", "spotify--abc123"));

    [Fact]
    public void TheFirstSpotifyAccountIsVincents_TheSecondIsCarleens_YouTubeMusicIsVincents()
    {
        var found = MusicAssistantAccounts.InstancesIn(["spotify--aaa://playlist/1", "spotify--bbb://playlist/2", "ytmusic--ccc://playlist/3", "library://playlist/4"]);

        var fill = MusicAssistantAccounts.Assign(found, null, null, null);

        Assert.Equal("spotify--aaa", fill[MusicAssistantAccounts.SpotifyVincent]);
        Assert.Equal("spotify--bbb", fill[MusicAssistantAccounts.SpotifyCarleen]);
        Assert.Equal("ytmusic--ccc", fill[MusicAssistantAccounts.YouTubeVincent]);
    }

    [Fact]
    public void WhatIsFilledInStays_AndANewAccountBecomesCarleens()
    {
        var found = MusicAssistantAccounts.InstancesIn(["spotify--aaa://playlist/1", "spotify--bbb://playlist/2"]);

        var fill = MusicAssistantAccounts.Assign(found, "spotify--aaa", null, "ytmusic--ccc");

        Assert.Single(fill);
        Assert.Equal("spotify--bbb", fill[MusicAssistantAccounts.SpotifyCarleen]);
    }
}
