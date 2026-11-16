using DiscWright.LinuxApp;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// The real desktop's way of opening a thing, held to its answer.
/// </summary>
/// <remarks>
/// <para>
/// Everything else here answers for the desktop. This runs the real
/// <see cref="TheDesktop"/>, with a small script standing where xdg-open
/// stands, because what went wrong on the test machine was between those
/// two: the opener started, found nothing to hand the log to, left with a
/// number, and the window was told its log was open.
/// </para>
/// <para>
/// The scripts are shell, so these run on Linux and say nothing on Windows.
/// </para>
/// </remarks>
public sealed class OpeningThingsOnTheDesktop : IDisposable
{
    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-opener-" + Guid.NewGuid().ToString("N"));

    public OpeningThingsOnTheDesktop() => Directory.CreateDirectory(_room);

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [Fact]
    public void A_thing_the_opener_took_is_opened_and_it_was_handed_exactly_that_thing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string heard = Path.Combine(_room, "heard.txt");
        string opener = AnOpenerThat($"printf '%s' \"$1\" > '{heard}'\nexit 0");

        // A name with a space in it, which is every log folder under a home
        // with a space in it. Handed over whole or it opens two things.
        string log = Path.Combine(_room, "my logs", "2026-11-16.txt");

        Assert.True(new TheDesktop(opener).Open(log));
        Assert.Equal(log, File.ReadAllText(heard));
    }

    [Fact]
    public void A_thing_the_opener_could_not_place_is_not_opened()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // What xdg-open did on the test machine: said why to nobody and
        // left with a number.
        string opener = AnOpenerThat("echo 'kfmclient: not found' >&2\nexit 3");

        Assert.False(new TheDesktop(opener).Open(Path.Combine(_room, "2026-11-16.txt")));
    }

    [Fact]
    public void An_opener_still_busy_after_a_moment_has_found_something_to_do()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Some openers stay until what they opened is shut. That is not a
        // failure, and the window is not kept waiting on it.
        string opener = AnOpenerThat("sleep 6\nexit 3");

        DateTime asked = DateTime.UtcNow;

        Assert.True(new TheDesktop(opener).Open(Path.Combine(_room, "2026-11-16.txt")));
        Assert.True(DateTime.UtcNow - asked < TimeSpan.FromSeconds(4), "The window was kept waiting on the opener.");
    }

    [Fact]
    public void No_opener_at_all_is_nothing_opened_and_not_a_fault()
    {
        TheDesktop desktop = new(Path.Combine(_room, "there-is-no-such-program"));

        Assert.False(desktop.Open(Path.Combine(_room, "2026-11-16.txt")));
    }

    /// <summary>A program standing where xdg-open stands, that does what its script says.</summary>
    private string AnOpenerThat(string does)
    {
        string script = Path.Combine(_room, "opener-" + Guid.NewGuid().ToString("N") + ".sh");

        File.WriteAllText(script, "#!/bin/sh\n" + does + "\n");
        // Every test that calls this has already left on Windows. Said
        // again here because the compiler cannot see that.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return script;
    }
}
