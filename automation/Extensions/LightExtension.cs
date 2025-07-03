using System.Reactive.Concurrency;
using Automation.Enum;

namespace Automation.Extensions;

/// <summary>
/// Provides extension methods for light entities.
/// </summary>
public static class LightExtension
{
    /// <summary>
    /// Turns off all lights except specified ones.
    /// </summary>
    /// <param name="lightEntities">The light entities to turn off.</param>
    public static void TurnAllOff(this LightEntities lightEntities)
    {
        lightEntities.EnumerateAll()
            .Where(x => x.EntityId is not "light.rt_ax88u_led" and not "light.tradfri_driver")
            .TurnOff(transition: 5);
    }

    /// <summary>
    /// Turns on the lights in the living room with a specific brightness and color temperature.
    /// </summary>
    /// <param name="entities">The entities to control.</param>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    public static void TurnOnLightsWoonkamer(IEntities entities, IScheduler scheduler)
    {
        entities.Light.HueFilamentBulb2.TurnOn(brightnessPct: 100, colorTempKelvin: GetColorTemp(entities));
        entities.Light.HueFilamentBulb2
            .StateChanges()
            .Where(x => x.Old.IsOff())
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ => { entities.Light.PlafondWoonkamer.TurnOn(brightnessPct: 100, colorTempKelvin: GetColorTemp(entities)); });
        entities.Light.PlafondWoonkamer
            .StateChanges()
            .Where(x => x.Old.IsOff())
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ => { entities.Light.HueFilamentBulb1.TurnOn(brightnessPct: 100, colorTempKelvin: GetColorTemp(entities)); });

        scheduler.Schedule(TimeSpan.FromMilliseconds(200), () =>
        {
            entities.Light.HueFilamentBulb2.TurnOn(colorTempKelvin: GetColorTemp(entities));
            entities.Light.PlafondWoonkamer.TurnOn(colorTempKelvin: GetColorTemp(entities));
            entities.Light.HueFilamentBulb1.TurnOn(colorTempKelvin: GetColorTemp(entities));
        });
    }

    /// <summary>
    /// Turns off the lights in the living room.
    /// </summary>
    /// <param name="entities">The entities to control.</param>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    public static void TurnOffLightsWoonkamer(IEntities entities, IScheduler scheduler)
    {
        entities.Light.HueFilamentBulb1.TurnOff();
        entities.Light.HueFilamentBulb1
            .StateChanges()
            .Where(x => x.Old.IsOn())
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ => { entities.Light.PlafondWoonkamer.TurnOff(); });
        entities.Light.PlafondWoonkamer
            .StateChanges()
            .Where(x => x.Old.IsOn())
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ => { entities.Light.HueFilamentBulb2.TurnOff(); });

        scheduler.Schedule(TimeSpan.FromMilliseconds(200), () =>
        {
            entities.Light.HueFilamentBulb1.TurnOff();
            entities.Light.PlafondWoonkamer.TurnOff();
            entities.Light.HueFilamentBulb2.TurnOff();
        });
    }

    /// <summary>
    /// Gets the color temperature based on the current house state.
    /// </summary>
    /// <param name="entities">The entities to use for determining the house state.</param>
    /// <returns>The color temperature in kelvin.</returns>
    private static int GetColorTemp(IEntities entities)
    {
        var houseState = Globals.GetHouseState(entities);
        const int whiteColor = 4504;
        const int warmColor = 2300;
        const int someColor = 150;

        return houseState
            switch
            {
                HouseState.Day or HouseState.Morning   => whiteColor, // White color
                HouseState.Evening or HouseState.Night => warmColor,  // Warm Color
                _                                              => someColor   // Some Color
            };
    }
}