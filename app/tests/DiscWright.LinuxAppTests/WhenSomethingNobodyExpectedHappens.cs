using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using DiscWright.Core;
using DiscWright.LinuxApp;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// A fault inside a press that nothing caught.
/// </summary>
/// <remarks>
/// Thrown for real, on the thread the window lives on, the way a fault in a
/// handler arrives. The program has to still be there afterwards.
/// </remarks>
public class WhenSomethingNobodyExpectedHappens
{
    [AvaloniaFact]
    public void It_is_written_down_whole_and_said_in_front_of_somebody()
    {
        Heard log = new();
        Window window = AWindow();

        using (AFaultNobodyExpected.IsHeardBy(Dispatcher.UIThread, log, () => window))
        {
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("The list was changed while it was read."));
            Dispatcher.UIThread.RunJobs();

            // Whole: what it was, what it said, and where, because the box
            // is gone at OK and the stack is the one thing that says where.
            string written = Assert.Single(log.Lines);

            Assert.StartsWith("Stopped by something nobody expected: System.InvalidOperationException", written, StringComparison.Ordinal);
            Assert.Contains("The list was changed while it was read.", written, StringComparison.Ordinal);
            Assert.Contains("   at ", written, StringComparison.Ordinal);

            SayingSo box = Assert.Single(window.OwnedWindows.OfType<SayingSo>());

            Assert.Equal("DiscWright stopped", box.Found<TextBlock>("Headline").Text);
            Assert.Contains("The list was changed while it was read.", box.Found<SelectableTextBlock>("Because").Text, StringComparison.Ordinal);

            box.Close();
        }
    }

    [AvaloniaFact]
    public void The_program_is_still_there_afterwards()
    {
        Window window = AWindow();
        bool ranAfterwards = false;

        using (AFaultNobodyExpected.IsHeardBy(Dispatcher.UIThread, new Heard(), () => window))
        {
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("One press went wrong."));
            Dispatcher.UIThread.Post(() => ranAfterwards = true);
            Dispatcher.UIThread.RunJobs();

            Assert.True(ranAfterwards, "What was waiting behind the fault never ran.");
            Assert.True(window.IsVisible);

            foreach (SayingSo box in window.OwnedWindows.OfType<SayingSo>().ToArray())
            {
                box.Close();
            }
        }
    }

    [AvaloniaFact]
    public void A_fault_from_a_handler_that_was_waiting_on_an_answer_arrives_too()
    {
        Heard log = new();
        Window window = AWindow();

        // A handler that asks a question is async void on this side, and a
        // fault in one of those has no caller to land on.
        async void AHandlerThatAsksAndThenFails()
        {
            await Task.Yield();

            throw new InvalidOperationException("After the answer came back.");
        }

        using (AFaultNobodyExpected.IsHeardBy(Dispatcher.UIThread, log, () => window))
        {
            AHandlerThatAsksAndThenFails();
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("After the answer came back.", Assert.Single(log.Lines), StringComparison.Ordinal);

            foreach (SayingSo box in window.OwnedWindows.OfType<SayingSo>().ToArray())
            {
                box.Close();
            }
        }
    }

    [AvaloniaFact]
    public void With_no_window_to_say_it_in_front_of_it_is_still_written_down()
    {
        Heard log = new();
        bool ranAfterwards = false;

        using (AFaultNobodyExpected.IsHeardBy(Dispatcher.UIThread, log, () => null))
        {
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("Before the window was up."));
            Dispatcher.UIThread.Post(() => ranAfterwards = true);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("Before the window was up.", Assert.Single(log.Lines), StringComparison.Ordinal);
            Assert.True(ranAfterwards);
        }
    }

    [AvaloniaFact]
    public void It_stops_listening_when_it_is_told_to()
    {
        Heard log = new();

        AFaultNobodyExpected.IsHeardBy(Dispatcher.UIThread, log, () => null).Dispose();

        // Nothing is listening now, so a fault is a fault again. Heard by
        // this test instead, or it would take the whole run down with it.
        bool reachedTheTest = false;

        void Hear(object sender, DispatcherUnhandledExceptionEventArgs wrong)
        {
            reachedTheTest = true;
            wrong.Handled = true;
        }

        Dispatcher.UIThread.UnhandledException += Hear;

        try
        {
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("Nobody is listening."));
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Hear;
        }

        Assert.True(reachedTheTest);
        Assert.Empty(log.Lines);
    }

    private static Window AWindow()
    {
        Window window = new() { Width = 920, Height = 740 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private sealed class Heard : IKeepALog
    {
        public List<string> Lines { get; } = [];

        public void Write(string line) => Lines.Add(line);
    }
}
