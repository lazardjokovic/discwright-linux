"""Staging a folder of game files, the kind GOG never packaged.

Ported from the Windows suite's "Building a disc from a folder of game files".
A GOG download is an installer and its numbered parts in one flat folder, so it
has no shape to lose. A folder of game files has one, and a game whose data/
folder was flattened onto the disc root is a broken game.
"""

from pathlib import Path

import pytest

from discwright.games import folder_info
from discwright.settings import DiscSettings
from discwright.stage import stage

from test_stage import ICONS, BACKGROUND, MB, digest, quiet, sparse, src, write  # noqa: F401


@pytest.fixture
def portable(tmp_path):
    """A game as it sits on a disk: an exe, data in subfolders, its own icons."""
    d = tmp_path / "src" / "Portable Game"
    sparse(d / "PortableGame.exe", 2 * MB)
    write(d / "readme.txt", "read me")
    write(d / "data" / "config.ini", "x=1")
    write(d / "data" / "textures" / "wall.dds", "dds")
    write(d / "tools" / "editor" / "edit.cfg", "cfg")
    return d


def settings_for(games, src, out, **kw):
    base = dict(games=games, label="PORTABLE", out_dir=out,
                icon_path=src / "art" / "alpha.ico", icon_is_ico=True, menu=True,
                bg_path=src / "art" / "alpha-bg.png",
                buttons=["Play", "Install", "Exit"])
    base.update(kw)
    return DiscSettings(**base)


def staged_files(stage_dir: Path, skip=()) -> set[str]:
    """Everything on the disc as posix-style relative paths, less the disc's own."""
    out = set()
    for p in stage_dir.rglob("*"):
        if p.is_file():
            rel = p.relative_to(stage_dir).as_posix()
            if not rel.startswith("AUTORUN/") and rel not in skip:
                out.add(rel)
    return out


def test_keeps_the_subfolders_the_game_expects(portable, src, tmp_path):
    st, _ = stage(settings_for([folder_info(portable)], src, tmp_path / "out"), quiet)
    for rel in ("PortableGame.exe", "readme.txt", "data/config.ini",
                "data/textures/wall.dds", "tools/editor/edit.cfg"):
        assert (st / rel).is_file(), rel


def test_puts_nothing_where_a_flat_copy_would_have_put_it(portable, src, tmp_path):
    st, _ = stage(settings_for([folder_info(portable)], src, tmp_path / "out"), quiet)
    assert not (st / "wall.dds").exists()
    assert not (st / "config.ini").exists()


def test_carries_every_file_and_nothing_else(portable, src, tmp_path):
    st, _ = stage(settings_for([folder_info(portable)], src, tmp_path / "out"), quiet)
    theirs = {p.relative_to(portable).as_posix() for p in portable.rglob("*") if p.is_file()}
    disc_own = {"PORTABLE.ico", "autorun.inf"}
    assert staged_files(st, skip=disc_own) == theirs


def test_leaves_the_games_own_icons_on_the_disc(portable, src, tmp_path):
    # The sweep that clears an earlier build's icon off the root matches on name,
    # and a real game folder is full of icons: Hollow Knight carries gog.ico,
    # support.ico and its own goggame-*.ico. Windows DiscWright deleted all three
    # from the disc, straight after copying them there, until this rule existed.
    write(portable / "gog.ico", "ICON")
    write(portable / "support.ico", "ICON")
    write(portable / "screenshot.png", "PNG")
    st, _ = stage(settings_for([folder_info(portable)], src, tmp_path / "out",
                               linux_info=True), quiet)
    assert (st / "gog.ico").is_file()
    assert (st / "support.ico").is_file()
    assert (st / "screenshot.png").is_file()
    # And the disc's own pair is still there, which is what the sweep protects.
    assert (st / "PORTABLE.ico").is_file()
    assert (st / "PORTABLE.png").is_file()


def test_still_clears_an_earlier_builds_icon(portable, src, tmp_path):
    # The guard is about the game's files, not about giving up on the sweep: a
    # rebuild under a new label still takes the old icon away.
    out = tmp_path / "out"
    stage(settings_for([folder_info(portable)], src, out, label="OLD NAME"), quiet)
    st, _ = stage(settings_for([folder_info(portable)], src, out, label="PORTABLE"), quiet)
    assert (st / "PORTABLE.ico").is_file()
    assert not (st / "OLDNAME.ico").exists()


