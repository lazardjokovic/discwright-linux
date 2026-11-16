using System.Text.RegularExpressions;
using System.Xml.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

using DiscWright.LinuxApp;
using DiscWright.Tests;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The look the Linux window wears, held to the look the Windows window wears.
/// </summary>
/// <remarks>
/// <para>
/// The palette is not copied here to be compared with itself. These read
/// <c>Look.xaml</c> and <c>LookInTheDark.xaml</c> out of the Windows app and
/// hold the running Linux program to what those say, so a colour changed on
/// Windows and not here is a failure the day the two are brought level.
/// </para>
/// <para>
/// The sizes and weights are written out, with the Windows file as their
/// source, because there they are spread through styles this cannot read.
/// </para>
/// <para>
/// A button is judged by what it draws: its parts are found by the names the
/// template gives them, and where it matters the colour of a pixel is read.
/// The Windows app could only photograph its window and measure the
/// photograph.
/// </para>
/// </remarks>
public class TheLookOnLinux
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TheoryData<string> BothLooks => new() { "Light", "Dark" };

    [Theory]
    [MemberData(nameof(BothLooks))]
    public void The_palette_is_the_Windows_palette_colour_for_colour(string which)
    {
        Dictionary<string, Color> windows = TheWindowsPalette(which == "Light" ? "Look.xaml" : "LookInTheDark.xaml");
        Dictionary<string, Color> linux = TheLinuxPalette(which == "Light" ? "Look.axaml" : "LookInTheDark.axaml", which);

        Assert.Equal(11, windows.Count);

        Assert.Equal(windows.Keys.Order(), linux.Keys.Order());

        foreach ((string name, Color colour) in windows)
        {
            Assert.True(
                linux[name] == colour,
                $"{name} is {colour} in the Windows app's {which.ToLowerInvariant()} look and {linux[name]} here.");
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_program_answers_for_every_colour_in_both_looks(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        // Written down right is not the same as found. The dark palette is a
        // file of its own that Look.axaml has to name, and a name it got
        // wrong would leave the dark look answering in the light one's ink.
        foreach ((string name, Color colour) in TheLinuxPalette(which == "Light" ? "Look.axaml" : "LookInTheDark.axaml", which))
        {
            Assert.Equal(colour, WhatWasDrawn.Of(name, look));
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_window_is_drawn_on_the_page(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        WhatWasDrawn.Wearing(look, () =>
        {
            MainWindow window = TheWindow.WithNothingOnIt();
            window.Show();

            // A pixel, not the property: a window whose background was set
            // and never painted is a white window in the dark.
            Assert.Equal(WhatWasDrawn.Of("Page", look), window.ColourAt(new Point(460, 370)));
        });
    }

    [AvaloniaFact]
    public void A_window_already_open_changes_with_the_look()
    {
        MainWindow window = TheWindow.WithNothingOnIt();
        window.Show();

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
            Assert.Equal(WhatWasDrawn.Of("Page", ThemeVariant.Light), window.ColourAt(new Point(460, 370))));

        // The whole reason every palette reference is a DynamicResource.
        WhatWasDrawn.Wearing(ThemeVariant.Dark, () =>
            Assert.Equal(WhatWasDrawn.Of("Page", ThemeVariant.Dark), window.ColourAt(new Point(460, 370))));
    }

    [AvaloniaFact]
    public void Ordinary_words_are_thirteen_in_the_ink_and_in_the_face_the_app_carries()
    {
        TextBlock words = new() { Text = "Label" };

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            WhatWasDrawn.Showing(words);

            Assert.Equal(13, words.FontSize);
            Assert.Equal(FontWeight.Normal, words.FontWeight);
            Assert.Equal(WhatWasDrawn.Of("Ink", ThemeVariant.Light), words.Foreground.AsColour());
            Assert.Equal(TextWrapping.Wrap, words.TextWrapping);

            // Inter, from inside the app, and not whatever the machine has.
            Assert.Contains("Inter", words.FontFamily.Name, StringComparison.Ordinal);
        });
    }

    [AvaloniaFact]
    public void The_title_is_twenty_one_and_heavier()
    {
        TextBlock words = new() { Text = "DiscWright", Classes = { "Title" } };

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            WhatWasDrawn.Showing(words);

            Assert.Equal(21, words.FontSize);
            Assert.Equal(FontWeight.SemiBold, words.FontWeight);
            Assert.Equal(WhatWasDrawn.Of("Ink", ThemeVariant.Light), words.Foreground.AsColour());
        });
    }

    [AvaloniaFact]
    public void A_step_is_twelve_heavier_and_quiet_with_room_under_it()
    {
        TextBlock words = new() { Text = "1. What goes on it", Classes = { "Step" } };

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            WhatWasDrawn.Showing(words);

            Assert.Equal(12, words.FontSize);
            Assert.Equal(FontWeight.SemiBold, words.FontWeight);
            Assert.Equal(WhatWasDrawn.Of("Quiet", ThemeVariant.Light), words.Foreground.AsColour());
            Assert.Equal(new Thickness(0, 0, 0, 6), words.Margin);
        });
    }

    [AvaloniaFact]
    public void An_aside_is_twelve_and_quiet_and_wraps()
    {
        TextBlock words = new() { Text = "Worth knowing.", Classes = { "Aside" } };

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            WhatWasDrawn.Showing(words);

            Assert.Equal(12, words.FontSize);
            Assert.Equal(FontWeight.Normal, words.FontWeight);
            Assert.Equal(WhatWasDrawn.Of("Quiet", ThemeVariant.Light), words.Foreground.AsColour());
            Assert.Equal(TextWrapping.Wrap, words.TextWrapping);
        });
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void An_ordinary_button_is_the_panel_with_an_edge_and_its_words_in_the_ink(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        Button button = AButton("Ordinary", "Add a game");

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(button);

            Border shape = button.Part<Border>("Shape");
            TextBlock words = button.Part<TextBlock>("Words");

            Assert.Equal(WhatWasDrawn.Of("Panel", look), shape.Background.AsColour());
            Assert.Equal(WhatWasDrawn.Of("Edge", look), shape.BorderBrush.AsColour());
            Assert.Equal(new Thickness(1), shape.BorderThickness);
            Assert.Equal(new CornerRadius(4), shape.CornerRadius);

            Assert.Equal("Add a game", words.Text);
            Assert.Equal(WhatWasDrawn.Of("Ink", look), words.Foreground.AsColour());
            Assert.Equal(13, words.FontSize);
            Assert.Equal(FontWeight.Normal, words.FontWeight);
            Assert.Equal(new Thickness(16, 8), words.Margin);
            Assert.Equal(HorizontalAlignment.Center, words.HorizontalAlignment);

            Assert.True(button.Bounds.Height >= 36, $"An ordinary button is at least 36 tall, and this is {button.Bounds.Height}.");
            Assert.Equal(StandardCursorType.Hand.ToString(), button.Cursor?.ToString());

            // And it is really drawn that way: just inside its left edge,
            // clear of the words, is panel.
            Point inside = button.TranslatePoint(new Point(5, button.Bounds.Height / 2), window)!.Value;
            Assert.Equal(WhatWasDrawn.Of("Panel", look), window.ColourAt(inside));
        });
    }

    [AvaloniaFact]
    public void An_ordinary_button_takes_the_page_under_the_pointer()
    {
        Button button = AButton("Ordinary", "Add a game");

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            Window window = WhatWasDrawn.Showing(button);

            window.MouseMove(button.MiddleIn(window));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("Page", ThemeVariant.Light), button.Part<Border>("Shape").Background.AsColour());

            window.MouseMove(new Point(1, 1));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("Panel", ThemeVariant.Light), button.Part<Border>("Shape").Background.AsColour());
        });
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void An_ordinary_button_that_cannot_be_pressed_says_so_in_its_words_and_its_cursor(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        Button button = AButton("Ordinary", "Burn to a disc");
        button.IsEnabled = false;

        WhatWasDrawn.Wearing(look, () =>
        {
            WhatWasDrawn.Showing(button);

            // On Windows this once drew in #16191C, the same ink as the live
            // button beside it, with markup that read as though it greyed.
            Assert.Equal(WhatWasDrawn.Of("Dead", look), button.Part<TextBlock>("Words").Foreground.AsColour());
            Assert.Equal(StandardCursorType.Arrow.ToString(), button.Cursor?.ToString());
        });
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_button_that_makes_the_disc_wears_the_accent(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        Button button = AButton("TheButton", "Make the disc");

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(button);

            Border shape = button.Part<Border>("Shape");
            TextBlock words = button.Part<TextBlock>("Words");

            Assert.Equal(WhatWasDrawn.Of("Accent", look), shape.Background.AsColour());
            Assert.Equal(new CornerRadius(4), shape.CornerRadius);
            Assert.Equal(WhatWasDrawn.Of("InkOnAccent", look), words.Foreground.AsColour());
            Assert.Equal(FontWeight.SemiBold, words.FontWeight);
            Assert.Equal(13, words.FontSize);
            Assert.True(button.Bounds.Height >= 36);

            Point inside = button.TranslatePoint(new Point(5, button.Bounds.Height / 2), window)!.Value;
            Assert.Equal(WhatWasDrawn.Of("Accent", look), window.ColourAt(inside));
        });
    }

    [AvaloniaFact]
    public void The_button_that_makes_the_disc_darkens_while_it_is_held_down()
    {
        Button button = AButton("TheButton", "Make the disc");

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            Window window = WhatWasDrawn.Showing(button);
            Point middle = button.MiddleIn(window);

            window.MouseDown(middle, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("AccentPressed", ThemeVariant.Light), button.Part<Border>("Shape").Background.AsColour());

            window.MouseUp(middle, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("Accent", ThemeVariant.Light), button.Part<Border>("Shape").Background.AsColour());
        });
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_button_that_makes_the_disc_is_dead_rather_than_hidden_when_it_cannot_be_pressed(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        Button button = AButton("TheButton", "Make the disc");
        button.IsEnabled = false;

        WhatWasDrawn.Wearing(look, () =>
        {
            Window window = WhatWasDrawn.Showing(button);

            Assert.True(button.IsVisible);
            Assert.Equal(WhatWasDrawn.Of("Dead", look), button.Part<Border>("Shape").Background.AsColour());

            // Quiet, not white: white on the dead grey is barely there.
            Assert.Equal(WhatWasDrawn.Of("Quiet", look), button.Part<TextBlock>("Words").Foreground.AsColour());
            Assert.Equal(StandardCursorType.Arrow.ToString(), button.Cursor?.ToString());

            Point inside = button.TranslatePoint(new Point(5, button.Bounds.Height / 2), window)!.Value;
            Assert.Equal(WhatWasDrawn.Of("Dead", look), window.ColourAt(inside));
        });
    }

    [AvaloniaFact]
    public void A_quiet_line_is_twelve_and_quiet_and_only_says_it_can_be_pressed_under_the_pointer()
    {
        Button button = AButton("Quietly", "Check for a new version");

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            Window window = WhatWasDrawn.Showing(button);
            TextBlock words = button.Part<TextBlock>("Words");

            Assert.Equal(12, words.FontSize);
            Assert.Equal(WhatWasDrawn.Of("Quiet", ThemeVariant.Light), words.Foreground.AsColour());
            Assert.True(words.TextDecorations is null or { Count: 0 });
            Assert.Equal(HorizontalAlignment.Left, button.HorizontalAlignment);

            window.MouseMove(button.MiddleIn(window));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("Accent", ThemeVariant.Light), words.Foreground.AsColour());
            Assert.Equal(TextDecorationLocation.Underline, Assert.Single(words.TextDecorations!).Location);
        });
    }

    [AvaloniaFact]
    public void A_quiet_line_that_cannot_be_pressed_is_dead_and_never_underlined()
    {
        Button button = AButton("Quietly", "Check for a new version");
        button.IsEnabled = false;

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            Window window = WhatWasDrawn.Showing(button);
            TextBlock words = button.Part<TextBlock>("Words");

            // A dead link underlined in the accent colour is the loudest
            // thing on the window, so the pointer is put on it first.
            window.MouseMove(button.MiddleIn(window));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("Dead", ThemeVariant.Light), words.Foreground.AsColour());
            Assert.True(words.TextDecorations is null or { Count: 0 });
            Assert.Equal(StandardCursorType.Arrow.ToString(), button.Cursor?.ToString());
        });
    }

    [AvaloniaFact]
    public void A_link_reads_as_a_link_before_it_is_hovered_and_is_otherwise_a_quiet_line()
    {
        Button button = AButton("AsALink", "Take it off the list");

        WhatWasDrawn.Wearing(ThemeVariant.Light, () =>
        {
            Window window = WhatWasDrawn.Showing(button);
            TextBlock words = button.Part<TextBlock>("Words");

            Assert.Equal(WhatWasDrawn.Of("Accent", ThemeVariant.Light), words.Foreground.AsColour());
            Assert.Equal(12, words.FontSize);
            Assert.True(words.TextDecorations is null or { Count: 0 });

            window.MouseMove(button.MiddleIn(window));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(TextDecorationLocation.Underline, Assert.Single(words.TextDecorations!).Location);

            button.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WhatWasDrawn.Of("Dead", ThemeVariant.Light), words.Foreground.AsColour());
        });
    }

    [AvaloniaFact]
    public void A_button_of_ours_can_still_be_pressed()
    {
        // A template of our own is a template that could have left out the
        // part that hears a click.
        foreach (string theme in new[] { "Ordinary", "TheButton", "Quietly", "AsALink" })
        {
            int presses = 0;
            Button button = AButton(theme, "Press");
            button.Click += (_, _) => presses++;

            Window window = WhatWasDrawn.Showing(button);
            Point middle = button.MiddleIn(window);

            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);

            Assert.True(presses == 1, $"A button wearing {theme} was pressed once and heard it {presses} times.");

            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_look_somebody_chose_is_the_look_the_program_wears()
    {
        Application app = Application.Current!;
        ThemeVariant? before = app.RequestedThemeVariant;
        List<string> kept = [];

        try
        {
            ChoosingTheLook choice = new("dark", kept.Add);
            WearingTheTheme.Follow(app, choice);

            Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);

            // Round the circle: following the desktop, light, dark.
            choice.Next();
            Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);

            choice.Next();
            Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);

            choice.Next();
            Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);

            Assert.Equal(["following windows", "light", "dark"], kept);
        }
        finally
        {
            app.RequestedThemeVariant = before;
        }
    }

    [AvaloniaFact]
    public void With_nothing_chosen_the_program_follows_the_desktop()
    {
        Application app = Application.Current!;
        ThemeVariant? before = app.RequestedThemeVariant;

        try
        {
            app.RequestedThemeVariant = ThemeVariant.Dark;

            WearingTheTheme.Follow(app, new ChoosingTheLook(null, _ => { }));

            Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);
        }
        finally
        {
            app.RequestedThemeVariant = before;
        }
    }

    [Fact]
    public void No_colour_is_written_in_figures_outside_the_palette()
    {
        List<string> stray = [];

        foreach (string file in Directory.EnumerateFiles(TheLinuxApp(), "*.axaml"))
        {
            // The comments go first, whole, because they quote figures from
            // the Windows app's history. Then the palette's own brushes, the
            // one place a figure belongs. Any figure still standing is stray.
            string markup = Regex.Replace(File.ReadAllText(file), "<!--.*?-->", string.Empty, RegexOptions.Singleline);
            markup = Regex.Replace(markup, "<SolidColorBrush x:Key=\"[A-Za-z]+\" Color=\"#[0-9A-Fa-f]{6}\" />", string.Empty);

            // Not after a letter: Border#Shape names a part, not a colour.
            foreach (Match figure in Regex.Matches(markup, @"(?<![A-Za-z])#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})\b"))
            {
                stray.Add(Path.GetFileName(file) + ": " + figure.Value);
            }
        }

        Assert.True(stray.Count == 0, "A colour in figures, where the palette cannot see it: " + string.Join("; ", stray));
    }

    [Fact]
    public void Every_colour_of_the_palette_is_asked_for_afresh_and_never_once_for_ever()
    {
        string[] names = [.. TheWindowsPalette("Look.xaml").Keys];
        List<string> fixedForEver = [];

        foreach (string file in Directory.EnumerateFiles(TheLinuxApp(), "*.axaml"))
        {
            foreach (Match asked in Regex.Matches(File.ReadAllText(file), "\\{StaticResource ([A-Za-z]+)\\}"))
            {
                // Resolved statically a brush is answered once, in whichever
                // look the window opened in, and the other look never reaches it.
                if (names.Contains(asked.Groups[1].Value))
                {
                    fixedForEver.Add(Path.GetFileName(file) + ": " + asked.Value);
                }
            }
        }

        Assert.True(fixedForEver.Count == 0, string.Join("; ", fixedForEver));
    }

    private static Button AButton(string theme, string words) =>
        new()
        {
            Content = words,
            Theme = (ControlTheme)Application.Current!.FindResource(theme)!,
        };

    private static string TheLinuxApp() => Repository.File("src", "DiscWright.LinuxApp");

    /// <summary>Every brush a Windows look names, read out of the Windows app's own file.</summary>
    private static Dictionary<string, Color> TheWindowsPalette(string file) =>
        Brushes(XDocument.Load(Repository.File("src", "DiscWright.App", file)).Root!);

    /// <summary>The same, out of the Linux app's file: the light look's are inside Look.axaml.</summary>
    private static Dictionary<string, Color> TheLinuxPalette(string file, string which)
    {
        XElement root = XDocument.Load(Path.Combine(TheLinuxApp(), file)).Root!;

        return Brushes(which == "Light"
            ? root.Descendants().Single(each =>
                each.Name.LocalName == "ResourceDictionary" && (string?)each.Attribute(X + "Key") == "Light")
            : root);
    }

    private static Dictionary<string, Color> Brushes(XElement within) =>
        within
            .Descendants()
            .Where(each => each.Name.LocalName == "SolidColorBrush")
            .ToDictionary(
                each => (string)each.Attribute(X + "Key")!,
                each => Color.Parse((string)each.Attribute("Color")!));
}
