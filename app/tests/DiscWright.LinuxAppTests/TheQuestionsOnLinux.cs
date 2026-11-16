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
using DiscWright.Tests;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The two small windows that stand in front of the main one: a question
/// with two answers, and something said plainly.
/// </summary>
/// <remarks>
/// <para>
/// Each is opened the way the app opens it, over a window that is really
/// showing, and answered the way a person answers it: a button pressed with
/// the pointer, Enter, or the window shut.
/// </para>
/// <para>
/// On Windows the second of these is a message box, which a test cannot
/// read. Here a refusal in a box is read word for word.
/// </para>
/// </remarks>
public class TheQuestionsOnLinux
{
    /// <summary>
    /// How long an answer gets to arrive. It arrives at once or never: a
    /// window that did not shut leaves its question standing for ever, and a
    /// test that waited for ever would say nothing at all about why.
    /// </summary>
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

    private const string Headline = "Start a new disc?";
    private const string Because = "What is on the screen now goes: the games, the name, the artwork and the manual.";

    // ------------------------------------------------------------ a question

    [AvaloniaFact]
    public async Task A_question_says_what_it_asks_and_offers_both_answers_by_name()
    {
        (Window owner, Task<bool> asked) = Asked();
        WorthAsking question = TheOneOpenOver<WorthAsking>(owner);

        Assert.Equal("DiscWright", question.Title);
        Assert.Equal("WorthAsking", AutomationProperties.GetAutomationId(question));

        Assert.Equal(Headline, question.Found<TextBlock>("Headline").Text);
        Assert.Equal(Because, question.Found<TextBlock>("Because").Text);
        Assert.Equal("Keep what is here", question.Found<Button>("TheLikelyOne").Content);
        Assert.Equal("Start a new disc", question.Found<Button>("TheOtherWay").Content);

        question.Close();
        await asked.WaitAsync(Soon);
    }

