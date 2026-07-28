using System.Reactive.Concurrency;
using Automation.Enum;
using Automation.Configuration;
using static Automation.Globals;

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
    public static void TurnAllOff(this LightEntities lightEntities, AppConfiguration? config = null)
    {
        config ??= new AppConfiguration();
        lightEntities.EnumerateAll()
            .Where(x => x.EntityId is not "light.rt_ax88u_led" and not "light.tradfri_driver")
            .TurnOff(transition: config.Lights.DefaultTransitionSeconds);
    }



    /// <summary>
    /// Sets the light scene based on the current house state.
    /// </summary>
    /// <param name="entities"> The entities to control.</param>
    public static void SetLightSceneWoonkamer(IEntities entities)
    {
        var houseState = GetHouseState(entities);
        
        switch (houseState)
        {
            case HouseState.Morning:
                entities.Scene.Woonkamermorning.TurnOn();
                break;
            case HouseState.Day:
                entities.Scene.Woonkamerday.TurnOn();
                break;
            case HouseState.Evening:
                entities.Scene.Woonkamerevening.TurnOn();
                break;
            case HouseState.Night:
                entities.Scene.Woonkamernight.TurnOn();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(houseState), $"{houseState} is not a valid house state!");
        }
    }
}