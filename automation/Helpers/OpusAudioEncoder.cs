using System.IO;
using Concentus;
using Concentus.Enums;

namespace Automation.Helpers;

/// <summary>
/// Encodes mono 16-bit PCM into an Ogg Opus file (RFC 7845), using Concentus, a pure .NET encoder, so this
/// works without ffmpeg or any other native dependency on the host.
/// </summary>
public static class OpusAudioEncoder
{
    /// <summary>The sample rate WhatsApp's own voice notes use, and so the one a reply should match.</summary>
    public const int WhatsAppVoiceNoteSampleRate = 16_000;

    /// <summary>
    /// Encodes mono PCM to an Ogg Opus file at <see cref="WhatsAppVoiceNoteSampleRate"/>, resampling first
    /// if <paramref name="pcm"/> is at a different rate.
    /// </summary>
    /// <param name="pcm">Mono 16-bit little-endian PCM.</param>
    /// <param name="sampleRate">The sample rate of <paramref name="pcm"/>.</param>
    public static byte[] EncodeToOggOpus(byte[] pcm, int sampleRate)
    {
        var samples = new short[pcm.Length / 2];
        Buffer.BlockCopy(pcm, 0, samples, 0, pcm.Length);
        samples = AudioResampler.Resample(samples, channels: 1, sampleRate, WhatsAppVoiceNoteSampleRate);

        var encoder = OpusCodecFactory.CreateEncoder(WhatsAppVoiceNoteSampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        var frameSize = WhatsAppVoiceNoteSampleRate / 50; // 20ms frames
        var packets = new List<byte[]>();

        for (var offset = 0; offset + frameSize <= samples.Length; offset += frameSize)
        {
            var outBuffer = new byte[4000];
            var length = encoder.Encode(samples.AsSpan(offset, frameSize), frameSize, outBuffer, outBuffer.Length);
            packets.Add(outBuffer.AsSpan(0, length).ToArray());
        }

        using var stream = new MemoryStream();
        OggPageWriter.WritePage(stream, serialNumber: 1, sequence: 0, granule: 0, headerType: OggPageWriter.BeginOfStream, [BuildOpusHead()]);
        OggPageWriter.WritePage(stream, serialNumber: 1, sequence: 1, granule: 0, headerType: OggPageWriter.Continuation, [BuildOpusTags()]);

        for (var i = 0; i < packets.Count; i++)
        {
            var isLast = i == packets.Count - 1;
            OggPageWriter.WritePage(stream, serialNumber: 1, sequence: (uint)(2 + i), granule: (i + 1) * (long)frameSize,
                headerType: isLast ? OggPageWriter.EndOfStream : OggPageWriter.Continuation, [packets[i]]);
        }

        return stream.ToArray();
    }

    private static byte[] BuildOpusHead()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("OpusHead"u8.ToArray());
        writer.Write((byte)1); // version
        writer.Write((byte)1); // channels (mono)
        writer.Write((ushort)0); // pre-skip
        writer.Write((uint)WhatsAppVoiceNoteSampleRate);
        writer.Write((short)0); // output gain
        writer.Write((byte)0); // channel mapping family
        return stream.ToArray();
    }

    private static byte[] BuildOpusTags()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("OpusTags"u8.ToArray());
        writer.Write((uint)0); // vendor string length
        writer.Write((uint)0); // user comment list length
        return stream.ToArray();
    }
}
