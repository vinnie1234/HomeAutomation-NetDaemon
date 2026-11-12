using Automation.apps.Rooms.LivingRoom;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class TvPauseLightTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void Start(string tv = "on", string hanglamp = "off")
    {
        _ctx.HaContext.GetState("input_boolean.disablelightautomationlivingroom").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("input_boolean.away").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("media_player.tv").Returns(new EntityState { State = tv });
        _ctx.HaContext.GetState("light.hanglamp").Returns(new EntityState { State = hanglamp });
        _ctx.InitApp<TvPauseLight>();
    }

    private void Cast(string from, string to)
    {
        _ctx.ChangeStateFor(TvPauseLight.CastEntity).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void Wait() => _ctx.AdvanceTimeBy(TvPauseLight.PauseDelay.Add(TimeSpan.FromSeconds(1)).Ticks);

    [Fact]
    public void WhenTheFilmStaysPaused_ThePendantLampGoesOn()
    {
        Start();

        Cast("playing", "paused");
        Wait();

        _ctx.VerifyCallService("light", "turn_on", "hanglamp", times: 1);
    }

    [Fact]
    public void WhenThePauseIsShort_TheLampStaysOff()
    {
        Start();

        Cast("playing", "paused");
        Cast("paused", "playing");
        Wait();

        _ctx.VerifyCallService("light", "turn_on", "hanglamp", times: 0);
    }

    [Fact]
    public void WhenTheFilmPlaysAgain_ThePendantLampGoesOut()
    {
        Start();
        Cast("playing", "paused");
        Wait();

        Cast("paused", "playing");

        _ctx.VerifyCallService("light", "turn_off", "hanglamp", times: 1);
    }

    [Fact]
    public void WhenTheFilmPlays_ThePendantLampAlwaysGoesOut_EvenWhenItWasOnBefore()
    {
        Start(hanglamp: "on");
        Cast("playing", "paused");
        Wait();

        Cast("paused", "playing");

        _ctx.VerifyCallService("light", "turn_on", "hanglamp", times: 0);
        _ctx.VerifyCallService("light", "turn_off", "hanglamp", times: 1);
    }

    [Fact]
    public void WhenTheTvIsOff_NothingHappens()
    {
        Start(tv: "off");

        Cast("playing", "paused");
        Wait();

        _ctx.VerifyCallService("light", "turn_on", "hanglamp", times: 0);
    }
}
