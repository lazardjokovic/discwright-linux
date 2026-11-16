using DiscWright.Platform;

namespace DiscWright.Tests;

/// <summary>
/// How much room the real disk says there is under a folder, which is asked
/// before a build.
/// </summary>
/// <remarks>
/// Over the real disk, in the machine's own paths, so it runs the same on
/// Windows and on Linux. It cannot ask for an exact number, since the
/// number is the machine's and moves while the test runs. It asks for an
/// answer where there is one and none where there is not.
/// </remarks>
public sealed class RoomUnderAFolder : IDisposable
{
    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-room-" + Guid.NewGuid().ToString("N"));

    public RoomUnderAFolder() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [Fact]
    public void A_folder_that_is_there_has_an_answer()
    {
        long? room = new WindowsFileSystem().RoomUnder(_room);

        Assert.NotNull(room);
        Assert.True(room > 0, "The temporary folder of this machine says it has no room at all.");
    }

    [Fact]
    public void A_folder_still_to_be_made_is_answered_for_where_it_would_be_made()
    {
        // The folder a disc is built into is usually not there yet, and
        // asking about it must not be asking about nothing.
        string notYet = Path.Combine(_room, "not", "made", "yet");

        long? there = new WindowsFileSystem().RoomUnder(_room);
        long? notYetThere = new WindowsFileSystem().RoomUnder(notYet);

        Assert.NotNull(notYetThere);
        Assert.False(Directory.Exists(notYet), "Asking how much room there is made the folder.");

        // The same disk, so the same answer but for whatever was written
        // between the two questions. Within a gigabyte is the same disk.
        Assert.True(
            Math.Abs(there!.Value - notYetThere.Value) < 1024L * 1024 * 1024,
            $"{there} under the folder and {notYetThere} under one to be made inside it.");
    }

    [Fact]
    public void A_path_that_is_no_path_has_no_answer_and_does_not_throw()
    {
        Assert.Null(new WindowsFileSystem().RoomUnder("a\0b"));
    }
}
