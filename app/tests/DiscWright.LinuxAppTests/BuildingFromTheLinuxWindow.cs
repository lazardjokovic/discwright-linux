using Avalonia;
using Avalonia.Controls;
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

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The right hand side of the window and its bottom line: what the disc is
/// called, how big, where it goes, what the window says it will make or why
/// it will not, and the button that makes it.
/// </summary>
/// <remarks>
/// <para>
/// The last test here presses Build it and waits for a real image, made by
/// the real xorriso out of a real folder. Everything under the window has
/// tests of its own. This is the one place the window, the screen, the
/// builder and xorriso meet, started by a pointer on a button.
/// </para>
/// </remarks>
public sealed class BuildingFromTheLinuxWindow : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-building-" + Guid.NewGuid().ToString("N"));

    public BuildingFromTheLinuxWindow() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    // ---------------------------------------------------------------- the name

    [AvaloniaFact]
    public void The_name_typed_reaches_the_screen_in_capitals_as_it_is_typed()
    {
        (MainWindow window, MakeADisc screen) = AWindow();
        TextBox name = window.Found<TextBox>("Label");

        Press(window, name);
        window.KeyTextInput("Hollow Knight");
        Dispatcher.UIThread.RunJobs();

        // This PC shows a disc's name in capitals, so that is how it is kept.
        Assert.Equal("HOLLOW KNIGHT", name.Text);
        Assert.Equal("HOLLOW KNIGHT", screen.Label);

        // And typing carries on from where it was, not from the start.
        window.KeyTextInput(" 2");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("HOLLOW KNIGHT 2", screen.Label);
    }

    [AvaloniaFact]
    public void The_name_stops_at_thirty_two_letters()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        Press(window, window.Found<TextBox>("Label"));
        window.KeyTextInput(new string('a', 40));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new string('A', 32), screen.Label);
    }

    [AvaloniaFact]
    public void A_name_the_screen_gives_shows_on_the_window()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        screen.Add(AGame());
        Dispatcher.UIThread.RunJobs();

        // Named after the first game, as the screen does it.
        Assert.Equal(screen.Label, window.Found<TextBox>("Label").Text);
        Assert.NotEqual(string.Empty, screen.Label);
    }

    // ---------------------------------------------------------------- the size

    [AvaloniaFact]
    public void The_size_lists_what_the_screen_offers_each_answering_to_its_words()
    {
        (MainWindow window, MakeADisc screen) = AWindow();
        ComboBox size = window.Found<ComboBox>("DiscSize");

        Assert.Equal(screen.DiscSizes.Count, size.ItemCount);
        Assert.Same(screen.TheSizeChosen, size.SelectedItem);

        // What is chosen is said in the screen's words on the shut box.
        Assert.Equal(
            screen.TheSizeChosen!.InWords,
            size.Part<ContentControl>("Chosen").GetVisualDescendants().OfType<TextBlock>().Single().Text);

        Press(window, size);
        Assert.True(size.IsDropDownOpen);

        for (int at = 0; at < screen.DiscSizes.Count; at++)
        {
            Assert.Equal(
                screen.DiscSizes[at].InWords,
                Avalonia.Automation.AutomationProperties.GetName(size.ContainerFromIndex(at)!));
        }
    }

    [AvaloniaFact]
    public void A_size_picked_with_the_pointer_is_the_size_the_screen_plans_for()
    {
        (MainWindow window, MakeADisc screen) = AWindow();
        ComboBox size = window.Found<ComboBox>("DiscSize");

        int other = screen.DiscSizes.ToList().IndexOf(screen.TheSizeChosen!) == 0 ? 1 : 0;

        Press(window, size);

        Control row = (Control)size.ContainerFromIndex(other)!;
        TopLevel list = TopLevel.GetTopLevel(row)!;
        Point onIt = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), list)!.Value;

        list.MouseMove(onIt);
        list.MouseDown(onIt, MouseButton.Left);
        list.MouseUp(onIt, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(screen.DiscSizes[other], screen.TheSizeChosen);
    }

    [AvaloniaFact]
    public void Splitting_is_a_tick_beside_the_size_and_what_it_would_do_is_said_under_it_only_when_there_is_something_to_say()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        CheckBox split = window.Found<CheckBox>("CutFiles");
        TextBlock would = window.Found<TextBlock>("WhatSplittingWouldDo");

        Assert.Equal("Split a file too big for one disc", split.Content);
        Assert.Equal(screen.CutFiles, split.IsChecked);
        Assert.Equal(screen.WhatSplittingWouldDo.Length > 0, would.IsVisible);

        bool before = screen.CutFiles;
        Press(window, split);

        Assert.Equal(!before, screen.CutFiles);
    }

    // ------------------------------------------------------------ where it goes

    [AvaloniaFact]
    public void Where_it_goes_can_be_typed_or_browsed_for()
    {
        ADesktopThatAnswers desktop = new() { WillPickFolders = [Path.Combine(_room, "picked")] };
        (MainWindow window, MakeADisc screen) = AWindow(desktop);

        TextBox where = window.Found<TextBox>("OutputFolder");

        Press(window, where);
        window.KeyTextInput("/somewhere/typed");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("/somewhere/typed", screen.OutputFolder);

        Press(window, window.Found<Button>("ChooseWhereItGoes"));

        Assert.Equal(["Where should the discs go?"], desktop.AskedForFolders);
        Assert.Equal(Path.Combine(_room, "picked"), screen.OutputFolder);
        Assert.Equal(Path.Combine(_room, "picked"), where.Text);
    }

    [AvaloniaFact]
    public void Browsing_and_picking_nothing_leaves_where_it_goes_alone()
    {
        (MainWindow window, MakeADisc screen) = AWindow(new ADesktopThatAnswers());

        screen.OutputFolder = "/already/here";
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("ChooseWhereItGoes"));

        Assert.Equal("/already/here", screen.OutputFolder);
    }

    // ------------------------------------------------------------- more options

    [AvaloniaFact]
    public void More_options_is_a_drawer_shut_to_begin_with_holding_four_ticks()
    {
        (MainWindow window, MakeADisc screen) = AWindow();
        Expander more = window.Found<Expander>("MoreOptions");

        Assert.False(more.IsExpanded);

        // Shut, there is nothing in it on the window at all.
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<CheckBox>(),
            each => Avalonia.Automation.AutomationProperties.GetAutomationId(each) == "Menu" && each.CanBeSeen());

        Press(window, more.Part<TextBlock>("Words"));

        Assert.True(more.IsExpanded);

        Assert.Equal("A menu that opens when the disc goes in", window.Found<CheckBox>("Menu").Content);
        Assert.Equal("A Start Here file at the top of the disc", window.Found<CheckBox>("StartHere").Content);
        Assert.Equal("A list of what every file should be", window.Found<CheckBox>("Checksums").Content);

        Assert.Equal(screen.Menu, window.Found<CheckBox>("Menu").IsChecked);
        Assert.Equal(screen.StartHere, window.Found<CheckBox>("StartHere").IsChecked);
        Assert.Equal(screen.Checksums, window.Found<CheckBox>("Checksums").IsChecked);

        // And the one the Windows window does not have yet.
        Assert.Equal("Its name and icon on Linux", window.Found<CheckBox>("LinuxNameAndIcon").Content);
        Assert.Equal(screen.LinuxNameAndIcon, window.Found<CheckBox>("LinuxNameAndIcon").IsChecked);

        Assert.Equal(
            ["Menu", "StartHere", "Checksums", "LinuxNameAndIcon"],
            more.GetVisualDescendants().OfType<CheckBox>().Select(Avalonia.Automation.AutomationProperties.GetAutomationId));

        // Not here: the older ways of laying a disc out are how this one is
        // laid out to begin with, so there is nothing to choose.
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<CheckBox>(),
            each => Avalonia.Automation.AutomationProperties.GetAutomationId(each) == "LegacyFileSystems");
    }

    [AvaloniaFact]
    public void Start_Here_is_dead_without_a_menu_for_it_to_open()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        Press(window, window.Found<Expander>("MoreOptions").Part<TextBlock>("Words"));

        CheckBox menu = window.Found<CheckBox>("Menu");
        CheckBox startHere = window.Found<CheckBox>("StartHere");

        Assert.True(screen.Menu);
        Assert.True(startHere.IsEnabled);

        Press(window, menu);

        Assert.False(screen.Menu);
        Assert.False(startHere.IsEnabled);

        Press(window, menu);

        Assert.True(startHere.IsEnabled);
    }

    [AvaloniaFact]
    public void Each_tick_in_the_drawer_reaches_the_screen()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        Press(window, window.Found<Expander>("MoreOptions").Part<TextBlock>("Words"));

        bool checksums = screen.Checksums;
        Press(window, window.Found<CheckBox>("Checksums"));
        Assert.Equal(!checksums, screen.Checksums);

        bool startHere = screen.StartHere;
        Press(window, window.Found<CheckBox>("StartHere"));
        Assert.Equal(!startHere, screen.StartHere);

        bool forLinux = screen.LinuxNameAndIcon;
        Press(window, window.Found<CheckBox>("LinuxNameAndIcon"));
        Assert.Equal(!forLinux, screen.LinuxNameAndIcon);
    }

    [AvaloniaFact]
    public void A_new_disc_here_starts_with_its_name_and_icon_for_Linux_on()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        // On here and off on Windows, the owner's word of 2026-11-15. The
        // ordinary options, which are what Windows starts with, say off.
        Assert.True(screen.LinuxNameAndIcon);
        Assert.False(new DiscOptions().LinuxNameAndIcon);

        Press(window, window.Found<Expander>("MoreOptions").Part<TextBlock>("Words"));

        CheckBox tick = window.Found<CheckBox>("LinuxNameAndIcon");

        Assert.True(tick.IsChecked);
        Assert.True(tick.IsEnabled);

        // What the hover promises is what was seen and no more: which
        // desktops read the file, and that Windows is left as it was.
        string hover = ((TextBlock)ToolTip.GetTip(tick)!).Text!;

        Assert.Contains(".xdg-volume-info", hover, StringComparison.Ordinal);
        Assert.Contains("Windows reads neither", hover, StringComparison.Ordinal);
        Assert.DoesNotContain("every", hover, StringComparison.OrdinalIgnoreCase);

        // The one thing that was seen to change: GNOME's file manager wrote
        // the full name where it had written the volume id.
        Assert.Contains("GNOME's file manager then shows the disc by its full name", hover, StringComparison.Ordinal);

        // And where it does nothing, which was seen too: Dolphin and Thunar
        // showed the same disc by its volume id, with and without the file.
        Assert.Contains("KDE's and XFCE's file managers show the short name either way", hover, StringComparison.Ordinal);

        // No picture is promised. It said "with its picture" once, because
        // GIO hands the picture over when asked, and no file manager that
        // was tried draws it. What is said of the picture is that.
        Assert.Contains("none of the three drew the picture", hover, StringComparison.Ordinal);
        Assert.DoesNotContain("with its picture", hover, StringComparison.Ordinal);
        Assert.DoesNotContain("plain icon", hover, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void A_screen_told_the_tick_is_off_shows_it_off()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        Press(window, window.Found<Expander>("MoreOptions").Part<TextBlock>("Words"));

        // What opening a project that was built without it does.
        screen.LinuxNameAndIcon = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.Found<CheckBox>("LinuxNameAndIcon").IsChecked);
    }

    // ---------------------------------------------------------- the bottom line

    [AvaloniaFact]
    public void The_bottom_line_says_what_will_be_made_and_does_not_promise_a_burn()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        Assert.Equal(screen.WhatItWillMake, window.Found<TextBlock>("WhatItWillMake").Text);

        string means = window.Found<TextBlock>("WhatTheButtonsMean").Text!;

        // The Windows window says here that burning writes a disc. This
        // DiscWright does not burn yet, and says what will.
        Assert.Contains("Burning is not in this version yet", means, StringComparison.Ordinal);
        Assert.Contains("K3b", means, StringComparison.Ordinal);
        Assert.DoesNotContain("this PC", means, StringComparison.Ordinal);

        // No button for what it cannot do.
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Button>(),
            each => Avalonia.Automation.AutomationProperties.GetAutomationId(each) is "BurnIt" or "EraseADisc");

        // Nothing is running, so nothing says what is being done.
        Assert.False(window.Found<TextBlock>("Doing").IsVisible);
        Assert.False(window.Found<ProgressBar>("HowFar").IsVisible);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Why_it_will_not_build_is_said_in_the_ink_of_a_refusal_before_anything_is_pressed(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        WhatWasDrawn.Wearing(look, () =>
        {
            (MainWindow window, MakeADisc screen) = AWindow();

            // A game and nowhere to put it.
            screen.Add(AGame());
            screen.OutputFolder = string.Empty;
            Dispatcher.UIThread.RunJobs();

            Refusal refusal = Assert.IsType<Refusal>(screen.WhyNot);
            TextBlock whyNot = window.Found<TextBlock>("WhyNot");

            Assert.True(whyNot.CanBeSeen());
            Assert.Equal(refusal.Headline, whyNot.Text);
            Assert.Equal(refusal.Because, ToolTip.GetTip(whyNot));
            Assert.Equal(WhatWasDrawn.Of("RefusalInk", look), whyNot.Foreground.AsColour());

            // And the button is dead rather than hidden.
            Button build = window.Found<Button>("MakeTheDisc");

            Assert.True(build.IsVisible);
            Assert.False(build.IsEnabled);
        });
    }

    [AvaloniaFact]
    public void The_way_out_of_a_refusal_sits_under_it_and_does_what_it_says()
    {
        ADesktopThatAnswers desktop = new() { WillPickFolders = [Path.Combine(_room, "discs")] };
        (MainWindow window, MakeADisc screen) = AWindow(desktop);

        screen.Add(AGame());
        screen.OutputFolder = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Button wayOut = window.Found<Button>("TheWayOut");

        Assert.True(wayOut.CanBeSeen());
        Assert.Equal(screen.WhatTheWayOutIsCalled, wayOut.Content);
        Assert.Equal(RemedyKind.PickFolder, screen.TheWayOut);

        Press(window, wayOut);

        // It asked for a folder, took it, and the refusal went.
        Assert.Equal(Path.Combine(_room, "discs"), screen.OutputFolder);
        Assert.Null(screen.WhyNot);
        Assert.False(window.Found<TextBlock>("WhyNot").IsVisible);
        Assert.False(wayOut.IsVisible);
        Assert.True(window.Found<Button>("MakeTheDisc").IsEnabled);
    }

    [AvaloniaFact]
    public void A_refusal_about_the_name_puts_the_cursor_in_the_name_with_all_of_it_selected()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        screen.Add(AGame());
        screen.OutputFolder = Path.Combine(_room, "discs");
        screen.Label = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(RemedyKind.FocusField, screen.TheWayOut);

        Press(window, window.Found<Button>("TheWayOut"));

        TextBox name = window.Found<TextBox>("Label");
        Assert.True(name.IsFocused);

        // Typing goes into the name, and the refusal goes.
        window.KeyTextInput("fixture");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("FIXTURE", screen.Label);
        Assert.Null(screen.WhyNot);
    }

    [AvaloniaFact]
    public void A_refusal_with_figures_draws_them_on_the_window()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        // A file bigger than the smallest disc, with splitting off.
        string download = Path.Combine(_room, "downloads", "Big Game");
        Directory.CreateDirectory(download);
        File.Copy(TheFixture(), Path.Combine(download, Installer));

        using (FileStream big = File.Create(Path.Combine(download, Installer.Replace(".exe", "-1.bin", StringComparison.Ordinal))))
        {
            // Sparse, so it costs nothing to make.
            big.SetLength(800L * 1024 * 1024);
        }

        screen.Add(TheGameIn(download));
        screen.OutputFolder = Path.Combine(_room, "discs");
        screen.CutFiles = false;
        screen.TheSizeChosen = screen.DiscSizes.OrderBy(size => size.Medium.Capacity).First();
        Dispatcher.UIThread.RunJobs();

        Refusal refusal = Assert.IsType<Refusal>(screen.WhyNot);
        Assert.NotEmpty(refusal.Figures);

        ItemsControl figures = window.Found<ItemsControl>("WhyNotFigures");

        Assert.True(figures.CanBeSeen());

        string[] drawn = [.. figures.GetVisualDescendants().OfType<TextBlock>().Select(each => each.Text ?? string.Empty)];

        foreach (Figure figure in refusal.Figures)
        {
            Assert.Contains(figure.Label, drawn);
            Assert.Contains(figure.Value, drawn);
        }

        // Inside the panel, whatever is in them.
        Assert.True(figures.TranslatePoint(new Point(figures.Bounds.Width, 0), window)!.Value.X < 920 - 28);
    }

    // ---------------------------------------------------------------- building

    [AvaloniaFact]
    public void Build_it_makes_a_disc_folder_and_an_image_and_offers_to_open_where_they_are()
    {
        if (!OperatingSystem.IsLinux())
        {
            // The image is xorriso's.
            return;
        }

        ADesktopThatAnswers desktop = new();
        (MainWindow window, MakeADisc screen) = AWindow(desktop);
        string discs = Path.Combine(_room, "discs");

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = discs;
        Dispatcher.UIThread.RunJobs();

        Button build = window.Found<Button>("MakeTheDisc");

        Assert.Equal("Build it", build.Content);
        Assert.Same(Application.Current!.FindResource("TheButton"), build.Theme);
        Assert.True(build.IsEnabled);

        Press(window, build);

        // Said when it is done, in front of somebody.
        Until(() => window.OwnedWindows.OfType<WorthAsking>().Any(each => each.IsVisible), "the disc to be ready", seconds: 60);

        WorthAsking ready = window.OwnedWindows.OfType<WorthAsking>().Single();

        Assert.Equal("The disc is ready", ready.Found<TextBlock>("Headline").Text);
        Assert.Equal("Open the folder", ready.Found<Button>("TheLikelyOne").Content);
        Assert.Equal("Not now", ready.Found<Button>("TheOtherWay").Content);

        BuiltDiscsOnDisk made = WhatIsIn(discs);

        // The disc as a folder, with the game and the menu on it.
        Assert.True(File.Exists(Path.Combine(made.DiscFolder, Installer)));
        Assert.True(File.Exists(Path.Combine(made.DiscFolder, "autorun.inf")));
        Assert.True(File.Exists(Path.Combine(made.DiscFolder, "AUTORUN", "menu.hta")));

        // And the image beside it, which is what gets burned: a real one,
        // with the name on it that was typed.
        Assert.True(new FileInfo(made.Image).Length > 100_000, "The image is next to nothing.");
        Assert.Equal("FIXTURE_GAME", TheJolietRecords.VolumeNameOf(made.Image));
        Assert.Contains(TheJolietRecords.FilesIn(made.Image), file => file.Path == Installer);

        // Named for Linux too, since nobody unticked it: the name in full,
        // and the icon the menu wears, there being no other.
        IReadOnlyList<TheJolietRecords.Recorded> onTheImage = TheJolietRecords.FilesIn(made.Image);
        TheJolietRecords.Recorded name = Assert.Single(onTheImage, file => file.Path == ".xdg-volume-info");

        Assert.Equal(
            "[Volume Info]\nName=FIXTURE GAME\nIconFile=AUTORUN/menu.png\n",
            System.Text.Encoding.UTF8.GetString(TheJolietRecords.BytesOf(made.Image, name)));
        Assert.Contains(onTheImage, file => file.Path == "AUTORUN/menu.png");

        Assert.Contains(made.DiscFolder, ready.Found<TextBlock>("Because").Text, StringComparison.Ordinal);

        // The likely answer opens the folder, through the desktop.
        Press(ready, ready.Found<Button>("TheLikelyOne"));
        Until(() => desktop.Opened.Count == 1, "the folder to be opened");

        Assert.Equal([made.DiscFolder], desktop.Opened);

        // The window is itself again.
        Assert.True(screen.NothingIsRunning);
        Assert.False(window.Found<TextBlock>("Doing").IsVisible);
        Assert.True(window.Found<TextBlock>("WhatItWillMake").IsVisible);
    }

    [AvaloniaFact]
    public void A_disc_built_with_that_tick_off_carries_nothing_for_Linux()
    {
        if (!OperatingSystem.IsLinux())
        {
            // The image is xorriso's.
            return;
        }

        (MainWindow window, MakeADisc screen) = AWindow(new ADesktopThatAnswers());
        string discs = Path.Combine(_room, "discs");

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = discs;
        Dispatcher.UIThread.RunJobs();

        // With the pointer, the way somebody would.
        Press(window, window.Found<Expander>("MoreOptions").Part<TextBlock>("Words"));
        Press(window, window.Found<CheckBox>("LinuxNameAndIcon"));

        Assert.False(screen.LinuxNameAndIcon);

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => window.OwnedWindows.OfType<WorthAsking>().Any(each => each.IsVisible), "the disc to be ready", seconds: 60);

        BuiltDiscsOnDisk made = WhatIsIn(discs);
        IReadOnlyList<TheJolietRecords.Recorded> onTheImage = TheJolietRecords.FilesIn(made.Image);

        Assert.Contains(onTheImage, file => file.Path == "autorun.inf");
        Assert.DoesNotContain(onTheImage, file => file.Path == ".xdg-volume-info");
        Assert.DoesNotContain(onTheImage, file => file.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        WorthAsking ready = window.OwnedWindows.OfType<WorthAsking>().Single();
        Press(ready, ready.Found<Button>("TheOtherWay"));
    }

    [AvaloniaFact]
    public void When_nothing_will_open_the_folder_it_says_where_the_disc_is()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        ADesktopThatAnswers desktop = new() { OpensThings = false };
        (MainWindow window, MakeADisc screen) = AWindow(desktop);

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = Path.Combine(_room, "discs");
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => window.OwnedWindows.OfType<WorthAsking>().Any(each => each.IsVisible), "the disc to be ready", seconds: 60);

        WorthAsking ready = window.OwnedWindows.OfType<WorthAsking>().Single();
        Press(ready, ready.Found<Button>("TheLikelyOne"));

        Until(() => window.OwnedWindows.OfType<SayingSo>().Any(each => each.IsVisible), "to be told where it is");

        SayingSo box = window.OwnedWindows.OfType<SayingSo>().Single();

        Assert.Equal("Nothing would open the folder", box.Found<TextBlock>("Headline").Text);
        Assert.Contains(Path.Combine(_room, "discs"), box.Found<SelectableTextBlock>("Because").Text, StringComparison.Ordinal);

        box.Close();
    }

    [AvaloniaFact]
    public void Not_now_opens_nothing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        ADesktopThatAnswers desktop = new();
        (MainWindow window, MakeADisc screen) = AWindow(desktop);

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = Path.Combine(_room, "discs");
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => window.OwnedWindows.OfType<WorthAsking>().Any(each => each.IsVisible), "the disc to be ready", seconds: 60);

        WorthAsking ready = window.OwnedWindows.OfType<WorthAsking>().Single();
        Press(ready, ready.Found<Button>("TheOtherWay"));
        Until(() => window.OwnedWindows.Count == 0, "the question to go");

        Assert.Empty(desktop.Opened);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_whole_window_is_kept_as_a_picture(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        WhatWasDrawn.Wearing(look, () =>
        {
            (MainWindow window, MakeADisc screen) = AWindow();

            screen.Add(AGame());
            screen.OutputFolder = "/home/somebody/Discs";
            Dispatcher.UIThread.RunJobs();

            Press(window, window.Found<Expander>("MoreOptions").Part<TextBlock>("Words"));
            window.MouseMove(new Point(14, 400));

            string kept = Repository.File("artifacts", "the-look", $"the-whole-window-{which.ToLowerInvariant()}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(kept)!);

            Dispatcher.UIThread.RunJobs();
            using Avalonia.Media.Imaging.WriteableBitmap drawn = window.CaptureRenderedFrame()!;
            drawn.Save(kept, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

            // The bottom line is a panel.
            Button build = window.Found<Button>("MakeTheDisc");
            Point beside = build.TranslatePoint(new Point(-10, build.Bounds.Height / 2), window)!.Value;

            Assert.Equal(WhatWasDrawn.Of("Panel", look), window.ColourAt(beside));
        });
    }

    // ------------------------------------------------------------------ helpers

    // ------------------------------------------- with nothing to make an image

    [AvaloniaFact]
    public void With_nothing_to_make_an_image_Build_says_so_before_anything_is_laid_out()
    {
        int asked = 0;
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(
            screen,
            new ADesktopThatAnswers(),
            new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows),
            whyNoImageCouldBeMade: () =>
            {
                asked++;
                return Refusals.NothingHereMakesAnImage("xorriso", DiscWright.Linux.XorrisoImageMaker.HowToGetIt);
            });

        string discs = Path.Combine(_room, "discs");

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = discs;
        Dispatcher.UIThread.RunJobs();

        // Not asked until somebody presses: a line on the window that came
        // and went with whether xorriso was installed would be news nobody
        // asked for.
        Assert.Equal(0, asked);

        Press(window, window.Found<Button>("MakeTheDisc"));

        SayingSo box = Assert.Single(window.OwnedWindows.OfType<SayingSo>());

        Assert.Equal("xorriso is not installed", box.Found<TextBlock>("Headline").Text);
        Assert.Contains("Nothing has been laid out yet.", box.Found<SelectableTextBlock>("Because").Text, StringComparison.Ordinal);

        // With the line to type, which is the half of the sentence that was
        // missing from the box.
        Assert.Contains(
            box.GetVisualDescendants().OfType<SelectableTextBlock>(),
            each => each.Text == "sudo apt install xorriso");

        // And nothing was laid out: no folder, no run, nothing that went
        // wrong part of the way through.
        Assert.False(Directory.Exists(discs), "A disc was laid out with nothing to make its image.");
        Assert.True(screen.NothingIsRunning);
        Assert.Null(screen.ItWentWrong);

        Press(box, box.Found<Button>("Understood"));

        Assert.Empty(window.OwnedWindows.OfType<SayingSo>());

        // Asked afresh at every press, since what somebody does next is
        // install it and press again.
        Press(window, window.Found<Button>("MakeTheDisc"));

        Assert.Equal(2, asked);

        Assert.Single(window.OwnedWindows.OfType<SayingSo>()).Close();
    }

    [AvaloniaFact]
    public void With_something_to_make_one_with_Build_goes_straight_on()
    {
        int asked = 0;
        ABuilderThatWaits builder = new();
        MakeADisc screen = new(TheLinuxMachine.Composed(new NothingIsLogged()) with { Builder = builder });
        MainWindow window = TheWindow.Showing(
            screen,
            new ADesktopThatAnswers(),
            new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows),
            whyNoImageCouldBeMade: () =>
            {
                asked++;
                return null;
            });

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = Path.Combine(_room, "discs");
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => builder.IsBuilding, "the build to begin");

        Assert.Equal(1, asked);
        Assert.Empty(window.OwnedWindows.OfType<SayingSo>());

        builder.LetItFinish();
        Until(() => screen.NothingIsRunning, "the build to be over");
        Dispatcher.UIThread.RunJobs();

        foreach (Window left in window.OwnedWindows.ToArray())
        {
            left.Close();
        }
    }

    // ------------------------------------------- with nowhere to write the disc

    [AvaloniaFact]
    public void Told_to_save_where_nothing_can_be_written_it_says_that_and_not_that_it_is_worth_reporting()
    {
        (MainWindow window, MakeADisc screen) = AWindow();

        // Inside a file, which is nowhere a folder can be made on any system.
        string aFile = Path.Combine(_room, "not-a-folder");
        File.WriteAllText(aFile, "a file, where a folder was asked for");
        string nowhere = Path.Combine(aFile, "discs");

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = nowhere;
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => window.OwnedWindows.OfType<SayingSo>().Any(each => each.IsVisible), "it to say so", seconds: 30);

        SayingSo box = window.OwnedWindows.OfType<SayingSo>().Single();
        string because = box.Found<SelectableTextBlock>("Because").Text!;

        Assert.Equal("That folder cannot be written to", box.Found<TextBlock>("Headline").Text);
        Assert.StartsWith($"The discs were to be built in {nowhere}, ", because, StringComparison.Ordinal);
        Assert.Contains("Choose another folder and build again.", because, StringComparison.Ordinal);

        // What it said before: that something went wrong that nobody had
        // accounted for, and was worth reporting. A folder typed wrong is
        // neither.
        Assert.DoesNotContain("worth reporting", because, StringComparison.Ordinal);
        Assert.DoesNotContain("Something went wrong", box.Found<TextBlock>("Headline").Text, StringComparison.Ordinal);

        Assert.True(screen.NothingIsRunning);
        Assert.Equal("a file, where a folder was asked for", File.ReadAllText(aFile));

        box.Close();
    }

    // --------------------------------------------- with a game that has gone

    [AvaloniaFact]
    public void A_game_taken_away_since_it_was_added_is_named_and_nothing_is_laid_out()
    {
        (MainWindow window, MakeADisc screen) = AWindow();
        string discs = Path.Combine(_room, "discs");

        GameFolder game = AGame();

        screen.Add(game);
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = discs;
        Dispatcher.UIThread.RunJobs();

        // The drive is unplugged: the folder the game was added from goes,
        // with the game still on the list.
        Directory.Move(Path.Combine(_room, "downloads"), Path.Combine(_room, "unplugged"));

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => window.OwnedWindows.OfType<SayingSo>().Any(each => each.IsVisible), "it to say so", seconds: 30);

        SayingSo box = window.OwnedWindows.OfType<SayingSo>().Single();
        string because = box.Found<SelectableTextBlock>("Because").Text!;

        Assert.Equal(game.Name + " is not where it was added from", box.Found<TextBlock>("Headline").Text);
        Assert.StartsWith(game.Path + " is not there any more.", because, StringComparison.Ordinal);
        Assert.Contains("on a drive that is no longer attached", because, StringComparison.Ordinal);

        // What it said before: that something went wrong nobody had
        // accounted for, and was worth reporting. And it left an empty disc
        // folder behind it.
        Assert.DoesNotContain("worth reporting", because, StringComparison.Ordinal);
        Assert.False(Directory.Exists(discs), "A disc folder was laid out for a game that is not there.");
        Assert.True(screen.NothingIsRunning);

        box.Close();

        // Put back, so the room can be cleared away.
        Directory.Move(Path.Combine(_room, "unplugged"), Path.Combine(_room, "downloads"));
    }

    // ------------------------------------------------- with not room to build

    [AvaloniaFact]
    public void With_not_room_where_the_disc_is_to_be_built_it_says_so_with_the_figures_and_lays_nothing_out()
    {
        // The real machine, with a disk that says it has a kilobyte left.
        MakeADisc screen = new(
            TheLinuxMachine.Composed(new NothingIsLogged()) with { Disk = new ARealDiskWithLittleRoom(free: 1024) });
        MainWindow window = TheWindow.Showing(
            screen, new ADesktopThatAnswers(), new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows));

        string discs = Path.Combine(_room, "discs");

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = discs;
        Dispatcher.UIThread.RunJobs();

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => window.OwnedWindows.OfType<SayingSo>().Any(each => each.IsVisible), "it to say so", seconds: 30);

        SayingSo box = window.OwnedWindows.OfType<SayingSo>().Single();
        string because = box.Found<SelectableTextBlock>("Because").Text!;

        Assert.Equal("There is not room to build it there", box.Found<TextBlock>("Headline").Text);
        Assert.StartsWith($"The discs were to be built in {discs}. ", because, StringComparison.Ordinal);
        Assert.DoesNotContain("worth reporting", because, StringComparison.Ordinal);

        // What it needs beside what there is, where an eye can compare
        // them: the two numbers are the whole of the argument.
        ItemsControl figures = box.Found<ItemsControl>("Figures");

        Assert.True(figures.CanBeSeen());
        Assert.Equal(
            ["building needs", "free there"],
            figures.GetVisualDescendants().OfType<TextBlock>().Where(each => each is not SelectableTextBlock).Select(each => each.Text));
        Assert.Equal(
            ByteSize.Describe(1024),
            figures.GetVisualDescendants().OfType<SelectableTextBlock>().Last().Text);

        // What it did before: copied until the disk was full, and left half
        // a disc folder behind.
        Assert.False(Directory.Exists(discs), "A disc folder was begun where there was not room for it.");
        Assert.True(screen.NothingIsRunning);

        box.Close();
    }

    // ------------------------------------------------------------ during a run

    [AvaloniaFact]
    public void While_a_disc_is_being_built_nothing_that_would_change_it_can_be_pressed()
    {
        ABuilderThatWaits builder = new();
        MakeADisc screen = new(TheLinuxMachine.Composed(new NothingIsLogged()) with { Builder = builder });
        MainWindow window = TheWindow.Showing(
            screen, new ADesktopThatAnswers(), new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows));

        screen.Add(AGame());
        screen.Label = "FIXTURE GAME";
        screen.OutputFolder = Path.Combine(_room, "discs");
        Dispatcher.UIThread.RunJobs();

        // The drawer open, so the ticks in it are on the window to be found.
        Press(window, window.Found<Expander>("MoreOptions").Part<TextBlock>("Words"));

        // Found by walking the window and not from a list, because the box
        // nobody tied to the run is exactly the one a list leaves out. The
        // tick for the Linux name was added without it, as a planted fault,
        // and nothing here noticed until this did.
        string[] before = [.. WhatSomebodyCanPress(window)];

        Assert.Contains("LinuxNameAndIcon", before);
        Assert.Contains("Checksums", before);
        Assert.Contains("Label", before);
        Assert.Contains("AddAGame", before);
        Assert.Contains("MakeTheDisc", before);

        Press(window, window.Found<Button>("MakeTheDisc"));
        Until(() => builder.IsBuilding, "the build to begin");
        Dispatcher.UIThread.RunJobs();

        Assert.False(screen.NothingIsRunning);

        string[] during = [.. WhatSomebodyCanPress(window)];

        // What stays alive, each for a reason, and nothing else.
        Assert.True(
            StillAliveDuringARun.Order(StringComparer.Ordinal).SequenceEqual(during.Order(StringComparer.Ordinal)),
            "Alive while a disc is being built: " + string.Join(", ", during.Order(StringComparer.Ordinal))
                + ". Meant to be: " + string.Join(", ", StillAliveDuringARun.Order(StringComparer.Ordinal)) + ".");

        builder.LetItFinish();
        Until(() => screen.NothingIsRunning, "the build to be over");
        Dispatcher.UIThread.RunJobs();

        foreach (Window asked in window.OwnedWindows.ToArray())
        {
            asked.Close();
        }

        Dispatcher.UIThread.RunJobs();

        // And all of it comes back.
        Assert.Equal(before.Order(StringComparer.Ordinal), WhatSomebodyCanPress(window).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// What can still be pressed while a disc is being made, because none of
    /// it changes the disc.
    /// </summary>
    private static readonly string[] StillAliveDuringARun =
    [
        // How the window dresses, which is nothing to do with the disc.
        "Appearance",

        // Where to read what is happening, which is most wanted now.
        "OpenTheLog",

        // The drawer itself, which only shows and hides the ticks.
        "MoreOptions",
    ];

    /// <summary>
    /// Every box, tick, dropdown, list and button on the window that can be
    /// seen and is alive, by the name the window gives it.
    /// </summary>
    private static IEnumerable<string> WhatSomebodyCanPress(MainWindow window) =>
        window.GetVisualDescendants()
            .OfType<Control>()
            .Where(each => each is CheckBox or TextBox or ComboBox or TreeView or Button or Expander)
            .Where(each => Avalonia.Automation.AutomationProperties.GetAutomationId(each) is { Length: > 0 })
            .Where(each => each.CanBeSeen() && each.IsEffectivelyEnabled)
            .Select(each => Avalonia.Automation.AutomationProperties.GetAutomationId(each)!)
            .Distinct();

    /// <summary>
    /// The real disk in everything but how much room it says it has.
    /// </summary>
    private sealed class ARealDiskWithLittleRoom(long free) : IFileSystem
    {
        private readonly IFileSystem _real = new WindowsFileSystem();

        public bool FolderExists(string path) => _real.FolderExists(path);

        public bool FileExists(string path) => _real.FileExists(path);

        public FolderListing List(string path) => _real.List(path);

        public FolderListing ListHere(string path) => _real.ListHere(path);

        public string? ReadText(string path) => _real.ReadText(path);

        public byte[]? ReadBytes(string path) => _real.ReadBytes(path);

        public long? RoomUnder(string folder) => free;
    }

    /// <summary>A builder that begins and then waits to be told it may finish.</summary>
    private sealed class ABuilderThatWaits : DiscWright.Staging.IBuildDiscs
    {
        private readonly TaskCompletionSource _mayFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public volatile bool IsBuilding;

        public void LetItFinish() => _mayFinish.TrySetResult();

        public async Task<Outcome<DiscWright.Staging.BuiltDiscs>> Build(
            DiscProject project,
            Medium medium,
            string outputFolder,
            IProgress<DiscWright.Core.Staging.BuildProgress>? progress,
            CancellationToken ct)
        {
            IsBuilding = true;

            await _mayFinish.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            return Refusals.ThereIsNothingToPutOnADisc();
        }
    }

    private sealed record BuiltDiscsOnDisk(string DiscFolder, string Image);

    /// <summary>The one disc folder and the one image under where the discs go.</summary>
    private static BuiltDiscsOnDisk WhatIsIn(string discs)
    {
        string image = Assert.Single(Directory.EnumerateFiles(discs, "*.iso", SearchOption.AllDirectories));
        string autorun = Assert.Single(Directory.EnumerateFiles(discs, "autorun.inf", SearchOption.AllDirectories));

        return new BuiltDiscsOnDisk(Path.GetDirectoryName(autorun)!, image);
    }

    private static (MainWindow Window, MakeADisc Screen) AWindow(ADesktopThatAnswers? desktop = null)
    {
        MakeADisc screen = TheWindow.AScreen();

        return (
            TheWindow.Showing(
                screen, desktop ?? new ADesktopThatAnswers(), new ChoosingTheLook(null, _ => { }, App.WhatTheLookFollows)),
            screen);
    }

    private static string TheFixture() => Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe");

    private GameFolder AGame()
    {
        string download = Path.Combine(_room, "downloads", "Fixture Game");

        if (!Directory.Exists(download))
        {
            Directory.CreateDirectory(download);
            File.Copy(TheFixture(), Path.Combine(download, Installer));
            File.WriteAllBytes(Path.Combine(download, Installer.Replace(".exe", "-1.bin", StringComparison.Ordinal)), new byte[300_000]);
        }

        return TheGameIn(download);
    }

    private static GameFolder TheGameIn(string download) =>
        Assert.IsType<Outcome<GameFolder>.Ok>(
            new GameFolders(new WindowsFileSystem(), TheLinuxMachine.TheReaderOfFolders()).At(download)).Value;

    private static void Until(Func<bool> itIsSo, string what, int seconds = 5)
    {
        DateTime giveUp = DateTime.UtcNow.AddSeconds(seconds);

        while (true)
        {
            Dispatcher.UIThread.RunJobs();

            if (itIsSo())
            {
                Dispatcher.UIThread.RunJobs();
                return;
            }

            Assert.True(DateTime.UtcNow < giveUp, $"Waited {seconds} seconds for {what}, and it did not come.");
            Thread.Sleep(5);
        }
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
