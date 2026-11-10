using Automation.apps.General.Work;

namespace Automation.Models.Persons;

/// <summary>
/// A work day taken from the calendar (see <see cref="WorkFromCalendar"/>): the weekday of the person was set to
/// Kantoor for that date, and is set back to Vrij the night after. The times are those of the event, when it has any.
/// </summary>
public class WorkCalendarEntry
{
    /// <summary>The person as used in the helper ids: <c>vincent</c> or <c>carleen</c>.</summary>
    public string Person { get; set; } = "";

    /// <summary>The date of the work day, <c>yyyy-MM-dd</c>.</summary>
    public string Date { get; set; } = "";

    /// <summary>When the person has to be at the office, <c>HH:mm</c>, or null when the event has no time.</summary>
    public string? Start { get; set; }

    /// <summary>When the person is done, <c>HH:mm</c>, or null when the event has no time.</summary>
    public string? End { get; set; }

    /// <summary><see cref="WorkKind"/> for a work day, <see cref="DayOffKind"/> for a day the calendar made Vrij.</summary>
    public string Kind { get; set; } = WorkKind;

    /// <summary>For a day off: what the weekday was set to before, which it goes back to when the day is over.</summary>
    public string? Previous { get; set; }

    public const string WorkKind = "werk";
    public const string DayOffKind = "vrij";
}

/// <summary>The work days taken from the calendar that are not over yet.</summary>
public class WorkCalendarStore
{
    public List<WorkCalendarEntry> Entries { get; set; } = [];
}
