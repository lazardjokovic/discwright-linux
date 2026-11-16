using Avalonia.Controls;

using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// The main window. It holds no decisions: it is handed the screen and shows it.
/// </summary>
public partial class MainWindow : Window
{
    /// <param name="screen">Everything the window knows and can do, the same object the Windows window is handed.</param>
    public MainWindow(MakeADisc screen)
    {
        InitializeComponent();
        DataContext = screen;
    }
}
