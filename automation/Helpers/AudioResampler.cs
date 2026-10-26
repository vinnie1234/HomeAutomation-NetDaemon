using Concentus;

namespace Automation.Helpers;

/// <summary>
/// Resamples 16-bit PCM audio, using Concentus's bundled SpeexDSP-based resampler.
/// </summary>
public static class AudioResampler
{
    public static short[] Resample(short[] pcm, int channels, int inputRate, int outputRate)
    {
        if (pcm.Length == 0 || inputRate == outputRate) return pcm;

        var resampler = ResamplerFactory.CreateResampler(channels, inputRate, outputRate, 7);
        var samplesPerChannel = pcm.Length / channels;
        var outCapacityPerChannel = (int)((long)samplesPerChannel * outputRate / inputRate) + 64;

        if (channels == 1)
        {
            var output = new short[outCapacityPerChannel];
            var inLen = samplesPerChannel;
            var outLen = output.Length;
            resampler.Process(0, pcm, ref inLen, output, ref outLen);
            return [.. output.AsSpan(0, outLen)];
        }

        var interleavedOutput = new short[outCapacityPerChannel * channels];
        var inLenInterleaved = samplesPerChannel;
        var outLenInterleaved = outCapacityPerChannel;
        resampler.ProcessInterleaved(pcm, ref inLenInterleaved, interleavedOutput, ref outLenInterleaved);
        return [.. interleavedOutput.AsSpan(0, outLenInterleaved * channels)];
    }
}
