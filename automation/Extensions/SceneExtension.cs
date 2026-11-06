using NetDaemon.HassModel.Entities;

namespace Automation.Extensions;

/// <summary>
/// Provides extension methods for scene entities.
/// </summary>
public static class SceneExtension
{
    /// <summary>
    /// Activates the scene without any service data.
    /// The generated <c>TurnOn()</c> always sends <c>"transition": null</c>, which the Hue integration of recent
    /// Home Assistant releases turns into an effect the bridge rejects, so the scene would not switch.
    /// </summary>
    /// <param name="scene">The scene to activate.</param>
    public static void Activate(this SceneEntity scene)
    {
        scene.CallService("turn_on");
    }
}
