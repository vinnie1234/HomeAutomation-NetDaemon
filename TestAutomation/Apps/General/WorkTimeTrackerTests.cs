using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.Work;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class WorkTimeTrackerTests
{
    // Tuesday 10 November 2026; the Netherlands is on winter time (UTC+1)
    private static DateTime Utc(int hour, int minute) => new(2026, 11, 10, hour - 1, minute, 0, DateTimeKind.Utc);

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();
    private Dictionary<string, WorkDay> _saved = [];
    private object? _published;

    public WorkTimeTrackerTests()
    {
        _storage.When(x => x.Save(WorkTimeTracker.StorageKey, Arg.Any<Dictionary<string, WorkDay>>()))
            .Do(c => _saved = c.ArgAt<Dictionary<string, WorkDay>>(1));
        _entityManager.When(x => x.SetState(WorkTimeTracker.Sensor, Arg.Any<object>(), Arg.Any<object?>())).Do(c => _published = c.ArgAt<object?>(2));
    }

    private void SetState(string entityId, string state, object? attributes = null) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = state,
            AttributesJson = attributes == null ? null : JsonSerializer.SerializeToElement(attributes)
        });

    private void Start(int hour, int minute, bool laptopOn = true, string place = "home", string workday = "Thuis",
        Dictionary<string, WorkDay>? before = null, bool vacation = false, string end = "17:00:00", string? lastCounted = null)
    {
        SetState("input_select.werkdag_vincent_dinsdag", workday);
        SetState("input_boolean.vrij_vincent", "off");
        SetState("input_boolean.onvacation", vacation ? "on" : "off");
        SetState("input_boolean.awayvincent", place == "home" ? "off" : "on");
        SetState("zone.werk", place == "office" ? "1" : "0", new { persons = place == "office" ? new[] { "person.vincent_maarschalkerweerd" } : [] });
        SetState("input_datetime.werk_start_vincent", "08:30:00");
        SetState("input_datetime.werk_eind_vincent", end);
        SetState(WorkLaptop.Uptime, laptopOn ? "3600" : "unavailable");
        _storage.Get<Dictionary<string, WorkDay>>(WorkTimeTracker.StorageKey).Returns(before);
        _storage.Get<string>("WorkTimeLastCounted").Returns(lastCounted);
        _entityManager.EntityExists(WorkTimeTracker.Sensor).Returns(true);
        _ctx.SetCurrentTime(Utc(hour, minute));
        _ = new WorkTimeTracker(_ctx.HaContext, Substitute.For<ILogger<WorkTimeTracker>>(), Substitute.For<INotify>(), _ctx.Scheduler,
            Options.Create(new AppConfig()), _storage, _entityManager);
    }

    private void Tick(int minutes)
    {
        for (var i = 0; i < minutes; i++)
        {
            _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(1).Ticks);
            _ctx.HaContextMock.ProcessPendingOperations();
        }
    }

    private WorkDay Today() => _saved["2026-11-10"];

    [Fact]
    public void WhileTheLaptopIsOnAtHome_TheMinutesAreCountedAsHome()
    {
        Start(9, 0);

        Tick(10);

        Today().Home.Should().BeInRange(520, 700);
        Today().Office.Should().Be(0);
        Today().First.Should().NotBeNull();
    }

    [Fact]
    public void ARestartOfNetDaemon_WhileTheLaptopStaysOn_IsCountedToo()
    {
        // NetDaemon was down from 08:58 to 09:01 (a deployment); the laptop was on before and is on after it
        Start(9, 0, lastCounted: "2026-11-10T08:58:00+01:00");

        Tick(1);

        Today().Home.Should().BeInRange(170, 190);
    }

    [Fact]
    public void ALongGapSinceTheLastCount_IsNotCounted()
    {
        // The last count was yesterday evening: the night in between is no work time
        Start(9, 0, lastCounted: "2026-11-09T17:00:00+01:00");

        Tick(1);

        Today().Home.Should().BeLessThan(5);
    }

    [Fact]
    public void AtTheOffice_TheMinutesAreCountedAsOffice()
    {
        Start(9, 0, place: "office");

        Tick(10);

        Today().Office.Should().BeInRange(520, 700);
        Today().Home.Should().Be(0);
    }

    [Fact]
    public void Elsewhere_TheMinutesAreCountedAsOther()
    {
        Start(9, 0, place: "other");

        Tick(5);

        Today().Other.Should().BeGreaterThan(200);
        Today().Home.Should().Be(0);
        Today().Office.Should().Be(0);
    }

    [Fact]
    public void WithTheLaptopOff_NothingIsCounted()
    {
        Start(9, 0, laptopOn: false);

        Tick(10);

        _saved.Should().BeEmpty();
    }

    [Fact]
    public void OnADayOff_NothingIsCounted()
    {
        Start(9, 0, workday: "Vrij");

        Tick(10);

        _saved.Should().BeEmpty();
    }

    [Fact]
    public void ALaptopThatIsOnLongBeforeTheHours_IsNotCounted_ButFromTheMargeBeforeTheStartItIs()
    {
        Start(6, 0);
        Tick(60);
        _saved.Should().BeEmpty();

        Tick(90);   // 07:30 and later is within the margin of an hour before 08:30

        Today().Home.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ALaptopThatStaysOnAfterTheMarge_IsNotCountedAnymore()
    {
        Start(17, 50);
        Tick(10);
        var before = Today().Home;

        Tick(120);   // the hours end at 17:00, the margin at 18:00

        Today().Home.Should().BeInRange(before, before + 20 * 60);
    }

    [Fact]
    public void OnVacation_TheLaptopNeverCounts_EvenInsideTheHours()
    {
        Start(10, 0, vacation: true);

        Tick(20);

        _saved.Should().BeEmpty();
    }

    [Fact]
    public void AfterHalfPastSixInTheEvening_TheLaptopIsPrivate_EvenWhenTheHoursEndLater()
    {
        // The hours end at 18:00, so the margin would run to 19:00; half past six is the limit
        Start(18, 20, end: "18:00:00");

        Tick(10);
        var untilHalfPastSix = Today().Home;
        Tick(30);

        untilHalfPastSix.Should().BeGreaterThan(0);
        Today().Home.Should().Be(untilHalfPastSix);
    }

    [Fact]
    public void WhatWasCountedBefore_IsKeptAndAddedTo()
    {
        Start(9, 0, before: new Dictionary<string, WorkDay> { ["2026-11-09"] = new() { Home = 3600 } });

        Tick(3);

        _saved["2026-11-09"].Home.Should().Be(3600);
        Today().Home.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ASensorIsCreatedByNetDaemon_AndHasTheFiguresForTheDashboard()
    {
        Start(9, 0);
        _entityManager.EntityExists(WorkTimeTracker.Sensor).Returns(false);

        Tick(10);

        _published.Should().NotBeNull();
        var json = JsonSerializer.SerializeToElement(_published);
        json.GetProperty("vandaag").GetProperty("b").GetInt32().Should().BeGreaterThan(0);
        json.GetProperty("dagen").GetArrayLength().Should().BeGreaterThan(0);
        json.GetProperty("weken").GetArrayLength().Should().BeGreaterThan(0);
        json.GetProperty("maanden").EnumerateArray().First().GetProperty("k").GetString().Should().Be("2026-11");
    }
}
