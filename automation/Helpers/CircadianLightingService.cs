using System.Globalization;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.Helpers;

/// <summary>
/// Central service that translates the position of the sun into a light color temperature
/// and brightness, so lights follow a natural rhythm over the day.
/// </summary>
public interface ICircadianLightingService
{
    /// <summary>
    /// Gets the brightness percentage that fits the current time of day.
    /// </summary>
    /// <param name="isNightMode">When <c>true</c> the (very dim) night mode brightness is returned.</param>
    int GetBrightness(bool isNightMode);

    /// <summary>
    /// Gets the color temperature in Kelvin that fits the current position of the sun.
    /// </summary>
    long GetColorTemperature();

    /// <summary>
    /// Gets the color temperature in Kelvin for a specific light, clamped to the range that
    /// light supports. Returns <c>null</c> when the light has no color temperature mode,
    /// so callers can simply omit the parameter.
    /// </summary>
    long? GetColorTemperature(LightEntity light);

    /// <summary>
    /// Gets the current sun elevation in degrees, or <c>null</c> when the sun entity is unavailable.
    /// </summary>
    double? GetSunElevation();
}

/// <inheritdoc cref="ICircadianLightingService"/>
public class CircadianLightingService : ICircadianLightingService
{
    private const string ColorTempMode = "color_temp";

    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    private static readonly TimeSpan HalfDay = TimeSpan.FromHours(12);

    private readonly Entities _entities;
    private readonly CircadianConfiguration _config;
    private readonly ILogger<CircadianLightingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircadianLightingService"/> class.
    /// </summary>
    /// <param name="haContext">The Home Assistant context, used to read the sun elevation.</param>
    /// <param name="config">The application configuration holding the circadian curve settings.</param>
    /// <param name="logger">The logger instance.</param>
    public CircadianLightingService(
        IHaContext haContext,
        IOptions<AppConfiguration> config,
        ILogger<CircadianLightingService> logger)
    {
        _entities = new Entities(haContext);
        _config = config.Value.Circadian;
        _logger = logger;
    }

    /// <inheritdoc />
    public double? GetSunElevation() => _entities.Sun.Sun.Attributes?.Elevation;

    /// <inheritdoc />
    public int GetBrightness(bool isNightMode)
    {
        if (isNightMode) return _config.NightModeBrightnessPct;

        var brightness = Interpolate(_config.MinBrightnessPct, _config.MaxBrightnessPct);
        return Math.Clamp((int)Math.Round(brightness), 1, 100);
    }

    /// <inheritdoc />
    public long GetColorTemperature()
    {
        var kelvin = Interpolate(_config.MinColorTempKelvin, _config.MaxColorTempKelvin);
        var step = Math.Max(1, _config.ColorTempStepKelvin);

        return (long)Math.Round(kelvin / step) * step;
    }

