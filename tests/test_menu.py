import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path, PureWindowsPath

import pytest

from discwright.menu import html_text, js_string, menu_hta

FIX = Path(__file__).parent / "fixtures" / "menu"
WINDOWS = FIX / "windows-0.7.4"
CASES = {c["name"]: c["cfg"] for c in json.loads((FIX / "cases.json").read_text(encoding="ascii"))["cases"]}


def build(cfg: dict) -> bytes:
    """menu_hta called with what New-MenuHta was given for the same case."""
    games = [{"name": g["Name"], "match_name": g["MatchName"], "setup": g["Setup"],
              "manual": g["Manual"], "extras": g["Extras"],
              "add_ons": [{"name": a["Name"], "setup": a["Setup"]} for a in g["AddOns"]]}
             for g in cfg["Games"]]
    return menu_hta(cfg["GameName"], games, cfg["Buttons"],
                    music_file=cfg["MusicFile"], manual_file=cfg["ManualFile"],
                    panel_side=cfg["PanelSide"], icon_name=cfg["IconName"],
                    window_border=cfg["WindowBorder"], button_style=cfg["ButtonStyle"])


def digest(data: bytes) -> str:
    # Compared as checksums: pytest's diff of two whole menus never finishes on CI.
    return hashlib.sha256(data).hexdigest()


def script_of(hta: bytes) -> str:
    text = hta.decode("ascii")
    return text[text.index('<script language="JScript">') + 27:text.index("</script>")]


@pytest.mark.parametrize("name", sorted(CASES))
def test_writes_the_menu_windows_writes(name):
    ours = build(CASES[name])
    theirs = (WINDOWS / f"{name}.hta").read_bytes()
    assert len(ours) == len(theirs)
    assert digest(ours) == digest(theirs)


def test_is_ascii_with_crlf_line_ends_like_every_windows_menu():
    hta = build(CASES["one-game"])
    assert hta.endswith(b"</html>\r\n")
    assert b"\n" not in hta.replace(b"\r\n", b"")
    assert max(hta) < 0x80


@pytest.mark.skipif(shutil.which("node") is None, reason="needs Node to parse the menu's script")
@pytest.mark.parametrize("name", sorted(CASES))
def test_the_menus_script_parses(tmp_path, name):
    # The menu runs as JScript under mshta, which nothing on Linux has. Node
    # parses the same ES3; a name that broke out of its string literal fails
    # here the way it would fail on Windows.
    js = tmp_path / "menu.js"
    js.write_text(script_of(build(CASES[name])), encoding="ascii")
    run = subprocess.run(["node", "--check", str(js)], capture_output=True, text=True)
    assert run.returncode == 0, run.stderr


# ---- where this differs from Windows, on purpose ------------------------------------

def placeholder_menu() -> bytes:
    games = [{"name": "Game %%BTNS%% Edition", "match_name": "%%TITLE%%", "setup": "setup.exe",
              "manual": "", "extras": "", "add_ons": []}]
    return menu_hta("%%GAMES%%", games, ["Play", "Exit"], icon_name="X.ico")


def test_fills_in_each_placeholder_once():
    # Windows chains its replaces, so a name holding a placeholder is filled in
    # again: "Game %%BTNS%% Edition" gets the button list pasted inside its
    # string literal, and the whole menu stops compiling. Measured under cscript.
    text = placeholder_menu().decode("ascii")
    assert 'var GAMES=[{n:"Game %%BTNS%% Edition",m:"%%TITLE%%"' in text
    assert 'var BTNS=["Play","Exit"];' in text
    assert "<title>%%GAMES%%</title>" in text


@pytest.mark.skipif(shutil.which("node") is None, reason="needs Node to parse the menu's script")
def test_a_name_holding_a_placeholder_leaves_the_script_parsing(tmp_path):
    js = tmp_path / "menu.js"
    js.write_text(script_of(placeholder_menu()), encoding="ascii")
    run = subprocess.run(["node", "--check", str(js)], capture_output=True, text=True)
    assert run.returncode == 0, run.stderr


# ---- the escapes, one rule at a time ---------------------------------------------------

@pytest.mark.parametrize("raw,escaped", [
    ('say "hi"', 'say \\"hi\\"'),
    ("C:\\Games", "C:\\\\Games"),
    ("</script>", "\\x3c/script\\x3e"),
    ("line\r\nbreak\x07", "linebreak"),
    ("Caf\u00e9\u2122", "Caf\\u00e9\\u2122"),
    ("\U0001F3AE", "\\ud83c\\udfae"),
    ("", ""),
    (None, ""),
])
def test_escapes_a_js_string(raw, escaped):
    assert js_string(raw) == escaped


@pytest.mark.parametrize("raw,escaped", [
    ('Tom & "Jerry" <3', "Tom &amp; &quot;Jerry&quot; &lt;3"),
    ("Caf\u00e9\u2122", "Caf&#233;&#8482;"),
    ("\U0001F3AE", "&#127918;"),
    ("tab\there", "tabhere"),
    (None, ""),
])
def test_escapes_html_text(raw, escaped):
    assert html_text(raw) == escaped


def test_names_the_menu_window_after_the_disc_alone():
    # singleinstance keys off it: two discs sharing one name would refocus each
    # other's menu instead of opening their own.
    text = menu_hta("Alan Wake: The Signal!", [], ["Exit"]).decode("ascii")
    assert 'applicationname="DiscMenu_AlanWakeTheSignal"' in text


def test_falls_back_to_the_games_name_when_it_has_no_match_name():
    games = [{"name": "Solo", "match_name": "", "setup": "s.exe", "add_ons": []}]
    assert 'm:"Solo"' in menu_hta("S", games, ["Play"]).decode("ascii")


@pytest.mark.parametrize("side,left", [("Right", 490), ("Left", 20), ("left", 20)])
def test_puts_the_buttons_on_the_side_asked_for(side, left):
    text = menu_hta("S", [], ["Exit"], panel_side=side).decode("ascii")
    assert f".panel{{position:absolute;left:{left}px;" in text


def test_leaves_no_placeholder_unfilled():
    assert not re.search(rb"%%[A-Z]+%%", build(CASES["two-games-with-add-ons"]))


def test_paths_reach_the_menu_with_windows_separators():
    # The menu runs on Windows and joins these to the drive's root.
    text = build(CASES["two-games-with-add-ons"]).decode("ascii")
    setup = CASES["two-games-with-add-ons"]["Games"][0]["Setup"]
    assert js_string(setup) in text
    assert "\\\\" in js_string(str(PureWindowsPath("Games", "a.exe")))
