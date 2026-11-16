using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using DiscWright.Core;
using DiscWright.LinuxApp;
using DiscWright.Platform;
using DiscWright.Tests;

using Path = System.IO.Path;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The two questions with a list in them: what else was in the folder a game
/// came out of, and one out of several.
/// </summary>
/// <remarks>
/// <para>
/// What the first of these decides is tested where it lives, in the shared
/// suite, with no window. What is asked here is whether the window says what
/// that decides and passes back what somebody pressed: the rows, the ticks,
/// the dropdowns, and the three ways it can be put away.
/// </para>
/// <para>
/// The folder is a real one on a real disk, and every answer is a press.
/// </para>
/// </remarks>
public sealed class TheOtherQuestionsOnLinux : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";
    private const string TheManual = "Fixture Game Manual.pdf";
    private const string TheSoundtrack = "soundtrack_fixture_game.exe";

    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);
    private static readonly string[] Drives = ["D: HL-DT-ST BD-RE", "E: ASUS DRW-24", "F: a third one"];

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-questions-" + Guid.NewGuid().ToString("N"));
    private readonly GameFolder _game;
    private readonly IReadOnlyList<LeftBehind> _left;

    public TheOtherQuestionsOnLinux()
    {
        string download = Path.Combine(_room, "Fixture Game");
        Directory.CreateDirectory(Path.Combine(download, "artbook"));

        File.Copy(Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"), Path.Combine(download, Installer));
        File.WriteAllBytes(Path.Combine(download, TheManual), new byte[300]);
        File.WriteAllBytes(Path.Combine(download, "artbook", "cover.png"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(download, "artbook", "map.png"), new byte[24]);
        File.WriteAllBytes(Path.Combine(download, TheSoundtrack), new byte[2000]);

        WindowsFileSystem disk = new();

        _game = Assert.IsType<Outcome<GameFolder>.Ok>(
            new GameFolders(disk, TheLinuxMachine.TheReaderOfFolders()).At(download)).Value;
        _left = WhatWasLeftBehind.In(disk.List(download), _game);
    }

    public void Dispose() => Directory.Delete(_room, recursive: true);

    // ------------------------------------------------ what else is in there

    [AvaloniaFact]
    public async Task Nothing_is_asked_when_nothing_was_left_behind()
    {
        Window owner = AnOwner();

        var answer = await WhatElseIsInThere.Ask(owner, _game, [], 0).WaitAsync(Soon);

        // No window at all: a dialog that appears to say "there is nothing
        // to tell you" is worse than silence.
        Assert.Empty(owner.OwnedWindows);
        Assert.Equal(_game, Assert.NotNull(answer).Game);
    }

    [AvaloniaFact]
    public async Task It_lists_each_thing_ticked_with_what_it_looks_like_and_a_guess_at_what_it_is()
    {
        (Window owner, var asked) = AskedWhatElse();
        WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

        Assert.Equal("There is more in that folder", question.Title);
        Assert.Equal("There are 3 more things in that folder", question.Found<TextBlock>("Headline").Text);

        // Each row's tick answers to the name of the thing.
        foreach (string name in new[] { TheManual, "artbook", TheSoundtrack })
        {
            CheckBox tick = question.Found<CheckBox>(name);

            Assert.True(tick.IsChecked);
            Assert.Equal(name, tick.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        }

        // And each row's dropdown to a name of its own, showing the guess.
        Assert.Equal("The game's manual", WhatItShows(question.Found<ComboBox>(TheManual + ": what to do with it")));
        Assert.Equal("One of the game's extras", WhatItShows(question.Found<ComboBox>("artbook: what to do with it")));
        Assert.Equal("Attach as an add-on", WhatItShows(question.Found<ComboBox>(TheSoundtrack + ": what to do with it")));

        // What each looks like, on a line of its own under the name.
        string[] said = [.. question.GetVisualDescendants().OfType<TextBlock>().Select(each => each.Text ?? string.Empty)];

        Assert.Contains("A folder of 2 files", said);
        Assert.Contains("Looks like the game's manual", said);

        question.Close();
        await asked.WaitAsync(Soon);
    }

    [AvaloniaFact]
    public async Task Put_these_on_too_takes_what_is_ticked_to_where_each_was_given()
    {
        (Window owner, var asked) = AskedWhatElse();
        WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

        Press(question, question.Found<Button>("TakeThem"));

        var (game, addOns, _) = Assert.NotNull(await asked.WaitAsync(Soon));

        Assert.Single(game.OwnOrNone, own => own.Kind == GameExtraKind.Manual);
        Assert.Single(game.OwnOrNone, own => own.Kind == GameExtraKind.Extra);
        Assert.Equal(TheSoundtrack, Assert.Single(addOns).Name);
    }

    [AvaloniaFact]
    public async Task A_thing_unticked_on_the_window_is_left_off_and_its_dropdown_goes_dead()
    {
        (Window owner, var asked) = AskedWhatElse();
        WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

        CheckBox tick = question.Found<CheckBox>("artbook");
        ComboBox itsDropdown = question.Found<ComboBox>("artbook: what to do with it");

        Assert.True(itsDropdown.IsEnabled);

        Press(question, tick);

        Assert.False(tick.IsChecked);
        Assert.False(itsDropdown.IsEnabled);

        Press(question, question.Found<Button>("TakeThem"));

        var (game, _, _) = Assert.NotNull(await asked.WaitAsync(Soon));

        Assert.DoesNotContain(game.OwnOrNone, own => own.Kind == GameExtraKind.Extra);
    }

    [AvaloniaFact]
    public async Task With_nothing_ticked_the_big_button_is_dead_and_tick_them_all_brings_it_back()
    {
        (Window owner, var asked) = AskedWhatElse();
        WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

        Button take = question.Found<Button>("TakeThem");
        Button tickThemAll = question.Found<Button>("TickThemAll");

        // Everything is ticked, so there is nothing for the link to do.
        Assert.True(take.IsEnabled);
        Assert.False(tickThemAll.IsEnabled);

        foreach (string name in new[] { TheManual, "artbook", TheSoundtrack })
        {
            Press(question, question.Found<CheckBox>(name));
        }

        // It would put nothing on, which is the other button's job.
        Assert.False(take.IsEnabled);
        Assert.True(tickThemAll.IsEnabled);

        Press(question, tickThemAll);

        Assert.True(take.IsEnabled);
        Assert.False(tickThemAll.IsEnabled);
        Assert.True(question.Found<CheckBox>("artbook").IsChecked);

        question.Close();
        await asked.WaitAsync(Soon);
    }

    [AvaloniaFact]
    public async Task A_dropdown_changed_with_the_pointer_changes_where_the_thing_goes()
    {
        (Window owner, var asked) = AskedWhatElse();
        WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

        ComboBox dropdown = question.Found<ComboBox>(TheSoundtrack + ": what to do with it");

        Press(question, dropdown);
        Assert.True(dropdown.IsDropDownOpen);

        // "One of the game's extras", the second row, in the list that opened.
        ComboBoxItem extras = Assert.IsType<ComboBoxItem>(dropdown.ContainerFromIndex(1));
        Assert.Equal("One of the game's extras", extras.Content);

        TopLevel list = TopLevel.GetTopLevel(extras)!;
        Point onIt = extras.TranslatePoint(new Point(extras.Bounds.Width / 2, extras.Bounds.Height / 2), list)!.Value;

        list.MouseMove(onIt);
        list.MouseDown(onIt, MouseButton.Left);
        list.MouseUp(onIt, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("One of the game's extras", WhatItShows(dropdown));

        Press(question, question.Found<Button>("TakeThem"));

        var (game, addOns, _) = Assert.NotNull(await asked.WaitAsync(Soon));

        Assert.Empty(addOns);
        Assert.Contains(game.OwnOrNone, own => own.Path == Path.Combine(_game.Path, TheSoundtrack));
    }

    [AvaloniaFact]
    public async Task None_of_them_puts_the_game_on_as_it_was()
    {
        (Window owner, var asked) = AskedWhatElse();
        WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

        Press(question, question.Found<Button>("NoneOfThem"));

        var (game, addOns, _) = Assert.NotNull(await asked.WaitAsync(Soon));

        Assert.Equal(_game, game);
        Assert.Empty(addOns);
    }

    [AvaloniaFact]
    public async Task Shutting_it_is_not_an_answer_and_the_folder_is_left_alone()
    {
        (Window owner, var asked) = AskedWhatElse();

        TheOneOpenOver<WhatElseIsInThere>(owner).Close();

        // Nothing, which is not "none of them": the game is not put on.
        Assert.Null(await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task The_same_for_the_rest_is_only_offered_when_more_games_follow_and_is_carried_when_ticked()
    {
        (Window owner, var asked) = AskedWhatElse(after: 0);
        WhatElseIsInThere alone = TheOneOpenOver<WhatElseIsInThere>(owner);

        Assert.False(alone.Found<CheckBox>("TheSameForTheRest").IsVisible);

        alone.Close();
        await asked.WaitAsync(Soon);

        (owner, asked) = AskedWhatElse(after: 2);
        WhatElseIsInThere withMore = TheOneOpenOver<WhatElseIsInThere>(owner);

        CheckBox same = withMore.Found<CheckBox>("TheSameForTheRest");

        Assert.True(same.CanBeSeen());
        Assert.Equal(
            "Answer the same way for the other 2 games being added",
            same.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.False(same.IsChecked);

        Press(withMore, same);
        Press(withMore, withMore.Found<Button>("TakeThem"));

        Assert.NotNull(Assert.NotNull(await asked.WaitAsync(Soon)).ForTheRest);
    }

    [AvaloniaFact]
    public async Task What_else_is_in_there_is_written_down_with_what_was_taken()
    {
        Heard log = new();
        IKeepALog before = WorthAsking.Log;
        WorthAsking.Log = log;

        try
        {
            (Window owner, var asked) = AskedWhatElse();
            WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

            Press(question, question.Found<CheckBox>("artbook"));
            Press(question, question.Found<Button>("TakeThem"));
            await asked.WaitAsync(Soon);

            Assert.Equal(2, log.Lines.Count);
            Assert.StartsWith("Asked: There are 3 more things in that folder, beside Fixture Game: Directors Cut (", log.Lines[0], StringComparison.Ordinal);
            Assert.Contains("artbook", log.Lines[0], StringComparison.Ordinal);

            Assert.StartsWith("Answered: Put these on too (", log.Lines[1], StringComparison.Ordinal);
            Assert.Contains(TheManual, log.Lines[1], StringComparison.Ordinal);
            Assert.DoesNotContain("artbook", log.Lines[1], StringComparison.Ordinal);
        }
        finally
        {
            WorthAsking.Log = before;
        }
    }

    [AvaloniaTheory]
    [InlineData("None of them", "Answered: None of them")]
    [InlineData("the cross", "Answered: nothing, the question was shut")]
    public async Task Leaving_them_off_and_leaving_the_question_are_written_down_as_different_things(string how, string written)
    {
        Heard log = new();
        IKeepALog before = WorthAsking.Log;
        WorthAsking.Log = log;

        try
        {
            (Window owner, var asked) = AskedWhatElse();
            WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

            if (how == "None of them")
            {
                Press(question, question.Found<Button>("NoneOfThem"));
            }
            else
            {
                question.Close();
            }

            await asked.WaitAsync(Soon);

            // One is a decision and the other is not, and somebody reading
            // the log to rebuild a disc has to be able to tell which.
            Assert.Equal(written, log.Lines[^1]);
        }
        finally
        {
            WorthAsking.Log = before;
        }
    }

    [AvaloniaFact]
    public async Task What_else_is_in_there_is_700_by_460_and_cannot_be_resized()
    {
        (Window owner, var asked) = AskedWhatElse();
        WhatElseIsInThere question = TheOneOpenOver<WhatElseIsInThere>(owner);

        Assert.Equal(700, question.Bounds.Width);
        Assert.Equal(460, question.Bounds.Height);
        Assert.False(question.CanResize);
        Assert.False(question.ShowInTaskbar);

        // Nothing in a row runs off the panel: each dropdown ends inside it.
        foreach (ComboBox dropdown in question.GetVisualDescendants().OfType<ComboBox>())
        {
            Assert.True(dropdown.TranslatePoint(new Point(dropdown.Bounds.Width, 0), question)!.Value.X < 700 - 24);
        }

        question.Close();
        await asked.WaitAsync(Soon);
    }

    // -------------------------------------------------------------- pick one

    [AvaloniaFact]
    public async Task Pick_one_says_what_it_asks_and_lists_the_choices_each_answering_to_its_words()
    {
        (Window owner, Task<string?> asked) = AskedToPickOne(pickTheFirst: true);
        PickOne question = TheOneOpenOver<PickOne>(owner);

        Assert.Equal("Which drive?", question.Found<TextBlock>("Headline").Text);
        Assert.Equal("Two of them hold a blank.", question.Found<TextBlock>("Because").Text);
        Assert.Equal("Use that one", question.Found<Button>("TakeThatOne").Content);
        Assert.Equal("Cancel", question.Found<Button>("NoneOfThem").Content);

        ListBox list = question.Found<ListBox>("Choices");

        Assert.Equal(
            Drives,
            Enumerable.Range(0, 3).Select(at => AutomationProperties.GetName(list.ContainerFromIndex(at)!)));

        Assert.Equal(520, question.Bounds.Width);
        Assert.False(question.CanResize);

        question.Close();
        await asked.WaitAsync(Soon);
    }

    [AvaloniaFact]
    public async Task Pick_one_starts_on_the_first_row_where_that_is_safe_and_Enter_takes_it()
    {
        (Window owner, Task<string?> asked) = AskedToPickOne(pickTheFirst: true);
        PickOne question = TheOneOpenOver<PickOne>(owner);

        Assert.Equal(0, question.Found<ListBox>("Choices").SelectedIndex);
        Assert.True(question.Found<Button>("TakeThatOne").IsEnabled);

        Type(question, PhysicalKey.Enter);

        Assert.Equal(Drives[0], await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task Where_the_answer_cannot_be_taken_back_nothing_is_chosen_for_somebody()
    {
        (Window owner, Task<string?> asked) = AskedToPickOne(pickTheFirst: false);
        PickOne question = TheOneOpenOver<PickOne>(owner);

        ListBox list = question.Found<ListBox>("Choices");
        Button take = question.Found<Button>("TakeThatOne");

        Assert.Equal(-1, list.SelectedIndex);
        Assert.False(take.IsEnabled);

        // Enter erased whichever drive happened to be first, once.
        Type(question, PhysicalKey.Enter);

        Assert.True(question.IsVisible, "Enter answered a question nobody had answered.");
        Assert.False(asked.IsCompleted);

        // Picked by somebody, the button comes alive and takes that one.
        Press(question, (Control)list.ContainerFromIndex(1)!);

        Assert.Equal(1, list.SelectedIndex);
        Assert.True(take.IsEnabled);

        Press(question, take);

        Assert.Equal(Drives[1], await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task A_double_press_on_a_row_takes_it()
    {
        (Window owner, Task<string?> asked) = AskedToPickOne(pickTheFirst: false);
        PickOne question = TheOneOpenOver<PickOne>(owner);

        Control row = (Control)question.Found<ListBox>("Choices").ContainerFromIndex(2)!;
        Point middle = row.MiddleIn(question);

        question.MouseMove(middle);
        question.MouseDown(middle, MouseButton.Left);
        question.MouseUp(middle, MouseButton.Left);
        question.MouseDown(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // The second press is what takes it, and the window is gone by the
        // time the pointer comes up.
        if (question.IsVisible)
        {
            question.MouseUp(middle, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(Drives[2], await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task A_double_press_where_there_is_no_row_takes_nothing_and_leaves_the_question_standing()
    {
        (Window owner, Task<string?> asked) = AskedToPickOne(pickTheFirst: false);
        PickOne question = TheOneOpenOver<PickOne>(owner);

        // In the list, in the room round its rows, with nothing chosen.
        ListBox list = question.Found<ListBox>("Choices");
        Point beside = list.TranslatePoint(new Point(2, 2), question)!.Value;

        question.MouseMove(beside);
        question.MouseDown(beside, MouseButton.Left);
        question.MouseUp(beside, MouseButton.Left);
        question.MouseDown(beside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // Taken as an answer, it would have been "none of them", given by
        // somebody who pressed nothing at all.
        Assert.True(question.IsVisible, "A double press on no row put the question away.");
        Assert.False(asked.IsCompleted);

        question.MouseUp(beside, MouseButton.Left);
        question.Close();
        await asked.WaitAsync(Soon);
    }

    [AvaloniaTheory]
    [InlineData("Cancel")]
    [InlineData("Escape")]
    [InlineData("the cross")]
    public async Task Pick_one_put_away_without_choosing_is_none_of_them(string how)
    {
        // With the first row chosen, so that leaving is seen to choose nothing.
        (Window owner, Task<string?> asked) = AskedToPickOne(pickTheFirst: true);
        PickOne question = TheOneOpenOver<PickOne>(owner);

        switch (how)
        {
            case "Cancel":
                Press(question, question.Found<Button>("NoneOfThem"));
                break;
            case "Escape":
                Type(question, PhysicalKey.Escape);
                break;
            default:
                question.Close();
                break;
        }

        Assert.Null(await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task Pick_one_is_written_down_with_what_was_offered_and_what_was_taken()
    {
        Heard log = new();
        IKeepALog before = WorthAsking.Log;
        WorthAsking.Log = log;

        try
        {
            (Window owner, Task<string?> asked) = AskedToPickOne(pickTheFirst: false);
            PickOne question = TheOneOpenOver<PickOne>(owner);

            Assert.Equal(["Asked: Which drive? (D: HL-DT-ST BD-RE; E: ASUS DRW-24; F: a third one)"], log.Lines);

            Press(question, (Control)question.Found<ListBox>("Choices").ContainerFromIndex(1)!);
            Press(question, question.Found<Button>("TakeThatOne"));
            await asked.WaitAsync(Soon);

            Assert.Equal("Answered: E: ASUS DRW-24", log.Lines[1]);

            (owner, asked) = AskedToPickOne(pickTheFirst: true);
            TheOneOpenOver<PickOne>(owner).Close();
            await asked.WaitAsync(Soon);

            Assert.Equal("Answered: none of them", log.Lines[^1]);
        }
        finally
        {
            WorthAsking.Log = before;
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Both_are_drawn_on_the_page_and_kept_as_pictures(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        ThemeVariant? before = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = look;

        try
        {
            (Window owner, var asked) = AskedWhatElse(after: 2);
            WhatElseIsInThere whatElse = TheOneOpenOver<WhatElseIsInThere>(owner);

            Assert.Equal(WhatWasDrawn.Of("Page", look), whatElse.ColourAt(new Point(4, 4)));
            Keep(whatElse, $"what-else-is-in-there-{which.ToLowerInvariant()}.png");
            whatElse.Close();
            await asked.WaitAsync(Soon);

            (owner, Task<string?> picked) = AskedToPickOne(pickTheFirst: true);
            PickOne pickOne = TheOneOpenOver<PickOne>(owner);

            Assert.Equal(WhatWasDrawn.Of("Page", look), pickOne.ColourAt(new Point(4, 4)));
            Keep(pickOne, $"pick-one-{which.ToLowerInvariant()}.png");
            pickOne.Close();
            await picked.WaitAsync(Soon);
        }
        finally
        {
            Application.Current.RequestedThemeVariant = before;
        }
    }

    // ------------------------------------------------------------------ helpers

    private (Window Owner, Task<(GameFolder Game, IReadOnlyList<LeftBehind> AddOns, TheSameAnswers? ForTheRest)?> Asked)
        AskedWhatElse(int after = 0)
    {
        Window owner = AnOwner();
        var asked = WhatElseIsInThere.Ask(owner, _game, _left, after);
        Dispatcher.UIThread.RunJobs();

        return (owner, asked);
    }

    private static (Window Owner, Task<string?> Asked) AskedToPickOne(bool pickTheFirst)
    {
        Window owner = AnOwner();

        Task<string?> asked = PickOne.From(
            owner, "Which drive?", "Two of them hold a blank.", Drives, each => each, pickTheFirst);
        Dispatcher.UIThread.RunJobs();

        return (owner, asked);
    }

    private static Window AnOwner()
    {
        Window owner = new() { Width = 920, Height = 740 };
        owner.Show();
        Dispatcher.UIThread.RunJobs();

        return owner;
    }

    private static T TheOneOpenOver<T>(Window owner)
        where T : Window
    {
        Dispatcher.UIThread.RunJobs();

        return Assert.Single(owner.OwnedWindows.OfType<T>());
    }

    /// <summary>The words a shut dropdown shows.</summary>
    private static string? WhatItShows(ComboBox dropdown)
    {
        Dispatcher.UIThread.RunJobs();

        return dropdown.Part<ContentControl>("Chosen").GetVisualDescendants().OfType<TextBlock>().Single().Text;
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

    private static void Type(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        if (window.IsVisible)
        {
            window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Keep(Window window, string name)
    {
        string kept = Repository.File("artifacts", "the-look", name);
        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);

        Dispatcher.UIThread.RunJobs();

        using WriteableBitmap drawn = window.CaptureRenderedFrame()!;
        drawn.Save(kept, new PngBitmapEncoderOptions());
    }

    private sealed class Heard : IKeepALog
    {
        public List<string> Lines { get; } = [];

        public void Write(string line) => Lines.Add(line);
    }
}
