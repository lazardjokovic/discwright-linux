using System.ComponentModel;

using DiscWright.Core;

namespace DiscWright.ViewModels;

/// <summary>How the question about what else was in a folder was put away.</summary>
public enum HowItWasAnswered
{
    /// <summary>The ticked ones go on the disc with the game.</summary>
    PutTheseOnToo,

    /// <summary>A decision to leave every one of them off, taken by somebody who read the list.</summary>
    NoneOfThem,

    /// <summary>
    /// The window was shut, which is neither answer: somebody wanting out of
    /// the question. The folder is left alone and can be pointed at again.
    /// </summary>
    ShutWithoutAnswering,
}

/// <summary>
/// What else was in the folder a game came out of, and what somebody wants
/// done with each thing: everything the question knows, with no window in it.
/// </summary>
/// <remarks>
/// <para>
/// In the Windows app this lives in the dialog's own code, where nothing can
/// test it: which rows start ticked, when the big button greys, that the
/// cross is not an answer, what "the same for the rest" carries forward. A
/// second window would have had to type all of it out again, untested twice.
/// So it is here, once, for the Linux window to stand on, and for the Windows
/// dialog to stand on too the day the two are brought together. Until then
/// the Windows dialog keeps its own copy, since nothing of the Windows app's
/// is moved here.
/// </para>
/// <para>
/// A folder is one row, not one per file inside it: an artbook is one
/// answer, not forty.
/// </para>
/// </remarks>
public sealed class WhatElseWasInThere : INotifyPropertyChanged
{
    private readonly GameFolder _game;

    /// <param name="game">The game the folder turned out to be.</param>
    /// <param name="left">What the folder held beyond it.</param>
    /// <param name="after">How many more folders are still to be asked about after this one.</param>
    public WhatElseWasInThere(GameFolder game, IReadOnlyList<LeftBehind> left, int after)
    {
        _game = game;

        string? installer = game.Installer?.Name;
        IReadOnlyList<LeftBehind> rows = WhatWasLeftBehind.Gathered(left);

        Files = [.. rows.Select(each => new OneThingLeftBehind(each, installer))];
        Headline = WhatWasLeftBehind.Headline(rows);
        AboutPatches = WhatWasLeftBehind.AboutThePatches(installer, left);
        TheSameForTheRestSays = WhatWasLeftBehind.TheSameForTheRest(after);

        // The two things that follow the ticks follow every tick: the big
        // button greys when nothing is wanted, and the tick-them-all link
        // greys when there is nothing left for it to tick.
        foreach (OneThingLeftBehind each in Files)
        {
            each.PropertyChanged += (_, what) =>
            {
                if (what.PropertyName == nameof(OneThingLeftBehind.Wanted))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnyWanted)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanTickThemAll)));
                }
            };
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Whether there is anything to ask. Nothing is asked when nothing was
    /// left behind, which is every folder of game files and most downloads:
    /// a dialog that appears to say "there is nothing to tell you" is worse
    /// than silence.
    /// </summary>
    public static bool IsWorthAsking(IReadOnlyList<LeftBehind> left) => left.Count > 0;

    /// <summary>The answer when nothing was left behind and nobody was asked.</summary>
    public static (GameFolder Game, IReadOnlyList<LeftBehind> AddOns, TheSameAnswers? ForTheRest) WithNothingToAsk(
        GameFolder game) => (game, [], null);

    /// <summary>The files, each with what somebody wants done with it.</summary>
    public IReadOnlyList<OneThingLeftBehind> Files { get; }

    /// <summary>The line at the top.</summary>
    public string Headline { get; }

    /// <summary>
    /// What a patch is and what this disc's installer already carries, said
    /// once above the rows, or nothing when no patch is among the files.
    /// </summary>
    public string? AboutPatches { get; }

    /// <summary>What the tick for the games after this one says, or nothing when none follow.</summary>
    public string? TheSameForTheRestSays { get; }

    /// <summary>Whether the games after this one are to be answered the same way.</summary>
    public bool TheSameForTheRest { get; set; }

    /// <summary>
    /// Whether anything is ticked at all. With nothing ticked, "Put these on
    /// too" would put nothing on, which is the other button's job, so it
    /// greys instead of lying.
    /// </summary>
    public bool AnyWanted => Files.Any(one => one.Wanted);

    /// <summary>Whether the tick-them-all link has anything left to do.</summary>
    public bool CanTickThemAll => Files.Any(one => !one.Wanted);

    /// <summary>
    /// Every row ticked at once, for the person keeping everything: the
    /// patch defaults untick what the installer covers, and an archive disc
    /// wants it anyway.
    /// </summary>
    public void TickThemAll()
    {
        foreach (OneThingLeftBehind each in Files)
        {
            each.Wanted = true;
        }
    }

    /// <summary>
    /// The game as it should now be and whatever is to be attached to it, or
    /// nothing when the question was shut rather than answered.
    /// </summary>
    /// <remarks>
    /// "None of them" and the cross both put nothing on, and they are not the
    /// same thing. Treating the cross as the decision dropped every file in
    /// silence, which is the one thing this question exists to prevent.
    /// </remarks>
    public (GameFolder Game, IReadOnlyList<LeftBehind> AddOns, TheSameAnswers? ForTheRest)? Answered(HowItWasAnswered how)
    {
        if (how == HowItWasAnswered.ShutWithoutAnswering)
        {
            return null;
        }

        bool taking = how == HowItWasAnswered.PutTheseOnToo;

        // The same answers for the games after this one: None of them
        // included, which is an answer too.
        TheSameAnswers? forTheRest = TheSameForTheRest && TheSameForTheRestSays is not null
            ? TheSameAnswers.From(
                Files.Select(one => (one.What, one.Wanted && taking, (WhatBecomesOfIt)one.Treatment)),
                _game.Installer?.Name)
            : null;

        if (!taking)
        {
            return (_game, [], forTheRest);
        }

        (GameFolder taken, IReadOnlyList<LeftBehind> addOns) = WhatWasLeftBehind.Answered(
            _game,
            Files.Where(one => one.Wanted).Select(one => (one.What, (WhatBecomesOfIt)one.Treatment)));

        return (taken, addOns, forTheRest);
    }
}

