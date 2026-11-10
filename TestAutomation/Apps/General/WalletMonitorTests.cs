using System.Text.Json;
using Automation.apps.General;
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

public class WalletMonitorTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly INotify _notify = Substitute.For<INotify>();

    private void SetState(string entityId, string state, object? attributes = null) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = state,
            AttributesJson = attributes == null ? null : JsonSerializer.SerializeToElement(attributes)
        });

    /// <summary>Starts the app at 12:00 Dutch time (11:00 UTC) with the wallet at home and Vincent home.</summary>
    private void Start(string vincentAway = "off", string tracker = "Huis Vincent")
    {
        _ctx.SetCurrentTime(new DateTime(2026, 10, 27, 11, 0, 0, DateTimeKind.Utc));
        SetState(WalletMonitor.Tracker, tracker, new { last_seen = "2026-10-27T10:00:00Z" });
        SetState(WalletMonitor.UnwantedTracking, "off");
        SetState("input_boolean.awayvincent", vincentAway);
        SetState("input_boolean.awaycarleen", "off");
        _ = new WalletMonitor(_ctx.HaContext, Substitute.For<ILogger<WalletMonitor>>(), _notify, _ctx.Scheduler, Options.Create(new AppConfig()));
    }

    private void Change(string entityId, string from, string to, object? attributes = null)
    {
        SetState(entityId, to, attributes);
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

    [Theory]
    [InlineData("home", true)]
    [InlineData("Huis Vincent", true)]
    [InlineData("Thuis", true)]
    [InlineData("Werk", false)]
    [InlineData("not_home", false)]
    [InlineData(null, false)]
    public void AtHome_IsTheZoneOrTheGoogleLabelOfTheHouse(string? state, bool expected)
    {
        WalletMonitor.IsAtHome(state).Should().Be(expected);
    }

    [Fact]
    public void VincentLeaves_TheWalletIsAskedWhereItIs_AndNotReported_WhenItWasNotSeenSinceHeLeft()
    {
        Start();

        Change("input_boolean.awayvincent", "off", "on");
        Wait(TimeSpan.FromMinutes(8));

        _ctx.HaContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == "button").Should().Be(1);
        VerifyVincent("Portemonnee vergeten?", 0);
    }

    [Fact]
    public void VincentLeaves_AndTheWalletIsSeenAtHomeAfterwards_WarnsHim()
    {
        Start();

        Change("input_boolean.awayvincent", "off", "on");
        Wait(TimeSpan.FromMinutes(5));
        // The answer to the question where it is: seen at home five minutes after he left
        SetState(WalletMonitor.Tracker, "Huis Vincent", new { last_seen = "2026-10-27T11:05:00Z" });
        Wait(TimeSpan.FromMinutes(3));

        VerifyVincent("Portemonnee vergeten?");
    }

    [Fact]
    public void VincentLeaves_AndTheWalletIsSeenElsewhere_WarnsNobody()
    {
        Start();

        Change("input_boolean.awayvincent", "off", "on");
        Wait(TimeSpan.FromMinutes(5));
        SetState(WalletMonitor.Tracker, "Werk", new { last_seen = "2026-10-27T11:05:00Z" });
        Wait(TimeSpan.FromMinutes(3));

        VerifyVincent("Portemonnee vergeten?", 0);
    }

    [Fact]
    public void TheWalletLeaves_WhileVincentIsHome_WarnsHim()
    {
        Start();

        Change(WalletMonitor.Tracker, "Huis Vincent", "Werk");
        Wait(TimeSpan.FromMinutes(20));
        VerifyVincent("Portemonnee weg", 0);

        Wait(TimeSpan.FromMinutes(11));
        VerifyVincent("Portemonnee weg");
    }

    [Fact]
    public void TheWalletLeaves_AndIsBackWithinHalfAnHour_WarnsNobody()
    {
        Start();

        Change(WalletMonitor.Tracker, "Huis Vincent", "Werk");
        Wait(TimeSpan.FromMinutes(20));
        Change(WalletMonitor.Tracker, "Werk", "Huis Vincent");
        Wait(TimeSpan.FromMinutes(30));

        VerifyVincent("Portemonnee weg", 0);
        AlertsOn().Should().Be(0);
    }

    [Fact]
    public void TheWalletLeaves_WhileVincentIsAway_WarnsNobody()
    {
        Start(vincentAway: "on");

        Change(WalletMonitor.Tracker, "Huis Vincent", "Werk");
        Wait(TimeSpan.FromMinutes(3));

        VerifyVincent("Portemonnee weg", 0);
    }

    [Fact]
    public void UnwantedTracking_WarnsVincent()
    {
        Start();

        Change(WalletMonitor.UnwantedTracking, "off", "on");

        VerifyVincent("Portemonnee: ongewenste tracking");
    }

    // ---- coming home with the wallet not at home: scan, scan again, and only then the alert

    private int Calls(string domain, string service, string entityId) =>
        _ctx.HaContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == domain
            && (string)c.GetArguments()[1]! == service && c.GetArguments()[2] is ServiceTarget t && t.EntityIds!.Contains(entityId));

    private int Scans() => Calls("button", "press", WalletMonitor.LocateButton);
    private int AlertsOn() => Calls("input_boolean", "turn_on", WalletMonitor.AlertHelper);

    [Fact]
    public void ComingHome_WithTheWalletNotAtHome_ScansNow_ScansAgainAMinuteLater_AndAlertsOnceItHasBeenAwayHalfAnHour()
    {
        Start(vincentAway: "on", tracker: "Werk");

        Change("input_boolean.awayvincent", "on", "off");
        Scans().Should().Be(1);
        AlertsOn().Should().Be(0);

        // (every Wait also lets the mock process a second)
        Wait(TimeSpan.FromSeconds(50));
        Scans().Should().Be(1);

        Wait(TimeSpan.FromSeconds(15));
        Scans().Should().Be(2);
        AlertsOn().Should().Be(0);

        Wait(TimeSpan.FromSeconds(40));
        AlertsOn().Should().Be(0);

        Wait(TimeSpan.FromSeconds(30));
        AlertsOn().Should().Be(0, "the wallet has not been away for half an hour yet");

        Wait(TimeSpan.FromMinutes(29));
        AlertsOn().Should().Be(1);
        Scans().Should().Be(2);
    }

    [Fact]
    public void ComingHome_AndTheWalletIsFoundByTheFirstScan_NeverAlerts()
    {
        Start(vincentAway: "on", tracker: "Werk");

        Change("input_boolean.awayvincent", "on", "off");
        Change(WalletMonitor.Tracker, "Werk", "Huis Vincent");
        Wait(TimeSpan.FromMinutes(3));

        Scans().Should().Be(1);
        AlertsOn().Should().Be(0);
    }

    [Fact]
    public void ComingHome_AndTheWalletIsFoundByTheSecondScan_NeverAlerts()
    {
        Start(vincentAway: "on", tracker: "Werk");

        Change("input_boolean.awayvincent", "on", "off");
        Wait(TimeSpan.FromSeconds(70));
        Change(WalletMonitor.Tracker, "Werk", "home");
        Wait(TimeSpan.FromMinutes(3));

        Scans().Should().Be(2);
        AlertsOn().Should().Be(0);
    }

    [Fact]
    public void ComingHome_WithTheWalletAtHome_DoesNotScanOrAlert()
    {
        Start(vincentAway: "on", tracker: "Huis Vincent");

        Change("input_boolean.awayvincent", "on", "off");
        Wait(TimeSpan.FromMinutes(3));

        Scans().Should().Be(0);
        AlertsOn().Should().Be(0);
    }

    [Fact]
    public void WhenVincentLeavesAgainDuringTheSearch_ThereIsNoAlert()
    {
        Start(vincentAway: "on", tracker: "Werk");

        Change("input_boolean.awayvincent", "on", "off");
        Wait(TimeSpan.FromSeconds(30));
        Change("input_boolean.awayvincent", "off", "on");
        Wait(TimeSpan.FromMinutes(3));

        AlertsOn().Should().Be(0);
    }

    [Fact]
    public void WhenTheWalletIsFoundAfterTheAlert_TheAlertGoesAway()
    {
        Start(vincentAway: "on", tracker: "Werk");
        Change("input_boolean.awayvincent", "on", "off");
        Wait(TimeSpan.FromMinutes(31));
        AlertsOn().Should().Be(1);

        Change(WalletMonitor.Tracker, "Werk", "Huis Vincent");

        Calls("input_boolean", "turn_off", WalletMonitor.AlertHelper).Should().BeGreaterThan(1);
    }

    [Fact]
    public void TheWalletLeaves_WhileVincentIsHome_AlsoScansTwiceBeforeTheAlert()
    {
        Start();

        Change(WalletMonitor.Tracker, "Huis Vincent", "Werk");
        Wait(TimeSpan.FromMinutes(31));

        Scans().Should().Be(2);
        AlertsOn().Should().Be(1);
    }
}
