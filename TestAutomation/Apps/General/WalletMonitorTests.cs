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
        Wait(TimeSpan.FromMinutes(3));

        VerifyVincent("Portemonnee weg");
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
}
