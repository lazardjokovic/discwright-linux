using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.Platform;

namespace DiscWright.Tests;

/// <summary>
/// Everything the Linux image maker decides, with xorriso played back.
/// </summary>
/// <remarks>
/// The lines the stand-in says are the lines xorriso 1.5.6 really printed on
/// Ubuntu on 2026-11-15, for a build that worked and for one that did not. So
/// what is tested is what DiscWright makes of xorriso's real words, without
/// xorriso, on either system. <see cref="ARealImageFromXorriso"/> is the half
/// that runs the real one.
/// </remarks>
public sealed class MakingAnImageWithXorriso : IDisposable
{
    private static readonly string[] ItWorked =
    [
        "xorriso 1.5.6 : RockRidge filesystem manipulator, libburnia project.",
        "",
        "xorriso : NOTE : Local character set is now assumed as: 'UTF-8'",
        "Drive current: -outdev 'stdio:/tmp/x.iso'",
        "Media current: stdio file, overwriteable",
        "Media status : is blank",
        "Media summary: 0 sessions, 0 data blocks, 0 data, 3712m free",
        "Added to ISO image: directory '/'='/tmp/x'",
        "xorriso : UPDATE :       2 files added in 1 seconds",
        "ISO image produced: 184 sectors",
        "Written to medium : 184 sectors at LBA 0",
        "Writing to 'stdio:/tmp/x.iso' completed successfully."
    ];

    private static readonly string[] ItFailed =
    [
        "xorriso 1.5.6 : RockRidge filesystem manipulator, libburnia project.",
        "",
        "Drive current: -outdev 'stdio:/tmp/y.iso'",
        "Media current: stdio file, overwriteable",
        "Media status : is blank",
        "Media summary: 0 sessions, 0 data blocks, 0 data, 3711m free",
        "xorriso : FAILURE : Cannot determine attributes of source file '/nonexistent-folder' : No such file or directory",
        "xorriso : aborting : -abort_on 'FAILURE' encountered 'FAILURE'"
    ];

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-xorriso-" + Guid.NewGuid().ToString("N"));

    public MakingAnImageWithXorriso()
    {
        Directory.CreateDirectory(Stage);
        File.WriteAllText(Path.Combine(Stage, "autorun.inf"), "[autorun]");
        Directory.CreateDirectory(Path.Combine(Stage, "AUTORUN"));
        File.WriteAllText(Path.Combine(Stage, "AUTORUN", "menu.hta"), "<html></html>");
    }

    private string Stage => Path.Combine(_room, "disc");

    private string Image => Path.Combine(_room, "HOLLOW KNIGHT.iso");

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [Fact]
    public async Task Asks_xorriso_for_exactly_what_was_measured()
    {
        XorrisoPlayedBack xorriso = new(0, ItWorked);

        await Make(xorriso);

        // Every argument here was measured against Windows 11, and
        // -input-charset is the one that looks optional and is not.
        Assert.Equal(
            [
                "-as", "mkisofs", "-input-charset", "UTF-8", "-iso-level", "3", "-J", "-joliet-long", "-r",
                "-V", "HOLLOW_KNIGHT", "-o", Image + ".partial", Stage
            ],
            Assert.Single(xorriso.Runs));
        Assert.Equal("xorriso", xorriso.Started.Single());
    }

    [Fact]
    public async Task Numbers_the_volume_name_of_a_disc_in_a_set()
    {
        XorrisoPlayedBack xorriso = new(0, ItWorked);

        await new XorrisoImageMaker(new WindowsFileSystem(), xorriso)
            .Make(Stage, Image, "Hollow Knight", false, 2, 3, null, default);

        Assert.Contains("HOLLOW_KNIGHT_D2", Assert.Single(xorriso.Runs));
    }

    [Fact]
    public async Task Hands_back_the_image_under_its_own_name_and_leaves_nothing_half_written()
    {
        Outcome<MadeImage> made = await Make(new XorrisoPlayedBack(0, ItWorked));

        MadeImage image = Assert.IsType<Outcome<MadeImage>.Ok>(made).Value;

        Assert.Equal(Image, image.Path);
        Assert.Equal(new FileInfo(Image).Length, image.Bytes);
        Assert.Null(image.Worry);
        Assert.False(File.Exists(Image + ".partial"));
    }

