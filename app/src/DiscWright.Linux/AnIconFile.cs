namespace DiscWright.Linux;

/// <summary>
/// A Windows icon file, put together from frames that are already drawn.
/// </summary>
/// <remarks>
/// <para>
/// Pixels in, bytes out, and nothing here draws anything. The container is
/// the part of an icon that goes wrong quietly: written slightly out, it is
/// still a file of the right size and fails only when Explorer tries to draw
/// it, on a disc somebody has already burned.
/// </para>
/// <para>
/// It is the same file <c>WindowsImages</c> writes, frame for frame: seven
/// sizes, each a 32 bit bitmap with its mask, none of them a PNG. The two are
/// written twice for now because nothing in the Windows app is moved until
/// this work goes into its repository, and that is the day they become one.
/// </para>
/// </remarks>
public static class AnIconFile
{
    /// <summary>
    /// The sizes a Windows icon carries. Explorer asks for 16 in a list and 256
    /// on the desktop, and given only one it scales the rest itself, badly.
    /// </summary>
    public static IReadOnlyList<int> Sizes { get; } = [16, 24, 32, 48, 64, 128, 256];

    /// <summary>
    /// The whole file, from one square frame for each size.
    /// </summary>
    /// <param name="frames">
    /// Each frame's pixels, top row first, four bytes a pixel in the order
    /// blue, green, red, alpha, with the colour NOT multiplied by the alpha.
    /// </param>
    public static byte[] From(IReadOnlyList<(int Size, byte[] Pixels)> frames)
    {
        List<byte[]> bitmaps = [.. frames.Select(frame => BitmapOf(frame.Size, frame.Pixels))];

        using MemoryStream file = new();
        using BinaryWriter write = new(file);

        // ICONDIR: reserved, type 1 meaning icon, how many frames.
        write.Write((short)0);
        write.Write((short)1);
        write.Write((short)bitmaps.Count);

        // Every entry is written before any pixel data, so each one has to know
        // where its own data will land.
        int offset = 6 + (16 * bitmaps.Count);

        for (int at = 0; at < bitmaps.Count; at++)
        {
            int size = frames[at].Size;

            // 256 is written as 0. One byte holds the size, and 256 does not fit
            // in one byte, so the format spends its only spare value on it.
            write.Write((byte)(size == 256 ? 0 : size));
            write.Write((byte)(size == 256 ? 0 : size));
            write.Write((byte)0);
            write.Write((byte)0);
            write.Write((short)1);
            write.Write((short)32);
            write.Write(bitmaps[at].Length);
            write.Write(offset);

            offset += bitmaps[at].Length;
        }

        foreach (byte[] bitmap in bitmaps)
        {
            write.Write(bitmap);
        }

        write.Flush();

        return file.ToArray();
    }

    /// <summary>
    /// One frame as an icon file wants it: a bitmap header, the pixels upside
    /// down, and a mask.
    /// </summary>
    private static byte[] BitmapOf(int size, byte[] pixels)
    {
        if (pixels.Length != size * size * 4)
        {
            throw new ArgumentException(
                $"A {size} pixel frame is {size * size * 4} bytes, and this one is {pixels.Length}.", nameof(pixels));
        }

        using MemoryStream made = new();
        using BinaryWriter write = new(made);

        // BITMAPINFOHEADER. The height is DOUBLED, because the format expects
        // the colour rows and the mask rows stacked together and describes
        // both at once.
        write.Write(40);
        write.Write(size);
        write.Write(size * 2);
        write.Write((short)1);
        write.Write((short)32);
        write.Write(0);
        write.Write(0);
        write.Write(0);
        write.Write(0);
        write.Write(0);
        write.Write(0);

        // Bottom row first, which is how a bitmap has stored its rows since
        // 1990 and is the single most common way to get an icon upside down.
        for (int row = size - 1; row >= 0; row--)
        {
            write.Write(pixels, row * size * 4, size * 4);
        }

        // The mask, one bit a pixel, each row padded out to four bytes. All
        // zeroes, because the alpha in the pixels above already says what is
        // transparent, but it has to be here and it has to be the right size
        // or everything after it is read at the wrong offset.
        int maskRow = ((size + 31) / 32) * 4;
        write.Write(new byte[maskRow * size]);

        write.Flush();

        return made.ToArray();
    }
}
