using DiscWright.Core;
using DiscWright.Core.Staging;
using DiscWright.Menu;

namespace DiscWright.Tests;

/// <summary>
/// A place on the disc is written the way Windows writes it, and it has to
/// become a real folder on whichever machine is building the disc.
/// </summary>
/// <remarks>
/// Every other test of a build step names its stage <c>C:\stage</c> and expects
/// <c>C:\stage\AUTORUN\menu.hta</c> back, which is true on Windows and says
/// nothing anywhere else. These build their paths out of the machine's own, so
/// they ask the same question on Linux, where it was measured going wrong on
/// 2026-11-15: joining <c>AUTORUN\menu.hta</c> onto a stage there wrote one
/// file with a backslash in its name, and a disc with no AUTORUN folder at all.
/// </remarks>
public class APlaceOnTheDisc
{
    private static readonly string Stage = Path.Combine(Path.GetTempPath(), "a stage");

    [Fact]
    public void Written_the_way_windows_writes_it_lands_in_its_folder_on_this_machine()
    {
        string backslash = ((char)92).ToString();

        Assert.Equal(
            Path.Combine(Stage, "AUTORUN", "menu.hta"),
            Context(new RecordingWriter()).On("AUTORUN" + backslash + "menu.hta"));
    }

    [Fact]
    public void Written_with_forward_slashes_lands_in_the_same_folder()
    {
        // A file planned on Linux arrives this way, because that is how its
        // folder was listed. Both spellings have to mean one place.
        Assert.Equal(
            Path.Combine(Stage, "Data", "sound", "music.ogg"),
            Context(new RecordingWriter()).On("Data/sound/music.ogg"));
    }

    [Fact]
    public void At_the_top_of_the_disc_it_is_a_file_in_the_stage()
    {
        Assert.Equal(
            Path.Combine(Stage, "autorun.inf"),
            Context(new RecordingWriter()).On("autorun.inf"));
    }

    [Fact]
    public async Task The_menu_really_goes_into_a_folder_called_AUTORUN()
    {
        RecordingWriter wrote = new();

        await new WriteTheMenuStep().Run(Context(wrote), CancellationToken.None);

        // The step itself, not only the method under it: this is the file
        // Windows is pointed at when the disc goes in.
        Assert.Contains(
            Path.Combine(Stage, "AUTORUN", "menu.hta"),
            wrote.Texts.Select(text => text.Path));
    }

    private static BuildContext Context(RecordingWriter writer)
    {
        GameFolder found = new(
            Path.Combine(Path.GetTempPath(), "games", "x"),
            GameKind.GogInstaller,
            "Hollow Knight",
            "Hollow Knight",
            null,
            [],
            [],
            null);
        PlannedDisc disc = new(1, [], 0);

        return new BuildContext(
            new DiscProject("HOLLOW KNIGHT", [found], new DiscOptions()),
            new DiscLayout([disc], 1_000_000, true),
            disc,
            Stage,
            writer);
    }
}
