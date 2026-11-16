using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using DiscWright.Core;
using DiscWright.LinuxApp;
using DiscWright.Platform;
using DiscWright.Tests;
using DiscWright.ViewModels;

using Path = System.IO.Path;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The list of what goes on the disc: a game and what is under it, a tick on
/// every row, every size in one column, and the row's own way off the list.
/// </summary>
/// <remarks>
/// <para>
/// The games are real folders on a real disk, recognised by the reader the
/// app uses, and the rows are the screen's own. What is asked of the window
/// is what somebody would see and do: where the sizes end, what a tick
/// pressed does, whether the cross is there before the pointer is.
/// </para>
/// <para>
/// The Windows app holds the row's widths to its markup by adding up the
/// numbers written in the file. Here the drawn window is measured.
/// </para>
/// </remarks>
public sealed class TheListOnLinux : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";
    private const string ItsPart = "setup_fixture_game_1.2.3_(64bit)_(12345)-1.bin";

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-list-" + Guid.NewGuid().ToString("N"));

    public TheListOnLinux() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    // -------------------------------------------------------------- when empty

    [AvaloniaFact]
    public void An_empty_list_says_what_to_do_and_adds_nothing_up()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        TextBlock nothing = window.Found<TextBlock>("NothingYet");

        Assert.True(nothing.CanBeSeen());
        Assert.Equal(
            "Nothing yet. Add the folder a game was downloaded into, or the folder it is installed in. "
                + "Dropping one here works too, the installer's own exe included.",
            nothing.Text);

        // Said, because it is so: the panel the list sits in takes drops,
        // and the list in it does because the panel does. The sentence was
        // once here without either.
        Assert.True(Avalonia.Input.DragDrop.GetAllowDrop((Control)window.Found<TreeView>("Games").Parent!.Parent!));
        Assert.True(Avalonia.Input.DragDrop.GetAllowDrop(window.Found<TreeView>("Games")));

        Assert.Empty(TheRows(window));
        Assert.False(window.Found<Border>("TheTotals").IsVisible);
        Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(), each => each.Classes.Contains("Step") && each.Text == "WHAT GOES ON IT");
    }

    [AvaloniaFact]
    public void The_words_about_an_empty_list_go_the_moment_there_is_a_game()
    {
        (MainWindow window, _) = WithAGame();

        Assert.False(window.Found<TextBlock>("NothingYet").IsVisible);
        Assert.True(window.Found<Border>("TheTotals").CanBeSeen());
    }

    // ------------------------------------------------------------------ a game

    [AvaloniaFact]
    public void A_game_is_a_row_with_its_files_under_it_each_answering_to_its_name()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = Assert.Single(screen.WhatGoesOnIt);

        Assert.Equal("Fixture Game: Directors Cut", game.Name);
        Assert.Equal(
            new[] { Installer, ItsPart }.Order(StringComparer.Ordinal),
            game.Inside.Select(each => each.Name).Order(StringComparer.Ordinal));

        // A game is open, so everything in it is on the window.
        TreeViewItem[] rows = TheRows(window);

        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.True(row.CanBeSeen()));

        // Each row answers to the name of what is in it, and not to whatever
        // the thing in it prints as.
        Assert.Equal(
            new[] { "Fixture Game: Directors Cut", Installer, ItsPart }.Order(StringComparer.Ordinal),
            rows.Select(AutomationProperties.GetName).Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public void Every_size_ends_in_one_column_however_far_in_its_row_is()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = screen.WhatGoesOnIt[0];
        AThingOnTheDisc file = game.Inside[0];

        double ofTheGame = WhereItsSizeEnds(RowOf(window, game), window);
        double ofItsFile = WhereItsSizeEnds(RowOf(window, file), window);

        // The whole reason the name is given a width: "4.37 GB" and
        // "817 bytes" end under each other and the column can be read down.
        Assert.Equal(ofTheGame, ofItsFile);

        // The file's name starts nineteen further in than the game's and is
        // nineteen narrower, which is how the two ends meet.
        TextBlock nameOfTheGame = NameOn(RowOf(window, game));
        TextBlock nameOfItsFile = NameOn(RowOf(window, file));

        Assert.Equal(19, nameOfItsFile.TranslatePoint(default, window)!.Value.X - nameOfTheGame.TranslatePoint(default, window)!.Value.X);
        Assert.Equal(19, nameOfTheGame.Bounds.Width - nameOfItsFile.Bounds.Width);
        Assert.Equal(game.HowWideTheNameIs, nameOfTheGame.Bounds.Width);

        // The column itself: seventy six wide, with the figures up against
        // its right edge. Narrower, "999.9 MB" is cut. Lined up on the left,
        // the figures start together and end nowhere near each other.
        TextBlock size = SizeOn(RowOf(window, file));

        Assert.Equal(76, size.Bounds.Width);
        Assert.Equal(Avalonia.Media.TextAlignment.Right, size.TextAlignment);
    }

    [AvaloniaFact]
    public void A_row_stays_inside_the_panel_whatever_it_is_called()
    {
        // An installer with a name far longer than the panel is wide, and a
        // part beside it with a longer one still.
        const string longInstaller =
            "setup_a_very_long_game_name_that_goes_on_and_on_and_on_well_past_the_edge_of_any_panel_1.2.3_(64bit)_(12345).exe";

        string download = Path.Combine(_room, "downloads", "A Game With A Long Name");
        Directory.CreateDirectory(download);

        File.Copy(
            Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"),
            Path.Combine(download, longInstaller));
        File.WriteAllBytes(Path.Combine(download, longInstaller.Replace(".exe", "-1.bin", StringComparison.Ordinal)), new byte[10]);

        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);
        screen.Add(TheGameIn(download));
        Dispatcher.UIThread.RunJobs();

        TreeView tree = window.Found<TreeView>("Games");
        double edgeOfTheList = tree.TranslatePoint(new Point(tree.Bounds.Width, 0), window)!.Value.X;

        foreach (TreeViewItem row in TheRows(window))
        {
            Button cross = row.GetVisualDescendants().OfType<Button>().First(each => each.Classes.Contains("OffTheRow"));
            double whereTheRowEnds = cross.TranslatePoint(new Point(cross.Bounds.Width, 0), window)!.Value.X;

            Assert.True(
                whereTheRowEnds <= edgeOfTheList,
                $"The row for {AutomationProperties.GetName(row)} ends at {whereTheRowEnds} and the list at {edgeOfTheList}.");
        }

        // The long name is cut with dots, and whole when hovered.
        AThingOnTheDisc longOne = screen.WhatGoesOnIt[0].Inside.Single(each => each.Name == longInstaller);
        TextBlock name = NameOn(RowOf(window, longOne));

        Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, name.TextTrimming);
        Assert.Equal(longOne.InFull, ToolTip.GetTip(name));
    }

    // ---------------------------------------------------------------- the tick

    [AvaloniaFact]
    public void Unticking_a_file_leaves_it_off_and_its_game_neither_on_nor_off()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = screen.WhatGoesOnIt[0];
        AThingOnTheDisc part = game.Inside.Single(each => each.Name == ItsPart);

        Assert.True(part.IsOn);
        Assert.True(game.IsOn);

        // Only a row with things under it can be some but not all. A file is
        // on the disc or off it, and its tick must not have a third state to
        // be pressed into.
        Assert.True(TickOn(RowOf(window, game)).IsThreeState);
        Assert.False(TickOn(RowOf(window, part)).IsThreeState);

        Press(window, TickOn(RowOf(window, part)));

        Assert.False(part.IsOn);

        // Some of the game but not all of it: the small square, not a tick
        // and not an empty box, rather than lying about what will be written.
        Assert.Null(game.IsOn);

        CheckBox ofTheGame = TickOn(RowOf(window, game));

        Assert.True(ofTheGame.Part<Rectangle>("Partly").IsVisible);
        Assert.False(ofTheGame.Part<Avalonia.Controls.Shapes.Path>("Tick").IsVisible);
    }

    [AvaloniaFact]
    public void The_line_under_the_list_adds_up_what_is_ticked_and_counts_what_is_not()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        TextBlock total = window.Found<TextBlock>("WhatIsOnIt");
        TextBlock leftOff = window.Found<TextBlock>("WhatIsLeftOff");
        TextBlock between = window.Found<TextBlock>("BetweenTheTwo");
        Button remedy = window.Found<Button>("TakeOffWhatIsLeftOff");

        Assert.Equal(screen.WhatIsOnIt, total.Text);
        Assert.NotEqual(string.Empty, total.Text);

        // Nothing unticked, so nothing said about it, no dot and no link.
        Assert.False(leftOff.IsVisible);
        Assert.False(between.IsVisible);
        Assert.False(remedy.IsVisible);

        string before = total.Text!;
        AThingOnTheDisc part = screen.WhatGoesOnIt[0].Inside.Single(each => each.Name == ItsPart);

        Press(window, TickOn(RowOf(window, part)));

        // The total follows the ticks.
        Assert.NotEqual(before, total.Text);
        Assert.Equal(screen.WhatIsOnIt, total.Text);

        Assert.True(leftOff.CanBeSeen());
        Assert.Equal(screen.WhatIsLeftOff, leftOff.Text);
        Assert.True(between.CanBeSeen());

        Assert.True(remedy.CanBeSeen());
        Assert.Equal(screen.WhatTakingTheLeftOffOnesSays, remedy.Content);
        Assert.Same(Application.Current!.FindResource("AsALink"), remedy.Theme);
    }

    [AvaloniaFact]
    public void The_link_beside_the_count_takes_the_unticked_rows_off_the_list()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc part = screen.WhatGoesOnIt[0].Inside.Single(each => each.Name == ItsPart);
        Press(window, TickOn(RowOf(window, part)));

        Press(window, window.Found<Button>("TakeOffWhatIsLeftOff"));

        Assert.Equal([Installer], screen.WhatGoesOnIt[0].Inside.Select(each => each.Name));
        Assert.Equal(2, TheRows(window).Length);
        Assert.False(window.Found<Button>("TakeOffWhatIsLeftOff").IsVisible);
    }

    // --------------------------------------------------------------- the cross

    [AvaloniaFact]
    public void The_cross_is_unseen_and_cannot_be_pressed_until_its_own_row_is_under_the_pointer()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = screen.WhatGoesOnIt[0];
        AThingOnTheDisc part = game.Inside.Single(each => each.Name == ItsPart);

        Button onTheGame = CrossOn(RowOf(window, game));
        Button onThePart = CrossOn(RowOf(window, part));

        window.MouseMove(new Point(1, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, onTheGame.Opacity);
        Assert.Equal(0, onThePart.Opacity);
        Assert.False(onThePart.IsHitTestVisible);

        // Unseen, not gone: the columns stand still.
        Assert.Equal(18, onThePart.Bounds.Width);

        window.MouseMove(NameOn(RowOf(window, part)).MiddleIn(window));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, onThePart.Opacity);
        Assert.True(onThePart.IsHitTestVisible);

        // And only that row's. Hovering one file must not light the cross on
        // the game above it, which is over its files too.
        Assert.Equal(0, onTheGame.Opacity);
    }

    [AvaloniaFact]
    public void The_cross_takes_its_own_row_off_the_list_and_no_other()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc part = screen.WhatGoesOnIt[0].Inside.Single(each => each.Name == ItsPart);
        TreeViewItem row = RowOf(window, part);

        // The way a person does it: onto the row, then onto the cross.
        window.MouseMove(NameOn(row).MiddleIn(window));
        Dispatcher.UIThread.RunJobs();

        Press(window, CrossOn(row));

        Assert.Equal([Installer], screen.WhatGoesOnIt[0].Inside.Select(each => each.Name));
        Assert.Equal("Take it off the list", ToolTip.GetTip(CrossOn(RowOf(window, screen.WhatGoesOnIt[0]))));
    }

    // ------------------------------------------------------------- picking one

    [AvaloniaFact]
    public void Pressing_a_row_picks_it_and_Delete_takes_it_off()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc part = screen.WhatGoesOnIt[0].Inside.Single(each => each.Name == ItsPart);

        Assert.Null(screen.WhatIsSelected);

        Press(window, NameOn(RowOf(window, part)));

        Assert.Same(part, screen.WhatIsSelected);
        Assert.True(RowOf(window, part).IsSelected);

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([Installer], screen.WhatGoesOnIt[0].Inside.Select(each => each.Name));
    }

    // ---------------------------------------------------------------- renaming

    [AvaloniaFact]
    public void A_double_press_on_a_game_opens_its_name_for_typing_with_all_of_it_selected()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = screen.WhatGoesOnIt[0];
        TreeViewItem row = RowOf(window, game);

        DoublePress(window, NameOn(row));

        Assert.True(game.IsBeingRenamed);

        TextBox box = RenameBoxOn(RowOf(window, game));

        Assert.True(box.CanBeSeen());
        Assert.False(NameOn(RowOf(window, game)).IsVisible);

        // The name as it stands, all of it selected because the commonest
        // rename replaces all of it, in the room the name had.
        Assert.Equal("Fixture Game: Directors Cut", box.Text);
        Assert.True(box.IsFocused);
        Assert.Equal(box.Text!.Length, Math.Abs(box.SelectionEnd - box.SelectionStart));
        Assert.Equal(game.HowWideTheNameIs, box.Bounds.Width);
    }

    [AvaloniaFact]
    public void Enter_keeps_the_new_name()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = screen.WhatGoesOnIt[0];
        DoublePress(window, NameOn(RowOf(window, game)));

        window.KeyTextInput("Fixture Game");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        AThingOnTheDisc renamed = screen.WhatGoesOnIt[0];

        Assert.Equal("Fixture Game", renamed.Name);
        Assert.False(renamed.IsBeingRenamed);
        Assert.Equal("Fixture Game", NameOn(RowOf(window, renamed)).Text);
        Assert.True(NameOn(RowOf(window, renamed)).CanBeSeen());
    }

    [AvaloniaFact]
    public void Escape_leaves_the_name_as_it_was_and_nothing_stale_for_next_time()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = screen.WhatGoesOnIt[0];
        DoublePress(window, NameOn(RowOf(window, game)));

        window.KeyTextInput("Something else entirely");
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Fixture Game: Directors Cut", screen.WhatGoesOnIt[0].Name);
        Assert.False(screen.WhatGoesOnIt[0].IsBeingRenamed);

        // Opened again, the box holds the name and not what was abandoned.
        DoublePress(window, NameOn(RowOf(window, screen.WhatGoesOnIt[0])));

        Assert.Equal("Fixture Game: Directors Cut", RenameBoxOn(RowOf(window, screen.WhatGoesOnIt[0])).Text);
    }

    [AvaloniaFact]
    public void Delete_inside_the_box_is_an_editing_key_and_takes_no_row_off()
    {
        (MainWindow window, MakeADisc screen) = WithAGame();

        AThingOnTheDisc game = screen.WhatGoesOnIt[0];
        DoublePress(window, NameOn(RowOf(window, game)));

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        // The whole name was selected, so Delete emptied the box. The game
        // is still on the list.
        Assert.Single(screen.WhatGoesOnIt);
        Assert.Equal(string.Empty, RenameBoxOn(RowOf(window, screen.WhatGoesOnIt[0])).Text ?? string.Empty);

        // And again, with nothing left in the box to delete. The box has no
        // use for the key now and lets it go, and it reaches the list, where
        // Delete means take the picked row off.
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(screen.WhatGoesOnIt);
    }

    // ------------------------------------------------------ what is worth knowing

    [AvaloniaFact]
    public void Nothing_is_said_under_the_list_while_there_is_nothing_to_say()
    {
        (MainWindow window, _) = WithAGame();

        foreach (string id in new[]
        {
            "WhatTheDownloadsSay", "APatchWithNothingToPatch", "WhoWearsTheSameName",
            "WhatDidNotComeBack", "WhyThatMatters", "WhatWillNotStartItself",
        })
        {
            Assert.False(window.Found<TextBlock>(id).IsVisible, $"{id} takes up room with nothing in it.");
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Two_games_reading_alike_are_said_so_under_the_list_in_the_ink_of_a_worry(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        WhatWasDrawn.Wearing(look, () =>
        {
            MakeADisc screen = TheWindow.AScreen();
            MainWindow window = TheWindow.Showing(screen);

            // Two downloads whose installers call themselves the same thing.
            screen.Add(TheGameIn(ADownload("First Copy")));
            screen.Add(TheGameIn(ADownload("Second Copy")));
            Dispatcher.UIThread.RunJobs();

            TextBlock said = window.Found<TextBlock>("WhoWearsTheSameName");

            Assert.True(said.CanBeSeen());
            Assert.Equal(screen.WhoWearsTheSameName, said.Text);
            Assert.Contains("Fixture Game: Directors Cut", said.Text, StringComparison.Ordinal);

            // A worry and not a refusal, and the two must not look the same.
            Assert.Equal(WhatWasDrawn.Of("WorryInk", look), said.Foreground.AsColour());

            // Under the panel the list is in, not inside it.
            Border totals = window.Found<Border>("TheTotals");
            Assert.True(
                said.TranslatePoint(default, window)!.Value.Y
                    >= totals.TranslatePoint(new Point(0, totals.Bounds.Height), window)!.Value.Y);

            Keep(window, $"the-list-{which.ToLowerInvariant()}.png");
        });
    }

    // ------------------------------------------------------------------ helpers

    private (MainWindow Window, MakeADisc Screen) WithAGame()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);

        screen.Add(TheGameIn(ADownload("Fixture Game")));
        Dispatcher.UIThread.RunJobs();

        return (window, screen);
    }

    /// <summary>A download as GOG ships one: an installer that says what it is, and a part beside it.</summary>
    private string ADownload(string folder)
    {
        string download = Path.Combine(_room, "downloads", folder);
        Directory.CreateDirectory(download);

        File.Copy(
            Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"),
            Path.Combine(download, Installer));
        File.WriteAllBytes(Path.Combine(download, ItsPart), new byte[5000]);

        return download;
    }

    private static GameFolder TheGameIn(string download) =>
        Assert.IsType<Outcome<GameFolder>.Ok>(
            new GameFolders(new WindowsFileSystem(), TheLinuxMachine.TheReaderOfFolders()).At(download)).Value;

    /// <summary>Every row of the list that is on the window, games and what is under them.</summary>
    private static TreeViewItem[] TheRows(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();

        return [.. window.Found<TreeView>("Games").GetVisualDescendants().OfType<TreeViewItem>()];
    }

    private static TreeViewItem RowOf(MainWindow window, AThingOnTheDisc thing) =>
        TheRows(window).Single(row => ReferenceEquals(row.DataContext, thing));

    /// <summary>What is on a row's own line, and not on the rows under it.</summary>
    private static IEnumerable<T> OnItsOwnLine<T>(TreeViewItem row)
        where T : Control =>
        row.GetVisualDescendants().OfType<T>().Where(each => ReferenceEquals(each.FindAncestorOfType<TreeViewItem>(), row));

    private static TextBlock NameOn(TreeViewItem row) =>
        OnItsOwnLine<TextBlock>(row).First(each => each.Text == ((AThingOnTheDisc)row.DataContext!).Name && ToolTip.GetTip(each) is not null);

    private static CheckBox TickOn(TreeViewItem row) => OnItsOwnLine<CheckBox>(row).Single();

    private static Button CrossOn(TreeViewItem row) =>
        OnItsOwnLine<Button>(row).Single(each => each.Classes.Contains("OffTheRow"));

    private static TextBox RenameBoxOn(TreeViewItem row) =>
        OnItsOwnLine<TextBox>(row).Single(each => AutomationProperties.GetAutomationId(each) == "RenameBox");

    private static TextBlock SizeOn(TreeViewItem row) =>
        OnItsOwnLine<TextBlock>(row).Last(each => each.Text == ((AThingOnTheDisc)row.DataContext!).HowBig);

    private static double WhereItsSizeEnds(TreeViewItem row, Window window)
    {
        TextBlock size = SizeOn(row);

        return size.TranslatePoint(new Point(size.Bounds.Width, 0), window)!.Value.X;
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

    private static void DoublePress(Window window, Control control)
    {
        Dispatcher.UIThread.RunJobs();

        Point middle = control.MiddleIn(window);

        window.MouseMove(middle);
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Keep(Window window, string name)
    {
        string kept = Repository.File("artifacts", "the-look", name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(kept)!);

        window.MouseMove(new Point(1, 1));
        Dispatcher.UIThread.RunJobs();

        using Avalonia.Media.Imaging.WriteableBitmap drawn = window.CaptureRenderedFrame()!;
        drawn.Save(kept, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
