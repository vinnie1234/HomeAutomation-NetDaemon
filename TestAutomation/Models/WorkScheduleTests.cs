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
    public void Weekend_IsAlwaysADayOff()
    {
        var schedule = Schedule();

        Assert.True(schedule.IsDayOff(DayOfWeek.Saturday));
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
}
