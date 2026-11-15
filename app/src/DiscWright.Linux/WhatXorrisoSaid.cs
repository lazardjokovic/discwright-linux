using System.Globalization;
using System.Text.RegularExpressions;

namespace DiscWright.Linux;

/// <summary>
/// Reading what xorriso prints. Text in, an answer out, and no program near it.
/// </summary>
/// <remarks>
/// Everything DiscWright knows about a run of xorriso is in the lines it
/// printed, so reading them is kept apart from running it. A line xorriso
/// turns out to print differently on somebody's machine becomes one more case
/// in a test here, not an afternoon with a debugger and a disc.
/// </remarks>
public static partial class WhatXorrisoSaid
{
    /// <summary>
    /// How far along a line says the writing is, from nought to one, or null
    /// when the line is not about that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The line is <c>xorriso : UPDATE :  35.19% done</c>, with an estimate of
    /// the time left after it on a long write. Measured with xorriso 1.5.6.
    /// </para>
    /// <para>
    /// A small disc never prints one: it says how many files it added and then
    /// that it finished. So no percentage is not a failure, and the maker says
    /// "done" itself when xorriso ends well.
    /// </para>
    /// <para>
    /// Read in the invariant culture, because xorriso prints a full stop
    /// whatever the machine's language, and a Serbian machine would read 35.19
    /// as three thousand five hundred and nineteen.
    /// </para>
    /// </remarks>
    public static double? HowFar(string line)
    {
        Match found = Percentage().Match(line);

        if (!found.Success
            || !double.TryParse(found.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
        {
            return null;
        }

        return Math.Clamp(percent / 100, 0, 1);
    }

    [GeneratedRegex(@"UPDATE\s*:\s*([0-9]+(?:\.[0-9]+)?)% done")]
    private static partial Regex Percentage();
}
