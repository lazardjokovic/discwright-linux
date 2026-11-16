using Avalonia.Controls;
using Avalonia.Interactivity;

using DiscWright.Core;
using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// What else was in the folder, and what to do with it.
/// </summary>
/// <remarks>
/// The window and nothing more. Which rows start ticked, when the big button
/// greys, what each answer means and that the cross is not one of them are
/// all <see cref="WhatElseWasInThere"/>'s, where they are tested. What is
/// left here is which of the three ways the window was put away.
/// </remarks>
public partial class WhatElseIsInThere : Window
{
    private readonly WhatElseWasInThere _asking;

    /// <summary>
    /// How the window was put away. Shut, until a button says otherwise, so
    /// the cross and anything else that closes it are the same thing: not an
    /// answer.
    /// </summary>
    private HowItWasAnswered _how = HowItWasAnswered.ShutWithoutAnswering;

    private WhatElseIsInThere(WhatElseWasInThere asking)
    {
        _asking = asking;

        InitializeComponent();
        DataContext = asking;
    }

    /// <summary>
    /// Asks about the files a folder held beyond the game, and answers with
    /// the game as it should now be and whatever is to be attached to it, or
    /// with nothing when the question was shut rather than answered.
    /// </summary>
    /// <remarks>
    /// Written to the log like every other question. The Windows dialog
    /// writes nothing, and a set rebuilt from the log would not know what
    /// was left off or why.
    /// </remarks>
    public static async Task<(GameFolder Game, IReadOnlyList<LeftBehind> AddOns, TheSameAnswers? ForTheRest)?> Ask(
        Window owner, GameFolder game, IReadOnlyList<LeftBehind> left, int after)
    {
        if (!WhatElseWasInThere.IsWorthAsking(left))
        {
            return WhatElseWasInThere.WithNothingToAsk(game);
        }

        WhatElseWasInThere asking = new(game, left, after);
        WhatElseIsInThere window = new(asking);

        WorthAsking.Log.Write(
            $"Asked: {asking.Headline}, beside {game.Name} ({string.Join("; ", asking.Files.Select(one => one.Name))})");

        await window.ShowDialog(owner);

        WorthAsking.Log.Write(window._how switch
        {
            HowItWasAnswered.PutTheseOnToo =>
                "Answered: Put these on too ("
                    + string.Join("; ", asking.Files.Where(one => one.Wanted).Select(one => one.Name)) + ")",
            HowItWasAnswered.NoneOfThem => "Answered: None of them",
            _ => "Answered: nothing, the question was shut",
        });

        return asking.Answered(window._how);
    }

    private void TickThemAll(object? sender, RoutedEventArgs e) => _asking.TickThemAll();

    private void TakeThem(object? sender, RoutedEventArgs e)
    {
        _how = HowItWasAnswered.PutTheseOnToo;
        Close();
    }

    private void NoneOfThem(object? sender, RoutedEventArgs e)
    {
        _how = HowItWasAnswered.NoneOfThem;
        Close();
    }
}
