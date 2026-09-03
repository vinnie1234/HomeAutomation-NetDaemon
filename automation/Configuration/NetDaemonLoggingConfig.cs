namespace Automation.Configuration;

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