    /// <inheritdoc />
    public long? GetColorTemperature(LightEntity light)
    {
        var attributes = light.Attributes;

        // Only skip when the light explicitly reports its modes and color temperature is not one of them.
        if (attributes?.SupportedColorModes is { Count: > 0 } modes
            && !modes.Contains(ColorTempMode, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Light {Light} does not support color temperature, skipping", light.EntityId);
            return null;
        }

        var kelvin = GetColorTemperature();

        // Respect the physical range of the lamp; Home Assistant would reject out-of-range values.
        var minKelvin = attributes?.MinColorTempKelvin;
        var maxKelvin = attributes?.MaxColorTempKelvin;

        if (minKelvin is > 0) kelvin = Math.Max(kelvin, (long)minKelvin.Value);
        if (maxKelvin is > 0) kelvin = Math.Min(kelvin, (long)maxKelvin.Value);

        return kelvin;
    }

    /// <summary>
    /// Interpolates between a night and a day value based on how high the sun is.
    /// </summary>
    private double Interpolate(double nightValue, double dayValue) =>
        nightValue + (dayValue - nightValue) * GetDayFactor();

    /// <summary>
    /// Gets how far the day has progressed towards "full daylight": 0.0 at night, 1.0 at solar noon.
    /// </summary>
    /// <remarks>
    /// The dawn/noon/dusk timestamps of the sun are preferred over the raw elevation. Around the
    /// winter solstice the sun never climbs higher than roughly 14 degrees in the Netherlands, so
    /// a fixed <see cref="CircadianConfiguration.DayElevation"/> threshold would keep the lights
    /// stuck near their night values for months. The timestamps move with the season, so every day
    /// reaches full daylight at its own solar noon. Elevation remains the fallback.
    /// </remarks>
    private double GetDayFactor() => GetSolarPhaseFactor() ?? GetElevationFactor();

    /// <summary>
    /// Builds the day factor from the sun timestamps: 0.0 at dawn and dusk, 1.0 at solar noon,
    /// linear in between. Returns <c>null</c> when the timestamps are missing or unusable.
    /// </summary>
    private double? GetSolarPhaseFactor()
    {
        var attributes = _entities.Sun.Sun.Attributes;

        var noon = ParseSunTimestamp(attributes?.NextNoon);
        var dawn = ParseSunTimestamp(attributes?.NextDawn);
        var dusk = ParseSunTimestamp(attributes?.NextDusk);

        if (noon is null || dawn is null || dusk is null) return null;

        var now = DateTimeOffset.Now;

        // The next_* attributes always point into the future. Shift them by whole days so they
        // describe the solar day around "now" instead of the next occurrence.
        var solarNoon = ShiftClosestTo(noon.Value, now);
        var solarDawn = ShiftBefore(dawn.Value, solarNoon);
        var solarDusk = ShiftAfter(dusk.Value, solarNoon);

        var span = now <= solarNoon ? solarNoon - solarDawn : solarDusk - solarNoon;
        if (span <= TimeSpan.Zero) return null;

        return Math.Clamp(1.0 - (now - solarNoon).Duration() / span, 0.0, 1.0);
    }

    /// <summary>
    /// Builds the day factor from the sun elevation: 0.0 at (or below)
    /// <see cref="CircadianConfiguration.NightElevation"/>, 1.0 at (or above)
    /// <see cref="CircadianConfiguration.DayElevation"/>.
    /// </summary>
    private double GetElevationFactor()
    {
        var elevation = GetSunElevation();

        // No sun data available: never dim or warm the lights based on a guess.
        if (elevation is null)
        {
            _logger.LogDebug("Sun position unavailable, falling back to daylight values");
            return 1.0;
        }

        var span = _config.DayElevation - _config.NightElevation;
        if (span <= 0) return 1.0;

        return Math.Clamp((elevation.Value - _config.NightElevation) / span, 0.0, 1.0);
    }

    /// <summary>
    /// Parses one of the <c>next_*</c> timestamps of the sun entity.
    /// </summary>
    private static DateTimeOffset? ParseSunTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    /// <summary>Shifts a timestamp by whole days until it is the occurrence closest to <paramref name="reference"/>.</summary>
    private static DateTimeOffset ShiftClosestTo(DateTimeOffset timestamp, DateTimeOffset reference)
    {
        while (timestamp - reference > HalfDay) timestamp = timestamp.AddDays(-1);
        while (reference - timestamp > HalfDay) timestamp = timestamp.AddDays(1);
        return timestamp;
    }

    /// <summary>Shifts a timestamp by whole days into the 24 hours before <paramref name="reference"/>.</summary>
    private static DateTimeOffset ShiftBefore(DateTimeOffset timestamp, DateTimeOffset reference)
    {
        while (timestamp > reference) timestamp = timestamp.AddDays(-1);
        while (reference - timestamp >= OneDay) timestamp = timestamp.AddDays(1);
        return timestamp;
    }

    /// <summary>Shifts a timestamp by whole days into the 24 hours after <paramref name="reference"/>.</summary>
    private static DateTimeOffset ShiftAfter(DateTimeOffset timestamp, DateTimeOffset reference)
    {
        while (timestamp < reference) timestamp = timestamp.AddDays(1);
        while (timestamp - reference >= OneDay) timestamp = timestamp.AddDays(-1);
        return timestamp;
    }
}
