using DiscWright.Core;
using DiscWright.Linux;

using SkiaSharp;

namespace DiscWright.Tests;

/// <summary>
/// The disc's icon and the menu's background, made by the code a Linux
/// DiscWright uses, and judged by reading the files it writes.
/// </summary>
/// <remarks>
/// <para>
/// The twin of <see cref="TheRealIconWriter"/>, which asks Windows' own icon
/// reader whether an icon is right. There is no such reader here, so these
/// read the icon's own bytes: the directory, each frame's header, and the
/// pixels where the format says they are. That is a stricter question than
/// "does it open", and the same on both systems.
/// </para>
/// <para>
/// They run on Windows too, because the code is plain .NET and Skia, and a
/// fault in it is as wrong there as anywhere.
/// </para>
/// </remarks>
public sealed class PicturesWithoutWindows : IDisposable
{
    private static readonly SKColor Red = new(255, 0, 0);
    private static readonly SKColor Green = new(0, 255, 0);
    private static readonly SKColor Blue = new(0, 0, 255);
    private static readonly SKColor White = new(255, 255, 255);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dw-pictures-" + Guid.NewGuid().ToString("N"));

    public PicturesWithoutWindows() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Carries_every_size_Explorer_asks_for()
    {
        byte[] icon = AnIconOf(APicture(512, 512, (0, 0, 512, 512, Blue)));

        Assert.Equal(0, BitConverter.ToInt16(icon, 0));
        Assert.Equal(1, BitConverter.ToInt16(icon, 2));
        Assert.Equal(7, BitConverter.ToInt16(icon, 4));

        // 256 is written as 0, because one byte holds the size.
        Assert.Equal(
            [16, 24, 32, 48, 64, 128, 0],
            Enumerable.Range(0, 7).Select(at => (int)icon[6 + (at * 16)]));
    }

    [Fact]
    public void Every_frame_is_where_the_directory_says_and_exactly_as_long()
    {
        byte[] icon = AnIconOf(APicture(512, 512, (0, 0, 512, 512, Blue)));
        int expected = 6 + (16 * 7);

        for (int at = 0; at < 7; at++)
        {
            int size = AnIconFile.Sizes[at];
            int length = BitConverter.ToInt32(icon, 6 + (at * 16) + 8);
            int offset = BitConverter.ToInt32(icon, 6 + (at * 16) + 12);

            // The header, the pixels, and a mask of one bit a pixel with each
            // row padded to four bytes. A mask of the wrong size puts every
            // frame after it at the wrong place.
            Assert.Equal(40 + (size * size * 4) + (((size + 31) / 32) * 4 * size), length);
            Assert.Equal(expected, offset);

            expected += length;
        }

        // Nothing after the last frame and nothing missing from it.
        Assert.Equal(expected, icon.Length);
    }

    [Fact]
    public void Every_frame_says_it_is_a_32_bit_bitmap_twice_as_tall_as_it_is()
    {
        byte[] icon = AnIconOf(APicture(512, 512, (0, 0, 512, 512, Blue)));

        for (int at = 0; at < 7; at++)
        {
            int size = AnIconFile.Sizes[at];
            int offset = BitConverter.ToInt32(icon, 6 + (at * 16) + 12);

            Assert.Equal(40, BitConverter.ToInt32(icon, offset));
            Assert.Equal(size, BitConverter.ToInt32(icon, offset + 4));

            // Doubled: the colour rows and the mask rows are described at once.
            Assert.Equal(size * 2, BitConverter.ToInt32(icon, offset + 8));
            Assert.Equal(1, BitConverter.ToInt16(icon, offset + 12));
            Assert.Equal(32, BitConverter.ToInt16(icon, offset + 14));
        }
    }

    [Fact]
    public void Draws_the_icon_the_right_way_up()
    {
        // A bitmap stores its rows bottom first. Written in the order they are
        // drawn, the icon is upside down, in a file of exactly the right size.
        byte[] icon = AnIconOf(APicture(64, 64, (0, 0, 64, 32, Red), (0, 32, 64, 32, Blue)));

        Assert.Equal(Red, PixelOf(icon, 64, across: 32, down: 8));
        Assert.Equal(Blue, PixelOf(icon, 64, across: 32, down: 56));
    }

    [Fact]
    public void Makes_a_square_icon_out_of_the_middle_of_a_picture_that_is_not_one()
    {
        // Three bands across. A square cut from the middle is all the middle
        // band. Squashed instead, or cut from one side, an edge is another colour.
        byte[] icon = AnIconOf(APicture(
            300, 100, (0, 0, 100, 100, Red), (100, 0, 100, 100, Green), (200, 0, 100, 100, Blue)));

        Assert.Equal(Green, PixelOf(icon, 64, across: 2, down: 32));
        Assert.Equal(Green, PixelOf(icon, 64, across: 61, down: 32));
    }

    [Fact]
    public void Keeps_what_is_see_through_without_darkening_it()
    {
        // Half see-through green. Stored the way it is drawn, with the colour
        // already multiplied by the alpha, the green would arrive as 128 and
        // every soft edge of every icon would be darker than its picture.
        byte[] icon = AnIconOf(APicture(64, 64, (0, 0, 64, 64, new SKColor(0, 255, 0, 128))));

        SKColor middle = PixelOf(icon, 64, across: 32, down: 32);

        Assert.InRange(middle.Alpha, 126, 130);
        Assert.InRange(middle.Green, 250, 255);
        Assert.Equal(0, middle.Red);
        Assert.Equal(0, middle.Blue);
    }

