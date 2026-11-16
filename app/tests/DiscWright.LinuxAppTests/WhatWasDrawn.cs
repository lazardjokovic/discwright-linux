using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// What a test needs to look at a window the way a person does: the colour of
/// a pixel that was really drawn, a part of a control by its name, and the
/// look the whole program is wearing.
/// </summary>
internal static class WhatWasDrawn
{
    /// <summary>The colour drawn at one point of a window, in the window's own units.</summary>
    public static Color ColourAt(this Window window, Point where)
    {
        Dispatcher.UIThread.RunJobs();

        using WriteableBitmap drawn = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("The window drew nothing.");

        using ILockedFramebuffer pixels = drawn.Lock();

        byte[] one = new byte[4];
        Marshal.Copy(pixels.Address + ((int)where.Y * pixels.RowBytes) + ((int)where.X * 4), one, 0, 4);

        // Skia draws in whichever order suits the machine, and says which.
        return pixels.Format == PixelFormat.Rgba8888
            ? Color.FromArgb(one[3], one[0], one[1], one[2])
            : Color.FromArgb(one[3], one[2], one[1], one[0]);
    }

    /// <summary>A named part of a control's template, which has to be there.</summary>
    public static T Part<T>(this Control control, string name)
        where T : Control =>
        control.GetVisualDescendants().OfType<T>().FirstOrDefault(part => part.Name == name)
            ?? throw new InvalidOperationException($"The {control.GetType().Name} has no {typeof(T).Name} called {name} in it.");

    /// <summary>
    /// A control on a window, found the way whatever drives the window finds
    /// it: by the AutomationId the markup gave it. Exactly one, or a failure
    /// that names what was looked for.
    /// </summary>
    public static T Found<T>(this Window window, string id)
        where T : Control
    {
        Dispatcher.UIThread.RunJobs();

        T[] found =
        [
            .. window.GetVisualDescendants().OfType<T>()
                .Where(each => Avalonia.Automation.AutomationProperties.GetAutomationId(each) == id)
        ];

        return found.Length == 1
            ? found[0]
            : throw new InvalidOperationException(
                $"Looked for one {typeof(T).Name} answering to {id} on the window and found {found.Length}.");
    }

    /// <summary>
    /// Whether a control is somewhere a person could see it: on a window,
    /// with nothing above it hidden, and taking up room.
    /// </summary>
    /// <remarks>
    /// Not IsEffectivelyVisible alone. A control that was never put on a
    /// window has nothing above it to be hidden, and says it is visible.
    /// </remarks>
    public static bool CanBeSeen(this Control control)
    {
        Dispatcher.UIThread.RunJobs();

        return TopLevel.GetTopLevel(control) is not null
            && control.IsEffectivelyVisible
            && control.Bounds.Width > 0
            && control.Bounds.Height > 0;
    }

    /// <summary>Where a control's middle is, in the window it is in.</summary>
    public static Point MiddleIn(this Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The control is not in that window.");

    /// <summary>A colour of the palette, as the program holds it for one look.</summary>
    public static Color Of(string name, ThemeVariant look) =>
        Application.Current!.TryGetResource(name, look, out object? found) && found is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"The palette has no brush called {name} for the {look} look.");

    /// <summary>A brush somebody set, as the colour it is.</summary>
    public static Color AsColour(this IBrush? brush) =>
        brush is ISolidColorBrush solid
            ? solid.Color
            : throw new InvalidOperationException("That is not one flat colour: " + (brush?.ToString() ?? "nothing"));

    /// <summary>
    /// Runs something with the whole program in one look, and puts the look
    /// back afterwards whatever happened, because the program is shared by
    /// every test here.
    /// </summary>
    public static void Wearing(ThemeVariant look, Action what)
    {
        ThemeVariant? before = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = look;

        try
        {
            Dispatcher.UIThread.RunJobs();
            what();
        }
        finally
        {
            Application.Current.RequestedThemeVariant = before;
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// A window with one thing in it, opened and laid out. The thing sits in
    /// the middle at the size it asks for, with nothing set on it, so what a
    /// test reads off it is the look's doing and not the test's.
    /// </summary>
    public static Window Showing(Control content)
    {
        Window window = new()
        {
            Width = 400,
            Height = 200,
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { content },
            },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }
}
