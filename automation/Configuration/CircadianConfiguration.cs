namespace Automation.Configuration;

/// <summary>
/// Configuration for the natural light rhythm (circadian lighting).
/// The sun elevation of <see cref="NightElevation"/> and below is treated as fully "night",
/// <see cref="DayElevation"/> and above as fully "day"; everything in between is interpolated.
/// </summary>
public class CircadianConfiguration
{
    /// <summary>Sun elevation (degrees) at which the warmest/dimmest values are used.</summary>
    public double NightElevation { get; set; } = -6;

    /// <summary>Sun elevation (degrees) at which the coolest/brightest values are used.</summary>
    public double DayElevation { get; set; } = 20;

    /// <summary>Warmest color temperature, used around and after sunset.</summary>
    public int MinColorTempKelvin { get; set; } = 2500;

    /// <summary>Coolest color temperature, used when the sun is high.</summary>
    public int MaxColorTempKelvin { get; set; } = 6000;

    /// <summary>Color temperatures are rounded to this step to avoid pointless updates.</summary>
    public int ColorTempStepKelvin { get; set; } = 50;

    /// <summary>Brightness used when the sun is at or below <see cref="NightElevation"/>.</summary>
    public int MinBrightnessPct { get; set; } = 50;

    /// <summary>Brightness used when the sun is at or above <see cref="DayElevation"/>.</summary>
    public int MaxBrightnessPct { get; set; } = 100;

    /// <summary>Brightness used when somebody is sleeping (night mode).</summary>
    public int NightModeBrightnessPct { get; set; } = 5;
}
