using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

using DiscWright.Tests;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// Everything the look has, drawn on one sheet in each look and left as a
/// picture, for the part of a look that only an eye can judge.
/// </summary>
/// <remarks>
/// The tests beside this one hold each figure. None of them can say whether
/// the result is pleasant, whether the dead button reads as dead, or whether
/// the weights sit well together in Inter. So this draws them all with the
/// same Skia the window is drawn with and saves the sheets under
/// <c>artifacts/the-look</c>, where a pull request can pick them up.
/// </remarks>
public class ASheetOfTheLook
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Is_drawn_and_kept(string which)
    {
        ThemeVariant look = which == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        string kept = Repository.File("artifacts", "the-look", which.ToLowerInvariant() + ".png");

        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        File.Delete(kept);

        WhatWasDrawn.Wearing(look, () =>
        {
            Window sheet = new() { Width = 560, Height = 420, Content = TheSheet() };
            sheet.Show();
            Dispatcher.UIThread.RunJobs();

            using WriteableBitmap drawn = sheet.CaptureRenderedFrame()!;
            drawn.Save(kept, new PngBitmapEncoderOptions());

            // The page at one corner and the accent on the one button that
            // wears it: enough to know the sheet is not blank and is in the
            // look it was asked for.
            Assert.Equal(WhatWasDrawn.Of("Page", look), sheet.ColourAt(new Point(4, 4)));

            Button making = ((Control)sheet.Content!).Part<Button>("Making");
            Point inside = making.TranslatePoint(new Point(5, making.Bounds.Height / 2), sheet)!.Value;
            Assert.Equal(WhatWasDrawn.Of("Accent", look), sheet.ColourAt(inside));
        });

        Assert.True(new FileInfo(kept).Length > 2000, "The sheet was saved as next to nothing.");
    }

    private static StackPanel TheSheet()
    {
        Application app = Application.Current!;

        ControlTheme Theme(string name) => (ControlTheme)app.FindResource(name)!;

        StackPanel Row(params Control[] across)
        {
            StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 14) };
            row.Children.AddRange(across);

            return row;
        }

        return new StackPanel
        {
            Margin = new Thickness(28, 16, 28, 18),
            Children =
            {
                new TextBlock { Text = "DiscWright", Classes = { "Title" }, Margin = new Thickness(0, 0, 0, 14) },
                new TextBlock { Text = "1. What goes on it", Classes = { "Step" } },
                new TextBlock { Text = "Ordinary words, thirteen, in the ink: Hollow Knight, 9.1 GB.", Margin = new Thickness(0, 0, 0, 4) },
                new TextBlock
                {
                    Text = "An aside is quieter and wraps when it runs out of room, which this one does because it goes on for longer than the sheet is wide.",
                    Classes = { "Aside" },
                    Margin = new Thickness(0, 0, 0, 14),
                },
                Row(
                    new Button { Content = "Add a game", Theme = Theme("Ordinary") },
                    new Button { Content = "Burn to a disc", Theme = Theme("Ordinary"), IsEnabled = false }),
                Row(
                    new Button { Name = "Making", Content = "Make the disc", Theme = Theme("TheButton") },
                    new Button { Content = "Make the disc", Theme = Theme("TheButton"), IsEnabled = false }),
                Row(
                    new Button { Content = "Check for a new version", Theme = Theme("Quietly") },
                    new Button { Content = "Take it off the list", Theme = Theme("AsALink") },
                    new Button { Content = "Not just now", Theme = Theme("AsALink"), IsEnabled = false }),
                new ProgressBar { Value = 40, Maximum = 100, Margin = new Thickness(0, 4, 0, 0) },
            },
        };
    }
}
