using System.IO;
using NLayer;

namespace Automation.Helpers;

/// <summary>
/// Decodes MP3 audio (e.g. Home Assistant Cloud's text-to-speech output) into 16-bit PCM, using NLayer, a
/// pure .NET decoder, so this works without ffmpeg or any other native dependency on the host.
/// </summary>
public static class Mp3Decoder
{
    /// <summary>
    /// Decodes a mono MP3 file to 16-bit little-endian PCM, at whatever sample rate the file itself uses.
    /// </summary>
    /// <param name="mp3">The raw bytes of the MP3 file.</param>
    /// <returns>The decoded PCM, and the sample rate it is at.</returns>
    /// <exception cref="NotSupportedException">The file is not mono.</exception>
    public static (byte[] Pcm, int SampleRate) Decode(byte[] mp3)
    {
        using var stream = new MemoryStream(mp3);
        using var reader = new MpegFile(stream);

        if (reader.Channels != 1)
            throw new NotSupportedException($"Expected a mono MP3 file, but it has {reader.Channels} channels");

        using var pcm = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = reader.ReadSamplesInt16(buffer, 0, buffer.Length)) > 0)
            pcm.Write(buffer, 0, read);

        return (pcm.ToArray(), reader.SampleRate);
    }
}
