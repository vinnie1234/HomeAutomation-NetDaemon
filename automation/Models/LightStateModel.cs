// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Automation.Models;

public class LightStateModel
{
    public LightStateModel(string entityId, IReadOnlyList<double>? rgbColors, double? brightness, double? colorTempKelvin, bool isOn, IReadOnlyList<string>? supportedColorModes)
    {
        EntityId = entityId;
        RgbColors = rgbColors;
        Brightness = brightness;
        ColorTempKelvin = colorTempKelvin;
        IsOn = isOn;
        SupportedColorModes = supportedColorModes;
    }

    public string EntityId { get; }
    public IReadOnlyList<double>? RgbColors { get; }
    public double? Brightness { get; }

    /// <summary>The color temperature in Kelvin (Home Assistant's <c>color_temp_kelvin</c>, not the mired <c>color_temp</c>).</summary>
    public double? ColorTempKelvin { get; }

    public bool IsOn { get; }

    public IReadOnlyList<string>? SupportedColorModes { get; set; }
}