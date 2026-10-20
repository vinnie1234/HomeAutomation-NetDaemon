namespace Automation.Configuration;

/// <summary>
/// A device that only reports an energy meter (kWh), for which <see cref="apps.General.PowerEstimator"/> estimates the
/// power while it runs.
/// </summary>
public class EstimatedPowerDevice
{
    /// <summary>The name of the estimated power sensor, e.g. <c>Wasmachine vermogen (geschat)</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The energy meter of the device in kWh, e.g. <c>sensor.badkamer_wasmachine_energie</c>.</summary>
    public string EnergySensor { get; set; } = string.Empty;

    /// <summary>The entity that tells whether the device runs, e.g. <c>sensor.badkamer_wasmachine_machinestatus</c>.</summary>
    public string StatusEntity { get; set; } = string.Empty;

    /// <summary>The states of <see cref="StatusEntity"/> in which the device runs; in any other state the power is 0.</summary>
    public List<string> RunningStates { get; set; } = [];

    /// <summary>The sensor the estimate is published to, e.g. <c>sensor.wasmachine_vermogen</c>.</summary>
    public string PowerSensor { get; set; } = string.Empty;
}
