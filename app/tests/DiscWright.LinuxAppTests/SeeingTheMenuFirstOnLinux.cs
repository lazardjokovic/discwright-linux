using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

using DiscWright.Core;
using DiscWright.LinuxApp;
using DiscWright.Platform;
using DiscWright.Tests;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The button that shows what the disc's menu will look like, which on
/// Linux cannot.
/// </summary>
/// <remarks>
/// The menu is a Windows page, opened on Windows by the thing that will open
/// it on the disc. Nothing on a Linux desktop draws one, and a second
/// drawing that only looked similar would be a preview that drifts from the
/// disc. So the button is there, dead, and says why: the owner's decision
/// on 2026-11-15.
/// </remarks>
public sealed class SeeingTheMenuFirstOnLinux : IDisposable
{
    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-preview-" + Guid.NewGuid().ToString("N"));

    public SeeingTheMenuFirstOnLinux() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [AvaloniaFact]
    public void The_button_is_where_it_is_on_Windows_under_the_same_name()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        Button preview = window.Found<Button>("PreviewTheMenu");
        Button add = window.Found<Button>("AddAGame");
        TreeView list = window.Found<TreeView>("Games");

        Assert.Equal("See what it will look like", preview.Content);
        Assert.Same(Application.Current!.FindResource("Ordinary"), preview.Theme);

        // On the row Add a game is on, at the far end of it: up against the
        // right edge of the list it is about.
        Assert.Equal(add.TranslatePoint(default, window)!.Value.Y, preview.TranslatePoint(default, window)!.Value.Y);

        Border panel = (Border)list.Parent!.Parent!;

        Assert.Equal(
            panel.TranslatePoint(new Point(panel.Bounds.Width, 0), window)!.Value.X,
            preview.TranslatePoint(new Point(preview.Bounds.Width, 0), window)!.Value.X);
    }

    [AvaloniaFact]
    public void It_is_dead_with_a_menu_and_a_game_on_the_disc_and_says_why_when_hovered()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);

        // Everything the Windows window wants before it lights this: a game
        // on the list and a menu on the disc.
        screen.Add(AGame());
        Dispatcher.UIThread.RunJobs();

        Assert.True(screen.Menu);
        Assert.True(screen.CanPreview);

        Button preview = window.Found<Button>("PreviewTheMenu");

        Assert.True(preview.IsVisible);
        Assert.False(preview.IsEnabled);
        Assert.Equal(
            WhatWasDrawn.Of("Dead", Application.Current!.ActualThemeVariant),
            preview.Part<TextBlock>("Words").Foreground.AsColour());

        // A dead button says nothing on hover unless it is told to, and the
        // reason is the only thing this one has to say.
        Assert.True(ToolTip.GetShowOnDisabled(preview));

        string why = (string)ToolTip.GetTip(preview)!;

        Assert.Contains("The menu is a Windows page", why, StringComparison.Ordinal);
        Assert.Contains("nothing on Linux can show one", why, StringComparison.Ordinal);

        // And that the disc is none the worse for it.
        Assert.Contains("It goes on the disc all the same", why, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Pressing_it_does_nothing_at_all()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);

        screen.Add(AGame());
        Dispatcher.UIThread.RunJobs();

        Button preview = window.Found<Button>("PreviewTheMenu");
        Point middle = preview.MiddleIn(window);

        window.MouseMove(middle);
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.OwnedWindows);
        Assert.True(screen.NothingIsRunning);
    }

    private GameFolder AGame()
    {
        string download = Path.Combine(_room, "Fixture Game");
        Directory.CreateDirectory(download);

        File.Copy(
            Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"),
            Path.Combine(download, "setup_fixture_game_1.2.3_(64bit)_(12345).exe"));

        return Assert.IsType<Outcome<GameFolder>.Ok>(
            new GameFolders(new WindowsFileSystem(), TheLinuxMachine.TheReaderOfFolders()).At(download)).Value;
    }
}
