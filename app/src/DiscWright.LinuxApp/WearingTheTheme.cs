using Avalonia;
using Avalonia.Styling;

using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// Dresses the program the way the desktop is dressed, or the way somebody
/// chose, and keeps it dressed when either changes its mind.
/// </summary>
/// <remarks>
/// <para>
/// The twin of <c>WearingTheTheme</c> in the Windows app, and a fifth of its
/// length, because three of the things that one does by hand Avalonia does
/// itself. Look.axaml names the dark palette as its answer for the dark, so
/// nothing is merged or taken off here. A look that follows the desktop is
/// one Avalonia already follows, hearing the change when the desktop
/// announces it. And the title bar is the desktop's own on Linux, drawn the
/// way the desktop draws every title bar, so there is nothing to paint.
/// </para>
/// <para>
/// Which look to wear is still <see cref="ChoosingTheLook"/>'s decision, the
/// same object on both systems, kept in the same note.
/// </para>
/// </remarks>
public static class WearingTheTheme
{
    public static void Follow(Application app, ChoosingTheLook choice)
    {
        Wear(app, choice.Chosen);

        // The person changing their mind, through the quiet control on the
        // window. It arrives from a click, on the thread the window is on.
        choice.PropertyChanged += (_, _) => Wear(app, choice.Chosen);
    }

    private static void Wear(Application app, ALook chosen) =>
        app.RequestedThemeVariant = chosen switch
        {
            ALook.Light => ThemeVariant.Light,
            ALook.Dark => ThemeVariant.Dark,

            // Whatever the desktop wears, now and after it changes.
            _ => ThemeVariant.Default,
        };
}
