using Automation.Configuration;
using Automation.Helpers;
using FluentAssertions;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Helpers;

public class LivingRoomPresenceServiceTests
{
    private const string Motion = "binary_sensor.motionwoonkamer";
    private const string Tv = "media_player.tv";

    private static readonly TimeSpan GracePeriod = new PresenceConfiguration().LivingRoomGracePeriod;

    private static AppTestContext Arrange(string motionState, string tvState)
    {
        var ctx = AppTestContext.NewWithScheduler();
        ctx.WithEntityState(Motion, motionState)
            .WithEntityState(Tv, tvState);
        return ctx;
    }

    /// <summary>
    /// Records every occupancy change the service publishes, so tests can assert both what changed
    /// and what deliberately did not change.
    /// </summary>
    private static List<bool> Record(ILivingRoomPresenceService service)
    {
        var changes = new List<bool>();
        service.OccupiedChanged.Subscribe(changes.Add);
        return changes;
    }

    private static void ChangeState(AppTestContext ctx, string entityId, string from, string to)
    {
        ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        ctx.HaContextMock.ProcessPendingOperations();
    }

    private static void AdvanceMinutes(AppTestContext ctx, double minutes)
    {
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(minutes).Ticks);
    }

    [Fact]
    public void Room_IsOccupied_WhenMotionIsDetected()
    {
        using var ctx = Arrange(motionState: "off", tvState: "off");
        var service = ctx.LivingRoomPresenceService;
        var changes = Record(service);

        service.IsOccupied.Should().BeFalse();

        ChangeState(ctx, Motion, "off", "on");

        service.IsOccupied.Should().BeTrue();
        changes.Should().Equal(true);
    }

    /// <summary>
    /// The core of the plan: a motion sensor that stops seeing small movements must not empty the
    /// room while the TV is still playing.
    /// </summary>
    [Fact]
    public void Room_StaysOccupied_WhenMotionStops_ButTvIsStillPlaying()
    {
        using var ctx = Arrange(motionState: "on", tvState: "playing");
        var service = ctx.LivingRoomPresenceService;
        var changes = Record(service);

        service.IsOccupied.Should().BeTrue();

        ChangeState(ctx, Motion, "on", "off");
        AdvanceMinutes(ctx, GracePeriod.TotalMinutes * 2);

        service.IsOccupied.Should().BeTrue("the TV is still playing");
        changes.Should().BeEmpty();
    }

    [Fact]
    public void Room_BecomesEmpty_OnlyAfterTheGracePeriod_WhenAllAnchorsAreGone()
    {
        using var ctx = Arrange(motionState: "on", tvState: "off");
        var service = ctx.LivingRoomPresenceService;
        var changes = Record(service);

        ChangeState(ctx, Motion, "on", "off");

        AdvanceMinutes(ctx, GracePeriod.TotalMinutes - 1);
        service.IsOccupied.Should().BeTrue("the grace period has not elapsed yet");
        changes.Should().BeEmpty();

        AdvanceMinutes(ctx, 2);
        service.IsOccupied.Should().BeFalse();
        changes.Should().Equal(false);
    }

    [Fact]
    public void Room_BecomesEmpty_AfterTheGracePeriod_WhenTheTvIsTurnedOff()
    {
        using var ctx = Arrange(motionState: "off", tvState: "playing");
        var service = ctx.LivingRoomPresenceService;
        var changes = Record(service);

        service.IsOccupied.Should().BeTrue();

        ChangeState(ctx, Tv, "playing", "off");

        AdvanceMinutes(ctx, GracePeriod.TotalMinutes - 1);
        service.IsOccupied.Should().BeTrue();

        AdvanceMinutes(ctx, 2);
        service.IsOccupied.Should().BeFalse();
        changes.Should().Equal(false);
    }

    [Fact]
    public void Room_StaysOccupied_WhenAnAnchorReturnsDuringTheGracePeriod()
    {
        using var ctx = Arrange(motionState: "on", tvState: "off");
        var service = ctx.LivingRoomPresenceService;
        var changes = Record(service);

        ChangeState(ctx, Motion, "on", "off");
        AdvanceMinutes(ctx, GracePeriod.TotalMinutes / 3);

        ChangeState(ctx, Motion, "off", "on");
        AdvanceMinutes(ctx, GracePeriod.TotalMinutes * 2);

        service.IsOccupied.Should().BeTrue();
        changes.Should().BeEmpty("the pending empty signal must be cancelled, not queued");
    }

    [Fact]
    public void Room_IsOccupied_WhenTheTvStartsPlayingWithoutMotion()
    {
        using var ctx = Arrange(motionState: "off", tvState: "off");
        var service = ctx.LivingRoomPresenceService;
        var changes = Record(service);

        ChangeState(ctx, Tv, "off", "playing");

        service.IsOccupied.Should().BeTrue();
        changes.Should().Equal(true);
    }

    [Fact]
    public void Room_IsNotOccupied_WhenTheTvIsMerelyIdle()
    {
        using var ctx = Arrange(motionState: "off", tvState: "off");
        var service = ctx.LivingRoomPresenceService;

        ChangeState(ctx, Tv, "off", "idle");

        service.IsOccupied.Should().BeFalse("an idle TV means nobody is watching");
    }

    [Fact]
    public void OccupiedChanged_DoesNotReplayTheStateAtStartup()
    {
        using var ctx = Arrange(motionState: "on", tvState: "playing");
        var service = ctx.LivingRoomPresenceService;
        var changes = Record(service);

        ctx.HaContextMock.ProcessPendingOperations();

        service.IsOccupied.Should().BeTrue();
        changes.Should().BeEmpty("replaying the startup state would switch lights right after a restart");
    }
}
