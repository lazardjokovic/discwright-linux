using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

using DiscWright.Core;
using DiscWright.LinuxApp;
using DiscWright.Platform;
using DiscWright.Tests;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The top line of the main window: the program's own links on the left and
/// the three things that are about the whole disc on the right.
/// </summary>
/// <remarks>
/// <para>
/// The window is the real one over the real Linux machine, with real files
/// in a folder of its own. Only the desktop is answered for: which file
/// somebody picks, and whether anything will open what it is asked to.
/// </para>
/// <para>
/// Every press is a pointer on the button, and every question that follows
/// is answered on the window that asks it. On Windows none of this could be
/// run without taking somebody's screen.
/// </para>
/// </remarks>
public sealed class TheTopLineOnLinux : IDisposable
{
    /// <summary>
    /// A point on the main window that is page and nothing else: in the
    /// margin down its left side, which nothing is ever laid out in.
    /// </summary>
    private static readonly Point InTheMargin = new(14, 400);

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-top-line-" + Guid.NewGuid().ToString("N"));

    public TheTopLineOnLinux() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    // ------------------------------------------------------------ what is there

    [AvaloniaFact]
    public void Everything_on_it_answers_to_the_name_the_Windows_window_gives_it()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        Assert.Equal("Appearance: following the desktop", window.Found<Button>("Appearance").Content);
        Assert.Equal("Open the log", window.Found<Button>("OpenTheLog").Content);
        Assert.Equal("Start a new disc", window.Found<Button>("StartANewDisc").Content);
        Assert.Equal("Open one you built", window.Found<Button>("OpenADisc").Content);
        Assert.Equal("Save it for later, without building", window.Found<Button>("SaveItForLater").Content);

