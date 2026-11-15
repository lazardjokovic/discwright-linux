namespace DiscWright.Core;

/// <summary>
/// The names on a disc that Windows would not read back as they were written,
/// when the disc is ISO9660 with Joliet rather than UDF.
/// </summary>
/// <remarks>
/// <para>
/// Windows DiscWright writes UDF through IMAPI, which holds any name a Windows
/// folder can. A disc made on Linux is written by xorriso, which has no UDF,
/// so Windows reads it through Joliet, and Joliet holds less. xorriso does not
/// refuse a name it cannot hold: <b>it cuts it or changes it, and says
/// nothing</b>. A manual whose name was changed on the way is a Manual button
/// that opens nothing, found by somebody who has already burned the disc.
/// </para>
/// <para>
/// So every name is looked at before anything is written, and the build stops
/// and says which. Every rule here was measured by building an image and
/// mounting it on Windows 11, in the Python DiscWright's xorriso spike, except
/// the device names, which are Windows' own documented rule.
/// </para>
/// <para>
/// It is in Core because it is a judgement and a sentence, and it is asked only
/// by an image maker that writes Joliet. The Windows one never asks.
/// </para>
/// </remarks>
public static class JolietNames
{
    /// <summary>
    /// The longest name that comes back whole. Windows 11 read files of up to
    /// 104 characters and folders of up to 103; one figure for both is simpler
    /// and loses nothing a real disc needs.
    /// </summary>
    public const int Longest = 103;

    /// <summary>
    /// Names Windows keeps for devices, with or without an extension: a file
    /// called CON.txt is a file on Linux and the console on Windows.
    /// </summary>
    private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Everything wrong with the names of these files and of the folders they
    /// sit in, each as a sentence naming the file. Empty when the disc is fine.
    /// </summary>
    /// <remarks>
    /// Paths are relative to the top of the disc, with either slash. A folder
    /// is looked at once however many files are under it, and the answer comes
    /// back in the order of the paths so two runs say the same thing.
    /// </remarks>
    public static IReadOnlyList<NameProblem> ProblemsIn(IEnumerable<string> relativePaths)
    {
        List<NameProblem> problems = [];
        HashSet<string> looked = new(StringComparer.Ordinal);

        // By folder, the spellings already seen there, to catch two names that
        // differ only in capitals. Windows sees one file where there are two.
        Dictionary<string, Dictionary<string, string>> seenIn = new(StringComparer.Ordinal);

        foreach (string path in relativePaths.Order(StringComparer.Ordinal))
        {
            string[] steps = path.Split(['/', (char)92], StringSplitOptions.RemoveEmptyEntries);
            string folder = string.Empty;

            foreach (string name in steps)
            {
                string here = folder.Length == 0 ? name : folder + "/" + name;

                if (looked.Add(here))
                {
                    problems.AddRange(WrongWith(name).Select(why => new NameProblem(here, why)));

                    if (!seenIn.TryGetValue(folder, out Dictionary<string, string>? seen))
                    {
                        seenIn[folder] = seen = new(StringComparer.OrdinalIgnoreCase);
                    }

                    if (seen.TryGetValue(name, out string? other))
                    {
                        problems.Add(new NameProblem(
                            here,
                            $"differs from {other} only in capitals, and Windows sees one file where there are two."));
                    }
                    else
                    {
                        seen[name] = name;
                    }
                }

                folder = here;
            }
        }

        return problems;
    }

    private static IEnumerable<string> WrongWith(string name)
    {
        char[] forbidden =
        [
            .. name.Where(WindowsFileNames.WillNotHave.Contains).Distinct().Order()
        ];

        if (forbidden.Length > 0)
        {
            string shown = string.Join(
                " ", forbidden.Select(each => each < ' ' ? "a control character" : each.ToString()).Distinct());

            yield return $"has {shown} in its name, which Windows does not allow.";
        }

        // Joliet stores sixteen bit characters and nothing wider. An emoji is
        // two of them that only mean something together, and xorriso wrote
        // each half as an underscore.
        if (name.Any(char.IsSurrogate))
        {
            yield return "has a character in its name, such as an emoji, that the disc's Windows names cannot hold.";
        }

        string beforeTheDot = name.Split('.')[0].TrimEnd(' ');

        if (Devices.Contains(beforeTheDot))
        {
            yield return $"Windows keeps the name {beforeTheDot} for a device and cannot open a file called that.";
        }

        if (name.EndsWith('.') || name.EndsWith(' '))
        {
            yield return $"ends with a {(name.EndsWith('.') ? "dot" : "space")}, which Windows drops.";
        }

        if (name.Length > Longest)
        {
            yield return $"the name is {name.Length} characters long, and Windows reads names on this disc up to {Longest}.";
        }
    }
}

/// <summary>One name that would not reach Windows, and why, as a sentence.</summary>
public sealed record NameProblem(string RelativePath, string Why)
{
    public override string ToString() => RelativePath + ": " + Why;
}
