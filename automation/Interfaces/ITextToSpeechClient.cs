namespace Automation.Interfaces;

/// <summary>
/// Generates spoken audio for text via Home Assistant's text-to-speech, as a URL that stays reachable for
/// others to download (e.g. so the WhatsApp add-on can fetch it to send as a voice note).
/// </summary>
public interface ITextToSpeechClient
{
    /// <summary>
    /// Generates speech for <paramref name="text"/> and returns a URL it can be downloaded from.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="language">The language to speak it in, e.g. "nl".</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    Task<string> GetSpeechUrlAsync(string text, string language, CancellationToken cancellationToken);
}
