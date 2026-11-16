using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Path = Avalonia.Controls.Shapes.Path;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The controls the look draws for itself: the text box, the checkbox, the
/// dropdown, the tree of what goes on the disc, the rows of a list, the
/// drawer and the scrollbars.
/// </summary>
/// <remarks>
/// <para>
/// Each of these is a template of our own, as it is on Windows and for the
/// same reason: the theme's version answers in colours the palette has never
/// heard of. A template of our own is also a template that could have left
/// out the part that does the work, so every one is used here the way a
/// person uses it, with the pointer and the keyboard, as well as looked at.
/// </para>
/// <para>
/// The figures are <c>Look.xaml</c>'s in the Windows app.
/// </para>
/// </remarks>
public class TheControlsOnLinux
{
    private static readonly ThemeVariant Light = ThemeVariant.Light;

    // ------------------------------------------------------------ the text box

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void A_text_box_is_the_panel_with_an_edge_and_room_round_its_words(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        TextBox box = new() { Text = "HOLLOW KNIGHT", Width = 200 };

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(box);
            Border shape = box.Part<Border>("PART_BorderElement");

            Assert.Equal(WhatWasDrawn.Of("Panel", look), shape.Background.AsColour());
            Assert.Equal(WhatWasDrawn.Of("Edge", look), shape.BorderBrush.AsColour());
            Assert.Equal(new Thickness(1), shape.BorderThickness);
            Assert.Equal(new Thickness(8, 6), box.Padding);
            Assert.Equal(13, box.FontSize);
            Assert.Equal(WhatWasDrawn.Of("Ink", look), box.Foreground.AsColour());

            // Drawn so: just inside the box, clear of the words, is panel.
            Point inside = box.TranslatePoint(new Point(3, 3), window)!.Value;
            Assert.Equal(WhatWasDrawn.Of("Panel", look), window.ColourAt(inside));
        });
    }

    [AvaloniaFact]
    public void A_text_box_can_be_typed_in_and_wears_the_accent_while_it_is()
    {
        TextBox box = new() { Width = 200 };

        WhatWasDrawn.Wearing(Light, () =>
        {
            Window window = WhatWasDrawn.Showing(box);
            Point middle = box.MiddleIn(window);

            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
            window.KeyTextInput("Disc 1");

            // The pointer goes away again first. Left over the box, an edge
            // that only answered the pointer would pass for one that answers
            // the keyboard, and did, the first time a fault was planted here.
            window.MouseMove(new Point(1, 1));
            Dispatcher.UIThread.RunJobs();

            // The template is ours, and a text box whose template lost the
            // part that takes the keys is a box that looks right and is dead.
            Assert.Equal("Disc 1", box.Text);
            Assert.True(box.IsFocused);
            Assert.Equal(WhatWasDrawn.Of("Accent", Light), box.Part<Border>("PART_BorderElement").BorderBrush.AsColour());
        });
    }

    [AvaloniaFact]
    public void A_text_box_that_cannot_be_typed_in_is_dimmed_whole()
    {
        TextBox box = new() { Text = "HOLLOW KNIGHT", Width = 200, IsEnabled = false };

        WhatWasDrawn.Showing(box);

        // Opacity, because it is on the whole control and nothing within it
        // can set it back. On Windows the words once stayed in full ink.
        Assert.Equal(0.45, box.Opacity);
    }

    // ------------------------------------------------------------ the checkbox

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void A_checkbox_at_rest_is_an_empty_box_of_our_own(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        CheckBox check = new() { Content = "A menu when the disc goes in" };

        WhatWasDrawn.Wearing(look, () =>
        {
            WhatWasDrawn.Showing(check);
            Border box = check.Part<Border>("Box");

            Assert.Equal(15, box.Bounds.Width);
            Assert.Equal(15, box.Bounds.Height);
            Assert.Equal(new CornerRadius(3), box.CornerRadius);
            Assert.Equal(WhatWasDrawn.Of("Panel", look), box.Background.AsColour());
            Assert.Equal(WhatWasDrawn.Of("Quiet", look), box.BorderBrush.AsColour());

            Assert.False(check.Part<Path>("Tick").IsVisible);
            Assert.False(check.Part<Rectangle>("Partly").IsVisible);

            Assert.Equal(13, check.FontSize);
            Assert.Equal(WhatWasDrawn.Of("Ink", look), check.Foreground.AsColour());
            Assert.Equal(new Thickness(0, 0, 0, 6), check.Margin);
        });
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void A_checkbox_pressed_on_its_words_is_ticked_and_takes_the_accent(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        CheckBox check = new() { Content = "A menu when the disc goes in" };

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(check);

            // On the words and not on the box: the whole line is the control.
            Point onTheWords = check.TranslatePoint(new Point(check.Bounds.Width - 20, check.Bounds.Height / 2), window)!.Value;
            window.MouseDown(onTheWords, MouseButton.Left);
            window.MouseUp(onTheWords, MouseButton.Left);
            window.MouseMove(new Point(1, 1));
            Dispatcher.UIThread.RunJobs();

            Assert.True(check.IsChecked);

            Border box = check.Part<Border>("Box");
            Assert.Equal(WhatWasDrawn.Of("Accent", look), box.Background.AsColour());
            Assert.Equal(WhatWasDrawn.Of("Accent", look), box.BorderBrush.AsColour());
            Assert.True(check.Part<Path>("Tick").IsVisible);
            Assert.False(check.Part<Rectangle>("Partly").IsVisible);
            Assert.Equal(WhatWasDrawn.Of("InkOnAccent", look), check.Part<Path>("Tick").Stroke.AsColour());

            // Drawn so: a corner of the box, where the tick does not reach.
            Point corner = box.TranslatePoint(new Point(3, 3), window)!.Value;
            Assert.Equal(WhatWasDrawn.Of("Accent", look), window.ColourAt(corner));
        });
    }

    [AvaloniaFact]
    public void A_checkbox_that_is_some_but_not_all_shows_a_small_square_in_a_resting_box()
    {
        CheckBox check = new() { Content = "Hollow Knight", IsThreeState = true, IsChecked = null };

        WhatWasDrawn.Wearing(Light, () =>
        {
            Window window = WhatWasDrawn.Showing(check);
            Border box = check.Part<Border>("Box");

            Assert.True(check.Part<Rectangle>("Partly").IsVisible);
            Assert.False(check.Part<Path>("Tick").IsVisible);
            Assert.Equal(WhatWasDrawn.Of("Panel", Light), box.Background.AsColour());
            Assert.Equal(WhatWasDrawn.Of("Accent", Light), check.Part<Rectangle>("Partly").Fill.AsColour());

            // The square is in the middle, and the corner is still paper.
            Assert.Equal(WhatWasDrawn.Of("Accent", Light), window.ColourAt(box.MiddleIn(window)));
            Assert.Equal(WhatWasDrawn.Of("Panel", Light), window.ColourAt(box.TranslatePoint(new Point(2.5, 2.5), window)!.Value));
        });
    }

    [AvaloniaFact]
    public void A_checkbox_takes_the_accent_on_its_edge_under_the_pointer()
    {
        CheckBox check = new() { Content = "Checksums" };

        WhatWasDrawn.Wearing(Light, () =>
        {
            Window window = WhatWasDrawn.Showing(check);

            window.MouseMove(check.MiddleIn(window));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("Accent", Light), check.Part<Border>("Box").BorderBrush.AsColour());
        });
    }

    [AvaloniaFact]
    public void A_checkbox_that_cannot_be_changed_is_dimmed_whole_and_does_not_change()
    {
        CheckBox check = new() { Content = "Checksums", IsEnabled = false };

        Window window = WhatWasDrawn.Showing(check);
        Point middle = check.MiddleIn(window);

        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);

        Assert.Equal(0.45, check.Opacity);
        Assert.False(check.IsChecked);
    }

    // ------------------------------------------------------------ the dropdown

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void A_dropdown_shut_is_the_shape_of_an_ordinary_button_and_says_what_is_chosen(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        ComboBox drop = ADropdown();

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(drop);
            Border shape = drop.Part<Border>("Shape");

            Assert.Equal(WhatWasDrawn.Of("Panel", look), shape.Background.AsColour());
            Assert.Equal(WhatWasDrawn.Of("Edge", look), shape.BorderBrush.AsColour());
            Assert.Equal(new Thickness(1), shape.BorderThickness);
            Assert.Equal(new CornerRadius(4), shape.CornerRadius);
            Assert.True(drop.Bounds.Height >= 30, $"A dropdown is at least 30 tall, and this is {drop.Bounds.Height}.");

            // What is chosen, in words, where somebody can read it.
            TextBlock said = drop.Part<ContentControl>("Chosen").GetVisualDescendants().OfType<TextBlock>().Single();
            Assert.Equal("DVD, 4.7 GB", said.Text);
            Assert.Equal(WhatWasDrawn.Of("Ink", look), said.Foreground.AsColour());

            // The arrow at its right, in the quiet grey.
            Assert.Equal(WhatWasDrawn.Of("Quiet", look), shape.GetVisualDescendants().OfType<Path>().Single().Stroke.AsColour());

            Point inside = drop.TranslatePoint(new Point(4, drop.Bounds.Height / 2), window)!.Value;
            Assert.Equal(WhatWasDrawn.Of("Panel", look), window.ColourAt(inside));
        });
    }

    [AvaloniaFact]
    public void A_dropdown_opens_when_pressed_and_takes_the_row_somebody_picks()
    {
        ComboBox drop = ADropdown();

        WhatWasDrawn.Wearing(Light, () =>
        {
            Window window = WhatWasDrawn.Showing(drop);
            Point middle = drop.MiddleIn(window);

            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(drop.IsDropDownOpen, "Pressing the dropdown did not open it.");

            // Every row is there, each one a row of ours.
            ComboBoxItem third = Assert.IsType<ComboBoxItem>(drop.ContainerFromIndex(2));
            Border row = third.Part<Border>("Row");
            Assert.Equal(new Thickness(10, 6), row.Padding);
            Assert.Equal("Blu-ray, 25 GB", third.GetVisualDescendants().OfType<TextBlock>().Single().Text);

            // Picked the way a person picks it, in the list that opened.
            TopLevel list = TopLevel.GetTopLevel(third)!;
            Point onIt = third.TranslatePoint(new Point(third.Bounds.Width / 2, third.Bounds.Height / 2), list)!.Value;

            list.MouseMove(onIt);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(WhatWasDrawn.Of("Page", Light), row.Background.AsColour());

            list.MouseDown(onIt, MouseButton.Left);
            list.MouseUp(onIt, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, drop.SelectedIndex);
            Assert.False(drop.IsDropDownOpen, "Picking a row did not shut the dropdown.");
            Assert.Equal(
                "Blu-ray, 25 GB",
                drop.Part<ContentControl>("Chosen").GetVisualDescendants().OfType<TextBlock>().Single().Text);
        });
    }

    [AvaloniaFact]
    public void A_dropdown_takes_the_page_under_the_pointer_and_is_dimmed_when_it_cannot_be_opened()
    {
        ComboBox drop = ADropdown();

        WhatWasDrawn.Wearing(Light, () =>
        {
            Window window = WhatWasDrawn.Showing(drop);

            window.MouseMove(drop.MiddleIn(window));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(WhatWasDrawn.Of("Page", Light), drop.Part<Border>("Shape").Background.AsColour());

            drop.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0.45, drop.Opacity);

            window.MouseDown(drop.MiddleIn(window), MouseButton.Left);
            window.MouseUp(drop.MiddleIn(window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(drop.IsDropDownOpen);
        });
    }

    // ---------------------------------------------------------------- the tree

    [AvaloniaFact]
    public void What_is_under_a_row_of_the_tree_steps_in_by_nineteen_exactly()
    {
        (TreeView tree, TreeViewItem game, TreeViewItem file, TreeViewItem alone) = ATree();
        Window window = WhatWasDrawn.Showing(tree);

        double ofTheGame = WhereItsNameStarts(game, window);
        double ofItsFile = WhereItsNameStarts(file, window);

        // AThingOnTheDisc gives every row its name back whatever the tree
        // pushed it in by, at nineteen a level, and the sizes only line up
        // if the tree agrees. Avalonia's own tree steps in by another number.
        Assert.Equal(19, ofItsFile - ofTheGame);

        // And a row with nothing under it starts its name in the same column
        // as one with: its opener is unseen, not gone.
        Assert.Equal(ofTheGame, WhereItsNameStarts(alone, window));
        Assert.Equal(19, ofTheGame - tree.TranslatePoint(default, window)!.Value.X);
    }

    [AvaloniaFact]
    public void A_row_with_nothing_under_it_has_no_opener_to_see_or_to_press()
    {
        (TreeView tree, TreeViewItem game, _, TreeViewItem alone) = ATree();
        WhatWasDrawn.Showing(tree);

        Assert.Equal(0, alone.Part<ToggleButton>("Opener").Opacity);
        Assert.False(alone.Part<ToggleButton>("Opener").IsHitTestVisible);

        Assert.Equal(1, game.Part<ToggleButton>("Opener").Opacity);
        Assert.True(game.Part<ToggleButton>("Opener").IsHitTestVisible);
    }

    [AvaloniaFact]
    public void The_opener_shuts_and_opens_what_is_under_a_row_and_turns_its_arrow()
    {
        (TreeView tree, TreeViewItem game, TreeViewItem file, _) = ATree();
        Window window = WhatWasDrawn.Showing(tree);

        ToggleButton opener = game.Part<ToggleButton>("Opener");
        Path arrow = opener.Part<Path>("Arrow");

        Assert.True(game.IsExpanded);
        Assert.True(file.CanBeSeen());
        Assert.Equal(0, HowFarItIsTurned(arrow));

        Point onIt = opener.MiddleIn(window);
        window.MouseDown(onIt, MouseButton.Left);
        window.MouseUp(onIt, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(game.IsExpanded);
        Assert.False(file.CanBeSeen());
        Assert.Equal(-90, HowFarItIsTurned(arrow));

        window.MouseDown(onIt, MouseButton.Left);
        window.MouseUp(onIt, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(game.IsExpanded);
        Assert.True(file.CanBeSeen());
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_row_somebody_picked_wears_the_deeper_paper_and_a_band_of_the_accent(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        (TreeView tree, TreeViewItem game, TreeViewItem file, _) = ATree();

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(tree);

            Assert.False(file.Part<Rectangle>("Chosen").IsVisible);

            // Picked by pressing its name.
            ContentPresenter name = file.Part<ContentPresenter>("PART_HeaderPresenter");
            Point onIt = name.MiddleIn(window);
            window.MouseDown(onIt, MouseButton.Left);
            window.MouseUp(onIt, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(file.IsSelected);
            Assert.Equal(WhatWasDrawn.Of("Page", look), file.Part<Border>("Row").Background.AsColour());
            Assert.Equal(new CornerRadius(3), file.Part<Border>("Row").CornerRadius);

            Rectangle band = file.Part<Rectangle>("Chosen");
            Assert.True(band.IsVisible);
            Assert.Equal(3, band.Bounds.Width);
            Assert.Equal(WhatWasDrawn.Of("Accent", look), band.Fill.AsColour());
            Assert.Equal(WhatWasDrawn.Of("Accent", look), window.ColourAt(band.MiddleIn(window)));

            // And only that one.
            Assert.False(game.IsSelected);
            Assert.False(game.Part<Rectangle>("Chosen").IsVisible);
        });
    }

    [AvaloniaFact]
    public void The_tree_held_while_a_disc_is_made_is_dimmed_whole()
    {
        (TreeView tree, _, _, _) = ATree();
        tree.IsEnabled = false;

        WhatWasDrawn.Showing(tree);

        Assert.Equal(0.45, tree.Opacity);
    }

    // ------------------------------------------------------------- a list's rows

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void A_row_of_a_list_wears_the_page_under_the_pointer_and_the_band_when_it_is_chosen(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        ListBox list = new() { ItemsSource = new[] { "D: HL-DT-ST BD-RE", "E: ASUS DRW-24" }, Width = 240 };

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(list);

            ListBoxItem first = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
            ListBoxItem second = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(1));

            // Nothing is chosen for somebody.
            Assert.False(first.Part<Rectangle>("Chosen").IsVisible);
            Assert.False(second.Part<Rectangle>("Chosen").IsVisible);

            window.MouseMove(second.MiddleIn(window));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(WhatWasDrawn.Of("Page", look), second.Part<Border>("Row").Background.AsColour());
            Assert.False(second.Part<Rectangle>("Chosen").IsVisible);

            window.MouseDown(second.MiddleIn(window), MouseButton.Left);
            window.MouseUp(second.MiddleIn(window), MouseButton.Left);
            window.MouseMove(new Point(1, 1));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, list.SelectedIndex);
            Assert.Equal(WhatWasDrawn.Of("Page", look), second.Part<Border>("Row").Background.AsColour());
            Assert.True(second.Part<Rectangle>("Chosen").IsVisible);
            Assert.Equal(WhatWasDrawn.Of("Accent", look), second.Part<Rectangle>("Chosen").Fill.AsColour());
            Assert.Equal(new Thickness(6, 0, 0, 0), second.Part<ContentPresenter>("PART_ContentPresenter").Margin);

            Assert.False(first.Part<Rectangle>("Chosen").IsVisible);
        });
    }

    // -------------------------------------------------------------- the drawer

    [AvaloniaFact]
    public void The_drawer_is_a_line_of_words_that_opens_what_is_under_it()
    {
        TextBlock inside = new() { Text = "Readable by Windows 98 and Mac OS 9" };
        Expander drawer = new() { Header = "More options", Content = inside };

        WhatWasDrawn.Wearing(Light, () =>
        {
            drawer.Foreground = (IBrush)Application.Current!.FindResource(Light, "Quiet")!;
            drawer.FontSize = 12;

            Window window = WhatWasDrawn.Showing(drawer);

            ToggleButton handle = drawer.Part<ToggleButton>("Handle");
            TextBlock words = handle.Part<TextBlock>("Words");
            Path arrow = handle.Part<Path>("Arrow");

            // The words are the drawer's own, in the ink and size it was given.
            Assert.Equal("More options", words.Text);
            Assert.Equal(WhatWasDrawn.Of("Quiet", Light), words.Foreground.AsColour());
            Assert.Equal(12, words.FontSize);
            Assert.Equal(StandardCursorType.Hand.ToString(), handle.Cursor?.ToString());

            Assert.False(drawer.IsExpanded, "The drawer starts open.");
            Assert.False(inside.CanBeSeen(), "What is in the drawer can be seen while it is shut.");
            Assert.Equal(-90, HowFarItIsTurned(arrow));

            Point onTheWords = words.MiddleIn(window);
            window.MouseDown(onTheWords, MouseButton.Left);
            window.MouseUp(onTheWords, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(drawer.IsExpanded);
            Assert.True(inside.CanBeSeen());
            Assert.Equal(0, HowFarItIsTurned(arrow));

            // What is in the drawer sits under the handle, not beside it.
            Assert.True(inside.TranslatePoint(default, window)!.Value.Y >= handle.TranslatePoint(new Point(0, handle.Bounds.Height), window)!.Value.Y);
        });
    }

    // ---------------------------------------------------------- the scrollbars

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void A_scrollbar_is_ten_wide_and_nothing_but_a_thumb(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        ScrollViewer scrolled = SomethingTooTallToShow();

        WhatWasDrawn.Wearing(look, () =>
        {
            WhatWasDrawn.Showing(scrolled);

            ScrollBar bar = scrolled.GetVisualDescendants().OfType<ScrollBar>().Single(each => each.Orientation == Avalonia.Layout.Orientation.Vertical);

            Assert.True(bar.IsEffectivelyVisible);
            Assert.Equal(10, bar.Bounds.Width);

            // No arrows at its ends: two buttons that page, and a thumb.
            Assert.Equal(
                ["PART_PageDownButton", "PART_PageUpButton"],
                bar.GetVisualDescendants().OfType<RepeatButton>().Select(each => each.Name).Order());

            Border thumb = bar.GetVisualDescendants().OfType<Thumb>().Single().GetVisualDescendants().OfType<Border>().Single();
            Assert.Equal(WhatWasDrawn.Of("Dead", look), thumb.Background.AsColour());
            Assert.Equal(new CornerRadius(4), thumb.CornerRadius);
            Assert.Equal(new Thickness(2), thumb.Margin);
        });
    }

    [AvaloniaFact]
    public void A_scrollbar_pages_when_its_track_is_pressed()
    {
        ScrollViewer scrolled = SomethingTooTallToShow();
        Window window = WhatWasDrawn.Showing(scrolled);

        ScrollBar bar = scrolled.GetVisualDescendants().OfType<ScrollBar>().Single(each => each.Orientation == Avalonia.Layout.Orientation.Vertical);

        Assert.Equal(0, scrolled.Offset.Y);

        // Near the bottom of the track, below the thumb.
        Point below = bar.TranslatePoint(new Point(5, bar.Bounds.Height - 4), window)!.Value;
        window.MouseDown(below, MouseButton.Left);
        window.MouseUp(below, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(scrolled.Offset.Y > 0, "Pressing the track under the thumb did not move what is shown.");
    }

    // ------------------------------------------------------------- the buttons

    [AvaloniaFact]
    public void The_words_on_a_button_stay_on_one_line_however_little_room_there_is()
    {
        // Every other piece of text wraps, by a rule on text itself, and that
        // rule reaches the words inside a button too.
        foreach (string theme in new[] { "Ordinary", "TheButton", "Quietly", "AsALink" })
        {
            Button button = new()
            {
                Content = "Check for a new version of DiscWright",
                Theme = (ControlTheme)Application.Current!.FindResource(theme)!,
                Width = 60,
            };

            Window window = WhatWasDrawn.Showing(button);

            Assert.True(
                button.Part<TextBlock>("Words").TextWrapping == TextWrapping.NoWrap,
                $"The words on a button wearing {theme} wrap.");

            window.Close();
        }
    }

    /// <summary>The angle a shape has been turned through, in whole degrees.</summary>
    private static int HowFarItIsTurned(Path shape)
    {
        Matrix turned = shape.RenderTransform?.Value ?? Matrix.Identity;

        return (int)Math.Round(Math.Atan2(turned.M12, turned.M11) * 180 / Math.PI);
    }

    private static ComboBox ADropdown() =>
        new()
        {
            ItemsSource = new[] { "CD, 700 MB", "DVD, 4.7 GB", "Blu-ray, 25 GB" },
            SelectedIndex = 1,
            Width = 200,
        };

    /// <summary>A game with one file under it, opened, and a row with nothing under it.</summary>
    private static (TreeView Tree, TreeViewItem Game, TreeViewItem File, TreeViewItem Alone) ATree()
    {
        TreeViewItem file = new() { Header = "setup_hollow_knight.exe" };
        TreeViewItem game = new() { Header = "Hollow Knight", IsExpanded = true, ItemsSource = new[] { file } };
        TreeViewItem alone = new() { Header = "Manual.pdf" };

        return (new TreeView { ItemsSource = new[] { game, alone }, Width = 300 }, game, file, alone);
    }

    private static double WhereItsNameStarts(TreeViewItem row, Window window) =>
        row.Part<ContentPresenter>("PART_HeaderPresenter").TranslatePoint(default, window)!.Value.X;

    private static ScrollViewer SomethingTooTallToShow() =>
        new()
        {
            Width = 200,
            Height = 100,
            Content = new Border { Height = 1000, Width = 100 },
        };
}
