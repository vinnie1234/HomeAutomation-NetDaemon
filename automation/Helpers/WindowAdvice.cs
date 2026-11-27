using System.Globalization;

namespace Automation.Helpers;

/// <summary>What the advice says about opening the windows.</summary>
public enum WindowVerdict
{
    /// <summary>The weather is not known.</summary>
    Unknown,

    /// <summary>Opening the windows is fine.</summary>
    Ventilate,

    /// <summary>Fine, but it is cold outside: a short airing with the windows wide open.</summary>
    VentilateShort,

    /// <summary>Opening them does not dry the house (the air outside is too damp).</summary>
    LittleUse,

    /// <summary>Better not (rain, wind, a warning, or bugs in the dark).</summary>
    KeepClosed
}

/// <summary>
/// The weather and the air in the house the advice is based on. A value that is not known is null.
/// </summary>
/// <param name="OutdoorTemperature">The temperature outside in °C.</param>
/// <param name="OutdoorHumidity">The relative humidity outside in %.</param>
/// <param name="GustSpeed">The strongest gusts in km/h.</param>
/// <param name="RainExpected">Whether rain is expected.</param>
/// <param name="WarningActive">Whether there is a weather warning (KNMI).</param>
/// <param name="IsDark">Whether the sun is below the horizon.</param>
/// <param name="IndoorTemperature">The temperature in the house in °C.</param>
/// <param name="IndoorHumidity">The relative humidity in the house in %.</param>
/// <param name="TargetHumidity">The humidity the dehumidifier aims for in %.</param>
public sealed record WindowConditions(
    double? OutdoorTemperature,
    double? OutdoorHumidity,
    double? GustSpeed,
    bool RainExpected,
    bool WarningActive,
    bool IsDark,
    double? IndoorTemperature,
    double? IndoorHumidity,
    double? TargetHumidity);

/// <summary>
/// The advice about opening the windows.
/// </summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Title">The verdict in words, as shown on the dashboard.</param>
/// <param name="Reasons">Why, one line each.</param>
/// <param name="Minutes">How long to air, when airing is fine.</param>
/// <param name="OutdoorAbsoluteHumidity">The water in the air outside in g/m³.</param>
/// <param name="IndoorAbsoluteHumidity">The water in the air in the house in g/m³.</param>
public sealed record WindowAdviceResult(
    WindowVerdict Verdict,
    string Title,
    IReadOnlyList<string> Reasons,
    int? Minutes,
    double? OutdoorAbsoluteHumidity,
    double? IndoorAbsoluteHumidity)
{
    /// <summary>Gets a short key of the verdict for the dashboard.</summary>
    public string Key => Verdict switch
    {
        WindowVerdict.Ventilate => "luchten",
        WindowVerdict.VentilateShort => "kort",
        WindowVerdict.LittleUse => "weinig_nut",
        WindowVerdict.KeepClosed => "dicht",
        _ => "onbekend"
    };
}

/// <summary>
/// A reason to keep the windows closed: the advice names it, and an open window that runs into it gets a message.
/// </summary>
/// <param name="Kind">What kind of reason it is, so a message is sent once per kind.</param>
/// <param name="Reason">The reason as a sentence.</param>
public sealed record WindowRisk(string Kind, string Reason);

/// <summary>
/// Whether the windows can be opened, from the weather, the dark and the humidity inside and outside.
/// </summary>
/// <remarks>
/// A damp house only dries when the air outside holds less water than the air inside, so the water in the air (g/m³) is compared and not
/// the percentage: 80% at 2° is drier than 60% at 22°. At 90% or more outside nothing dries, whatever the temperature.
/// </remarks>
public static class WindowAdvice
{
    /// <summary>From this relative humidity outside opening the windows does not dry the house.</summary>
    public const double LittleUseHumidity = 90;

    /// <summary>From these gusts (km/h) the windows are better closed.</summary>
    public const double GustLimit = 50;

    /// <summary>From this temperature outside the bugs are active: the reason in the dark then names them.</summary>
    public const double BugTemperature = 10;

    /// <summary>At or below this temperature outside only a short airing makes sense.</summary>
    public const double ColdLimit = 5;

    /// <summary>The air outside must hold at least this much less water (g/m³) than the air inside to dry the house.</summary>
    public const double MinimumGain = 1.5;

    /// <summary>The minutes of a short airing.</summary>
    public const int ShortMinutes = 10;

    /// <summary>The minutes of an airing.</summary>
    public const int NormalMinutes = 30;

    private static readonly CultureInfo Dutch = new("nl-NL");

    /// <summary>
    /// The water in the air in g/m³.
    /// </summary>
    /// <param name="temperature">The temperature in °C.</param>
    /// <param name="relativeHumidity">The relative humidity in %.</param>
    /// <returns>The absolute humidity.</returns>
    public static double AbsoluteHumidity(double temperature, double relativeHumidity) =>
        216.7 * (relativeHumidity / 100 * 6.112 * Math.Exp(17.62 * temperature / (243.12 + temperature))) / (273.15 + temperature);

