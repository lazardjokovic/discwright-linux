using DiscWright.Core;

namespace DiscWright.Tests;

/// <summary>
/// A disc written on Linux is read by Windows through Joliet, and a name
/// Joliet cannot hold is cut or changed without a word. These are the names
/// that have to be refused before anything is written.
/// </summary>
public class NamesThatWouldNotReachWindows
{
    [Fact]
    public void An_ordinary_disc_has_nothing_wrong_with_it()
    {
        Assert.Empty(JolietNames.ProblemsIn(
        [
            "autorun.inf",
            "AUTORUN/menu.hta",
            "setup_hollow_knight_1.5.78.11833_(64bit)_(51898).exe",
            "Extras/Hollow Knight - Manual (EN).pdf",
            "01 - The Witcher - Enhanced Edition/setup_the_witcher-1.bin",
            "Data/Čudesa/ゲーム.txt"
        ]));
    }

    [Theory]
    [InlineData(':')]
    [InlineData('*')]
    [InlineData('?')]
    [InlineData('"')]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData('|')]
    public void A_character_windows_does_not_allow_is_named(char one)
    {
        NameProblem problem = Assert.Single(JolietNames.ProblemsIn(["Extras/what" + one + "ever.txt"]));

        Assert.Equal("Extras/what" + one + "ever.txt", problem.RelativePath);
        Assert.Equal($"has {one} in its name, which Windows does not allow.", problem.Why);
    }

    [Fact]
    public void A_control_character_is_said_in_words_because_it_cannot_be_shown()
    {
        NameProblem problem = Assert.Single(JolietNames.ProblemsIn(["a" + (char)7 + "b.txt"]));

        Assert.Equal("has a control character in its name, which Windows does not allow.", problem.Why);
    }

    [Fact]
    public void An_emoji_is_refused_because_each_half_came_back_as_an_underscore()
    {
        NameProblem problem = Assert.Single(JolietNames.ProblemsIn(["Extras/party \U0001F389.txt"]));

        Assert.Contains("such as an emoji", problem.Why, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("Nul.tar.gz")]
    [InlineData("COM1.log")]
    [InlineData("lpt9")]
    [InlineData("AUX .txt")]
    public void A_name_windows_keeps_for_a_device_is_refused_with_or_without_an_extension(string name)
    {
        Assert.Contains(
            JolietNames.ProblemsIn([name]),
            problem => problem.Why.Contains("for a device", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CONTENT.txt")]
    [InlineData("COM10.log")]
    [InlineData("console.exe")]
    [InlineData("my.con")]
    public void A_name_that_only_looks_like_a_device_is_left_alone(string name)
    {
        Assert.Empty(JolietNames.ProblemsIn([name]));
    }

    [Fact]
    public void A_name_ending_in_a_dot_or_a_space_is_refused_because_windows_drops_it()
    {
        Assert.Equal(
            ["ends with a dot, which Windows drops.", "ends with a space, which Windows drops."],
            JolietNames.ProblemsIn(["Extras/notes.", "Extras/readme "]).Select(problem => problem.Why));
    }

    [Fact]
    public void A_name_of_a_hundred_and_three_is_fine_and_one_more_is_not()
    {
        string fits = new string('a', 99) + ".txt";
        string tooLong = new string('a', 100) + ".txt";

        Assert.Equal(JolietNames.Longest, fits.Length);
        Assert.Empty(JolietNames.ProblemsIn([fits]));

        NameProblem problem = Assert.Single(JolietNames.ProblemsIn([tooLong]));
        Assert.Equal(
            "the name is 104 characters long, and Windows reads names on this disc up to 103.",
            problem.Why);
    }

    [Fact]
    public void A_folder_is_held_to_the_same_rules_as_a_file()
    {
        string folder = new('f', 104);

        NameProblem problem = Assert.Single(JolietNames.ProblemsIn([folder + "/a.txt"]));

        Assert.Equal(folder, problem.RelativePath);
    }

    [Fact]
    public void A_folder_is_named_once_however_many_files_are_in_it()
    {
        Assert.Single(JolietNames.ProblemsIn(["bad: name/a.txt", "bad: name/b.txt", "bad: name/deeper/c.txt"]));
    }

    [Fact]
    public void Two_names_that_differ_only_in_capitals_are_one_file_to_windows()
    {
        NameProblem problem = Assert.Single(JolietNames.ProblemsIn(["Data/Readme.txt", "Data/README.TXT"]));

        Assert.Equal("Data/Readme.txt", problem.RelativePath);
        Assert.Equal(
            "differs from README.TXT only in capitals, and Windows sees one file where there are two.",
            problem.Why);
    }

    [Fact]
    public void The_same_name_in_two_different_folders_is_two_files()
    {
        Assert.Empty(JolietNames.ProblemsIn(["one/readme.txt", "two/README.TXT"]));
    }

    [Fact]
    public void Two_folders_that_differ_only_in_capitals_are_caught_as_well()
    {
        Assert.Single(JolietNames.ProblemsIn(["Data/a.txt", "DATA/b.txt"]));
    }

    [Fact]
    public void Paths_written_with_a_backslash_are_taken_apart_the_same_way()
    {
        string backslash = ((char)92).ToString();

        NameProblem problem = Assert.Single(JolietNames.ProblemsIn(["Extras" + backslash + "what?.txt"]));

        Assert.Equal("Extras/what?.txt", problem.RelativePath);
    }

    [Fact]
    public void Says_the_same_thing_whatever_order_the_files_arrive_in()
    {
        string[] oneWay = ["b?.txt", "a*.txt", "c ."];
        string[] theOther = ["c .", "a*.txt", "b?.txt"];

        Assert.Equal(JolietNames.ProblemsIn(oneWay), JolietNames.ProblemsIn(theOther));
    }

    [Fact]
    public void The_refusal_names_the_files_so_somebody_can_go_and_rename_them()
    {
        Refusal refusal = Refusals.SomeNamesWouldNotReachWindows(
            JolietNames.ProblemsIn(["Extras/what?.txt", "Extras/notes."]));

        Assert.Equal("Some names on the disc would not reach Windows", refusal.Headline);
        Assert.Contains("Extras/what?.txt: has ? in its name", refusal.Because, StringComparison.Ordinal);
        Assert.Contains("Extras/notes.: ends with a dot", refusal.Because, StringComparison.Ordinal);
        Assert.Equal([new Figure("names to change", "2")], refusal.Figures);
    }

    [Fact]
    public void The_refusal_shows_ten_and_says_how_many_more_there_are()
    {
        Refusal refusal = Refusals.SomeNamesWouldNotReachWindows(
            JolietNames.ProblemsIn(Enumerable.Range(1, 13).Select(number => $"file {number:00}?.txt")));

        Assert.Contains("file 10?.txt", refusal.Because, StringComparison.Ordinal);
        Assert.DoesNotContain("file 11?.txt", refusal.Because, StringComparison.Ordinal);
        Assert.Contains("and 3 more", refusal.Because, StringComparison.Ordinal);
        Assert.Equal([new Figure("names to change", "13")], refusal.Figures);
    }

    [Fact]
    public void The_refusal_does_not_say_and_more_when_there_are_no_more()
    {
        Refusal refusal = Refusals.SomeNamesWouldNotReachWindows(JolietNames.ProblemsIn(["a?.txt"]));

        Assert.DoesNotContain("more", refusal.Because, StringComparison.Ordinal);
    }
}
