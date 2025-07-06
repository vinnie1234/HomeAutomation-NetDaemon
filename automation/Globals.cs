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

    public static bool IsHomeWorkDay(IEntities entities, DayOfWeek dayOfWeek)
    {
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


    public static bool IsOfficeDay(IEntities entities, DayOfWeek dayOfWeek)
    {
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
        return (entities.InputSelect.Housemodeselect.State ?? "Day")
            switch
            {
                "Morning" => HouseState.Morning,
                "Day"     => HouseState.Day,
                "Evening" => HouseState.Evening,
                "Night"   => HouseState.Night,
                _         => HouseState.Day
            };
    }

    public static bool AmIHomeCheck(Entities entities)
    {
        return (entities.Person.VincentMaarschalkerweerd.State ?? "not_home") != "home" || entities.InputBoolean.Onvacation.IsOn() && (entities.Person.Timo.State ?? "not_home") != "home";
    }
}