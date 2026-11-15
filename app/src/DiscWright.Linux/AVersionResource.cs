using System.Buffers.Binary;
using System.Text;

namespace DiscWright.Linux;

/// <summary>
/// The version strings inside a Windows executable, read by walking the file.
/// </summary>
/// <remarks>
/// <para>
/// A GOG installer is an Inno Setup program, and the game's name is in its
/// version resource as ProductName. On Windows that is one call to
/// <c>FileVersionInfo</c>. On Linux the same call answers with an empty string
/// for every Windows executable there is, with no exception to say so:
/// measured on Ubuntu on 2026-11-15 against <c>where.exe</c>,
/// <c>charmap.exe</c> and a real GOG installer that Windows reads as "Alan
/// Wake". So the format is walked here by hand.
/// </para>
/// <para>
/// It reads the headers, the section table and the one section that holds the
/// resources, and nothing else. An installer with no .bin parts can be several
/// gigabytes, and none of that is loaded.
/// </para>
/// <para>
/// Ported from <c>pe.py</c> in the Python DiscWright, which agreed with
/// Windows on four real GOG installers and seven patches.
/// </para>
/// </remarks>
public static class AVersionResource
{
    /// <summary>The resource type Windows files a version under.</summary>
    private const uint VersionType = 16;

    /// <summary>Which of the optional header's directories is the resources.</summary>
    private const int ResourceDirectory = 2;

    /// <summary>
    /// A version resource is a few hundred bytes. A section claiming to be
    /// larger than this is a damaged file, and reading it would mean loading
    /// whatever the damaged number says.
    /// </summary>
    private const long LargestResourceSection = 64L << 20;

    /// <summary>A subdirectory rather than a leaf, in the resource tree.</summary>
    private const uint IsADirectory = 0x80000000;

    /// <summary>
    /// Every string in the version resource, or null when the file has none,
    /// is not a Windows executable, or cannot be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null and never an exception. A download that stopped half way, a disc
    /// going bad and a file that is not a program at all are ordinary things
    /// to be pointed at, and "it has no name in it" is the whole answer to
    /// each of them.
    /// </para>
    /// <para>
    /// Values come back as stored, with the spaces Inno Setup pads them with,
    /// because that is what Windows hands over too. With more than one string
    /// table, one per language, the first is used. Windows picks by the
    /// language list; GOG installers carry a single table, so the two agree on
    /// everything this is used for.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<string, string>? StringsIn(string path)
    {
        try
        {
            using FileStream file = File.OpenRead(path);

            return Read(file);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            // A number in the file that points outside the file. Every read
            // below goes through a span, so a damaged offset ends here rather
            // than as a wrong answer.
            return null;
        }
    }

    private static Dictionary<string, string>? Read(FileStream file)
    {
        byte[] head = UpTo(file, 4096);

        if (head.Length < 2 || head[0] != 'M' || head[1] != 'Z')
        {
            return null;
        }

        long pe = U32(head, 0x3C);

        if (pe + 24 > head.Length || U32(head, pe) != 0x00004550)
        {
            return null;
        }

        int sections = U16(head, pe + 6);
        long optionalHeader = pe + 24;
        long sectionTable = optionalHeader + U16(head, pe + 20);

        // The two layouts differ only in where the directories start.
        long directories;

        switch (U16(head, optionalHeader))
        {
            case 0x10B:
                directories = optionalHeader + 96;
                break;
            case 0x20B:
                directories = optionalHeader + 112;
                break;
            default:
                return null;
        }

        if (U32(head, directories - 4) <= ResourceDirectory)
        {
            return null;
        }

        long resources = U32(head, directories + (8 * ResourceDirectory));

        if (resources == 0)
        {
            return null;
        }

        // Find the section holding the resources, and read just that one.
        for (int index = 0; index < sections; index++)
        {
            long section = sectionTable + (40 * index);
            long address = U32(head, section + 12);
            long sizeOnDisk = U32(head, section + 16);
            long whereOnDisk = U32(head, section + 20);
            long sizeInMemory = U32(head, section + 8);

            if (resources < address || resources >= address + Math.Max(sizeInMemory, sizeOnDisk))
            {
                continue;
            }

            if (sizeOnDisk > LargestResourceSection)
            {
                return null;
            }

            file.Seek(whereOnDisk, SeekOrigin.Begin);

            return FromTheResources(UpTo(file, (int)sizeOnDisk), resources - address, address);
        }

        return null;
    }

