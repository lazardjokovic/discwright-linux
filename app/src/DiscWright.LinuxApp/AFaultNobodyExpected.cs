using Avalonia.Controls;
using Avalonia.Threading;

using DiscWright.Core;

namespace DiscWright.LinuxApp;

/// <summary>
/// What happens to a fault inside a press that nothing caught: it is written
/// down whole, said in front of somebody, and the program carries on.
/// </summary>
/// <remarks>
/// <para>
/// A fault inside a click would otherwise close the program with no word
/// about why, which is the one thing worse than a crash. The Windows app
/// hears these through its dispatcher and so does this.
/// </para>
/// <para>
/// It matters more here than there. A handler that asks a question is
/// <c>async void</c> on this side, since a question is awaited, and a fault
/// in one of those has no caller to land on. It arrives here or nowhere.
/// </para>
/// <para>
/// Whole, in the log, because the box is gone at OK and the stack is the one
/// thing that says where it happened.
/// </para>
/// </remarks>
public static class AFaultNobodyExpected
{
    public const string Headline = "DiscWright stopped";

    /// <summary>
    /// Starts listening, and stops when what it hands back is disposed of.
    /// </summary>
    /// <param name="dispatcher">The thread the window lives on.</param>
    /// <param name="log">Where the fault is written down, stack and all.</param>
    /// <param name="inFrontOf">The window to say it in front of, when there is one.</param>
    public static IDisposable IsHeardBy(Dispatcher dispatcher, IKeepALog log, Func<Window?> inFrontOf)
    {
        void Hear(object sender, DispatcherUnhandledExceptionEventArgs wrong)
        {
            log.Write($"Stopped by something nobody expected: {wrong.Exception}");

            // Before anything is shown: a box that could not be put up must
            // not turn a fault that was caught into one that was not.
            wrong.Handled = true;

            if (inFrontOf() is Window window && window.IsVisible)
            {
                _ = SayingSo.Tell(window, Headline, wrong.Exception.ToString());
            }
        }

        dispatcher.UnhandledException += Hear;

        return new NoLongerListening(() => dispatcher.UnhandledException -= Hear);
    }

    private sealed class NoLongerListening(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }
}
