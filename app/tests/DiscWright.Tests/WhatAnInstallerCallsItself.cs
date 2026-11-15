using System.Diagnostics;

using DiscWright.Linux;

namespace DiscWright.Tests;

/// <summary>
/// The name inside a Windows installer, read without Windows.
/// </summary>
/// <remarks>
/// <para>
/// The two executables in <c>fixtures/pe</c> were compiled by Windows' own C#
/// compiler, so their version resources come from a real Windows toolchain
/// rather than from anything this reader assumes. What Windows reads from them
/// was written down on Windows, and is what these expect.
/// </para>
/// <para>
/// They run on both systems, and that is the point. On Linux they are the only
/// proof the name can be read at all, since .NET reads nothing there. On
/// Windows one of them asks Windows itself and compares, which is what stops
/// the reader and the real thing drifting apart.
/// </para>
/// </remarks>
public class WhatAnInstallerCallsItself
{
    private static string Fixture(string name) =>
        Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", name);

    [Fact]
    public void Reads_the_product_name_windows_reads_padding_and_all()
    {
        // The padding is Inno Setup's and Windows hands it over too. Trimming
        // is for whoever asked, the same as on Windows.
        Assert.Equal(
            "Fixture Game: Directors Cut   ",
            AVersionResource.StringsIn(Fixture("with-product.exe"))?["ProductName"]);
    }

    [Fact]
    public void Reads_the_other_strings_too()
    {
        IReadOnlyDictionary<string, string>? strings = AVersionResource.StringsIn(Fixture("with-product.exe"));

        Assert.NotNull(strings);
        Assert.Equal("1.2.3 (GOG)", strings["ProductVersion"]);
        Assert.Equal("1.2.3.4", strings["FileVersion"]);
    }

    [Fact]
    public void Gives_the_recogniser_the_name_as_windows_would()
    {
        Assert.Equal(
            "Fixture Game: Directors Cut   ",
            new VersionResourceFacts().ProductNameOf(Fixture("with-product.exe")));
    }

    [Fact]
    public void Says_nothing_for_an_executable_that_names_no_product()
    {
        // Null, so the recogniser falls back on the file's own name, which is
        // what it does on Windows when the name comes back blank.
        Assert.Null(new VersionResourceFacts().ProductNameOf(Fixture("no-product.exe")));
    }

    [Fact]
    public void Says_nothing_for_a_file_that_is_not_there()
    {
        Assert.Null(new VersionResourceFacts().ProductNameOf(Fixture("nobody put this here.exe")));
    }

    [Fact]
    public void Says_nothing_for_something_that_is_not_a_program()
    {
        using ATemporaryFile file = new("not a program"u8.ToArray());

        Assert.Null(AVersionResource.StringsIn(file.Path));
    }

    [Fact]
    public void Says_nothing_for_an_empty_file()
    {
        using ATemporaryFile file = new([]);

        Assert.Null(AVersionResource.StringsIn(file.Path));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(700)]
    public void Says_nothing_for_a_program_that_was_cut_short_and_never_throws(int keep)
    {
        // A download that stopped, or a disc going bad. Cut before the name is
        // reached, the answer is "no name", never an exception out of a
        // recogniser. Cut after it the name is still there to read, and is.
        byte[] whole = File.ReadAllBytes(Fixture("with-product.exe"));
        using ATemporaryFile file = new(whole[..keep]);

        Assert.Null(new VersionResourceFacts().ProductNameOf(file.Path));
    }

    [Theory]
    [InlineData("with-product.exe")]
    [InlineData("no-product.exe")]
    public void Agrees_with_windows_itself_when_windows_is_there_to_ask(string fixture)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Nothing to ask: on Linux .NET reads an empty string from every
            // Windows executable, measured on 2026-11-15, which is why this
            // reader exists. The expectations above were recorded on Windows.
            return;
        }

        FileVersionInfo windows = FileVersionInfo.GetVersionInfo(Fixture(fixture));
        IReadOnlyDictionary<string, string>? ours = AVersionResource.StringsIn(Fixture(fixture));

        Assert.NotNull(ours);
        Assert.Equal(windows.ProductName ?? string.Empty, ours.GetValueOrDefault("ProductName", string.Empty));
        Assert.Equal(windows.FileVersion ?? string.Empty, ours.GetValueOrDefault("FileVersion", string.Empty));
        Assert.Equal(windows.ProductVersion ?? string.Empty, ours.GetValueOrDefault("ProductVersion", string.Empty));
    }

    /// <summary>A file that exists for one test and is gone after it.</summary>
    private sealed class ATemporaryFile : IDisposable
    {
        public ATemporaryFile(byte[] holding)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllBytes(Path, holding);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
