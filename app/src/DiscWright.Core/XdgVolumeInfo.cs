using System.Text;

namespace DiscWright.Core;

/// <summary>
/// The text of <c>.xdg-volume-info</c>, the Linux half of a disc's identity.
/// </summary>
/// <remarks>
/// <para>
/// <c>autorun.inf</c> is a Windows file and no Linux desktop reads it. GIO,
/// which is GNOME and anything else built on gvfs, reads this file at the top
/// of a mounted volume instead and shows the name and the icon it names. The
/// two sit side by side and the disc introduces itself on either machine.
/// </para>
/// <para>
/// The PowerShell app wrote this file (<c>New-XdgVolumeInfo</c>) and the C#
/// one did not, until somebody on Linux asked where the name had gone. It is
/// the same file, byte for byte: the test holds this to one copied off a disc
/// DiscWright 0.7.2 burned.
/// </para>
/// </remarks>
public static class XdgVolumeInfo
{
    /// <summary>What the file is called, at the top of the disc.</summary>
    public const string FileName = ".xdg-volume-info";

    /// <summary>
    /// The exact bytes of the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UTF-8 with NO byte order mark, and LF. GKeyFile takes a mark as part
    /// of the first group's name, so a file with one parses as nothing at all
    /// and the disc shows its volume id and a blank icon. That is why this
    /// hands back bytes and not text: a writer that is asked for text gets to
    /// choose the mark, and the usual choice is wrong here.
    /// </para>
    /// <para>
    /// A backslash is doubled, because a key file reads one as the start of
    /// an escape. Control characters are dropped, the same as in
    /// <c>autorun.inf</c>: a line break in a name would otherwise be a second
    /// line, and a second line is a second key.
    /// </para>
    /// </remarks>
    /// <param name="name">What the disc is called, in full.</param>
    /// <param name="iconFile">
    /// The picture to wear, as a path from the top of the disc, or nothing
    /// when the disc carries none. A file naming a picture that is not there
    /// is worse than a file naming none.
    /// </param>
    public static byte[] For(string name, string? iconFile)
    {
        StringBuilder text = new();

        text.Append("[Volume Info]\n");
        text.Append("Name=").Append(Escaped(name)).Append('\n');

        if (!string.IsNullOrEmpty(iconFile))
        {
            // Forward slashes, whatever the rest of the program writes: this
            // is the one file on the disc that only Linux reads.
            text.Append("IconFile=").Append(Escaped(iconFile).Replace(@"\\", "/", StringComparison.Ordinal)).Append('\n');
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
    }

    private static string Escaped(string value) =>
        new string([.. value.Where(character => character >= ' ' && character != '\u007F')])
            .Replace(@"\", @"\\", StringComparison.Ordinal);
}
