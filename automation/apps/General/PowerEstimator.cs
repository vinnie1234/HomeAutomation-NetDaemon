using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Estimates the power of devices that only report an energy meter (kWh), such as the washing machine, which counts in
/// steps of 0.1 kWh. While a device runs, the average power since the previous step of its meter is published to its
/// power sensor; when it stops the power is 0. Rough per moment, but right over a whole run.
/// The devices are configured in <see cref="PowerEstimationConfiguration"/>.
/// </summary>
[NetDaemonApp(Id = nameof(PowerEstimator))]
public class PowerEstimator : BaseApp
{
    private readonly IEntityManager _entityManager;
    private readonly Lock _lock = new();

    /// <summary>Per power sensor: the energy meter (kWh) and the time of the previous step, while the device runs.</summary>
    private readonly Dictionary<string, (double Energy, DateTimeOffset Time)> _previous = [];

    public PowerEstimator(
        IHaContext ha,
        ILogger<PowerEstimator> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IOptions<AppConfiguration> appConfiguration,
        IEntityManager entityManager)
        : base(ha, logger, notify, scheduler, config)
    {
        _entityManager = entityManager;

        foreach (var device in appConfiguration.Value.PowerEstimation.Devices)
        {
            _ = CreateSensorAsync(device);

            new Entity(HaContext, device.StatusEntity).StateChanges()
                .Subscribe(change =>
                {
                    var running = IsRunning(device, change.New?.State);
                    if (running && !IsRunning(device, change.Old?.State)) StartMeasuring(device);
                    else if (!running && IsRunning(device, change.Old?.State)) StopMeasuring(device);
                });

            new Entity(HaContext, device.EnergySensor).StateChanges()
                .Subscribe(change => OnEnergyChanged(device, ReadEnergy(change.New?.State)));
        }
    }

    private async Task CreateSensorAsync(EstimatedPowerDevice device)
    {
        try
        {
            if (!_entityManager.EntityExists(device.PowerSensor))
                await _entityManager.Create(device.PowerSensor, new EntityCreationOptions
                {
                    Name = device.Name,
                    DeviceClass = "power",
                    UnitOfMeasurement = "W",
                    Icon = "mdi:flash-outline"
                });

            // After a restart while the device runs, the next step of the meter gives the first estimate
            if (IsRunning(device, new Entity(HaContext, device.StatusEntity).State)) StartMeasuring(device);
            else _entityManager.SetState(device.PowerSensor, 0);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {PowerSensor}", device.PowerSensor);
        }
    }

    private static bool IsRunning(EstimatedPowerDevice device, string? state) =>
        state != null && device.RunningStates.Contains(state);

    private static double? ReadEnergy(string? state) =>
        double.TryParse(state, NumberStyles.Float, CultureInfo.InvariantCulture, out var energy) ? energy : null;

    private void StartMeasuring(EstimatedPowerDevice device)
    {
        lock (_lock)
        {
            if (_previous.ContainsKey(device.PowerSensor)) return;
            if (ReadEnergy(new Entity(HaContext, device.EnergySensor).State) is not { } energy) return;
            _previous[device.PowerSensor] = (energy, Scheduler.Now);
        }
    }

    private void StopMeasuring(EstimatedPowerDevice device)
    {
        lock (_lock)
        {
            _previous.Remove(device.PowerSensor);
            _entityManager.SetState(device.PowerSensor, 0);
        }
    }

    private void OnEnergyChanged(EstimatedPowerDevice device, double? energy)
    {
        lock (_lock)
        {
            if (!_previous.TryGetValue(device.PowerSensor, out var previous) || energy is not { } current || current <= previous.Energy) return;

            var hours = (Scheduler.Now - previous.Time).TotalHours;
            _previous[device.PowerSensor] = (current, Scheduler.Now);
            if (hours <= 0) return;

            var watts = Math.Round((current - previous.Energy) * 1000 / hours);
            _entityManager.SetState(device.PowerSensor, watts);
            Logger.LogDebug("{Device}: gemiddeld {Watts} W", device.Name, watts);
        }
    }
}
