using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Cronos;

namespace Automation.Extensions;

/// <summary>
/// Scheduling and clock helpers that always work in Dutch time (Europe/Amsterdam), including
/// summer/winter time, regardless of the time zone of the machine or container NetDaemon runs in.
/// Use these instead of <see cref="DateTime.Now"/>, <see cref="DateTimeOffset.Now"/> and
/// <see cref="IScheduler.Now"/> (which is UTC) whenever a time of day or day of week matters.
/// </summary>
public static class SchedulerExtensions
{
    /// <summary>
    /// The Dutch time zone.
    /// </summary>
    public static readonly TimeZoneInfo DutchTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

    /// <summary>
    /// Gets the current Dutch time of the scheduler.
    /// </summary>
    /// <param name="scheduler">The scheduler providing the current time.</param>
    /// <returns>The current time, with the Dutch UTC offset.</returns>
    public static DateTimeOffset NowNl(this IScheduler scheduler) => ToDutchTime(scheduler.Now);

    /// <summary>
    /// Converts a point in time to Dutch time.
    /// </summary>
    /// <param name="time">The point in time.</param>
    /// <returns>The same point in time, with the Dutch UTC offset.</returns>
    public static DateTimeOffset ToDutchTime(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, DutchTimeZone);

    /// <summary>
    /// Converts a UTC or local <see cref="DateTime"/> to the Dutch clock time.
    /// </summary>
    /// <param name="time">The time; its <see cref="DateTime.Kind"/> tells whether it is UTC or local.</param>
    /// <returns>The Dutch clock time.</returns>
    public static DateTime ToDutchTime(DateTime time) => TimeZoneInfo.ConvertTime(time, DutchTimeZone);

    /// <summary>
    /// Schedules an action to run daily at a specified Dutch time of day.
    /// Stays at the same clock time when summer/winter time changes.
    /// </summary>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    /// <param name="timeOfDay">The Dutch time of day to run the action.</param>
    /// <param name="action">The action to run daily.</param>
    /// <returns>An IDisposable object that can be used to cancel the scheduled action.</returns>
    public static IDisposable RunDaily(this IScheduler scheduler, TimeSpan timeOfDay, Action action) =>
        scheduler.ScheduleCronNl($"{timeOfDay.Seconds} {timeOfDay.Minutes} {timeOfDay.Hours} * * *", action, hasSeconds: true);

    /// <summary>
    /// Schedules an action to run daily at the Dutch time of day set in an <c>input_datetime</c>, and
    /// reschedules it whenever that time is changed in Home Assistant.
    /// </summary>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    /// <param name="timeOfDay">The input_datetime holding the time of day (e.g. "07:15:00").</param>
    /// <param name="fallback">The time to use while the input_datetime has no valid time;
    /// <c>null</c> to not run at all until it has one.</param>
    /// <param name="action">The action to run daily.</param>
    /// <returns>An IDisposable object that can be used to cancel the scheduled action.</returns>
    public static IDisposable RunDailyAt(this IScheduler scheduler, InputDatetimeEntity timeOfDay, TimeSpan? fallback, Action action)
    {
        var schedule = new SerialDisposable();

        void Plan(string? state)
        {
            var time = ParseTimeOfDay(state) ?? fallback;
            schedule.Disposable = time is null ? Disposable.Empty : scheduler.RunDaily(time.Value, action);
        }

        Plan(timeOfDay.State);
        var changes = timeOfDay.StateChanges().Subscribe(change => Plan(change.New?.State));

        return new CompositeDisposable(schedule, changes);
    }

    /// <summary>
    /// Parses a time of day such as "07:15:00".
    /// </summary>
    /// <param name="value">The value to parse, e.g. the state of an input_datetime.</param>
    /// <returns>The time of day, or <c>null</c> when the value isn't a valid time of day (e.g. "unknown").</returns>
    public static TimeSpan? ParseTimeOfDay(string? value) =>
        TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1)
            ? time
            : null;

    /// <summary>
    /// Schedules an action using a cron expression that is evaluated in Dutch time.
    /// </summary>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    /// <param name="cronExpression">The cron expression, e.g. <c>"00 22 * * *"</c> for 22:00 every day.</param>
    /// <param name="action">The action to run.</param>
    /// <param name="hasSeconds">Whether the expression starts with a seconds field.</param>
    /// <returns>An IDisposable object that can be used to cancel the scheduled action.</returns>
    public static IDisposable ScheduleCronNl(this IScheduler scheduler, string cronExpression, Action action, bool hasSeconds = false)
    {
        var cron = CronExpression.Parse(cronExpression, hasSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard);
        var nextRun = new SerialDisposable();
        DateTimeOffset? planned = null;

        void ScheduleNext()
        {
            // A timer can fire a few milliseconds early, while the clock is still just before the planned time.
            // Searching from "now" would then find the same occurrence again and run the action twice (or more),
            // so search from the planned time of the previous run when the clock hasn't passed it yet.
            var from = planned is { } previous && previous > scheduler.Now ? previous : scheduler.Now;
            var next = cron.GetNextOccurrence(from, DutchTimeZone);
            if (next is null) return;
            planned = next;

            nextRun.Disposable = scheduler.Schedule(next.Value, () =>
            {
                // Schedule the next run first, so a failing action doesn't stop the schedule
                ScheduleNext();
                action();
            });
        }

        ScheduleNext();
        return nextRun;
    }
}