        // Nothing saved yet, so nothing said about it and no gap left for it.
        Assert.False(window.Found<TextBlock>("HowTheSaveWent").IsVisible);
    }

    [AvaloniaFact]
    public void The_two_buttons_share_a_column_340_wide_up_against_the_right_edge()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        Button start = window.Found<Button>("StartANewDisc");
        Button open = window.Found<Button>("OpenADisc");

        double left = start.TranslatePoint(default, window)!.Value.X;
        double between = open.TranslatePoint(default, window)!.Value.X - (left + start.Bounds.Width);
        double right = open.TranslatePoint(new Point(open.Bounds.Width, 0), window)!.Value.X;

        // The column every part of the right hand side lines up on.
        Assert.Equal(340, right - left);
        Assert.Equal(920 - 28, right);
        Assert.Equal(start.Bounds.Width, open.Bounds.Width);
        Assert.Equal(10, between);

        // The quiet third sits under them at the same right edge.
        Button save = window.Found<Button>("SaveItForLater");
        Assert.Equal(right, save.TranslatePoint(new Point(save.Bounds.Width, 0), window)!.Value.X);
        Assert.True(save.TranslatePoint(default, window)!.Value.Y > start.TranslatePoint(new Point(0, start.Bounds.Height), window)!.Value.Y);
    }

    // ---------------------------------------------------------------- the look

    [AvaloniaFact]
    public void The_look_is_changed_from_the_window_and_the_program_changes_with_it()
    {
        Application app = Application.Current!;
        ThemeVariant? before = app.RequestedThemeVariant;
        List<string> kept = [];

        try
        {
            ChoosingTheLook look = new(null, kept.Add, App.WhatTheLookFollows);
            WearingTheTheme.Follow(app, look);

            MainWindow window = TheWindow.Showing(TheWindow.AScreen(), new ADesktopThatAnswers(), look);
            Button control = window.Found<Button>("Appearance");

            Press(window, control);

            Assert.Equal("Appearance: light", control.Content);
            Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);

            Press(window, control);

            Assert.Equal("Appearance: dark", control.Content);
            Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);

            // And the window it was pressed on is dark by then.
            Assert.Equal(WhatWasDrawn.Of("Page", ThemeVariant.Dark), window.ColourAt(InTheMargin));

            Press(window, control);

            Assert.Equal("Appearance: following the desktop", control.Content);
            Assert.Equal(["light", "dark", "following windows"], kept);
        }
        finally
        {
            app.RequestedThemeVariant = before;
        }
    }

    [AvaloniaFact]
    public void Nothing_on_the_top_line_mentions_Windows_or_a_PC()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        // The Windows window says "whatever Windows wears" and "It stays on
        // this PC". This is not Windows and may not be a PC.
        foreach (string id in new[] { "Appearance", "OpenTheLog", "SaveItForLater" })
        {
            Button button = window.Found<Button>(id);
            string said = $"{button.Content} {ToolTip.GetTip(button)}";

            Assert.DoesNotContain("Windows", said, StringComparison.Ordinal);
            Assert.DoesNotContain(" PC", said, StringComparison.Ordinal);
        }

        Assert.Contains("the desktop", (string)ToolTip.GetTip(window.Found<Button>("Appearance"))!, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------- the log

    [AvaloniaFact]
    public void Open_the_log_opens_todays_file_when_today_has_anything_in_it()
    {
        TheLogOnDisk log = new(Path.Combine(_room, "logs"));
        log.Write("Something happened.");

        ADesktopThatAnswers desktop = new();
        MainWindow window = TheWindow.Showing(TheWindow.AScreen(), desktop, ALook(), log);

        Press(window, window.Found<Button>("OpenTheLog"));

        Assert.True(File.Exists(log.Today));
        Assert.Equal([log.Today], desktop.Opened);
    }

    [AvaloniaFact]
    public void Open_the_log_opens_the_folder_when_today_has_nothing_in_it_yet()
    {
        TheLogOnDisk log = new(Path.Combine(_room, "logs"));

        ADesktopThatAnswers desktop = new();
        MainWindow window = TheWindow.Showing(TheWindow.AScreen(), desktop, ALook(), log);

        Press(window, window.Found<Button>("OpenTheLog"));

        // The folder, made if it was not there, so there is something to open.
        Assert.Equal([log.Folder], desktop.Opened);
        Assert.True(Directory.Exists(log.Folder));
    }

    [AvaloniaFact]
    public void When_nothing_will_open_the_log_it_says_where_the_log_is()
    {
        TheLogOnDisk log = new(Path.Combine(_room, "logs"));

        ADesktopThatAnswers desktop = new() { OpensThings = false };
        MainWindow window = TheWindow.Showing(TheWindow.AScreen(), desktop, ALook(), log);

        Press(window, window.Found<Button>("OpenTheLog"));

        SayingSo box = TheOneOpenOver<SayingSo>(window);

        Assert.Equal("Nothing would open the log", box.Found<TextBlock>("Headline").Text);
        Assert.Contains(log.Folder, box.Found<SelectableTextBlock>("Because").Text, StringComparison.Ordinal);

        Press(box, box.Found<Button>("Understood"));
    }

    // ------------------------------------------------------- start a new disc

    [AvaloniaFact]
    public void Start_a_new_disc_is_dead_while_there_is_nothing_to_start_again_from()
    {
        MainWindow window = TheWindow.WithNothingOnIt();
        Button start = window.Found<Button>("StartANewDisc");

        Assert.False(start.IsEnabled);
        Assert.Equal(
            WhatWasDrawn.Of("Dead", Application.Current!.ActualThemeVariant),
            start.Part<TextBlock>("Words").Foreground.AsColour());
    }

    [AvaloniaFact]
    public void Start_a_new_disc_asks_first_and_keeping_what_is_here_keeps_it()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);

        screen.Label = "HOLLOW KNIGHT";
        Dispatcher.UIThread.RunJobs();

        Button start = window.Found<Button>("StartANewDisc");
        Assert.True(start.IsEnabled);

        Press(window, start);

        WorthAsking question = TheOneOpenOver<WorthAsking>(window);

        Assert.Equal("Start a new disc?", question.Found<TextBlock>("Headline").Text);
        Assert.Equal(
            "What is on the screen now goes: the games, the name, the artwork and the manual. "
                + "Nothing already built or burned is touched. A folder you chose for the discs stays; "
                + "one that was only suggested from the game goes with it.",
            question.Found<TextBlock>("Because").Text);

        // Keeping is the likely answer, on the button Enter presses: the
        // other one throws an afternoon away.
        Assert.Equal("Keep what is here", question.Found<Button>("TheLikelyOne").Content);
        Assert.Equal("Start a new disc", question.Found<Button>("TheOtherWay").Content);

        Press(question, question.Found<Button>("TheLikelyOne"));

        Assert.Equal("HOLLOW KNIGHT", screen.Label);
    }

    [AvaloniaFact]
    public void Start_a_new_disc_clears_the_window_when_that_is_the_answer()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);

        screen.Label = "HOLLOW KNIGHT";
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("StartANewDisc"));

        WorthAsking question = TheOneOpenOver<WorthAsking>(window);
        Press(question, question.Found<Button>("TheOtherWay"));

        Assert.Equal(string.Empty, screen.Label);
        Assert.False(screen.HasAnythingOnIt);
        Assert.False(window.Found<Button>("StartANewDisc").IsEnabled);
    }

    [AvaloniaFact]
    public void Shutting_the_question_about_a_new_disc_clears_the_window_as_it_does_on_Windows()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);

        screen.Label = "HOLLOW KNIGHT";
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("StartANewDisc"));
        TheOneOpenOver<WorthAsking>(window).Close();
        Dispatcher.UIThread.RunJobs();

        // The cross is the other answer, and here the other answer is to
        // start again. Written down as a test because it is a thing somebody
        // may want changed, on both systems at once.
        Assert.Equal(string.Empty, screen.Label);
    }

    // ---------------------------------------------------------- save for later

    [AvaloniaFact]
    public void Save_it_for_later_is_dead_until_there_is_something_to_save_and_somewhere_to_put_it()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);
        Button save = window.Found<Button>("SaveItForLater");

        Assert.False(save.IsEnabled);

        screen.Label = "HOLLOW KNIGHT";
        screen.OutputFolder = Path.Combine(_room, "discs");
        Dispatcher.UIThread.RunJobs();

        Assert.True(save.IsEnabled);
    }

    [AvaloniaFact]
    public void Save_it_for_later_writes_the_project_and_says_where_it_went()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);
        string discs = Path.Combine(_room, "discs");

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = discs;
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("SaveItForLater"));

        Assert.True(File.Exists(Path.Combine(discs, "discproject.json")));

        TextBlock said = window.Found<TextBlock>("HowTheSaveWent");

        Assert.True(said.CanBeSeen());
        Assert.Equal($"Saved. Open one you built finds it in {discs}.", said.Text);
        Assert.Equal(TextAlignment.Right, said.TextAlignment);
    }

    // ------------------------------------------------------ open one you built

    [AvaloniaFact]
    public void Open_one_you_built_asks_the_desktop_for_a_project_file_by_its_own_name_first()
    {
        ADesktopThatAnswers desktop = new();
        MainWindow window = TheWindow.Showing(TheWindow.AScreen(), desktop, ALook());

        Press(window, window.Found<Button>("OpenADisc"));

        (string title, IReadOnlyList<AKindOfFile> kinds) = Assert.Single(desktop.AskedFor);

        Assert.Equal("Which disc?", title);
        Assert.Equal("A disc DiscWright made (discproject.json)", kinds[0].Name);
        Assert.Equal(["discproject.json"], kinds[0].Patterns);
        Assert.Equal("Any project file", kinds[1].Name);
        Assert.Equal(["*.json"], kinds[1].Patterns);
    }

    [AvaloniaFact]
    public void Open_one_you_built_changes_nothing_when_somebody_would_rather_not_pick()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen, new ADesktopThatAnswers { WillPick = null }, ALook());

        screen.Label = "HOLLOW KNIGHT";
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("OpenADisc"));

        Assert.Empty(window.OwnedWindows);
        Assert.Equal("HOLLOW KNIGHT", screen.Label);
    }

    [AvaloniaFact]
    public void A_disc_saved_from_the_window_opens_again_on_an_empty_window_without_a_question()
    {
        string saved = ADiscSavedFromTheWindow();

        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen, new ADesktopThatAnswers { WillPick = saved }, ALook());

        Press(window, window.Found<Button>("OpenADisc"));

        // Nothing to lose, so nothing asked.
        Assert.Empty(window.OwnedWindows);

        Assert.Equal("FIXTURE GAME", screen.Label);
        Assert.Equal("Fixture Game: Directors Cut", Assert.Single(screen.Games).Name);
        Assert.Equal(Path.Combine(_room, "discs"), screen.OutputFolder);
    }

    [AvaloniaFact]
    public void Opening_a_disc_over_what_is_on_the_window_asks_and_leaving_it_alone_leaves_it()
    {
        string saved = ADiscSavedFromTheWindow();

        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen, new ADesktopThatAnswers { WillPick = saved }, ALook());

        screen.Label = "AN AFTERNOON";
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("OpenADisc"));

        WorthAsking question = TheOneOpenOver<WorthAsking>(window);

        Assert.Equal("Replace what is on the window?", question.Found<TextBlock>("Headline").Text);
        Assert.Equal(
            "Opening a disc puts its games, its name and its settings here, and takes away what is here now. "
                + "Nothing on disk is touched either way.",
            question.Found<TextBlock>("Because").Text);
        Assert.Equal("Open it", question.Found<Button>("TheLikelyOne").Content);
        Assert.Equal("Leave this alone", question.Found<Button>("TheOtherWay").Content);

        Press(question, question.Found<Button>("TheOtherWay"));

        Assert.Equal("AN AFTERNOON", screen.Label);
        Assert.Empty(screen.Games);
    }

    [AvaloniaFact]
    public void Opening_a_disc_over_what_is_on_the_window_replaces_it_when_that_is_the_answer()
    {
        string saved = ADiscSavedFromTheWindow();

        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen, new ADesktopThatAnswers { WillPick = saved }, ALook());

        screen.Label = "AN AFTERNOON";
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("OpenADisc"));

        WorthAsking question = TheOneOpenOver<WorthAsking>(window);
        Press(question, question.Found<Button>("TheLikelyOne"));

        Assert.Equal("FIXTURE GAME", screen.Label);
        Assert.Single(screen.Games);
    }

    [AvaloniaFact]
    public void A_file_that_is_not_a_project_is_refused_before_anybody_is_asked_to_give_anything_up()
    {
        string notOne = Path.Combine(_room, "notes.json");
        File.WriteAllText(notOne, "this is not a disc");

        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen, new ADesktopThatAnswers { WillPick = notOne }, ALook());

        screen.Label = "AN AFTERNOON";
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("OpenADisc"));

        // The refusal, and not the question: nobody has said yes to
        // replacing an afternoon's work with a file that cannot be opened.
        Assert.Empty(window.OwnedWindows.OfType<WorthAsking>());
        SayingSo box = TheOneOpenOver<SayingSo>(window);

        Assert.Equal("That is not a DiscWright project", box.Found<TextBlock>("Headline").Text);
        Assert.StartsWith("The file could not be read as one.", box.Found<SelectableTextBlock>("Because").Text, StringComparison.Ordinal);

        Press(box, box.Found<Button>("Understood"));

        Assert.Equal("AN AFTERNOON", screen.Label);
    }

    [AvaloniaFact]
    public void A_file_that_went_away_after_it_was_picked_is_refused_and_not_a_fault()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(
            screen, new ADesktopThatAnswers { WillPick = Path.Combine(_room, "gone", "discproject.json") }, ALook());

        Press(window, window.Found<Button>("OpenADisc"));

        SayingSo box = TheOneOpenOver<SayingSo>(window);

        Assert.Equal("That is not a DiscWright project", box.Found<TextBlock>("Headline").Text);

        Press(box, box.Found<Button>("Understood"));
    }

    [AvaloniaFact]
    public void A_disc_whose_games_have_all_gone_is_refused_by_name_and_the_window_left_alone()
    {
        string saved = ADiscSavedFromTheWindow();
        Directory.Delete(Path.Combine(_room, "downloads"), recursive: true);

        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen, new ADesktopThatAnswers { WillPick = saved }, ALook());

        Press(window, window.Found<Button>("OpenADisc"));

        SayingSo box = TheOneOpenOver<SayingSo>(window);

        Assert.Equal("Nothing on that disc is where it was", box.Found<TextBlock>("Headline").Text);
        Assert.Contains("Fixture Game", box.Found<SelectableTextBlock>("Because").Text, StringComparison.Ordinal);

        Press(box, box.Found<Button>("Understood"));

        Assert.Empty(screen.Games);
        Assert.Equal(string.Empty, screen.Label);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_top_line_is_kept_as_a_picture(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        WhatWasDrawn.Wearing(look, () =>
        {
            MakeADisc screen = TheWindow.AScreen();
            MainWindow window = TheWindow.Showing(screen);

            screen.Add(AGame());
            screen.Label = "FIXTURE GAME";
            screen.OutputFolder = Path.Combine(_room, "discs");
            Dispatcher.UIThread.RunJobs();

            Press(window, window.Found<Button>("SaveItForLater"));
            window.MouseMove(new Point(460, 600));

            string kept = Repository.File("artifacts", "the-look", $"the-top-line-{which.ToLowerInvariant()}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(kept)!);

            Dispatcher.UIThread.RunJobs();
            using Avalonia.Media.Imaging.WriteableBitmap drawn = window.CaptureRenderedFrame()!;
            drawn.Save(kept, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

            Assert.Equal(WhatWasDrawn.Of("Page", look), window.ColourAt(InTheMargin));
        });
    }

    // ------------------------------------------------------------------ helpers

    private static ChoosingTheLook ALook() => new(null, _ => { }, App.WhatTheLookFollows);

    /// <summary>A game in a download folder of its own, as GOG ships one.</summary>
    private GameFolder AGame()
    {
        string download = Path.Combine(_room, "downloads", "Fixture Game");

        if (!Directory.Exists(download))
        {
            Directory.CreateDirectory(download);

            File.Copy(
                Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"),
                Path.Combine(download, "setup_fixture_game_1.2.3_(64bit)_(12345).exe"));
        }

        return Assert.IsType<Outcome<GameFolder>.Ok>(
            new GameFolders(new WindowsFileSystem(), TheLinuxMachine.TheReaderOfFolders()).At(download)).Value;
    }

    /// <summary>A project file, made by pressing Save it for later on a window with a game on it.</summary>
    private string ADiscSavedFromTheWindow()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);
        string discs = Path.Combine(_room, "discs");

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = discs;
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("SaveItForLater"));
        window.Close();

        string saved = Path.Combine(discs, "discproject.json");
        Assert.True(File.Exists(saved), "Save it for later wrote no project file.");

        return saved;
    }

    private static T TheOneOpenOver<T>(Window owner)
        where T : Window
    {
        Dispatcher.UIThread.RunJobs();

        return Assert.Single(owner.OwnedWindows.OfType<T>());
    }

    private static void Press(Window window, Button button)
    {
        Dispatcher.UIThread.RunJobs();

        Point middle = button.MiddleIn(window);

        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
