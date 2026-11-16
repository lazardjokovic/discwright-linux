using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using DiscWright.Platform;
using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// The Linux app: where the window is handed the screen, and the screen the
/// machine. What the machine is made of is <see cref="TheLinuxMachine"/>.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// What sets the look when nobody has chosen one, as the control on the
    /// window names it. The Windows app says Windows.
    /// </summary>
    public const string WhatTheLookFollows = "the desktop";

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Under a test there is no desktop to put a window on, and the test
        // opens the window it wants to look at.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The look first, read back from where it was kept, so the window
            // opens dressed the way it was left and never flashes the other
            // look on its way. Here and not above, because a test must not be
            // dressed by a note on the machine it happens to run on.
            ANoteAboutTheLook keptLook = ANoteAboutTheLook.ForThisUser();
            ChoosingTheLook look = new(keptLook.Recalled(), keptLook.Keep, WhatTheLookFollows);
            WearingTheTheme.Follow(this, look);

            // Next, so that everything after it has somewhere to write. One
            // file a day under the user's own data, never sent anywhere.
            TheLogOnDisk log = new(TheLogOnDisk.ForThisUser());

            // Every question asked in front of somebody, and what they said.
            WorthAsking.Log = log;

            // A fault inside a press would otherwise close the program with
            // no word about why. Kept for as long as the program runs.
            _ = AFaultNobodyExpected.IsHeardBy(Dispatcher.UIThread, log, () => desktop.MainWindow);

            MakeADisc screen = new(TheLinuxMachine.Composed(log));

            // Everything the window says, as it says it.
            WhatTheLogHears.Listen(screen, log);

            desktop.MainWindow = new MainWindow(
                screen, TheLinuxMachine.TheReaderOfFolders(), new WindowsFileSystem(), look, new TheDesktop())
            {
                TheLog = log,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
