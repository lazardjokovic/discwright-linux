import shutil
import subprocess
import sys
from pathlib import Path

import pytest

from discwright import __version__
from discwright import cli as cli_module
from discwright.cli import main
from discwright.iso import IsoError

from test_stage import BACKGROUND, ICONS, MB, sparse, src, write  # noqa: F401  (src is a fixture)

needs_xorriso = pytest.mark.skipif(shutil.which("xorriso") is None, reason="xorriso is not installed")


def args(src, out, *extra):
    """A build of the fixture disc, with everything the window would need."""
    return ["build", "--game", str(src / "Alpha"), "--icon", str(src / "art" / "alpha.ico"),
            "--background", str(src / "art" / "alpha-bg.png"), "--out", str(out), *extra]


@pytest.fixture
def caught(monkeypatch):
    """The settings the command would build, without building anything."""
    seen = {}

    def fake(s, log=print, progress=None):
        seen["settings"] = s
        return s.out_dir / "x.iso"
    monkeypatch.setattr(cli_module, "build", fake)
    return seen


def test_reports_its_version(capsys):
    with pytest.raises(SystemExit) as done:
        main(["--version"])
    assert done.value.code == 0
    assert __version__ in capsys.readouterr().out


def test_shows_what_it_can_do_when_asked_for_nothing(capsys):
    assert main([]) == 0
    assert "build" in capsys.readouterr().out


# ---- what it refuses, before anything is copied ----------------------------------------

def test_refuses_a_build_with_no_game(src, tmp_path, capsys):
    assert main(["build", "--icon", str(src / "art" / "alpha.ico"), "--out", str(tmp_path)]) == 1
    assert "--game" in capsys.readouterr().err


def test_refuses_a_folder_that_holds_no_installer(src, tmp_path, capsys):
    assert main(["build", "--game", str(src / "art"), "--icon", str(src / "art" / "alpha.ico"),
                 "--out", str(tmp_path)]) == 1
    assert "no" in capsys.readouterr().err.casefold()


def test_refuses_a_build_with_no_icon(src, tmp_path, capsys):
    assert main(["build", "--game", str(src / "Alpha"), "--out", str(tmp_path)]) == 1
    assert "--icon" in capsys.readouterr().err


def test_refuses_a_build_with_nowhere_to_put_it(src, tmp_path, capsys):
    assert main(["build", "--game", str(src / "Alpha"),
                 "--icon", str(src / "art" / "alpha.ico")]) == 1
    assert "--out" in capsys.readouterr().err


def test_refuses_a_menu_with_no_background(src, tmp_path, capsys):
    out = tmp_path / "out"
    assert main(["build", "--game", str(src / "Alpha"), "--icon", str(src / "art" / "alpha.ico"),
                 "--out", str(out)]) == 1
    assert "background" in capsys.readouterr().err
    assert not (out / "disc").exists()


def test_refuses_an_add_on_with_no_game_before_it(src, tmp_path, capsys):
    patch = sparse(src / "Alpha" / "patch_alpha_1.0_to_1.1.exe", MB)
    assert main(["build", "--add-on", str(patch), "--game", str(src / "Alpha"),
                 "--icon", str(src / "art" / "alpha.ico"), "--out", str(tmp_path)]) == 1
    assert "name a --game before it" in capsys.readouterr().err


def test_refuses_a_button_that_is_not_one(src, tmp_path, capsys):
    with pytest.raises(SystemExit) as done:
        main(args(src, tmp_path / "out", "--buttons", "play,teleport"))
    assert done.value.code == 2
    assert "teleport" in capsys.readouterr().err


def test_says_what_the_build_said_when_it_stops(src, tmp_path, capsys, monkeypatch):
    def fail(*a, **k):
        raise IsoError("xorriso is not installed, and it is what writes the ISO.")
    monkeypatch.setattr(cli_module, "build", fail)
    assert main(args(src, tmp_path / "out")) == 1
    assert "xorriso is not installed" in capsys.readouterr().err


# ---- the settings the options make -----------------------------------------------------

