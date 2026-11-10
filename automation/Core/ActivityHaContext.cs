using System.Reactive.Linq;
using System.Text.Json;
using NetDaemon.HassModel.Entities;

namespace Automation.Core;

/// <summary>
/// Wraps the Home Assistant context of one app: everything works as before, and every service call the app makes is also written to the
/// activity log (<see cref="ActivityLog"/>) under the name of that app.
/// </summary>
public sealed class ActivityHaContext(IHaContext inner, string app, IReadOnlyDictionary<string, string>? knownPeople = null, string? appRoom = null) : IHaContext
{
    public IHaContext Inner => inner;

    /// <summary>The state as before; while an app handles a trigger every state it looks at is a condition of what it does next.</summary>
    public EntityState? GetState(string entityId)
    {
        var state = inner.GetState(entityId);
        ActivityContext.Current?.Read(entityId, state?.State);
        return state;
    }

    public IReadOnlyList<Entity> GetAllEntities() => inner.GetAllEntities();

    /// <summary>The state changes as before; while an app handles one it is the trigger of what the app does (and of timers it starts).</summary>
    public IObservable<StateChange> StateAllChanges() =>
        Observable.Create<StateChange>(observer => inner.StateAllChanges().Subscribe(
            change =>
            {
                // (An observable that is not a real one, as in tests, can hand over nothing: that is passed on to nobody)
                if ((object?)change == null) return;
                using var scope = ActivityContext.Begin(ActivityTrigger.FromState(change));
                observer.OnNext(change);
            },
            observer.OnError,
            observer.OnCompleted));

    /// <summary>The events as before; the ones that are a reason for something (a button, a message) are the trigger while an app handles them.</summary>
    public IObservable<Event> Events =>
        Observable.Create<Event>(observer => inner.Events.Subscribe(
            e =>
            {
                if ((object?)e == null) return;
                if (Array.IndexOf(ActivityContext.IgnoredEvents, e.EventType) >= 0)
                {
                    observer.OnNext(e);
                    return;
                }
                using var scope = ActivityContext.Begin(ActivityTrigger.FromEvent(e));
                observer.OnNext(e);
            },
            observer.OnError,
            observer.OnCompleted));

    public Area? GetAreaFromEntityId(string entityId) => inner.GetAreaFromEntityId(entityId);

    public EntityRegistration? GetEntityRegistration(string entityId) => inner.GetEntityRegistration(entityId);

    public void CallService(string domain, string service, ServiceTarget? target = null, object? data = null)
    {
        inner.CallService(domain, service, target, data);
        ActivityLog.Service(inner, app, domain, service, target, data, knownPeople, appRoom);
    }

    public async Task<JsonElement?> CallServiceWithResponseAsync(string domain, string service, ServiceTarget? target = null, object? data = null)
    {
        var response = await inner.CallServiceWithResponseAsync(domain, service, target, data);
        ActivityLog.Service(inner, app, domain, service, target, data, knownPeople, appRoom);
        return response;
    }

    public void SendEvent(string eventType, object? data = null) => inner.SendEvent(eventType, data);
}
