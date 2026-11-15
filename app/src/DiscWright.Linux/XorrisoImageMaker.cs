using DiscWright.Core;

namespace DiscWright.Linux;

/// <summary>
/// Turns a staged disc folder into one image file, with xorriso.
/// </summary>
/// <remarks>
/// <para>
/// The Linux side of <c>WindowsImageMaker</c>, behind the same port. Windows
/// writes UDF through IMAPI. Linux has no IMAPI and xorriso has no UDF, so the
/// image is ISO9660 with Joliet and Rock Ridge. Measured for the Python
/// DiscWright in September 2026: Windows 11 mounts such an image with the same
/// label, reads its autorun.inf, and reads every file back identical,
/// including a 4 GiB part of an installer.
/// </para>
/// <para>
/// Three things are settled before xorriso is started, because each is a
/// sentence somebody can act on and xorriso's own words for them are not: a
/// folder with nothing in it, a name Windows could not read back, and xorriso
/// not being installed.
/// </para>
/// <para>
/// The option to add the older file systems means nothing here. They are what
/// this image is made of, so it is always readable by the oldest Windows, and
/// the 2 GiB ceiling that goes with the option on Windows is IMAPI's and not
/// the format's.
/// </para>
/// </remarks>
public sealed class XorrisoImageMaker(IFileSystem files, IRunPrograms programs) : IMakeImages
{
    /// <summary>The program, by the name a person installs it under.</summary>
    public const string Program = "xorriso";

    /// <summary>
    /// The line to type to install it, for the three families of system.
    /// </summary>
    /// <remarks>
    /// On Arch the package is called libisoburn and xorriso is the program
    /// inside it, which is exactly the kind of thing nobody should have to
    /// find out for themselves.
    /// </remarks>
    public static IReadOnlyList<Figure> HowToGetIt { get; } =
    [
        new("Ubuntu, Debian, Mint", "sudo apt install xorriso"),
        new("Fedora", "sudo dnf install xorriso"),
        new("Arch, Manjaro", "sudo pacman -S libisoburn")
    ];

    /// <summary>How much of what xorriso said last is kept to show when it fails.</summary>
    private const int LinesKept = 12;

    public async Task<Outcome<MadeImage>> Make(
        string stagedFolder,
        string imagePath,
        string label,
        bool wantsLegacyFileSystems,
        int discNumber,
        int ofDiscs,
        IProgress<ImageProgress>? progress,
        CancellationToken ct)
    {
        FolderListing staged = files.List(stagedFolder);

        if (staged.Files.Count == 0)
        {
            return Refusals.ThereIsNothingToPutOnADisc();
        }

        // Before anything is written, and before xorriso is even looked for.
        // xorriso would cut or change these names and say nothing, so this is
        // the only place the disc can be stopped from lying about what it holds.
        IReadOnlyList<NameProblem> names = JolietNames.ProblemsIn(staged.Files.Select(file => file.RelativePath));

        if (names.Count > 0)
        {
            return Refusals.SomeNamesWouldNotReachWindows(names);
        }

        if (!programs.Has(Program))
        {
            return Refusals.TheProgramThatMakesImagesIsNotInstalled(Program, HowToGetIt);
        }

        ImagePlan plan = ImagePlan.For(
            label,
            staged.Bytes,
            staged.Files.Max(file => file.Bytes),
            wantsLegacyFileSystems: false,
            discNumber,
            ofDiscs);

        // Written beside the image under another name and moved over it only
        // once xorriso has finished, so a build that fails leaves the image
        // from last time exactly as it was. A half written image with the
        // right name is one somebody burns.
        string unfinished = imagePath + ".partial";
        List<string> lastSaid = [];

        try
        {
            File.Delete(unfinished);

            int ended = await programs.Run(
                Program,
                TheCommand(stagedFolder, unfinished, plan.VolumeName),
                line =>
                {
                    if (WhatXorrisoSaid.HowFar(line) is double far)
                    {
                        progress?.Report(AsProgress(far));
                        return;
                    }

                    lastSaid.Add(line);

                    if (lastSaid.Count > LinesKept)
                    {
                        lastSaid.RemoveAt(0);
                    }
                },
                ct).ConfigureAwait(false);

            if (ended != 0)
            {
                File.Delete(unfinished);

                return Refusals.TheImageCouldNotBeMade(Program, string.Join(Environment.NewLine, lastSaid));
            }

            File.Move(unfinished, imagePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            File.Delete(unfinished);

            throw;
        }
        catch (Exception wrong) when (wrong is IOException or UnauthorizedAccessException)
        {
            return Refusals.TheImageCouldNotBeMade("This machine", wrong.Message);
        }

        // Said here rather than waited for: a small disc is written before
        // xorriso prints a single percentage.
        progress?.Report(AsProgress(1));

        return new MadeImage(imagePath, new FileInfo(imagePath).Length, plan.Worry);
    }

    /// <summary>
    /// What xorriso is asked, one argument each. Every one was measured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>-input-charset UTF-8</c> is not optional. Without it xorriso takes
    /// the locale's, and under a C locale it wrote "Café" into the names
    /// Windows reads as "Caf__", with no warning.
    /// </para>
    /// <para>
    /// <c>-iso-level 3</c> lets a file over 4 GiB be stored in pieces, which
    /// Windows 11 read back whole. <c>-J -joliet-long</c> writes the names
    /// Windows reads and raises their limit from 64 characters to 103.
    /// <c>-r</c> writes the names and permissions Linux reads.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> TheCommand(string stagedFolder, string imagePath, string volumeName) =>
    [
        "-as", "mkisofs",
        "-input-charset", "UTF-8",
        "-iso-level", "3",
        "-J", "-joliet-long",
        "-r",
        "-V", volumeName,
        "-o", imagePath,
        stagedFolder
    ];

    /// <summary>
    /// The port counts in blocks because IMAPI does. xorriso gives a
    /// percentage, so it is handed over as so many ten thousandths.
    /// </summary>
    private static ImageProgress AsProgress(double far) => new((long)Math.Round(far * 10_000), 10_000);
}
