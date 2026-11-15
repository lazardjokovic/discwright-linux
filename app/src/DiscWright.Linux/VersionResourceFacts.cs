using DiscWright.Core;

namespace DiscWright.Linux;

/// <summary>
/// Reads the product name an executable reports about itself, on a machine
/// where nothing else will.
/// </summary>
/// <remarks>
/// The Linux side of <c>FileVersionFacts</c>, and it answers the same way: the
/// name as stored, or null when there is none worth having, so the recogniser
/// falls back on the file's own name exactly as it does on Windows. It is the
/// difference between a disc called "The Witcher: Enhanced Edition" and one
/// called "setup the witcher enhanced edition directors cut".
/// </remarks>
public sealed class VersionResourceFacts : IFileFacts
{
    public string? ProductNameOf(string path)
    {
        string? reported = AVersionResource.StringsIn(path)?.GetValueOrDefault("ProductName");

        return string.IsNullOrWhiteSpace(reported) ? null : reported;
    }
}
