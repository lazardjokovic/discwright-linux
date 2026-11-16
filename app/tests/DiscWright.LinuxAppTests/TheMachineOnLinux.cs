using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.LinuxApp;
using DiscWright.Platform;
using DiscWright.Tests;
using DiscWright.ViewModels;

namespace DiscWright.LinuxAppTests;

/// <summary>
/// What the Linux app hands the screen, and that the window shows the screen
/// it was handed.
/// </summary>
/// <remarks>
/// The composition root is the one place nothing else tests: every part has
/// tests of its own, and the wrong part in the wrong place still compiles
/// wherever two of them answer the same port. So this asks the machine which
/// answer it holds for each port that has a Linux one.
/// </remarks>
public class TheMachineOnLinux
{
    [Fact]
    public void Holds_the_Linux_answer_wherever_there_is_one()
    {
        TheMachine machine = TheLinuxMachine.Composed(new NothingIsLogged());

        Assert.IsType<XorrisoImageMaker>(machine.Images);
        Assert.IsType<SkiaImages>(machine.Pictures);

        // And the shared ones where the answer is plain .NET.
        Assert.IsType<WindowsFileSystem>(machine.Disk);
        Assert.IsType<Sha256Hasher>(machine.Hasher);
        Assert.IsType<ANoteOnDisk>(machine.SetsPlace);
        Assert.IsType<WhatGitHubHasReleased>(machine.Releases);
    }

    [Fact]
    public void Has_no_drives_and_nothing_that_burns_until_there_is_a_recorder()
    {
        TheMachine machine = TheLinuxMachine.Composed(new NothingIsLogged());

        Assert.Empty(machine.Drives.All());
        Assert.IsType<NothingBurnsYet>(machine.Burner);
        Assert.IsType<NothingBurnsYet>(machine.Eraser);
        Assert.IsType<NothingBurnsYet>(machine.Tray);
    }

    [Fact]
    public void Cannot_update_itself_and_is_not_the_Stores()
    {
        TheMachine machine = TheLinuxMachine.Composed(new NothingIsLogged());

        // The updater downloads a Windows installer, and the Store is
        // Microsoft's. A newer release offers its page.
        Assert.Null(machine.Updater);
        Assert.Null(machine.StorePage);
    }

    [Fact]
    public void Says_which_build_it_is_as_the_build_stamps_it()
    {
        string stamped = Regex.Match(
            File.ReadAllText(Repository.File("Directory.Build.props")),
            "<Version>([0-9]+[.][0-9]+[.][0-9]+)</Version>").Groups[1].Value;

        Assert.NotEqual(string.Empty, stamped);

        // The app's own version and not the test's, and with nothing after
        // it: the commit the build adds after a plus is not for a title bar.
        Assert.Equal(stamped, TheLinuxMachine.WhatThisBuildIs());
        Assert.Equal(stamped, TheLinuxMachine.Composed(new NothingIsLogged()).ThisBuild);
    }

    [Theory]
    [InlineData("2.0.4+5f3a9c1d0e", "2.0.4")]
    [InlineData("2.0.4", "2.0.4")]
    [InlineData("2.1.0+", "2.1.0")]
    public void Leaves_the_commit_off_the_version(string stamped, string shown)
    {
        // The build adds the commit after a plus, which is for finding the
        // source and not for a title bar.
        Assert.Equal(shown, TheLinuxMachine.WithoutTheCommit(stamped));
    }

    [Fact]
    public void Writes_down_which_build_started()
    {
        Heard log = new();

        TheLinuxMachine.Composed(log);

        Assert.Equal($"DiscWright {TheLinuxMachine.WhatThisBuildIs()} for Linux started.", Assert.Single(log.Lines));
    }

