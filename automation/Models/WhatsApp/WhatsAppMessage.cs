namespace Automation.Models.WhatsApp;

/// <summary>
/// The <c>message</c> of the service <c>whatsapp.send_message</c>: the integration wants the kind of message that is chosen
/// (<c>active_choice</c>) and the fields of that kind under its own name, nothing else. Exact keys, so they are plain dictionaries.
/// </summary>
public static class WhatsAppMessage
{
    /// <summary>A text message.</summary>
    public static Dictionary<string, object> Text(string text) => new() { ["active_choice"] = "Text", ["Text"] = text };

    /// <summary>A voice message (an audio file that is played as a recording), from an address the integration can fetch.</summary>
    public static Dictionary<string, object> Voice(string url) =>
        new() { ["active_choice"] = "Audio", ["Audio"] = new Dictionary<string, object> { ["url"] = url, ["ptt"] = true } };
}
