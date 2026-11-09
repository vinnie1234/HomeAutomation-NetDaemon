using System.Globalization;

namespace Automation.Models.Persons;

/// <summary>The working hours of today when they come from the calendar.</summary>
public static class WorkHours
{
    private static readonly TimeSpan DefaultStart = new(8, 30, 0);
    private static readonly TimeSpan DefaultEnd = new(17, 0, 0);

    /// <summary>
    /// After this time in the evening the laptop is private: it never counts as work (not for the work time, and it does not keep him
    /// "working" past the end of the hours), whatever the margin after the end says.
    /// </summary>
    public static readonly TimeSpan LatestEnd = new(18, 30, 0);

    /// <summary>The working hours of today: from the calendar when the work comes from it, otherwise the hours of the person.</summary>
    public static (DateTime Start, DateTime End) Today(IHaContext ha, WorkSchedule schedule, string person, DateTime now)
    {
        if (FromCalendar(ha, person, now) is { } calendar) return calendar;
        DateTime At(string? state, TimeSpan fallback) => now.Date + (Extensions.SchedulerExtensions.ParseTimeOfDay(state) ?? fallback);
        return (At(schedule.WorkStart.State, DefaultStart), At(schedule.WorkEnd.State, DefaultEnd));
    }

    /// <summary>
    /// From the time to be at the office plus the margin before it, up to the time to leave minus the margin after it (see
    /// <see cref="OfficeTimes"/>). Null when the work does not come from the calendar or today has no hours from it.
    /// </summary>
    public static (DateTime Start, DateTime End)? FromCalendar(IHaContext ha, string person, DateTime now)
    {
        if (ha.GetState($"input_boolean.werk_uit_agenda_{person}")?.State != "on") return null;
        var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        DateTime? Read(string entity)
        {
            var state = ha.GetState($"input_datetime.{entity}_{person}")?.State;
            return state != null && state.StartsWith(today, StringComparison.Ordinal)
                   && DateTime.TryParse(state, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
        }

        double Minutes(string helper) =>
            double.TryParse(ha.GetState($"input_number.{helper}_{person}")?.State, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 10;

        if (Read("kantoor_aankomst") is not { } arrive || Read("kantoor_vertrek") is not { } leave) return null;
        return (arrive.AddMinutes(Minutes("reis_marge_voor")), leave.AddMinutes(-Minutes("reis_marge_na")));
    }
}
