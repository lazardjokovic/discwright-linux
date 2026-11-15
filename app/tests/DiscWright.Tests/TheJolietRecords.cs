using System.Buffers.Binary;
using System.Text;

namespace DiscWright.Tests;

/// <summary>
/// What an image really holds in the names Windows reads, read straight out
/// of the records on it.
/// </summary>
/// <remarks>
/// Read from the records and not asked of xorriso, because xorriso's own
/// reader is not a witness for what Windows sees. Measured for the Python
/// DiscWright: asked for the Joliet tree, xorriso reported a name whole while
/// the record on the disc held it cut to 64 characters. Windows reads the
/// record, so the tests do too.
/// </remarks>
internal static class TheJolietRecords
{
    private const int Sector = 2048;

    /// <summary>A file as the image records it: where it starts and how long it is.</summary>
    internal sealed record Recorded(string Path, long StartsAtSector, long Bytes);

    /// <summary>The volume name in the first descriptor, the one a drive is labelled with.</summary>
    internal static string VolumeNameOf(string image)
    {
        using FileStream file = File.OpenRead(image);

        byte[] descriptor = Read(file, 16, Sector);

        return Encoding.ASCII.GetString(descriptor, 40, 32).TrimEnd(' ');
    }

    /// <summary>Every file in the Joliet tree, as a path with forward slashes.</summary>
    internal static IReadOnlyList<Recorded> FilesIn(string image)
    {
        using FileStream file = File.OpenRead(image);

        byte[]? root = null;

        for (int sector = 16; sector < 32 && root is null; sector++)
        {
            byte[] descriptor = Read(file, sector, Sector);

            // A supplementary descriptor whose escape sequence says UCS-2 is
            // the Joliet one. 255 ends the list.
            if (descriptor[0] == 2 && descriptor[88] == '%' && descriptor[89] == '/')
            {
                root = descriptor[156..190];
            }
            else if (descriptor[0] == 255)
            {
                break;
            }
        }

        if (root is null)
        {
            throw new InvalidDataException("The image has no Joliet names in it at all.");
        }

        List<Recorded> found = [];
        Walk(file, root, string.Empty, found);

        return found;
    }

    /// <summary>The bytes of one recorded file, read out of the image.</summary>
    internal static byte[] BytesOf(string image, Recorded one)
    {
        using FileStream file = File.OpenRead(image);

        return Read(file, one.StartsAtSector, (int)one.Bytes);
    }

    private static void Walk(FileStream file, byte[] folder, string under, List<Recorded> found)
    {
        long sector = BinaryPrimitives.ReadUInt32LittleEndian(folder.AsSpan(2, 4));
        int bytes = (int)BinaryPrimitives.ReadUInt32LittleEndian(folder.AsSpan(10, 4));
        byte[] records = Read(file, sector, bytes);

        int at = 0;

        while (at < records.Length)
        {
            int length = records[at];

            if (length == 0)
            {
                // A record never crosses a sector, so the rest of this one is
                // padding and the next record starts the next sector.
                at = ((at / Sector) + 1) * Sector;
                continue;
            }

            byte[] record = records[at..(at + length)];
            at += length;

            int nameLength = record[32];

            // The folder itself and the one above it, which are a single byte.
            if (nameLength == 1 && record[33] <= 1)
            {
                continue;
            }

            string name = Encoding.BigEndianUnicode.GetString(record, 33, nameLength);

            if ((record[25] & 2) != 0)
            {
                Walk(file, record, under + name + "/", found);
            }
            else
            {
                found.Add(new Recorded(
                    under + name.Split(';')[0],
                    BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(2, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(10, 4))));
            }
        }
    }

    private static byte[] Read(FileStream file, long sector, int bytes)
    {
        byte[] buffer = new byte[bytes];
        file.Seek(sector * Sector, SeekOrigin.Begin);
        file.ReadExactly(buffer);

        return buffer;
    }
}
