
namespace Automation.Helpers;

/// <summary>
/// Whether the PetSnowy litter box has cleaned "today", for the checks in <see cref="apps.General.Alarm"/> (22:00) and
/// <see cref="apps.General.SleepManager"/> (bedtime). The day does not start at midnight but at 06:00: we go to bed after
/// midnight, and by the calendar date the box would never have cleaned "today" yet, so every night a message came that
/// there was something wrong.
/// </summary>
public static class PetSnowyCleaning
{
    /// <summary>The storage key of the day (see <see cref="DayKey"/>) of the last cleaning.</summary>
    public const string LastCleanedDateKey = "PetsnowyLastCleanedDate";

    /// <summary>How long after midnight the previous day still counts.</summary>
    private static readonly TimeSpan NewDayStartsAt = TimeSpan.FromHours(6);

    /// <summary>The day that <paramref name="nowNl"/> belongs to, as it is stored.</summary>
    public static string DayKey(DateTimeOffset nowNl) => (nowNl - NewDayStartsAt).Date.ToString("O");

    /// <summary>Saves that the box cleaned at <paramref name="nowNl"/>.</summary>
    public static void MarkCleaned(IDataRepository storage, DateTimeOffset nowNl) => storage.Save(LastCleanedDateKey, DayKey(nowNl));

    /// <summary>Whether the box has cleaned on the day <paramref name="nowNl"/> belongs to.</summary>
    public static bool IsCleanedToday(IDataRepository storage, DateTimeOffset nowNl) =>
        storage.Get<string>(LastCleanedDateKey) == DayKey(nowNl);
}
