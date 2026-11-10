using Automation.apps.General.Work;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class WorkLaptopTests
{
    // Tuesday 10 November 2026; the Netherlands is on winter time (UTC+1)
    private static DateTime Utc(int hour, int minute) => new(2026, 11, 10, hour - 1, minute, 0, DateTimeKind.Utc);

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly INotify _notify = Substitute.For<INotify>();

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    public WorkLaptopTests() =>
        // Like Home Assistant: a switch the app turns on or off is on or off from then on
        _ctx.HaContext.When(x => x.CallService("input_boolean", Arg.Any<string>(), Arg.Any<ServiceTarget?>(), Arg.Any<object?>())).Do(call =>
        {
            foreach (var id in call.ArgAt<ServiceTarget?>(2)?.EntityIds ?? [])
                SetState(id, call.ArgAt<string>(1) == "turn_on" ? "on" : "off");
        });

    /// <summary>Starts the app at this Dutch time on a working day from home, with the hours 08:30 to 17:00.</summary>
    private void Start(int hour, int minute, bool laptopOn = true, bool home = true, string workday = "Thuis", string working = "off")
    {
        SetState("input_select.werkdag_vincent_dinsdag", workday);
        SetState("input_boolean.vrij_vincent", "off");
        SetState("input_boolean.onvacation", "off");
        SetState("input_boolean.awayvincent", home ? "off" : "on");
        SetState("input_boolean.sleepingvincent", "off");
        SetState("input_boolean.workingvincent", working);
        SetState("input_datetime.werk_start_vincent", "08:30:00");
        SetState("input_datetime.werk_eind_vincent", "17:00:00");
        SetState(WorkLaptop.Uptime, laptopOn ? "3600" : "unavailable");
        _ctx.SetCurrentTime(Utc(hour, minute));
        _ = new WorkLaptop(_ctx.HaContext, Substitute.For<ILogger<WorkLaptop>>(), _notify, _ctx.Scheduler, Options.Create(new AppConfig()));
    }

    private void Laptop(string from, string to)
    {
        SetState(WorkLaptop.Uptime, to);
        _ctx.ChangeStateFor(WorkLaptop.Uptime).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void Tick(int minutes)
    {
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(minutes).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private int Calls(string domain, string service, string entityId) => _ctx.HaContext.ReceivedCalls().Count(c =>
        c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == domain && (string)c.GetArguments()[1]! == service &&
        c.GetArguments()[2] is ServiceTarget t && t.EntityIds!.Contains(entityId));

    private int WorkingOn() => Calls("input_boolean", "turn_on", "input_boolean.workingvincent");
    private int WorkingOff() => Calls("input_boolean", "turn_off", "input_boolean.workingvincent");

    // ---- starting earlier

    [Fact]
    public void ALaptopThatIsOnAnHourBeforeTheHours_StartsTheWork()
    {
        Start(7, 45);

        Tick(2);

        WorkingOn().Should().Be(1);
    }

    [Fact]
    public void ALaptopThatIsOnMoreThanTheMargeBeforeTheHours_DoesNot()
    {
        Start(6, 0);

        Tick(3);

        WorkingOn().Should().Be(0);
    }

    [Fact]
    public void TheWorkStartsWhenTheLaptopTurnsOn_NotBeforeTheMarge()
    {
        Start(6, 0, laptopOn: false);
        Tick(60);
        WorkingOn().Should().Be(0);

        Laptop("unavailable", "10");
        Tick(2);

        WorkingOn().Should().Be(0);
    }

    [Fact]
    public void AnOfficeDayThatStartsAtHome_CountsAsWorkWhileHeIsHome()
    {
        Start(7, 50, workday: "Kantoor");

        Tick(2);

        WorkingOn().Should().Be(1);
    }

    [Fact]
    public void WhenHeIsNotHome_OrItIsADayOff_TheLaptopStartsNothing()
    {
        Start(8, 0, home: false);
        Tick(3);
        WorkingOn().Should().Be(0);
    }

    [Fact]
    public void OnADayOff_TheLaptopStartsNothing()
    {
        Start(8, 0, workday: "Vrij");

        Tick(3);

        WorkingOn().Should().Be(0);
    }

    // ---- stopping earlier or later

    [Fact]
    public void ALaptopThatGoesOffWithinTheMargeBeforeTheEnd_StopsTheWorkThen()
    {
        Start(16, 30, working: "on");

        Laptop("3600", "unavailable");

        WorkingOff().Should().Be(1);
    }

    [Fact]
    public void ALaptopThatGoesOffHoursBeforeTheEnd_IsNotTheEndOfTheWork()
    {
        Start(14, 0, working: "on");

        Laptop("3600", "unavailable");

        WorkingOff().Should().Be(0);
    }

    [Fact]
    public void ALaptopThatStaysOnPastTheEnd_KeepsTheWorkGoingUntilItGoesOff()
    {
        Start(17, 5, working: "on");
        Tick(20);
        WorkingOff().Should().Be(0);

        Laptop("3600", "unavailable");
        Tick(2);

        WorkingOff().Should().Be(1);
    }

    [Fact]
    public void ALaptopThatStaysOnAllNight_StopsTheWorkAfterTheMarge()
    {
        Start(17, 5, working: "on");

        Tick(70);

        WorkingOff().Should().Be(1);
    }

    [Fact]
    public void AfterHalfPastSix_TheLaptopNeverKeepsHimWorking()
    {
        Start(18, 40);
        var end = new DateTime(2026, 11, 10, 18, 20, 0);   // the margin would run to 19:20

        WorkLaptop.KeepsWorking(_ctx.HaContext, "vincent", true, new DateTime(2026, 11, 10, 18, 29, 0), end).Should().BeTrue();
        WorkLaptop.KeepsWorking(_ctx.HaContext, "vincent", true, new DateTime(2026, 11, 10, 18, 31, 0), end).Should().BeFalse();
    }

    [Theory]
    [InlineData(17, 30, true)]
    [InlineData(17, 59, true)]
    [InlineData(18, 5, false)]
    public void TheLaptopKeepsHimWorking_UntilTheMargeAfterTheEnd(int hour, int minute, bool expected)
    {
        Start(hour, minute);
        var end = new DateTime(2026, 11, 10, 17, 0, 0);

        WorkLaptop.KeepsWorking(_ctx.HaContext, "vincent", true, new DateTime(2026, 11, 10, hour, minute, 0), end).Should().Be(expected);
    }

    [Fact]
    public void TheLaptopOnlyKeepsHimWorking_WhenHeIsHomeAndItIsOn()
    {
        Start(17, 30);
        var now = new DateTime(2026, 11, 10, 17, 30, 0);
        var end = new DateTime(2026, 11, 10, 17, 0, 0);

        WorkLaptop.KeepsWorking(_ctx.HaContext, "vincent", false, now, end).Should().BeFalse();
        WorkLaptop.KeepsWorking(_ctx.HaContext, "carleen", true, now, end).Should().BeFalse();
        SetState(WorkLaptop.Uptime, "unavailable");
        WorkLaptop.KeepsWorking(_ctx.HaContext, "vincent", true, now, end).Should().BeFalse();
    }

    // ---- work mode, leaving, sleeping

    [Fact]
    public void WhenTheLaptopStartsOnAWorkingDay_TheDeskLightGoesOn_OncePerDay()
    {
        Start(8, 0, laptopOn: false);

        Laptop("unavailable", "10");
        Laptop("10", "unavailable");
        Laptop("unavailable", "10");

        Calls("light", "turn_on", "light.bureau").Should().Be(1);
        Calls("light", "turn_off", "light.nachtkastje").Should().Be(1);
    }

    [Fact]
    public void WhenTheLaptopStartsOnADayOff_NothingHappens()
    {
        Start(8, 0, laptopOn: false, workday: "Vrij");

        Laptop("unavailable", "10");

        Calls("light", "turn_on", "light.bureau").Should().Be(0);
    }

    [Fact]
    public void WhenHeLeaves_TheLaptopIsLocked()
    {
        Start(8, 0);

        _ctx.ChangeStateFor("input_boolean.awayvincent").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        Calls("button", "press", WorkLaptop.LockButton).Should().Be(1);
    }

    [Fact]
    public void WhenHeLeaves_AndTheLaptopIsOff_NothingIsPressed()
    {
        Start(8, 0, laptopOn: false);

        _ctx.ChangeStateFor("input_boolean.awayvincent").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        Calls("button", "press", WorkLaptop.LockButton).Should().Be(0);
    }

    [Fact]
    public void WhenHeGoesToSleep_TheLaptopIsShutDown()
    {
        Start(23, 0);

        _ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        Calls("button", "press", WorkLaptop.ShutdownButton).Should().Be(1);
    }

    // ---- the end of the day and the battery

    [Fact]
    public void AtTheEndOfTheDay_WithTheLaptopStillOn_HeGetsOneReminder()
    {
        Start(16, 58);

        Tick(5);
        Tick(10);

        _notify.Received(1).NotifyPhoneVincent("Werklaptop", "Het is tijd om af te sluiten", false, Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public void AtTheEndOfTheDay_WithTheLaptopOff_ThereIsNoReminder()
    {
        Start(16, 58, laptopOn: false);

        Tick(10);

        _notify.DidNotReceive().NotifyPhoneVincent("Werklaptop", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    private void Battery(string level, string plugged)
    {
        SetState(WorkLaptop.Battery, level);
        SetState(WorkLaptop.PluggedIn, plugged);
        _ctx.ChangeStateFor(WorkLaptop.Battery).FromState("50").ToState(level);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void VerifyBatteryMessages(int times) =>
        _notify.Received(times).NotifyPhoneVincent("Werklaptop", Arg.Is<string>(m => m.Contains("batterij")), false, Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

    [Fact]
    public void ALowBattery_WithoutTheCharger_GivesOneMessage_UntilItRecovers()
    {
        Start(10, 0);

        Battery("18", "off");
        Battery("17", "off");
        VerifyBatteryMessages(1);

        Battery("80", "on");
        Battery("15", "off");
        VerifyBatteryMessages(2);
    }

    [Fact]
    public void ALowBattery_OnTheCharger_GivesNoMessage()
    {
        Start(10, 0);

        Battery("10", "on");

        VerifyBatteryMessages(0);
    }
}
