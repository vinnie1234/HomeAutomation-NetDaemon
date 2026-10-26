using Automation.Helpers;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Helpers;

public class OpusAudioDecoderTests
{
    [Fact]
    public void DecodeToPcm16_DoesNotProduceSilence_ForARealHybridModePacketDecodedBelow48kHz()
    {
        // A real Hybrid-mode (SILK+CELT) Opus packet captured from a live WebRTC call, from the report of
        // https://github.com/lostromb/concentus/issues/67: Concentus silently decodes Hybrid-mode packets
        // to all-zero PCM when the output rate is below the native 48kHz, with no error or exception. Real
        // speech (e.g. WhatsApp voice notes) commonly uses Hybrid mode, so this must be worked around.
        var packet = Convert.FromBase64String(
            "eAQPIzMOKgxLnJI+goxXFNRlSWLRdqvLJUtfncLBYzBzkBMB3sREp2Tb9E0M8uIz+FtWp/N+078z6ZBsSLmP1XJ/IJgOwBHut34=");
        var ogg = OggOpusTestFile.EncodeRawPackets([packet], inputSampleRate: 48000);

        var result = OpusAudioDecoder.DecodeToPcm16(ogg, outputSampleRate: 16000);

        result.SkippedPacketCount.Should().Be(0);
        var decoded = ToInt16Samples(result.Pcm);
        decoded.Should().NotBeEmpty();
        decoded.Max(s => Math.Abs((int)s)).Should().BeGreaterThan(100);
    }

    [Fact]
    public void DecodeToPcm16_RoundTripsASineWave_AtTheRequestedSampleRate()
    {
        const int inputRate = 16000;
        const int outputRate = 16000;
        const double frequencyHz = 440;
        const double durationSeconds = 1.0;

        var original = GenerateSineWave(inputRate, frequencyHz, durationSeconds);
        var ogg = OggOpusTestFile.Encode(original, inputRate);

        var result = OpusAudioDecoder.DecodeToPcm16(ogg, outputRate);
        result.SkippedPacketCount.Should().Be(0);
        var decoded = ToInt16Samples(result.Pcm);

        // Opus is lossy and the exact sample count can differ slightly (frame boundaries, pre-skip rounding),
        // so compare duration and signal energy rather than requiring a bit-exact match.
        var expectedSamples = (int)(inputRate * durationSeconds);
        decoded.Length.Should().BeCloseTo(expectedSamples, (inputRate / 10));

        var originalRms = Rms(original);
        var decodedRms = Rms(decoded);
        decodedRms.Should().BeApproximately(originalRms, originalRms * 0.3);
    }

    [Fact]
    public void DecodeToPcm16_CanDecodeToADifferentSampleRateThanTheOriginal()
    {
        var original = GenerateSineWave(sampleRate: 48000, frequencyHz: 440, durationSeconds: 0.5);
        var ogg = OggOpusTestFile.Encode(original, sampleRate: 48000);

        var result = OpusAudioDecoder.DecodeToPcm16(ogg, outputSampleRate: 16000);
        var decoded = ToInt16Samples(result.Pcm);

        // Half a second at 16kHz, within the tolerance of frame-boundary rounding
        decoded.Length.Should().BeCloseTo(8000, 1600);
    }

    [Fact]
    public void DecodeToPcm16_SkipsASingleCorruptPacket_InsteadOfLosingTheWholeMessage()
    {
        // Seen in practice with a real WhatsApp voice note (root cause in the Opus bitstream unconfirmed):
        // Concentus throws OpusException deep inside native decoding for a single packet. One bad frame
        // in an otherwise valid stream must not make the whole voice note fail to transcribe.
        var original = GenerateSineWave(16000, 440, 1.0);
        var ogg = CorruptOneAudioPacket(OggOpusTestFile.Encode(original, 16000));

        var result = OpusAudioDecoder.DecodeToPcm16(ogg, 16000);
        result.SkippedPacketCount.Should().Be(1);
        var decoded = ToInt16Samples(result.Pcm);

        // Only the one corrupted 20ms frame (320 samples at 16kHz) is missing from the second of audio
        decoded.Length.Should().BeGreaterThan(16000 - 320 * 2);
    }

    /// <summary>
    /// Overwrites one audio packet's payload (leaving the Ogg page structure and lacing values intact) with
    /// bytes that are not a valid Opus packet: a TOC byte requesting 4+ VBR frames with a frame count the
    /// packet is too short to actually contain.
    /// </summary>
    private static byte[] CorruptOneAudioPacket(byte[] ogg)
    {
        var corrupted = ogg.ToArray();
        var pageIndex = 0;
        var offset = 0;

        while (offset < corrupted.Length)
        {
            var segmentCount = corrupted[offset + 26];
            var packetStart = offset + 27 + segmentCount;
            var packetLength = corrupted.AsSpan(offset + 27, segmentCount).ToArray().Sum(b => b);

            // Pages 0 and 1 are the OpusHead and OpusTags packets; corrupt the first audio packet after those
            if (pageIndex == 2 && packetLength >= 2)
            {
                corrupted[packetStart] = 0xFF; // TOC byte: code 3 (arbitrary frame count), stereo, config 31
                corrupted[packetStart + 1] = 0xFF; // frame count byte: VBR, padding, count 63 (invalid, max is 48)
                return corrupted;
            }

            offset = packetStart + packetLength;
            pageIndex++;
        }

        throw new InvalidOperationException("Test setup error: no audio page found to corrupt");
    }

    private static short[] GenerateSineWave(int sampleRate, double frequencyHz, double durationSeconds)
    {
        var sampleCount = (int)(sampleRate * durationSeconds);
        var samples = new short[sampleCount];
        for (var i = 0; i < sampleCount; i++)
            samples[i] = (short)(short.MaxValue * 0.5 * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate));
        return samples;
    }

    private static double Rms(short[] samples) =>
        Math.Sqrt(samples.Select(s => (double)s * s).Average());

    private static short[] ToInt16Samples(byte[] pcmBytes)
    {
        var samples = new short[pcmBytes.Length / 2];
        Buffer.BlockCopy(pcmBytes, 0, samples, 0, pcmBytes.Length);
        return samples;
    }
}
