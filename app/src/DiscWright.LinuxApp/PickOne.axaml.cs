using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DiscWright.LinuxApp;

/// <summary>
/// One out of several, where picking for somebody would be guessing.
/// </summary>
/// <remarks>
/// <c>PickOne</c> in the Windows app, awaited where that one waits on the
/// spot, and written to the same log in the same words.
/// </remarks>
public partial class PickOne : Window
{
    private PickOne(string headline, string because, IEnumerable<string> choices, bool pickTheFirst)
    {
        Headline = headline;
        Because = because;
        Choices = [.. choices];

        InitializeComponent();
        DataContext = this;

        // NOTHING IS CHOSEN FOR SOMEBODY WHEN THE ANSWER DESTROYS A DISC.
        //
        // The list used to start on the first row with "Use that one" as the
        // default button, so Enter erased whichever drive happened to be
        // first. That is fine for picking an image, where the worst case is
        // building the wrong one again, and wrong for erasing, where there
        // is nothing to undo.
        TheList.SelectedIndex = pickTheFirst ? 0 : -1;
        TheButton.IsEnabled = TheList.SelectedIndex >= 0;

        // A double press on a row takes it. Among presses already dealt
        // with, because a row keeps the press that picked it.
        TheList.AddHandler(DoubleTappedEvent, TakeTheRowThatWasPressedTwice, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public string Headline { get; }

    public string Because { get; }

    public IReadOnlyList<string> Choices { get; }

    /// <summary>
    /// Asks, and answers with the one chosen, or with nothing when none of
    /// them was: Cancel, Escape and the cross alike.
    /// </summary>
    /// <param name="owner">The window it stands in front of.</param>
    /// <param name="headline">What is being asked.</param>
    /// <param name="because">Why somebody is being asked rather than guessed for.</param>
    /// <param name="choices">What there is to choose from.</param>
    /// <param name="describe">Each choice in words.</param>
    /// <param name="pickTheFirst">
    /// Whether the first row starts chosen. Never where the answer cannot be
    /// taken back.
    /// </param>
    public static async Task<T?> From<T>(
        Window owner,
        string headline,
        string because,
        IReadOnlyList<T> choices,
        Func<T, string> describe,
        bool pickTheFirst = true)
        where T : class
    {
        PickOne asking = new(headline, because, choices.Select(describe), pickTheFirst);

        // Written down like every yes or no, through the same log. Which disc
        // to erase came through the Windows one on 2026-11-10 and the log had
        // the erase but not the question.
        WorthAsking.Log.Write($"Asked: {headline} ({string.Join("; ", asking.Choices)})");

        int chosen = await asking.ShowDialog<int?>(owner) ?? -1;

        T? answer = chosen >= 0 && chosen < choices.Count ? choices[chosen] : null;

        WorthAsking.Log.Write(answer is null ? "Answered: none of them" : $"Answered: {asking.Choices[chosen]}");

        return answer;
    }

    private void TakeThatOne(object? sender, RoutedEventArgs e)
    {
        if (TheList.SelectedIndex >= 0)
        {
            Close(TheList.SelectedIndex);
        }
    }

    private void TakeTheRowThatWasPressedTwice(object? sender, TappedEventArgs e) => TakeThatOne(sender, e);

    private void NoneOfThem(object? sender, RoutedEventArgs e) => Close(null);

    private void SomethingWasChosen(object? sender, SelectionChangedEventArgs e) =>
        TheButton.IsEnabled = TheList.SelectedIndex >= 0;
}
