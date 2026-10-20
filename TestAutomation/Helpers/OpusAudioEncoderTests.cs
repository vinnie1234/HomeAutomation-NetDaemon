using Automation.Helpers;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Helpers;

public class OpusAudioEncoderTests
{
    [Fact]
    public void EncodeToOggOpus_ProducesAFileThatDecodesBackToTheSameAudio()
    {
        const int inputRate = 16000;
        var original = GenerateSineWave(inputRate, frequencyHz: 440, durationSeconds: 1.0);
        var pcmBytes = new byte[original.Length * 2];
        Buffer.BlockCopy(original, 0, pcmBytes, 0, pcmBytes.Length);

        var ogg = OpusAudioEncoder.EncodeToOggOpus(pcmBytes, inputRate);

        var result = OpusAudioDecoder.DecodeToPcm16(ogg, OpusAudioEncoder.WhatsAppVoiceNoteSampleRate);
        result.SkippedPacketCount.Should().Be(0);

        var decoded = new short[result.Pcm.Length / 2];
        Buffer.BlockCopy(result.Pcm, 0, decoded, 0, result.Pcm.Length);
        decoded.Length.Should().BeCloseTo(inputRate, 1600);
        decoded.Max(s => Math.Abs((int)s)).Should().BeGreaterThan(1000);
    }

    [Fact]
    public void EncodeToOggOpus_ResamplesWhenTheInputIsAtADifferentRate()
    {
        // Home Assistant Cloud's TTS produces 24kHz; the encoded file must still end up at WhatsApp's own rate
        const int inputRate = 24000;
        var original = GenerateSineWave(inputRate, frequencyHz: 440, durationSeconds: 1.0);
        var pcmBytes = new byte[original.Length * 2];
        Buffer.BlockCopy(original, 0, pcmBytes, 0, pcmBytes.Length);

        var ogg = OpusAudioEncoder.EncodeToOggOpus(pcmBytes, inputRate);

        var result = OpusAudioDecoder.DecodeToPcm16(ogg, OpusAudioEncoder.WhatsAppVoiceNoteSampleRate);
        var decoded = new short[result.Pcm.Length / 2];
        Buffer.BlockCopy(result.Pcm, 0, decoded, 0, result.Pcm.Length);

        decoded.Length.Should().BeCloseTo(OpusAudioEncoder.WhatsAppVoiceNoteSampleRate, 1600);
    }

    private static short[] GenerateSineWave(int sampleRate, double frequencyHz, double durationSeconds)
    {
        var sampleCount = (int)(sampleRate * durationSeconds);
        var samples = new short[sampleCount];
        for (var i = 0; i < sampleCount; i++)
            samples[i] = (short)(short.MaxValue * 0.5 * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate));
        return samples;
    }
}
