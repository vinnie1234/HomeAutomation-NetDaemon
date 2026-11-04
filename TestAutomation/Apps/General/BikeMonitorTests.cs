using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class BikeMonitorTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly INotify _notify = Substitute.For<INotify>();

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    /// <summary>Starts the app with the bike at home, charged, and both of us home.</summary>
    private void Start(string vincentAway = "off", string carleenAway = "off")
    {
        _ctx.SetCurrentTime(new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc));
        SetState(BikeMonitor.Tracker, "home");
        SetState(BikeMonitor.BatteryLevel, "80");
        SetState(BikeMonitor.Charging, "off");
        SetState(BikeMonitor.Range, "47");
        SetState("input_boolean.awayvincent", vincentAway);
        SetState("input_boolean.awaycarleen", carleenAway);
        _ = new BikeMonitor(_ctx.HaContext, Substitute.For<ILogger<BikeMonitor>>(), _notify, _ctx.Scheduler, Options.Create(new AppConfig()));
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

    private void VerifyVincent(string title, int times = 1) =>
        _notify.Received(times).NotifyPhoneVincent(title, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

    private void VerifyBoth(string title, int times = 1) =>
        _notify.Received(times).NotifyPhoneVincentCarleen(title, Arg.Any<string>(), true, 10,
            Arg.Any<List<ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

    [Theory]
    [InlineData("100", 1)]
    [InlineData("96", 1)]
    [InlineData("60", 0)]
    public void ChargingStops_MeansFullOnlyFromNinetyFivePercent(string level, int times)
    {
        Start();
        SetState(BikeMonitor.Charging, "on");
        SetState(BikeMonitor.BatteryLevel, level);

        Change(BikeMonitor.Charging, "on", "off");

        VerifyVincent("Fiets: accu vol", times);
    }

    [Fact]
    public void TheBikeLeaves_WhileBothAreHome_WarnsBoth()
    {
        Start();

        Change(BikeMonitor.Tracker, "home", "not_home");
        Wait(TimeSpan.FromMinutes(3));

        VerifyBoth("De fiets is weg");
    }

    [Fact]
    public void TheBikeLeaves_WhileOnlyVincentIsHome_WarnsVincent()
    {
        Start(carleenAway: "on");

        Change(BikeMonitor.Tracker, "home", "not_home");
        Wait(TimeSpan.FromMinutes(3));

        VerifyVincent("De fiets is weg");
    }

    [Fact]
    public void TheBikeLeaves_WhileVincentIsAway_WarnsNobody()
    {
        Start(vincentAway: "on");

        Change(BikeMonitor.Tracker, "home", "not_home");
        Wait(TimeSpan.FromMinutes(3));

        VerifyVincent("De fiets is weg", 0);
        VerifyBoth("De fiets is weg", 0);
    }

    [Fact]
    public void ABikeThatIsBackAfterAMoment_WarnsNobody()
    {
        Start();

        Change(BikeMonitor.Tracker, "home", "not_home");
        Wait(TimeSpan.FromMinutes(1));
        SetState(BikeMonitor.Tracker, "home");
        Wait(TimeSpan.FromMinutes(2));

        VerifyVincent("De fiets is weg", 0);
        VerifyBoth("De fiets is weg", 0);
    }
}
