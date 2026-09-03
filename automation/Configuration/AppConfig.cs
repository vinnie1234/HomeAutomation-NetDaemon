using System.ComponentModel.DataAnnotations;

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

    [Required]
    [Url]
    public string BaseUrlHomeAssistant { get; set; } = "";

    [Url]
    public string SpotifyRadioNlUrl { get; set; } = "";
    [Url]
    public string SpotifyDoucheUrl { get; set; } = "";
    [Url]
    public string SpotifyKeukenUrl { get; set; } = "";
    public DiscordConfig Discord { get; set; } = new();
    public RoombaConfig Roomba { get; set; } = new();
    public TwitterConfig Twitter { get; set; } = new();
    public NetDaemonLoggingConfig NetDaemonLogging { get; set; } = new();
}
