import os
from pathlib import Path

import pytest

from discwright.games import add_on_info, add_on_name

MB = 1 << 20


def sparse(path: Path, size: int) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "wb") as f:
        f.truncate(size)
    return path


# Ported from 'Naming an add-on' in the Windows suite.

def test_puts_the_version_a_gog_patch_moves_to_at_the_front():
    # The menu button clips at about twenty characters, and two patches of one
    # game differ only in their versions, so that part has to come first.
    assert (add_on_name("patch_hollow_knight_1.5.12459_(88294)_to_1.5.12618_(89712).exe")
            == "Update 1.5.12618 (89712)")


def test_gives_two_patches_of_one_game_different_names():
    a = add_on_name("patch_hollow_knight_1.5.12459_(88294)_to_1.5.12618_(89712).exe")
    b = add_on_name("patch_hollow_knight_1.5.12618_(89712)_to_1.5.12620_(89718).exe")
    assert a != b
    assert a[:18] != b[:18]


def test_reads_a_plain_installer_name_as_words():
    assert add_on_name("gmdx_v10_overhaul.exe") == "gmdx v10 overhaul"


@pytest.mark.parametrize("name", ["x.exe", "patch_.exe", "setup_.exe", "_.exe"])
def test_never_comes_back_empty(name):
    assert add_on_name(name)


def test_strips_setup_from_a_dlc_installer():
    assert add_on_name("setup_witcher_bonus_content_1.0.exe") == "witcher bonus content 1.0"


# Ported from 'Accepting an add-on installer' in the Windows suite.

@pytest.fixture
def addon_dir(tmp_path):
    d = tmp_path / "addon-src"
    for n in ("setup_base_1.0.exe", "patch_base_1.0_to_1.1.exe", "GMDX_v10.exe"):
        sparse(d / n, 2 * MB)
    # A part belonging to the mod, to prove parts are collected for add-ons too.
    sparse(d / "GMDX_v10-1.bin", MB)
    sparse(d / "readme.txt", 10)
    return d


def test_accepts_an_installer_that_is_not_named_setup(addon_dir):
    # The point of the relaxed filter: a mod is never setup_*, nor is a GOG patch.
    a = add_on_info(addon_dir / "GMDX_v10.exe")
    assert a.ok and a.kind == "AddOn"


def test_accepts_a_gog_patch(addon_dir):
    assert add_on_info(addon_dir / "patch_base_1.0_to_1.1.exe").ok


def test_collects_an_add_ons_own_parts(addon_dir):
    assert len(add_on_info(addon_dir / "GMDX_v10.exe").files) == 2


def test_does_not_take_the_base_games_files_with_it(addon_dir):
    a = add_on_info(addon_dir / "patch_base_1.0_to_1.1.exe")
    assert [p.name for p in a.files] == ["patch_base_1.0_to_1.1.exe"]


def test_refuses_something_that_is_not_an_installer(addon_dir):
    a = add_on_info(addon_dir / "readme.txt")
    assert not a.ok
    assert "extra content" in a.msg.lower()


def test_refuses_a_file_that_is_not_there(addon_dir):
    assert not add_on_info(addon_dir / "nope.exe").ok


def test_starts_filed_under_nothing(addon_dir):
    # Which game it belongs to is decided afterwards, by whoever adds it.
    assert add_on_info(addon_dir / "GMDX_v10.exe").parent_index == -1


# Linux on top of the Windows cases.

def test_accepts_an_installer_whose_extension_is_in_capitals(tmp_path):
    # Windows' comparison ignores case, so it accepts MOD.EXE. So must this.
    a = add_on_info(sparse(tmp_path / "BIG_MOD_2.0.EXE", MB))
    assert a.ok
    assert a.game_name == "BIG MOD 2.0"


# Real GOG patches, against what Windows DiscWright 0.7.2's own Get-AddOnInfo
# said about the same files. Runs only where DISCWRIGHT_GOG_DIR points at them.
GOG = os.environ.get("DISCWRIGHT_GOG_DIR")

WINDOWS_SAID = {
    ("Alan Wake", "patch_alan_wake_1.0_Hotfix_(24778)_to_1.1_music_fixed_(75607).exe"):
        "Detected: Update 1.1 music fixed (75607)  (1 file, 94 MB)",
    ("Alan Wake", "patch_alan_wake_1.1_music_fixed_(75607)_to_1.1_music_lang_fix_(80728).exe"):
        "Detected: Update 1.1 music lang fix (80728)  (1 file, 4 MB)",
    ("Hollow Knight", "patch_hollow_knight_1.5.12459_(88294)_to_1.5.12618_(89712).exe"):
        "Detected: Update 1.5.12618 (89712)  (1 file, 28 MB)",
    ("Hollow Knight", "patch_hollow_knight_1.5.12618_(89712)_to_1.5.12620_(89718).exe"):
        "Detected: Update 1.5.12620 (89718)  (1 file, 4 MB)",
    ("Hollow Knight", "patch_hollow_knight_1.5.78.11833_(50884)_to_1.5.78.11833a_(85515).exe"):
        "Detected: Update 1.5.78.11833a (85515)  (1 file, 4 MB)",
    ("Hollow Knight", "patch_hollow_knight_1.5.78.11833a_(85515)_to_1.5.12459_(88294).exe"):
        "Detected: Update 1.5.12459 (88294)  (1 file, 621 MB)",
    ("The Witcher", "patch_the_witcher_enhanced_edition_directors_cut_1.5_(A)_(10712)_to_1.5_(CS)_GOG_0.2_(77554).exe"):
        "Detected: Update 1.5 (CS) GOG 0.2 (77554)  (1 file, 4 MB)",
}


@pytest.mark.skipif(not GOG, reason="DISCWRIGHT_GOG_DIR not set")
@pytest.mark.parametrize("where,expected", WINDOWS_SAID.items())
def test_real_gog_patches_read_as_windows_reads_them(where, expected):
    folder, file = where
    a = add_on_info(Path(GOG, folder, file))
    assert a.ok
    assert a.msg == expected
