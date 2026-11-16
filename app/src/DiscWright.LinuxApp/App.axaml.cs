using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using DiscWright.Platform;
using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// The Linux app: the one place that will know the real answers are a Linux
/// machine's, and for now the place that opens the window.
/// </summary>
public partial class App : Application
{
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
            ChoosingTheLook look = new(keptLook.Recalled(), keptLook.Keep);
            WearingTheTheme.Follow(this, look);

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
