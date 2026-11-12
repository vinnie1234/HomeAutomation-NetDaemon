using Automation.apps.Rooms.LivingRoom;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class DateNightTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly INotify _notify = Substitute.For<INotify>();

    private void Start(string vacuum = "docked")
    {
        _ctx.HaContext.GetState("input_boolean.datenight").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("input_boolean.away").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("input_boolean.disablelightautomationlivingroom").Returns(new EntityState { State = "off" });
        _ctx.HaContext.GetState("vacuum.jaap").Returns(new EntityState { State = vacuum });
        _ = new DateNight(_ctx.HaContext, Substitute.For<ILogger<DateNight>>(), _notify, _ctx.Scheduler, _ctx.Spotcast,
            Options.Create(new AppConfig()));
    }

    private void Change(string entityId, string from, string to)
    {
        _ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    [Fact]
    public void WhenDateNightStarts_TheLightsTurnRomantic()
    {
        Start();

        Change("input_boolean.datenight", "off", "on");

        _ctx.VerifyCallService("light", "turn_on", "hue_filament_bulb_1", times: 1);
        _ctx.VerifyCallService("light", "turn_on", "bank", times: 1);
        _ctx.VerifyCallService("light", "turn_on", "hue_play_links", times: 1);
    }

    [Fact]
    public void WhenDateNightStarts_TheMusicIsAskedAbout_AndNotStartedYet()
    {
        Start();

        Change("input_boolean.datenight", "off", "on");

        _notify.Received(1).NotifyPhoneVincentCarleen("Date night", Arg.Any<string>(), true, Arg.Any<double?>(),
            Arg.Is<List<ActionModel>>(a => a.Count == 2 && a[0].Title == "Ja" && a[1].Title == "Nee"), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), DateNight.MusicQuestionTimeout);
        _ctx.Spotcast.DidNotReceiveWithAnyArgs().PlaySpotify(default!, default!, default);
    }

    [Fact]
    public void WhenDateNightStarts_WhileTheVacuumCleans_ItGoesBackToItsDock()
    {
        Start(vacuum: "cleaning");

        Change("input_boolean.datenight", "off", "on");

        _ctx.VerifyCallService("vacuum", "return_to_base", "jaap", times: 1);
    }

    [Fact]
    public void WhenSomeoneGoesToSleep_DateNightEnds()
    {
        Start();
        _ctx.HaContext.GetState("input_boolean.datenight").Returns(new EntityState { State = "on" });

        Change("input_boolean.sleepingvincent", "off", "on");

        _ctx.VerifyCallService("input_boolean", "turn_off", "datenight", times: 1);
    }

    [Fact]
    public void WhenDateNightIsNotOn_GoingToSleepChangesNothing()
    {
        Start();

        Change("input_boolean.sleepingvincent", "off", "on");

        _ctx.VerifyCallService("input_boolean", "turn_off", "datenight", times: 0);
    }
}
