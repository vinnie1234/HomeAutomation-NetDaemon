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

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    private void Start(string playlist = Playlist, string tree = "off")
    {
        SetState(ChristmasTree.Tree, tree);
        _ctx.InitApp<ChristmasTree>(_ctx.Spotcast, Options.Create(new AppConfig { SpotifyKerstUrl = playlist }));
    }

    private void Change(string entityId, string from, string to)
    {
        _ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    [Theory]
    [InlineData(2026, 12, 5, false)]
    [InlineData(2026, 12, 6, true)]
    [InlineData(2027, 1, 6, true)]
    [InlineData(2027, 1, 7, false)]
    [InlineData(2026, 7, 1, false)]
    public void TheChristmasPeriod_RunsFromSixDecemberUpToAndIncludingSixJanuary(int year, int month, int day, bool expected) =>
        Assert.Equal(expected, ChristmasTree.InPeriod(new DateTime(year, month, day)));

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
    public void WhenSomethingElseSwitchesTheTreeOff_AndVincentIsAwake_ItGoesOnAgain()
    {
        SetState("input_boolean.sleepingvincent", "off");
        Start(tree: "on");

        SetState(ChristmasTree.Tree, "off");
        Change(ChristmasTree.Tree, "on", "off");
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(3).Ticks);

        _ctx.VerifyCallService("light", "turn_on", "festavia_string_lights_1", times: 1);
    }

    [Fact]
    public void WhenTheTreeGoesOff_WhileVincentSleeps_ItStaysOff()
    {
        SetState("input_boolean.sleepingvincent", "on");
        Start(tree: "on");

        SetState(ChristmasTree.Tree, "off");
        Change(ChristmasTree.Tree, "on", "off");
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(3).Ticks);

        _ctx.HaContext.DidNotReceive().CallService("light", "turn_on", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("unknown")]
    public void WhenTheLampsAreGone_NothingIsSwitched(string state)
    {
        Start(tree: state);

        Change("input_boolean.sleepingvincent", "on", "off");
        Change("input_boolean.sleepingvincent", "off", "on");

        _ctx.HaContext.DidNotReceive().CallService("light", Arg.Any<string>(), Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void TheMusicButton_PlaysTheChristmasPlaylistOnTheKitchenSpeaker()
    {
        Start();

        Change(ChristmasTree.MusicButton, "unknown", "2026-11-09T10:00:00+00:00");

        _ctx.Spotcast.Received(1).PlaySpotify(Arg.Is<MediaPlayerEntity>(m => m.EntityId == ChristmasTree.Speaker), Playlist);
    }

    [Fact]
    public void TheChristmasButton_StartsTheChristmasSceneOfTheLivingRoom()
    {
        Start();

        Change(ChristmasTree.MusicButton, "unknown", "2026-11-09T10:00:00+00:00");

        _ctx.VerifyCallService("scene", "turn_on", "woonkamer_woonkamer_sneeuwschittering", times: 1);
    }

    [Fact]
    public void TheChristmasButton_WithoutAPlaylist_StillStartsTheScene()
    {
        Start(playlist: "");

        Change(ChristmasTree.MusicButton, "unknown", "2026-11-09T10:00:00+00:00");

        _ctx.VerifyCallService("scene", "turn_on", "woonkamer_woonkamer_sneeuwschittering", times: 1);
    }

    [Fact]
    public void TheMusicButton_WithoutAPlaylistInTheConfig_PlaysNothing()
    {
        Start(playlist: "");

        Change(ChristmasTree.MusicButton, "unknown", "2026-11-09T10:00:00+00:00");

        _ctx.Spotcast.DidNotReceiveWithAnyArgs().PlaySpotify(null!, null!);
    }
}
