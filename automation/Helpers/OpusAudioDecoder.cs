using System.IO;
using Concentus;

namespace Automation.Helpers;

/// <summary>
/// Decodes an Ogg-contained Opus audio stream (as used by WhatsApp voice notes) into 16-bit PCM, using a pure
/// .NET Opus decoder (Concentus) so this works without ffmpeg or any other native dependency on the host.
/// </summary>
public static class OpusAudioDecoder
{
    /// <summary>The "OggS" page magic that starts every Ogg page.</summary>
    private static readonly byte[] PageMagic = "OggS"u8.ToArray();

    /// <summary>The Opus identification header packet, which must be the first packet of the stream.</summary>
    private static readonly byte[] OpusHeadMagic = "OpusHead"u8.ToArray();

    /// <summary>
    /// Opus's native sample rate. Decoding always happens at this rate, then is resampled down ourselves if a
    /// lower output rate is requested: Concentus has a confirmed bug
    /// (<see href="https://github.com/lostromb/concentus/issues/67"/>) where decoding Hybrid-mode packets
    /// (common in real speech, e.g. most WhatsApp voice notes) directly to a rate below 48kHz silently
    /// produces all-zero output for the CELT-coded part of the signal, with no error or exception.
    /// </summary>
    private const int NativeSampleRate = 48_000;

    /// <summary>
    /// Decodes an Ogg Opus audio stream to mono, 16-bit little-endian PCM at <paramref name="outputSampleRate"/>.
    /// </summary>
    /// <param name="oggOpus">The raw bytes of the Ogg Opus file (e.g. a decrypted WhatsApp voice note).</param>
    /// <param name="outputSampleRate">The PCM sample rate to decode to; must be one Opus supports (8000, 12000, 16000, 24000 or 48000).</param>
    /// <returns>The decoded audio, and how much of it could not be decoded.</returns>
    public static OpusDecodeResult DecodeToPcm16(ReadOnlySpan<byte> oggOpus, int outputSampleRate)
    {
        var packets = ReadPackets(oggOpus);
        if (packets.Count < 2)
            throw new InvalidDataException("The Ogg stream does not contain an Opus header and comment packet");

        var header = ParseOpusHead(packets[0]);
        var decoder = OpusCodecFactory.CreateDecoder(NativeSampleRate, header.Channels);

        // RFC 7845: discard the first PreSkip samples of decoded audio; they are encoder priming data, not
        // part of the actual sound. PreSkip is always defined in 48kHz units, matching the decode rate here.
        var samplesToSkip = header.PreSkip;

        var maxFrameSize = NativeSampleRate * 120 / 1000; // the longest Opus frame is 120ms
        var frameBuffer = new short[maxFrameSize * header.Channels];
        var pcm = new List<short>();
        var skippedPackets = 0;

        // packets[0] is the identification header and packets[1] the comment header; audio starts at packets[2]
        for (var i = 2; i < packets.Count; i++)
        {
            int decoded;
            try
            {
                decoded = decoder.Decode(packets[i], frameBuffer, maxFrameSize, decode_fec: false);
            }
            catch (OpusException)
            {
                // A single malformed packet (seen in practice with real voice notes, cause unconfirmed) must
                // not lose the whole message: skip it, the same way a decoder tolerates a lost network packet.
                skippedPackets++;
                continue;
            }

            var samples = decoded * header.Channels;

            if (samplesToSkip > 0)
            {
                var skip = Math.Min(samplesToSkip, samples);
                samplesToSkip -= skip;
                if (skip == samples) continue;
                pcm.AddRange(frameBuffer.AsSpan(skip, samples - skip).ToArray());
            }
            else
            {
                pcm.AddRange(frameBuffer.AsSpan(0, samples).ToArray());
            }
        }

        var pcm48KHz = pcm.ToArray();
        var resampled = AudioResampler.Resample(pcm48KHz, header.Channels, NativeSampleRate, outputSampleRate);

        var bytes = new byte[resampled.Length * 2];
        Buffer.BlockCopy(resampled, 0, bytes, 0, bytes.Length);
        return new OpusDecodeResult { Pcm = bytes, AudioPacketCount = packets.Count - 2, SkippedPacketCount = skippedPackets };
    }

    private sealed record OpusHead(int Channels, int PreSkip);

    /// <summary>
    /// Parses the Opus identification header packet (RFC 7845 section 5.1).
    /// </summary>
    private static OpusHead ParseOpusHead(byte[] packet)
    {
        if (packet.Length < 19 || !packet.AsSpan(0, 8).SequenceEqual(OpusHeadMagic))
            throw new InvalidDataException("The first packet of the Ogg stream is not an Opus identification header");

        var channels = packet[9];
        var preSkip = BitConverter.ToUInt16(packet, 10);
        return new OpusHead(channels, preSkip);
    }

    /// <summary>
    /// Demuxes an Ogg stream into its packets, by reading pages (RFC 3533) and reassembling packets that
    /// are split across multiple pages or multiple segments of a page.
    /// </summary>
    private static List<byte[]> ReadPackets(ReadOnlySpan<byte> ogg)
    {
        var packets = new List<byte[]>();
        var current = new List<byte>();
        var offset = 0;

        while (offset < ogg.Length)
        {
            if (ogg.Length - offset < 27 || !ogg.Slice(offset, 4).SequenceEqual(PageMagic))
                throw new InvalidDataException("Expected an Ogg page here, but did not find one");

            var headerType = ogg[offset + 5];
            var segmentCount = ogg[offset + 26];
            var segmentTable = ogg.Slice(offset + 27, segmentCount);
            var dataOffset = offset + 27 + segmentCount;

            // A page that doesn't continue the previous packet starts a fresh one
            if ((headerType & 0x01) == 0 && current.Count > 0)
            {
                packets.Add(current.ToArray());
                current = [];
            }

            foreach (var segmentLength in segmentTable)
            {
                current.AddRange(ogg.Slice(dataOffset, segmentLength).ToArray());
                dataOffset += segmentLength;

                // A segment shorter than 255 ends the packet; 255 means the packet continues in the next segment
                if (segmentLength < 255)
                {
                    packets.Add(current.ToArray());
                    current = [];
                }
            }

            offset = dataOffset;
        }

        // A packet left over without a terminating page is incomplete and must be dropped
        return packets;
    }
}
