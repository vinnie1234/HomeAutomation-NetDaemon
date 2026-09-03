using Automation.apps.Rooms.Kitchen;
using Automation.Configuration;
using HomeAssistantGenerated;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class KitchenMusicOnMotionTests
{
    private const string Motion = "binary_sensor.motionwoonkamer";
    private const string KitchenPlayer = "media_player.nestmini9818";
    private const string SpotifyPlayer = "media_player.spotify_vincent_maarschalkerweerd";
    private const string Tv = "media_player.tv";
    private const string AwayVincent = "input_boolean.awayvincent";
    private const string Working = "input_boolean.working";
    private const string SleepingVincent = "input_boolean.sleepingvincent";
    private const string SleepingCarleen = "input_boolean.sleepingcarleen";
    private const string AwayCarleen = "input_boolean.awaycarleen";

    /// <summary>
    /// Creates a context with all guards satisfied: Vincent home, not working, Spotify idle,
    /// TV off, nobody sleeping, kitchen speaker idle.
    /// </summary>
    private static AppTestContext ArrangeAllGreen()
    {
        var ctx = AppTestContext.NewWithScheduler();
        ctx.WithEntityState(AwayVincent, "off")   // Vincent is home
            .WithEntityState(Working, "off")
            .WithEntityState(SpotifyPlayer, "idle")
            .WithEntityState(Tv, "off")
            .WithEntityState(SleepingVincent, "off")
            .WithEntityState(SleepingCarleen, "off")
            .WithEntityState(AwayCarleen, "on")    // Carleen away — her sleep state is irrelevant
            .WithEntityState(KitchenPlayer, "idle");
        return ctx;
    }

    private static void TriggerMotion(AppTestContext ctx)
    {
        ctx.ChangeStateFor(Motion).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public void Music_Starts_When_AllConditionsMet()
    {
        using var ctx = ArrangeAllGreen();
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.Received(1).PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    [Fact]
    public void Music_StartsOnCorrectSpeaker()
    {
        using var ctx = ArrangeAllGreen();
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.Received(1).PlaySpotify(
            Arg.Is<MediaPlayerEntity>(e => e.EntityId == KitchenPlayer),
            Arg.Any<string>());
    }

    // ── Guard: Vincent thuis ────────────────────────────────────────────────

    [Fact]
    public void Music_DoesNotStart_WhenVincentAway()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(AwayVincent, "on"); // Vincent away
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    // ── Guard: niet aan het werk ────────────────────────────────────────────

    [Fact]
    public void Music_DoesNotStart_WhenWorking()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(Working, "on");
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    // ── Guard: Spotify speelt niet ──────────────────────────────────────────

    [Fact]
    public void Music_DoesNotStart_WhenSpotifyAlreadyPlaying()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(SpotifyPlayer, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    // ── Guard: TV uit ───────────────────────────────────────────────────────

    [Fact]
    public void Music_DoesNotStart_WhenTvOn()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(Tv, "on");
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    [Fact]
    public void Music_DoesNotStart_WhenTvPlaying()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(Tv, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    // ── Guard: niemand slapend ──────────────────────────────────────────────

    [Fact]
    public void Music_DoesNotStart_WhenVincentSleeping()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(SleepingVincent, "on");
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    [Fact]
    public void Music_DoesNotStart_WhenCarleenHomeSleeping()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(AwayCarleen, "off");    // Carleen home
        ctx.WithEntityState(SleepingCarleen, "on"); // and sleeping
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    [Fact]
    public void Music_Starts_WhenCarleenAwaySleeping()
    {
        // Carleen is away — her sleep state must not block the music
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(AwayCarleen, "on");     // Carleen away
        ctx.WithEntityState(SleepingCarleen, "on"); // sleep flag set (stale/irrelevant)
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.Received(1).PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    // ── Guard: 15-minuten cooldown ──────────────────────────────────────────

    [Fact]
    public void Music_DoesNotStart_WhenRecentlyStopped()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        // Simulate the kitchen speaker stopping
        ctx.ChangeStateFor(KitchenPlayer).FromState("playing").ToState("idle");
        ctx.HaContextMock.ProcessPendingOperations();

        // Motion immediately after — cooldown must block playback
        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    [Fact]
    public void Music_Starts_WhenCooldownExpired()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        // Speaker stops → cooldown starts
        ctx.ChangeStateFor(KitchenPlayer).FromState("playing").ToState("idle");
        ctx.HaContextMock.ProcessPendingOperations();

        // Advance 15 minutes + 1 second → cooldown expired
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks + TimeSpan.FromSeconds(1).Ticks);

        // Kitchen speaker is now idle again
        ctx.WithEntityState(KitchenPlayer, "idle");

        TriggerMotion(ctx);

        ctx.Spotcast.Received(1).PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    // ── Guard: keuken speelt al ─────────────────────────────────────────────

    [Fact]
    public void Music_DoesNotStart_WhenKitchenAlreadyPlaying()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        TriggerMotion(ctx);

        ctx.Spotcast.DidNotReceive().PlaySpotify(Arg.Any<MediaPlayerEntity>(), Arg.Any<string>());
    }

    // ── Stop: away en TV ────────────────────────────────────────────────────

    [Fact]
    public void Music_Stops_WhenBothGoAway()
    {
        // Carleen already away (ArrangeAllGreen), Vincent goes away too → house empty
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        ctx.ChangeStateFor(AwayVincent).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 1);
    }

    [Fact]
    public void Music_DoesNotStop_WhenOnlyVincentAway_CarleenHome()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(AwayCarleen, "off"); // Carleen is home
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        ctx.ChangeStateFor(AwayVincent).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 0);
    }

    [Fact]
    public void Music_Stops_WhenCarleenGoesAway_AndVincentAlreadyAway()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(AwayVincent, "on");  // Vincent already away
        ctx.WithEntityState(AwayCarleen, "off"); // Carleen still home
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.InitApp<KitchenMusicOnMotion>();

        ctx.ChangeStateFor(AwayCarleen).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 1);
    }

    [Fact]
    public void Music_DoesNotStop_WhenBothGoAway_ButKitchenNotPlaying()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "idle");
        ctx.InitApp<KitchenMusicOnMotion>();

        ctx.ChangeStateFor(AwayVincent).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 0);
    }

    [Fact]
    public void Music_Stops_WhenTvTurnsOn()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.WithEntityState(Tv, "off");
        ctx.InitApp<KitchenMusicOnMotion>();

        ctx.ChangeStateFor(Tv).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 1);
    }

    [Fact]
    public void Music_Stops_WhenTvStartsPlaying()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "playing");
        ctx.WithEntityState(Tv, "idle");
        ctx.InitApp<KitchenMusicOnMotion>();

        ctx.ChangeStateFor(Tv).FromState("idle").ToState("playing");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 1);
    }

    [Fact]
    public void Music_DoesNotStop_WhenTvTurnsOn_ButKitchenNotPlaying()
    {
        using var ctx = ArrangeAllGreen();
        ctx.WithEntityState(KitchenPlayer, "idle");
        ctx.WithEntityState(Tv, "off");
        ctx.InitApp<KitchenMusicOnMotion>();

        ctx.ChangeStateFor(Tv).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 0);
    }
}
