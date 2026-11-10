using System.Globalization;

namespace Automation.Models.Work;

/// <summary>What was counted for one day: the seconds the work laptop was on while Vincent worked, per place.</summary>
public sealed class WorkDay
{
    /// <summary>At home.</summary>
    public double Home { get; set; }

    /// <summary>At the office (in the zone of the place of work).</summary>
    public double Office { get; set; }

    /// <summary>Anywhere else (on the way, at a customer).</summary>
    public double Other { get; set; }

    /// <summary>When the laptop was first counted today, "HH:mm".</summary>
    public string? First { get; set; }

    /// <summary>When the laptop was counted for the last time today, "HH:mm".</summary>
    public string? Last { get; set; }
}

/// <summary>
/// The time worked of a day, a week or a month, in minutes. <see cref="Gross"/> is the time the laptop was on; <see cref="WithBreak"/>
/// has the lunch break taken off and <see cref="Net"/> the smoking breaks too (<see cref="WorkTimeReport"/>). <see cref="Home"/>,
/// <see cref="Office"/> and <see cref="Other"/> divide the gross time over the places.
/// </summary>
public sealed record WorkTotals(int Gross, int WithBreak, int Net, int Home, int Office, int Other)
{
    public static readonly WorkTotals Zero = new(0, 0, 0, 0, 0, 0);

    public static WorkTotals operator +(WorkTotals a, WorkTotals b) =>
        new(a.Gross + b.Gross, a.WithBreak + b.WithBreak, a.Net + b.Net, a.Home + b.Home, a.Office + b.Office, a.Other + b.Other);
}

/// <summary>A summary for the dashboard: the totals plus what they are about.</summary>
public sealed record WorkPeriod(string Key, string From, string To, WorkTotals Totals, string? First = null, string? Last = null);

/// <summary>The figures behind the work time on the dashboard: the breaks per day, and the sums per week and per calendar month.</summary>
public static class WorkTimeReport
{
    /// <summary>The lunch break taken off a day of real work.</summary>
    public const int LunchBreakMinutes = 30;

    /// <summary>The smoking breaks together, about.</summary>
    public const int SmokingMinutes = 20;

    /// <summary>
    /// From this much time worked the day has had its lunch and all of its smoking breaks. Before that the lunch is taken off once the day
    /// has gone past <see cref="LunchTime"/> (it started before it and the laptop was counted after it), and the smoking breaks are taken off
    /// in proportion to the time worked, so a day that is still running already shows what it will come to.
    /// </summary>
    public const int BreaksFromGrossMinutes = 4 * 60;

    /// <summary>The moment the lunch break lies at ("HH:mm"): a day that spans it has had its lunch.</summary>
    public const string LunchTime = "12:30";

    /// <summary>The totals of one day, with the breaks taken off. The breaks always go per day, also in the sums of weeks and months.</summary>
    public static WorkTotals Of(WorkDay day)
    {
        var home = Minutes(day.Home);
        var office = Minutes(day.Office);
        var other = Minutes(day.Other);
        var gross = home + office + other;
        var hadLunch = gross >= BreaksFromGrossMinutes
                       || (day.First is { } first && day.Last is { } last
                           && string.CompareOrdinal(first, LunchTime) < 0 && string.CompareOrdinal(last, LunchTime) >= 0);
        var withBreak = hadLunch ? Math.Max(0, gross - LunchBreakMinutes) : gross;
        var smoking = gross >= BreaksFromGrossMinutes ? SmokingMinutes : (int)Math.Round(SmokingMinutes * gross / (double)BreaksFromGrossMinutes, MidpointRounding.AwayFromZero);
        var net = Math.Max(0, withBreak - smoking);
        return new WorkTotals(gross, withBreak, net, home, office, other);
    }

    private static int Minutes(double seconds) => (int)Math.Round(seconds / 60, MidpointRounding.AwayFromZero);

    private static DateTime Parse(string key) => DateTime.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The days of the dictionary as periods, newest first.</summary>
    public static List<WorkPeriod> Days(IReadOnlyDictionary<string, WorkDay> days, int count) =>
        days.OrderByDescending(d => d.Key, StringComparer.Ordinal).Take(count)
            .Select(d => new WorkPeriod(d.Key, d.Key, d.Key, Of(d.Value), d.Value.First, d.Value.Last)).ToList();

    /// <summary>The weeks (Monday to Sunday, numbered as ISO weeks), newest first.</summary>
    public static List<WorkPeriod> Weeks(IReadOnlyDictionary<string, WorkDay> days, int count) =>
        days.GroupBy(d => ISOWeek.GetYear(Parse(d.Key)) * 100 + ISOWeek.GetWeekOfYear(Parse(d.Key)))
            .OrderByDescending(g => g.Key).Take(count)
            .Select(g =>
            {
                var monday = ISOWeek.ToDateTime(g.Key / 100, g.Key % 100, DayOfWeek.Monday);
                return new WorkPeriod($"{g.Key / 100}-W{g.Key % 100:00}", monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    monday.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g.Select(d => Of(d.Value)).Aggregate(WorkTotals.Zero, (a, b) => a + b));
            }).ToList();

    /// <summary>The calendar months (November, October, ...; not the last 30 days), newest first.</summary>
    public static List<WorkPeriod> Months(IReadOnlyDictionary<string, WorkDay> days, int count) =>
        days.GroupBy(d => d.Key[..7])
            .OrderByDescending(g => g.Key, StringComparer.Ordinal).Take(count)
            .Select(g =>
            {
                var first = new DateTime(int.Parse(g.Key[..4], CultureInfo.InvariantCulture), int.Parse(g.Key[5..], CultureInfo.InvariantCulture), 1);
                return new WorkPeriod(g.Key, first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    first.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), g.Select(d => Of(d.Value)).Aggregate(WorkTotals.Zero, (a, b) => a + b));
            }).ToList();
}
