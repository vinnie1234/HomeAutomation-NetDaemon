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

    public EntityState? GetState(string entityId) => inner.GetState(entityId);

    public IReadOnlyList<Entity> GetAllEntities() => inner.GetAllEntities();

    public IObservable<StateChange> StateAllChanges() => inner.StateAllChanges();

    public IObservable<Event> Events => inner.Events;

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
