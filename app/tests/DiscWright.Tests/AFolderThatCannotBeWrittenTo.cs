using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.Platform;
using DiscWright.Staging;

namespace DiscWright.Tests;

/// <summary>
/// Building into a folder that cannot be made: said as that, and not as a
/// fault nobody expected.
/// </summary>
/// <remarks>
/// <para>
/// Found on 2026-11-16 by typing a folder that cannot be made into the Linux
/// window and pressing Build. It said "Something went wrong while building
/// the discs. This one is not accounted for, which means it is worth
/// reporting." A folder typed wrong is not worth reporting.
/// </para>
/// <para>
/// Written in the machine's own paths and over real folders, so it runs the
/// same on Windows and on Linux. The folder that cannot be made is one
/// inside a file, which no system allows and no test has to be an
/// administrator, or not one, to arrange.
/// </para>
/// </remarks>
public sealed class AFolderThatCannotBeWrittenTo : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-no-folder-" + Guid.NewGuid().ToString("N"));

    public AFolderThatCannotBeWrittenTo()
    {
        Directory.CreateDirectory(Download);
        File.Copy(Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"), Path.Combine(Download, Installer));
        File.WriteAllText(AFile, "a file, where a folder was asked for");
    }

    private string Download => Path.Combine(_room, "downloads", "Fixture Game");

    private string AFile => Path.Combine(_room, "not-a-folder");

    /// <summary>Inside a file, which is nowhere a folder can be made.</summary>
    private string Nowhere => Path.Combine(AFile, "discs");

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [Fact]
    public async Task A_folder_that_cannot_be_made_is_said_as_that()
    {
        Outcome<BuiltDiscs> built = await ABuilder().Build(AProject(), ADisc, Nowhere, null, CancellationToken.None);

        Refusal refusal = Assert.IsType<Outcome<BuiltDiscs>.No>(built).Refusal;

        Assert.Equal("That folder cannot be written to", refusal.Headline);

        // Which folder, in the words somebody typed, and in the sentence
        // itself. Looked for anywhere, it was found in what the system said
        // at the end, which names the path too, and a refusal that had
        // stopped naming the folder passed.
        Assert.StartsWith($"The discs were to be built in {Nowhere}, ", refusal.Because, StringComparison.Ordinal);
        Assert.Contains("Choose another folder and build again.", refusal.Because, StringComparison.Ordinal);
        Assert.Contains("Nothing has been laid out.", refusal.Because, StringComparison.Ordinal);

        // And it is true: nothing was.
        Assert.False(Directory.Exists(Nowhere));
        Assert.Equal("a file, where a folder was asked for", File.ReadAllText(AFile));
    }

    [Fact]
    public async Task What_the_system_said_comes_last_and_is_not_left_out()
    {
        Outcome<BuiltDiscs> built = await ABuilder().Build(AProject(), ADisc, Nowhere, null, CancellationToken.None);

        string because = Assert.IsType<Outcome<BuiltDiscs>.No>(built).Refusal.Because;
        string theirs = because[(because.IndexOf("The system said: ", StringComparison.Ordinal) + "The system said: ".Length)..];

        // Whatever this system says about it, which differs from one to the
        // next, and is the only thing that says which kind of nowhere it was.
        Assert.True(theirs.Trim().Length > 0, "The system's own words were left out: " + because);
        Assert.True(
            because.IndexOf("The system said: ", StringComparison.Ordinal)
                > because.IndexOf("Nothing has been laid out.", StringComparison.Ordinal),
            "The system's words came before the sentence somebody can act on.");
    }

    [Theory]
    [InlineData("an account that may not write there")]
    [InlineData("a drive that is not there")]
    [InlineData("a name the system will not have")]
    public async Task Each_ordinary_reason_a_folder_cannot_be_made_is_this_refusal(string why)
    {
        Exception thrown = why switch
        {
            "an account that may not write there" => new UnauthorizedAccessException("Access to the path is denied."),
            "a drive that is not there" => new DirectoryNotFoundException("Could not find a part of the path."),
            "a name the system will not have" => new NotSupportedException("The given path's format is not supported."),
            _ => throw new ArgumentOutOfRangeException(nameof(why)),
        };

        string into = Path.Combine(_room, "out");
        AWriterThatCannotMake writer = new(into, thrown);

        Outcome<BuiltDiscs> built = await ABuilder(writer).Build(AProject(), ADisc, into, null, CancellationToken.None);

        Refusal refusal = Assert.IsType<Outcome<BuiltDiscs>.No>(built).Refusal;

        Assert.Equal("That folder cannot be written to", refusal.Headline);
        Assert.EndsWith("The system said: " + thrown.Message, refusal.Because, StringComparison.Ordinal);

        // Asked once, and nothing else was written anywhere.
        Assert.Equal([into], writer.AskedToMake);
        Assert.Empty(writer.Wrote);
    }

    [Fact]
    public async Task Anything_else_that_goes_wrong_making_it_is_not_dressed_as_this()
    {
        string into = Path.Combine(_room, "out");
        AWriterThatCannotMake writer = new(into, new InvalidOperationException("something nobody expected"));

        // Thrown on, whole, for whoever catches faults nobody expected. A
        // refusal about a folder would send somebody to choose another one
        // for a fault that has nothing to do with folders.
        InvalidOperationException wrong = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ABuilder(writer).Build(AProject(), ADisc, into, null, CancellationToken.None));

        Assert.Equal("something nobody expected", wrong.Message);
    }

    [Fact]
    public async Task A_disc_that_was_never_going_to_be_built_leaves_no_folder_behind_it()
    {
        string into = Path.Combine(_room, "never");

        // Nothing to put on it, which the plan refuses before any folder is
        // thought about.
        DiscProject empty = new("FIXTURE GAME", [], new DiscOptions());

        Outcome<BuiltDiscs> built = await ABuilder().Build(empty, ADisc, into, null, CancellationToken.None);

        Assert.Equal(
            Refusals.ThereIsNothingToPutOnADisc().Headline,
            Assert.IsType<Outcome<BuiltDiscs>.No>(built).Refusal.Headline);
        Assert.False(Directory.Exists(into), "An empty folder was left for a disc that was never going to be built.");
    }

    [Fact]
    public async Task A_folder_that_can_be_made_is_made_and_the_disc_is_built_in_it()
    {
        string into = Path.Combine(_room, "a folder", "that is not there yet");

        Outcome<BuiltDiscs> built = await ABuilder().Build(AProject(), ADisc, into, null, CancellationToken.None);

        BuiltDisc disc = Assert.Single(Assert.IsType<Outcome<BuiltDiscs>.Ok>(built).Value.Discs);

        Assert.True(Directory.Exists(into));
        Assert.True(File.Exists(Path.Combine(disc.Folder, Installer)));
    }

    // ------------------------------------------------------------------ helpers

    private static readonly Medium ADisc = new("TEST", "a test disc", 10_000_000);

    private DiscProject AProject() =>
        new(
            "FIXTURE GAME",
            [
                Assert.IsType<Outcome<GameFolder>.Ok>(
                    GameFolders.InTheUsualOrder(new WindowsFileSystem(), new VersionResourceFacts()).At(Download)).Value
            ],
            new DiscOptions());

    private static DiscBuilder ABuilder(IStageWriter? writer = null) =>
        new(new WindowsFileSystem(), writer ?? new WindowsStageWriter(), new SkiaImages(), new Sha256Hasher());

    /// <summary>
    /// A writer that cannot make one folder, for the reason it is given, and
    /// writes everything else down without writing it.
    /// </summary>
    private sealed class AWriterThatCannotMake(string folder, Exception because) : IStageWriter
    {
        internal List<string> AskedToMake { get; } = [];

        internal List<string> Wrote { get; } = [];

        public void MakeFolder(string path)
        {
            AskedToMake.Add(path);

            if (path == folder)
            {
                throw because;
            }
        }

        public void EmptyTheFolder(string path) => Wrote.Add(path);

        public void Copy(string source, string destination) => Wrote.Add(destination);

        public void CopyPart(string source, long offset, long bytes, string destination) => Wrote.Add(destination);

        public void WriteText(string path, string text) => Wrote.Add(path);

        public void WriteBytes(string path, byte[] bytes) => Wrote.Add(path);

        public void WriteAutorunText(string path, string text) => Wrote.Add(path);
    }
}
