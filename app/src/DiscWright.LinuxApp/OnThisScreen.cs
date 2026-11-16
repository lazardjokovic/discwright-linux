using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// Opens a window at a size that fits the screen it is on.
/// </summary>
/// <remarks>
/// <para>
/// The window is drawn at 920 by 740. A laptop at 1366 by 768 has less than
/// that once the desktop's panel has its share, and the bottom line, which
/// is where Build it is, sat under the panel: seen on the test machine on
/// 2026-11-16, whose screen is 768 tall. How big it should be then is
/// <see cref="FittingTheScreen"/>'s arithmetic, the same on both systems.
/// What is here is asking this desktop how much room there is.
/// </para>
/// <para>
/// <strong>After the window is up, and only when it does not fit.</strong>
/// The Windows window is fitted before it is shown, so it never appears at
/// one size and jumps to another. Here the room cannot be known until the
/// window is up, because the desktop puts a title bar on it and the title
/// bar is the desktop's, of a height only the desktop knows. So a window
/// that fits as drawn is left exactly where the desktop put it, and only
/// one that does not is made smaller, once.
/// </para>
/// </remarks>
public static class OnThisScreen
{
    /// <summary>Fits a window that is already up to the screen it is on.</summary>
    public static void Fit(Window window)
    {
        if ((window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary) is not Screen screen)
        {
            return;
        }

        double scale = screen.Scaling;

        // What the desktop put round the window: a title bar above it and,
        // on some, an edge below. It comes out of the room the window has.
        double frame = window.FrameSize is Size whole ? Math.Max(0, whole.Height - window.ClientSize.Height) : 0;

        Fit(
            window,
            new FittingTheScreen.Room(
                screen.WorkingArea.X / scale,
                screen.WorkingArea.Y / scale,
                screen.WorkingArea.Width / scale,
                (screen.WorkingArea.Height / scale) - frame),
            scale);
    }

    /// <summary>Fits a window to a room of a known size.</summary>
    /// <param name="window">The window, at the size it was drawn.</param>
    /// <param name="room">The room there is for it, in the window's own units.</param>
    /// <param name="scale">How many of the screen's pixels one of the window's units is.</param>
    public static void Fit(Window window, FittingTheScreen.Room room, double scale = 1)
    {
        FittingTheScreen.Fitted fitted = FittingTheScreen.In(
            (window.Width, window.Height), (window.MinWidth, window.MinHeight), room);

        // It fits as drawn. The desktop has already put it somewhere, and
        // moving a window nobody asked to move is its own small fault.
        if (fitted.Width >= window.Width && fitted.Height >= window.Height)
        {
            return;
        }

        window.MinWidth = fitted.MinWidth;
        window.MinHeight = fitted.MinHeight;
        window.Width = fitted.Width;
        window.Height = fitted.Height;
        window.Position = new PixelPoint((int)(fitted.Left * scale), (int)(fitted.Top * scale));
    }
}
