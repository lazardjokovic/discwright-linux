import json
from pathlib import Path

import pytest

from discwright.games import game_info
from discwright.project import (PROJECT_FILE, SCHEMA, Project, ProjectEntry, read_project,
                                save_project, settings_from_project)
from discwright.settings import DiscSettings

from test_stage import MB, sparse, src, write  # noqa: F401  (src is a fixture)

FIX = Path(__file__).parent / "fixtures" / "projects"
WINDOWS_V8 = FIX / "windows-0.7.1.json"      # off the demo disc, schema 8
WINDOWS_V5 = FIX / "windows-0.4.2.json"      # a real one, three schemas older


def loaded(path: Path) -> dict:
    return json.loads(path.read_bytes().decode("utf-8-sig"))


def settings(src: Path, out: Path, **kw) -> DiscSettings:
    base = dict(games=[game_info(src / "Alpha")], label="ALPHA", out_dir=out,
                icon_path=src / "art" / "alpha.ico", icon_is_ico=True, menu=True,
                bg_path=src / "art" / "alpha-bg.png", panel_side="Left", divider=True,
                show_title=True, title_text="Alpha: The Return", window_border=False,
                button_style="Minimal", buttons=["Play", "Exit"], linux_info=True,
                manual_path=src / "media" / "Alpha Manual.pdf",
                extras_path=src / "media" / "Extras",
                extra_items=[src / "media" / "Extras" / "wallpaper.txt"])
    base.update(kw)
    return DiscSettings(**base)


# ---- the file itself -------------------------------------------------------------------

def test_writes_the_schema_windows_writes(src, tmp_path):
    p = save_project(settings(src, tmp_path), tmp_path)
    assert p.name == PROJECT_FILE
    assert loaded(p)["Version"] == SCHEMA == loaded(WINDOWS_V8)["Version"]


def test_writes_the_keys_windows_writes(src, tmp_path):
    ours = loaded(save_project(settings(src, tmp_path), tmp_path))
    theirs = loaded(WINDOWS_V8)
    assert sorted(ours) == sorted(theirs)
    assert sorted(ours["Games"][0]) == sorted(theirs["Games"][0])


def test_writes_it_the_way_windows_powershell_reads_it(src, tmp_path):
    # UTF-8 with a byte order mark and CRLF: without the mark, PowerShell 5.1
    # reads the file in the machine's ANSI codepage and mangles any path with an
    # accent in it.
    raw = save_project(settings(src, tmp_path), tmp_path).read_bytes()
    assert raw[:3] == b"\xef\xbb\xbf"
    assert b"\r\n" in raw and b"\n" not in raw.replace(b"\r\n", b"")


def test_still_opens_in_the_oldest_app(src, tmp_path):
    # Schema 1 knew one game, in SourceFolder and GameName. Both are still
    # written, pointing at the first game.
    j = loaded(save_project(settings(src, tmp_path), tmp_path))
    assert j["SourceFolder"] == str(src / "Alpha") and j["GameName"] == "alpha"


def test_takes_a_path_with_an_accent_through_the_file(src, tmp_path):
    icon = write(src / "art" / "Caf\u00e9 \u2122.ico", "x")
    j = loaded(save_project(settings(src, tmp_path, icon_path=icon), tmp_path))
    assert j["IconPath"].endswith("Caf\u00e9 \u2122.ico")
    assert read_project(tmp_path / PROJECT_FILE).icon_path.endswith("Caf\u00e9 \u2122.ico")


# ---- reading back ----------------------------------------------------------------------

def test_reads_back_everything_it_wrote(src, tmp_path):
    s = settings(src, tmp_path)
    save_project(s, tmp_path)
    p = read_project(tmp_path / PROJECT_FILE)
    assert (p.label, p.panel_side, p.divider) == ("ALPHA", "Left", True)
    assert (p.show_title, p.title_text) == (True, "Alpha: The Return")
    assert (p.window_border, p.button_style) == (False, "Minimal")
    assert (p.menu, p.icon_is_ico, p.linux_info, p.legacy_fs) == (True, True, True, False)
    assert p.buttons == ["Play", "Exit"]
    assert p.entries[0].folder == str(src / "Alpha")
    assert p.entries[0].setup.endswith("setup_alpha_1.0.exe")
    assert p.out_dir == str(tmp_path)


def test_reads_a_real_windows_file(src):
    p = read_project(WINDOWS_V8)
    assert (p.schema, p.app_version) == (8, "0.7.1")
    assert p.label == "ALAN WAKE" and p.menu and p.linux_info
    assert len(p.entries) == 1
    e = p.entries[0]
    assert e.kind == "Game" and e.parent_index == -1
    assert e.name == "Alan Wake" and e.match_name == "Alan Wake"
    assert e.folder.startswith("F:\\DWdemo")


def test_reads_a_file_three_schemas_older(src):
    # Schema 5: no MatchName, no LinuxInfo, no LegacyFs. Each missing key reads
    # back as what that disc was, not as today's default.
    p = read_project(WINDOWS_V5)
    assert p.schema == 5 and p.app_version == "0.4.2"
    assert p.entries[0].match_name is None
    assert p.linux_info is False and p.legacy_fs is False


