using System.Security.Cryptography;
using System.Text;

using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.Platform;
using DiscWright.Staging;

using SkiaSharp;

namespace DiscWright.Tests;

/// <summary>
/// A folder somebody downloaded, all the way to a disc folder and an image,
/// through every part a Linux DiscWright is made of, on a real disk.
/// </summary>
/// <remarks>
/// <para>
/// Each part has tests of its own. This is the only one where they meet: the
/// installer's name read by <see cref="VersionResourceFacts"/>, the folder
/// recognised, the disc planned and staged by the shared builder with
/// <see cref="SkiaImages"/> for its pictures, every file hashed, and the image
/// made by <see cref="XorrisoImageMaker"/> and read back by its records.
/// </para>
/// <para>
/// Everything up to the image runs on Windows as well, since none of it is a
/// Linux call. Run there, it is the same code building the same disc, which is
/// the nearest thing there is to the two systems being held side by side.
/// </para>
/// </remarks>
public sealed class AWholeDiscWithoutWindows : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";
    private const string ItsPart = "setup_fixture_game_1.2.3_(64bit)_(12345)-1.bin";

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-whole-disc-" + Guid.NewGuid().ToString("N"));

    public AWholeDiscWithoutWindows()
    {
        Directory.CreateDirectory(Download);
        Directory.CreateDirectory(Out);

        // A real Windows executable with a version resource in it, standing in
        // for the installer, and a part beside it the way GOG ships them.
        File.Copy(Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"), InDownload(Installer));

        byte[] part = new byte[50_000];
        new Random(20261115).NextBytes(part);
        File.WriteAllBytes(InDownload(ItsPart), part);

        Paint(Icon, 512, 512, new SKColor(40, 160, 150));
        Paint(Background, 1920, 1080, new SKColor(20, 30, 60));
    }

    private string Download => Path.Combine(_room, "downloads", "Fixture Game");

    private string Out => Path.Combine(_room, "out");

    private string Icon => Path.Combine(_room, "icon.png");

    private string Background => Path.Combine(_room, "background.png");

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [Fact]
    public void The_game_is_named_by_its_installer_and_not_by_its_file()
    {
        GameFolder game = TheGame();

        Assert.Equal(GameKind.GogInstaller, game.Kind);
        // The name inside the executable, with Inno's padding trimmed off.
        // Worked out from the file's name instead it would be "Fixture Game",
        // which is close enough to pass a careless test and is what a disc
        // made on Linux would have been called without the reader.
        Assert.Equal("Fixture Game: Directors Cut", game.Name);
    }

    [Fact]
    public async Task The_disc_folder_holds_everything_a_disc_is_in_real_folders()
    {
        BuiltDisc disc = await TheDisc();

        string[] onIt = EverythingOn(disc);

        // No name with a backslash in it anywhere. That is what a disc staged
        // on Linux held before a place on the disc was taken apart: one file
        // called AUTORUN\menu.hta and no AUTORUN folder.
        Assert.DoesNotContain(onIt, name => name.Contains((char)92));

        Assert.Contains("AUTORUN/menu.hta", onIt);
        Assert.Contains("AUTORUN/menu.js", onIt);
        Assert.Contains("AUTORUN/background.png", onIt);
        Assert.Contains("Start Here.hta", onIt);
        Assert.Contains("autorun.inf", onIt);
        Assert.Contains("checksums.sha256", onIt);
        Assert.Contains(Installer, onIt);
        Assert.Contains(ItsPart, onIt);
    }

    [Fact]
    public async Task The_installer_and_its_part_arrive_byte_for_byte()
    {
        BuiltDisc disc = await TheDisc();

        Assert.Equal(File.ReadAllBytes(InDownload(Installer)), File.ReadAllBytes(On(disc, Installer)));
        Assert.Equal(File.ReadAllBytes(InDownload(ItsPart)), File.ReadAllBytes(On(disc, ItsPart)));
    }

    [Fact]
    public async Task Autorun_is_written_the_way_windows_reads_it_and_names_an_icon_that_is_there()
    {
        BuiltDisc disc = await TheDisc();

        byte[] bytes = File.ReadAllBytes(On(disc, "autorun.inf"));
        string text = Encoding.Latin1.GetString(bytes);

        // CRLF, ending in one, and no byte order mark: AutoRun has no Unicode
        // mode, and reads a mark as part of the first line.
        Assert.Equal((byte)'[', bytes[0]);
        Assert.EndsWith("\r\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("label=FIXTURE GAME", text, StringComparison.Ordinal);

        string icon = text.Split("\r\n").Single(line => line.StartsWith("icon=", StringComparison.Ordinal))["icon=".Length..];

        Assert.True(File.Exists(On(disc, icon)), $"autorun.inf names {icon}, and it is not on the disc.");
        Assert.Equal(7, BitConverter.ToInt16(File.ReadAllBytes(On(disc, icon)), 4));
    }

    [Fact]
    public async Task The_background_is_the_size_of_the_menu()
    {
        BuiltDisc disc = await TheDisc();

        using SKCodec made = SKCodec.Create(On(disc, "AUTORUN/background.png"));

        Assert.Equal(760, made.Info.Width);
        Assert.Equal(480, made.Info.Height);
    }

    [Fact]
    public async Task The_checksum_list_is_true_of_every_file_it_names()
    {
        BuiltDisc disc = await TheDisc();

        // Checked the way somebody would by hand, with nothing of ours: a
        // line is a hash, a star and a path, and the file at that path has to
        // have that hash.
        (string Hash, string Path)[] listed =
        [
            .. File.ReadAllLines(On(disc, "checksums.sha256"))
                .Where(line => line.Length > 66 && !line.StartsWith('#') && line[64] == ' ' && line[65] == '*')
                .Select(line => (line[..64], line[66..]))
        ];

        Assert.Contains(listed, one => one.Path == Installer);
        Assert.Contains(listed, one => one.Path == "AUTORUN/menu.hta");

        foreach ((string hash, string path) in listed)
        {
            Assert.True(File.Exists(On(disc, path)), $"The list names {path}, and it is not on the disc.");
            Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(On(disc, path)))));
        }

        // And nothing on the disc is missing from it, bar the list itself.
        Assert.Equal(
            EverythingOn(disc).Where(name => name != "checksums.sha256").Order(StringComparer.Ordinal),
            listed.Select(one => one.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_image_holds_exactly_what_the_disc_folder_holds()
    {
        BuiltDisc disc = await TheDisc();

        if (!OperatingSystem.IsLinux())
        {
            // The image is xorriso's on Linux and IMAPI's on Windows, and the
            // Windows one has tests of its own.
            return;
        }

        Outcome<MadeImage> made = await new XorrisoImageMaker(new WindowsFileSystem(), new RunningPrograms())
            .Make(disc.Folder, Path.Combine(Out, "FIXTURE GAME.iso"), disc.Label, false, disc.Number, disc.OfDiscs, null, default);

        MadeImage image = Assert.IsType<Outcome<MadeImage>.Ok>(made).Value;

        IReadOnlyList<TheJolietRecords.Recorded> recorded = TheJolietRecords.FilesIn(image.Path);

        Assert.Equal(
            EverythingOn(disc).Order(StringComparer.Ordinal),
            recorded.Select(file => file.Path).Order(StringComparer.Ordinal));

        // Every byte of every file, read from where its record says it is.
        foreach (TheJolietRecords.Recorded file in recorded)
        {
            Assert.Equal(File.ReadAllBytes(On(disc, file.Path)), TheJolietRecords.BytesOf(image.Path, file));
        }

        Assert.Equal("FIXTURE_GAME", TheJolietRecords.VolumeNameOf(image.Path));
    }

    private GameFolder TheGame() =>
        Assert.IsType<Outcome<GameFolder>.Ok>(
            GameFolders.InTheUsualOrder(new WindowsFileSystem(), new VersionResourceFacts()).At(Download)).Value;

    private async Task<BuiltDisc> TheDisc()
    {
        DiscProject project = new("FIXTURE GAME", [TheGame()], new DiscOptions())
        {
            Artwork = new DiscArtwork(IconPath: Icon, BackgroundPath: Background)
        };

        Outcome<BuiltDiscs> built = await new DiscBuilder(
                new WindowsFileSystem(), new WindowsStageWriter(), new SkiaImages(), new Sha256Hasher())
            .Build(project, new Medium("TEST", "a test disc", 10_000_000), Out, null, CancellationToken.None);

        return Assert.Single(Assert.IsType<Outcome<BuiltDiscs>.Ok>(built).Value.Discs);
    }

    /// <summary>Every file on the disc, as a path from its top with forward slashes.</summary>
    private static string[] EverythingOn(BuiltDisc disc) =>
    [
        .. Directory
            .EnumerateFiles(disc.Folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(disc.Folder, file).Replace(Path.DirectorySeparatorChar, '/'))
    ];

    private static string On(BuiltDisc disc, string relative) => Path.Combine([disc.Folder, .. relative.Split('/')]);

    private string InDownload(string name) => Path.Combine(Download, name);

    private static void Paint(string path, int width, int height, SKColor colour)
    {
        using SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(colour);
        }

        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());
    }
}
