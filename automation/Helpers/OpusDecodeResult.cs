namespace Automation.Helpers;

/// <summary>
/// The result of <see cref="OpusAudioDecoder.DecodeToPcm16"/>, including how many packets could not be
/// decoded, so a caller can tell a clean decode from one where a meaningful part of the audio was lost
/// (which would explain poor speech recognition without a dedicated error message or crash).
/// </summary>
public record OpusDecodeResult
{
    /// <summary>The decoded audio as mono 16-bit little-endian PCM samples.</summary>
    public byte[] Pcm { get; init; } = [];

    /// <summary>How many audio packets the Ogg stream contained (excluding the header and comment packets).</summary>
    public int AudioPacketCount { get; init; }

    /// <summary>How many of those packets failed to decode and were skipped.</summary>
    public int SkippedPacketCount { get; init; }
}
