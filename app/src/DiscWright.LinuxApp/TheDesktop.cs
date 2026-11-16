using System.ComponentModel;
using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DiscWright.LinuxApp;

/// <summary>A kind of file somebody may be asked for: what it is called, and the names it goes by.</summary>
public sealed record AKindOfFile(string Name, IReadOnlyList<string> Patterns);

/// <summary>
/// What the window asks of the desktop it is on: a file from somebody, and a
/// thing opened in whatever opens it.
/// </summary>
/// <remarks>
/// <para>
/// A seam, and here for the reason the Windows app has none: a test cannot
/// answer a file dialog, and on Windows the window was never under test at
/// all. Here it is, so the two things the window cannot do by itself are
/// behind this, the real one asks the desktop, and a test answers in its
/// place and remembers what was asked.
/// </para>
/// <para>
/// It lives with the window and not in Core. Core has no idea there is a
/// desktop.
/// </para>
/// </remarks>
public interface IAskTheDesktop
{
    /// <summary>One file somebody picked, or nothing if they would rather not.</summary>
    Task<string?> ForAFile(Window owner, string title, IReadOnlyList<AKindOfFile> kinds);

    /// <summary>
    /// Opens a file, a folder or an address in whatever the desktop opens it
    /// with, and says whether anything took it.
    /// </summary>
    bool Open(string what);
}

/// <summary>
/// The real desktop: its own file dialog, through the portal where there is
/// one, and its own idea of what opens what.
/// </summary>
/// <remarks>
/// Measured on KDE on 2026-11-16: the dialog is Plasma's own, with the title
/// it was given, and comes back with the path picked.
/// </remarks>
public sealed class TheDesktop(string opener = "xdg-open") : IAskTheDesktop
{
    /// <summary>
    /// How long the opener gets to say it could not. It hands the thing on
    /// and is gone in a blink when it works, so this is only ever spent on
    /// one that is still busy, which is one that found something to do.
    /// </summary>
    private static readonly TimeSpan LongEnoughToFail = TimeSpan.FromSeconds(2);

    public async Task<string?> ForAFile(Window owner, string title, IReadOnlyList<AKindOfFile> kinds)
    {
        IReadOnlyList<IStorageFile> picked = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [.. kinds.Select(kind => new FilePickerFileType(kind.Name) { Patterns = kind.Patterns })],
            });

        // A file that is somewhere this machine can read by a path. One that
        // is not, a phone over a portal, say, is no file as far as a disc
        // is concerned.
        return picked.Count == 0 ? null : picked[0].TryGetLocalPath();
    }

    /// <remarks>
    /// <para>
    /// Handed to the desktop's own opener, never to a program by name: on a
    /// Linux desktop that is xdg-open, which knows what this desktop uses.
    /// </para>
    /// <para>
    /// <strong>Starting it is not the same as it working.</strong> It starts
    /// whenever it is installed, and then may find nothing to hand the thing
    /// to, say so to nobody, and leave with a number. Seen on the test
    /// machine on 2026-11-16: "kfmclient: not found", and a window that had
    /// been told its log was open. So it is given a moment, and one that
    /// leaves in that moment with anything but nought has not opened it.
    /// </para>
    /// </remarks>
    public bool Open(string what)
    {
        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(opener)
            {
                ArgumentList = { what },
                UseShellExecute = false,
            });

            if (started is null)
            {
                return false;
            }

            return !started.WaitForExit(LongEnoughToFail) || started.ExitCode == 0;
        }
        catch (Exception wrong) when (wrong is Win32Exception or InvalidOperationException)
        {
            // Not installed, or not something that can be run.
            return false;
        }
    }
}
