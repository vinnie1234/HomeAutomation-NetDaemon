using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class HouseStateManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldSetHouseStateToDayWhenSunIsAboveHorizon()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.ChangeStateFor("sun.sun")
            .FromState("below_horizon")
            .ToState("above_horizon");

        _ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
    }

    [Fact]
    public void ShouldSetHouseStateToEveningWhenSunIsBelowHorizon()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.ChangeStateFor("sun.sun")
            .FromState("above_horizon")
            .ToState("below_horizon");

        _ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void ShouldSetHouseStateToNightWhenSleepingTurnsOn()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.ChangeStateFor("input_boolean.sleeping")
            .FromState("off")
            .ToState("on");

        _ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }

    [Fact]
    public void ShouldSetHouseStateToMorningWhenSunIsAboveHorizon()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.ChangeStateFor("sun.sun")
            .FromState("below_horizon")
            .ToState("above_horizon");

        _ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Morning");
    }

    [Fact]
    public void ShouldTurnOnWorkingAtStartWorkingTime()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.SetCurrentTime(DateTime.Parse("08:30:00"));

        _ctx.VerifyCallService("input_boolean", "turn_on", "working", 1);
    }

    [Fact]
    public void ShouldTurnOffWorkingAtEndWorkingTime()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.SetCurrentTime(DateTime.Parse("17:00:00"));

        _ctx.VerifyCallService("input_boolean", "turn_off", "working", 1);
    }

    [Fact]
    public void ShouldSetHouseStateToDayOnWeekendAtDaytimeWeekend()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.SetCurrentTime(DateTime.Parse("10:00:00"));
        _ctx.ChangeStateFor("input_boolean.holliday")
            .FromState("off")
            .ToState("on");

        _ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
    }

    [Fact]
    public void ShouldSetHouseStateToNightOnWeekdayAtNighttimeWeekdays()
    {
        _ctx.InitApp<HouseStateManager>();
        _ctx.SetCurrentTime(DateTime.Parse("00:00:00"));

        _ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }

    [Fact]
    public void ShouldSetHouseStateToNightOnWeekendAtNighttimeWeekends()
    {
        var friday = DateTime.Today.AddDays(((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7);
        _ctx.SetCurrentTime(friday.AddHours(00).AddMinutes(29).AddSeconds(59));
        _ctx.InitApp<HouseStateManager>();
        
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        
        _ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }
}