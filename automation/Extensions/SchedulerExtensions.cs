using System.Reactive.Concurrency;

namespace Automation.Extensions;

public static class SchedulerExtensions
{
    /// <summary>
    /// Schedules an action to run daily at a specified time of day.
    /// </summary>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    /// <param name="timeOfDay">The time of day to run the action.</param>
    /// <param name="action">The action to run daily.</param>
    /// <returns>An IDisposable object that can be used to cancel the scheduled action.</returns>
    public static IDisposable RunDaily(this IScheduler scheduler, TimeSpan timeOfDay, Action action)
    {
        var startTime = scheduler.Now.Date.Add(timeOfDay);
        if (scheduler.Now > startTime) startTime = startTime.AddDays(1);

        return scheduler.RunEvery(TimeSpan.FromDays(1), startTime, action);
    }
}