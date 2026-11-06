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
    /// <param name="config">The light configuration; the defaults when null.</param>
    /// <param name="keepOn">More lights that stay as they are.</param>
    public static void TurnAllOff(this LightEntities lightEntities, AppConfiguration? config = null, params string[] keepOn)
    {
        config ??= new AppConfiguration();
        lightEntities.EnumerateAll()
            .Where(x => x.EntityId is not "light.rt_ax88u_led" and not "light.tradfri_driver" && !keepOn.Contains(x.EntityId))
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
                entities.Scene.Woonkamermorning.Activate();
                break;
            case HouseState.Day:
                entities.Scene.Woonkamerday.Activate();
                break;
            case HouseState.Evening:
                entities.Scene.Woonkamerevening.Activate();
                break;
            case HouseState.Night:
                entities.Scene.Woonkamernight.Activate();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(houseState), $"{houseState} is not a valid house state!");
        }
    }
}