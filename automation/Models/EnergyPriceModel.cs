using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Automation.Models;

// ReSharper disable once ClassNeverInstantiated.Global
public class EnergyPriceModel
{
    public EnergyPriceModel(DateTime startTime, double pricePerKwh, DateTime endTime)
    {
        StartTime = startTime;
        PricePerKwh = pricePerKwh;
        EndTime = endTime;
    }

    [JsonPropertyName("start_time")]
    public DateTime StartTime { get; set; }
    
    [JsonPropertyName("end_time")]
    public DateTime EndTime { get; set; }
    
    [JsonPropertyName("price_per_kwh")]
    public double PricePerKwh { get; set; }
}