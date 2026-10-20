using System.Net.Http;

namespace Automation.Core;

/// <summary>
/// Downloads WhatsApp media files over plain HTTP(S), with no Home Assistant or WhatsApp authentication:
/// WhatsApp's media URLs are themselves the credential (and the content is encrypted on top of that).
/// </summary>
public class WhatsAppMediaClient(IHttpClientFactory httpClientFactory) : IWhatsAppMediaClient
{
    public async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(nameof(WhatsAppMediaClient));
        return await client.GetByteArrayAsync(url, cancellationToken);
    }
}
