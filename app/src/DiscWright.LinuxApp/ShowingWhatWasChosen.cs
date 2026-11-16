using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DiscWright.LinuxApp;

/// <summary>
/// A small picture of a file somebody chose, for showing it back to them.
/// </summary>
/// <remarks>
/// <para>
/// Loaded whole and then let go, rather than left attached to the file: the
/// first thing somebody does after choosing the wrong picture is try to
/// replace it. The file is opened here, read, and shut by the end of the
/// method.
/// </para>
/// <para>
/// Decoded small on purpose. A cover is a couple of thousand pixels tall and
/// this shows it at seventy two at most.
/// </para>
/// </remarks>
public sealed class APicture : IValueConverter
{
    public static APicture Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            return Bitmap.DecodeToHeight(file, 144);
        }
        catch (Exception wrong) when (wrong is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // Why it cannot be read is said beside the picker, by the screen,
            // in words that name the file. A broken thumbnail says it twice
            // and worse.
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Nothing becomes a file by being shown.");
}

/// <summary>
/// A colour the screen keeps as text, shown as the brush it names.
/// </summary>
/// <remarks>
/// For the swatches and the backdrop. The screen only ever holds a colour it
/// has tidied, so anything unreadable here is a fault worth seeing, and it
/// comes out as nothing rather than thrown because a swatch must not take
/// the window down.
/// </remarks>
public sealed class AsABrush : IValueConverter
{
    public static AsABrush Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && text.Length > 0 && Color.TryParse(text, out Color colour)
            ? new SolidColorBrush(colour)
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A brush is not turned back into the words for it.");
}
