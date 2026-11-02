using System.Text.Json;
using Automation.apps.General;
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

public class OfficeTimesTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    /// <summary>Starts the app on Thursday 29 October 2026 at the given Dutch time (winter time, UTC+1).</summary>
    private void Start(int hour, int minute, string thursday = "Kantoor", string friday = "Vrij")
    {
        SetState("input_select.werkdag_vincent_donderdag", thursday);
        SetState("input_select.werkdag_vincent_vrijdag", friday);
        SetState("input_boolean.vrij_vincent", "off");
        SetState("input_boolean.onvacation", "off");
        SetState("input_datetime.werk_start_vincent", "08:30:00");
        SetState("input_datetime.werk_eind_vincent", "17:00:00");
        _ctx.SetCurrentTime(new DateTime(2026, 10, 29, hour - 1, minute, 0, DateTimeKind.Utc));
        _ = new OfficeTimes(_ctx.HaContext, Substitute.For<ILogger<OfficeTimes>>(), Substitute.For<INotify>(), _ctx.Scheduler, _storage, Options.Create(new AppConfig()));
    }

    /// <summary>The <c>datetime</c> values the app set on an entity, in order.</summary>
    private List<string> SetTo(string entityId) =>
        _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "set_datetime"
                        && (c.GetArguments()[2] as ServiceTarget)?.EntityIds?.Contains(entityId) == true)
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("datetime").GetString()!)
            .ToList();

    [Fact]
    public void OfficeDay_SetsTenMinutesBeforeTheStart_AndTenMinutesAfterTheEnd()
    {
        Start(7, 0);

        SetTo("input_datetime.kantoor_aankomst_vincent").Should().Equal("2026-10-29 08:20:00");
        SetTo("input_datetime.kantoor_vertrek_vincent").Should().Equal("2026-10-29 17:10:00");
    }

    [Fact]
    public void AfterTheDepartureTime_ItShowsTheNextOfficeDay()
    {
        Start(18, 0, friday: "Kantoor");

        SetTo("input_datetime.kantoor_aankomst_vincent").Should().Equal("2026-10-30 08:20:00");
        SetTo("input_datetime.kantoor_vertrek_vincent").Should().Equal("2026-10-30 17:10:00");
    }

    [Fact]
    public void TheTimesOfTheCalendarEvent_ReplaceTheWorkHours()
    {
        _storage.Get<WorkCalendarStore>(WorkFromCalendar.StoreKey).Returns(new WorkCalendarStore
        {
            Entries = [new WorkCalendarEntry { Person = "vincent", Date = "2026-10-29", Start = "14:00", End = "22:00" }]
        });

        Start(7, 0);

        SetTo("input_datetime.kantoor_aankomst_vincent").Should().Equal("2026-10-29 13:50:00");
        SetTo("input_datetime.kantoor_vertrek_vincent").Should().Equal("2026-10-29 22:10:00");
    }

    [Fact]
    public void NothingChanged_IsNotSetAgain()
    {
        Start(7, 0);
        SetState("input_datetime.kantoor_aankomst_vincent", "2026-10-29 08:20:00");
        SetState("input_datetime.kantoor_vertrek_vincent", "2026-10-29 17:10:00");
        _ctx.HaContext.ClearReceivedCalls();

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(6).Ticks);

        SetTo("input_datetime.kantoor_aankomst_vincent").Should().BeEmpty();
        SetTo("input_datetime.kantoor_vertrek_vincent").Should().BeEmpty();
    }

    [Fact]
    public void NoOfficeDayAtAll_SetsNothing()
    {
        Start(7, 0, thursday: "Thuis");

        SetTo("input_datetime.kantoor_aankomst_vincent").Should().BeEmpty();
    }
}
