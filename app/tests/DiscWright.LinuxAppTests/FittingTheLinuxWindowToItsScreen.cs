using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using DiscWright.LinuxApp;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The window on a screen with less room than it was drawn for.
/// </summary>
/// <remarks>
/// How big it should be is <see cref="FittingTheScreen"/>'s arithmetic,
/// tested in the shared suite. What is held here is that the Linux window
/// takes the answer: its size, the smallest it may then be made, and where
/// it is put. The room is handed in, since a test has no say in the screen
/// it runs on. How much room a real desktop has was seen on the test
/// machine.
/// </remarks>
public class FittingTheLinuxWindowToItsScreen
{
    [AvaloniaFact]
    public void On_a_screen_with_room_to_spare_the_window_is_left_exactly_as_it_was_put()
    {
        MainWindow window = TheWindow.WithNothingOnIt();
        PixelPoint where = window.Position;

        OnThisScreen.Fit(window, new FittingTheScreen.Room(0, 0, 1920, 1040));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(920, window.Width);
        Assert.Equal(740, window.Height);
        Assert.Equal(560, window.MinHeight);
        Assert.Equal(where, window.Position);
    }

    [AvaloniaFact]
    public void On_the_screen_of_a_laptop_it_is_made_as_tall_as_the_room_and_put_at_the_top_of_it()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        // 1366 by 768 with a panel of 44 along the bottom and a title bar of
        // 28 on the window: what the test machine has, near enough.
        OnThisScreen.Fit(window, new FittingTheScreen.Room(0, 0, 1366, 696));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(920, window.Width);
        Assert.Equal(696, window.Height);
        Assert.Equal(696, window.Bounds.Height);

        // Across the middle, and from the top of the room down.
        Assert.Equal(new PixelPoint(223, 0), window.Position);

        // It can still be made shorter, down to where it always could.
        Assert.Equal(560, window.MinHeight);

        // And what matters: the button that builds the disc is on it.
        Button build = window.Found<Button>("MakeTheDisc");
        double itsBottom = build.TranslatePoint(new Point(0, build.Bounds.Height), window)!.Value.Y;

        Assert.True(build.CanBeSeen());
        Assert.True(itsBottom < 696, $"Build it ends at {itsBottom} on a window 696 tall.");
    }

    [AvaloniaFact]
    public void A_room_that_does_not_start_at_the_corner_is_where_the_window_goes()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        // A panel down the left and one along the top.
        OnThisScreen.Fit(window, new FittingTheScreen.Room(60, 30, 1000, 650));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(650, window.Height);
        Assert.Equal(new PixelPoint(60 + 40, 30), window.Position);
    }

    [AvaloniaFact]
    public void On_a_screen_smaller_than_the_smallest_it_may_be_it_is_held_to_the_screen_and_not_larger()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        OnThisScreen.Fit(window, new FittingTheScreen.Room(0, 0, 800, 500));
        Dispatcher.UIThread.RunJobs();

        // The smallest it may be made can never be more than the screen, or
        // it would be held larger than the room again.
        Assert.Equal(800, window.MinWidth);
        Assert.Equal(500, window.MinHeight);
        Assert.Equal(800, window.Width);
        Assert.Equal(500, window.Height);
    }

    [AvaloniaFact]
    public void Where_one_unit_of_the_window_is_two_pixels_of_the_screen_it_is_put_by_pixels()
    {
        MainWindow window = TheWindow.WithNothingOnIt();

        OnThisScreen.Fit(window, new FittingTheScreen.Room(0, 0, 1366, 696), scale: 2);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(696, window.Height);
        Assert.Equal(new PixelPoint(446, 0), window.Position);
    }
}
