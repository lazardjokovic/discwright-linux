using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.LinuxApp;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The window itself, before there is anything in it: what it is called, how
/// big it is, and that it can be drawn at all.
/// </summary>
/// <remarks>
/// The numbers are the Windows window's, from <c>MainWindow.xaml</c>, and they
/// are held here because each of them was arrived at by something going wrong
/// on a real screen. A Linux window that quietly took a different width would
/// meet the same faults again.
/// </remarks>
public class TheLinuxWindowOpens
{
    [AvaloniaFact]
    public void Is_called_DiscWright()
    {
        MainWindow window = Opened();

        Assert.Equal("DiscWright", window.Title);
    }

    [AvaloniaFact]
    public void Answers_to_the_name_the_Windows_window_answers_to()
    {
        // Whatever drives the window finds it by this, on both systems.
        Assert.Equal("TheWindow", AutomationProperties.GetAutomationId(Opened()));
    }

    [AvaloniaFact]
    public void Opens_at_the_size_it_was_drawn_at()
    {
        MainWindow window = Opened();

        Assert.Equal(920, window.Bounds.Width);
        Assert.Equal(740, window.Bounds.Height);
    }

    [AvaloniaFact]
    public void Cannot_be_made_wider_or_narrower()
    {
        MainWindow window = Opened();

        // The games list lines its sizes up at a fixed width. Narrower cut
        // them off and wider left them stranded, both seen on a real screen.
        Assert.Equal(920, window.MinWidth);
        Assert.Equal(920, window.MaxWidth);
    }

    [AvaloniaFact]
    public void Can_be_made_as_short_as_an_older_laptop_needs_and_no_shorter()
    {
        MainWindow window = Opened();

        // 1366 by 768 with a taskbar has no room for 740.
        Assert.True(window.CanResize);
        Assert.Equal(560, window.MinHeight);
        Assert.Equal(double.PositiveInfinity, window.MaxHeight);
    }

    [AvaloniaFact]
    public void Carries_an_icon()
    {
        // Without one a desktop shows a blank square in the title bar and
        // the task bar, which is what the first window on the test machine had.
        Assert.NotNull(Opened().Icon);
    }

    [AvaloniaFact]
    public void Can_be_drawn_whole()
    {
        MainWindow window = Opened();

        WriteableBitmap? drawn = window.CaptureRenderedFrame();

        Assert.NotNull(drawn);
        Assert.Equal(920, drawn.PixelSize.Width);
        Assert.Equal(740, drawn.PixelSize.Height);
    }

    [AvaloniaFact]
    public void Pictures_for_a_disc_can_be_read_in_the_process_that_draws_the_window()
    {
        // Draw the window, save what was drawn, and have the code that makes
        // a disc's icon read it back: both are Skia, in one process.
        string saved = Path.Combine(Path.GetTempPath(), "dw-window-" + Guid.NewGuid().ToString("N") + ".png");

        try
        {
            Opened().CaptureRenderedFrame()!.Save(saved, new PngBitmapEncoderOptions());

            ImageFacts facts = new SkiaImages().Read(saved);

            Assert.True(facts.Readable, facts.Why);
            Assert.Equal(920, facts.Width);
            Assert.Equal(740, facts.Height);
        }
        finally
        {
            File.Delete(saved);
        }
    }

    [AvaloniaFact]
    public void The_Skia_in_the_process_is_the_one_Avalonia_was_built_for()
    {
        // DiscWright.Linux asks for SkiaSharp by version, for the disc's
        // pictures, and Avalonia brings its own. When the two were different
        // (4.156 beside 3.119) every test here still passed, and the app
        // published as one file died as it started: two native libraries of
        // one name went into it, and the older one won. So this holds the
        // versions together where a test can see them, and the-published-app
        // script starts the real file.
        Version? builtFor = typeof(Avalonia.Skia.SkiaPlatform).Assembly
            .GetReferencedAssemblies()
            .Single(each => each.Name == "SkiaSharp")
            .Version;

        Version? loaded = typeof(SkiaSharp.SKBitmap).Assembly.GetName().Version;

        Assert.NotNull(builtFor);
        Assert.True(
            loaded?.Major == builtFor.Major && loaded.Minor == builtFor.Minor,
            $"Avalonia was built for SkiaSharp {builtFor} and the one in the process is {loaded}. "
                + "DiscWright.Linux.csproj names the version, and it has to follow Avalonia's.");
    }

    private static MainWindow Opened()
    {
        MainWindow window = new();
        window.Show();

        return window;
    }
}
