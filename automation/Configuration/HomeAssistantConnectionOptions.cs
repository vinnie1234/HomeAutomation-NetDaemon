namespace Automation.Configuration;

/// <summary>
/// The Home Assistant connection details, bound from the same <c>HomeAssistant</c> configuration section
/// NetDaemon's own runtime uses. Used by code that needs its own, separate websocket connection (e.g.
/// <see cref="Core.AssistPipelineClient"/>), because NetDaemon's own connection does not support streaming
/// commands such as <c>assist_pipeline/run</c>.
/// </summary>
public class HomeAssistantConnectionOptions
{
    /// <summary>The default Home Assistant websocket API port.</summary>
    public const int DefaultPort = 8123;

    /// <summary>
    /// The default websocket path, matching NetDaemon's own <c>HomeAssistantSettings.DefaultWebSocketPath</c>.
    /// The NetDaemon add-on's startup script overrides this to <c>core/websocket</c>, because inside the add-on
    /// NetDaemon talks to Home Assistant through the Supervisor's proxy rather than directly.
    /// </summary>
    public const string DefaultWebsocketPath = "api/websocket";

    public string Host { get; set; } = "";

    /// <summary>The port Home Assistant listens on. Defaults to <see cref="DefaultPort"/> when not configured.</summary>
    public int? Port { get; set; }

    public bool Ssl { get; set; }

    public string Token { get; set; } = "";

    /// <summary>
    /// The websocket path. Defaults to <see cref="DefaultWebsocketPath"/>, but must be read from configuration
    /// (not hardcoded) because the NetDaemon add-on overrides it to reach Home Assistant through the Supervisor.
    /// </summary>
    public string? WebsocketPath { get; set; }

    /// <summary>The effective port, applying <see cref="DefaultPort"/> when none is configured.</summary>
    public int EffectivePort => Port ?? DefaultPort;

    /// <summary>The effective websocket path, applying <see cref="DefaultWebsocketPath"/> when none is configured.</summary>
    public string EffectiveWebsocketPath => string.IsNullOrEmpty(WebsocketPath) ? DefaultWebsocketPath : WebsocketPath;
}
