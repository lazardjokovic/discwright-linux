namespace DiscWright.LinuxApp;

/// <summary>
/// What to say when DiscWright cannot start because the system has not got a
/// library it leans on.
/// </summary>
/// <remarks>
/// <para>
/// The one file carries .NET, Avalonia and Skia inside it, and still leans on
/// a few libraries of the system's: the ones a window is drawn through and
/// the one that finds the system's typefaces. Every desktop has them. A
/// system with no desktop on it, a server or a container, may not.
/// </para>
/// <para>
/// Measured on 2026-11-16 in bare containers of Debian 12, Ubuntu 22.04 and
/// Ubuntu 20.04: without <c>libfontconfig.so.1</c> DiscWright died with an
/// exception forty lines long, the library's name on the third of them, and
/// a core dump. Nothing can be shown in a window when the thing that is
/// missing is what draws windows, so this is a sentence on the terminal, and
/// the line to type.
/// </para>
/// <para>
/// .NET wants <c>libicu</c> as well, and says so itself, in its own sentence,
/// before any of this runs. So that one is not here.
/// </para>
/// </remarks>
public static class WhatTheSystemLacks
{
    /// <summary>
    /// A library of the system's, what DiscWright uses it for, and what the
    /// package that brings it is called on each family of distribution.
    /// </summary>
    public sealed record ALibrary(string File, string UsedFor, string OnDebian, string OnFedora, string OnArch);

    /// <summary>
    /// Every library of the system's DiscWright leans on, bar the C library
    /// and <c>libicu</c>. <c>install.sh</c> looks for the same four, and a
    /// test holds the two lists together.
    /// </summary>
    public static IReadOnlyList<ALibrary> LeanedOn { get; } =
    [
        new("libfontconfig.so.1", "finds the system's typefaces with", "libfontconfig1", "fontconfig", "fontconfig"),
        new("libX11.so.6", "draws its window through", "libx11-6", "libX11", "libx11"),
        new("libICE.so.6", "draws its window through", "libice6", "libICE", "libice"),
        new("libSM.so.6", "draws its window through", "libsm6", "libSM", "libsm"),
    ];

    /// <summary>
    /// Starts the program, and when it cannot start for want of a library,
    /// says so in a sentence and answers one, in place of the exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing can be shown in a window, the thing that is missing being
    /// what draws one. So it is said where somebody starting DiscWright from
    /// a terminal will read it, in place of forty lines and a core dump.
    /// </para>
    /// <para>
    /// Only that. Anything else that goes wrong on the way in is not caught
    /// here and arrives whole, and a program that started and ended hands
    /// back whatever it ended with.
    /// </para>
    /// </remarks>
    /// <param name="start">The program, which answers with the code it ended on.</param>
    /// <param name="says">Where the sentence goes: the terminal's line for what went wrong.</param>
    public static int SaidInPlaceOfThrown(Func<int> start, TextWriter says)
    {
        try
        {
            return start();
        }
        catch (Exception wrong) when (Said(wrong) is not null)
        {
            says.WriteLine(Said(wrong));

            return 1;
        }
    }

    /// <summary>
    /// The sentence for this failure, or nothing when it is not a library
    /// that could not be loaded.
    /// </summary>
    /// <remarks>
    /// Nothing, for anything else, and then the failure is left exactly as it
    /// was: a fault nobody expected should arrive whole, and not be dressed
    /// as a missing library because it happened on the way in.
    /// </remarks>
    public static string? Said(Exception wrong)
    {
        // The loader's refusal comes wrapped: a type could not be made ready
        // because a library could not be loaded.
        for (Exception? each = wrong; each is not null; each = each.InnerException)
        {
            if (each is DllNotFoundException notLoaded)
            {
                return About(notLoaded.Message);
            }
        }

        return null;
    }

    /// <summary>The sentence, from what the loader said.</summary>
    public static string About(string whatTheLoaderSaid)
    {
        // The first of ours the loader names. It lists everything it tried,
        // and the system's library that was not there is among the lines.
        ALibrary? lacking = LeanedOn.FirstOrDefault(
            each => whatTheLoaderSaid.Contains(each.File, StringComparison.Ordinal));

        if (lacking is null)
        {
            // Not one this knows. Said as what it is, with every word the
            // system had, because those words are all anybody has to go on.
            return "DiscWright cannot start: a library it needs could not be loaded. The system said:"
                + Environment.NewLine + Environment.NewLine + whatTheLoaderSaid;
        }

        return string.Join(
            Environment.NewLine,
            $"DiscWright cannot start: this system has no {lacking.File}, which it {lacking.UsedFor}.",
            "Every desktop has it, so this is most likely a system with no desktop on it.",
            $"  Ubuntu, Debian, Mint:  sudo apt install {lacking.OnDebian}",
            $"  Fedora:                sudo dnf install {lacking.OnFedora}",
            $"  Arch, Manjaro:         sudo pacman -S {lacking.OnArch}");
    }
}
