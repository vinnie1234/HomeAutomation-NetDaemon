namespace Automation.Interfaces;

/// <summary>
/// Temporarily holds generated voice notes (Ogg Opus bytes) so NetDaemon's own small web server can serve
/// them for the WhatsApp add-on to download, under a short-lived, hard-to-guess URL.
/// </summary>
public interface IWhatsAppVoiceNoteCache
{
    /// <summary>Stores a voice note and returns the id it is reachable under.</summary>
    string Add(byte[] oggOpus);

    /// <summary>Gets a previously stored voice note, or <c>null</c> if the id is unknown or has expired.</summary>
    byte[]? TryGet(string id);
}
