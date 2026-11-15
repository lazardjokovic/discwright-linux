using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.Platform;

namespace DiscWright.Tests;

/// <summary>
/// The real xorriso, a real folder, and the image read back by its records.
/// </summary>
/// <remarks>
/// <para>
/// This is the only proof that the image maker makes an image. Everything in
/// <see cref="MakingAnImageWithXorriso"/> plays xorriso back, which proves
/// what DiscWright makes of its words and nothing about the file it writes.
/// </para>
/// <para>
/// It runs on Linux, where xorriso is what the app uses, and there a missing
/// xorriso is a failure and not a reason to pass: a test that quietly does
/// nothing reads exactly like one that checked something. On Windows there is
/// no xorriso to run and the Windows image maker has tests of its own, so
/// these say so and stop.
/// </para>
/// </remarks>
public sealed class ARealImageFromXorriso : IDisposable
{
    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-real-image-" + Guid.NewGuid().ToString("N"));

    public ARealImageFromXorriso() => Directory.CreateDirectory(Stage);

    private string Stage => Path.Combine(_room, "disc");

    private string Image => Path.Combine(_room, "HOLLOW KNIGHT.iso");

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [Fact]
    public async Task Every_name_reaches_windows_as_it_was_written_and_every_byte_with_it()
    {
        if (!ThereIsAnXorrisoToRun())
        {
            return;
        }

        // The longest name that comes back whole, an accent, another script,
        // and a space, which between them are the names that went wrong while
        // the flags were being measured.
        string longest = new string('n', 99) + ".txt";
        byte[] installer = SomeBytes(300_000);

        Put("autorun.inf", "[autorun]\r\nlabel=Hollow Knight\r\n"u8.ToArray());
        Put("AUTORUN/menu.hta", "<html></html>"u8.ToArray());
        Put("setup_hollow_knight.exe", installer);
        Put("Café/ゲーム.txt", "accents and another script"u8.ToArray());
        Put("Extras/Hollow Knight - Manual (EN).pdf", "a manual"u8.ToArray());
        Put("Extras/" + longest, "the longest name"u8.ToArray());

        Outcome<MadeImage> made = await Make("Hollow Knight");

        MadeImage image = Assert.IsType<Outcome<MadeImage>.Ok>(made).Value;
        IReadOnlyList<TheJolietRecords.Recorded> onIt = TheJolietRecords.FilesIn(image.Path);

        Assert.Equal(
            new[]
            {
                "AUTORUN/menu.hta",
                "Café/ゲーム.txt",
                "Extras/Hollow Knight - Manual (EN).pdf",
                "Extras/" + longest,
                "autorun.inf",
                "setup_hollow_knight.exe"
            }.Order(StringComparer.Ordinal),
            onIt.Select(file => file.Path).Order(StringComparer.Ordinal));

        Assert.Equal("HOLLOW_KNIGHT", TheJolietRecords.VolumeNameOf(image.Path));

        // Not only the name. The bytes at the place the record points to.
        TheJolietRecords.Recorded recorded = onIt.Single(file => file.Path == "setup_hollow_knight.exe");
        Assert.Equal(installer.Length, recorded.Bytes);
        Assert.Equal(installer, TheJolietRecords.BytesOf(image.Path, recorded));

        Assert.Equal(new FileInfo(image.Path).Length, image.Bytes);
        Assert.False(File.Exists(image.Path + ".partial"));
    }

    [Fact]
    public async Task A_disc_of_a_set_carries_its_number_in_the_volume_name()
    {
        if (!ThereIsAnXorrisoToRun())
        {
            return;
        }

        Put("autorun.inf", "[autorun]"u8.ToArray());

        Outcome<MadeImage> made = await new XorrisoImageMaker(new WindowsFileSystem(), new RunningPrograms())
            .Make(Stage, Image, "The Witcher - Enhanced Edition", false, 2, 3, null, default);

        Assert.Equal(
            "THE_WITCHER_E_D2",
            TheJolietRecords.VolumeNameOf(Assert.IsType<Outcome<MadeImage>.Ok>(made).Value.Path));
    }

    [Fact]
    public async Task A_name_that_would_be_cut_stops_it_before_any_image_exists()
    {
        if (!ThereIsAnXorrisoToRun())
        {
            return;
        }

        Put("Extras/" + new string('n', 100) + ".txt", "one character too many"u8.ToArray());

        Outcome<MadeImage> made = await Make("Hollow Knight");

        Assert.Equal(
            "Some names on the disc would not reach Windows",
            Assert.IsType<Outcome<MadeImage>.No>(made).Refusal.Headline);
        Assert.False(File.Exists(Image));
    }

    [Fact]
    public async Task Says_so_in_xorrisos_words_when_the_image_has_nowhere_to_go()
    {
        if (!ThereIsAnXorrisoToRun())
        {
            return;
        }

        Put("autorun.inf", "[autorun]"u8.ToArray());

        // A folder that is not there. xorriso finds that out, not DiscWright.
        string nowhere = Path.Combine(_room, "not a folder", "x.iso");

        Outcome<MadeImage> made = await new XorrisoImageMaker(new WindowsFileSystem(), new RunningPrograms())
            .Make(Stage, nowhere, "Hollow Knight", false, 1, 1, null, default);

        Refusal refusal = Assert.IsType<Outcome<MadeImage>.No>(made).Refusal;

        Assert.Equal("xorriso could not make the image", refusal.Headline);
        Assert.Contains("FAILURE", refusal.Because, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hears_both_of_a_programs_outputs_and_the_number_it_ended_with()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        List<string> heard = [];

        int ended = await new RunningPrograms().Run(
            "sh", ["-c", "echo on the one; echo on the other 1>&2; exit 3"], heard.Add, default);

        Assert.Equal(3, ended);
        Assert.Equal(["on the one", "on the other"], heard.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Stopping_stops_the_program_and_not_only_the_waiting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using CancellationTokenSource stop = new(TimeSpan.FromMilliseconds(300));
        System.Diagnostics.Stopwatch took = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RunningPrograms().Run("sleep", ["30"], _ => { }, stop.Token));

        Assert.True(took.Elapsed < TimeSpan.FromSeconds(10), $"It took {took.Elapsed} to stop.");
    }

    [Fact]
    public void Knows_a_program_that_is_not_installed_from_one_that_is()
    {
        Assert.False(new RunningPrograms().Has("a-program-nobody-has-ever-installed"));

        if (OperatingSystem.IsLinux())
        {
            Assert.True(new RunningPrograms().Has("sh"));
        }
    }

    /// <summary>
    /// False on Windows, where there is nothing to run. On Linux a missing
    /// xorriso fails the test rather than excusing it.
    /// </summary>
    private static bool ThereIsAnXorrisoToRun()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        Assert.True(
            new RunningPrograms().Has(XorrisoImageMaker.Program),
            "xorriso is not installed, and these tests are the only proof that DiscWright makes an image on Linux. "
            + "sudo apt install xorriso");

        return true;
    }

    private Task<Outcome<MadeImage>> Make(string label) =>
        new XorrisoImageMaker(new WindowsFileSystem(), new RunningPrograms())
            .Make(Stage, Image, label, false, 1, 1, null, default);

    private void Put(string relative, byte[] holding)
    {
        string full = Path.Combine([Stage, .. relative.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, holding);
    }

    /// <summary>Bytes that are the same every run and are not all one value.</summary>
    private static byte[] SomeBytes(int howMany)
    {
        byte[] bytes = new byte[howMany];
        new Random(20261115).NextBytes(bytes);

        return bytes;
    }
}