    [Fact]
    public void Suggests_the_desktop_and_names_no_synced_folders()
    {
        WhereDiscsWouldGo places = TheLinuxMachine.WhereADiscWouldGo();

        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), places.Home);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), places.Desktop);
        Assert.Empty(places.Synced);
    }

    [Fact]
    public void Measures_a_file_and_a_folder_and_remembers_what_it_measured()
    {
        string room = Path.Combine(Path.GetTempPath(), "dw-how-big-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(room, "extras"));

        try
        {
            string file = Path.Combine(room, "manual.pdf");
            File.WriteAllBytes(file, new byte[300]);
            File.WriteAllBytes(Path.Combine(room, "extras", "a.png"), new byte[1000]);
            File.WriteAllBytes(Path.Combine(room, "extras", "b.png"), new byte[24]);

            Func<string, long> howBig = TheLinuxMachine.HowBigIs(new WindowsFileSystem());

            Assert.Equal(300, howBig(file));
            Assert.Equal(1024, howBig(Path.Combine(room, "extras")));

            // Asked again after the file has grown, it gives the first
            // answer: the screen asks on every keystroke, and a folder of
            // extras is not walked again each time.
            File.WriteAllBytes(file, new byte[5000]);
            Assert.Equal(300, howBig(file));

            // And a thing that is not there is nothing, not a fault.
            Assert.Equal(0, howBig(Path.Combine(room, "gone.bin")));
        }
        finally
        {
            Directory.Delete(room, recursive: true);
        }
    }

    [Fact]
    public void Keeps_two_folders_apart_that_differ_only_in_their_capitals()
    {
        if (!OperatingSystem.IsLinux())
        {
            // Windows cannot hold the two folders this needs.
            return;
        }

        string room = Path.Combine(Path.GetTempPath(), "dw-capitals-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(room, "Extras"));
        Directory.CreateDirectory(Path.Combine(room, "extras"));

        try
        {
            File.WriteAllBytes(Path.Combine(room, "Extras", "a.png"), new byte[10]);
            File.WriteAllBytes(Path.Combine(room, "extras", "a.png"), new byte[700]);

            Func<string, long> howBig = TheLinuxMachine.HowBigIs(new WindowsFileSystem());

            // The Windows app remembers sizes ignoring capitals, which there
            // is right. Here it would give the second folder the first's size.
            Assert.Equal(10, howBig(Path.Combine(room, "Extras")));
            Assert.Equal(700, howBig(Path.Combine(room, "extras")));
        }
        finally
        {
            Directory.Delete(room, recursive: true);
        }
    }

    [Fact]
    public void Reads_the_name_of_a_game_out_of_its_installer()
    {
        string room = Path.Combine(Path.GetTempPath(), "dw-reader-" + Guid.NewGuid().ToString("N"));
        string download = Path.Combine(room, "Fixture Game");
        Directory.CreateDirectory(download);

        try
        {
            File.Copy(
                Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"),
                Path.Combine(download, "setup_fixture_game_1.2.3_(64bit)_(12345).exe"));

            Outcome<GameFolder> found = new GameFolders(new WindowsFileSystem(), TheLinuxMachine.TheReaderOfFolders())
                .At(download);

            // By the file's name alone it would be "Fixture Game". The reader
            // the Linux app is given asks the installer.
            Assert.Equal("Fixture Game: Directors Cut", Assert.IsType<Outcome<GameFolder>.Ok>(found).Value.Name);
        }
        finally
        {
            Directory.Delete(room, recursive: true);
        }
    }

    [AvaloniaFact]
    public void The_window_shows_the_screen_it_was_handed()
    {
        MakeADisc screen = TheWindow.AScreen();
        MainWindow window = TheWindow.Showing(screen);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(screen, window.DataContext);

        // The name, and beside it which build this is, in the screen's words.
        TextBlock build = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(each => AutomationProperties.GetAutomationId(each) == "ThisBuild");

        Assert.Equal(screen.ThisBuild, build.Text);
        Assert.Equal(TheLinuxMachine.WhatThisBuildIs(), build.Text);
        Assert.True(build.CanBeSeen());

        TextBlock name = window.GetVisualDescendants().OfType<TextBlock>().Single(each => each.Text == "DiscWright");
        Assert.Contains("Title", name.Classes);

        // The version sits to the right of the name, on its line.
        Assert.True(build.TranslatePoint(default, window)!.Value.X > name.TranslatePoint(default, window)!.Value.X + name.Bounds.Width);
    }

    private sealed class Heard : IKeepALog
    {
        public List<string> Lines { get; } = [];

        public void Write(string line) => Lines.Add(line);
    }
}
