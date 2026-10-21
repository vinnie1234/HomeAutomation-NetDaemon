using Automation.apps.General;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class PresenceManagerTests
{
    private static AppTestContext Arrange(string awayVincent, string awayCarleen, string away)
    {
        var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(
            new NetDaemon.HassModel.Entities.EntityState { EntityId = "input_boolean.awayvincent", State = awayVincent });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(
            new NetDaemon.HassModel.Entities.EntityState { EntityId = "input_boolean.awaycarleen", State = awayCarleen });
        ctx.HaContext.GetState("input_boolean.away").Returns(
            new NetDaemon.HassModel.Entities.EntityState { EntityId = "input_boolean.away", State = away });
        ctx.HaContext.GetState("input_boolean.vrij_vincent").Returns(
            new NetDaemon.HassModel.Entities.EntityState { EntityId = "input_boolean.vrij_vincent", State = "off" });
        ctx.InitApp<PresenceManager>();
        return ctx;
    }

    [Fact]
    public void Away_TurnsOn_WhenBothPeopleAreAway()
    {
        // Carleen already away, Vincent now leaves too → both away → derived away on
        var ctx = Arrange(awayVincent: "off", awayCarleen: "on", away: "off");

        ctx.ChangeStateFor("input_boolean.awayvincent").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("input_boolean", "turn_on", "away");
    }

    [Fact]
    public void Away_StaysOff_WhenOnlyVincentIsAway()
    {
        // Carleen still home, Vincent leaves → not both away → away must not turn on
        var ctx = Arrange(awayVincent: "off", awayCarleen: "off", away: "off");

        ctx.ChangeStateFor("input_boolean.awayvincent").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyNotCallService("input_boolean.turn_on");
    }

    [Fact]
    public void Away_TurnsOff_WhenSomeoneComesHome()
    {
        // Both were away, Carleen comes home → away should turn off
        var ctx = Arrange(awayVincent: "on", awayCarleen: "on", away: "on");

        // Carleen comes home: her away boolean is cleared, then the derivation runs
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(
            new NetDaemon.HassModel.Entities.EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        ctx.ChangeStateFor("input_boolean.awaycarleen").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("input_boolean", "turn_off", "away");
    }

    [Fact]
    public void HouseSitterLeaves_WhenNobodyHome_TurnsEverythingOff()
    {
        var ctx = Arrange(awayVincent: "on", awayCarleen: "on", away: "on");
        ctx.HaContext.GetState("media_player.hele_huis").Returns(
            new NetDaemon.HassModel.Entities.EntityState { EntityId = "media_player.hele_huis", State = "playing" });

        ctx.ChangeStateFor("person.timo").FromState("home").ToState("not_home");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("media_player", "turn_off", "tv");
        ctx.VerifyCallService("media_player", "media_stop", "hele_huis");
        // The kitchen and bathroom speakers aren't playing, so no (failing) stop is sent to them
        ctx.VerifyCallService("media_player", "media_stop", "nestmini9818", times: 0);
        ctx.VerifyCallService("media_player", "media_stop", "googlehome0351", times: 0);
    }

    [Fact]
    public void HouseSitterLeaves_WhenSomeoneHome_DoesNothing()
    {
        var ctx = Arrange(awayVincent: "off", awayCarleen: "on", away: "off");

        ctx.ChangeStateFor("person.timo").FromState("home").ToState("not_home");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyNotCallService("media_player.turn_off");
        ctx.VerifyNotCallService("media_player.media_stop");
    }
}