    [AvaloniaFact]
    public async Task The_likely_answer_wears_the_accent_and_is_the_one_Enter_presses()
    {
        (Window owner, Task<bool> asked) = Asked();
        WorthAsking question = TheOneOpenOver<WorthAsking>(owner);

        Button likely = question.Found<Button>("TheLikelyOne");
        Button other = question.Found<Button>("TheOtherWay");

        Assert.Same(Application.Current!.FindResource("TheButton"), likely.Theme);
        Assert.Same(Application.Current!.FindResource("Ordinary"), other.Theme);
        Assert.True(likely.IsDefault);
        Assert.False(other.IsDefault);

        // The likely one is on the right, where the eye ends.
        Assert.True(likely.TranslatePoint(default, question)!.Value.X > other.TranslatePoint(default, question)!.Value.X);

        Type(question, PhysicalKey.Enter);

        Assert.True(await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task Pressing_the_likely_answer_takes_it()
    {
        (Window owner, Task<bool> asked) = Asked();
        WorthAsking question = TheOneOpenOver<WorthAsking>(owner);

        Press(question, question.Found<Button>("TheLikelyOne"));

        Assert.True(await asked.WaitAsync(Soon));
        Assert.False(question.IsVisible);
    }

    [AvaloniaFact]
    public async Task Pressing_the_other_answer_takes_that_one()
    {
        (Window owner, Task<bool> asked) = Asked();
        WorthAsking question = TheOneOpenOver<WorthAsking>(owner);

        Press(question, question.Found<Button>("TheOtherWay"));

        Assert.False(await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task Shutting_the_question_is_the_other_answer_and_never_the_likely_one()
    {
        (Window owner, Task<bool> asked) = Asked();

        // The cross on the title bar. Where the likely answer destroys
        // something, a window shut in a hurry must not have said yes.
        TheOneOpenOver<WorthAsking>(owner).Close();

        Assert.False(await asked.WaitAsync(Soon));
    }

    [AvaloniaFact]
    public async Task A_question_and_its_answer_are_written_down()
    {
        Heard log = new();
        IKeepALog before = WorthAsking.Log;
        WorthAsking.Log = log;

        try
        {
            (Window owner, Task<bool> asked) = Asked();

            Assert.Equal(["Asked: Start a new disc?"], log.Lines);

            WorthAsking question = TheOneOpenOver<WorthAsking>(owner);
            Press(question, question.Found<Button>("TheOtherWay"));
            await asked.WaitAsync(Soon);

            Assert.Equal(["Asked: Start a new disc?", "Answered: Start a new disc"], log.Lines);
        }
        finally
        {
            WorthAsking.Log = before;
        }
    }

    [AvaloniaFact]
    public async Task A_question_is_560_wide_as_tall_as_its_words_and_cannot_be_resized()
    {
        (Window owner, Task<bool> asked) = Asked();
        WorthAsking question = TheOneOpenOver<WorthAsking>(owner);

        Assert.Equal(560, question.Bounds.Width);
        Assert.False(question.CanResize);
        Assert.False(question.ShowInTaskbar);
        Assert.Equal(WindowStartupLocation.CenterOwner, question.WindowStartupLocation);

        // As tall as what is in it: the buttons are on it, with the margin
        // under them and no more.
        Button likely = question.Found<Button>("TheLikelyOne");
        double underTheButtons = likely.TranslatePoint(new Point(0, likely.Bounds.Height), question)!.Value.Y;

        Assert.Equal(18, question.Bounds.Height - underTheButtons, 1.0);

        question.Close();
        await asked.WaitAsync(Soon);
    }

    [AvaloniaFact]
    public async Task A_long_question_wraps_and_the_window_grows_to_hold_it()
    {
        Window owner = AnOwner();

        Task<bool> brief = WorthAsking.Ask(owner, Headline, "Short.", "Yes", "No");
        Dispatcher.UIThread.RunJobs();
        WorthAsking first = TheOneOpenOver<WorthAsking>(owner);
        double heightOfTheBriefOne = first.Bounds.Height;
        first.Close();
        await brief.WaitAsync(Soon);

        Task<bool> atLength = WorthAsking.Ask(owner, Headline, string.Join(" ", Enumerable.Repeat(Because, 6)), "Yes", "No");
        Dispatcher.UIThread.RunJobs();
        WorthAsking second = TheOneOpenOver<WorthAsking>(owner);

        TextBlock words = second.Found<TextBlock>("Because");

        Assert.True(words.Bounds.Width <= 560 - 48, $"The words are {words.Bounds.Width} wide in a window with 512 of room.");
        Assert.True(second.Bounds.Height > heightOfTheBriefOne + 40, "Six times the words, and the window is no taller.");
        Assert.True(second.Found<Button>("TheLikelyOne").CanBeSeen());

        second.Close();
        await atLength.WaitAsync(Soon);
    }

    // ------------------------------------------------------- something said

    [AvaloniaFact]
    public async Task A_refusal_is_said_whole_the_headline_and_the_reason()
    {
        Window owner = AnOwner();
        Refusal refusal = Refusals.BurningIsNotInThisVersion();

        Task said = SayingSo.Explain(owner, refusal);
        Dispatcher.UIThread.RunJobs();
        SayingSo box = TheOneOpenOver<SayingSo>(owner);

        Assert.Equal("DiscWright", box.Title);
        Assert.Equal("SayingSo", AutomationProperties.GetAutomationId(box));
        Assert.Equal(refusal.Headline, box.Found<TextBlock>("Headline").Text);
        Assert.Equal(refusal.Because, box.Found<SelectableTextBlock>("Because").Text);
        Assert.True(box.Found<SelectableTextBlock>("Because").CanBeSeen());

        box.Close();
        await said.WaitAsync(Soon);
    }

    [AvaloniaFact]
    public async Task A_refusal_that_is_all_headline_leaves_no_gap_where_a_reason_would_be()
    {
        Window owner = AnOwner();

        Task said = SayingSo.Explain(owner, new Refusal("A disc needs a name", string.Empty));
        Dispatcher.UIThread.RunJobs();
        SayingSo box = TheOneOpenOver<SayingSo>(owner);

        Assert.False(box.Found<SelectableTextBlock>("Because").IsVisible);

        box.Close();
        await said.WaitAsync(Soon);
    }

    [AvaloniaTheory]
    [InlineData("the button")]
    [InlineData("Enter")]
    [InlineData("Escape")]
    [InlineData("the cross")]
    public async Task Something_said_goes_away_however_it_is_answered(string how)
    {
        Window owner = AnOwner();

        Task said = SayingSo.Tell(owner, "No browser would open", "The page is:\n\nhttps://github.com/lazardjokovic/discwright/releases");
        Dispatcher.UIThread.RunJobs();
        SayingSo box = TheOneOpenOver<SayingSo>(owner);

        Button ok = box.Found<Button>("Understood");
        Assert.Equal("OK", ok.Content);

        switch (how)
        {
            case "the button":
                Press(box, ok);
                break;
            case "Enter":
                Type(box, PhysicalKey.Enter);
                break;
            case "Escape":
                Type(box, PhysicalKey.Escape);
                break;
            default:
                box.Close();
                break;
        }

        Dispatcher.UIThread.RunJobs();
        await said.WaitAsync(Soon);

        Assert.False(box.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Both_are_drawn_on_the_page_in_the_look_and_kept_as_pictures(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        ThemeVariant? before = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = look;

        try
        {
            Window owner = AnOwner();

            Task<bool> asked = WorthAsking.Ask(
                owner,
                Headline,
                Because + " Nothing already built or burned is touched.",
                "Keep what is here",
                "Start a new disc");
            Dispatcher.UIThread.RunJobs();
            WorthAsking question = TheOneOpenOver<WorthAsking>(owner);

            Assert.Equal(WhatWasDrawn.Of("Page", look), question.ColourAt(new Point(4, 4)));
            Keep(question, $"a-question-{which.ToLowerInvariant()}.png");
            question.Close();
            await asked.WaitAsync(Soon);

            Task said = SayingSo.Explain(owner, Refusals.BurningIsNotInThisVersion());
            Dispatcher.UIThread.RunJobs();
            SayingSo box = TheOneOpenOver<SayingSo>(owner);

            Assert.Equal(WhatWasDrawn.Of("Page", look), box.ColourAt(new Point(4, 4)));
            Keep(box, $"a-refusal-{which.ToLowerInvariant()}.png");
            box.Close();
            await said.WaitAsync(Soon);
        }
        finally
        {
            Application.Current.RequestedThemeVariant = before;
        }
    }

    private static (Window Owner, Task<bool> Asked) Asked()
    {
        Window owner = AnOwner();

        Task<bool> asked = WorthAsking.Ask(owner, Headline, Because, "Keep what is here", "Start a new disc");
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

    /// <summary>The one window of that kind standing in front of another.</summary>
    private static T TheOneOpenOver<T>(Window owner)
        where T : Window =>
        Assert.Single(owner.OwnedWindows.OfType<T>());

    private static void Press(Window window, Button button)
    {
        Point middle = button.MiddleIn(window);

        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// One key, down and up. The key going down is what a window answers,
    /// and a window that shut on it is not there to be told it came up.
    /// </summary>
    private static void Type(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        if (window.IsVisible)
        {
            window.KeyReleaseQwerty(key, RawInputModifiers.None);
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
