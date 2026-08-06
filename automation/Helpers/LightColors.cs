namespace Automation.Helpers;

/// <summary>
/// RGB values for the named colors that were previously passed to the light
/// <c>turn_on</c> service via <c>color_name</c>. Home Assistant removed that
/// parameter, so colors are set through <c>rgb_color</c> instead.
/// </summary>
public static class LightColors
{
    public static readonly IReadOnlyCollection<int> Red = [255, 0, 0];
    public static readonly IReadOnlyCollection<int> Green = [0, 255, 0];
    public static readonly IReadOnlyCollection<int> Blue = [0, 0, 255];
    public static readonly IReadOnlyCollection<int> Yellow = [255, 255, 0];
    public static readonly IReadOnlyCollection<int> White = [255, 255, 255];
}