def test_reads_the_first_schema_which_knew_one_game(tmp_path):
    # Written by hand, the way the Windows suite writes it: a round-trip of our
    # own output would only prove we can read what we just wrote.
    old = {"Version": 1, "SavedUtc": "2026-08-16T20:30:55",
           "SourceFolder": "/games/Game Two", "GameName": "Game Two", "Label": "Game Two",
           "IconPath": "/art/two.png", "IconIsIco": False, "Menu": True, "BgPath": "/art/bg.jpg",
           "BgAsIs": False, "PanelSide": "Left", "Divider": True, "ShowTitle": True,
           "TitleText": "G2", "WindowBorder": False, "ButtonStyle": "Bordered",
           "MusicFile": None, "Buttons": ["Play", "Exit"], "ManualPath": None,
           "ExtrasPath": None, "ExtraItems": [], "OutDir": str(tmp_path)}
    (tmp_path / PROJECT_FILE).write_text(json.dumps(old), encoding="utf-8")
    p = read_project(tmp_path / PROJECT_FILE)
    assert len(p.entries) == 1
    assert p.entries[0].folder == "/games/Game Two" and p.entries[0].kind == "Game"
    assert (p.panel_side, p.divider, p.button_style) == ("Left", True, "Bordered")
    assert p.media_key == "" and p.linux_info is False


def test_reads_the_second_schema_which_had_no_kinds(tmp_path):
    old = {"Version": 2, "Games": [{"Folder": "/games/One"}, {"Folder": "/games/Two"}],
           "Label": "TWO GAMES", "Menu": True, "OutDir": str(tmp_path)}
    (tmp_path / PROJECT_FILE).write_text(json.dumps(old), encoding="utf-8")
    p = read_project(tmp_path / PROJECT_FILE)
    assert [e.kind for e in p.entries] == ["Game", "Game"]
    assert all(e.parent_index == -1 for e in p.entries)
    # No ButtonStyle in the file, and those discs had bordered buttons.
    assert p.button_style == "Bordered" and p.panel_side == "Right"


@pytest.mark.parametrize("text", ["", "not json at all", "[1, 2, 3]"])
def test_refuses_a_file_that_is_not_a_project(tmp_path, text):
    (tmp_path / PROJECT_FILE).write_text(text, encoding="utf-8")
    assert read_project(tmp_path / PROJECT_FILE) is None


def test_refuses_a_file_that_is_not_there(tmp_path):
    assert read_project(tmp_path / "nowhere.json") is None


# ---- settings from a project -----------------------------------------------------------

def test_reads_the_games_off_disk_again(src, tmp_path):
    save_project(settings(src, tmp_path), tmp_path)
    s, problems = settings_from_project(read_project(tmp_path / PROJECT_FILE))
    assert problems == []
    assert len(s.games) == 1 and s.games[0].ok
    assert s.games[0].setup_exe.name == "setup_alpha_1.0.exe"
    assert s.label == "ALPHA" and s.icon_is_ico and s.buttons == ["Play", "Exit"]


def test_keeps_the_name_that_was_edited_and_the_name_that_matches(src, tmp_path):
    # Two fields on purpose: the name on the menu can be edited, while the menu
    # searches the registry for whatever GOG registered. Sharing one made a
    # rename stop Play finding the installed game.
    game = game_info(src / "Alpha")
    game.game_name = "Alpha: The Director's Cut"
    game.match_name = "Alpha The Game GOG Registered"
    save_project(settings(src, tmp_path, games=[game]), tmp_path)
    s, _ = settings_from_project(read_project(tmp_path / PROJECT_FILE))
    assert s.games[0].game_name == "Alpha: The Director's Cut"
    assert s.games[0].match_name == "Alpha The Game GOG Registered"


def test_brings_an_add_on_back_as_an_add_on(src, tmp_path):
    from discwright.games import add_on_info
    patch = add_on_info(sparse(src / "Alpha" / "patch_alpha_1.0_to_1.1.exe", MB))
    patch.parent_index = 0
    save_project(settings(src, tmp_path, games=[game_info(src / "Alpha"), patch]), tmp_path)
    s, problems = settings_from_project(read_project(tmp_path / PROJECT_FILE))
    assert problems == []
    assert [g.kind for g in s.games] == ["Game", "AddOn"]
    assert s.games[1].parent_index == 0
    # The add-on is read from its own installer, not from the folder it shares
    # with the game, which would find the game again.
    assert s.games[1].setup_exe.name == "patch_alpha_1.0_to_1.1.exe"


def test_says_which_game_has_gone(src, tmp_path):
    save_project(settings(src, tmp_path), tmp_path)
    import shutil
    shutil.rmtree(src / "Alpha")
    s, problems = settings_from_project(read_project(tmp_path / PROJECT_FILE))
    assert len(problems) == 1 and "Alpha" in problems[0]
    assert s.games == []


def test_a_windows_project_names_files_this_machine_has_not_got(src):
    s, problems = settings_from_project(read_project(WINDOWS_V8))
    assert problems and "DWdemo" in problems[0]
