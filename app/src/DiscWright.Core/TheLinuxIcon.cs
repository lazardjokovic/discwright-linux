using System.Buffers.Binary;

namespace DiscWright.Core;

/// <summary>
/// The disc's icon as Linux wants it: one PNG, made from the Windows icon
/// that is already on the disc.
/// </summary>
/// <remarks>
/// <para>
/// A Linux file manager will not wear a <c>.ico</c>. It wants a PNG, named in
/// <see cref="XdgVolumeInfo"/>. The picture is the largest frame of the icon
/// the disc already carries, taken out of the file and written again as a
/// PNG, so the two cannot be different pictures and nothing is drawn twice.
/// </para>
/// <para>
/// The PowerShell app drew it a second time from the picture somebody chose,
/// through GDI+, and for any icon that held a 256 pixel PNG frame the result
/// was noise, or a blur, or a failure (fixed in 0.7.4). Reading the frame's
/// own bytes has no decoder in it to get that wrong.
/// </para>
/// </remarks>
public static class TheLinuxIcon
{
    /// <summary>
    /// The most this file can be: 256 by 256, four bytes a pixel, and room
    /// for a PNG's own furniture. What the sum of a disc allows for it.
    /// </summary>
    public const long AtMost = (256L * 256 * 4) + 4096;

    /// <summary>
    /// What the PNG is called: the icon's own name and place, as a PNG.
    /// </summary>
    /// <remarks>
    /// Beside the icon and named after it, so that a name the disc already
    /// keeps for itself keeps this one too, and so two discs in a row do not
    /// hand a desktop the same file name for different pictures.
    /// </remarks>
    public static string NameBeside(string iconOnTheDisc)
    {
        int dot = iconOnTheDisc.LastIndexOf('.');
        int slash = iconOnTheDisc.LastIndexOfAny(['/', (char)92]);

        return (dot > slash ? iconOnTheDisc[..dot] : iconOnTheDisc) + ".png";
    }

    /// <summary>
    /// Whether a name at the top of the disc is the one this would be written
    /// under, for a disc called this.
    /// </summary>
    public static bool Claims(string name, string? label) =>
        string.Equals(name, NameBeside(DiscIconName.For(label)), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The largest frame of a Windows icon, as a PNG, or nothing when the
    /// file is not an icon this can read.
    /// </summary>
    /// <remarks>
    /// Nothing, and never an exception. A disc that shows a plain icon on
    /// Linux is still a working disc, so this is never what stops a build.
    /// </remarks>
    public static byte[]? FromTheWindowsOne(byte[] ico)
    {
        // ICONDIR: reserved, type 1 meaning icon, how many frames.
        if (ico.Length < 6
            || BinaryPrimitives.ReadUInt16LittleEndian(ico) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)) != 1)
        {
            return null;
        }

        int frames = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4));

        if (ico.Length < 6 + (16 * frames))
        {
            return null;
        }

        int best = -1;
        int bestSize = 0;

        for (int at = 0; at < frames; at++)
        {
            // One byte holds the size, and 256 is written as 0.
            int size = ico[6 + (16 * at)] == 0 ? 256 : ico[6 + (16 * at)];

            if (size > bestSize)
            {
                best = at;
                bestSize = size;
            }
        }

        if (best < 0)
        {
            return null;
        }

        long length = BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(6 + (16 * best) + 8));
        long offset = BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(6 + (16 * best) + 12));

        if (offset + length > ico.Length || length < 40)
        {
            return null;
        }

        ReadOnlySpan<byte> frame = ico.AsSpan((int)offset, (int)length);

        // A frame may be a PNG already, which is how most icons made since
        // Vista keep their largest one. Then it is the file, as it stands.
        if (frame.StartsWith(APngFile.Signature))
        {
            return frame.ToArray();
        }

        return FromABitmap(frame, bestSize);
    }

    /// <summary>
    /// A frame kept as a bitmap, which is how DiscWright writes every frame.
    /// </summary>
    private static byte[]? FromABitmap(ReadOnlySpan<byte> frame, int size)
    {
        int header = BinaryPrimitives.ReadInt32LittleEndian(frame);
        int width = BinaryPrimitives.ReadInt32LittleEndian(frame[4..]);

        // The height is doubled: the colour rows and the mask rows are
        // described together.
        int height = BinaryPrimitives.ReadInt32LittleEndian(frame[8..]) / 2;
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(frame[14..]);
        int squeezed = BinaryPrimitives.ReadInt32LittleEndian(frame[16..]);

        if (header < 40 || width != size || height != size || bits != 32 || squeezed != 0
            || frame.Length < header + (size * size * 4))
        {
            return null;
        }

        byte[] rgba = new byte[size * size * 4];
        int row = size * 4;

        for (int at = 0; at < size; at++)
        {
            // Bottom row first in the bitmap, top row first in a PNG, and
            // blue first in the bitmap where a PNG has red.
            ReadOnlySpan<byte> from = frame.Slice(header + ((size - 1 - at) * row), row);
            Span<byte> to = rgba.AsSpan(at * row, row);

            for (int pixel = 0; pixel < row; pixel += 4)
            {
                to[pixel] = from[pixel + 2];
                to[pixel + 1] = from[pixel + 1];
                to[pixel + 2] = from[pixel];
                to[pixel + 3] = from[pixel + 3];
            }
        }

        return APngFile.From(size, size, rgba);
    }
}
