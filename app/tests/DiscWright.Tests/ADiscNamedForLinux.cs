using System.Buffers.Binary;
using System.Text;

using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.Platform;
using DiscWright.Staging;

using SkiaSharp;

namespace DiscWright.Tests;

/// <summary>
/// The disc's name and icon for a Linux desktop: <c>.xdg-volume-info</c> at
/// the top, and the icon as a PNG beside the Windows one.
/// </summary>
/// <remarks>
/// <para>
/// Written in the machine's own paths and over real folders, so it runs the
/// same on Windows and on Linux. What it cannot say is what a desktop does
/// with the files: that is looked at, on a real one.
/// </para>
/// <para>
/// The PNG is read back with Skia, which is nothing of ours. A test that read
/// it with the code that wrote it would agree with itself about anything.
/// </para>
/// </remarks>
public sealed class ADiscNamedForLinux : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-linux-name-" + Guid.NewGuid().ToString("N"));

    public ADiscNamedForLinux()
    {
        Directory.CreateDirectory(Download);
        Directory.CreateDirectory(Out);

        File.Copy(Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"), Path.Combine(Download, Installer));

        PaintInHalves(Icon, 512);
    }

    private string Download => Path.Combine(_room, "downloads", "Fixture Game");

    private string Out => Path.Combine(_room, "out");

    private string Icon => Path.Combine(_room, "icon.png");

    public void Dispose() => Directory.Delete(_room, recursive: true);

    // ------------------------------------------------------------ the name file

    [Fact]
    public void The_name_file_is_byte_for_byte_what_the_PowerShell_app_wrote()
    {
        // Copied off a disc DiscWright 0.7.2 burned, where it was 51 bytes:
        // tests/fixtures/windows-0.7.2/alanwake/.xdg-volume-info in the
        // Python port.
        byte[] offARealDisc = Encoding.ASCII.GetBytes("[Volume Info]\nName=ALAN WAKE\nIconFile=ALANWAKE.png\n");

        Assert.Equal(51, offARealDisc.Length);
        Assert.Equal(offARealDisc, XdgVolumeInfo.For("ALAN WAKE", "ALANWAKE.png"));
    }

    [Fact]
    public void The_name_file_has_no_mark_in_front_and_no_carriage_returns()
    {
        byte[] bytes = XdgVolumeInfo.For("Wiedźmin", "W.png");

        // A byte order mark is read as part of the first group's name, and
        // then the file holds no group called Volume Info and says nothing.
        Assert.Equal((byte)'[', bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);

        // The whole name, in UTF-8, which is the point of the file: the
        // volume id beside it cannot hold a z with an acute.
        Assert.Contains("Name=Wiedźmin\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void A_backslash_in_a_name_is_doubled_and_a_line_break_is_dropped()
    {
        Assert.Contains("Name=A\\\\B\n", Text(XdgVolumeInfo.For("A\\B", null)), StringComparison.Ordinal);

        // A name with a line break in it would otherwise be two lines, and
        // the second one a key of somebody else's choosing.
        string[] lines = Text(XdgVolumeInfo.For("Game\nIconFile=evil.png", null)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["[Volume Info]", "Name=GameIconFile=evil.png"], lines);
    }

    [Fact]
    public void A_disc_with_no_picture_names_none()
    {
        Assert.Equal("[Volume Info]\nName=PLAIN\n", Text(XdgVolumeInfo.For("PLAIN", null)));
        Assert.Equal("[Volume Info]\nName=PLAIN\n", Text(XdgVolumeInfo.For("PLAIN", string.Empty)));
    }

    [Fact]
    public void A_picture_in_a_folder_is_named_the_way_Linux_writes_a_path()
    {
        Assert.Contains(
            "IconFile=AUTORUN/menu.png\n",
            Text(XdgVolumeInfo.For("PLAIN", TheLinuxIcon.NameBeside(@"AUTORUN\menu.ico"))),
            StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- the PNG

    [Fact]
    public void A_PNG_made_by_hand_is_one_something_else_can_read()
    {
        // Three pixels by two, every one different, so a row or a channel in
        // the wrong place shows.
        byte[] rgba =
        [
            255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255,
            10, 20, 30, 128, 40, 50, 60, 0, 70, 80, 90, 255,
        ];

        using SKBitmap read = SKBitmap.Decode(APngFile.From(3, 2, rgba));

        Assert.Equal(3, read.Width);
        Assert.Equal(2, read.Height);
        Assert.Equal(new SKColor(255, 0, 0, 255), read.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 255, 0, 255), read.GetPixel(1, 0));
        Assert.Equal(new SKColor(0, 0, 255, 255), read.GetPixel(2, 0));
        Assert.Equal(new SKColor(70, 80, 90, 255), read.GetPixel(2, 1));
        Assert.Equal(128, read.GetPixel(0, 1).Alpha);
        Assert.Equal(0, read.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public void Every_chunk_of_it_carries_a_check_that_is_right()
    {
        byte[] png = APngFile.From(2, 2, new byte[16]);

        Assert.True(png.AsSpan().StartsWith(APngFile.Signature));

        // The check every PNG ends on, which is the same in all of them.
        Assert.Equal(0xAE426082, APngFile.CheckOf("IEND"u8));

        List<string> kinds = [];

        for (int at = 8; at < png.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));

            kinds.Add(Encoding.ASCII.GetString(png, at + 4, 4));

            // A reader that minds, and most do, throws the file away on one
            // wrong check.
            Assert.Equal(
                APngFile.CheckOf(png.AsSpan(at + 4, 4 + length)),
                BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at + 8 + length)));

            at += 12 + length;
        }

        Assert.Equal(["IHDR", "IDAT", "IEND"], kinds);
    }

    [Fact]
    public void A_picture_of_the_wrong_length_is_refused_and_not_written_short()
    {
        Assert.Throws<ArgumentException>(() => APngFile.From(2, 2, new byte[15]));
    }

    // --------------------------------------------- out of the Windows icon

    [Fact]
    public void The_Linux_icon_is_the_largest_frame_of_the_Windows_one_the_right_way_up()
    {
        string ico = Path.Combine(_room, "made.ico");
        new SkiaImages().WriteIcon(Icon, ico);

        byte[]? png = TheLinuxIcon.FromTheWindowsOne(File.ReadAllBytes(ico));

        using SKBitmap read = SKBitmap.Decode(Assert.IsType<byte[]>(png));

        Assert.Equal(256, read.Width);
        Assert.Equal(256, read.Height);

        // Red on top and blue underneath, as it was painted. A bitmap keeps
        // its bottom row first and its blue first, and either one carried
        // across untouched swaps these two.
        AssertNearly(new SKColor(220, 30, 30), read.GetPixel(128, 40));
        AssertNearly(new SKColor(30, 30, 220), read.GetPixel(128, 216));
    }

    [Fact]
    public void A_frame_that_is_a_PNG_already_is_handed_over_as_it_stands()
    {
        byte[] inside = APngFile.From(1, 1, [1, 2, 3, 4]);

        Assert.Equal(inside, TheLinuxIcon.FromTheWindowsOne(AnIconHolding(inside, size: 0)));
    }

    [Theory]
    [InlineData("nothing at all")]
    [InlineData("a picture that is not an icon")]
    [InlineData("an icon that stops short")]
    [InlineData("an icon whose frame is not thirty-two bit")]
    public void A_file_it_cannot_read_gets_no_picture_and_no_exception(string what)
    {
        byte[] real = AnIconHolding(ABitmapFrame(16, bits: 32), size: 16);

        byte[] file = what switch
        {
            "nothing at all" => [],
            "a picture that is not an icon" => APngFile.From(1, 1, [1, 2, 3, 4]),
            "an icon that stops short" => real[..(real.Length - 200)],
            "an icon whose frame is not thirty-two bit" => AnIconHolding(ABitmapFrame(16, bits: 24), size: 16),
            _ => throw new ArgumentOutOfRangeException(nameof(what)),
        };

        // And the one it is cut down from does read, so these are refused
        // for what is wrong with them.
        Assert.NotNull(TheLinuxIcon.FromTheWindowsOne(real));
        Assert.Null(TheLinuxIcon.FromTheWindowsOne(file));
    }

    [Theory]
    [InlineData("FIXTUREGAME.ico", "FIXTUREGAME.png")]
    [InlineData(@"AUTORUN\menu.ico", @"AUTORUN\menu.png")]
    [InlineData("noending", "noending.png")]
    [InlineData(@"a.folder\icon", @"a.folder\icon.png")]
    public void It_is_called_what_the_icon_is_called(string icon, string png)
    {
        Assert.Equal(png, TheLinuxIcon.NameBeside(icon));
    }

    [Fact]
    public void Its_name_belongs_to_the_disc_and_an_extra_may_not_take_it()
    {
        Assert.True(ReservedDiscNames.Claims("FIXTUREGAME.png", "FIXTURE GAME"));
        Assert.True(ReservedDiscNames.Claims("fixturegame.PNG", "FIXTURE GAME"));
        Assert.True(ReservedDiscNames.Claims(".xdg-volume-info", "FIXTURE GAME"));
        Assert.False(ReservedDiscNames.Claims("cover.png", "FIXTURE GAME"));
    }

    // ------------------------------------------------------------ whole discs

    [Fact]
    public async Task A_disc_asked_to_carries_its_name_and_its_icon_for_Linux()
    {
        BuiltDisc disc = await ADisc(new DiscOptions(LinuxNameAndIcon: true), withAnIcon: true);

        Assert.Equal(
            "[Volume Info]\nName=FIXTURE GAME\nIconFile=FIXTUREGAME.png\n",
            Text(File.ReadAllBytes(On(disc, ".xdg-volume-info"))));

        using SKBitmap picture = SKBitmap.Decode(File.ReadAllBytes(On(disc, "FIXTUREGAME.png")));

        Assert.Equal(256, picture.Width);
        AssertNearly(new SKColor(220, 30, 30), picture.GetPixel(128, 40));
        AssertNearly(new SKColor(30, 30, 220), picture.GetPixel(128, 216));

        // And the Windows half is what it was.
        Assert.True(File.Exists(On(disc, "FIXTUREGAME.ico")));
        Assert.Contains("icon=FIXTUREGAME.ico", File.ReadAllText(On(disc, "autorun.inf")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Both_are_in_the_list_of_what_every_file_should_be()
    {
        BuiltDisc disc = await ADisc(new DiscOptions(LinuxNameAndIcon: true), withAnIcon: true);

        // Which they are only if the step runs before the list is made.
        string list = File.ReadAllText(On(disc, "checksums.sha256"));

        Assert.Contains("*.xdg-volume-info", list, StringComparison.Ordinal);
        Assert.Contains("*FIXTUREGAME.png", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_disc_not_asked_to_carries_neither()
    {
        BuiltDisc disc = await ADisc(new DiscOptions(), withAnIcon: true);

        Assert.DoesNotContain(".xdg-volume-info", EverythingOn(disc));
        Assert.DoesNotContain(EverythingOn(disc), name => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        // Which is what an ordinary disc is, and every disc the C# app made
        // before the option.
        Assert.False(new DiscOptions().LinuxNameAndIcon);
    }

    [Fact]
    public async Task With_no_icon_chosen_it_wears_the_one_the_menu_wears()
    {
        BuiltDisc disc = await ADisc(new DiscOptions(LinuxNameAndIcon: true), withAnIcon: false);

        Assert.Equal(
            "[Volume Info]\nName=FIXTURE GAME\nIconFile=AUTORUN/menu.png\n",
            Text(File.ReadAllBytes(On(disc, ".xdg-volume-info"))));

        using SKBitmap picture = SKBitmap.Decode(File.ReadAllBytes(On(disc, "AUTORUN/menu.png")));

        Assert.NotNull(picture);
        Assert.True(picture.Width >= 48, $"DiscWright's own icon came out {picture.Width} across.");
    }

    [Fact]
    public async Task With_no_icon_and_no_menu_it_is_still_named_and_names_no_picture()
    {
        BuiltDisc disc = await ADisc(new DiscOptions(Menu: false, LinuxNameAndIcon: true), withAnIcon: false);

        Assert.Equal("[Volume Info]\nName=FIXTURE GAME\n", Text(File.ReadAllBytes(On(disc, ".xdg-volume-info"))));
        Assert.DoesNotContain(EverythingOn(disc), name => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_image_carries_both_under_the_names_they_were_given()
    {
        if (!OperatingSystem.IsLinux())
        {
            // The image is xorriso's on Linux and IMAPI's on Windows.
            return;
        }

        BuiltDisc disc = await ADisc(new DiscOptions(LinuxNameAndIcon: true), withAnIcon: true);

        Outcome<MadeImage> made = await new XorrisoImageMaker(new WindowsFileSystem(), new RunningPrograms())
            .Make(disc.Folder, Path.Combine(Out, "FIXTURE GAME.iso"), disc.Label, false, disc.Number, disc.OfDiscs, null, default);

        MadeImage image = Assert.IsType<Outcome<MadeImage>.Ok>(made).Value;

        IReadOnlyList<TheJolietRecords.Recorded> recorded = TheJolietRecords.FilesIn(image.Path);

        // A name that starts with a dot, which is the one name on the disc
        // that does. Whole, and holding what the folder holds.
        TheJolietRecords.Recorded name = Assert.Single(recorded, file => file.Path == ".xdg-volume-info");

        Assert.Equal(File.ReadAllBytes(On(disc, ".xdg-volume-info")), TheJolietRecords.BytesOf(image.Path, name));
        Assert.Contains(recorded, file => file.Path == "FIXTUREGAME.png");
    }

    // ------------------------------------------- the project file and the sum

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_project_file_says_whether_and_says_it_under_the_old_key(bool asked)
    {
        DiscProject project = new("FIXTURE GAME", [TheGame()], new DiscOptions(LinuxNameAndIcon: asked));

        string json = TheProjectAsJson.Of(project, "2.0.4", "DVD5", Out, DateTimeOffset.UnixEpoch);

        // LinuxInfo is what the PowerShell app called it. The C# one wrote
        // false there from its first day, so a file of any age reads true.
        Assert.Contains($"\"LinuxInfo\": {(asked ? "true" : "false")}", json, StringComparison.Ordinal);

        OpenedProject back = Assert.IsType<Outcome<OpenedProject>.Ok>(AProjectFile.Read(json)).Value;

        Assert.Equal(asked, back.Options.LinuxNameAndIcon);
    }

    [Fact]
    public void A_project_file_that_does_not_say_means_no()
    {
        DiscProject project = new("FIXTURE GAME", [TheGame()], new DiscOptions(LinuxNameAndIcon: true));

        string json = TheProjectAsJson.Of(project, "2.0.4", "DVD5", Out, DateTimeOffset.UnixEpoch);
        string without = string.Join('\n', json.Split('\n').Where(line => !line.Contains("\"LinuxInfo\"", StringComparison.Ordinal)));

        Assert.NotEqual(json, without);

        OpenedProject back = Assert.IsType<Outcome<OpenedProject>.Ok>(AProjectFile.Read(without)).Value;

        Assert.False(back.Options.LinuxNameAndIcon);
    }

    [Fact]
    public void The_sum_of_a_disc_leaves_room_for_the_picture()
    {
        DiscProject without = new("FIXTURE GAME", [TheGame()], new DiscOptions());
        DiscProject with = without with { Options = new DiscOptions(LinuxNameAndIcon: true) };

        Assert.Equal(
            TheLinuxIcon.AtMost,
            DiscOverhead.For(with, _ => 0) - DiscOverhead.For(without, _ => 0));

        // And the most it can be is more than the most it is.
        Assert.True(APngFile.From(256, 256, Noise(256 * 256 * 4)).Length <= TheLinuxIcon.AtMost);
    }

    // ------------------------------------------------------------------ helpers

    private GameFolder TheGame() =>
        Assert.IsType<Outcome<GameFolder>.Ok>(
            GameFolders.InTheUsualOrder(new WindowsFileSystem(), new VersionResourceFacts()).At(Download)).Value;

    private async Task<BuiltDisc> ADisc(DiscOptions options, bool withAnIcon)
    {
        DiscProject project = new("FIXTURE GAME", [TheGame()], options)
        {
            Artwork = withAnIcon ? new DiscArtwork(IconPath: Icon) : null
        };

        Outcome<BuiltDiscs> built = await new DiscBuilder(
                new WindowsFileSystem(), new WindowsStageWriter(), new SkiaImages(), new Sha256Hasher())
            .Build(project, new Medium("TEST", "a test disc", 10_000_000), Out, null, CancellationToken.None);

        return Assert.Single(Assert.IsType<Outcome<BuiltDiscs>.Ok>(built).Value.Discs);
    }

    private static string[] EverythingOn(BuiltDisc disc) =>
    [
        .. Directory
            .EnumerateFiles(disc.Folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(disc.Folder, file).Replace(Path.DirectorySeparatorChar, '/'))
    ];

    private static string On(BuiltDisc disc, string relative) => Path.Combine([disc.Folder, .. relative.Split('/')]);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static void AssertNearly(SKColor wanted, SKColor got)
    {
        // Within a little, because the picture was scaled on the way.
        Assert.True(
            Math.Abs(wanted.Red - got.Red) <= 6 && Math.Abs(wanted.Green - got.Green) <= 6
                && Math.Abs(wanted.Blue - got.Blue) <= 6 && got.Alpha >= 250,
            $"Wanted about {wanted} and got {got}.");
    }

    /// <summary>Red on top and blue underneath, so upside down and back to front both show.</summary>
    private static void PaintInHalves(string path, int size)
    {
        using SKBitmap bitmap = new(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));

        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(new SKColor(220, 30, 30));

            using SKPaint blue = new() { Color = new SKColor(30, 30, 220) };
            canvas.DrawRect(SKRect.Create(0, size / 2, size, size / 2), blue);
        }

        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());
    }

    private static byte[] Noise(int length)
    {
        byte[] noise = new byte[length];
        new Random(20261116).NextBytes(noise);
        return noise;
    }

    /// <summary>An icon file with one frame in it, which is these bytes.</summary>
    private static byte[] AnIconHolding(byte[] frame, int size)
    {
        using MemoryStream file = new();
        using BinaryWriter write = new(file);

        write.Write((short)0);
        write.Write((short)1);
        write.Write((short)1);

        write.Write((byte)size);
        write.Write((byte)size);
        write.Write((byte)0);
        write.Write((byte)0);
        write.Write((short)1);
        write.Write((short)32);
        write.Write(frame.Length);
        write.Write(22);
        write.Write(frame);
        write.Flush();

        return file.ToArray();
    }

    /// <summary>A frame kept as a bitmap, the way DiscWright keeps every one.</summary>
    private static byte[] ABitmapFrame(int size, short bits)
    {
        using MemoryStream made = new();
        using BinaryWriter write = new(made);

        write.Write(40);
        write.Write(size);
        write.Write(size * 2);
        write.Write((short)1);
        write.Write(bits);
        write.Write(0);
        write.Write(0);
        write.Write(0);
        write.Write(0);
        write.Write(0);
        write.Write(0);
        write.Write(new byte[size * size * 4]);
        write.Write(new byte[((size + 31) / 32) * 4 * size]);
        write.Flush();

        return made.ToArray();
    }
}