    [Fact]
    public void Makes_a_background_the_exact_size_of_the_menu()
    {
        using SKBitmap background = ABackgroundOf(APicture(1920, 1080, (0, 0, 1920, 1080, Blue)), asIs: false);

        Assert.Equal(760, background.Width);
        Assert.Equal(480, background.Height);
    }

    [Fact]
    public void Makes_a_background_from_a_picture_of_the_wrong_shape_entirely()
    {
        // The deliberately wrong shape in the testing kit: 1920x500.
        using SKBitmap background = ABackgroundOf(APicture(1920, 500, (0, 0, 1920, 500, Blue)), asIs: false);

        Assert.Equal(760, background.Width);
        Assert.Equal(480, background.Height);
    }

    [Fact]
    public void Fills_the_menu_and_takes_the_overflow_off_both_sides_evenly()
    {
        // Twice as wide as the menu and exactly as tall, with a red quarter on
        // the left and a blue one on the right. Filled and cropped evenly,
        // both quarters are cut away and what is left is white from edge to edge.
        string picture = APicture(
            1520, 480, (0, 0, 380, 480, Red), (380, 0, 760, 480, White), (1140, 0, 380, 480, Blue));

        using SKBitmap background = ABackgroundOf(picture, asIs: false);

        Assert.Equal(White, background.GetPixel(3, 240));
        Assert.Equal(White, background.GetPixel(756, 240));
    }

    [Fact]
    public void Stretches_the_whole_picture_in_when_it_is_asked_to_use_it_as_it_is()
    {
        string picture = APicture(
            1520, 480, (0, 0, 380, 480, Red), (380, 0, 760, 480, White), (1140, 0, 380, 480, Blue));

        using SKBitmap background = ABackgroundOf(picture, asIs: true);

        // Nothing cut away: the red is still at the left and the blue at the right.
        Assert.Equal(Red, background.GetPixel(3, 240));
        Assert.Equal(White, background.GetPixel(380, 240));
        Assert.Equal(Blue, background.GetPixel(756, 240));
    }

    [Fact]
    public void Measures_a_picture_it_can_read()
    {
        ImageFacts facts = new SkiaImages().Read(APicture(800, 600, (0, 0, 800, 600, Blue)));

        Assert.True(facts.Readable);
        Assert.Equal(800, facts.Width);
        Assert.Equal(600, facts.Height);
        Assert.False(facts.IsSquare);
        Assert.Null(facts.Why);
    }

    [Fact]
    public void Says_what_is_wrong_with_a_file_it_cannot_read()
    {
        string notAPicture = Path.Combine(_folder, "notes.txt");
        File.WriteAllText(notAPicture, "this is not a picture");

        ImageFacts facts = new SkiaImages().Read(notAPicture);

        Assert.False(facts.Readable);
        Assert.Contains("cannot be read as an image", facts.Why, StringComparison.Ordinal);
        Assert.Contains("PNG", facts.Why, StringComparison.Ordinal);
    }

    [Fact]
    public void Says_a_file_is_gone_rather_than_that_it_is_not_a_picture()
    {
        ImageFacts facts = new SkiaImages().Read(Path.Combine(_folder, "it was here a minute ago.png"));

        Assert.False(facts.Readable);
        Assert.Equal("That file is not there any more.", facts.Why);
    }

    [Fact]
    public void Refuses_a_frame_of_the_wrong_length_rather_than_writing_a_broken_icon()
    {
        Assert.Throws<ArgumentException>(() => AnIconFile.From([(16, new byte[100])]));
    }

    private byte[] AnIconOf(string picture)
    {
        string made = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".ico");

        new SkiaImages().WriteIcon(picture, made);

        return File.ReadAllBytes(made);
    }

    private SKBitmap ABackgroundOf(string picture, bool asIs)
    {
        string made = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".png");

        new SkiaImages().WriteBackground(picture, made, 760, 480, asIs);

        return SKBitmap.Decode(made);
    }

    /// <summary>
    /// One pixel of one frame, found the way the format says: the frame of
    /// that size, past its header, rows stored bottom first.
    /// </summary>
    private static SKColor PixelOf(byte[] icon, int size, int across, int down)
    {
        int frame = AnIconFile.Sizes.ToList().IndexOf(size);
        int offset = BitConverter.ToInt32(icon, 6 + (frame * 16) + 12);
        int at = offset + 40 + ((size - 1 - down) * size * 4) + (across * 4);

        return new SKColor(icon[at + 2], icon[at + 1], icon[at], icon[at + 3]);
    }

    /// <summary>A picture of flat coloured rectangles, so every pixel's colour is known.</summary>
    private string APicture(int width, int height, params (int Left, int Top, int Width, int Height, SKColor Colour)[] blocks)
    {
        string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".png");

        using SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            foreach ((int left, int top, int blockWidth, int blockHeight, SKColor colour) in blocks)
            {
                // Src, so a see-through colour replaces what is there rather
                // than being laid over it.
                using SKPaint paint = new() { Color = colour, BlendMode = SKBlendMode.Src };
                canvas.DrawRect(SKRect.Create(left, top, blockWidth, blockHeight), paint);
            }
        }

        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());

        return path;
    }
}