/// <summary>One thing a folder held beyond the game, and what is to become of it.</summary>
public sealed class OneThingLeftBehind : INotifyPropertyChanged
{
    private readonly string? _installer;
    private bool _wanted;
    private int _treatment;

    internal OneThingLeftBehind(LeftBehind what, string? installer)
    {
        What = what;
        _installer = installer;

        // Ticked to begin with, because somebody pointed at the folder these
        // came from, and the surprise worth avoiding is losing a file rather
        // than gaining one. The one exception is a patch the installer
        // provably already covers, which starts off with its reason on its
        // line, the owner's design on 2026-11-09.
        _wanted = what.WantedAgainst(installer);
        _treatment = (int)what.Suggested;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The thing itself.</summary>
    public LeftBehind What { get; }

    public string Name => What.Name;

    public string Size => ByteSize.Describe(What.Bytes);

    /// <summary>
    /// What the row's dropdown answers to, which has to differ per row.
    /// </summary>
    /// <remarks>
    /// Every row in the list is the same template, so an id written into it
    /// would be the same id on all of them, and a test finding the first
    /// would pass while saying nothing about the rest.
    /// </remarks>
    public string WhatToDoWithIt => Name + ": what to do with it";

    /// <summary>
    /// The line under the name: what the file looks like, and for a patch
    /// the names can judge, what it adds against this disc's installer.
    /// </summary>
    public string Explains => What.ExplainedAgainst(_installer);

    public bool Wanted
    {
        get => _wanted;
        set
        {
            _wanted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Wanted)));
        }
    }

    /// <summary>Which of the answers, by <see cref="WhatBecomesOfIt"/>, in the dropdown's order.</summary>
    public int Treatment
    {
        get => _treatment;
        set
        {
            _treatment = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Treatment)));
        }
    }
}
