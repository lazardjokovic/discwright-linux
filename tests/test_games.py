import os
import shutil
from pathlib import Path

import pytest

from discwright.games import (folder_executables, folder_info, format_size, game_info,
                              gog_subfolders)

PE = Path(__file__).parent / "fixtures" / "pe"
MB = 1 << 20


def sparse(path: Path, size: int) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "wb") as f:
        f.truncate(size)
    return path


@pytest.fixture
def game_a(tmp_path):
    # Same shape as the Windows suite's GameA: an installer and two parts, 8 MB.
    d = tmp_path / "aaa_first_game"
    sparse(d / "setup_first_game_1.0.exe", 2 * MB)
    sparse(d / "setup_first_game_1.0-1.bin", 3 * MB)
    sparse(d / "setup_first_game_1.0-2.bin", 3 * MB)
    return d


# Ported from the Get-GameInfo block of the Windows suite.

def test_finds_the_installer(game_a):
    assert game_info(game_a).ok


def test_collects_the_installer_and_its_parts(game_a):
    assert len(game_info(game_a).files) == 3


def test_totals_the_payload_across_every_file(game_a):
    assert game_info(game_a).total_bytes == 8 * MB


def test_reports_no_missing_parts_for_a_complete_download(game_a):
    assert game_info(game_a).missing_parts == []


def test_reports_no_missing_parts_for_a_game_that_has_none(tmp_path):
    sparse(tmp_path / "g" / "setup_single_1.0.exe", MB)
    assert game_info(tmp_path / "g").missing_parts == []


def test_spots_a_gap_in_the_part_numbering(tmp_path):
    d = tmp_path / "torn"
    sparse(d / "setup_torn_1.0.exe", MB)
    sparse(d / "setup_torn_1.0-1.bin", MB)
    sparse(d / "setup_torn_1.0-4.bin", MB)
    info = game_info(d)
    assert info.missing_parts == [2, 3]
    assert info.warning.startswith("INCOMPLETE")


def test_recognises_a_built_disc_folder_and_says_so(tmp_path):
    sparse(tmp_path / "Some Game Disc" / "disc" / "setup_some_game_1.0_(1).exe", MB)
    info = game_info(tmp_path / "Some Game Disc")
    assert not info.ok
    assert "disc DiscWright built" in info.msg


def test_still_gives_the_plain_message_for_an_empty_folder(tmp_path):
    (tmp_path / "empty").mkdir()
    assert "No GOG" in game_info(tmp_path / "empty").msg


def test_is_not_ok_for_a_folder_that_does_not_exist(tmp_path):
    info = game_info(tmp_path / "no-such-folder")
    assert not info.ok
    assert info.folder is None


# What Linux needs on top of the Windows cases.

def test_matches_names_without_regard_to_case(tmp_path):
    # Windows finds these for free; a case-sensitive glob would miss all three.
    d = tmp_path / "shouty"
    sparse(d / "Setup_Shouty_1.0.EXE", MB)
    sparse(d / "SETUP_SHOUTY_1.0-1.BIN", MB)
    sparse(d / "setup_shouty_1.0-2.Bin", MB)
    info = game_info(d)
    assert info.ok and len(info.files) == 3


def test_takes_the_largest_installer_and_says_which(tmp_path):
    d = tmp_path / "with_dlc"
    sparse(d / "setup_base_game_1.0.exe", 5 * MB)
    sparse(d / "setup_base_game_dlc_1.0.exe", MB)
    info = game_info(d)
    assert info.setup_exe.name == "setup_base_game_1.0.exe"
    assert "2 installers" in info.warning


def test_does_not_sweep_in_another_installers_parts(tmp_path):
    d = tmp_path / "two_games"
    sparse(d / "setup_big_1.0.exe", 5 * MB)
    sparse(d / "setup_big_1.0-1.bin", MB)
    sparse(d / "setup_small_1.0.exe", MB)
    sparse(d / "setup_small_1.0-1.bin", MB)
    names = [p.name for p in game_info(d).files]
    assert names == ["setup_big_1.0.exe", "setup_big_1.0-1.bin"]


def test_falls_back_to_the_file_name_when_the_installer_has_no_name(tmp_path):
    sparse(tmp_path / "g" / "setup_the_witcher_1.5_(77554).exe", MB)
    assert game_info(tmp_path / "g").game_name == "the witcher"


def test_takes_the_name_from_the_installer_and_trims_the_padding(tmp_path):
    d = tmp_path / "named"
    d.mkdir()
    shutil.copy(PE / "with-product.exe", d / "setup_whatever_1.0.exe")
    info = game_info(d)
    assert info.game_name == "Fixture Game: Directors Cut"
    assert info.match_name == info.game_name


def test_says_what_it_found_the_way_windows_does(game_a):
    assert game_info(game_a).msg == "Detected: first game  (3 files, 8 MB)"


@pytest.mark.parametrize("size,shown", [
    (0, "0 bytes"), (1023, "1,023 bytes"), (1024, "1 KB"), (5 * MB, "5 MB"),
    (8360420845, "7.79 GB"), (10180015440, "9.48 GB"),
])
def test_formats_sizes_the_way_windows_does(size, shown):
    assert format_size(size) == shown


# A folder that is not a GOG download. Ported from the Windows suite's block of
# the same name, added in 0.8.0.
#
# Asked for publicly: somebody had burned a 17 GB GOG disc with DiscWright and
# then wanted the same disc from game files GOG never packaged. Until then a
# game had to be a folder holding a setup_*.exe, which ruled out an installed
# game, an unpacked archive, an itch.io download and anything portable.

