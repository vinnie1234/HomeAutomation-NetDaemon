using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;
using EntityCreationOptions = Automation.Interfaces.EntityCreationOptions;

namespace Automation.apps.General;

/// <summary>
/// What has to be put outside tonight, for the dashboard: <c>sensor.afval_buiten_zetten</c> has the garbage of tomorrow (PMD, Papier, ...) from
/// <see cref="From"/> in the evening until midnight, and "Geen" the rest of the day. The garbage of tomorrow is not news at breakfast: it only
/// has to be outside tonight, so the dashboard shows it (the chip and the bags on the pavement) from this sensor and not from tomorrow's
/// garbage itself.
/// </summary>
[NetDaemonApp(Id = nameof(GarbageOutside))]
public class GarbageOutside : BaseApp
{
    public const string Sensor = "sensor.afval_buiten_zetten";

    /// <summary>From when in the evening the garbage of tomorrow counts as "to put outside tonight".</summary>
    public static readonly TimeSpan From = new(17, 0, 0);

    private readonly IEntityManager _entityManager;

    public GarbageOutside(
        IHaContext ha,
        ILogger<GarbageOutside> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _entityManager = entityManager;
        _ = CreateSensorAsync();
        Entities.Sensor.AfvalMorgen.StateChanges().Subscribe(_ => Publish());
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), Publish);
    }

    private async Task CreateSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(Sensor))
                await _entityManager.Create(Sensor, new EntityCreationOptions { Name = "Afval buiten zetten", Icon = "mdi:trash-can-outline" });
            Publish();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {Sensor}", Sensor);
        }
    }

    /// <summary>The garbage of tomorrow from the evening on, otherwise "Geen".</summary>
    public static string Value(string? tomorrow, TimeSpan timeOfDay) =>
        timeOfDay >= From && tomorrow is not (null or "" or "Geen" or "unknown" or "unavailable") ? tomorrow : "Geen";

    private void Publish()
    {
        try
        {
            _entityManager.SetState(Sensor, Value(Entities.Sensor.AfvalMorgen.State, Scheduler.NowNl().TimeOfDay));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not publish {Sensor}", Sensor);
        }
    }
}
