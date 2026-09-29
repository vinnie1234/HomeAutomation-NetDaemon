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

    /// <summary>
    /// Whether Vincent has a day off: holiday at home (<c>holliday</c>) or on vacation (<c>onvacation</c>).
    /// Weekends are not included; use <see cref="IsDayOff"/> for that.
    /// </summary>
    public static bool IsOnLeave(IEntities entities) =>
        entities.InputBoolean.Holliday.IsOn() || entities.InputBoolean.Onvacation.IsOn();

    /// <summary>
    /// Whether the given day is a non-working day: weekend, holiday at home or on vacation.
    /// </summary>
    public static bool IsDayOff(IEntities entities, DayOfWeek dayOfWeek) =>
        WeekendDays.Contains(dayOfWeek) || IsOnLeave(entities);

    /// <summary>
    /// Whether the given day is a work-from-home day. Always false while on leave.
    /// </summary>
    public static bool IsHomeWorkDay(IEntities entities, DayOfWeek dayOfWeek)
    {
        if (IsOnLeave(entities)) return false;

        return dayOfWeek switch
        {
            DayOfWeek.Monday => entities.InputBoolean.OfficedayMonday.IsOff(),
            DayOfWeek.Tuesday => entities.InputBoolean.OfficedayTuesday.IsOff(),
            DayOfWeek.Wednesday => entities.InputBoolean.OfficedayWednesday.IsOff(),
            DayOfWeek.Thursday => entities.InputBoolean.OfficedayThursday.IsOff(),
            DayOfWeek.Friday => entities.InputBoolean.OfficedayFriday.IsOff(),
            _ => false
        };
    }

    /// <summary>
    /// Whether the given day is an office day. Always false while on leave.
    /// </summary>
    public static bool IsOfficeDay(IEntities entities, DayOfWeek dayOfWeek)
    {
        if (IsOnLeave(entities)) return false;

        return dayOfWeek switch
        {
            DayOfWeek.Monday    => entities.InputBoolean.OfficedayMonday.IsOn(),
            DayOfWeek.Tuesday   => entities.InputBoolean.OfficedayTuesday.IsOn(),
            DayOfWeek.Wednesday => entities.InputBoolean.OfficedayWednesday.IsOn(),
            DayOfWeek.Thursday  => entities.InputBoolean.OfficedayThursday.IsOn(),
            DayOfWeek.Friday    => entities.InputBoolean.OfficedayFriday.IsOn(),
            _                   => false
        };
    }

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