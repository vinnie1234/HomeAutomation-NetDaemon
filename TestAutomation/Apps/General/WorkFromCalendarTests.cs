using System.Text.Json;
using Automation.apps.General.Work;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.Persons;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class WorkFromCalendarTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    /// <summary>Makes the calendar return these events (summary, start, end) for the day asked.</summary>
    private void Events(params (string Summary, string Start, string End)[] events) =>
        _ctx.HaContext.CallServiceWithResponseAsync("calendar", "get_events",
                Arg.Is<ServiceTarget?>(t => t != null && t.EntityIds!.Single() == WorkFromCalendar.CarleenCalendar), Arg.Any<object?>())
            .Returns(Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [WorkFromCalendar.CarleenCalendar] = new { events = events.Select(e => new { summary = e.Summary, start = e.Start, end = e.End }) }
            })));

    /// <summary>Starts the app on Wednesday 28 October 2026 at 22:00 UTC (23:00 Dutch time).</summary>
    private void Start(string thursday = "Vrij", string wednesday = "Kantoor")
    {
        SetState("input_select.werkdag_carleen_donderdag", thursday);
        SetState("input_select.werkdag_carleen_woensdag", wednesday);
        _ctx.SetCurrentTime(new DateTime(2026, 10, 28, 22, 0, 0, DateTimeKind.Utc));
        _ = new WorkFromCalendar(_ctx.HaContext, Substitute.For<ILogger<WorkFromCalendar>>(), Substitute.For<INotify>(), _ctx.Scheduler, _storage,
            Options.Create(new AppConfig()));
    }

    private void AdvanceTo(DateTime utc)
    {
        _ctx.AdvanceTimeTo(utc.Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(100);
    }

    [Theory]
    [InlineData("C: werk", true)]
    [InlineData("c:werk", true)]
    [InlineData("  C :  Werk 14:00", true)]
    [InlineData("C:WERK", true)]
    [InlineData("Werk", false)]
    [InlineData("C: werkoverleg", false)]
    [InlineData("V: werk", false)]
    [InlineData("Tandarts C: werk", false)]
    [InlineData(null, false)]
    public void IsWorkEvent_IgnoresCapitalsAndSpaces(string? summary, bool expected)
    {
        WorkFromCalendar.IsWorkEvent(summary).Should().Be(expected);
    }

    [Fact]
    public void WorkEventTomorrow_MakesTomorrowKantoor_WithTheTimesOfTheEvent()
    {
        Events(("c : Werk", "2026-10-29T14:00:00+01:00", "2026-10-29T22:00:00+01:00"));
        Start();
        _ctx.HaContext.ClearReceivedCalls();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 51, 0, DateTimeKind.Utc));

        _ctx.VerifyCallService("input_select", "select_option", "werkdag_carleen_donderdag", times: 1);
        _storage.Received().Save(WorkFromCalendar.StoreKey, Arg.Is<WorkCalendarStore>(s =>
            s.Entries.Count == 1 && s.Entries[0].Person == "carleen" && s.Entries[0].Date == "2026-10-29" && s.Entries[0].Start == "14:00" && s.Entries[0].End == "22:00"));
    }

    [Fact]
    public void AnAllDayWorkEvent_HasNoTimes()
    {
        Events(("C: werk", "2026-10-29", "2026-10-30"));
        Start();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 51, 0, DateTimeKind.Utc));

        _storage.Received().Save(WorkFromCalendar.StoreKey, Arg.Is<WorkCalendarStore>(s => s.Entries.Count == 1 && s.Entries[0].Start == null && s.Entries[0].End == null));
    }

    [Fact]
    public void OtherEvents_ChangeNothing()
    {
        Events(("Tandarts", "2026-10-29T10:00:00+01:00", "2026-10-29T11:00:00+01:00"));
        Start();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 51, 0, DateTimeKind.Utc));

        _ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void ADayThatIsOver_GoesBackToVrij_AtJustAfterMidnight()
    {
        Events();
        _storage.Get<WorkCalendarStore>(WorkFromCalendar.StoreKey).Returns(new WorkCalendarStore
        {
            Entries = [new WorkCalendarEntry { Person = "carleen", Date = "2026-10-28" }]
        });
        Start();
        _ctx.HaContext.ClearReceivedCalls();

        // Just after midnight: Wednesday's Kantoor was set from the calendar and goes back to Vrij
        AdvanceTo(new DateTime(2026, 10, 28, 23, 6, 0, DateTimeKind.Utc));

        _ctx.VerifyCallService("input_select", "select_option", "werkdag_carleen_woensdag", times: 1);
        _storage.Received().Save(WorkFromCalendar.StoreKey, Arg.Is<WorkCalendarStore>(s => s.Entries.Count == 0));
    }

    [Fact]
    public void ADayThatIsNotOverYet_StaysKantoor()
    {
        Events();
        _storage.Get<WorkCalendarStore>(WorkFromCalendar.StoreKey).Returns(new WorkCalendarStore
        {
            Entries = [new WorkCalendarEntry { Person = "carleen", Date = "2026-10-29" }]
        });
        Start();

        _ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void Carleen_SwitchedOff_IsNotSetFromTheCalendar()
    {
        Events(("C: werk", "2026-10-29T14:00:00+01:00", "2026-10-29T22:00:00+01:00"));
        SetState("input_boolean.werk_uit_agenda_carleen", "off");
        Start();
        _ctx.HaContext.ClearReceivedCalls();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 51, 0, DateTimeKind.Utc));

        _ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void Vincent_SwitchedOn_GetsHisV_WerkEvents_AndNotCarleensEvents()
    {
        Events(("V: werk", "2026-10-29T09:00:00+01:00", "2026-10-29T17:00:00+01:00"));
        SetState("input_boolean.werk_uit_agenda_vincent", "on");
        SetState("input_select.werkdag_vincent_donderdag", "Vrij");
        Start();
        _ctx.HaContext.ClearReceivedCalls();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 51, 0, DateTimeKind.Utc));

        _ctx.VerifyCallService("input_select", "select_option", "werkdag_vincent_donderdag", times: 1);
        _ctx.VerifyCallService("input_select", "select_option", "werkdag_carleen_donderdag", times: 0);
    }

    [Fact]
    public void TheSyncButton_ReadsTheNextSixDays()
    {
        Events(("C: werk", "2026-11-02T14:00:00+01:00", "2026-11-02T22:00:00+01:00"), ("C: werk", "2026-10-29", "2026-10-30"));
        SetState("input_select.werkdag_carleen_maandag", "Vrij");
        Start();
        _ctx.HaContext.ClearReceivedCalls();

        _ctx.ChangeStateFor("input_button.sync_agenda_carleen").FromState("2026-10-01T10:00:00+00:00").ToState("2026-10-28T22:10:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(100);

        _ctx.VerifyCallService("input_select", "select_option", "werkdag_carleen_donderdag", times: 1);
        _ctx.VerifyCallService("input_select", "select_option", "werkdag_carleen_maandag", times: 1);
    }

    [Theory]
    [InlineData("C: Vrij", true)]
    [InlineData("c:vakantie", true)]
    [InlineData("  C :  Vakantie Spanje", true)]
    [InlineData("C: vrijdag", false)]
    [InlineData("V: vrij", false)]
    [InlineData("C: werk", false)]
    public void IsDayOffEvent_RecognisesVrijAndVakantie(string summary, bool expected)
    {
        WorkFromCalendar.IsDayOffEvent(summary).Should().Be(expected);
    }

    [Theory]
    [InlineData("C: Tandarts", true)]
    [InlineData("c:ouders", true)]
    [InlineData("C: werk", false)]
    [InlineData("C: vakantie", false)]
    [InlineData("V: Tandarts", false)]
    [InlineData("Tandarts", false)]
    public void IsAppointment_IsAnythingElseWithTheLetter(string summary, bool expected)
    {
        WorkFromCalendar.IsAppointment(summary).Should().Be(expected);
    }

    [Fact]
    public void ADayOffEvent_MakesTheDayVrij_AndGivesTheOldSettingBackAfterwards()
    {
        Events(("C: Vakantie", "2026-10-29", "2026-10-31"));
        Start("Kantoor");
        _ctx.HaContext.ClearReceivedCalls();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 51, 0, DateTimeKind.Utc));

        _ctx.VerifyCallService("input_select", "select_option", "werkdag_carleen_donderdag", times: 1);
        _storage.Received().Save(WorkFromCalendar.StoreKey, Arg.Is<WorkCalendarStore>(s =>
            s.Entries.Count == 1 && s.Entries[0].Kind == WorkCalendarEntry.DayOffKind && s.Entries[0].Previous == "Kantoor"));
    }

    [Fact]
    public void ADayOffEvent_AlsoWorksForPeopleWhoseWorkDoesNotComeFromTheCalendar()
    {
        Events(("V: vrij", "2026-10-29", "2026-10-30"));
        SetState("input_boolean.werk_uit_agenda_vincent", "off");
        SetState("input_select.werkdag_vincent_donderdag", "Kantoor");
        Start();
        _ctx.HaContext.ClearReceivedCalls();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 51, 0, DateTimeKind.Utc));

        _ctx.VerifyCallService("input_select", "select_option", "werkdag_vincent_donderdag", times: 1);
    }

    [Fact]
    public void ADayOff_GetsItsOldSettingBack_WhenTheDayIsOver()
    {
        Events();
        _storage.Get<WorkCalendarStore>(WorkFromCalendar.StoreKey).Returns(new WorkCalendarStore
        {
            Entries = [new WorkCalendarEntry { Person = "carleen", Date = "2026-10-28", Kind = WorkCalendarEntry.DayOffKind, Previous = "Kantoor" }]
        });
        Start(wednesday: "Vrij");
        _ctx.HaContext.ClearReceivedCalls();

        AdvanceTo(new DateTime(2026, 10, 28, 23, 6, 0, DateTimeKind.Utc));

        _ctx.VerifyCallService("input_select", "select_option", "werkdag_carleen_woensdag", times: 1);
    }

    [Fact]
    public void TheNextAppointmentOfToday_IsKeptInAHelper()
    {
        Events(("C: Tandarts", "2026-10-29T14:00:00+01:00", "2026-10-29T15:00:00+01:00"));
        Start();
        _ctx.HaContext.ClearReceivedCalls();

        AdvanceTo(new DateTime(2026, 10, 28, 22, 31, 0, DateTimeKind.Utc));

        _ctx.VerifyCallService("input_text", "set_value", "afspraak_carleen", times: 1);
    }
}
