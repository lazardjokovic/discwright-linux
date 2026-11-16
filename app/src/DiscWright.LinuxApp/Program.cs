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
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

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
