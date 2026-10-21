using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Remembers when Pixel threw up, forever: these moments are important. Every increase of <c>counter.pixel_gekotst</c> is
/// saved with its time; a decrease of one (undoing a mistake) removes the last time again, while a bigger drop (such as
/// resetting the counter) never deletes anything. All moments are published as <c>sensor.pixel_gekotst_momenten</c> for the
/// graph on the dashboard (the recorder only keeps 10 days); its state is the number of the last two weeks.
/// </summary>
[NetDaemonApp(Id = nameof(CatVomitTracker))]
public class CatVomitTracker : BaseApp
{
    public const string SensorId = "sensor.pixel_gekotst_momenten";
    private const string StorageKey = "PixelGekotstMomenten";
    private static readonly TimeSpan Recent = TimeSpan.FromDays(14);

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

        // The count of the last two weeks moves on, also on days nothing happens
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
    /// Adds <paramref name="difference"/> moments at the current time, or removes the latest one when the counter went one
    /// down, and publishes the result. Nothing is ever deleted otherwise.
    /// </summary>
    private void Update(int difference)
    {
        lock (_lock)
        {
            var now = Scheduler.NowNl();
            var moments = _storage.Get<List<DateTimeOffset>>(StorageKey) ?? [];

            for (var i = 0; i < difference; i++) moments.Add(now);
            if (difference == -1 && moments.Count > 0) moments.RemoveAt(moments.Count - 1);
            else if (difference < -1)
                Logger.LogWarning("counter.pixel_gekotst dropped by {Drop} (reset?); the saved moments are kept", -difference);

            moments = moments.Order().ToList();
            _storage.Save(StorageKey, moments);

            var recent = moments.Count(x => x > now - Recent);
            _entityManager.SetState(SensorId, recent, new
            {
                momenten = moments.Select(x => x.ToString("yyyy-MM-ddTHH:mm:sszzz")).ToList(),
                laatste = moments.Count > 0 ? moments[^1].ToString("yyyy-MM-ddTHH:mm:sszzz") : null
            });

            if (difference != 0)
                Logger.LogInformation("Pixel gekotst: {Difference:+0;-0}, {Count} keer in de afgelopen twee weken", difference, recent);
        }
    }
}
