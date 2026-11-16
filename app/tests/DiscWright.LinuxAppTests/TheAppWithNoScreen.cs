using Avalonia;
using Avalonia.Headless;

using DiscWright.LinuxApp;
using DiscWright.LinuxAppTests;

[assembly: AvaloniaTestApplication(typeof(TheAppWithNoScreen))]

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The real app, started the way every test here starts it: with no screen,
/// and with everything else as it is on a desktop.
/// </summary>
/// <remarks>
/// Real Skia, not Avalonia's stand-in that draws nothing. With the stand-in a
/// test can press buttons and never find out the window cannot be drawn, and
/// the first thing this project caught was exactly that: a second Skia beside
/// Avalonia's, and an app that died before its window opened.
/// </remarks>
public static class TheAppWithNoScreen
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
