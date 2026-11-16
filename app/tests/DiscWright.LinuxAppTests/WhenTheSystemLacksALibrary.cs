using DiscWright.LinuxApp;
using DiscWright.Tests;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// What DiscWright says when it cannot start because the system has not got
/// a library it leans on.
/// </summary>
/// <remarks>
/// These hold the words. That the words are what comes out, with the right
/// code and no core dump, is measured in a container of a system that really
/// lacks the library, by <c>tools/see-the-linux-downloads-on-another-system.sh</c>:
/// nothing here can take a library away from the machine it runs on.
/// </remarks>
public sealed class WhenTheSystemLacksALibrary
{
    /// <summary>
    /// What the loader said in a bare Debian 12 container on 2026-11-16,
    /// copied as it came, with the folders it looked in.
    /// </summary>
    private const string WhatDebianSaid =
        "Unable to load shared library 'libSkiaSharp' or one of its dependencies. In order to help diagnose loading "
        + "problems, consider using a tool like strace. If you're using glibc, consider setting the LD_DEBUG "
        + "environment variable: \n"
        + "libfontconfig.so.1: cannot open shared object file: No such file or directory\n"
        + "/tmp/t/DiscWright/libSkiaSharp.so: cannot open shared object file: No such file or directory\n"
        + "/root/.net/DiscWright/XhUNC3HvB_sc/liblibSkiaSharp.so: cannot open shared object file: No such file or directory\n"
        + "/tmp/t/DiscWright/liblibSkiaSharp.so: cannot open shared object file: No such file or directory\n";

    [Fact]
    public void What_a_system_with_no_fontconfig_said_becomes_a_sentence_and_a_line_to_type()
    {
        // Wrapped the way it arrives: a type could not be made ready because
        // a library could not be loaded.
        Exception arrived = new TypeInitializationException(
            "SkiaSharp.SKImageInfo", new DllNotFoundException(WhatDebianSaid));

        string said = Assert.IsType<string>(WhatTheSystemLacks.Said(arrived));
        string[] lines = said.Split(Environment.NewLine);

        Assert.Equal(
            "DiscWright cannot start: this system has no libfontconfig.so.1, which it finds the system's typefaces with.",
            lines[0]);
        Assert.Equal("Every desktop has it, so this is most likely a system with no desktop on it.", lines[1]);
        Assert.Equal("  Ubuntu, Debian, Mint:  sudo apt install libfontconfig1", lines[2]);
        Assert.Equal("  Fedora:                sudo dnf install fontconfig", lines[3]);
        Assert.Equal("  Arch, Manjaro:         sudo pacman -S fontconfig", lines[4]);
        Assert.Equal(5, lines.Length);

        // And none of what it replaces.
        Assert.DoesNotContain("strace", said, StringComparison.Ordinal);
        Assert.DoesNotContain("liblibSkiaSharp", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("libX11.so.6", "libx11-6", "libX11", "libx11")]
    [InlineData("libICE.so.6", "libice6", "libICE", "libice")]
    [InlineData("libSM.so.6", "libsm6", "libSM", "libsm")]
    public void Each_library_a_window_is_drawn_through_is_named_with_what_brings_it(
        string file, string onDebian, string onFedora, string onArch)
    {
        string said = WhatTheSystemLacks.About(
            $"Unable to load shared library '{file}' or one of its dependencies.\n{file}: cannot open shared object file");

        Assert.Contains($"this system has no {file}, which it draws its window through.", said, StringComparison.Ordinal);
        Assert.Contains("sudo apt install " + onDebian + Environment.NewLine, said, StringComparison.Ordinal);
        Assert.Contains("sudo dnf install " + onFedora + Environment.NewLine, said, StringComparison.Ordinal);
        Assert.EndsWith("sudo pacman -S " + onArch, said, StringComparison.Ordinal);
    }

    [Fact]
    public void A_library_it_does_not_know_is_said_as_that_with_every_word_the_system_had()
    {
        string theirs = "Unable to load shared library 'libsomething' or one of its dependencies.\nlibsomething.so.9: cannot open";

        string said = WhatTheSystemLacks.About(theirs);

        Assert.StartsWith("DiscWright cannot start: a library it needs could not be loaded. The system said:", said, StringComparison.Ordinal);

        // Whole, because those words are all anybody has to go on.
        Assert.EndsWith(theirs, said, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", said, StringComparison.Ordinal);
    }

    [Fact]
    public void Anything_else_that_goes_wrong_on_the_way_in_is_not_dressed_as_a_missing_library()
    {
        // Nothing, and then Main does not catch it: a fault nobody expected
        // arrives whole, stack and all.
        Assert.Null(WhatTheSystemLacks.Said(new InvalidOperationException("something else entirely")));
        Assert.Null(WhatTheSystemLacks.Said(
            new TypeInitializationException("Some.Type", new InvalidOperationException("no library in this"))));
    }

    // ------------------------------------------------------ as the program starts

    [Fact]
    public void A_start_that_fails_for_want_of_a_library_says_so_and_answers_one()
    {
        StringWriter terminal = new();

        int code = WhatTheSystemLacks.SaidInPlaceOfThrown(
            () => throw new TypeInitializationException("SkiaSharp.SKImageInfo", new DllNotFoundException(WhatDebianSaid)),
            terminal);

        Assert.Equal(1, code);
        Assert.StartsWith(
            "DiscWright cannot start: this system has no libfontconfig.so.1", terminal.ToString(), StringComparison.Ordinal);

        // The sentence and its line break, and nothing after it.
        Assert.EndsWith("sudo pacman -S fontconfig" + Environment.NewLine, terminal.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_start_that_fails_for_any_other_reason_fails_as_it_always_did()
    {
        StringWriter terminal = new();

        // Thrown on, whole. Caught here it would be a fault nobody ever saw:
        // a one and an empty line.
        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => WhatTheSystemLacks.SaidInPlaceOfThrown(
                () => throw new InvalidOperationException("something else entirely"), terminal));

        Assert.Equal("something else entirely", thrown.Message);
        Assert.Equal(string.Empty, terminal.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void A_program_that_started_and_ended_hands_back_what_it_ended_with(int ended)
    {
        StringWriter terminal = new();

        Assert.Equal(ended, WhatTheSystemLacks.SaidInPlaceOfThrown(() => ended, terminal));
        Assert.Equal(string.Empty, terminal.ToString());
    }

    [Fact]
    public void The_script_that_installs_it_looks_for_the_same_libraries_and_names_the_same_packages()
    {
        // Two lists of the same four, one in the program and one in a shell
        // script, which nothing but this would hold together.
        string script = File.ReadAllText(Repository.File("packaging", "linux", "install.sh"));

        Assert.Equal(4, WhatTheSystemLacks.LeanedOn.Count);

        foreach (WhatTheSystemLacks.ALibrary each in WhatTheSystemLacks.LeanedOn)
        {
            Assert.Contains(
                $"\n{each.File} {each.OnDebian} {each.OnFedora} {each.OnArch}\n",
                script.Replace("\r\n", "\n", StringComparison.Ordinal),
                StringComparison.Ordinal);
        }
    }
}
