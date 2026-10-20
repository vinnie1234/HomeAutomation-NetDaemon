namespace Automation.Configuration;

/// <summary>
/// Configuration for estimating the power of devices that only report an energy meter.
/// </summary>
public class PowerEstimationConfiguration
{
    /// <summary>
    /// The devices to estimate. Add a device here to get a power sensor for it; a list bound from configuration is
    /// added to these defaults, so configure new devices either here or there, not both.
    /// </summary>
    public List<EstimatedPowerDevice> Devices { get; set; } =
    [
        new()
        {
            Name = "Wasmachine vermogen (geschat)",
            EnergySensor = "sensor.badkamer_wasmachine_energie",
            StatusEntity = "sensor.badkamer_wasmachine_machinestatus",
            RunningStates = ["active"],
            PowerSensor = "sensor.wasmachine_vermogen"
        }
    ];
}
