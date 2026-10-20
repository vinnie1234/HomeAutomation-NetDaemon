using System.Text.Json.Serialization;

namespace Automation.Models.Energy;

/// <summary>
/// The electricity of one day of working from home, counted only during the working hours.
/// </summary>
public class HomeWorkDay
{
    /// <summary>The day (Dutch time), as <c>yyyy-MM-dd</c>.</summary>
    [JsonPropertyName("datum")]
    public string Date { get; set; } = string.Empty;

    /// <summary>The minutes that were counted as working from home.</summary>
    [JsonPropertyName("minuten")]
    public double Minutes { get; set; }

    /// <summary>Taken from the grid, in kWh.</summary>
    [JsonPropertyName("afname_kwh")]
    public double ImportKwh { get; set; }

    /// <summary>What the energy taken from the grid cost, at the all-in price.</summary>
    [JsonPropertyName("afname_kosten")]
    public double ImportCost { get; set; }

    /// <summary>Fed back to the grid, in kWh.</summary>
    [JsonPropertyName("teruglevering_kwh")]
    public double ExportKwh { get; set; }

    /// <summary>What feeding back earned, at the market price.</summary>
    [JsonPropertyName("teruglevering_opbrengst")]
    public double ExportRevenue { get; set; }

    /// <summary>Used by the washing machine, in kWh; not part of working from home.</summary>
    [JsonPropertyName("wasmachine_kwh")]
    public double WasherKwh { get; set; }

    /// <summary>What the washing machine cost; subtracted from the costs.</summary>
    [JsonPropertyName("wasmachine_kosten")]
    public double WasherCost { get; set; }

    /// <summary>The net electricity of working from home: taken minus fed back, without the washing machine.</summary>
    [JsonPropertyName("verbruik_kwh")]
    public double NetKwh => ImportKwh - ExportKwh - WasherKwh;

    /// <summary>The net costs of working from home: import costs minus feed-in revenue, without the washing machine.</summary>
    [JsonPropertyName("kosten")]
    public double NetCost => ImportCost - ExportRevenue - WasherCost;
}
