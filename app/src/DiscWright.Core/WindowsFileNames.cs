namespace DiscWright.Core;

/// <summary>
/// What Windows will not have in the name of a file or a folder.
/// </summary>
/// <remarks>
/// <para>
/// Written out rather than asked for. <c>Path.GetInvalidFileNameChars</c>
/// answers for the machine it runs on, and a disc is read on Windows whichever
/// machine built it. On Linux that method gives two characters, NUL and the
/// forward slash, so a game called "Command &amp; Conquer: Red Alert" kept its
/// colon as a folder on the disc: a disc that builds, burns, and holds a
/// folder Windows cannot open. Measured on Ubuntu on 2026-11-15.
/// </para>
/// <para>
/// The list is Windows' own, all forty one of them, and a test compares it
/// with what Windows says whenever the suite runs there, so it cannot drift.
/// </para>
/// </remarks>
public static class WindowsFileNames
{
    /// <summary>
    /// The quote, the angle brackets, the bar, the colon, the star, the
    /// question mark, both slashes, and every control character under a space.
    /// </summary>
    public static IReadOnlyList<char> WillNotHave { get; } =
    [
        '"', '<', '>', '|', ':', '*', '?', (char)92, '/',
        .. Enumerable.Range(0, 32).Select(code => (char)code)
    ];
}
