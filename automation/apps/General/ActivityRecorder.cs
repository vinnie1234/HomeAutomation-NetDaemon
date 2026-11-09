using System.Reactive.Concurrency;
using System.Threading.Channels;
using Automation.Configuration;
using Automation.Core;
using Microsoft.Extensions.Options;
using NetDaemon.Extensions.MqttEntityManager;
using EntityCreationOptions = Automation.Interfaces.EntityCreationOptions;

namespace Automation.apps.General;

/// <summary>
/// Turns the actions of NetDaemon (the <c>netdaemon_activity</c> events of <see cref="ActivityLog"/>) into the sensor
/// <c>sensor.netdaemon_activiteit</c>, which NetDaemon creates itself through MQTT like its other sensors. Home Assistant keeps the
/// states of that sensor in its history, and the dashboard page Activiteit reads that history.
/// <para>
/// One action is one state: the state is the unique id of the action and the attributes hold the rest. MQTT sets the attributes and the
/// state in two messages, so the attributes go first and then the state: only the second state has both of the same action (the first has
/// the new attributes with the old id, which the dashboard skips). The actions are written one by one, in the order they came.
/// </para>
/// </summary>
[NetDaemonApp(Id = nameof(ActivityRecorder))]
public sealed class ActivityRecorder : BaseApp
{
    public const string Sensor = "sensor.netdaemon_activiteit";

    private readonly Channel<JsonElement> _queue = Channel.CreateUnbounded<JsonElement>(new UnboundedChannelOptions { SingleReader = true });
    private readonly IEntityManager _entityManager;
    private readonly IMqttEntityManager _mqtt;
    private readonly HashSet<string> _written = [];

    public ActivityRecorder(
        IHaContext ha,
        ILogger<ActivityRecorder> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IEntityManager entityManager,
        IMqttEntityManager mqtt,
        ActivityBacklog? backlog = null)
        : base(ha, logger, notify, scheduler, config)
    {
        _entityManager = entityManager;
        _mqtt = mqtt;

        HaContext.Events
            .Where(e => e.EventType == ActivityLog.EventType && e.DataElement is { ValueKind: JsonValueKind.Object })
            .Subscribe(e => _queue.Writer.TryWrite(e.DataElement!.Value.Clone()));

        // What happened before this app was listening (the apps start in no fixed order); an action in both places is written once
        foreach (var early in (backlog ?? ActivityLog.Backlog).Drain()) _queue.Writer.TryWrite(early);

        _ = Task.Run(WriteAsync);
    }

    private async Task WriteAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(Sensor))
                await _entityManager.Create(Sensor, new EntityCreationOptions { Name = "NetDaemon activiteit", Icon = "mdi:history" });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {Sensor}", Sensor);
        }

        await foreach (var entry in _queue.Reader.ReadAllAsync())
        {
            try
            {
                var id = entry.TryGetProperty("id", out var i) ? i.GetString() : null;
                if (string.IsNullOrEmpty(id) || !_written.Add(id)) continue;
                if (_written.Count > 5000) _written.Clear();
                await _mqtt.SetAttributesAsync(Sensor, entry);
                await _mqtt.SetStateAsync(Sensor, id);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not write an action to {Sensor}", Sensor);
            }
        }
    }
}
