using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using DiscWright.LinuxApp;
using DiscWright.Tests;
using DiscWright.ViewModels;

using SkiaSharp;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The window for everything that goes on a disc and is not a game.
/// </summary>
/// <remarks>
/// Opened from the main window by its button, the way somebody opens it.
/// The pictures are real files, so the thumbnail is a real picture read off
/// a real disk, and the file that is not a picture really is not one. Only
/// the desktop is answered for: which file somebody picks.
/// </remarks>
public sealed class WhatElseGoesOnItOnLinux : IDisposable
{
    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-artwork-" + Guid.NewGuid().ToString("N"));

    public WhatElseGoesOnItOnLinux() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    // ------------------------------------------------------------ getting to it

    [AvaloniaFact]
    public void The_button_on_the_main_window_opens_it_and_Done_puts_it_away()
    {
        (MainWindow main, MakeADisc screen, _) = TheMainWindow();

        Button open = main.Found<Button>("OpenWhatElseGoesOnIt");

        Assert.Equal("Artwork, music and extras", open.Content);
        Assert.Equal(screen.WhatElseIsChosen, main.Found<TextBlock>("WhatElseIsChosen").Text);

        Press(main, open);

        WhatElseGoesOnIt window = Assert.Single(main.OwnedWindows.OfType<WhatElseGoesOnIt>());

        Assert.Equal("What else goes on it", window.Title);
        Assert.Equal(680, window.Bounds.Width);
        Assert.Equal(700, window.Bounds.Height);
        Assert.False(window.CanResize);

        Button done = window.Found<Button>("Done");
        Assert.True(done.IsDefault);

        Press(window, done);

        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void With_nothing_chosen_every_one_of_the_seven_says_so()
    {
        (WhatElseGoesOnIt window, _, _) = Opened();

        foreach (string id in new[]
        {
            "TheDiscIcon", "TheBackground", "TheMusic", "TheManual", "TheExtrasFolder", "TheCover", "TheDiscFace",
        })
        {
            Assert.Equal("Nothing chosen", window.Found<TextBlock>(id).Text);
        }

        Assert.False(window.Found<TextBlock>("WhyNotThatPicture").IsVisible);
        Assert.False(window.Found<TextBlock>("WhatTheColourSays").IsVisible);
    }

    // ------------------------------------------------------------- the pictures

    [AvaloniaTheory]
    [InlineData("ChooseTheDiscIcon", "ClearTheDiscIcon", "TheDiscIcon", "TheDiscIconPicture", "The icon the disc wears")]
    [InlineData("ChooseTheBackground", "ClearTheBackground", "TheBackground", "TheBackgroundPicture", "The picture the menu is drawn on")]
    [InlineData("ChooseTheCover", "ClearTheCover", "TheCover", "TheCoverPicture", "The picture for the printed cover")]
    [InlineData("ChooseTheDiscFace", "ClearTheDiscFace", "TheDiscFace", "TheDiscFacePicture", "The picture for the printed disc face")]
    public void A_picture_chosen_is_named_and_shown_and_cleared_again(
        string choose, string clear, string named, string shown, string asked)
    {
        (WhatElseGoesOnIt window, MakeADisc screen, ADesktopThatAnswers desktop) = Opened();

        string picture = APicture("art.png", 600, 600);
        desktop.WillPick = picture;

        Press(window, window.Found<Button>(choose));

        // Asked for a picture, by a title that says what it is for.
        (string title, IReadOnlyList<AKindOfFile> kinds) = Assert.Single(desktop.AskedFor);

        Assert.Equal(asked, title);
        Assert.Equal("Pictures", kinds[0].Name);
        Assert.Contains("*.png", kinds[0].Patterns);
        Assert.Contains("*.jpg", kinds[0].Patterns);
        Assert.Equal("Anything", kinds[1].Name);

        // Named, and shown: a picture is only really confirmed by seeing it.
        Assert.Equal(picture, window.Found<TextBlock>(named).Text);
        Assert.NotNull(window.Found<Image>(shown).Source);

        // The screen has it under the same name the window does.
        Assert.Equal(picture, WhatTheScreenHolds(screen, named));

        Press(window, window.Found<Button>(clear));

        Assert.Equal("Nothing chosen", window.Found<TextBlock>(named).Text);
        Assert.Null(window.Found<Image>(shown).Source);
        Assert.Null(WhatTheScreenHolds(screen, named));
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void A_file_that_is_not_a_picture_is_not_taken_and_the_window_says_why_at_once(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        WhatWasDrawn.Wearing(look, () =>
        {
            (WhatElseGoesOnIt window, MakeADisc screen, ADesktopThatAnswers desktop) = Opened();

            string notOne = Path.Combine(_room, "notes.png");
            File.WriteAllText(notOne, "this is not a picture, whatever it is called");
            desktop.WillPick = notOne;

            Press(window, window.Found<Button>("ChooseTheDiscIcon"));

            // Not taken.
            Assert.Null(screen.DiscIcon);
            Assert.Equal("Nothing chosen", window.Found<TextBlock>("TheDiscIcon").Text);

            // And said now, in a worry's ink, rather than at the end of a
            // build in somebody else's words.
            TextBlock why = window.Found<TextBlock>("WhyNotThatPicture");

            Assert.True(why.CanBeSeen());
            Assert.Equal(screen.WhyNotThatPicture, why.Text);
            Assert.Contains("cannot be read as an image", why.Text, StringComparison.Ordinal);
            Assert.Equal(WhatWasDrawn.Of("WorryInk", look), why.Foreground.AsColour());

            // A good one after it is taken, and the reason goes.
            desktop.WillPick = APicture("art.png", 300, 300);
            Press(window, window.Found<Button>("ChooseTheDiscIcon"));

            Assert.NotNull(screen.DiscIcon);
            Assert.False(why.IsVisible);
        });
    }

    [AvaloniaFact]
    public void Choosing_and_picking_nothing_leaves_what_was_chosen()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, ADesktopThatAnswers desktop) = Opened();

        desktop.WillPick = APicture("art.png", 300, 300);
        Press(window, window.Found<Button>("ChooseTheCover"));

        string kept = screen.Cover!;

        desktop.WillPick = null;
        Press(window, window.Found<Button>("ChooseTheCover"));

        Assert.Equal(kept, screen.Cover);
    }