def test_takes_the_label_from_the_first_game_unless_told(src, tmp_path, caught, capsys):
    assert main(args(src, tmp_path / "out")) == 0
    assert caught["settings"].label == "alpha"
    assert "--label sets your own" in capsys.readouterr().out
    assert main(args(src, tmp_path / "out", "--label", "ALPHA DISC")) == 0
    assert caught["settings"].label == "ALPHA DISC"


def test_files_an_add_on_under_the_game_named_before_it(src, tmp_path, caught):
    sparse(src / "Beta" / "setup_beta_2.0.exe", MB)
    patch = sparse(src / "Alpha" / "patch_alpha_1.0_to_1.1.exe", MB)
    assert main(["build", "--game", str(src / "Alpha"), "--add-on", str(patch),
                 "--game", str(src / "Beta"), "--icon", str(src / "art" / "alpha.ico"),
                 "--background", str(src / "art" / "alpha-bg.png"), "--out", str(tmp_path)]) == 0
    games = caught["settings"].games
    assert [g.kind for g in games] == ["Game", "AddOn", "Game"]
    assert games[1].parent_index == 0


def test_passes_the_menu_options_through(src, tmp_path, caught):
    assert main(args(src, tmp_path / "out", "--panel-side", "left", "--divider",
                     "--title", "Alpha: The Return", "--buttons", "Play, exit",
                     "--bordered-buttons", "--no-window-border", "--background-as-is")) == 0
    s = caught["settings"]
    assert (s.panel_side, s.divider, s.bg_as_is) == ("Left", True, True)
    assert (s.show_title, s.title_text) == (True, "Alpha: The Return")
    assert s.buttons == ["Play", "Exit"]
    assert (s.button_style, s.window_border) == ("Bordered", False)


def test_a_bare_title_means_the_label(src, tmp_path, caught):
    assert main(args(src, tmp_path / "out", "--label", "ALPHA DISC", "--title")) == 0
    s = caught["settings"]
    assert s.show_title and s.title_text == ""


def test_leaves_the_title_off_unless_asked(src, tmp_path, caught):
    assert main(args(src, tmp_path / "out")) == 0
    assert caught["settings"].show_title is False


def test_passes_the_extra_content_through(src, tmp_path, caught):
    loose = write(src / "loose" / "readme.txt", "HI")
    assert main(args(src, tmp_path / "out", "--manual", str(src / "media" / "Alpha Manual.pdf"),
                     "--extras", str(src / "media" / "Extras"),
                     "--extra", str(loose), "--extra", str(src / "media" / "Extras"))) == 0
    s = caught["settings"]
    assert s.manual_path.name == "Alpha Manual.pdf" and s.extras_path.name == "Extras"
    assert [p.name for p in s.extra_items] == ["readme.txt", "Extras"]


def test_knows_an_ico_from_a_picture(src, tmp_path, caught):
    assert main(args(src, tmp_path / "out")) == 0
    assert caught["settings"].icon_is_ico is True
    assert main(["build", "--game", str(src / "Alpha"), "--icon", str(ICONS / "source.png"),
                 "--background", str(BACKGROUND / "wide.png"), "--out", str(tmp_path)]) == 0
    assert caught["settings"].icon_is_ico is False


def test_no_menu_and_no_linux_name(src, tmp_path, caught):
    assert main(args(src, tmp_path / "out", "--no-menu", "--no-linux-name")) == 0
    s = caught["settings"]
    assert s.menu is False and s.linux_info is False


def test_names_the_disc_for_linux_unless_told_not_to(src, tmp_path, caught):
    assert main(args(src, tmp_path / "out")) == 0
    assert caught["settings"].linux_info is True


# ---- a real disc, built by the command -------------------------------------------------

def test_stages_the_disc_without_writing_an_iso(src, tmp_path, capsys):
    out = tmp_path / "out"
    assert main(args(src, out, "--stage-only")) == 0
    assert (out / "disc" / "autorun.inf").is_file()
    assert list(out.glob("*.iso")) == []
    assert "disc folder is" in capsys.readouterr().out


