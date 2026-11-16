using Avalonia.Controls;
using Avalonia.Interactivity;

using DiscWright.Core;

namespace DiscWright.LinuxApp;

/// <summary>
/// A question with two answers, where neither is a refusal.
/// </summary>
/// <remarks>
/// <para>
/// It asks the questions the Windows one asks, in the same words, and writes
/// each question and its answer to the same log.
/// </para>
/// <para>
/// One thing differs, and it is the framework's: on Windows the answer is
/// there when <c>ShowDialog</c> returns, and here it is awaited. Every caller
/// is an <c>await</c>, and the window behind stays alive and cannot be
/// pressed while the question stands.
/// </para>
/// </remarks>
public partial class WorthAsking : Window
{
    private WorthAsking(string headline, string because, string likely, string other)
    {
        Headline = headline;
        Because = because;
        TheLikelyOne = likely;
        TheOtherWay = other;

        InitializeComponent();
        DataContext = this;
    }

    public string Headline { get; }

    public string Because { get; }

    /// <summary>What most people want, on the button Enter presses.</summary>
    public string TheLikelyOne { get; }

    public string TheOtherWay { get; }

    /// <summary>
    /// Where questions and their answers are written down. Set once by the
    /// app as it starts, and nowhere until then.
    /// </summary>
    public static IKeepALog Log { get; set; } = new NothingIsLogged();

    /// <summary>
    /// Asks, and says whether the likely answer was taken.
    /// </summary>
    /// <remarks>
    /// Shut with the cross, it was not: that is the other way, never a third
    /// answer and never the likely one by default.
    /// </remarks>
    public static async Task<bool> Ask(Window owner, string headline, string because, string likely, string other)
    {
        Log.Write($"Asked: {headline}");

        bool likelyOne = await new WorthAsking(headline, because, likely, other).ShowDialog<bool?>(owner) == true;

        Log.Write($"Answered: {(likelyOne ? likely : other)}");

        return likelyOne;
    }

    private void TakeTheLikelyOne(object? sender, RoutedEventArgs e) => Close(true);

    private void TakeTheOtherWay(object? sender, RoutedEventArgs e) => Close(false);
}