    [AvaloniaFact]
    public void A_picture_shown_back_is_not_held_open()
    {
        (WhatElseGoesOnIt window, _, ADesktopThatAnswers desktop) = Opened();

        string picture = APicture("art.png", 300, 300);
        desktop.WillPick = picture;

        Press(window, window.Found<Button>("ChooseTheDiscIcon"));
        Assert.NotNull(window.Found<Image>("TheDiscIconPicture").Source);

        // The first thing somebody does after choosing the wrong picture is
        // try to replace it. Windows will not let go of a file something
        // holds, so only a Windows run can fail here.
        File.Delete(picture);

        Assert.False(File.Exists(picture));
    }

    // ---------------------------------------------------------- the background

    [AvaloniaFact]
    public void Stretch_to_fit_is_a_tick_that_reaches_the_screen()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, _) = Opened();

        CheckBox stretch = window.Found<CheckBox>("BackgroundAsIs");

        Assert.Equal("Stretch to fit", stretch.Content);

        bool before = screen.BackgroundAsIs;
        Press(window, stretch);

        Assert.Equal(!before, screen.BackgroundAsIs);
    }

    [AvaloniaFact]
    public void A_colour_is_picked_from_the_swatches_and_shown_where_the_picture_would_be()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, _) = Opened();

        Button[] swatches = [.. window.Found<ItemsControl>("TheColoursOffered").GetVisualDescendants().OfType<Button>()];

        // One for each colour the screen offers, each answering to its colour.
        Assert.Equal(screen.TheColoursOffered.Count, swatches.Length);
        Assert.Equal(
            screen.TheColoursOffered,
            swatches.Select(Avalonia.Automation.AutomationProperties.GetName));

        string second = screen.TheColoursOffered[1];
        Button swatch = swatches[1];

        // Drawn in the colour it stands for.
        Border face = swatch.GetVisualDescendants().OfType<Border>().First();
        Assert.Equal(Color.Parse(second), face.Background.AsColour());

        Press(window, swatch);

        Assert.Equal(second, screen.BackgroundColour);
        Assert.Equal(second, window.Found<TextBox>("BackgroundColourText").Text);

        // Shown where the picture would be.
        Border backdrop = window.Found<Border>("TheBackgroundColour");

        Assert.True(backdrop.CanBeSeen());
        Assert.Equal(Color.Parse(second), backdrop.Background.AsColour());
        Assert.Equal(Color.Parse(second), window.ColourAt(backdrop.MiddleIn(window)));
    }

    [AvaloniaFact]
    public void A_colour_and_a_picture_are_never_both_the_background()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, ADesktopThatAnswers desktop) = Opened();

        desktop.WillPick = APicture("backdrop.png", 760, 480);
        Press(window, window.Found<Button>("ChooseTheBackground"));

        Assert.NotNull(screen.Background);

        // A colour takes the picture off.
        Press(window, window.Found<ItemsControl>("TheColoursOffered").GetVisualDescendants().OfType<Button>().First());

        Assert.Null(screen.Background);
        Assert.Null(window.Found<Image>("TheBackgroundPicture").Source);
        Assert.True(window.Found<Border>("TheBackgroundColour").IsVisible);

        // And a picture takes the colour off.
        Press(window, window.Found<Button>("ChooseTheBackground"));

        Assert.NotNull(screen.Background);
        Assert.False(window.Found<Border>("TheBackgroundColour").IsVisible);
    }

    [AvaloniaFact]
    public void A_colour_typed_is_taken_as_it_is_typed_and_a_bad_one_is_said_to_be()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, _) = Opened();

        TextBox typed = window.Found<TextBox>("BackgroundColourText");

        Press(window, typed);
        window.KeyTextInput("336699");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("336699", screen.BackgroundColourText);
        Assert.NotNull(screen.BackgroundColour);
        Assert.False(window.Found<TextBlock>("WhatTheColourSays").IsVisible);

        window.KeyTextInput("zz");
        Dispatcher.UIThread.RunJobs();

        TextBlock said = window.Found<TextBlock>("WhatTheColourSays");

        Assert.True(said.CanBeSeen());
        Assert.Equal(screen.WhatTheColourSays, said.Text);
    }

    // --------------------------------------------------------- the rest of it

    [AvaloniaFact]
    public void Music_is_asked_for_as_music_and_named_and_cleared()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, ADesktopThatAnswers desktop) = Opened();

        string tune = Path.Combine(_room, "theme.mp3");
        File.WriteAllBytes(tune, new byte[2000]);
        desktop.WillPick = tune;

        Press(window, window.Found<Button>("ChooseTheMusic"));

        (string title, IReadOnlyList<AKindOfFile> kinds) = Assert.Single(desktop.AskedFor);

        Assert.Equal("What plays behind the menu", title);
        Assert.Equal("Music", kinds[0].Name);
        Assert.Equal(["*.mp3", "*.wav", "*.wma", "*.m4a"], kinds[0].Patterns);

        Assert.Equal(tune, screen.Music);
        Assert.Equal(tune, window.Found<TextBlock>("TheMusic").Text);

        Press(window, window.Found<Button>("ClearTheMusic"));

        Assert.Null(screen.Music);
        Assert.Equal("Nothing chosen", window.Found<TextBlock>("TheMusic").Text);
    }

    [AvaloniaFact]
    public void A_manual_is_asked_for_as_a_manual_and_named_and_cleared()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, ADesktopThatAnswers desktop) = Opened();

        string manual = Path.Combine(_room, "Manual.pdf");
        File.WriteAllBytes(manual, new byte[2000]);
        desktop.WillPick = manual;

        Press(window, window.Found<Button>("ChooseTheManual"));

        (string title, IReadOnlyList<AKindOfFile> kinds) = Assert.Single(desktop.AskedFor);

        Assert.Equal("The manual", title);
        Assert.Equal("A manual", kinds[0].Name);
        Assert.Contains("*.pdf", kinds[0].Patterns);

        Assert.Equal(manual, screen.Manual);
        Assert.Equal(manual, window.Found<TextBlock>("TheManual").Text);

        Press(window, window.Found<Button>("ClearTheManual"));

        Assert.Null(screen.Manual);
    }

    [AvaloniaFact]
    public void Extras_are_a_folder_asked_for_as_one_and_named_and_cleared()
    {
        (WhatElseGoesOnIt window, MakeADisc screen, ADesktopThatAnswers desktop) = Opened();

        string extras = Path.Combine(_room, "extras");
        Directory.CreateDirectory(extras);
        desktop.WillPickFolders = [extras];

        Press(window, window.Found<Button>("ChooseTheExtras"));

        Assert.Equal(["A folder of extras to put on the disc"], desktop.AskedForFolders);
        Assert.Equal(extras, screen.ExtrasFolder);
        Assert.Equal(extras, window.Found<TextBlock>("TheExtrasFolder").Text);

        Press(window, window.Found<Button>("ClearTheExtras"));

        Assert.Null(screen.ExtrasFolder);
    }

    [AvaloniaFact]
    public void What_was_chosen_is_said_on_the_main_window_once_the_window_is_put_away()
    {
        (MainWindow main, MakeADisc screen, ADesktopThatAnswers desktop) = TheMainWindow();

        string before = screen.WhatElseIsChosen;

        Press(main, main.Found<Button>("OpenWhatElseGoesOnIt"));
        WhatElseGoesOnIt window = Assert.Single(main.OwnedWindows.OfType<WhatElseGoesOnIt>());

        desktop.WillPick = APicture("art.png", 300, 300);
        Press(window, window.Found<Button>("ChooseTheDiscIcon"));
        Press(window, window.Found<Button>("Done"));

        Assert.NotEqual(before, screen.WhatElseIsChosen);
        Assert.Equal(screen.WhatElseIsChosen, main.Found<TextBlock>("WhatElseIsChosen").Text);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_window_is_kept_as_a_picture_with_everything_chosen(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        WhatWasDrawn.Wearing(look, () =>
        {
            (WhatElseGoesOnIt window, MakeADisc screen, _) = Opened();

            screen.DiscIcon = APicture("icon.png", 256, 256, new SKColor(40, 160, 150));
            screen.Background = APicture("backdrop.png", 760, 480, new SKColor(60, 70, 120));
            screen.Cover = APicture("cover.png", 500, 700, new SKColor(170, 80, 60));
            screen.DiscFace = APicture("face.png", 600, 600, new SKColor(200, 170, 60));
            screen.Music = Path.Combine(_room, "theme.mp3");
            Dispatcher.UIThread.RunJobs();

            string kept = Repository.File("artifacts", "the-look", $"what-else-goes-on-it-{which.ToLowerInvariant()}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(kept)!);

            using Avalonia.Media.Imaging.WriteableBitmap drawn = window.CaptureRenderedFrame()!;
            drawn.Save(kept, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

            Assert.Equal(WhatWasDrawn.Of("Page", look), window.ColourAt(new Point(4, 4)));

            // The icon's own colour is on the window, where its thumbnail is.
            Image icon = window.Found<Image>("TheDiscIconPicture");
            Assert.Equal(Color.FromRgb(40, 160, 150), window.ColourAt(icon.MiddleIn(window)));
        });
    }

    // ------------------------------------------------------------------ helpers

    private static string? WhatTheScreenHolds(MakeADisc screen, string named) => named switch
    {
        "TheDiscIcon" => screen.DiscIcon,
        "TheBackground" => screen.Background,
        "TheCover" => screen.Cover,
        "TheDiscFace" => screen.DiscFace,
        _ => throw new ArgumentOutOfRangeException(nameof(named)),
    };

    private static (MainWindow Main, MakeADisc Screen, ADesktopThatAnswers Desktop) TheMainWindow()
    {
        ADesktopThatAnswers desktop = new();
        MakeADisc screen = TheWindow.AScreen();

        return (TheWindow.Showing(screen, desktop, new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows)), screen, desktop);
    }

    /// <summary>The window, opened from the main one by its button.</summary>
    private static (WhatElseGoesOnIt Window, MakeADisc Screen, ADesktopThatAnswers Desktop) Opened()
    {
        (MainWindow main, MakeADisc screen, ADesktopThatAnswers desktop) = TheMainWindow();

        Press(main, main.Found<Button>("OpenWhatElseGoesOnIt"));

        return (Assert.Single(main.OwnedWindows.OfType<WhatElseGoesOnIt>()), screen, desktop);
    }

    /// <summary>A real picture on a real disk, of one flat colour.</summary>
    private string APicture(string name, int width, int height, SKColor? colour = null)
    {
        string path = Path.Combine(_room, name);

        using SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(colour ?? new SKColor(40, 160, 150));
        }

        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());

        return path;
    }

    private static void Press(Window window, Control control)
    {
        Dispatcher.UIThread.RunJobs();

        // What is pressed may be down a list that scrolls.
        control.BringIntoView();
        Dispatcher.UIThread.RunJobs();

        Point middle = control.MiddleIn(window);

        window.MouseMove(middle);
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

    }
}
