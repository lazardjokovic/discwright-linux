using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using DiscWright.Core;
using DiscWright.LinuxApp;
using DiscWright.Platform;
using DiscWright.Tests;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// A game's own manual and extras, kept apart from its installer: added to
/// the game that is picked, and one turned into the other when it was put
/// in the wrong place.
/// </summary>
/// <remarks>
/// Real files, a real game, and the desktop answered for. What a manual or
/// an extra is and where it goes on the disc are the screen's and are tested
/// there. What is held here is that the window names the game it is adding
/// to, asks for the right thing, and passes it on.
/// </remarks>
public sealed class AGamesOwnOnLinux : IDisposable
{
    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-own-" + Guid.NewGuid().ToString("N"));

    public AGamesOwnOnLinux() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [AvaloniaFact]
    public void Nothing_is_offered_until_a_game_is_picked_and_then_the_game_is_named()
    {
        (MainWindow window, MakeADisc screen, _) = WithAGame();

        WrapPanel offered = window.Found<WrapPanel>("ForThePickedGame");

        Assert.False(offered.IsVisible);

        Pick(window, screen.WhatGoesOnIt[0]);

        // Named, so it is plain which game they go to.
        Assert.True(offered.CanBeSeen());
        Assert.Equal("Fixture Game: Directors Cut:", window.Found<TextBlock>("ThePickedGame").Text);
        Assert.Equal("Add a manual", window.Found<Button>("AddAManual").Content);
        Assert.Equal("Add extras", window.Found<Button>("AddExtras").Content);
        Assert.Same(Application.Current!.FindResource("AsALink"), window.Found<Button>("AddAManual").Theme);
    }

    [AvaloniaFact]
    public void A_file_under_a_game_picks_the_game_it_is_under()
    {
        (MainWindow window, MakeADisc screen, _) = WithAGame();

        Pick(window, screen.WhatGoesOnIt[0].Inside[0]);

        Assert.Equal("Fixture Game: Directors Cut:", window.Found<TextBlock>("ThePickedGame").Text);
    }

    [AvaloniaFact]
    public void Add_a_manual_asks_for_manuals_by_the_name_of_the_game_and_puts_them_under_it()
    {
        (MainWindow window, MakeADisc screen, ADesktopThatAnswers desktop) = WithAGame();

        string manual = AFile("Fixture Game Manual.pdf");
        string quickStart = AFile("Quick Start.pdf");
        desktop.WillPickFiles = [manual, quickStart];

        Pick(window, screen.WhatGoesOnIt[0]);
        Press(window, window.Found<Button>("AddAManual"));

        (string title, IReadOnlyList<AKindOfFile> kinds) = Assert.Single(desktop.AskedFor);

        Assert.Equal("The manual of Fixture Game: Directors Cut", title);
        Assert.Equal("A manual", kinds[0].Name);
        Assert.Contains("*.pdf", kinds[0].Patterns);
        Assert.Equal("Anything", kinds[1].Name);

        // Both, as the game's own, and on the window as rows under it.
        Assert.Equal(2, screen.Games[0].OwnOrNone.Count(own => own.Kind == GameExtraKind.Manual));

        string[] under = [.. screen.WhatGoesOnIt[0].EveryThing().Select(each => each.Name)];

        Assert.Contains("Fixture Game Manual.pdf", under);
        Assert.Contains("Quick Start.pdf", under);
    }

    [AvaloniaFact]
    public void Add_extras_asks_for_one_folder_and_puts_it_under_the_game()
    {
        (MainWindow window, MakeADisc screen, ADesktopThatAnswers desktop) = WithAGame();

        string artbook = Path.Combine(_room, "Artbook");
        Directory.CreateDirectory(artbook);
        File.WriteAllBytes(Path.Combine(artbook, "cover.png"), new byte[900]);
        desktop.WillPickFolders = [artbook];

        Pick(window, screen.WhatGoesOnIt[0]);
        Press(window, window.Found<Button>("AddExtras"));

        Assert.Equal(["An extra of Fixture Game: Directors Cut, one folder at a time"], desktop.AskedForFolders);

        GameExtra extra = Assert.Single(screen.Games[0].OwnOrNone);

        Assert.Equal(GameExtraKind.Extra, extra.Kind);
        Assert.Equal(artbook, extra.Path);
        Assert.Contains(screen.WhatGoesOnIt[0].EveryThing(), each => each.Name == "Artbook");
    }

    [AvaloniaFact]
    public void Picking_nothing_adds_nothing()
    {
        (MainWindow window, MakeADisc screen, _) = WithAGame();

        Pick(window, screen.WhatGoesOnIt[0]);
        Press(window, window.Found<Button>("AddAManual"));
        Press(window, window.Found<Button>("AddExtras"));

        Assert.Empty(screen.Games[0].OwnOrNone);
    }

