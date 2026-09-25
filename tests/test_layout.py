import re
from pathlib import Path

import pytest

from discwright.games import GameInfo
from discwright.layout import (
    ascii_fold, disc_entry_extras, disc_entry_folder, disc_entry_setup, disc_game_indexes,
    disc_icon_name, entry_file_relative, game_folder_name, is_reserved_name, menu_games,
)


def ent(name, kind="Game", parent=-1, manual=None, extras=None):
    return GameInfo(ok=True, game_name=name, kind=kind, parent_index=parent,
                    setup_exe=Path("setup_" + re.sub(r"\W", "", name) + ".exe"),
                    manual_path=manual, extras_path=extras)


# ---- the icon's name, against what Windows DiscWright 0.7.2 produced -----------

@pytest.mark.parametrize("label,expected", [
    ("ALAN WAKE", "ALANWAKE.ico"),                          # off a real disc
    ("Wiedźmin 3", "Wiedzmin3.ico"),
    ("Ведьмак", "discd20191a5.ico"),  # Cyrillic: hashed
    ("", "disc.ico"),
    ("Z" * 60, "Z" * 40 + ".ico"),
])
def test_names_the_icon_the_way_windows_does(label, expected):
    assert disc_icon_name(label) == expected


def test_folds_accents_to_the_plain_letter():
    assert ascii_fold("Wiedźmin") == "Wiedzmin"
    assert ascii_fold("Über") == "Uber"


def test_leaves_an_alphabet_with_no_latin_equivalent_alone():
    assert ascii_fold("Вед") == "Вед"


# ---- Get-GameFolderName, ported from the Windows suite -------------------------

def test_numbers_from_one_and_pads_to_two_digits():
    assert game_folder_name(1, "Hollow Knight") == "01 - Hollow Knight"


def test_keeps_two_digit_numbers_intact():
    assert game_folder_name(12, "Doom") == "12 - Doom"


def test_folds_accents_rather_than_deleting_the_letter():
    assert game_folder_name(2, "Wiedźmin") == "02 - Wiedzmin"


def test_removes_characters_windows_will_not_accept_in_a_folder_name():
    assert not re.search(r'[\\/:*?"<>|]', game_folder_name(3, 'A/B\\C:D*E?F"G<H>I|J'))


def test_collapses_runs_of_whitespace():
    assert game_folder_name(4, "A     B") == "04 - A B"


def test_never_ends_in_a_dot_which_windows_silently_strips():
    assert not game_folder_name(5, "Fallout...").endswith(".")


def test_never_ends_in_a_dot_even_when_the_cut_lands_on_one():
    # Beyond the Windows suite, which misses this: 47 letters then a dot puts the
    # dot at the 48-character cut. Measured on Windows 0.7.2: it asks for
    # '...AAA.' and Windows creates '...AAA', so the menu's path points at nothing.
    assert not game_folder_name(1, "A" * 47 + ".B").endswith(".")


def test_caps_a_very_long_title():
    assert len(game_folder_name(6, "X" * 300)) <= 53


def test_still_produces_a_folder_for_an_empty_title():
    assert game_folder_name(7, "") == "07 - Game"


def test_still_produces_a_folder_for_a_title_with_no_usable_characters():
    assert game_folder_name(8, "???") == "08 - Game"


def test_gives_identically_titled_games_distinct_folders():
    assert len({game_folder_name(i, "Same Title") for i in (1, 2, 3)}) == 3


# ---- Test-ReservedDiscName, ported --------------------------------------------

def test_reserves_the_names_the_linux_half_of_the_disc_uses():
    assert is_reserved_name(".xdg-volume-info", "TheWitcher.ico")
    assert is_reserved_name("TheWitcher.png", "TheWitcher.ico")
    assert is_reserved_name("disc.png", "TheWitcher.ico")
    assert not is_reserved_name("screenshot.png", "TheWitcher.ico")


@pytest.mark.parametrize("name", ["autorun.inf", "AUTORUN", "Extras", "Games",
                                  "discproject.json", "disc.ico", "Add-ons"])
def test_reserves(name):
    assert is_reserved_name(name, "Witcher.ico")


def test_reserves_the_disc_icon_itself():
    assert is_reserved_name("Witcher.ico", "Witcher.ico")


def test_reserves_anything_that_looks_like_a_gog_installer():
    assert is_reserved_name("setup_doom_1.0.exe", "Witcher.ico")


