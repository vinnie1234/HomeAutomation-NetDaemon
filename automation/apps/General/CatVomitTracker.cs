using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Remembers when Pixel threw up. Every increase of <c>counter.pixel_gekotst</c> is saved with its time, a decrease
/// (a mistake) removes the last time again. The moments of the last two weeks are published as
/// <c>sensor.pixel_gekotst_momenten</c> for the graph on the dashboard; the recorder only keeps 10 days.
/// </summary>
[NetDaemonApp(Id = nameof(CatVomitTracker))]
public class CatVomitTracker : BaseApp
{
    public const string SensorId = "sensor.pixel_gekotst_momenten";
    private const string StorageKey = "PixelGekotstMomenten";
    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(14);

    private readonly IDataRepository _storage;
    private readonly IEntityManager _entityManager;
    private readonly Lock _lock = new();

    public CatVomitTracker(
        IHaContext ha,
        ILogger<CatVomitTracker> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IDataRepository storage,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
        _entityManager = entityManager;

        _ = CreateSensorAsync();

        Entities.Counter.PixelGekotst.StateChanges()
            .Subscribe(change =>
            {
                // Unavailable (e.g. during a restart of Home Assistant) is not a change of the count
                if (!int.TryParse(change.Old?.State, out var oldCount) || !int.TryParse(change.New?.State, out var newCount)) return;
                Update(newCount - oldCount);
            });

        // Moments older than two weeks drop off the graph, also on days nothing happens
        Scheduler.RunDaily(new TimeSpan(0, 5, 0), () => Update(0));
    }

    private async Task CreateSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(SensorId))
                await _entityManager.Create(SensorId, new EntityCreationOptions
                {
                    Name = "Pixel gekotst (2 weken)",
                    Icon = "mdi:cat",
                    UnitOfMeasurement = "keer"
                });

            Update(0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {SensorId}", SensorId);
        }
    }

    /// <summary>
    /// Adds <paramref name="difference"/> moments at the current time, or removes the latest ones when it is negative,
    /// drops moments older than two weeks and publishes the result.
    /// </summary>
    private void Update(int difference)
    {
        lock (_lock)
        {
            var now = Scheduler.NowNl();
            var moments = _storage.Get<List<DateTimeOffset>>(StorageKey) ?? [];

            for (var i = 0; i < difference; i++) moments.Add(now);
            for (var i = 0; i < -difference && moments.Count > 0; i++) moments.RemoveAt(moments.Count - 1);

            moments = moments.Where(x => x > now - KeepFor).Order().ToList();
            _storage.Save(StorageKey, moments);

            _entityManager.SetState(SensorId, moments.Count, new
            {
                momenten = moments.Select(x => x.ToString("yyyy-MM-ddTHH:mm:sszzz")).ToList(),
                laatste = moments.Count > 0 ? moments[^1].ToString("yyyy-MM-ddTHH:mm:sszzz") : null
            });

            if (difference != 0)
                Logger.LogInformation("Pixel gekotst: {Difference:+0;-0}, {Count} keer in de afgelopen twee weken", difference, moments.Count);
        }
    }
}
