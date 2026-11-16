using Avalonia;

namespace DiscWright.LinuxApp;

/// <summary>
/// Where the Linux DiscWright starts.
/// </summary>
internal static class Program
{
    // Nothing of Avalonia's may be touched before this has run: it is what
    // makes there be a dispatcher and a screen to touch.
    [STAThread]
    public static int Main(string[] args) =>

        // Through WhatTheSystemLacks, so that a system with no desktop on it,
        // which has not got the libraries a window is drawn through, is told
        // so in a sentence. That this line goes through it is held by
        // tools/see-the-linux-downloads-on-another-system.sh, on a system
        // that really lacks one: no test here can take a library away.
        WhatTheSystemLacks.SaidInPlaceOfThrown(
            () => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args), Console.Error);

    /// <summary>
    /// The app as it really runs. Public and by this name because Avalonia's
    /// previewer looks for exactly this.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()

            // The face comes inside the app. A Linux machine has no Segoe UI,
            // and which faces it does have differs from one to the next, so a
            // window drawn in whatever is there is a different width on each.
            .WithInterFont();
}