def test_leaves_ordinary_extra_content_alone():
    assert not is_reserved_name("Soundtrack", "Witcher.ico")


def test_reserves_a_name_whatever_its_case():
    # Windows reads the disc too, and there AUTORUN.INF is autorun.inf.
    assert is_reserved_name("AUTORUN.INF", "Witcher.ico")
    assert is_reserved_name("Setup_Doom.EXE", "Witcher.ico")
    assert is_reserved_name("witcher.PNG", "Witcher.ico")


# ---- Filing an add-on under the game it belongs to, ported ----------------------

@pytest.fixture
def grp():
    # Two games, patches on each. The old layout made five sibling folders of this.
    return [ent("Hollow Knight"), ent("Update 1.5.12459", "AddOn", 0),
            ent("Update 1.5.12618", "AddOn", 0), ent("Ori and the Blind Forest"),
            ent("Definitive Edition Upgrade", "AddOn", 3)]


@pytest.fixture
def grp_one():
    # One game carrying patches: still one game, so still a flat disc.
    return [ent("Hollow Knight"), ent("Update 1.5.12459", "AddOn", 0),
            ent("Update 1.5.12618", "AddOn", 0)]


def test_puts_an_add_on_inside_its_games_folder(grp):
    game, add_on = disc_entry_folder(grp, 0), disc_entry_folder(grp, 1)
    assert game == r"Games\01 - Hollow Knight"
    assert add_on == r"Games\01 - Hollow Knight\Add-ons\01 - Update 1.5.12459"
    assert add_on.startswith(game + "\\Add-ons\\")


def test_numbers_the_games_by_games(grp):
    assert disc_entry_folder(grp, 3) == r"Games\02 - Ori and the Blind Forest"


def test_numbers_add_ons_within_their_own_game(grp):
    assert disc_entry_folder(grp, 2) == r"Games\01 - Hollow Knight\Add-ons\02 - Update 1.5.12618"
    assert (disc_entry_folder(grp, 4)
            == r"Games\02 - Ori and the Blind Forest\Add-ons\01 - Definitive Edition Upgrade")


def test_keeps_the_flat_root_for_one_game_and_its_patches(grp_one):
    assert disc_entry_folder(grp_one, 0) == ""
    assert disc_entry_folder(grp_one, 1) == r"Add-ons\01 - Update 1.5.12459"
    assert disc_entry_folder(grp_one, 2) == r"Add-ons\02 - Update 1.5.12618"


def test_builds_the_installer_path_from_wherever_the_folder_is(grp_one, grp):
    assert disc_entry_setup(grp_one, 1) == r"Add-ons\01 - Update 1.5.12459\setup_Update1512459.exe"
    assert disc_entry_setup(grp, 4) == disc_entry_folder(grp, 4) + r"\setup_DefinitiveEditionUpgrade.exe"
    assert disc_entry_setup(grp_one, 0) == "setup_HollowKnight.exe"


def test_files_an_add_ons_own_extras_inside_the_add_ons_folder(grp):
    assert disc_entry_extras(grp, 1) == r"Games\01 - Hollow Knight\Add-ons\01 - Update 1.5.12459\Extras"


def test_leaves_a_lone_game_and_a_pair_of_plain_games_where_they_were():
    assert disc_entry_folder([ent("Solo")], 0) == ""
    two = [ent("One"), ent("Two")]
    assert disc_entry_folder(two, 0) == r"Games\01 - One"
    assert disc_entry_folder(two, 1) == r"Games\02 - Two"
    assert disc_entry_extras([ent("Solo")], 0) == "Extras"


def test_promotes_an_add_on_whose_game_is_missing():
    e = [ent("Hollow Knight"), ent("Stray Patch", "AddOn", 7)]
    assert disc_entry_folder(e, 1) == r"Games\02 - Stray Patch"
    assert len(menu_games(e)) == 2


def test_promotes_an_add_on_hanging_off_another_add_on():
    e = [ent("Hollow Knight"), ent("Update 1", "AddOn", 0), ent("Patch of a patch", "AddOn", 1)]
    assert disc_entry_folder(e, 2) == r"Games\02 - Patch of a patch"
    assert disc_entry_folder(e, 1) == r"Games\01 - Hollow Knight\Add-ons\01 - Update 1"


