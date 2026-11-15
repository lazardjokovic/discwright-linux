namespace DiscWright.Linux;

/// <summary>
/// Starting another program and hearing what it says, line by line.
/// </summary>
/// <remarks>
/// <para>
/// On Linux the image is made, and later the disc is written, by xorriso, a
/// program of its own rather than a library. Everything DiscWright knows about
/// how that went is what the program printed and the number it ended with.
/// </para>
/// <para>
/// It is an interface because a test needs the seam, which is the rule for
/// adding one. The fake plays back what xorriso really printed, including a
/// run that fails half way, so every answer DiscWright gives about a failure
/// is tested without xorriso, without a disc and in milliseconds. It lives
/// here and not in Core because Core has no idea that programs exist.
/// </para>
/// </remarks>
public interface IRunPrograms
{
    /// <summary>Whether a program of that name can be started on this machine.</summary>
    bool Has(string program);

    /// <summary>
    /// Runs it to the end and gives back the number it ended with, handing
    /// over each line it prints as it prints it, from either of its outputs.
    /// </summary>
    /// <remarks>
    /// Cancelling stops the program as well as the waiting. A program left
    /// running after its window has moved on is still writing a file somebody
    /// believes was abandoned.
    /// </remarks>
    Task<int> Run(string program, IReadOnlyList<string> arguments, Action<string> said, CancellationToken ct);
}
