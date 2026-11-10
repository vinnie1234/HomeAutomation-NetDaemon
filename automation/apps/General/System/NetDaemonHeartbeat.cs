using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Tells Home Assistant every minute that NetDaemon is alive, by setting <c>input_datetime.netdaemon_heartbeat</c> to the
/// current time. NetDaemon cannot report that it is down, so Home Assistant does the watching: the template sensor
/// <c>binary_sensor.netdaemon_online</c> (see <c>dashboard/ha/template.yaml</c>) turns off when the beat is missed for
/// a few minutes, and the dashboard then shows an alert with a button that restarts the add-on.
/// The beat goes over the same connection every app uses, so a lost connection to Home Assistant is noticed too.
/// <para>
/// The sensors NetDaemon creates (<c>sensor.pixel_medicijnen</c>, ...) go through MQTT, which can fail without a trace: when
/// the broker refuses the login, publishing just stops and every app carries on as if nothing happened. So a second beat
/// goes over MQTT, as <c>sensor.netdaemon_mqtt_heartbeat</c>; <c>binary_sensor.netdaemon_mqtt_ok</c> turns off when it
/// becomes stale and the dashboard then warns that the sensors have stopped.
/// </para>
/// </summary>
[NetDaemonApp(Id = nameof(NetDaemonHeartbeat))]
public class NetDaemonHeartbeat : BaseApp
{
    public const string HeartbeatId = "input_datetime.netdaemon_heartbeat";
    public const string MqttBeatId = "sensor.netdaemon_mqtt_heartbeat";

    /// <summary>How often the beat is sent. The template sensor allows a few missed beats before it calls NetDaemon offline.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IEntityManager _entityManager;

    public NetDaemonHeartbeat(
        IHaContext ha,
        ILogger<NetDaemonHeartbeat> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _entityManager = entityManager;
        Beat();
        Scheduler.SchedulePeriodic(Interval, Beat);
        _ = CreateMqttSensorAsync();
    }

    private async Task CreateMqttSensorAsync()
    {
        try
        {
            // When it exists, Beat() already published the first beat; a new sensor gets it right after it is created
            if (_entityManager.EntityExists(MqttBeatId)) return;
            await _entityManager.Create(MqttBeatId, new EntityCreationOptions
            {
                Name = "NetDaemon MQTT levensteken",
                DeviceClass = "timestamp",
                Icon = "mdi:heart-pulse"
            });
            PublishMqttBeat();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {SensorId}", MqttBeatId);
        }
    }

    /// <summary>Publishes the time through MQTT. Only once the sensor exists: the first beat comes from its creation.</summary>
    private void PublishMqttBeat()
    {
        if (!_entityManager.EntityExists(MqttBeatId)) return;
        _entityManager.SetState(MqttBeatId, Scheduler.NowNl().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
    }

    private void Beat()
    {
        try
        {
            HaContext.CallService("input_datetime", "set_datetime", ServiceTarget.FromEntity(HeartbeatId),
                new { timestamp = Scheduler.Now.ToUnixTimeSeconds() });
            PublishMqttBeat();
        }
        catch (Exception ex)
        {
            // Not connected right now: the next beat tries again, and Home Assistant notices the gap
            Logger.LogWarning(ex, "Could not send the heartbeat");
        }
    }
}