    /// <summary>
    /// The reasons to keep the windows closed, whatever the humidity: a weather warning, rain, strong gusts and the dark. The advice is
    /// built on this list and the messages about an open window too, so the two can never disagree.
    /// </summary>
    /// <param name="c">The weather and the air in the house.</param>
    /// <returns>The reasons, none when the windows may be opened.</returns>
    public static IReadOnlyList<WindowRisk> Risks(WindowConditions c)
    {
        var risks = new List<WindowRisk>();
        if (c.WarningActive) risks.Add(new WindowRisk("waarschuwing", "Er is een weerwaarschuwing"));
        if (c.RainExpected) risks.Add(new WindowRisk("regen", "Er wordt regen verwacht"));
        if (c.GustSpeed is >= GustLimit and { } gust) risks.Add(new WindowRisk("wind", $"Windstoten tot {Math.Round(gust)} km/u"));

        // The dark counts at any temperature: bugs when it is warm, and otherwise it is simply not the time to leave a window open
        if (c.IsDark)
        {
            risks.Add(new WindowRisk("donker", c.OutdoorTemperature is >= BugTemperature and { } warm
                ? $"Het is donker en {Math.Round(warm)}° buiten: dan komen er beestjes binnen"
                : "Het is donker: dan blijven de ramen liever dicht"));
        }

        return risks;
    }

    /// <summary>
    /// Gives the advice.
    /// </summary>
    /// <param name="c">The weather and the air in the house.</param>
    /// <returns>The advice with the reasons.</returns>
    public static WindowAdviceResult Advise(WindowConditions c)
    {
        if (c.OutdoorTemperature is null && c.OutdoorHumidity is null)
            return new WindowAdviceResult(WindowVerdict.Unknown, "Onbekend", ["Er zijn geen weergegevens"], null, null, null);

        double? outside = c.OutdoorTemperature is { } ot && c.OutdoorHumidity is { } oh ? AbsoluteHumidity(ot, oh) : null;
        double? inside = c.IndoorTemperature is { } it && c.IndoorHumidity is { } ih ? AbsoluteHumidity(it, ih) : null;

        // Better closed: the weather and the dark (reasons of their own: they do not depend on the humidity)
        var closed = Risks(c).Select(r => r.Reason).ToList();

        // No use: the air outside does not dry the house
        var littleUse = new List<string>();
        if (c.OutdoorHumidity is >= LittleUseHumidity and { } damp)
            littleUse.Add($"Buiten is het {Math.Round(damp)}% vochtig: luchten droogt het huis niet");
        else if (outside is { } o && inside is { } i && o > i - MinimumGain)
            littleUse.Add($"De lucht buiten is niet droger dan binnen ({Grams(o)} tegen {Grams(i)}): luchten droogt niet");

        if (closed.Count > 0)
            return new WindowAdviceResult(WindowVerdict.KeepClosed, "Liever dicht", [.. closed, .. littleUse], null, outside, inside);

        if (littleUse.Count > 0)
            return new WindowAdviceResult(WindowVerdict.LittleUse, "Weinig nut", [.. littleUse, "Laat de ontvochtiger zijn werk doen"], null, outside, inside);

        var reasons = new List<string>();
        if (outside is { } dry && inside is { } wet && c.IndoorTemperature is { } warmTo)
        {
            // Cold air holds little water: at 70% it is still drier than the warm air inside at 46%. Warmed up inside it is much drier still.
            var warmed = dry / AbsoluteHumidity(warmTo, 100) * 100;
            reasons.Add($"De lucht buiten is droger dan binnen ({Grams(dry)} tegen {Grams(wet)}): opgewarmd tot {Math.Round(warmTo)}° is hij nog maar {Math.Round(warmed)}% vochtig");
        }
        if (c.IndoorHumidity is { } room && c.TargetHumidity is { } target && room <= target)
            reasons.Add($"Binnen is het al {Math.Round(room)}% (doel {Math.Round(target)}%): voor het vocht hoeft het niet");

        if (c.OutdoorTemperature is <= ColdLimit and { } cold)
        {
            reasons.Add($"Het is {Math.Round(cold)}° buiten: kort en wijd open, dan koelt het huis niet af");
            return new WindowAdviceResult(WindowVerdict.VentilateShort, "Kort luchten", reasons, ShortMinutes, outside, inside);
        }

        return new WindowAdviceResult(WindowVerdict.Ventilate, "Luchten kan", reasons, NormalMinutes, outside, inside);
    }

    private static string Grams(double value) => $"{value.ToString("0.0", Dutch)} g/m³";
}
