using Avalonia.Controls;
using Avalonia.Interactivity;

using DiscWright.Core;

namespace DiscWright.LinuxApp;

/// <summary>
/// Something said plainly, with one way to answer it.
/// </summary>
/// <remarks>
/// <para>
/// What a message box is in the Windows app. Every refusal arrives in one,
/// and so do the few things that are not refusals and still have to be said
/// in front of somebody: the address of a page when no browser would open,
/// where the log is when nothing would open that.
/// </para>
/// <para>
/// What it says can be selected and copied, because the commonest thing said
/// here that is not a refusal is an address.
/// </para>
/// </remarks>
public partial class SayingSo : Window
{
    private SayingSo(string headline, string because, IReadOnlyList<Figure> figures)
    {
        Headline = headline;
        Because = because;
        Figures = figures;

        InitializeComponent();
        DataContext = this;
    }

    public string Headline { get; }

    public string Because { get; }

    public bool HasMoreToSay => Because.Length > 0;

    /// <summary>
    /// What a refusal has to show beside its words: a size and the room for
    /// it, or the line to type to get what is missing.
    /// </summary>
    public IReadOnlyList<Figure> Figures { get; }

    public bool HasFigures => Figures.Count > 0;

    /// <summary>Says it, and comes back when it has been read.</summary>
    public static Task Tell(Window owner, string headline, string because) =>
        new SayingSo(headline, because, []).ShowDialog(owner);

    /// <summary>
    /// A refusal, with everything it knows: what, why, and its figures.
    /// </summary>
    /// <remarks>
    /// The figures too. They were left out, as a Windows message box leaves
    /// them out, and on Linux that left out the one thing the refusal for a
    /// missing xorriso is for: the line to type.
    /// </remarks>
    public static Task Explain(Window owner, Refusal refusal) =>
        new SayingSo(refusal.Headline, refusal.Because, refusal.Figures).ShowDialog(owner);

    private void Understood(object? sender, RoutedEventArgs e) => Close();
}
