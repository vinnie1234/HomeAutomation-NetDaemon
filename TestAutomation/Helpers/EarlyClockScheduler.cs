using System.Reactive.Concurrency;
using Microsoft.Reactive.Testing;

namespace TestAutomation.Helpers;

/// <summary>
/// A scheduler whose clock lags a little behind the <see cref="TestScheduler"/> it runs on, so a planned action sees a
/// "now" just before its due time. Real timers can fire a few milliseconds early in the same way.
/// </summary>
/// <param name="inner">The test scheduler that actually runs the actions.</param>
/// <param name="lag">How far <see cref="Now"/> lags behind the inner scheduler's clock.</param>
public sealed class EarlyClockScheduler(TestScheduler inner, TimeSpan lag) : IScheduler
{
    /// <inheritdoc />
    public DateTimeOffset Now => inner.Now - lag;

    /// <inheritdoc />
    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action) =>
        inner.Schedule(state, (_, s) => action(this, s));

    /// <inheritdoc />
    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action) =>
        inner.Schedule(state, dueTime, (_, s) => action(this, s));

    /// <inheritdoc />
    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action) =>
        inner.Schedule(state, dueTime, (_, s) => action(this, s));
}
