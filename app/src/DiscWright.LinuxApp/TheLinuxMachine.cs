using System.Reflection;

using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.Platform;
using DiscWright.Staging;
using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// Everything the screen needs from the machine it is running on, as a Linux
/// machine has it.
/// </summary>
/// <remarks>
/// <para>
/// The twin of what <c>App.xaml.cs</c> builds in the Windows app, port for
/// port. Where the Windows answer is plain .NET it is the same class here,
/// compiled for Linux through <c>DiscWright.Machine</c>: the disk, the stage
/// writer, the hasher, the note beside a set, what GitHub has released.
/// Where the Windows answer is Windows, the Linux one is in
/// <c>DiscWright.Linux</c>: the image over xorriso, the pictures over Skia,
/// an installer's name out of its own bytes.
/// </para>
/// <para>
/// Built in one place and handed over whole, with nothing choosing between
/// machines at run time. Each app has its own composition root and builds the
/// one machine it was compiled for.
/// </para>
/// </remarks>
public static class TheLinuxMachine
{
    public static TheMachine Composed(IKeepALog log)
    {
        // Logged for the day somebody's disc goes wrong and the first
        // question is which build made it.
        log.Write($"DiscWright {WhatThisBuildIs()} for Linux started.");

        WindowsFileSystem disk = new();
        SkiaImages pictures = new();
        RunningPrograms programs = new();

        // Until there is a recorder over xorriso. See the class.
        NothingBurnsYet recorder = new();

        // The version goes in, because every disc it builds writes a project
        // file that says which build made it.
        DiscBuilder builder = new(disk, new WindowsStageWriter(), pictures, new Sha256Hasher(), WhatThisBuildIs());

        return new TheMachine(
            // What a label will look like once AutoRun has read it. The
            // machine's own codepage, as on Windows: somebody whose Linux is
            // in Polish most likely plays on a Windows that is too.
            new WindowsAutorunEncoding(),
            builder,
            new XorrisoImageMaker(disk, programs),
            recorder,
            recorder,
            recorder,
            disk,
            new Sha256Hasher(),
            new ANoteOnDisk(),
            recorder,
            new WhatGitHubHasReleased(),
            new APreviewOfTheMenu(new WindowsStageWriter(), pictures),
            pictures,
            WhatThisBuildIs(),
            HowBigIs(disk),
            WhereADiscWouldGo());
    }

    /// <summary>The reader of folders, with an installer's name read the Linux way.</summary>
    public static GameFolderReader TheReaderOfFolders() =>
        GameFolderReader.InTheUsualOrder(new VersionResourceFacts());

    /// <summary>
    /// What this build calls itself.
    /// </summary>
    /// <remarks>
    /// The informational version of the app's own assembly, which carries the
    /// commit after a plus, cut back to the part that is a version. Read from
    /// the assembly rather than written down twice. The app's assembly and
    /// not whichever one started the process, which under a test is the test.
    /// </remarks>
    public static string WhatThisBuildIs()
    {
        Assembly app = typeof(TheLinuxMachine).Assembly;

        string? said = app.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? app.GetName().Version?.ToString();

        return said is null ? string.Empty : WithoutTheCommit(said);
    }

    /// <summary>
    /// A version as a person reads one: what the build stamped, up to the
    /// plus the commit is added after.
    /// </summary>
    /// <remarks>
    /// On its own so it can be asked directly. Whether a build carries a
    /// commit at all depends on where it was built: one made from a folder
    /// that is not a git checkout has none, and a test that could only ask
    /// the assembly would pass there whatever this did.
    /// </remarks>
    public static string WithoutTheCommit(string stamped)
    {
        int plus = stamped.IndexOf('+', StringComparison.Ordinal);

        return plus >= 0 ? stamped[..plus] : stamped;
    }

    /// <summary>
    /// The folders to suggest a place to build in, as this machine has them.
    /// </summary>
    /// <remarks>
    /// No synced folders are named. On Windows the Desktop is often one
    /// OneDrive watches, and a disc built there is uploaded as it is built.
    /// Nothing on a Linux desktop says where a sync client watches in a way
    /// that can be asked, so none is assumed.
    /// </remarks>
    public static WhereDiscsWouldGo WhereADiscWouldGo() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            []);

    /// <summary>
    /// How big a thing is, answered once per path and then remembered.
    /// </summary>
    /// <remarks>
    /// The screen asks this again every time anything changes, which on a
    /// folder of extras means walking it on every keystroke. The sizes it asks
    /// about are of things somebody chose, so they do not move while the
    /// window is open. Remembered by the path exactly as written: here two
    /// folders that differ only in their capitals are two folders.
    /// </remarks>
    public static Func<string, long> HowBigIs(IFileSystem disk)
    {
        Dictionary<string, long> known = new(StringComparer.Ordinal);

        return path =>
        {
            if (known.TryGetValue(path, out long remembered))
            {
                return remembered;
            }

            long bytes = 0;

            try
            {
                bytes = disk.FolderExists(path)
                    ? disk.List(path).Files.Sum(file => file.Bytes)
                    : new FileInfo(path).Length;
            }
            catch (IOException)
            {
                // A thing that has gone away since it was chosen counts as
                // nothing. The build will say so properly; a window must not
                // fall over while somebody is still typing.
            }
            catch (UnauthorizedAccessException)
            {
            }

            known[path] = bytes;
            return bytes;
        };
    }
}
