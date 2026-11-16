using DiscWright.Core;
using DiscWright.LinuxApp;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The main window as a test wants it: built the way the app builds it, with
/// the real Linux machine behind it, and nothing written to anybody's log.
/// </summary>
internal static class TheWindow
{
    /// <summary>The window as it is when the app has just started.</summary>
    public static MainWindow WithNothingOnIt() => Showing(AScreen());

    /// <summary>The window over a screen the test keeps hold of.</summary>
    public static MainWindow Showing(MakeADisc screen) => new(screen);

    /// <summary>The screen over the machine the app composes, logging nowhere.</summary>
    public static MakeADisc AScreen() => new(TheLinuxMachine.Composed(new NothingIsLogged()));
}
