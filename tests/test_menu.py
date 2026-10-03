import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path, PureWindowsPath

import pytest

from discwright.menu import html_text, js_string, menu_hta

FIX = Path(__file__).parent / "fixtures" / "menu"
# Menus the Windows app itself wrote, at the version the port is level with.
# The old sets are kept beside it: the diff between two of them is the record
# of what changed in the menu, and 0.10.0 changed a good deal. A folder of
# game files with no installer is offered Open Folder rather than a Play
# button that could never work, a long name wraps instead of being cut at
# twenty characters, the menu works out the disc from where it actually sits,
# and the name above the buttons can be turned off.
WINDOWS = FIX / "windows-0.10.0"
CASES = {c["name"]: c["cfg"] for c in json.loads((FIX / "cases.json").read_text(encoding="ascii"))["cases"]}


def build(cfg: dict) -> bytes:
    """menu_hta called with what New-MenuHta was given for the same case."""
    # Every field New-MenuHta reads has to come through here, or the port is
    # compared against a menu built from more than it was given. Source was
    # missed when it was added, and the two menus came out the same length with
    # different bytes: files:0 against files:1.
    games = [{"name": g["Name"], "match_name": g["MatchName"], "setup": g["Setup"],
              "folder": g["Folder"], "manual": g["Manual"], "extras": g["Extras"],
              "source": g.get("Source", "GOG"),
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


# ---- a game with no installer ---------------------------------------------------

def test_carries_the_folder_each_game_sits_in():
    # The menu needs it for two decisions: which button a game gets, and whether
    # a game with no installer is on the disc at all.
    folder = str(PureWindowsPath("Games", "02 - Portable"))
    games = [{"name": "Portable", "match_name": "Portable", "setup": "",
              "folder": folder, "add_ons": []}]
    text = menu_hta("S", games, ["Play"]).decode("ascii")
    # Doubled in the JS literal, because the menu joins it to the drive's root.
    assert 'd:"' + js_string(folder) + '"' in text


def test_a_game_with_no_installer_gets_open_folder_instead_of_install():
    text = build(CASES["a-folder-of-files"]).decode("ascii")
    # Both buttons are in the script, one per branch of the same if.
    assert 'btnHtml("btn_Open","install","Open Folder","doOpenFolder()"' in text
    assert "function doOpenFolder()" in text
    # And the entry that has no installer says so with an empty path, which is
    # what that branch tests. Its folder comes straight from the case.
    portable = CASES["a-folder-of-files"]["Games"][1]
    assert portable["Setup"] == ""
    assert 's:"",d:"' + js_string(portable["Folder"]) + '"' in text


def test_the_chooser_asks_for_the_folder_when_there_is_no_installer():
    # Asking FileExists about an empty path greyed out every such game on the
    # chooser, which is a game nobody could reach.
    text = build(CASES["a-folder-of-files"]).decode("ascii")
    assert "GAMES[i].s ? fso.FileExists" in text
    assert "fso.FolderExists(fso.BuildPath(root,GAMES[i].d))" in text


# ---- the files flag, which decides Play from disc ------------------------------------

def test_writes_the_flag_the_menu_reads():
    hta = menu_hta("DISC", [{"name": "Gothic", "match_name": "Gothic",
                             "setup": "Games/01 - Gothic/gothic.exe", "folder": "Games/01 - Gothic",
                             "manual": "", "extras": "", "source": "Files", "add_ons": []}],
                   ["Play", "Install", "Exit"])
    assert b"files:1" in hta
    assert b"files:0" not in hta


def test_calls_a_gog_download_an_installer():
    hta = menu_hta("DISC", [{"name": "Alan Wake", "match_name": "Alan Wake",
                             "setup": "setup_alan_wake.exe", "folder": "",
                             "manual": "", "extras": "", "source": "GOG", "add_ons": []}],
                   ["Play", "Install", "Exit"])
    assert b"files:0" in hta
    assert b"files:1" not in hta


def test_an_entry_with_no_source_is_a_gog_download():
    # Nothing should reach here without one, and if it does the old behaviour is
    # the safe answer: that is what every disc built before Source existed was.
    hta = menu_hta("DISC", [{"name": "Alpha", "match_name": "Alpha", "setup": "setup.exe",
                             "folder": "", "manual": "", "extras": "", "add_ons": []}],
                   ["Play", "Install", "Exit"])
    assert b"files:0" in hta


def test_the_template_plays_a_files_entry_from_the_disc():
    # Lifted verbatim from the Windows app by tools/menu_template.py, so this
    # checks the lift happened rather than that the port invented anything.
    tpl = (Path(__file__).parent.parent / "src" / "discwright" / "menu.hta.in").read_text(encoding="ascii")
    assert "Play from disc" in tpl
    assert 'if(has("Install") && !g.files){' in tpl
    assert 'setEnabled("btn_Play", (g.files ? true : parentOn)' in tpl
    # And a GOG disc still says what it always said.
    assert "isn't installed yet" in tpl


# ---- what the menu does with an entry that has nothing to run ------------------------

def eval_js(tmp_path, source: str) -> str:
    """Run a snippet of the menu's own script and hand back what it printed.

    Node rather than cscript: this suite runs on Linux, where nothing has mshta.
    The menu is ES3 and Node parses it unchanged, which is already how
    test_the_menus_script_parses works.
    """
    js = tmp_path / "probe.js"
    js.write_text(source, encoding="ascii")
    run = subprocess.run(["node", str(js)], capture_output=True, text=True)
    assert run.returncode == 0, run.stderr
    return run.stdout.strip()


def function_of(script: str, name: str) -> str:
    """One top-level function out of the menu's script, by name."""
    m = re.search(r"(?sm)^(  function " + re.escape(name) + r"\(.*?\n  \})", script)
    assert m, f"{name} was not found in the menu's script"
    return m.group(1)


@pytest.mark.skipif(shutil.which("node") is None, reason="needs Node to run the menu's script")
def test_a_folder_with_no_installer_is_offered_the_folder(tmp_path):
    # The bug this answers, found on Windows and sitting here untouched: a game
    # added with "no installer" was given a Play button that could never work,
    # because doPlay builds its path from an empty setup and lands on the disc
    # root, which is a folder rather than a file. The Open Folder button meant
    # to replace it was written behind !g.files, so the one kind of entry that
    # can be given no installer never saw it.
    script = script_of(build(CASES["a-folder-with-no-installer"]))
    out = eval_js(tmp_path, """
var GAMES=[{ n:"Arcanum", files:1, s:"", d:"Games/01 - Arcanum", a:[], m:"Arcanum" }];
var cur=0, CAPTURED="";
function has(b){ return ",Play,Install,Exit,".indexOf(","+b+",") >= 0; }
function setPanel(h,cap){ CAPTURED=h; }
function capFor(n){ return ""; }
function btnHtml(id,cls,label,fn,tip){ return "|"+label; }
""" + function_of(script, "renderGame") + """
renderGame();
console.log(CAPTURED);
""")
    buttons = [b for b in out.split("|") if b]
    assert "Open Folder" in buttons
    assert "Play from disc" not in buttons
    assert "Play" not in buttons


@pytest.mark.skipif(shutil.which("node") is None, reason="needs Node to run the menu's script")
def test_a_folder_with_an_executable_still_plays_from_the_disc(tmp_path):
    # The case that was already right, kept that way.
    script = script_of(build(CASES["a-game-played-from-disc"]))
    out = eval_js(tmp_path, """
var GAMES=[{ n:"Gothic", files:1, s:"Games/01 - Gothic/gothic.exe", d:"Games/01 - Gothic", a:[], m:"Gothic" }];
var cur=0, CAPTURED="";
function has(b){ return ",Play,Install,Exit,".indexOf(","+b+",") >= 0; }
function setPanel(h,cap){ CAPTURED=h; }
function capFor(n){ return ""; }
function btnHtml(id,cls,label,fn,tip){ return "|"+label; }
""" + function_of(script, "renderGame") + """
renderGame();
console.log(CAPTURED);
""")
    buttons = [b for b in out.split("|") if b]
    assert "Play from disc" in buttons
    assert "Open Folder" not in buttons


@pytest.mark.skipif(shutil.which("node") is None, reason="needs Node to run the menu's script")
def test_a_long_name_wraps_rather_than_losing_its_tail(tmp_path):
    # Cut at twenty characters before 0.10.0, so "The Witcher 3 Wild Hunt -
    # Game of the Year Edition" reached the disc as "THE WITCHER 3 WILD ...".
    script = script_of(build(CASES["one-game"]))
    out = eval_js(tmp_path,
                  function_of(script, "fitStyle") + "\n" +
                  re.search(r"(?m)^  (function clip\(s\).*)$", script).group(1) + """
var w = "The Witcher 3 Wild Hunt - Game of the Year Edition";
console.log(clip(w) === w);
console.log(fitStyle(w));
console.log(clip("Hollow Knight") === "Hollow Knight");
console.log(clip(new Array(40).join("ab")).length <= 64);
""")
    survives, size, short, capped = out.splitlines()
    assert survives == "true", "fifty characters should survive whole"
    assert "12px" in size, "a name that long should step down a size"
    assert short == "true", "a short name is untouched"
    assert capped == "true", "and something past two lines is still cut, ellipsis included"


def test_the_menu_lets_a_name_wrap_at_all():
    # Both of these made wrapping impossible by construction: the buttons were
    # nowrap, and every space in a label was replaced with a non-breaking one.
    script = script_of(build(CASES["one-game"]))
    assert "white-space:nowrap" not in build(CASES["one-game"]).decode("ascii")
    assert 'replace(/ /g,"&nbsp;")' not in script
    assert "offsetHeight/16" in script, "the line count is measured, not counted"
