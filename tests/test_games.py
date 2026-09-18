import os
import shutil
from pathlib import Path

import pytest

from discwright.games import format_size, game_info

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