def test_files_go_under_the_games_folder_on_a_two_game_disc(portable, src, tmp_path):
    entries = [folder_info(portable), folder_info(portable.parent / "Other")]
    write(portable.parent / "Other" / "other.txt", "other")
    entries[1] = folder_info(portable.parent / "Other")
    st, _ = stage(settings_for(entries, src, tmp_path / "out"), quiet)
    assert (st / "Games" / "01 - Portable Game" / "data" / "config.ini").is_file()
    assert (st / "Games" / "02 - Other" / "other.txt").is_file()
    assert not (st / "data").exists()


def test_the_menu_gets_the_folder_instead_of_an_installer(portable, src, tmp_path):
    st, _ = stage(settings_for([folder_info(portable)], src, tmp_path / "out"), quiet)
    hta = (st / "AUTORUN" / "menu.hta").read_text(encoding="ascii")
    assert 's:"",d:""' in hta          # one game: its folder is the disc root
    assert "doOpenFolder()" in hta


def test_an_installer_named_for_it_still_reaches_the_menu(portable, src, tmp_path):
    g = folder_info(portable, portable / "PortableGame.exe")
    st, _ = stage(settings_for([g], src, tmp_path / "out"), quiet)
    hta = (st / "AUTORUN" / "menu.hta").read_text(encoding="ascii")
    assert 's:"PortableGame.exe"' in hta


def test_rebuilds_over_itself_without_losing_the_files(portable, src, tmp_path):
    # The second build reads out of the folder the first one wrote, which is set
    # aside rather than wiped. A folder of files has to come back with its shape.
    out = tmp_path / "out"
    stage(settings_for([folder_info(portable)], src, out), quiet)
    st, aside = stage(settings_for([folder_info(portable)], src, out), quiet)
    assert (st / "data" / "textures" / "wall.dds").is_file()
    assert aside is not None


# ---- when the game folder already holds the names the disc uses ------------------

def collected(lines):
    return "\n".join(lines)


@pytest.fixture
def clashing(portable):
    """A game folder carrying an autorun.inf and an AUTORUN folder of its own.
    Plenty of games ship one; a GOG download never could, because its files are
    setup_*.exe and .bin parts."""
    write(portable / "autorun.inf", "[autorun]\nopen=THEIRS.EXE\n")
    write(portable / "AUTORUN" / "theirs.txt", "the game has one too")
    write(portable / "PORTABLE.ico", "not an icon, but named like the disc's")
    return portable


def test_the_discs_own_files_win(clashing, src, tmp_path):
    # They have to: the disc's autorun.inf is what opens the menu.
    st, _ = stage(settings_for([folder_info(clashing)], src, tmp_path / "out"), quiet)
    assert "AUTORUN" in (st / "autorun.inf").read_text(encoding="latin-1")
    assert (st / "PORTABLE.ico").read_bytes()[:4] == b"\0\0\1\0"


def test_it_says_which_of_the_games_files_were_replaced(clashing, src, tmp_path):
    # Winning silently contradicts the promise made when the folder was added:
    # that it goes on the disc as it stands. Here part of it did not.
    said = []
    stage(settings_for([folder_info(clashing)], src, tmp_path / "out"), said.append)
    log = collected(said)
    assert "the disc's own autorun.inf replaced" in log
    assert "the disc's own PORTABLE.ico replaced" in log


def test_it_says_the_game_brought_an_autorun_folder(clashing, src, tmp_path):
    said = []
    st, _ = stage(settings_for([folder_info(clashing)], src, tmp_path / "out"), said.append)
    assert (st / "AUTORUN" / "theirs.txt").is_file()
    assert (st / "AUTORUN" / "menu.hta").is_file()
    assert "brings an AUTORUN folder" in collected(said)


def test_it_says_nothing_of_the_sort_for_an_ordinary_folder(portable, src, tmp_path):
    # A note on every disc would be a note nobody reads.
    said = []
    stage(settings_for([folder_info(portable)], src, tmp_path / "out"), said.append)
    assert "replaced" not in collected(said)


def test_a_game_in_its_own_folder_cannot_clash_at_all(clashing, src, tmp_path):
    # Two games means numbered folders, so the game's autorun.inf is inside one
    # and never meets the disc's. Nothing to say.
    other = clashing.parent / "Other Game"
    write(other / "other.txt", "other")
    said = []
    st, _ = stage(settings_for([folder_info(clashing), folder_info(other)], src,
                               tmp_path / "out"), said.append)
    assert (st / "Games" / "01 - Portable Game" / "autorun.inf").is_file()
    assert "replaced" not in collected(said)