    [Fact]
    public async Task Replaces_the_image_from_last_time_only_once_the_new_one_is_whole()
    {
        File.WriteAllText(Image, "the image from last time");

        await Make(new XorrisoPlayedBack(0, ItWorked));

        Assert.Equal(XorrisoPlayedBack.WhatItWrites, File.ReadAllText(Image));
    }

    [Fact]
    public async Task Leaves_the_image_from_last_time_alone_when_xorriso_fails()
    {
        File.WriteAllText(Image, "the image from last time");

        Outcome<MadeImage> made = await Make(new XorrisoPlayedBack(5, ItFailed));

        // A half written image with the right name is one somebody burns.
        Assert.IsType<Outcome<MadeImage>.No>(made);
        Assert.Equal("the image from last time", File.ReadAllText(Image));
        Assert.False(File.Exists(Image + ".partial"));
    }

    [Fact]
    public async Task Says_what_xorriso_said_when_it_fails()
    {
        Outcome<MadeImage> made = await Make(new XorrisoPlayedBack(5, ItFailed));

        Refusal refusal = Assert.IsType<Outcome<MadeImage>.No>(made).Refusal;

        Assert.Equal("xorriso could not make the image", refusal.Headline);
        Assert.Contains("Cannot determine attributes of source file", refusal.Because, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_only_the_end_of_what_xorriso_said()
    {
        string[] aLot = [.. Enumerable.Range(1, 40).Select(number => $"line {number}")];

        Outcome<MadeImage> made = await Make(new XorrisoPlayedBack(5, aLot));

        Refusal refusal = Assert.IsType<Outcome<MadeImage>.No>(made).Refusal;

        // The reason is at the end. Forty lines of it is a wall, and the first
        // twenty eight are xorriso saying hello.
        Assert.DoesNotContain("line 28" + Environment.NewLine, refusal.Because, StringComparison.Ordinal);
        Assert.Contains("line 29", refusal.Because, StringComparison.Ordinal);
        Assert.Contains("line 40", refusal.Because, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Says_how_far_along_it_is_and_ends_on_all_of_it()
    {
        Heard heard = new();

        await new XorrisoImageMaker(
                new WindowsFileSystem(),
                new XorrisoPlayedBack(
                    0,
                    "xorriso : UPDATE :  35.19% done",
                    "xorriso : UPDATE :  71.50% done, estimate finish Sun Nov 15 21:30:00 2026",
                    "Writing to 'stdio:/tmp/x.iso' completed successfully."))
            .Make(Stage, Image, "Hollow Knight", false, 1, 1, heard, default);

        Assert.Equal([0.3519, 0.715, 1.0], heard.Said.Select(each => each.HowFar));
    }

    [Fact]
    public async Task Ends_on_all_of_it_even_when_xorriso_never_gave_a_percentage()
    {
        Heard heard = new();

        // A small disc: xorriso counts the files it added and then it is done.
        await new XorrisoImageMaker(new WindowsFileSystem(), new XorrisoPlayedBack(0, ItWorked))
            .Make(Stage, Image, "Hollow Knight", false, 1, 1, heard, default);

        Assert.Equal([1.0], heard.Said.Select(each => each.HowFar));
    }

    [Fact]
    public async Task Refuses_a_name_windows_could_not_read_before_xorriso_is_started()
    {
        Directory.CreateDirectory(Path.Combine(Stage, "Extras"));
        File.WriteAllText(Path.Combine(Stage, "Extras", new string('a', 100) + ".txt"), "too long a name");

        XorrisoPlayedBack xorriso = new(0, ItWorked);
        Outcome<MadeImage> made = await Make(xorriso);

        Refusal refusal = Assert.IsType<Outcome<MadeImage>.No>(made).Refusal;

        Assert.Equal("Some names on the disc would not reach Windows", refusal.Headline);
        Assert.Empty(xorriso.Runs);
        Assert.False(File.Exists(Image));
    }

    [Fact]
    public async Task Says_xorriso_is_not_installed_and_how_to_get_it()
    {
        XorrisoPlayedBack xorriso = new(0, ItWorked) { Installed = false };

        Outcome<MadeImage> made = await Make(xorriso);

        Refusal refusal = Assert.IsType<Outcome<MadeImage>.No>(made).Refusal;

        Assert.Equal("xorriso is not installed", refusal.Headline);
        Assert.Contains(refusal.Figures, figure => figure.Value == "sudo apt install xorriso");
        Assert.Empty(xorriso.Runs);
    }

    [Fact]
    public async Task Refuses_a_folder_with_nothing_in_it()
    {
        Directory.Delete(Stage, recursive: true);
        Directory.CreateDirectory(Stage);

        XorrisoPlayedBack xorriso = new(0, ItWorked);
        Outcome<MadeImage> made = await Make(xorriso);

        Assert.Equal(
            "There is nothing to put on a disc",
            Assert.IsType<Outcome<MadeImage>.No>(made).Refusal.Headline);
        Assert.Empty(xorriso.Runs);
    }

    [Fact]
    public async Task Leaves_nothing_half_written_when_it_is_stopped()
    {
        using CancellationTokenSource stop = new();
        XorrisoPlayedBack xorriso = new(0, ItWorked) { WhileItRuns = stop.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new XorrisoImageMaker(new WindowsFileSystem(), xorriso)
                .Make(Stage, Image, "Hollow Knight", false, 1, 1, null, stop.Token));

        Assert.False(File.Exists(Image + ".partial"));
        Assert.False(File.Exists(Image));
    }

    [Theory]
    [InlineData("xorriso : UPDATE :  35.19% done", 0.3519)]
    [InlineData("xorriso : UPDATE :  71.50% done, estimate finish Sun Nov 15 21:30:00 2026", 0.715)]
    [InlineData("xorriso : UPDATE : 100.00% done", 1.0)]
    [InlineData("xorriso : UPDATE :   0.00% done", 0.0)]
    [InlineData("xorriso : UPDATE : 7% done", 0.07)]
    public void Reads_how_far_from_the_line_xorriso_prints(string line, double far)
    {
        Assert.Equal(far, WhatXorrisoSaid.HowFar(line)!.Value, precision: 6);
    }

    [Theory]
    [InlineData("xorriso : UPDATE :       2 files added in 1 seconds")]
    [InlineData("ISO image produced: 184 sectors")]
    [InlineData("xorriso : FAILURE : Cannot determine attributes of source file")]
    [InlineData("")]
    [InlineData("50% done")]
    public void Takes_no_other_line_for_a_percentage(string line)
    {
        Assert.Null(WhatXorrisoSaid.HowFar(line));
    }

    [Fact]
    public void Reads_the_percentage_the_same_on_a_machine_that_writes_a_comma()
    {
        System.Globalization.CultureInfo before = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            // xorriso prints a full stop whatever the language. Read in the
            // machine's culture, 35.19 becomes three thousand and some.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("sr-Latn-RS");

            Assert.Equal(0.3519, WhatXorrisoSaid.HowFar("xorriso : UPDATE :  35.19% done")!.Value, precision: 6);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = before;
        }
    }

    private Task<Outcome<MadeImage>> Make(XorrisoPlayedBack xorriso) =>
        new XorrisoImageMaker(new WindowsFileSystem(), xorriso)
            .Make(Stage, Image, "Hollow Knight", false, 1, 1, null, default);

    /// <summary>Hears every report on the thread that made it, in order.</summary>
    private sealed class Heard : IProgress<ImageProgress>
    {
        public List<ImageProgress> Said { get; } = [];

        public void Report(ImageProgress value) => Said.Add(value);
    }

    /// <summary>
    /// Stands in for xorriso: writes a file where it was told to, says its
    /// lines, and ends with the number it was given.
    /// </summary>
    private sealed class XorrisoPlayedBack(int endsWith, params string[] says) : IRunPrograms
    {
        public const string WhatItWrites = "an image, as far as this test is concerned";

        public bool Installed { get; init; } = true;

        public Action? WhileItRuns { get; init; }

        public List<string> Started { get; } = [];

        public List<IReadOnlyList<string>> Runs { get; } = [];

        public bool Has(string program) => Installed;

        public Task<int> Run(
            string program, IReadOnlyList<string> arguments, Action<string> said, CancellationToken ct)
        {
            Started.Add(program);
            Runs.Add(arguments);

            // xorriso makes its file the moment it starts, before it knows
            // whether it will finish, which is the whole reason for .partial.
            File.WriteAllText(arguments[arguments.ToList().IndexOf("-o") + 1], WhatItWrites);

            foreach (string line in says)
            {
                said(line);
            }

            WhileItRuns?.Invoke();
            ct.ThrowIfCancellationRequested();

            return Task.FromResult(endsWith);
        }
    }
}