def test_hands_the_menu_the_same_paths_the_layout_decided(grp):
    from_menu = []
    for g in menu_games(grp):
        from_menu.append(g["setup"])
        from_menu += [a["setup"] for a in g["add_ons"]]
    from_layout = [disc_entry_setup(grp, i) for i in range(len(grp))]
    assert sorted(from_menu) == sorted(from_layout)


MIXED = [("A",), ("B", "AddOn", 0), ("C", "AddOn", 7), ("D",), ("E", "AddOn", 3), ("F", "AddOn", 1)]


def test_never_lands_two_entries_in_the_same_folder():
    mixed = [ent(*m) for m in MIXED]
    folders = [disc_entry_folder(mixed, i).casefold() for i in range(len(mixed))]
    assert len(set(folders)) == len(mixed)


def test_counts_the_same_entries_as_games_that_the_menu_does():
    mixed = [ent(*m) for m in MIXED]
    assert len(disc_game_indexes(mixed)) == len(menu_games(mixed))


def test_gives_the_menu_an_entrys_own_manual_beside_its_installer():
    e = [ent("One", manual=Path("/somewhere/One Manual.pdf")), ent("Two")]
    one, two = menu_games(e)
    assert one["manual"] == r"Games\01 - One\Extras\One Manual.pdf"
    assert one["extras"] == r"Games\01 - One\Extras"
    assert two["manual"] == "" and two["extras"] == ""


def test_falls_back_to_the_game_name_when_there_is_no_match_name():
    e = [ent("Old Project Game")]
    assert menu_games(e)[0]["match_name"] == "Old Project Game"


# ---- an entry that is a folder of game files, not a GOG download ----------------

def files_ent(folder, name=None, setup=None):
    """An entry as folder_info makes one: everything under a folder, shape kept."""
    folder = Path(folder)
    return GameInfo(ok=True, source="Files", folder=folder,
                    game_name=name or folder.name,
                    setup_exe=Path(setup) if setup else None,
                    files=sorted(p for p in folder.rglob("*") if p.is_file()))


@pytest.fixture
def loose_folder(tmp_path):
    d = tmp_path / "loose game"
    (d / "data" / "textures").mkdir(parents=True)
    (d / "Game.exe").write_bytes(b"x")
    (d / "data" / "config.ini").write_text("x=1")
    (d / "data" / "textures" / "wall.dds").write_text("dds")
    return d


def test_keeps_the_shape_of_a_folder_of_files(loose_folder):
    # A game that expects data/textures/wall.dds beside its exe arrives broken if
    # the disc flattens it. A GOG download has no shape to keep, so its files go
    # by name, exactly as before.
    e = files_ent(loose_folder)
    deep = next(f for f in e.files if f.name == "wall.dds")
    assert entry_file_relative(e, deep) == str(Path("data", "textures", "wall.dds"))
    assert entry_file_relative(ent("Alpha"), Path("/somewhere/setup_Alpha.exe")) == "setup_Alpha.exe"


def test_takes_a_file_from_outside_the_folder_by_its_name(loose_folder):
    # Nothing here should produce one, but dropping it would lose a file off the
    # disc, which is worse than putting it at the entry's root.
    e = files_ent(loose_folder)
    assert entry_file_relative(e, Path("/elsewhere/stray.dat")) == "stray.dat"


def test_an_entry_with_no_installer_has_no_setup_path(loose_folder):
    entries = [files_ent(loose_folder)]
    assert disc_entry_setup(entries, 0) == ""
    # And one that has an installer still names it, wherever the entry landed.
    entries = [files_ent(loose_folder, setup=loose_folder / "Game.exe"), ent("Beta")]
    assert disc_entry_setup(entries, 0).endswith("Game.exe")


def test_tells_the_menu_where_the_files_are(loose_folder):
    # Empty setup is the menu's signal to offer the folder instead of Install,
    # so it has to be told which folder that is.
    entries = [files_ent(loose_folder), ent("Beta")]
    games = menu_games(entries)
    assert games[0]["setup"] == ""
    assert games[0]["folder"] == disc_entry_folder(entries, 0)
    assert games[1]["setup"] != ""


def test_a_lone_folder_of_files_keeps_the_disc_root(loose_folder):
    # One game on a disc has no numbered folder, so its files sit at the root and
    # the menu's folder for it is the root: empty.
    entries = [files_ent(loose_folder)]
    assert menu_games(entries)[0]["folder"] == ""
