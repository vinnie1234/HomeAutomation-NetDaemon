using System.Reactive.Concurrency;

namespace Automation.Core;

/// <summary>
/// Wraps the scheduler of one app: everything is scheduled as before, and what runs at the planned time gets a time trigger
/// (see <see cref="ActivityTrigger"/>), so an action that comes out of a schedule or timer can say so. A timer that was started while
/// something else was the trigger (a motion sensor, for example) keeps that: the action then says "5 min later, after: motion in ...".
/// </summary>
public sealed class ActivityScheduler(IScheduler inner) : IScheduler
{
    public DateTimeOffset Now => inner.Now;

    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action) =>
        inner.Schedule(state, action);

    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        var startedBy = StartedBy();
        return inner.Schedule(state, dueTime, (_, s) =>
        {
            using var scope = ActivityContext.Begin(ActivityTrigger.FromTime(startedBy, dueTime, null));
            return action(this, s);
        });
    }

    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        var startedBy = StartedBy();
        var wait = dueTime - inner.Now;
        return inner.Schedule(state, dueTime, (_, s) =>
        {
            using var scope = ActivityContext.Begin(ActivityTrigger.FromTime(startedBy, startedBy == null ? null : wait, dueTime));
            return action(this, s);
        });
    }

    /// <summary>
    /// What a new timer is started by: the trigger that is running. A timer started by another timer (a repeating one) is started by what
    /// started that one, so a chain of repeats does not grow.
    /// </summary>
    private static ActivityTrigger? StartedBy() =>
        ActivityContext.Current is { Kind: "tijd" } time ? time.Parent : ActivityContext.Current;
}
