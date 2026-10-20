using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Estimates how much power the washing machine uses. The machine only reports its energy meter
/// (<c>sensor.badkamer_wasmachine_energie</c>) in steps of 0.1 kWh, about every half hour while washing. While it
/// runs, the average power since the previous step is published as <c>sensor.wasmachine_vermogen</c>; when it is
/// paused or done the power is 0. Rough per moment, but right over a whole wash.
/// </summary>
[NetDaemonApp(Id = nameof(WasherPowerEstimator))]
public class WasherPowerEstimator : BaseApp
{
    public const string SensorId = "sensor.wasmachine_vermogen";

    private readonly IEntityManager _entityManager;
    private readonly Lock _lock = new();

    /// <summary>The energy meter (kWh) and the time of the previous step, while the machine runs.</summary>
    private (double Energy, DateTimeOffset Time)? _previous;

    public WasherPowerEstimator(
        IHaContext ha,
        ILogger<WasherPowerEstimator> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _entityManager = entityManager;

        _ = CreateSensorAsync();

        Entities.Sensor.BadkamerWasmachineMachinestatus.StateChanges()
            .Subscribe(change =>
            {
                if (change.New?.State == "active") StartMeasuring();
                else if (change.Old?.State == "active") StopMeasuring();
            });

        Entities.Sensor.BadkamerWasmachineEnergie.StateChanges()
            .Subscribe(change => OnEnergyChanged(change.New?.State));
    }

    private async Task CreateSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(SensorId))
                await _entityManager.Create(SensorId, new EntityCreationOptions
                {
                    Name = "Wasmachine vermogen (geschat)",
                    DeviceClass = "power",
                    UnitOfMeasurement = "W",
                    Icon = "mdi:washing-machine"
                });

            // After a restart during a wash the next step of the meter gives the first estimate
            if (Entities.Sensor.BadkamerWasmachineMachinestatus.State == "active") StartMeasuring();
            else _entityManager.SetState(SensorId, 0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {SensorId}", SensorId);
        }
    }

    private void StartMeasuring()
    {
        lock (_lock)
        {
            if (_previous != null) return;
            if (Entities.Sensor.BadkamerWasmachineEnergie.State is not { } energy) return;
            _previous = (energy, Scheduler.Now);
        }
    }

    private void StopMeasuring()
    {
        lock (_lock)
        {
            _previous = null;
            _entityManager.SetState(SensorId, 0);
        }
    }

    private void OnEnergyChanged(double? energy)
    {
        lock (_lock)
        {
            if (_previous is not { } previous || energy is not { } current || current <= previous.Energy) return;

            var hours = (Scheduler.Now - previous.Time).TotalHours;
            _previous = (current, Scheduler.Now);
            if (hours <= 0) return;

            var watts = Math.Round((current - previous.Energy) * 1000 / hours);
            _entityManager.SetState(SensorId, watts);
            Logger.LogDebug("Wasmachine gebruikt gemiddeld {Watts} W", watts);
        }
    }
}