@pytest.fixture
def loose(tmp_path):
    d = tmp_path / "loose-game"
    sparse(d / "Game.exe", 3 * MB)
    sparse(d / "CrashHandler.exe", 64 * 1024)
    write(d / "readme.txt", "read me")
    write(d / "data" / "config.ini", "x=1")
    write(d / "data" / "textures" / "wall.dds", "dds")
    return d


def write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="ascii")
    return path


def test_the_gog_reader_refuses_it_which_is_what_asks_the_question(loose):
    info = game_info(loose)
    assert not info.ok
    assert info.msg.startswith("No GOG")


def test_takes_the_folder_with_everything_in_it(loose):
    info = folder_info(loose)
    assert info.ok
    assert info.source == "Files"
    assert len(info.files) == 5
    assert info.total_bytes > 3 * MB


def test_names_it_after_the_folder_when_no_installer_was_picked(loose):
    info = folder_info(loose)
    assert info.game_name == "loose-game"
    assert info.setup_exe is None
    # Play has nothing to look for either way, but an empty match makes the menu
    # search for nothing and light Play up for every game it finds.
    assert info.match_name == "loose-game"


def test_installs_with_the_executable_that_was_picked(loose):
    info = folder_info(loose, loose / "Game.exe")
    assert info.setup_exe == loose / "Game.exe"


def test_ignores_an_installer_that_is_not_there(loose):
    # Not a failure: the folder is still a folder of files, and the menu offers
    # it rather than an Install button pointing at nothing.
    info = folder_info(loose, loose / "Missing.exe")
    assert info.ok
    assert info.setup_exe is None


def test_refuses_a_folder_with_nothing_in_it(tmp_path):
    empty = tmp_path / "empty"
    empty.mkdir()
    info = folder_info(empty)
    assert not info.ok
    assert "no files" in info.msg


def test_refuses_a_folder_that_is_not_there(tmp_path):
    info = folder_info(tmp_path / "nowhere")
    assert not info.ok
    assert info.msg == "Folder not found."


def test_offers_every_executable_biggest_first(loose):
    # An installer is rarely the smallest thing in a game folder: a crash
    # handler or an uninstaller is.
    exes = folder_executables(loose)
    assert [p.name for p in exes] == ["Game.exe", "CrashHandler.exe"]


def test_looks_for_executables_all_the_way_down(loose):
    sparse(loose / "tools" / "Helper.exe", MB)
    assert "Helper.exe" in [p.name for p in folder_executables(loose)]


def test_orders_a_list_it_was_handed_exactly_as_one_it_read_itself(loose):
    # The dialog reads the folder once and hands the list over, because a cold
    # 20 GB game folder makes a second walk visible. Same rule either way, or
    # the list somebody sees stops matching the one under test.
    files = list(loose.rglob("*"))
    given = folder_executables(loose, files=[p for p in files if p.is_file()])
    assert [p.name for p in given] == [p.name for p in folder_executables(loose)]


def test_caps_the_list_however_the_folder_was_read(tmp_path):
    many = tmp_path / "many"
    for i in range(1, 31):
        sparse(many / f"tool{i}.exe", 1024 * i)
    assert len(folder_executables(many)) == 25
    assert len(folder_executables(many, files=[p for p in many.rglob("*") if p.is_file()])) == 25


def test_says_nothing_about_executables_in_a_folder_that_is_not_there(tmp_path):
    assert folder_executables(tmp_path / "nowhere") == []


def test_spots_the_folder_that_holds_the_downloads_rather_than_a_game(tmp_path, game_a):
    # The likeliest way to reach the question by mistake: the folder holding the
    # downloads has no setup_*.exe of its own, so it is not a GOG download, and
    # taking it whole would put every game on one entry named after the folder.
    shelf = tmp_path / "shelf"
    for name in ("game one", "game two"):
        sparse(shelf / name / "setup_a_game_1.0.exe", MB)
    (shelf / "artwork").mkdir()
    assert [p.name for p in gog_subfolders(shelf)] == ["game one", "game two"]
    # And a download itself is not one of those, or every ordinary folder would
    # carry the warning.
    assert gog_subfolders(game_a) == []
    assert gog_subfolders(tmp_path / "nowhere") == []


# Real GOG downloads, against what Windows DiscWright 0.7.2's own Get-GameInfo
# said about the same folders. Runs only where DISCWRIGHT_GOG_DIR points at them.
GOG = os.environ.get("DISCWRIGHT_GOG_DIR")

WINDOWS_SAID = {
    "Alan Wake": "Detected: Alan Wake  (3 files, 7.79 GB)",
    "Dead Space": "Detected: Dead Space  (4 files, 8.13 GB)",
    "Hollow Knight": "Detected: Hollow Knight  (1 file, 1.16 GB)",
    "The Witcher": "Detected: The Witcher - Enhanced Edition  (4 files, 9.48 GB)",
}


@pytest.mark.skipif(not GOG, reason="DISCWRIGHT_GOG_DIR not set")
@pytest.mark.parametrize("folder,expected", WINDOWS_SAID.items())
def test_real_gog_downloads_read_as_windows_reads_them(folder, expected):
    info = game_info(Path(GOG, folder))
    assert info.ok
    assert info.msg == expected
    assert info.warning == ""
