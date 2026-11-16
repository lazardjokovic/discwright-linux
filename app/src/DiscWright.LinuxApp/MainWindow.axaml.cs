using System.Text;

using Avalonia.Controls;
using Avalonia.Interactivity;

using DiscWright.Core;
using DiscWright.Platform;
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
    }

    /// <summary>The log the app keeps, for the link that opens it.</summary>
    public TheLogOnDisk? TheLog { get; init; }

    private void ChooseTheNextLook(object? sender, RoutedEventArgs e) => _look.Next();

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
