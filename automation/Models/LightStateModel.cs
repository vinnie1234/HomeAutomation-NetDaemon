// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Automation.Models;

public class LightStateModel
{
    public LightStateModel(string entityId, IReadOnlyList<double>? rgbColors, double? brightness, JsonElement? colorTemp, bool isOn, IReadOnlyList<string>? supportedColorModes)
    {
        EntityId = entityId;
        RgbColors = rgbColors;
        Brightness = brightness;
        ColorTemp = colorTemp;
        IsOn = isOn;
        SupportedColorModes = supportedColorModes;
    }

    public string EntityId { get; }
    public IReadOnlyList<double>? RgbColors { get; }
    public double? Brightness { get; }
    public JsonElement? ColorTemp { get; }

    public bool IsOn { get; }

    public IReadOnlyList<string>? SupportedColorModes { get; set; }
}