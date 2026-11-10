using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Energy;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps track of the electricity used while Vincent works from home: on home work days, while
/// <c>input_boolean.workingvincent</c> is on and Vincent is home. Every step of the P1 meters is priced at that moment:
/// energy taken from the grid at the all-in price, energy fed back at the market price. The washing machine is left
/// out: its energy is subtracted, at the market price when it ran on our own solar power (we were feeding back), else
/// at the all-in price. The days are saved and published as <c>sensor.thuiswerken_energie</c> for the dashboard.
/// </summary>
[NetDaemonApp(Id = nameof(HomeWorkEnergyTracker))]
public class HomeWorkEnergyTracker : BaseApp
{
    public const string SensorId = "sensor.thuiswerken_energie";
    private const string StorageKey = "HomeWorkEnergyDays";
    private const int KeepDays = 90;
    private const int PublishedDays = 31;

    private readonly IDataRepository _storage;
    private readonly IEntityManager _entityManager;
    private readonly Lock _lock = new();
    private readonly List<HomeWorkDay> _days;

    /// <summary>The meter readings (kWh) at the previous step, while working from home.</summary>
    private (double Import, double Export, double Washer)? _previous;
    private DateTimeOffset _lastTick;
    private bool _changed;

    public HomeWorkEnergyTracker(
        IHaContext ha,
        ILogger<HomeWorkEnergyTracker> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IDataRepository storage,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
        _entityManager = entityManager;
        _days = _storage.Get<List<HomeWorkDay>>(StorageKey) ?? [];
        _lastTick = Scheduler.Now;

        _ = CreateSensorAsync();

        VincentSchedule.Working.StateChanges().Subscribe(_ => UpdateActive());
        Entities.InputBoolean.Awayvincent.StateChanges().Subscribe(_ => UpdateActive());

        Entities.Sensor.P1Meter3c39e72a64e8TotalPowerImport.StateChanges().Subscribe(_ => CountStep());
        Entities.Sensor.P1Meter3c39e72a64e8TotalPowerExport.StateChanges().Subscribe(_ => CountStep());
        Entities.Sensor.BadkamerWasmachineEnergie.StateChanges().Subscribe(_ => CountStep());

        // Count the working minutes and save and publish at most once a minute, the meters change every few seconds
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(1), Tick);
        UpdateActive();
    }

    private bool IsWorkingFromHome =>
        VincentSchedule.IsHomeWorkDay(Scheduler.NowNl().DayOfWeek) &&
        VincentSchedule.Working.IsOn() &&
        Entities.InputBoolean.Awayvincent.IsOff();

    private async Task CreateSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(SensorId))
                await _entityManager.Create(SensorId, new EntityCreationOptions
                {
                    Name = "Thuiswerken kosten vandaag",
                    UnitOfMeasurement = "€",
                    Icon = "mdi:home-lightning-bolt-outline"
                });

            lock (_lock) Publish();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {SensorId}", SensorId);
        }
    }

    /// <summary>Starts counting from the current meter readings, or counts the last step and stops.</summary>
    private void UpdateActive()
    {
        lock (_lock)
        {
            var active = IsWorkingFromHome;
            if (active && _previous == null)
            {
                _previous = ReadMeters();
                _lastTick = Scheduler.Now;
                Logger.LogInformation("Thuiswerken: begonnen met meten");
            }
            else if (!active && _previous != null)
            {
                CountStepLocked();
                AddMinutes();
                _previous = null;
                Save();
                Logger.LogInformation("Thuiswerken: gestopt met meten");
            }
        }
    }

    private void CountStep()
    {
        lock (_lock) CountStepLocked();
    }

    private void CountStepLocked()
    {
        if (_previous is not { } previous || ReadMeters() is not { } current) return;

        // Only increases count: a meter that is briefly unavailable or reset must not give negative energy
        var import = Math.Max(0, current.Import - previous.Import);
        var export = Math.Max(0, current.Export - previous.Export);
        var washer = Math.Max(0, current.Washer - previous.Washer);
        _previous = current;
        if (import == 0 && export == 0 && washer == 0) return;

        var allIn = Entities.Sensor.AnwbElectricityAllInPriceCurrent.State ?? 0;
        var market = Entities.Sensor.AnwbElectricityMarketPriceCurrent.State ?? 0;
        var feedingBack = Entities.Sensor.P1Meter3c39e72a64e8ActivePower.State < 0;

        var day = Today();
        day.ImportKwh += import;
        day.ImportCost += import * allIn;
        day.ExportKwh += export;
        day.ExportRevenue += export * market;
        day.WasherKwh += washer;
        day.WasherCost += washer * (feedingBack ? market : allIn);
        _changed = true;
    }

    private (double Import, double Export, double Washer)? ReadMeters()
    {
        var import = Entities.Sensor.P1Meter3c39e72a64e8TotalPowerImport.State;
        var export = Entities.Sensor.P1Meter3c39e72a64e8TotalPowerExport.State;
        if (import is null || export is null) return null;

        // The washing machine may be offline; then it simply adds nothing
        return (import.Value, export.Value, Entities.Sensor.BadkamerWasmachineEnergie.State ?? _previous?.Washer ?? 0);
    }

    private void Tick()
    {
        lock (_lock)
        {
            if (_previous != null) AddMinutes();
            _lastTick = Scheduler.Now;
            if (_changed) Save();
        }
    }

    private void AddMinutes()
    {
        Today().Minutes += Math.Max(0, (Scheduler.Now - _lastTick).TotalMinutes);
        _lastTick = Scheduler.Now;
        _changed = true;
    }

    private HomeWorkDay Today()
    {
        var date = Scheduler.NowNl().ToString("yyyy-MM-dd");
        var day = _days.Find(x => x.Date == date);
        if (day != null) return day;

        day = new HomeWorkDay { Date = date };
        _days.Add(day);
        _days.RemoveAll(x => string.CompareOrdinal(x.Date, Scheduler.NowNl().AddDays(-KeepDays).ToString("yyyy-MM-dd")) < 0);
        return day;
    }

    private void Save()
    {
        _storage.Save(StorageKey, _days);
        _changed = false;
        Publish();
    }

    /// <summary>Publishes today's costs as the state, and the last days and this month as attributes.</summary>
    private void Publish()
    {
        var now = Scheduler.NowNl();
        var today = _days.Find(x => x.Date == now.ToString("yyyy-MM-dd"));
        var month = _days.Where(x => x.Date.StartsWith(now.ToString("yyyy-MM"), StringComparison.Ordinal)).ToList();

        _entityManager.SetState(SensorId, Math.Round(today?.NetCost ?? 0, 2), new
        {
            actief = _previous != null,
            vandaag_kwh = Math.Round(today?.NetKwh ?? 0, 3),
            maand_kosten = Math.Round(month.Sum(x => x.NetCost), 2),
            maand_kwh = Math.Round(month.Sum(x => x.NetKwh), 3),
            maand_dagen = month.Count(x => x.Minutes > 0),
            dagen = _days.OrderByDescending(x => x.Date).Take(PublishedDays).Select(x => new
            {
                datum = x.Date,
                minuten = Math.Round(x.Minutes),
                verbruik_kwh = Math.Round(x.NetKwh, 3),
                afname_kwh = Math.Round(x.ImportKwh, 3),
                teruglevering_kwh = Math.Round(x.ExportKwh, 3),
                wasmachine_kwh = Math.Round(x.WasherKwh, 3),
                afname_kosten = Math.Round(x.ImportCost, 2),
                teruglevering_opbrengst = Math.Round(x.ExportRevenue, 2),
                wasmachine_kosten = Math.Round(x.WasherCost, 2),
                kosten = Math.Round(x.NetCost, 2)
            }).ToList()
        });
    }
}
