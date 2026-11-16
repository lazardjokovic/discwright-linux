using Avalonia.Controls;
using Avalonia.Threading;

using DiscWright.Core;
using DiscWright.Platform;
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
    public static MainWindow Showing(MakeADisc screen) =>
        Showing(screen, new ADesktopThatAnswers(), new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows));

    /// <summary>The window over a screen, a desktop and a look the test keeps hold of.</summary>
    public static MainWindow Showing(
        MakeADisc screen, IAskTheDesktop desktop, ChoosingTheLook look, TheLogOnDisk? log = null)
    {
        MainWindow window = new(screen, TheLinuxMachine.TheReaderOfFolders(), new WindowsFileSystem(), look, desktop)
        {
            TheLog = log,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>The screen over the machine the app composes, logging nowhere.</summary>
    public static MakeADisc AScreen() => new(TheLinuxMachine.Composed(new NothingIsLogged()));
}

/// <summary>
/// The desktop, answered by a test: the file it will say somebody picked,
/// whether anything it is asked to open will open, and a record of both.
/// </summary>
internal sealed class ADesktopThatAnswers : IAskTheDesktop
{
    /// <summary>The file somebody will be said to have picked. Nothing, if they would rather not.</summary>
    public string? WillPick { get; set; }

    /// <summary>The folders somebody will be said to have picked. None, if they would rather not.</summary>
    public IReadOnlyList<string> WillPickFolders { get; set; } = [];

    /// <summary>Every folder dialog asked for, by its title.</summary>
    public List<string> AskedForFolders { get; } = [];

    /// <summary>Whether anything on this desktop opens anything.</summary>
    public bool OpensThings { get; set; } = true;

    /// <summary>Every file dialog asked for, by its title and the kinds of file it offered.</summary>
    public List<(string Title, IReadOnlyList<AKindOfFile> Kinds)> AskedFor { get; } = [];

    /// <summary>Everything it was asked to open.</summary>
    public List<string> Opened { get; } = [];

    public Task<string?> ForAFile(Window owner, string title, IReadOnlyList<AKindOfFile> kinds)
    {
        AskedFor.Add((title, kinds));

        return Task.FromResult(WillPick);
    }

    public Task<IReadOnlyList<string>> ForFolders(Window owner, string title)
    {
        AskedForFolders.Add(title);

        return Task.FromResult(WillPickFolders);
    }

    public bool Open(string what)
    {
        Opened.Add(what);

        return OpensThings;
    }
}
