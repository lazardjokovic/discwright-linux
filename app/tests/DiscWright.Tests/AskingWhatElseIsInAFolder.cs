using DiscWright.Core;
using DiscWright.Linux;
using DiscWright.Platform;
using DiscWright.ViewModels;

namespace DiscWright.Tests;

/// <summary>
/// The question asked when a folder holds more than the game: what else was
/// in there, and what is to become of each thing.
/// </summary>
/// <remarks>
/// <para>
/// In the Windows app everything here is inside the dialog's own code, where
/// no test could reach it. <see cref="WhatElseWasInThere"/> is the same
/// logic with no window in it, written for the Linux window to stand on.
/// These are the first tests any of it has had.
/// </para>
/// <para>
/// The folder is a real one, recognised by a real reader, so what is left
/// behind is what the program itself would have found left behind.
/// </para>
/// </remarks>
public sealed class AskingWhatElseIsInAFolder : IDisposable
{
    private const string Installer = "setup_fixture_game_1.2.3_(64bit)_(12345).exe";

    private readonly string _room = Path.Combine(Path.GetTempPath(), "dw-what-else-" + Guid.NewGuid().ToString("N"));
    private readonly GameFolder _game;
    private readonly IReadOnlyList<LeftBehind> _left;

    public AskingWhatElseIsInAFolder()
    {
        string download = Path.Combine(_room, "Fixture Game");
        Directory.CreateDirectory(Path.Combine(download, "artbook"));

        File.Copy(Repository.File("tests", "DiscWright.Tests", "fixtures", "pe", "with-product.exe"), Path.Combine(download, Installer));

        // A manual, a folder of pictures, and something that installs.
        File.WriteAllBytes(Path.Combine(download, "Fixture Game Manual.pdf"), new byte[300]);
        File.WriteAllBytes(Path.Combine(download, "artbook", "cover.png"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(download, "artbook", "map.png"), new byte[24]);
        File.WriteAllBytes(Path.Combine(download, "soundtrack_fixture_game.exe"), new byte[2000]);

        WindowsFileSystem disk = new();

        _game = Assert.IsType<Outcome<GameFolder>.Ok>(
            new GameFolders(disk, GameFolderReader.InTheUsualOrder(new VersionResourceFacts())).At(download)).Value;
        _left = WhatWasLeftBehind.In(disk.List(download), _game);
    }

    public void Dispose() => Directory.Delete(_room, recursive: true);

    [Fact]
    public void A_folder_is_one_row_and_not_one_for_each_file_inside_it()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        // Four files left behind, and three things to answer for: an
        // artbook is one answer, not one per page.
        Assert.Equal(4, _left.Count);
        Assert.Equal(
            ["Fixture Game Manual.pdf", "artbook", "soundtrack_fixture_game.exe"],
            asking.Files.Select(one => one.Name).Order(StringComparer.Ordinal));

        Assert.Equal("There are 3 more things in that folder", asking.Headline);

        OneThingLeftBehind artbook = asking.Files.Single(one => one.Name == "artbook");

        Assert.Equal("A folder of 2 files", artbook.Explains);
        Assert.Equal(ByteSize.Describe(1024), artbook.Size);
    }

    [Fact]
    public void Everything_starts_ticked_with_a_guess_at_what_it_is()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        // Ticked, because somebody pointed at the folder these came from,
        // and the surprise worth avoiding is losing a file.
        Assert.All(asking.Files, one => Assert.True(one.Wanted));

