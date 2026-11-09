using Automation.Enum;
using Automation.Models.Persons;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Models;

public class WorkScheduleTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    private WorkSchedule Schedule(string dayOff = "off", string onVacation = "off")
    {
        SetState("input_boolean.vrij_carleen", dayOff);
        SetState("input_boolean.onvacation", onVacation);
        SetState("input_select.werkdag_carleen_maandag", "Kantoor");
        SetState("input_select.werkdag_carleen_dinsdag", "Thuis");
        SetState("input_select.werkdag_carleen_woensdag", "Vrij");
        return new WorkSchedule(_ctx.HaContext, "carleen");
    }

    [Fact]
    public void WorkDays_FollowTheSettingPerWeekday()
    {
        var schedule = Schedule();

        Assert.Equal(WorkDay.Office, schedule.GetWorkDay(DayOfWeek.Monday));
        Assert.Equal(WorkDay.Home, schedule.GetWorkDay(DayOfWeek.Tuesday));
        Assert.Equal(WorkDay.Off, schedule.GetWorkDay(DayOfWeek.Wednesday));
        Assert.True(schedule.IsOfficeDay(DayOfWeek.Monday));
        Assert.True(schedule.IsHomeWorkDay(DayOfWeek.Tuesday));
    }

    [Fact]
    public void Weekend_FollowsItsSettingToo()
    {
        var schedule = Schedule();
        SetState("input_select.werkdag_carleen_zaterdag", "Kantoor");
        SetState("input_select.werkdag_carleen_zondag", "Vrij");

        Assert.True(schedule.IsOfficeDay(DayOfWeek.Saturday));
        Assert.True(schedule.IsDayOff(DayOfWeek.Sunday));
    }

    [Fact]
    public void UnknownSetting_IsADayOff_SoNothingStartsByMistake()
    {
        var schedule = Schedule();

        // Thursday has no state in Home Assistant (e.g. the helper doesn't exist yet)
        Assert.True(schedule.IsDayOff(DayOfWeek.Thursday));
    }

    [Theory]
    [InlineData("on", "off")]
    [InlineData("off", "on")]
    public void DayOffOrVacation_MakesEveryDayADayOff(string dayOff, string onVacation)
    {
        var schedule = Schedule(dayOff, onVacation);

        Assert.True(schedule.IsDayOff(DayOfWeek.Monday));
        Assert.False(schedule.IsOfficeDay(DayOfWeek.Monday));
        Assert.False(schedule.IsHomeWorkDay(DayOfWeek.Tuesday));
    }

    // ---- how to get to the office today

    [Theory]
    [InlineData("Volgens schema", "OV", "OV")]
    [InlineData("Volgens schema", "Auto", "Auto")]
    [InlineData("Auto", "OV", "Auto")]
    [InlineData("OV", "Auto", "OV")]
    [InlineData("unknown", "OV", "OV")]
    public void TransportToday_IsWhatWasChosenForToday_OtherwiseTheSettingOfTheWeekday(string today, string weekday, string expected)
    {
        var schedule = Schedule();
        SetState("input_select.vervoer_vandaag_carleen", today);
        SetState("input_select.vervoer_carleen_maandag", weekday);

        Assert.Equal(expected, schedule.TransportToday(DayOfWeek.Monday));
    }
}
