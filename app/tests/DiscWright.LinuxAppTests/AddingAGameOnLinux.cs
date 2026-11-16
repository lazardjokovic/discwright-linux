using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using DiscWright.Core;
using DiscWright.LinuxApp;
using DiscWright.Tests;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// Add a game, from the press to the game being on the list, with every
/// question it can ask on the way answered on the window that asks it.
/// </summary>
/// <remarks>
/// <para>
/// The folders are real ones on a real disk. Only the desktop is answered
/// for: which folders somebody picks.
/// </para>
/// <para>
/// The walk over the folders runs on a thread of its own and comes back to
/// the window's for each question, so a test here presses, then lets the
/// window's thread run until what it is waiting for has happened. Nothing is
/// slept through: each wait is for a thing, and says what it was waiting for
/// when the thing does not come.
/// </para>
/// </remarks>
public sealed class AddingAGameOnLinux : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-adding-" + Guid.NewGuid().ToString("N"));

    public AddingAGameOnLinux() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    // ------------------------------------------------------------- the press

    [AvaloniaFact]
    public void Add_a_game_sits_above_the_list_at_its_left_edge()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        Button add = window.Found<Button>("AddAGame");
        TreeView list = window.Found<TreeView>("Games");

        Assert.Equal("Add a game", add.Content);
        Assert.Same(Application.Current!.FindResource("Ordinary"), add.Theme);
        Assert.True(add.IsEnabled);

        // Above the list and not inside it, where the tree would cover it
        // and no pointer could reach it.
        Assert.True(add.TranslatePoint(new Point(0, add.Bounds.Height), window)!.Value.Y <= list.TranslatePoint(default, window)!.Value.Y);
        Assert.Equal(28, add.TranslatePoint(default, window)!.Value.X);
    }

    [AvaloniaFact]
    public void It_asks_the_desktop_which_folder_and_does_nothing_when_none_is_picked()
    {
        ADesktopThatAnswers desktop = new();
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        Assert.Equal(["Which folder is the game in?"], desktop.AskedForFolders);
        Assert.Empty(screen.Games);
        Assert.Empty(window.OwnedWindows);
    }

    // --------------------------------------------------------- a plain download

    [AvaloniaFact]
    public void A_download_with_nothing_beside_the_game_goes_on_with_no_question_asked()
    {
        ADesktopThatAnswers desktop = new() { WillPickFolders = [ADownload("Fixture Game")] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));
        Until(() => screen.Games.Count == 1, "the game to be on the list");

        Assert.Empty(window.OwnedWindows);
        Assert.Equal("Fixture Game: Directors Cut", screen.Games[0].Name);

        // And on the window, as a row.
        Assert.False(window.Found<TextBlock>("NothingYet").IsVisible);
        Assert.Equal("Fixture Game: Directors Cut", screen.WhatGoesOnIt[0].Name);
    }

    // ------------------------------------------------------ more in the folder

    [AvaloniaFact]
    public void A_download_with_more_in_it_asks_what_else_and_takes_what_is_ticked()
    {
        ADesktopThatAnswers desktop = new() { WillPickFolders = [ADownloadWithAManual("Fixture Game")] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        WhatElseIsInThere question = TheOneThatOpens<WhatElseIsInThere>(window);

        Assert.Equal("There is one more file in that folder", question.Found<TextBlock>("Headline").Text);

        // Not on the list while the question stands.
        Assert.Empty(screen.Games);

        Press(question, question.Found<Button>("TakeThem"));
        Until(() => screen.Games.Count == 1, "the game to be on the list");

        Assert.Equal(GameExtraKind.Manual, Assert.Single(screen.Games[0].OwnOrNone).Kind);
    }

    [AvaloniaFact]
    public void Something_that_installs_onto_the_game_goes_on_with_it_as_its_add_on()
    {
        string download = ADownload("Fixture Game");
        File.WriteAllBytes(Path.Combine(download, "soundtrack_fixture_game.exe"), new byte[2000]);

        ADesktopThatAnswers desktop = new() { WillPickFolders = [download] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        WhatElseIsInThere question = TheOneThatOpens<WhatElseIsInThere>(window);
        Press(question, question.Found<Button>("TakeThem"));

        Until(() => screen.Games.Count == 1, "the game to be on the list");

        // One game, with the thing that installs onto it under it: attached
        // to its game, and not a game of its own with nothing to install onto.
        AThingOnTheDisc onTheList = Assert.Single(screen.WhatGoesOnIt);

        Assert.Equal("Fixture Game: Directors Cut", onTheList.Name);

        Assert.Contains(onTheList.EveryThing(), each => each.Name.Contains("soundtrack", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void Shutting_the_question_about_what_else_leaves_the_game_off_the_list()
    {
        ADesktopThatAnswers desktop = new() { WillPickFolders = [ADownloadWithAManual("Fixture Game")] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Button add = window.Found<Button>("AddAGame");

        Press(window, add);
        TheOneThatOpens<WhatElseIsInThere>(window).Close();
        Until(() => window.OwnedWindows.Count == 0, "the question to go");

        // Not an answer, so nothing was done. The folder can be pointed at
        // again, and is asked about again.
        Assert.Empty(screen.Games);

        Until(() => TryPress(window, add) && window.OwnedWindows.Count == 1, "the question to be asked again");
        TheOneThatOpens<WhatElseIsInThere>(window).Close();
    }

    [AvaloniaFact]
    public void Several_folders_are_asked_about_in_turn_and_each_game_is_on_the_list_before_the_next_is_asked_about()
    {
        ADesktopThatAnswers desktop = new()
        {
            WillPickFolders = [ADownloadWithAManual("First Game"), ADownloadWithAManual("Second Game")],
        };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        WhatElseIsInThere first = TheOneThatOpens<WhatElseIsInThere>(window);

        // One more follows, and the window offers to answer for it too.
        Assert.Equal(
            "Answer the same way for the other game being added",
            Assert.Single(first.Found<CheckBox>("TheSameForTheRest").GetVisualDescendants().OfType<TextBlock>()).Text);

        Press(first, first.Found<Button>("TakeThem"));

        // The second question, and by then the first game is on the list.
        Until(() => window.OwnedWindows.OfType<WhatElseIsInThere>().Any(each => !ReferenceEquals(each, first) && each.IsVisible), "the second question");

        Assert.Single(screen.Games);

        WhatElseIsInThere second = window.OwnedWindows.OfType<WhatElseIsInThere>().Single(each => each.IsVisible);

        // Nothing follows the second.
        Assert.False(second.Found<CheckBox>("TheSameForTheRest").IsVisible);

        Press(second, second.Found<Button>("NoneOfThem"));
        Until(() => screen.Games.Count == 2, "both games to be on the list");

        Assert.Single(screen.Games[0].OwnOrNone);
        Assert.Empty(screen.Games[1].OwnOrNone);
    }

    [AvaloniaFact]
    public void Answering_the_same_way_for_the_rest_asks_once()
    {
        ADesktopThatAnswers desktop = new()
        {
            WillPickFolders = [ADownloadWithAManual("First Game"), ADownloadWithAManual("Second Game")],
        };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        WhatElseIsInThere first = TheOneThatOpens<WhatElseIsInThere>(window);

        Press(first, first.Found<CheckBox>("TheSameForTheRest"));
        Press(first, first.Found<Button>("TakeThem"));

        Until(() => screen.Games.Count == 2, "both games to be on the list");

        // Asked once, answered twice: a compilation of nine was once nine
        // of these.
        Assert.DoesNotContain(window.OwnedWindows, each => each.IsVisible);
        Assert.All(screen.Games, game => Assert.Single(game.OwnOrNone));
    }

    // ---------------------------------------------------- a folder of downloads

    [AvaloniaTheory]
    [InlineData("separately", 2)]
    [InlineData("as one", 1)]
    public void A_folder_that_holds_games_asks_whether_to_add_them_separately(string how, int entries)
    {
        string shelf = Path.Combine(_room, "downloads", "GOG Games");
        ADownload(Path.Combine("GOG Games", "First Game"));
        ADownload(Path.Combine("GOG Games", "Second Game"));

        ADesktopThatAnswers desktop = new() { WillPickFolders = [shelf] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        WorthAsking question = TheOneThatOpens<WorthAsking>(window);

        Assert.Equal("That folder holds 2 games", question.Found<TextBlock>("Headline").Text);
        Assert.StartsWith(
            "GOG Games is not a game itself, it is where these are kept.",
            question.Found<TextBlock>("Because").Text,
            StringComparison.Ordinal);
        Assert.Equal("Add all 2 separately", question.Found<Button>("TheLikelyOne").Content);
        Assert.Equal("Add it as one game", question.Found<Button>("TheOtherWay").Content);

        Press(question, question.Found<Button>(how == "separately" ? "TheLikelyOne" : "TheOtherWay"));

        if (how == "as one")
        {
            // As one game it is a folder of files, and two of the files in
            // it could be what starts it: the two installers. So that is
            // asked next, as it would be of any folder of game files.
            PickOne which = TheOneThatOpens<PickOne>(window);

            Assert.Equal("Which file starts GOG Games?", which.Found<TextBlock>("Headline").Text);

            Press(which, which.Found<Button>("NoneOfThem"));
        }

        Until(() => screen.Games.Count == entries, $"{entries} on the list");

        Assert.Equal(entries, screen.WhatGoesOnIt.Count);
    }

    // ----------------------------------------------------------- not a game

    [AvaloniaFact]
    public void A_folder_with_nothing_in_it_is_refused_in_words_and_nothing_goes_on()
    {
        string empty = Path.Combine(_room, "downloads", "Nothing Here");
        Directory.CreateDirectory(empty);

        ADesktopThatAnswers desktop = new() { WillPickFolders = [empty] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        SayingSo box = TheOneThatOpens<SayingSo>(window);

        // The refusal the shared code gives for that folder, word for word.
        Refusal refusal = Assert.IsType<Outcome<GameFolder>.No>(
            new GameFolders(new DiscWright.Platform.WindowsFileSystem(), TheLinuxMachine.TheReaderOfFolders()).At(empty)).Refusal;

        Assert.Equal(refusal.Headline, box.Found<TextBlock>("Headline").Text);
        Assert.Equal(refusal.Because, box.Found<SelectableTextBlock>("Because").Text);

        Press(box, box.Found<Button>("Understood"));
        Until(() => window.OwnedWindows.Count == 0, "the refusal to go");

        Assert.Empty(screen.Games);
    }

    [AvaloniaFact]
    public void An_add_on_by_itself_is_questioned_and_left_off_when_that_is_the_answer()
    {
        string download = Path.Combine(_room, "downloads", "Fixture Game Soundtrack");
        Directory.CreateDirectory(download);
        File.Copy(TheFixture(), Path.Combine(download, "setup_fixture_game_soundtrack_(12345).exe"));

        ADesktopThatAnswers desktop = new() { WillPickFolders = [download] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        WorthAsking question = TheOneThatOpens<WorthAsking>(window);

        Assert.Equal("That looks like an add-on, not a game", question.Found<TextBlock>("Headline").Text);
        Assert.StartsWith(
            "setup_fixture_game_soundtrack_(12345).exe installs onto a game rather than being one.",
            question.Found<TextBlock>("Because").Text,
            StringComparison.Ordinal);
        Assert.Equal("Leave it for now", question.Found<Button>("TheLikelyOne").Content);
        Assert.Equal("Put it on anyway", question.Found<Button>("TheOtherWay").Content);

        Press(question, question.Found<Button>("TheLikelyOne"));
        Until(() => window.OwnedWindows.Count == 0, "the question to go");

        Assert.Empty(screen.Games);
    }

    [AvaloniaFact]
    public void An_add_on_by_itself_goes_on_when_somebody_wants_it_to()
    {
        string download = Path.Combine(_room, "downloads", "Fixture Game Soundtrack");
        Directory.CreateDirectory(download);
        File.Copy(TheFixture(), Path.Combine(download, "setup_fixture_game_soundtrack_(12345).exe"));

        ADesktopThatAnswers desktop = new() { WillPickFolders = [download] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        WorthAsking question = TheOneThatOpens<WorthAsking>(window);
        Press(question, question.Found<Button>("TheOtherWay"));

        // Not a refusal: it still goes on. It just must not go on in silence.
        Until(() => screen.Games.Count == 1, "it to be on the list");
    }

    // ------------------------------------------------- a folder of game files

    [AvaloniaFact]
    public void A_folder_of_game_files_with_two_that_could_start_it_asks_which_and_keeps_the_one_chosen()
    {
        string installed = AnInstalledGame();

        ADesktopThatAnswers desktop = new() { WillPickFolders = [installed] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        PickOne question = TheOneThatOpens<PickOne>(window);

        Assert.Equal("Which file starts An Installed Game?", question.Found<TextBlock>("Headline").Text);

        ListBox choices = question.Found<ListBox>("Choices");

        Assert.Equal(["Editor.exe", "Game.exe"], choices.Items.Cast<string>().Order(StringComparer.Ordinal));

        int game = choices.Items.Cast<string>().ToList().IndexOf("Game.exe");

        Press(question, (Control)choices.ContainerFromIndex(game)!);
        Press(question, question.Found<Button>("TakeThatOne"));

        Until(() => screen.Games.Count == 1, "the game to be on the list");

        // Narrowed to the one chosen, which is all the menu ever looks at.
        Assert.Equal("Game.exe", Assert.Single(screen.Games[0].LauncherCandidates).RelativePath);
    }

    [AvaloniaFact]
    public void Preferring_not_to_say_which_file_starts_it_still_puts_the_game_on()
    {
        string installed = AnInstalledGame();

        ADesktopThatAnswers desktop = new() { WillPickFolders = [installed] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Press(window, window.Found<Button>("AddAGame"));

        PickOne question = TheOneThatOpens<PickOne>(window);
        Press(question, question.Found<Button>("NoneOfThem"));

        Until(() => screen.Games.Count == 1, "the game to be on the list");

        // Saying no is allowed, and leaves the disc copying the folder.
        Assert.Equal(2, screen.Games[0].LauncherCandidates.Count);
    }

    // --------------------------------------------------------- the same again

    [AvaloniaFact]
    public void The_same_folder_again_with_nothing_missing_says_it_is_already_on_the_disc()
    {
        string download = ADownload("Fixture Game");

        ADesktopThatAnswers desktop = new() { WillPickFolders = [download] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Button add = window.Found<Button>("AddAGame");

        Press(window, add);
        Until(() => screen.Games.Count == 1, "the game to be on the list");

        Until(() => TryPress(window, add) && window.OwnedWindows.OfType<SayingSo>().Any(), "to be told it is on already");

        SayingSo box = window.OwnedWindows.OfType<SayingSo>().Single();

        Assert.Equal(Refusals.ItIsAlreadyOnTheDisc("Fixture Game: Directors Cut").Headline, box.Found<TextBlock>("Headline").Text);

        Press(box, box.Found<Button>("Understood"));
        Until(() => window.OwnedWindows.Count == 0, "the box to go");

        Assert.Single(screen.Games);
    }

    [AvaloniaFact]
    public void The_same_folder_again_offers_back_what_was_taken_off_and_puts_it_back_on_a_yes()
    {
        string download = ADownload("Fixture Game");

        ADesktopThatAnswers desktop = new() { WillPickFolders = [download] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Button add = window.Found<Button>("AddAGame");

        Press(window, add);
        Until(() => screen.Games.Count == 1, "the game to be on the list");

        // The installer's part, taken off the list.
        AThingOnTheDisc part = screen.WhatGoesOnIt[0].Inside.Single(each => each.Name.EndsWith("-1.bin", StringComparison.Ordinal));
        screen.TakeOff(part);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(screen.WhatGoesOnIt[0].Inside);

        Until(() => TryPress(window, add) && window.OwnedWindows.OfType<WorthAsking>().Any(), "to be offered it back");

        WorthAsking question = window.OwnedWindows.OfType<WorthAsking>().Single();

        // The screen's own words, which a test of the screen holds.
        Assert.Contains("-1.bin", question.Found<TextBlock>("Because").Text, StringComparison.Ordinal);

        Press(question, question.Found<Button>("TheLikelyOne"));
        Until(() => screen.WhatGoesOnIt[0].Inside.Count == 2, "the part to be back");
    }

    [AvaloniaFact]
    public void What_was_taken_off_stays_off_when_the_answer_to_having_it_back_is_no()
    {
        string download = ADownload("Fixture Game");

        ADesktopThatAnswers desktop = new() { WillPickFolders = [download] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Button add = window.Found<Button>("AddAGame");

        Press(window, add);
        Until(() => screen.Games.Count == 1, "the game to be on the list");

        screen.TakeOff(screen.WhatGoesOnIt[0].Inside.Single(each => each.Name.EndsWith("-1.bin", StringComparison.Ordinal)));
        Dispatcher.UIThread.RunJobs();

        Until(() => TryPress(window, add) && window.OwnedWindows.OfType<WorthAsking>().Any(), "to be offered it back");

        WorthAsking question = window.OwnedWindows.OfType<WorthAsking>().Single();

        Press(question, question.Found<Button>("TheOtherWay"));
        Until(() => window.OwnedWindows.Count == 0, "the question to go");

        // Somebody took it off on purpose and has just said so again.
        Assert.Single(screen.WhatGoesOnIt[0].Inside);
    }

    // --------------------------------------------------------- one at a time

    [AvaloniaFact]
    public void A_second_press_while_a_question_stands_does_not_start_a_second_walk()
    {
        ADesktopThatAnswers desktop = new() { WillPickFolders = [ADownloadWithAManual("Fixture Game")] };
        (MainWindow window, MakeADisc screen) = AWindowOver(desktop);

        Button add = window.Found<Button>("AddAGame");

        Press(window, add);
        WhatElseIsInThere question = TheOneThatOpens<WhatElseIsInThere>(window);

        // The window behind can be pressed here, which on Windows it cannot.
        Press(window, add);
        Press(window, add);

        Assert.Single(window.OwnedWindows);

        Press(question, question.Found<Button>("NoneOfThem"));
        Until(() => screen.Games.Count == 1, "the game to be on the list");

        // Once, not three times over, and no question left behind.
        Assert.DoesNotContain(window.OwnedWindows, each => each.IsVisible);
        Assert.Single(screen.Games);
    }

    // ------------------------------------------------- named on the command line

    [AvaloniaFact]
    public void A_folder_named_when_the_program_was_started_is_added_and_anything_else_is_let_pass()
    {
        string download = ADownload("Fixture Game");
        (MainWindow window, MakeADisc screen) = AWindowOver(new ADesktopThatAnswers());

        // A switch it does not know, a file, and a folder.
        Task adding = window.AddWhatWeWereOpenedWith(["--no-such-switch", Path.Combine(download, Installer), download]);

        Until(() => adding.IsCompleted, "the folders to be taken on");

        Assert.Equal("Fixture Game: Directors Cut", Assert.Single(screen.Games).Name);
        Assert.Empty(window.OwnedWindows);
    }

    // ------------------------------------------------------------------ helpers

    private static (MainWindow Window, MakeADisc Screen) AWindowOver(ADesktopThatAnswers desktop)
    {
        MakeADisc screen = TheWindow.AScreen();

        return (TheWindow.Showing(screen, desktop, new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows)), screen);
    }

    private static string TheFixture() => Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe");

    /// <summary>A download as GOG ships one: an installer that says what it is, and a part beside it.</summary>
    private string ADownload(string folder)
    {
        string download = Path.Combine(_room, "downloads", folder);
        Directory.CreateDirectory(download);

        File.Copy(TheFixture(), Path.Combine(download, Installer));
        File.WriteAllBytes(Path.Combine(download, Installer.Replace(".exe", "-1.bin", StringComparison.Ordinal)), new byte[5000]);

        return download;
    }

    private string ADownloadWithAManual(string folder)
    {
        string download = ADownload(folder);
        File.WriteAllBytes(Path.Combine(download, "Fixture Game Manual.pdf"), new byte[300]);

        return download;
    }

    /// <summary>A game as it sits installed: files, and two of them that could be what starts it.</summary>
    private string AnInstalledGame()
    {
        string installed = Path.Combine(_room, "games", "An Installed Game");
        Directory.CreateDirectory(Path.Combine(installed, "data"));

        File.WriteAllBytes(Path.Combine(installed, "Game.exe"), new byte[4000]);
        File.WriteAllBytes(Path.Combine(installed, "Editor.exe"), new byte[3000]);
        File.WriteAllBytes(Path.Combine(installed, "data", "levels.pak"), new byte[9000]);

        return installed;
    }

    /// <summary>
    /// Lets the window's thread run until something is so, and says what it
    /// was waiting for when five seconds pass without it.
    /// </summary>
    private static void Until(Func<bool> itIsSo, string what)
    {
        DateTime giveUp = DateTime.UtcNow.AddSeconds(5);

        while (true)
        {
            Dispatcher.UIThread.RunJobs();

            if (itIsSo())
            {
                Dispatcher.UIThread.RunJobs();
                return;
            }

            Assert.True(DateTime.UtcNow < giveUp, $"Waited five seconds for {what}, and it did not come.");

            // The walk is on another thread and needs a moment of its own.
            Thread.Sleep(5);
        }
    }

    /// <summary>The one window of that kind that opens over another, waited for.</summary>
    private static T TheOneThatOpens<T>(Window owner)
        where T : Window
    {
        Until(() => owner.OwnedWindows.OfType<T>().Any(each => each.IsVisible), $"a {typeof(T).Name} to open");

        return owner.OwnedWindows.OfType<T>().Single(each => each.IsVisible);
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

    /// <summary>
    /// A press that may land while the last walk is still finishing, for
    /// pressing again until one takes. Always true, so it can stand in a
    /// condition that is asked again.
    /// </summary>
    private static bool TryPress(Window window, Control control)
    {
        if (window.OwnedWindows.Count == 0)
        {
            Press(window, control);
        }

        return true;
    }
}