    /// <summary>
    /// Type, then name, then language, then the data: three levels down to the
    /// first version there is.
    /// </summary>
    /// <remarks>
    /// Offsets inside the tree count from the start of the resource directory,
    /// with the top bit marking a subdirectory. The data itself is addressed
    /// as it would sit in memory, which is why the section's own address is
    /// needed to find it in the file.
    /// </remarks>
    private static Dictionary<string, string>? FromTheResources(byte[] section, long root, long sectionAddress)
    {
        uint? level = FirstUnder(section, root, VersionType);

        for (int down = 0; down < 2; down++)
        {
            if (level is not uint directory || (directory & IsADirectory) == 0)
            {
                return null;
            }

            level = FirstUnder(section, root + (directory & ~IsADirectory), null);
        }

        if (level is not uint leaf || (leaf & IsADirectory) != 0)
        {
            return null;
        }

        long start = U32(section, root + leaf) - sectionAddress;
        long size = U32(section, root + leaf + 4);

        if (start < 0 || start + size > section.Length)
        {
            return null;
        }

        return FromTheVersion(section.AsSpan((int)start, (int)size).ToArray());
    }

    /// <summary>
    /// Where the first entry of a resource directory points, or the first one
    /// filed under a number when one is asked for.
    /// </summary>
    private static uint? FirstUnder(byte[] section, long directory, uint? wanted)
    {
        int entries = U16(section, directory + 12) + U16(section, directory + 14);

        for (int index = 0; index < entries; index++)
        {
            long entry = directory + 16 + (8 * index);
            uint name = U32(section, entry);

            // An entry with a name of its own has the top bit set, and is
            // never the numbered one being looked for.
            if (wanted is null || ((name & IsADirectory) == 0 && name == wanted))
            {
                return U32(section, entry + 4);
            }
        }

        return null;
    }

    private static Dictionary<string, string>? FromTheVersion(byte[] version)
    {
        Block whole = BlockAt(version, 0);

        if (whole.Key != "VS_VERSION_INFO")
        {
            return null;
        }

        foreach (int child in whole.Children)
        {
            Block info = BlockAt(version, child);

            if (info.Key != "StringFileInfo" || info.Children.Count == 0)
            {
                continue;
            }

            Dictionary<string, string> strings = [];

            foreach (int each in BlockAt(version, info.Children[0]).Children)
            {
                Block one = BlockAt(version, each);
                string text = Encoding.Unicode.GetString(one.Value);
                int ends = text.IndexOf((char)0, StringComparison.Ordinal);

                strings[one.Key] = ends < 0 ? text : text[..ends];
            }

            return strings;
        }

        // A version with no strings in it, which is a version all the same.
        return [];
    }

    /// <summary>One block of a version resource: a key, a value, and blocks inside it.</summary>
    private sealed record Block(string Key, byte[] Value, IReadOnlyList<int> Children);

    /// <summary>
    /// Every block is laid out alike: its length, the length of its value,
    /// whether the value is text, its key ending in a zero, then the value and
    /// the blocks inside it, each starting on a multiple of four.
    /// </summary>
    private static Block BlockAt(byte[] data, int at)
    {
        int length = U16(data, at);
        int valueLength = U16(data, at + 2);
        bool isText = U16(data, at + 4) == 1;
        int end = Math.Min(at + length, data.Length);

        int keyAt = at + 6;
        int keyEnd = keyAt;

        while (keyEnd + 1 < end && (data[keyEnd] != 0 || data[keyEnd + 1] != 0))
        {
            keyEnd += 2;
        }

        string key = Encoding.Unicode.GetString(data, keyAt, keyEnd - keyAt);

        // A text value's length is counted in characters of two bytes, and a
        // binary one's in bytes. Reading the first as the second gives half a
        // name, which is how a reader of this format usually goes wrong.
        int valueAt = Rounded(keyEnd + 2);
        int valueBytes = isText ? valueLength * 2 : valueLength;
        int valueFrom = Math.Min(valueAt, end);
        int valueTo = Math.Max(valueFrom, Math.Min(valueAt + valueBytes, end));

        List<int> children = [];
        int child = Rounded(valueAt + valueBytes);

        while (child + 6 <= end)
        {
            int childLength = U16(data, child);

            if (childLength == 0)
            {
                break;
            }

            children.Add(child);
            child = Rounded(child + childLength);
        }

        return new Block(key, data[valueFrom..valueTo], children);
    }

    /// <summary>Up to the next multiple of four.</summary>
    private static int Rounded(int offset) => (offset + 3) & ~3;

    private static ushort U16(byte[] data, long at) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(checked((int)at), 2));

    private static uint U32(byte[] data, long at) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(checked((int)at), 4));

    /// <summary>As many bytes as were asked for, or as many as there are.</summary>
    private static byte[] UpTo(FileStream file, int bytes)
    {
        byte[] buffer = new byte[(int)Math.Min(bytes, Math.Max(0, file.Length - file.Position))];
        file.ReadExactly(buffer);

        return buffer;
    }
}
