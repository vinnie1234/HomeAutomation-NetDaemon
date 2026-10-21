using Automation.Enum;

namespace Automation.Models.Persons;

/// <summary>
/// The work schedule of one person, from their own helpers (created by <see cref="apps.General.HelperManager"/>):
/// <list type="bullet">
/// <item><c>input_select.werkdag_{person}_{dag}</c>: per day of the week (weekend included) Vrij, Thuis or Kantoor</item>
/// <item><c>input_boolean.vrij_{person}</c>: a day off now and then (holiday at home)</item>
/// <item><c>input_datetime.opstaan_kantoor_{person}</c> / <c>opstaan_thuiswerk_{person}</c>: when the house wakes up</item>
/// <item><c>input_datetime.werk_start_{person}</c> / <c>werk_eind_{person}</c>: the working hours</item>
/// <item><c>input_boolean.working{person}</c>: whether the person is working right now</item>
/// </list>
/// A day off and being on vacation (<c>input_boolean.onvacation</c>, shared) are always days off.
/// </summary>
public class WorkSchedule
{
    /// <summary>The day names the helpers use.</summary>
    public static readonly IReadOnlyDictionary<DayOfWeek, string> DayNames = new Dictionary<DayOfWeek, string>
    {
        [DayOfWeek.Monday] = "maandag",
        [DayOfWeek.Tuesday] = "dinsdag",
        [DayOfWeek.Wednesday] = "woensdag",
        [DayOfWeek.Thursday] = "donderdag",
        [DayOfWeek.Friday] = "vrijdag",
        [DayOfWeek.Saturday] = "zaterdag",
        [DayOfWeek.Sunday] = "zondag"
    };

    public const string Off = "Vrij";
    public const string Home = "Thuis";
    public const string Office = "Kantoor";

    private readonly IHaContext _haContext;
    private readonly string _person;

    /// <param name="haContext">The Home Assistant context.</param>
    /// <param name="person">The person as used in the helper ids: <c>vincent</c> or <c>carleen</c>.</param>
    public WorkSchedule(IHaContext haContext, string person)
    {
        _haContext = haContext;
        _person = person;
    }

    public InputBooleanEntity Working => new(_haContext, $"input_boolean.working{_person}");
    public InputBooleanEntity DayOff => new(_haContext, $"input_boolean.vrij_{_person}");
    public InputDatetimeEntity WakeUpOffice => new(_haContext, $"input_datetime.opstaan_kantoor_{_person}");
    public InputDatetimeEntity WakeUpHomeWork => new(_haContext, $"input_datetime.opstaan_thuiswerk_{_person}");
    public InputDatetimeEntity WorkStart => new(_haContext, $"input_datetime.werk_start_{_person}");
    public InputDatetimeEntity WorkEnd => new(_haContext, $"input_datetime.werk_eind_{_person}");

    /// <summary>The setting of a day of the week.</summary>
    public InputSelectEntity WorkDaySetting(DayOfWeek day) => new(_haContext, $"input_select.werkdag_{_person}_{DayNames[day]}");

    /// <summary>Whether the person has a day off or is on vacation, regardless of the weekday.</summary>
    public bool IsOnLeave => DayOff.IsOn() || new InputBooleanEntity(_haContext, "input_boolean.onvacation").IsOn();

    /// <summary>What kind of day the given weekday is. Unknown settings count as a day off, so nothing starts by mistake.</summary>
    public WorkDay GetWorkDay(DayOfWeek day)
    {
        if (IsOnLeave) return WorkDay.Off;

        return WorkDaySetting(day).State switch
        {
            Office => WorkDay.Office,
            Home => WorkDay.Home,
            _ => WorkDay.Off
        };
    }

    public bool IsOfficeDay(DayOfWeek day) => GetWorkDay(day) == WorkDay.Office;
    public bool IsHomeWorkDay(DayOfWeek day) => GetWorkDay(day) == WorkDay.Home;
    public bool IsDayOff(DayOfWeek day) => GetWorkDay(day) == WorkDay.Off;
}
