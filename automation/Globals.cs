using Automation.Enum;

namespace Automation;

public static class Globals
{
    #region DayOfWeekConfig

    public static readonly DayOfWeek[] WeekdayNightDays =
    [
        DayOfWeek.Sunday,
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday
    ];

    public static readonly DayOfWeek[] WeekendNightDays =
    [
        DayOfWeek.Friday,
        DayOfWeek.Saturday
    ];

    public static readonly DayOfWeek[] WeekendDays =
    [
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    #endregion

    public static HouseState GetHouseState(IEntities entities)
    {
        var state = entities.InputSelect.Housemodeselect.State;
        return (state is null or "unknown" or "unavailable" ? "Day" : state)
            switch
            {
                "Morning" => HouseState.Morning,
                "Day"     => HouseState.Day,
                "Evening" => HouseState.Evening,
                "Night"   => HouseState.Night,
                _         => HouseState.Day
            };
    }
}