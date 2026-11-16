using System.Buffers.Binary;
using System.IO.Compression;

namespace DiscWright.Core;

/// <summary>
/// A PNG file, put together by hand from the pixels.
/// </summary>
/// <remarks>
/// <para>
/// By hand because this is the one picture both programs have to write the
/// same way, and they draw with different things: GDI+ on Windows, Skia on
/// Linux. A PNG is a header, the rows squeezed with zlib, and an end, and
/// .NET has zlib. So the Linux icon needs no drawing library at all, comes
/// out the same on either machine from the same icon, and can be tested where
/// there is no screen and no Skia.
/// </para>
/// <para>
/// Eight bits a channel with alpha, no interlacing, every row unfiltered. It
/// is not the smallest file a PNG can be. It is one every reader opens.
/// </para>
/// </remarks>
public static class APngFile
{
    /// <summary>The eight bytes every PNG starts with.</summary>
    public static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// A PNG of these pixels.
    /// </summary>
    /// <param name="width">How many pixels across.</param>
    /// <param name="height">How many rows.</param>
    /// <param name="rgba">
    /// Red, green, blue, alpha, a byte each, top row first, alpha straight
    /// and not multiplied in.
    /// </param>
    public static byte[] From(int width, int height, byte[] rgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException(
                $"A picture {width} by {height} is {width * height * 4} bytes, and this is {rgba.Length}.", nameof(rgba));
        }

        using MemoryStream file = new();

        file.Write(Signature);

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;   // bits a channel
        header[9] = 6;   // red, green, blue and alpha
        header[10] = 0;  // the only way of squeezing there is
        header[11] = 0;  // the only set of filters there is
        header[12] = 0;  // not interlaced

        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", Squeezed(width, height, rgba));
        Chunk(file, "IEND", []);

        return file.ToArray();
    }

    /// <summary>
    /// The rows, each with the byte in front that says how it was filtered,
    /// which here is always "it was not".
    /// </summary>
    private static byte[] Squeezed(int width, int height, byte[] rgba)
    {
        using MemoryStream squeezed = new();

        using (ZLibStream zlib = new(squeezed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            int row = width * 4;

            for (int at = 0; at < height; at++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgba, at * row, row);
            }
        }

        return squeezed.ToArray();
    }

    /// <summary>
    /// One chunk: how long, what it is, the bytes, and a check over the last two.
    /// </summary>
    private static void Chunk(Stream file, string kind, byte[] bytes)
    {
        Span<byte> four = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(four, bytes.Length);
        file.Write(four);

        byte[] named = new byte[4 + bytes.Length];

        for (int at = 0; at < 4; at++)
        {
            named[at] = (byte)kind[at];
        }

        bytes.CopyTo(named, 4);
        file.Write(named);

        BinaryPrimitives.WriteUInt32BigEndian(four, CheckOf(named));
        file.Write(four);
    }

    /// <summary>
    /// The CRC a PNG wants, which is the ordinary one. Worked out a bit at a
    /// time: a table is faster and this is run over a third of a megabyte,
    /// once a disc.
    /// </summary>
    public static uint CheckOf(ReadOnlySpan<byte> bytes)
    {
        uint check = 0xFFFFFFFF;

        foreach (byte each in bytes)
        {
            check ^= each;

            for (int bit = 0; bit < 8; bit++)
            {
                check = (check & 1) != 0 ? (check >> 1) ^ 0xEDB88320 : check >> 1;
            }
        }

        return check ^ 0xFFFFFFFF;
    }
}
