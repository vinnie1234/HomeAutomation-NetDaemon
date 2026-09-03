namespace Automation.Configuration;

/// <summary>
/// Roomba vacuum robot configuration.
/// </summary>
public class RoombaConfig
{
    public string PmapId { get; set; } = "";
    public Dictionary<string, RoombaRoomOptions> Rooms { get; set; } = new();
}
