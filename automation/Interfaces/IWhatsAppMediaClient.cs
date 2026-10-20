namespace Automation.Interfaces;

/// <summary>
/// Downloads WhatsApp media files (e.g. voice notes). The downloaded bytes are still end-to-end encrypted;
/// see <see cref="Helpers.WhatsAppMediaDecryptor"/>.
/// </summary>
public interface IWhatsAppMediaClient
{
    Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken);
}
