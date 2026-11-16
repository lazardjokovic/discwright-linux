using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using DiscWright.Core;
using DiscWright.Platform;
using DiscWright.Staging;
using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// The main window. It holds no decisions.
/// </summary>
/// <remarks>
/// <para>
/// <c>MainWindow.xaml.cs</c> in the Windows app, a handler at a time, with
/// each one's words and reasons. What a press means is the screen's to say.
/// What is here is the press itself, and the things only a window can do:
/// put a question in front of somebody, ask the desktop for a file.
/// </para>
/// <para>
/// A question is awaited here where the Windows window waits on the spot, so
/// a handler that asks one is <c>async</c>. Nothing else about it changes.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MakeADisc _screen;
    private readonly GameFolderReader _reader;
    private readonly IFileSystem _disk;
    private readonly ChoosingTheLook _look;
    private readonly IAskTheDesktop _desktop;

    /// <summary>Whether folders are being taken on now, a question at a time.</summary>
    private bool _takingFoldersOn;

    /// <param name="screen">Everything the window knows and can do, the same object the Windows window is handed.</param>
    /// <param name="reader">What recognises a folder as a game, for a disc opened again.</param>
    /// <param name="disk">The real disk, for the same.</param>
    /// <param name="look">Which look somebody chose, for the one control that is about the program and not the disc.</param>
    /// <param name="desktop">The desktop the window is on: its file dialog, and whatever it opens things with.</param>
    public MainWindow(
        MakeADisc screen, GameFolderReader reader, IFileSystem disk, ChoosingTheLook look, IAskTheDesktop desktop)
    {
        _screen = screen;
        _reader = reader;
        _disk = disk;
        _look = look;
        _desktop = desktop;

        InitializeComponent();
        DataContext = screen;

        // The one control not about the disc being made, so it reads its
        // words from the choice rather than from the screen.
        HowItDresses.DataContext = look;

        // A double press on the list begins a rename. Listened for here and
        // not in the markup, and for presses already dealt with: a row of
        // the tree opens and shuts on a double press and says it has dealt
        // with it, so a listener that waited to be asked never heard one.
        // The row opening and the rename beginning on the same press is what
        // the Windows window does too.
        Games.AddHandler(DoubleTappedEvent, RenameWhatIsPicked, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    static MainWindow()
    {
        // The box a row is renamed in wakes as it comes into view. WPF has an
        // event for that on the box. Here every text box is listened to once,
        // and the ones that are not a row's rename box are let pass.
        IsVisibleProperty.Changed.AddClassHandler<TextBox>((box, _) => AwakenTheRenameBox(box));
    }

    /// <summary>The log the app keeps, for the link that opens it.</summary>
    public TheLogOnDisk? TheLog { get; init; }

    private void ChooseTheNextLook(object? sender, RoutedEventArgs e) => _look.Next();

    // --------------------------------------------- renaming a game in place
    //
    // The window's share is only the gestures: a double click opens the row
    // the screen allows, Enter and Escape close it, and wandering off
    // commits like Enter does. Who may rename and what a name is are the
    // screen's decisions.

    private void RenameWhatIsPicked(object? sender, TappedEventArgs e) =>
        _screen.BeginRenaming(_screen.WhatIsSelected);

    /// <summary>
    /// The box waking up: it takes the name as it stands, so a rename
    /// abandoned last time leaves nothing stale, and the whole name is
    /// selected because the commonest rename replaces all of it.
    /// </summary>
    private static void AwakenTheRenameBox(TextBox box)
    {
        if (box.IsVisible && box.DataContext is AThingOnTheDisc row && row.IsBeingRenamed)
        {
            // The width the name it stands in for would have had.
            box.Width = row.HowWideTheNameIs;
            box.Text = row.Name;
            box.Focus();
            box.SelectAll();
        }
    }

    private void TheRenameKeys(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not AThingOnTheDisc row)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _screen.FinishRenaming(row, box.Text ?? string.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _screen.CancelRenaming(row);
            e.Handled = true;
        }
    }

    private void TheRenameBoxLostItsFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox box
            && box.DataContext is AThingOnTheDisc row
            && row.IsBeingRenamed)
        {
            _screen.FinishRenaming(row, box.Text ?? string.Empty);
        }
    }

    // ------------------------------------------- the name, and where it goes

    /// <summary>
    /// The name of a disc is in capitals, because that is how This PC shows
    /// one. WPF has a setting for it on the box. Here the box is told as
    /// somebody types, and the cursor is put back where it was.
    /// </summary>
    private void KeepTheNameInCapitals(object? sender, TextChangedEventArgs e)
    {
        if (TheName.Text is string typed && typed.Any(char.IsLower))
        {
            int cursor = TheName.CaretIndex;

            TheName.Text = typed.ToUpperInvariant();
            TheName.CaretIndex = cursor;
        }
    }

    private async void ChooseWhereItGoes(object? sender, RoutedEventArgs e)
    {
        if (await _desktop.ForAFolder(this, "Where should the discs go?") is string chosen)
        {
            _screen.OutputFolder = chosen;
        }
    }

    /// <summary>
    /// Carries out the way out of a refusal.
    /// </summary>
    /// <remarks>
    /// Which remedies are worth offering is the screen's judgement, handed
    /// over as a kind this window knows how to carry out. Anything else is
    /// not offered at all, so there is nothing here that can be pressed and
    /// do nothing.
    /// </remarks>
    private void TakeTheWayOut(object? sender, RoutedEventArgs e)
    {
        switch (_screen.TheWayOut)
        {
            case RemedyKind.PickFolder:
                ChooseWhereItGoes(sender, e);
                break;

            case RemedyKind.FocusField:
                PutTheCursorOnTheName();
                break;

            case RemedyKind.ChangeMedium:
                _screen.UseTheLargerDisc();
                break;
        }
    }

    /// <summary>
    /// The worry's way out, which is always about the name: Windows cannot
    /// show it, and the fix is to type another.
    /// </summary>
    private void TakeTheWorrysWayOut(object? sender, RoutedEventArgs e)
    {
        if (_screen.Worry?.Remedy is { Kind: RemedyKind.FocusField })
        {
            PutTheCursorOnTheName();
        }
    }

    /// <summary>
    /// Puts the cursor in the name and selects what is there, so that typing
    /// replaces it. FocusField is what every remedy about a name asks for.
    /// </summary>
    private void PutTheCursorOnTheName()
    {
        TheName.Focus();
        TheName.SelectAll();
    }

    // ------------------------------------------------------------- building it

    private async void MakeTheDisc(object? sender, RoutedEventArgs e)
    {
        // Everything it can throw is caught here rather than left to the
        // listener for faults nobody expected, so that what is said names
        // what was being done.
        try
        {
            await _screen.MakeIt();
        }
        catch (Exception wrong)
        {
            // A sentence, never the exception's own words on their own. Raw
            // machine text in a box reads as the program falling over. The
            // refusal says what was happening and that nothing is lost, and
            // still carries what was said.
            await SayingSo.Explain(this, Refusals.SomethingNobodyExpected("building the discs", wrong.Message));
            return;
        }

        if (_screen.ItWentWrong is Refusal refusal)
        {
            await SayingSo.Explain(this, refusal);
            return;
        }

        if (_screen.Made is BuiltDiscs made)
        {
            await Say(made);
        }
    }

    /// <summary>
    /// Says where the discs are, and offers to open the folder.
    /// </summary>
    /// <remarks>
    /// The Windows window names explorer.exe here. This hands the folder to
    /// the desktop, and says where it is when nothing will open it.
    /// </remarks>
    private async Task Say(BuiltDiscs made)
    {
        string where = made.Discs[0].Folder;
        string folder = made.Discs.Count == 1 ? where : Path.GetDirectoryName(where) ?? where;

        bool open = await WorthAsking.Ask(
            this,
            made.Discs.Count == 1 ? "The disc is ready" : $"All {made.Discs.Count} discs are ready",
            "In:\n\n" + folder,
            "Open the folder",
            "Not now");

        if (open && !_desktop.Open(folder))
        {
            await SayingSo.Tell(this, "Nothing would open the folder", "It is:\n\n" + folder);
        }
    }

    // ------------------------------------------------------------ adding a game

    private async void AddAGame(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> folders = await _desktop.ForFolders(this, "Which folder is the game in?");

        if (folders.Count == 0)
        {
            return;
        }

        await Add(folders);
    }

    /// <summary>
    /// Adds folders somebody named on the command line, so DiscWright can be
    /// opened with a game rather than opened and then pointed at one.
    /// </summary>
    /// <remarks>
    /// Anything that is not a folder is ignored rather than complained about.
    /// A program started with a switch it does not know should open, not stop.
    /// </remarks>
    public Task AddWhatWeWereOpenedWith(IEnumerable<string> arguments) =>
        Add([.. arguments.Where(_disk.FolderExists)]);

    /// <summary>
    /// Takes folders on: which are games, which hold games, what is asked
    /// about each and in what order all live in TakingFoldersOn, where a test
    /// can reach them. What is left here is the translation: a question
    /// becomes a window, and a step becomes a change to the screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>The walk runs off the window's thread, and comes back to it
    /// for every question and every step.</strong> TakingFoldersOn asks
    /// through four plain functions that answer on the spot. On Windows a
    /// dialog blocks, so they can. Here an answer is awaited, and a function
    /// that has to return an answer cannot wait for one on the thread the
    /// answer will arrive on. So the walk has a thread of its own, which can
    /// wait: each question is put to the window's thread and the walk stands
    /// still until it is answered. The shared code and its tests are as they
    /// were, and the disk is read without the window standing still, which
    /// on Windows it does.
    /// </para>
    /// <para>
    /// The steps arrive one at a time, so a game appears on the list before
    /// the next folder is asked about, which is how this has always behaved.
    /// </para>
    /// </remarks>
    private async Task Add(IReadOnlyList<string> folders)
    {
        // One walk at a time. The Windows window cannot be pressed while a
        // question stands, since its dialogs block. This one can, between
        // two questions, and a second walk begun then would ask about the
        // same folders across the first.
        if (folders.Count == 0 || _takingFoldersOn)
        {
            return;
        }

        _takingFoldersOn = true;

        try
        {
            TakingFoldersOn.TheQuestions asking = new(
                (folder, inside, loose) => OnTheWindow(() => AddingThemSeparately(folder, inside, loose)),
                game => OnTheWindow(() => LeavingItForItsGame(game)),
                (game, left, after) => OnTheWindow(() => WhatElseIsInThere.Ask(this, game, left, after)),
                game => OnTheWindow(() => WhicheverStartsIt(game)));

            await Task.Run(() =>
            {
                foreach (TakingFoldersOn.WhatToDo step in new TakingFoldersOn(_disk, _reader).From(
                    folders, asking, folder => OnTheWindow(() => Task.FromResult(_screen.IsOnAlready(folder)))))
                {
                    OnTheWindow(async () =>
                    {
                        await Do(step);
                        return true;
                    });
                }
            });
        }
        finally
        {
            _takingFoldersOn = false;
        }
    }

    /// <summary>
    /// Something done on the window's thread on behalf of the walk, which
    /// stands still until it is done.
    /// </summary>
    private static T OnTheWindow<T>(Func<Task<T>> what) =>
        Dispatcher.UIThread.InvokeAsync(what).GetAwaiter().GetResult();

    private async Task Do(TakingFoldersOn.WhatToDo step)
    {
        switch (step)
        {
            case TakingFoldersOn.WhatToDo.PutItOn(GameFolder game, IReadOnlyList<LeftBehind> asAddOns):
                _screen.Add(game, asAddOns);
                break;

            case TakingFoldersOn.WhatToDo.AlsoOnTheDisc(IReadOnlyList<string> paths):
                _screen.AlsoOnTheDisc(paths);
                break;

            case TakingFoldersOn.WhatToDo.ItIsOnAlready(GameFolder game, IReadOnlyList<LeftBehind> left):
                await OfferBackWhatIsMissing(game, left);
                break;

            case TakingFoldersOn.WhatToDo.Explain(Refusal refusal):
                await SayingSo.Explain(this, refusal);
                break;
        }
    }

    /// <summary>
    /// A folder that is on the list already: offers back whatever it holds
    /// that the disc does not, or says that there is nothing more in it.
    /// </summary>
    /// <remarks>
    /// What is missing and the words are the screen's, so a test can read
    /// them. What is left here is one window, and the one answer that puts
    /// things back.
    /// </remarks>
    private async Task OfferBackWhatIsMissing(GameFolder game, IReadOnlyList<LeftBehind> left)
    {
        if (_screen.WhatIsMissingFrom(game, left) is not TheSameFolderAgain missing)
        {
            return;
        }

        if (missing.NothingIsMissing)
        {
            await SayingSo.Explain(this, missing.AlreadyWhole);
        }
        else if (await Ask(missing.Question))
        {
            _screen.PutBack(missing);
        }
    }

    /// <summary>
    /// Asks a question the screen worded, and answers true for the likely one.
    /// </summary>
    /// <remarks>
    /// The words and which answer is the likely one are the screen's, so a
    /// test can read them.
    /// </remarks>
    private Task<bool> Ask(AQuestion question) =>
        WorthAsking.Ask(this, question.Headline, question.Because, question.TheLikelyOne, question.TheOtherWay);

    /// <summary>
    /// Which file starts a folder of game files, when more than one could.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recogniser deliberately never picks one. The answer narrows the
    /// candidates to the one chosen, which is all the menu has ever looked
    /// at, so nothing downstream had to change.
    /// </para>
    /// <para>
    /// Not asked for one candidate, because there is nothing to choose
    /// between, and not asked for none. Saying no is allowed and leaves the
    /// disc copying the folder, which the window then says out loud.
    /// </para>
    /// </remarks>
    private async Task<GameFolder> WhicheverStartsIt(GameFolder game) =>
        await PickOne.From(
            this,
            $"Which file starts {game.Name}?",
            "Nothing is guessed here. A disc whose only button runs the wrong file is worse than one that "
                + "copies the folder and opens it, which is what this does if you would rather not say.",
            game.LauncherCandidates,
            file => file.RelativePath) is ListedFile chosen
            ? game with { LauncherCandidates = [chosen] }
            : game;

    private Task<bool> AddingThemSeparately(string folder, IReadOnlyList<string> inside, int loose) =>
        WorthAsking.Ask(
            this,
            $"That folder holds {inside.Count} games",
            $"{Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar))} "
                + "is not a game itself, it is where these are kept. Added separately they each get their own "
                + "entry on the disc and its menu. Added as one, the disc holds a single entry named after the "
                + "folder, with everything inside it."
                + (loose == 0
                    ? string.Empty
                    : loose == 1
                        ? " The one file sitting loose in it goes on the disc either way."
                        : $" The {loose} files sitting loose in it go on the disc either way."),
            $"Add all {inside.Count} separately",
            "Add it as one game");

    /// <summary>
    /// Whether to leave an add-on off and wait for the game it belongs to.
    /// </summary>
    /// <remarks>
    /// Not a refusal. It still goes on if somebody wants it to; it just must
    /// not go on in silence, because on the disc it becomes a game in the menu
    /// and running it does nothing.
    /// </remarks>
    private Task<bool> LeavingItForItsGame(GameFolder game) =>
        WorthAsking.Ask(
            this,
            "That looks like an add-on, not a game",
            $"{game.Installer?.Name} installs onto a game rather than being one. On a disc of its own it appears "
                + "in the menu as a game, and running it does nothing, because the game it belongs to is not "
                + "there. To put it with its game, add the game first and attach this to it.",
            "Leave it for now",
            "Put it on anyway");

    // ------------------------------------------------- the list of what goes on

    private void WhatIsPickedChanged(object? sender, SelectionChangedEventArgs e) =>
        _screen.WhatIsSelected = Games.SelectedItem as AThingOnTheDisc;

    private void TakeOffThisRow(object? sender, RoutedEventArgs e)
    {
        // The row whose cross was pressed, which is the row the button was
        // drawn on: no selection in between, so there is no way to press one
        // row's cross and lose a different row.
        if (sender is Control pressed && pressed.DataContext is AThingOnTheDisc row)
        {
            _screen.TakeOff(row);
        }
    }

    private void TheListKeys(object? sender, KeyEventArgs e)
    {
        // Delete takes the picked row off, the keyboard's half of the cross
        // on the row.
        //
        // Not from inside the rename box, where Delete is an editing key. On
        // Windows that needs saying here, because the box lets the key go at
        // the end of its text and it arrives. Here the box keeps every Delete
        // it is given, even with nothing left to delete, and a key that was
        // kept never reaches this. A check for it was written, a fault was
        // planted by taking the check out, and nothing changed: it was a line
        // that read as though it did something. What holds the behaviour is
        // the test that presses Delete in an empty box and counts the rows.
        if (e.Key == Key.Delete && _screen.CanTakeSomethingOff)
        {
            _screen.TakeOff(_screen.WhatIsSelected);
            e.Handled = true;
        }
    }

    private void TakeOffWhatIsLeftOff(object? sender, RoutedEventArgs e) =>
        _screen.TakeOffWhatIsLeftOff();

    /// <summary>
    /// Opens a disc that was built before, from the file it carries.
    /// </summary>
    /// <remarks>
    /// The file is read as bytes and handed over as text, because every one of
    /// them starts with a byte order mark and the reader is the thing that
    /// knows what to do about that.
    /// </remarks>
    private async void OpenADisc(object? sender, RoutedEventArgs e)
    {
        string? file = await _desktop.ForAFile(
            this,
            "Which disc?",
            [
                new AKindOfFile("A disc DiscWright made (discproject.json)", ["discproject.json"]),
                new AKindOfFile("Any project file", ["*.json"]),
            ]);

        if (file is null)
        {
            return;
        }

        // Read before anybody is asked to give anything up for it. A file
        // this cannot open, the PowerShell DiscWright's among them, is refused
        // here with the window untouched and nobody having said yes to
        // replacing an afternoon's work with it.
        Outcome<OpenedProject> read;

        try
        {
            read = AProjectFile.Read(Encoding.UTF8.GetString(File.ReadAllBytes(file)));
        }
        catch (Exception wrong) when (wrong is IOException or UnauthorizedAccessException)
        {
            // Picked, and gone or shut away by the time it was read. The
            // Windows window lets this one through to the box for faults
            // nobody expected. Said plainly here, since it is expected.
            await SayingSo.Explain(this, Refusals.ThatIsNotAProjectFile(wrong.Message));
            return;
        }

        if (read is Outcome<OpenedProject>.No refused)
        {
            await SayingSo.Explain(this, refused.Refusal);
            return;
        }

        // Asked before anything is replaced, because what is on the window may
        // be somebody's afternoon. Anything at all, the same rule Start a new
        // disc keeps: a name typed, a cover chosen and a manual attached are an
        // afternoon too.
        if (_screen.HasAnythingOnIt && !await WorthAsking.Ask(
            this,
            "Replace what is on the window?",
            "Opening a disc puts its games, its name and its settings here, and takes away what is here now. "
                + "Nothing on disk is touched either way.",
            "Open it",
            "Leave this alone"))
        {
            return;
        }

        Reopened back = ReopenAProject.From(
            ((Outcome<OpenedProject>.Ok)read).Value,
            _reader,
            _disk,
            path => _disk.FolderExists(path) ? _disk.List(path).Bytes : SizeOfOne(path));

        if (!back.AnythingLeft)
        {
            await SayingSo.Explain(this, Refusals.NothingOnThatDiscIsWhereItWas(back.Gone));
            return;
        }

        // Opened even when something is missing, and the screen says what.
        // Refusing a whole disc over one game somebody moved would be a poor
        // answer to the commonest thing that happens to a project a year
        // later.
        _screen.OpenThis(back);
    }

    private static long SizeOfOne(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Opens today's log, or the folder when today has nothing in it yet.
    /// </summary>
    private async void OpenTheLog(object? sender, RoutedEventArgs e)
    {
        if (TheLog is not TheLogOnDisk log)
        {
            return;
        }

        string target = File.Exists(log.Today) ? log.Today : log.Folder;
        bool opened;

        try
        {
            Directory.CreateDirectory(log.Folder);
            opened = _desktop.Open(target);
        }
        catch (Exception wrong) when (wrong is IOException or UnauthorizedAccessException)
        {
            opened = false;
        }

        if (!opened)
        {
            // A log nothing will open is not worth losing the way to it
            // over, so where it is is the fallback.
            await SayingSo.Tell(this, "Nothing would open the log", "It is in:\n\n" + log.Folder);
        }
    }

    /// <summary>
    /// Clears the screen and starts a new disc.
    /// </summary>
    /// <remarks>
    /// Asked first, but only when there is something to lose. Confirming an
    /// empty screen is how somebody learns to click through the question that
    /// matters, and the question that matters here is the one about erasing a
    /// disc.
    /// </remarks>
    private async void StartANewDisc(object? sender, RoutedEventArgs e)
    {
        if (_screen.HasAnythingOnIt
            && await WorthAsking.Ask(
                this,
                "Start a new disc?",
                "What is on the screen now goes: the games, the name, the artwork and the manual. "
                    + "Nothing already built or burned is touched. A folder you chose for the discs stays; "
                    + "one that was only suggested from the game goes with it.",
                "Keep what is here",
                "Start a new disc"))
        {
            return;
        }

        _screen.StartAgain();
    }

    /// <summary>
    /// The recipe without the discs. The screen decides everything; this is
    /// only the press.
    /// </summary>
    private void SaveItForLater(object? sender, RoutedEventArgs e) => _screen.SaveItForLater();
}
