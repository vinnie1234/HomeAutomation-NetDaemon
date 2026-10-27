using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Automation.Models;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class CarMonitorTests
{
    private const string Trunk = "binary_sensor.buiten_peugeot_2008_trunk";
    private const string DriverDoor = "binary_sensor.buiten_peugeot_2008_driver_door";

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly INotify _notify = Substitute.For<INotify>();

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    /// <summary>Starts the app at 12:00 Dutch time with the car at home, locked and closed, and both of us home and awake.</summary>
    private void Start(string tracker = "home", string vincentAway = "off", string carleenAway = "off", string vincentSleeping = "off", string carleenSleeping = "off")
    {
        _ctx.SetCurrentTime(new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc));
        SetState(CarMonitor.Tracker, tracker);
        SetState(CarMonitor.Unlocked, "off");
        SetState(CarMonitor.Engine, "off");
        foreach (var opening in CarMonitor.Openings.Keys) SetState(opening, "off");
        SetState("input_boolean.awayvincent", vincentAway);
        SetState("input_boolean.awaycarleen", carleenAway);
        SetState("input_boolean.sleepingvincent", vincentSleeping);
        SetState("input_boolean.sleepingcarleen", carleenSleeping);
        SetState("input_boolean.onvacation", "off");
        SetState("input_boolean.away", "off");
        _ = new CarMonitor(_ctx.HaContext, Substitute.For<ILogger<CarMonitor>>(), _notify, _ctx.Scheduler, Options.Create(new AppConfig()));
    }

    private void Change(string entityId, string from, string to)
    {
        SetState(entityId, to);
        _ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void Wait(TimeSpan time)
    {
        _ctx.AdvanceTimeBy(time.Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void VerifyVincent(string title, string message, int times = 1) =>
        _notify.Received(times).NotifyPhoneVincent(title, Arg.Is<string>(m => m.Contains(message)), Arg.Any<bool>(), Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

    private void VerifyBoth(string title, int times = 1) =>
        _notify.Received(times).NotifyPhoneVincentCarleen(title, Arg.Any<string>(), true, 10,
            Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

    // ---- fuel and service

    [Theory]
    [InlineData("90", 1)]
    [InlineData("99.9", 1)]
    [InlineData("150", 0)]
    [InlineData("unknown", 0)]
    public void FuelRange_WarnsVincentBelowHundredKilometres(string range, int times)
    {
        Start();

        Change(CarMonitor.FuelRange, "400", range);

        VerifyVincent("Tanken", "bijna leeg", times);
    }

    [Theory]
    [InlineData("25", "7000", 1)]
    [InlineData("60", "800", 1)]
    [InlineData("60", "7000", 0)]
    [InlineData("unknown", "unknown", 0)]
    public void Service_WarnsVincentWhenTheDaysOrTheKilometresRunOut(string days, string km, int times)
    {
        Start();
        SetState(CarMonitor.KmToService, km);

        Change(CarMonitor.DaysToService, "75", days);

        VerifyVincent("Onderhoudsbeurt auto", "naar de garage", times);
    }

    // ---- 22:00

    [Fact]
    public void Evening_WhenTheCarIsNotLocked_SendsAMessageWithALockButton()
    {
        Start();
        SetState(CarMonitor.Unlocked, "on");

        Wait(TimeSpan.FromHours(11));

        _notify.Received(1).NotifyPeopleHome("De auto is niet dicht", Arg.Is<string>(m => m.Contains("niet op slot")), false, 60,
            Arg.Is<List<ActionModel>?>(a => a!.Single().Action == "LOCK_CAR"), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public void Evening_TheLockButtonPressesTheLockButtonOfTheCar()
    {
        Start();
        SetState(CarMonitor.Unlocked, "on");
        Wait(TimeSpan.FromHours(11));
        var action = _notify.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "NotifyPeopleHome").GetArguments()[4] as List<ActionModel>;

        action!.Single().Func!();

        _ctx.HaContext.Received(1).CallService("button", "press", Arg.Is<ServiceTarget?>(t => t!.EntityIds!.Single() == CarMonitor.LockButton), Arg.Any<object?>());
    }

    [Fact]
    public void Evening_NamesTheOpenDoorsAndTheTrunk()
    {
        Start();
        SetState(Trunk, "on");
        SetState(DriverDoor, "on");

        Wait(TimeSpan.FromHours(11));

        _notify.Received(1).NotifyPeopleHome("De auto is niet dicht", Arg.Is<string>(m => m.Contains("bestuurdersdeur open") && m.Contains("kofferbak open")),
            false, 60, Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public void Evening_WhenLockedAndClosed_SaysNothing()
    {
        Start();

        Wait(TimeSpan.FromHours(11));

        _notify.DidNotReceiveWithAnyArgs().NotifyPeopleHome(null!, null!, false);
    }

    [Fact]
    public void Evening_WhenTheCarIsNotAtHome_SaysNothing()
    {
        Start(tracker: "Werk");
        SetState(CarMonitor.Unlocked, "on");

        Wait(TimeSpan.FromHours(11));

        _notify.DidNotReceiveWithAnyArgs().NotifyPeopleHome(null!, null!, false);
    }

    // ---- left open

    [Fact]
    public void TrunkOpenForTenMinutes_WarnsVincent()
    {
        Start();

        Change(Trunk, "off", "on");
        Wait(TimeSpan.FromMinutes(11));

        VerifyVincent("De auto: kofferbak staat open", "10 minuten");
    }

    [Fact]
    public void TrunkClosedWithinTenMinutes_SaysNothing()
    {
        Start();

        Change(Trunk, "off", "on");
        Wait(TimeSpan.FromMinutes(5));
        Change(Trunk, "on", "off");
        Wait(TimeSpan.FromMinutes(6));

        VerifyVincent("De auto: kofferbak staat open", "", 0);
    }

    [Fact]
    public void DoorOpenWhileTheEngineRuns_SaysNothing()
    {
        Start();
        SetState(CarMonitor.Engine, "on");

        Change(DriverDoor, "off", "on");
        Wait(TimeSpan.FromMinutes(11));

        VerifyVincent("De auto: bestuurdersdeur staat open", "", 0);
    }

    // ---- security

    [Fact]
    public void CarLeavesHome_WhileBothOfUsAreHome_WarnsBothPhones_AfterTwoMinutes()
    {
        Start();

        Change(CarMonitor.Tracker, "home", "not_home");
        VerifyBoth("De auto is weggereden", 0);
        Wait(TimeSpan.FromMinutes(3));

        VerifyBoth("De auto is weggereden");
    }

    [Fact]
    public void CarLeavesHome_WhenVincentLeavesToo_SaysNothing()
    {
        Start();

        Change(CarMonitor.Tracker, "home", "not_home");
        Change("input_boolean.awayvincent", "off", "on");   // the phone of whoever drives follows a moment later
        Wait(TimeSpan.FromMinutes(3));

        VerifyBoth("De auto is weggereden", 0);
    }

    [Fact]
    public void CarLeavesHome_ButIsBackWithinTwoMinutes_SaysNothing()
    {
        Start();

        Change(CarMonitor.Tracker, "home", "not_home");
        Change(CarMonitor.Tracker, "not_home", "home");
        Wait(TimeSpan.FromMinutes(3));

        VerifyBoth("De auto is weggereden", 0);
    }

    [Fact]
    public void DoorOpensAtNight_WhileEveryoneSleeps_WarnsBothPhones()
    {
        Start(vincentSleeping: "on", carleenSleeping: "on");

        Change(DriverDoor, "off", "on");

        VerifyBoth("Auto: bestuurdersdeur open");
    }

    [Fact]
    public void DoorOpens_WhileSomeoneIsAwake_SaysNothing()
    {
        Start(vincentSleeping: "on", carleenSleeping: "off");

        Change(DriverDoor, "off", "on");

        VerifyBoth("Auto: bestuurdersdeur open", 0);
    }
}
