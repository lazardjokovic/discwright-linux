using System.Runtime.InteropServices;

using DiscWright.Core;

using SkiaSharp;

namespace DiscWright.Linux;

/// <summary>
/// Reads pictures and writes the two a disc needs, with no Windows under it.
/// </summary>
/// <remarks>
/// <para>
/// The Linux side of <c>WindowsImages</c>, behind the same port. That one uses
/// System.Drawing, which has been Windows only since .NET 7. This one uses
/// Skia, the drawing library the Linux window is drawn with as well, so it is
/// the one dependency for both jobs.
/// </para>
/// <para>
/// What it makes is the same in kind and not in bytes: the same seven frames
/// cropped from the middle, the same background filled and cropped evenly.
/// Two drawing libraries do not smooth a picture alike, so a test of this
/// checks the shape exactly and the colours within reason.
/// </para>
/// </remarks>
public sealed class SkiaImages : IImages
{
    /// <summary>The smoothing used whenever a picture changes size.</summary>
    private static readonly SKSamplingOptions Smoothly = new(SKCubicResampler.Mitchell);

    public ImageFacts Read(string path)
    {
        if (!File.Exists(path))
        {
            return new ImageFacts(false, 0, 0, "That file is not there any more.");
        }

        try
        {
            using SKCodec? codec = SKCodec.Create(path);

            if (codec is null)
            {
                // Said in words about the file, never in the library's own. On
                // Windows the library's word for this is "Out of memory", which
                // sent people hunting a problem they did not have.
                return new ImageFacts(false, 0, 0,
                    "That file cannot be read as an image. It may be corrupt, still downloading, or not a "
                    + "picture at all. PNG, JPG and BMP always work.");
            }

            return new ImageFacts(true, codec.Info.Width, codec.Info.Height, null);
        }
        catch (Exception wrong) when (wrong is IOException or UnauthorizedAccessException)
        {
            return new ImageFacts(false, 0, 0, "That file cannot be opened: " + wrong.Message);
        }
    }

    public void WriteIcon(string source, string destination)
    {
        using SKBitmap original = Decoded(source);
        using SKImage image = SKImage.FromBitmap(original);

        // Cropped from the middle when the picture is not square, the same
        // amount off both sides, rather than squashed into one.
        int side = Math.Min(original.Width, original.Height);
        SKRect middle = SKRect.Create((original.Width - side) / 2, (original.Height - side) / 2, side, side);

        List<(int Size, byte[] Pixels)> frames = [];

        foreach (int size in AnIconFile.Sizes)
        {
            using SKBitmap frame = new(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));

            using (SKCanvas canvas = new(frame))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawImage(image, middle, SKRect.Create(0, 0, size, size), Smoothly);
            }

            frames.Add((size, WithStraightAlpha(frame)));
        }

        File.WriteAllBytes(destination, AnIconFile.From(frames));
    }

    public void WriteBackground(string source, string destination, int width, int height, bool asIs)
    {
        using SKBitmap original = Decoded(source);
        using SKImage image = SKImage.FromBitmap(original);
        using SKBitmap made = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

        using (SKCanvas canvas = new(made))
        {
            canvas.Clear(SKColors.Transparent);

            if (asIs)
            {
                // Stretched to the shape of the menu, proportions and all.
                canvas.DrawImage(image, Whole(original), SKRect.Create(0, 0, width, height), Smoothly);
            }
            else
            {
                // Filled and cropped evenly. Keeping the proportions is almost
                // always what somebody wants from a screenshot or cover art,
                // and the overflow is taken off both sides rather than one.
                double scale = Math.Max((double)width / original.Width, (double)height / original.Height);
                int drawnWidth = (int)Math.Ceiling(original.Width * scale);
                int drawnHeight = (int)Math.Ceiling(original.Height * scale);

                canvas.DrawImage(
                    image,
                    Whole(original),
                    SKRect.Create((width - drawnWidth) / 2, (height - drawnHeight) / 2, drawnWidth, drawnHeight),
                    Smoothly);
            }
        }

        using SKData png = made.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(destination, png.ToArray());
    }

    private static SKRect Whole(SKBitmap picture) => SKRect.Create(0, 0, picture.Width, picture.Height);

    /// <summary>
    /// The picture, or an exception that names the file. The window looks at a
    /// picture the moment it is picked, so by the time one is written it has
    /// already been read once, and a failure here is a file that changed since.
    /// </summary>
    private static SKBitmap Decoded(string source) =>
        SKBitmap.Decode(source)
        ?? throw new IOException($"{source} cannot be read as an image.");

    /// <summary>
    /// The frame's pixels as an icon stores them: blue, green, red, alpha, with
    /// the colour not multiplied by the alpha.
    /// </summary>
    /// <remarks>
    /// Skia draws with the colour already multiplied, which is the right way to
    /// draw and the wrong thing to store. Written into an icon as it is, every
    /// soft edge comes out darker than the picture it was cut from.
    /// </remarks>
    private static byte[] WithStraightAlpha(SKBitmap frame)
    {
        byte[] pixels = new byte[frame.Width * frame.Height * 4];
        GCHandle held = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            SKImageInfo straight = new(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

            if (!frame.PeekPixels().ReadPixels(straight, held.AddrOfPinnedObject(), frame.Width * 4))
            {
                throw new IOException("The icon's pixels could not be read back out of the drawing.");
            }
        }
        finally
        {
            held.Free();
        }

        return pixels;
    }
}
