namespace Automation.Helpers;

/// <summary>
/// Provides helper methods for working with collections of entities.
/// </summary>
public static class Collections
{
    /// <summary>
    /// Gets a dictionary of feed times with their corresponding feed amounts.
    /// </summary>
    /// <param name="entities">The entities to retrieve the feed times from.</param>
    /// <returns>A dictionary where the key is an <see cref="InputDatetimeEntity"/> and the value is an <see cref="InputNumberEntity"/>.</returns>
    public static Dictionary<InputDatetimeEntity, InputNumberEntity> GetFeedTimes(Entities entities)
    {
        return new Dictionary<InputDatetimeEntity, InputNumberEntity>
        {
            { entities.InputDatetime.Pixelfeedfirsttime, entities.InputNumber.Pixelfeedfirstamount },
            { entities.InputDatetime.Pixelfeedsecondtime, entities.InputNumber.Pixelfeedsecondamount },
            { entities.InputDatetime.Pixelfeedthirdtime, entities.InputNumber.Pixelfeedthirdamount },
            { entities.InputDatetime.Pixelfeedfourthtime, entities.InputNumber.Pixelfeedfourthamount }
        };
    }

    /// <summary>
    /// Gets a dictionary of all temperature sensors with their corresponding descriptions.
    /// </summary>
    /// <param name="entities">The entities to retrieve the temperature sensors from.</param>
    /// <returns>A dictionary where the key is a <see cref="NumericSensorEntity"/> and the value is a description string.</returns>
    public static Dictionary<NumericSensorEntity, string> GetAllTemperatureSensors(Entities entities)
    {
        return new Dictionary<NumericSensorEntity, string>
        {
            { entities.Sensor.BadkamerTemperature, "Badkamer" },
            { entities.Sensor.BergingTemperature, "Berging" },
            { entities.Sensor.GangTemperature, "Gang" }
        };
    }
    
    /// <summary>
    /// Gets a dictionary of Roomba rooms with their corresponding identifiers.
    /// </summary>
    /// <returns>A dictionary where the key is a room name and the value is a tuple containing two strings representing the room identifiers.</returns>
    public static Dictionary<string,  (string, string)> GetRoombaRooms()
    {
        return new Dictionary<string, (string, string)>
        {
            { "Kattenbak", ("0", "zid") },
            { "Bank", ("1", "zid") },
            { "Slaapkamer", ("2", "rid") },
            { "Gang", ("4", "rid") },
            { "Woonkamer", ("3" , "rid") }
        };
    }
}