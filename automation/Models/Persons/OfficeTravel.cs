namespace Automation.Models.Persons;

/// <summary>
/// The choices of when somebody travels to and from the office (<c>input_select.reis_ochtend_soort_{person}</c> and
/// <c>reis_avond_soort_{person}</c>): in the morning a time to be at the office or a time to leave home, in the evening a time to leave
/// the office or a time to be home. A time to be somewhere gets the advice when to leave; a time to leave needs none.
/// </summary>
public static class OfficeTravel
{
    public const string MorningArrive = "Op kantoor zijn";
    public const string MorningDepart = "Van huis vertrekken";
    public const string EveningLeave = "Van kantoor vertrekken";
    public const string EveningArrive = "Thuis zijn";
}