@needs_xorriso
def test_builds_a_disc_end_to_end(src, tmp_path, capsys):
    out = tmp_path / "out"
    assert main(args(src, out, "--label", "ALPHA DISC",
                     "--manual", str(src / "media" / "Alpha Manual.pdf"))) == 0
    printed = capsys.readouterr().out
    assert (out / "ALPHA DISC.iso").is_file()
    assert "game: alpha" in printed and "100.0%" in printed and "DONE." in printed


@needs_xorriso
def test_says_nothing_when_told_to_be_quiet(src, tmp_path, capsys):
    assert main(args(src, tmp_path / "out", "--quiet")) == 0
    assert capsys.readouterr().out == ""


def test_runs_as_a_command(src, tmp_path):
    # The entry point pyproject installs, run the way a person runs it.
    run = subprocess.run([sys.executable, "-m", "discwright.cli", "build",
                          "--game", str(src / "Alpha"), "--icon", str(src / "art" / "alpha.ico"),
                          "--background", str(src / "art" / "alpha-bg.png"),
                          "--out", str(tmp_path / "out"), "--stage-only"],
                         capture_output=True, text=True)
    assert run.returncode == 0, run.stderr
    assert (tmp_path / "out" / "disc" / "autorun.inf").is_file()


# ---- rebuilding a saved disc ------------------------------------------------------------

def saved_project(src, out) -> Path:
    """A project as a build would have left it, without building."""
    from discwright.games import game_info
    from discwright.project import save_project
    from discwright.settings import DiscSettings
    out.mkdir(parents=True, exist_ok=True)
    return save_project(DiscSettings(
        games=[game_info(src / "Alpha")], label="ALPHA DISC", out_dir=out,
        icon_path=src / "art" / "alpha.ico", icon_is_ico=True, menu=True,
        bg_path=src / "art" / "alpha-bg.png", buttons=["Play", "Exit"], linux_info=True), out)


def test_rebuilds_the_disc_a_project_describes(src, tmp_path, capsys):
    out = tmp_path / "out"
    project = saved_project(src, out)
    assert main(["build", "--project", str(project), "--stage-only"]) == 0
    assert (out / "disc" / "AUTORUN" / "menu.hta").is_file()
    printed = capsys.readouterr().out
    assert "schema 8" in printed and "game: alpha" in printed


def test_puts_a_rebuilt_disc_where_told(src, tmp_path):
    project = saved_project(src, tmp_path / "out")
    elsewhere = tmp_path / "elsewhere"
    assert main(["build", "--project", str(project), "--out", str(elsewhere), "--stage-only"]) == 0
    assert (elsewhere / "disc" / "autorun.inf").is_file()


def test_refuses_a_project_alongside_the_options_it_already_holds(src, tmp_path, capsys):
    project = saved_project(src, tmp_path / "out")
    assert main(["build", "--project", str(project), "--game", str(src / "Alpha")]) == 1
    assert "cannot be given with --project" in capsys.readouterr().err


def test_refuses_something_that_is_not_a_project(tmp_path, capsys):
    bad = tmp_path / "notes.txt"
    bad.write_text("not a project")
    assert main(["build", "--project", str(bad)]) == 1
    assert "not a disc project file" in capsys.readouterr().err


def test_says_which_file_a_project_cannot_find(src, tmp_path, capsys):
    import shutil
    project = saved_project(src, tmp_path / "out")
    shutil.rmtree(src / "Alpha")
    assert main(["build", "--project", str(project), "--stage-only"]) == 1
    err = capsys.readouterr().err
    assert "Alpha" in err and "cannot find" in err


# ---- the window ----------------------------------------------------------------------

def test_says_how_to_get_the_window_when_gtk_is_missing(monkeypatch, capsys):
    # None in sys.modules makes the import fail, the way it does on a machine
    # without GTK or PyGObject.
    monkeypatch.setitem(sys.modules, "discwright.window", None)
    assert main(["window"]) == 1
    err = capsys.readouterr().err
    assert "gir1.2-gtk-4.0" in err and "discwright build" in err
