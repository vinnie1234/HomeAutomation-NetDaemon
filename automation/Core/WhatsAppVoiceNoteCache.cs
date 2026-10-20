using Microsoft.Extensions.Caching.Memory;

namespace Automation.Core;

/// <summary>
/// An in-memory, time-limited store for generated voice notes. A random id (not a sequential one, so a
/// stored file can't be found by guessing) is handed out for the WhatsApp add-on to fetch the file by.
/// </summary>
public class WhatsAppVoiceNoteCache : IWhatsAppVoiceNoteCache
{
    /// <summary>How long a voice note stays available; long enough for the WhatsApp add-on to fetch it, no longer.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public string Add(byte[] oggOpus)
    {
        var id = Guid.NewGuid().ToString("N");
        _cache.Set(id, oggOpus, Lifetime);
        return id;
    }

    public byte[]? TryGet(string id) => _cache.TryGetValue(id, out byte[]? bytes) ? bytes : null;
}
