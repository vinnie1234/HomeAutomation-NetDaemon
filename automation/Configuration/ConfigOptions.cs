namespace Automation.Configuration;

/// <summary>
/// Strongly-typed root mapping of config.json.
/// Registered as IOptions&lt;AppConfig&gt; via DI.
/// </summary>
public class AppConfig
{
    public string ZedarDeviceId { get; set; } = "";
    public string SnowyFeeder { get; set; } = "";
    public string SnowyFountain { get; set; } = "";
    public string PetSnowyDeviceId { get; set; } = "";
    public string BaseUrlHomeAssistant { get; set; } = "";
    public string SpotifyRadioNlUrl { get; set; } = "";
    public DiscordConfig Discord { get; set; } = new();
    public RoombaConfig Roomba { get; set; } = new();
    public TwitterConfig Twitter { get; set; } = new();
    public NetDaemonLoggingConfig NetDaemonLogging { get; set; } = new();
}

/// <summary>
/// Discord channel IDs configuration.
/// </summary>
public class DiscordConfig
{
    public string Pixel { get; set; } = "";
    public string Logs { get; set; } = "";
    public string Updates { get; set; } = "";
    public string Yts { get; set; } = "";
    public string COC { get; set; } = "";
    public string TODO { get; set; } = "";
}

/// <summary>
/// Roomba vacuum robot configuration.
/// </summary>
public class RoombaConfig
{
    public string PmapId { get; set; } = "";
}

/// <summary>
/// Twitter API configuration.
/// </summary>
public class TwitterConfig
{
    public string BearerToken { get; set; } = "";
}

/// <summary>
/// NetDaemon logging webhook configuration.
/// Note: Used by DiscordLogger via static ConfigManager, not via IOptions.
/// </summary>
public class NetDaemonLoggingConfig
{
    public string Debug { get; set; } = "";
    public string Information { get; set; } = "";
    public string Warning { get; set; } = "";
    public string Error { get; set; } = "";
    public string Exception { get; set; } = "";
}
