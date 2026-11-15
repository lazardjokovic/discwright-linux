using DiscWright.Core;

namespace DiscWright.Tests;

/// <summary>
/// A disc is read on Windows whichever machine built it, so what may not be in
/// a name on it is Windows' answer and never the building machine's.
/// </summary>
/// <remarks>
/// Asked of the machine, the answer on Linux is two characters, NUL and the
/// forward slash. Measured on Ubuntu on 2026-11-15: "Command &amp; Conquer: Red
/// Alert" kept its colon as a folder on the disc, which Windows cannot have,
/// and not one of the tests that existed then noticed, because every one of
/// them ran where the machine's answer and Windows' answer are the same.
/// </remarks>
public class WhatWindowsWillNotHaveInAName
{
    [Fact]
    public void Is_the_list_windows_gives_wherever_this_runs()
    {
        // Forty one: the nine below and every control character under a space.
        Assert.Equal(41, WindowsFileNames.WillNotHave.Count);
        Assert.Equal(41, WindowsFileNames.WillNotHave.Distinct().Count());

        foreach (char each in "\"<>|:*?/" + (char)92)
        {
            Assert.Contains(each, WindowsFileNames.WillNotHave);
        }

        for (int code = 0; code < 32; code++)
        {
            Assert.Contains((char)code, WindowsFileNames.WillNotHave);
        }
    }

    [Fact]
    public void Agrees_with_windows_itself_when_windows_is_there_to_ask()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Nothing to ask here. The test above holds the list on its own,
            // and this one is what stops the list drifting from the real thing.
            return;
        }

        Assert.Equal(
            Path.GetInvalidFileNameChars().Order(),
            WindowsFileNames.WillNotHave.Order());
    }

    [Theory]
    [InlineData(':')]
    [InlineData('*')]
    [InlineData('?')]
    [InlineData('"')]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData('|')]
    [InlineData('/')]
    [InlineData((char)92)]
    public void A_folder_on_the_disc_never_keeps_one_of_them(char one)
    {
        Assert.Equal("a b", OnTheDisc.Tidied("a" + one + "b"));
    }

    [Fact]
    public void A_game_with_a_colon_in_its_name_keeps_its_shape_without_it()
    {
        Assert.Equal("Command & Conquer  Red Alert", OnTheDisc.Tidied("Command & Conquer: Red Alert"));
    }
}
