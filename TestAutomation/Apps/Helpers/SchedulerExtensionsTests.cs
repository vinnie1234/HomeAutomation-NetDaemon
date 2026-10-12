using Automation.Extensions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace TestAutomation.Apps.Helpers;

public class SchedulerExtensionsTests
{
    private static TestScheduler SchedulerAt(DateTimeOffset utcTime)
    {
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(utcTime.UtcTicks);
        return scheduler;
    }

    /// <summary>
    /// Records the Dutch clock time of every run while advancing the scheduler.
    /// </summary>
    private static List<DateTimeOffset> RunsUntil(TestScheduler scheduler, DateTimeOffset utcEnd, Func<Action, IDisposable> schedule)
    {
        var runs = new List<DateTimeOffset>();
        using var _ = schedule(() => runs.Add(scheduler.NowNl()));
        scheduler.AdvanceTo(utcEnd.UtcTicks);
        return runs;
    }

    [Fact]
    public void NowNl_IsDutchTime_InSummerAndWinter()
    {
        // 12:00 UTC is 14:00 in summer (UTC+2) and 13:00 in winter (UTC+1)
        Assert.Equal(14, SchedulerAt(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)).NowNl().Hour);
        Assert.Equal(13, SchedulerAt(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)).NowNl().Hour);
    }

    [Fact]
    public void NowNl_UsesTheDutchDate_JustAfterMidnight()
    {
        // 23:30 UTC on Sunday is already 01:30 on Monday in the Netherlands
        var scheduler = SchedulerAt(new DateTimeOffset(2026, 6, 7, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(DayOfWeek.Monday, scheduler.NowNl().DayOfWeek);
    }

    [Fact]
    public void RunDaily_StaysAtTheSameDutchClockTime_WhenWinterTimeStarts()
    {
        // Winter time starts on Sunday 25 October 2026 at 03:00 (clocks go back to 02:00)
        var scheduler = SchedulerAt(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

        var runs = RunsUntil(scheduler, new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero),
            action => scheduler.RunDaily(new TimeSpan(7, 15, 0), action));

        Assert.Equal(5, runs.Count); // 24, 25, 26, 27 and 28 October
        Assert.All(runs, run => Assert.Equal(new TimeSpan(7, 15, 0), run.TimeOfDay));
    }

    [Fact]
    public void RunDaily_StaysAtTheSameDutchClockTime_WhenSummerTimeStarts()
    {
        // Summer time starts on Sunday 29 March 2026 at 02:00 (clocks go forward to 03:00)
        var scheduler = SchedulerAt(new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero));

        var runs = RunsUntil(scheduler, new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            action => scheduler.RunDaily(new TimeSpan(22, 0, 0), action));

        Assert.Equal(5, runs.Count); // 27, 28, 29, 30 and 31 March
        Assert.All(runs, run => Assert.Equal(new TimeSpan(22, 0, 0), run.TimeOfDay));
    }

    [Fact]
    public void ScheduleCronNl_IsEvaluatedInDutchTime()
    {
        // 22:00 Dutch summer time is 20:00 UTC
        var scheduler = SchedulerAt(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

        var runs = RunsUntil(scheduler, new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero),
            action => scheduler.ScheduleCronNl("00 22 * * *", action));

        var run = Assert.Single(runs);
        Assert.Equal(22, run.Hour);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 20, 0, 0, TimeSpan.Zero), run.ToUniversalTime());
    }

    [Fact]
    public void ScheduleCronNl_SupportsSeconds()
    {
        var scheduler = SchedulerAt(new DateTimeOffset(2026, 12, 31, 12, 0, 0, TimeSpan.Zero));

        var runs = RunsUntil(scheduler, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            action => scheduler.ScheduleCronNl("10 58 23 31 12 *", action, hasSeconds: true));

        Assert.Equal(new TimeSpan(23, 58, 10), Assert.Single(runs).TimeOfDay);
    }
}
