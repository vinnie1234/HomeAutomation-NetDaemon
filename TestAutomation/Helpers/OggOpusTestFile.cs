using Automation.Helpers;
using Concentus;
using Concentus.Enums;

namespace TestAutomation.Helpers;

/// <summary>
/// Builds a minimal, real Ogg Opus file (RFC 7845) for tests: an OpusHead page, an OpusTags page, and one
/// page per 20ms audio frame. Shared by tests that need Ogg Opus input, e.g. for <c>OpusAudioDecoder</c>
/// or a (simulated) WhatsApp voice note.
/// </summary>
public static class OggOpusTestFile
{
    public static byte[] Encode(short[] pcm, int sampleRate)
    {
        var encoder = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        var frameSize = sampleRate / 50; // 20ms frames
        var packets = new List<byte[]>();

        for (var offset = 0; offset + frameSize <= pcm.Length; offset += frameSize)
        {
            var outBuffer = new byte[4000];
            var length = encoder.Encode(pcm.AsSpan(offset, frameSize), frameSize, outBuffer, outBuffer.Length);
            packets.Add(outBuffer.AsSpan(0, length).ToArray());
        }

        return BuildFile(packets, sampleRate);
    }

    /// <summary>Builds a short burst of silent mono PCM, e.g. to stand in for a WhatsApp voice note in tests.</summary>
    public static short[] Silence(int sampleRate, double durationSeconds) => new short[(int)(sampleRate * durationSeconds)];

    /// <summary>
    /// Wraps already-encoded Opus packets (e.g. a real captured packet, rather than one encoded by this class)
    /// in a minimal, real Ogg Opus container, one packet per page.
    /// </summary>
    public static byte[] EncodeRawPackets(IReadOnlyList<byte[]> packets, int inputSampleRate) => BuildFile(packets, inputSampleRate);

    private static byte[] BuildFile(IReadOnlyList<byte[]> packets, int inputSampleRate)
    {
        using var stream = new MemoryStream();
        OggPageWriter.WritePage(stream, serialNumber: 1, sequence: 0, granule: 0, headerType: OggPageWriter.BeginOfStream, [BuildOpusHead(inputSampleRate)]);
        OggPageWriter.WritePage(stream, serialNumber: 1, sequence: 1, granule: 0, headerType: OggPageWriter.Continuation, [BuildOpusTags()]);

        for (var i = 0; i < packets.Count; i++)
        {
            var isLast = i == packets.Count - 1;
            OggPageWriter.WritePage(stream, serialNumber: 1, sequence: (uint)(2 + i), granule: i + 1,
                headerType: isLast ? OggPageWriter.EndOfStream : OggPageWriter.Continuation, [packets[i]]);
        }

        return stream.ToArray();
    }

    private static byte[] BuildOpusHead(int inputSampleRate)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("OpusHead"u8.ToArray());
        writer.Write((byte)1); // version
        writer.Write((byte)1); // channels (mono)
        writer.Write((ushort)0); // pre-skip
        writer.Write((uint)inputSampleRate);
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