        Assert.Equal((int)WhatBecomesOfIt.Manual, asking.Files.Single(one => one.Name == "Fixture Game Manual.pdf").Treatment);
        Assert.Equal((int)WhatBecomesOfIt.Extra, asking.Files.Single(one => one.Name == "artbook").Treatment);
        Assert.Equal((int)WhatBecomesOfIt.AddOn, asking.Files.Single(one => one.Name == "soundtrack_fixture_game.exe").Treatment);
    }

    [Fact]
    public void The_dropdown_on_each_row_answers_to_a_name_of_its_own()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        // Every row is the same template, so an id written into it would be
        // the same on all of them.
        Assert.Equal(asking.Files.Count, asking.Files.Select(one => one.WhatToDoWithIt).Distinct().Count());
        Assert.Contains("artbook: what to do with it", asking.Files.Select(one => one.WhatToDoWithIt));
    }

    [Fact]
    public void The_big_button_has_nothing_to_do_when_nothing_is_ticked_and_says_so_as_the_ticks_change()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);
        List<string?> told = [];
        asking.PropertyChanged += (_, what) => told.Add(what.PropertyName);

        Assert.True(asking.AnyWanted);

        foreach (OneThingLeftBehind each in asking.Files)
        {
            each.Wanted = false;
        }

        // "Put these on too" with nothing ticked would put nothing on, which
        // is the other button's job.
        Assert.False(asking.AnyWanted);
        Assert.Contains(nameof(WhatElseWasInThere.AnyWanted), told);
        Assert.Contains(nameof(WhatElseWasInThere.CanTickThemAll), told);
    }

    [Fact]
    public void Tick_them_all_ticks_every_row_and_then_has_nothing_left_to_do()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        Assert.False(asking.CanTickThemAll);

        asking.Files[0].Wanted = false;
        asking.Files[2].Wanted = false;

        Assert.True(asking.CanTickThemAll);

        asking.TickThemAll();

        Assert.All(asking.Files, one => Assert.True(one.Wanted));
        Assert.False(asking.CanTickThemAll);
    }

    [Fact]
    public void Shutting_the_question_is_not_an_answer()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        // Not "none of them". Treated as that, every file was dropped in
        // silence, which is the one thing the question exists to prevent.
        Assert.Null(asking.Answered(HowItWasAnswered.ShutWithoutAnswering));
    }

    [Fact]
    public void None_of_them_puts_the_game_on_as_it_was()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        (GameFolder game, IReadOnlyList<LeftBehind> addOns, TheSameAnswers? forTheRest) =
            Assert.NotNull(asking.Answered(HowItWasAnswered.NoneOfThem));

        Assert.Equal(_game, game);
        Assert.Empty(addOns);
        Assert.Null(forTheRest);
    }

    [Fact]
    public void Put_these_on_too_gives_each_ticked_thing_the_place_it_was_given()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        (GameFolder game, IReadOnlyList<LeftBehind> addOns, _) =
            Assert.NotNull(asking.Answered(HowItWasAnswered.PutTheseOnToo));

        // The manual and the artbook become the game's own.
        GameExtra manual = Assert.Single(game.OwnOrNone, own => own.Kind == GameExtraKind.Manual);
        GameExtra artbook = Assert.Single(game.OwnOrNone, own => own.Kind == GameExtraKind.Extra);

        Assert.Equal(Path.Combine(_game.Path, "Fixture Game Manual.pdf"), manual.Path);
        Assert.Equal(Path.Combine(_game.Path, "artbook"), artbook.Path);

        // And the one that installs is attached to the game as an add-on.
        Assert.Equal("soundtrack_fixture_game.exe", Assert.Single(addOns).Name);
    }

    [Fact]
    public void What_was_unticked_is_left_off()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);

        asking.Files.Single(one => one.Name == "artbook").Wanted = false;
        asking.Files.Single(one => one.Name == "soundtrack_fixture_game.exe").Wanted = false;

        (GameFolder game, IReadOnlyList<LeftBehind> addOns, _) =
            Assert.NotNull(asking.Answered(HowItWasAnswered.PutTheseOnToo));

        Assert.Equal(GameExtraKind.Manual, Assert.Single(game.OwnOrNone).Kind);
        Assert.Empty(addOns);
    }

    [Fact]
    public void A_thing_goes_where_the_dropdown_was_changed_to_and_not_where_it_was_guessed()
    {
        WhatElseWasInThere asking = new(_game, _left, 0);
        List<string?> told = [];

        OneThingLeftBehind soundtrack = asking.Files.Single(one => one.Name == "soundtrack_fixture_game.exe");
        soundtrack.PropertyChanged += (_, what) => told.Add(what.PropertyName);
        soundtrack.Treatment = (int)WhatBecomesOfIt.Extra;

        Assert.Contains(nameof(OneThingLeftBehind.Treatment), told);

        (GameFolder game, IReadOnlyList<LeftBehind> addOns, _) =
            Assert.NotNull(asking.Answered(HowItWasAnswered.PutTheseOnToo));

        Assert.Empty(addOns);
        Assert.Contains(game.OwnOrNone, own => own.Path == Path.Combine(_game.Path, "soundtrack_fixture_game.exe"));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "Answer the same way for the other game being added")]
    [InlineData(3, "Answer the same way for the other 3 games being added")]
    public void Offers_to_answer_the_same_way_only_when_more_games_follow(int after, string? says)
    {
        Assert.Equal(says, new WhatElseWasInThere(_game, _left, after).TheSameForTheRestSays);
    }

    [Fact]
    public void The_same_for_the_rest_carries_the_answers_forward_when_it_is_ticked()
    {
        WhatElseWasInThere asking = new(_game, _left, 2) { TheSameForTheRest = true };
        asking.Files.Single(one => one.Name == "artbook").Wanted = false;

        (_, _, TheSameAnswers? forTheRest) = Assert.NotNull(asking.Answered(HowItWasAnswered.PutTheseOnToo));

        Assert.NotNull(forTheRest);

        // Asked of the same rows, it gives the same answers: the manual and
        // the soundtrack, and not the artbook that was unticked.
        IReadOnlyList<LeftBehind> rows = WhatWasLeftBehind.Gathered(_left);

        Assert.Equal(
            ["Fixture Game Manual.pdf", "soundtrack_fixture_game.exe"],
            forTheRest.For(rows, _game.Installer?.Name).Select(answer => answer.What.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void None_of_them_is_an_answer_that_can_be_carried_forward_too()
    {
        WhatElseWasInThere asking = new(_game, _left, 2) { TheSameForTheRest = true };

        (_, _, TheSameAnswers? forTheRest) = Assert.NotNull(asking.Answered(HowItWasAnswered.NoneOfThem));

        Assert.NotNull(forTheRest);
        Assert.Empty(forTheRest.For(WhatWasLeftBehind.Gathered(_left), _game.Installer?.Name));
    }

    [Fact]
    public void Nothing_is_carried_forward_when_it_was_not_ticked_or_when_no_games_follow()
    {
        WhatElseWasInThere notTicked = new(_game, _left, 2);
        Assert.Null(Assert.NotNull(notTicked.Answered(HowItWasAnswered.PutTheseOnToo)).ForTheRest);

        // Ticked by something that had no business ticking it: with no games
        // to follow there is no tick on the window to have pressed.
        WhatElseWasInThere nothingFollows = new(_game, _left, 0) { TheSameForTheRest = true };
        Assert.Null(Assert.NotNull(nothingFollows.Answered(HowItWasAnswered.PutTheseOnToo)).ForTheRest);
    }

    [Fact]
    public void Nothing_is_asked_when_nothing_was_left_behind()
    {
        Assert.False(WhatElseWasInThere.IsWorthAsking([]));
        Assert.True(WhatElseWasInThere.IsWorthAsking(_left));

        (GameFolder game, IReadOnlyList<LeftBehind> addOns, TheSameAnswers? forTheRest) =
            WhatElseWasInThere.WithNothingToAsk(_game);

        Assert.Equal(_game, game);
        Assert.Empty(addOns);
        Assert.Null(forTheRest);
    }
}