    [AvaloniaFact]
    public void A_manual_says_what_it_is_as_a_link_and_a_press_makes_it_an_extra_and_another_makes_it_a_manual_again()
    {
        (MainWindow window, MakeADisc screen, ADesktopThatAnswers desktop) = WithAGame();

        desktop.WillPickFiles = [AFile("Fixture Game Manual.pdf")];

        Pick(window, screen.WhatGoesOnIt[0]);
        Press(window, window.Found<Button>("AddAManual"));

        AThingOnTheDisc TheRow() =>
            screen.WhatGoesOnIt[0].EveryThing().Single(each => each.Name == "Fixture Game Manual.pdf");

        Assert.True(TheRow().CanBeEither);

        // A game's own are kept together under the game, in a group that
        // starts shut. Opened, as somebody would have to.
        OpenDownTo(screen.WhatGoesOnIt[0], TheRow());
        Dispatcher.UIThread.RunJobs();

        // Its row and the link on it, found again each time: a press takes
        // the row out of one group and puts it in another, and the other may
        // be shut.
        Button TheLink()
        {
            OpenDownTo(screen.WhatGoesOnIt[0], TheRow());
            Dispatcher.UIThread.RunJobs();

            return TheLinkOn(window, TheRow());
        }

        string asAManual = TheRow().What!;
        Button link = TheLink();

        // What it is, pressable, and what pressing it would do on hover.
        Assert.True(link.CanBeSeen());
        Assert.Equal(asAManual, link.Content);
        Assert.Equal(TheRow().WhatItCouldBe, ToolTip.GetTip(link));

        // Said once. The plain words stand where the link stands, for a row
        // that cannot be anything else, and both at once is one drawn over
        // the other.
        TreeViewItem itsRow = RowOf(window, TheRow());

        Assert.Single(
            itsRow.GetVisualDescendants().OfType<TextBlock>(),
            each => each.Text == asAManual
                && ReferenceEquals(each.FindAncestorOfType<TreeViewItem>(), itsRow)
                && each.CanBeSeen());

        Press(window, link);

        Assert.NotEqual(asAManual, TheRow().What);
        Assert.Equal(GameExtraKind.Extra, Assert.Single(screen.Games[0].OwnOrNone).Kind);
        Assert.Equal(TheRow().What, TheLink().Content);

        Press(window, TheLink());

        Assert.Equal(asAManual, TheRow().What);
        Assert.Equal(GameExtraKind.Manual, Assert.Single(screen.Games[0].OwnOrNone).Kind);
    }

    [AvaloniaFact]
    public void An_installer_says_what_it_is_in_plain_words_with_nothing_to_press()
    {
        (MainWindow window, MakeADisc screen, _) = WithAGame();

        AThingOnTheDisc installer = screen.WhatGoesOnIt[0].Inside.Single(each => each.What is not null);

        Assert.False(installer.CanBeEither);

        TreeViewItem row = RowOf(window, installer);

        // No link on it that can be seen: an installer cannot be anything else.
        Assert.DoesNotContain(
            row.GetVisualDescendants().OfType<Button>(),
            each => Avalonia.Automation.AutomationProperties.GetAutomationId(each) == "SwapWhatItIs"
                && ReferenceEquals(each.FindAncestorOfType<TreeViewItem>(), row)
                && each.IsVisible);

        Assert.Contains(
            row.GetVisualDescendants().OfType<TextBlock>(),
            each => each.Text == installer.What && each.CanBeSeen());
    }

    // ------------------------------------------------------------------ helpers

    private (MainWindow Window, MakeADisc Screen, ADesktopThatAnswers Desktop) WithAGame()
    {
        ADesktopThatAnswers desktop = new();
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen, desktop, new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows));

        string download = Path.Combine(_room, "downloads", "Fixture Game");
        Directory.CreateDirectory(download);

        File.Copy(
            Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"),
            Path.Combine(download, "setup_fixture_game_1.2.3_(64bit)_(12345).exe"));
        File.WriteAllBytes(Path.Combine(download, "setup_fixture_game_1.2.3_(64bit)_(12345)-1.bin"), new byte[5000]);

        screen.Add(Assert.IsType<Outcome<GameFolder>.Ok>(
            new GameFolders(new WindowsFileSystem(), TheLinuxMachine.TheReaderOfFolders()).At(download)).Value);
        Dispatcher.UIThread.RunJobs();

        return (window, screen, desktop);
    }

    /// <summary>Opens every row between a game and one of the things under it.</summary>
    private static bool OpenDownTo(AThingOnTheDisc from, AThingOnTheDisc thing)
    {
        if (ReferenceEquals(from, thing))
        {
            return true;
        }

        foreach (AThingOnTheDisc under in from.Inside)
        {
            if (OpenDownTo(under, thing))
            {
                from.IsOpened = true;
                return true;
            }
        }

        return false;
    }

    private string AFile(string name)
    {
        string path = Path.Combine(_room, name);
        File.WriteAllBytes(path, new byte[700]);

        return path;
    }

    private static TreeViewItem RowOf(MainWindow window, AThingOnTheDisc thing)
    {
        Dispatcher.UIThread.RunJobs();

        return window.Found<TreeView>("Games").GetVisualDescendants().OfType<TreeViewItem>()
            .Single(row => ReferenceEquals(row.DataContext, thing));
    }

    private static Button TheLinkOn(MainWindow window, AThingOnTheDisc thing)
    {
        TreeViewItem row = RowOf(window, thing);

        return row.GetVisualDescendants().OfType<Button>().Single(each =>
            Avalonia.Automation.AutomationProperties.GetAutomationId(each) == "SwapWhatItIs"
            && ReferenceEquals(each.FindAncestorOfType<TreeViewItem>(), row));
    }

    /// <summary>Picks a row the way somebody does, by pressing its name.</summary>
    private static void Pick(MainWindow window, AThingOnTheDisc thing)
    {
        TreeViewItem row = RowOf(window, thing);

        TextBlock name = row.GetVisualDescendants().OfType<TextBlock>().First(each =>
            each.Text == thing.Name && ReferenceEquals(each.FindAncestorOfType<TreeViewItem>(), row));

        Press(window, name);
    }

    private static void Press(Window window, Control control)
    {
        Dispatcher.UIThread.RunJobs();

        Point middle = control.MiddleIn(window);

        window.MouseMove(middle);
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
