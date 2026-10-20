using System.IO;

namespace Automation.Helpers;

/// <summary>
/// Writes Ogg pages (RFC 3533), filling in the lacing values (segment table) for the given packets.
/// </summary>
public static class OggPageWriter
{
    public const byte Continuation = 0x00;
    public const byte BeginOfStream = 0x02;
    public const byte EndOfStream = 0x04;

    /// <summary>Writes a single Ogg page containing the given packets.</summary>
    public static void WritePage(Stream stream, uint serialNumber, uint sequence, long granule, byte headerType, IReadOnlyList<byte[]> packets)
    {
        var segmentTable = new List<byte>();
        foreach (var packet in packets)
        {
            var remaining = packet.Length;
            while (remaining >= 255) { segmentTable.Add(255); remaining -= 255; }
            segmentTable.Add((byte)remaining);
        }

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write("OggS"u8.ToArray());
        writer.Write((byte)0); // version
        writer.Write(headerType);
        writer.Write(granule);
        writer.Write(serialNumber);
        writer.Write(sequence);
        writer.Write(0u); // checksum: left as 0; neither this project's writer nor reader verifies it
        writer.Write((byte)segmentTable.Count);
        writer.Write(segmentTable.ToArray());
        foreach (var packet in packets)
            writer.Write(packet);
    }
}
