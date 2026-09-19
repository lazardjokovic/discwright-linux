import hashlib
import os
import shutil
from pathlib import Path

import pytest

from PIL import Image, ImageChops

from discwright.autorun import autorun_inf
from discwright.xdg import xdg_volume_info
from discwright.games import add_on_info, game_info
from discwright.settings import DiscSettings
from discwright.stage import StagingError, stage

MB = 1 << 20
PE = Path(__file__).parent / "fixtures" / "pe"
ICONS = Path(__file__).parent / "fixtures" / "icons"


def sparse(path: Path, size: int) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "wb") as f:
        f.truncate(size)
    return path


def write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text)
    return path


@pytest.fixture
def src(tmp_path):
    """A GOG download, an icon, a manual and an extras folder, as a user has them."""
    root = tmp_path / "src"
    game = root / "Alpha"
    sparse(game / "setup_alpha_1.0.exe", MB)
    sparse(game / "setup_alpha_1.0-1.bin", 2 * MB)
    (root / "art").mkdir(parents=True)
    shutil.copy(ICONS / "windows-0.7.3" / "source.ico", root / "art" / "alpha.ico")
    write(root / "media" / "Alpha Manual.pdf", "MANUAL")
    write(root / "media" / "Extras" / "wallpaper.txt", "WALL")
    write(root / "media" / "Extras" / "deep" / "notes.txt", "NOTES")
    return root


def settings(src: Path, out: Path, **kw) -> DiscSettings:
    base = dict(games=[game_info(src / "Alpha")], label="ALPHA", out_dir=out,
                icon_path=src / "art" / "alpha.ico", icon_is_ico=True, menu=True,
                buttons=["Play", "Install", "Manual", "Extras", "Exit"],
                manual_path=src / "media" / "Alpha Manual.pdf",
                extras_path=src / "media" / "Extras")
    base.update(kw)
    return DiscSettings(**base)


def listing(folder: Path) -> list[str]:
    return sorted(str(p.relative_to(folder)).replace(os.sep, "/")
                  for p in folder.rglob("*") if p.is_file())


def quiet(_msg):
    pass


# ---- one game ----------------------------------------------------------------

def test_lays_a_one_game_disc_out_flat(src, tmp_path):
    st, aside = stage(settings(src, tmp_path / "out"), quiet)
    assert aside is None
    assert listing(st) == [
        "ALPHA.ico", "AUTORUN/ALPHA.ico",
        "Extras/Alpha Manual.pdf", "Extras/deep/notes.txt", "Extras/wallpaper.txt",
        "autorun.inf", "setup_alpha_1.0-1.bin", "setup_alpha_1.0.exe",
    ]


def test_writes_the_same_autorun_inf_the_module_produces(src, tmp_path):
    st, _ = stage(settings(src, tmp_path / "out"), quiet)
    assert (st / "autorun.inf").read_bytes() == autorun_inf("ALPHA", "ALPHA.ico", True)


def test_hard_links_the_installers_rather_than_copying_them(src, tmp_path):
    st, _ = stage(settings(src, tmp_path / "out"), quiet)
    assert os.path.samefile(st / "setup_alpha_1.0-1.bin", src / "Alpha" / "setup_alpha_1.0-1.bin")


def test_copies_the_disc_wide_manual_only_when_the_menu_has_a_manual_button(src, tmp_path):
    st, _ = stage(settings(src, tmp_path / "out", buttons=["Play", "Install", "Exit"]), quiet)
    assert not (st / "Extras").exists()


def test_leaves_the_menu_files_out_when_there_is_no_menu(src, tmp_path):
    st, _ = stage(settings(src, tmp_path / "out", menu=False), quiet)
    assert not (st / "AUTORUN" / "ALPHA.ico").exists()
    assert b"shellexecute" not in (st / "autorun.inf").read_bytes()


def test_copies_music_into_autorun_under_a_fixed_name(src, tmp_path):
    song = write(src / "media" / "Dusk.mp3", "SONG")
    st, _ = stage(settings(src, tmp_path / "out", music_file=song), quiet)
    assert (st / "AUTORUN" / "music.mp3").read_text() == "SONG"


# ---- several games, add-ons, and each entry's own files ------------------------

def test_files_two_games_and_an_add_on_the_way_the_layout_says(src, tmp_path):
    sparse(src / "Beta" / "setup_beta_2.0.exe", MB)
    sparse(src / "Alpha" / "patch_alpha_1.0_to_1.1.exe", MB)
    patch = add_on_info(src / "Alpha" / "patch_alpha_1.0_to_1.1.exe")
    patch.parent_index = 0
    games = [game_info(src / "Alpha"), game_info(src / "Beta"), patch]
    st, _ = stage(settings(src, tmp_path / "out", games=games, buttons=["Play", "Install", "Exit"]), quiet)
    got = listing(st)
    assert "Games/01 - alpha/setup_alpha_1.0.exe" in got
    assert "Games/01 - alpha/setup_alpha_1.0-1.bin" in got
    assert "Games/01 - alpha/Add-ons/01 - Update 1.1/patch_alpha_1.0_to_1.1.exe" in got
    assert "Games/02 - beta/setup_beta_2.0.exe" in got
    # Only the first game's files: the patch is its own entry and goes under Add-ons.
    assert "Games/01 - alpha/patch_alpha_1.0_to_1.1.exe" not in got


def test_puts_an_entrys_own_manual_and_extras_beside_its_installer(src, tmp_path):
    sparse(src / "Beta" / "setup_beta_2.0.exe", MB)
    a, b = game_info(src / "Alpha"), game_info(src / "Beta")
    a.manual_path = src / "media" / "Alpha Manual.pdf"
    a.extras_path = src / "media" / "Extras"
    st, _ = stage(settings(src, tmp_path / "out", games=[a, b], buttons=["Play", "Exit"]), quiet)
    got = listing(st)
    assert "Games/01 - alpha/Extras/Alpha Manual.pdf" in got
    assert "Games/01 - alpha/Extras/deep/notes.txt" in got
    assert not any(p.startswith("Games/02 - beta/Extras") for p in got)


# ---- extra content -------------------------------------------------------------

def test_copies_extra_content_to_the_root_under_its_own_name(src, tmp_path):
    loose = write(src / "loose" / "readme.txt", "HI")
    folder = src / "media" / "Extras"
    st, _ = stage(settings(src, tmp_path / "out", extra_items=[loose, folder / "deep"]), quiet)
    assert (st / "readme.txt").read_text() == "HI"
    assert (st / "deep" / "notes.txt").read_text() == "NOTES"


def test_refuses_extra_content_that_would_overwrite_the_disc_itself(src, tmp_path):
    evil = write(src / "loose" / "AUTORUN.INF", "[autorun]\nopen=evil.exe")
    log = []
    st, _ = stage(settings(src, tmp_path / "out", extra_items=[evil]), log.append)
    assert b"evil" not in (st / "autorun.inf").read_bytes()
    assert any("reserved" in m for m in log)


def test_skips_extra_content_that_has_gone_and_says_so(src, tmp_path):
    log = []
    stage(settings(src, tmp_path / "out", extra_items=[src / "nowhere.txt"]), log.append)
    assert any("MISSING" in m for m in log)


# ---- rebuilding over an existing disc folder ------------------------------------

def test_sets_the_old_disc_folder_aside_rather_than_deleting_it(src, tmp_path):
    out = tmp_path / "out"
    write(out / "disc" / "old-file.txt", "OLD")
    st, aside = stage(settings(src, out), quiet)
    assert aside is not None and (aside / "old-file.txt").read_text() == "OLD"
    assert not (st / "old-file.txt").exists()


def test_still_finds_an_icon_that_was_picked_from_inside_the_old_folder(src, tmp_path):
    # The build that ate its own source: the icon lived in the stage being replaced.
    out = tmp_path / "out"
    (out / "disc").mkdir(parents=True)
    icon = out / "disc" / "ALPHA.ico"
    shutil.copy(src / "art" / "alpha.ico", icon)
    original = icon.read_bytes()
    st, _ = stage(settings(src, out, icon_path=icon), quiet)
    assert (st / "ALPHA.ico").read_bytes() == original


def test_rebuilds_in_place_when_the_game_lives_in_the_disc_folder(src, tmp_path):
    out = tmp_path / "out"
    shutil.copytree(src / "Alpha", out / "disc")
    log = []
    st, aside = stage(settings(src, out, games=[game_info(out / "disc")]), log.append)
    assert aside is None
    assert any("in place" in m for m in log)
    assert (st / "setup_alpha_1.0.exe").exists()


def test_replaces_a_read_only_file_left_by_the_last_build(src, tmp_path):
    out = tmp_path / "out"
    shutil.copytree(src / "Alpha", out / "disc")
    old_manual = write(out / "disc" / "Extras" / "Alpha Manual.pdf", "STALE")
    old_manual.chmod(0o444)
    st, _ = stage(settings(src, out, games=[game_info(out / "disc")]), quiet)
    assert (st / "Extras" / "Alpha Manual.pdf").read_text() == "MANUAL"


# ---- what is refused, and what is not ported yet ---------------------------------

def test_makes_the_disc_icon_from_a_picture(src, tmp_path):
    st, _ = stage(settings(src, tmp_path / "out", icon_path=ICONS / "source.png", icon_is_ico=False), quiet)
    assert (st / "ALPHA.ico").read_bytes()[:4] == b"\0\0\1\0"
    assert (st / "AUTORUN" / "ALPHA.ico").read_bytes() == (st / "ALPHA.ico").read_bytes()


def test_refuses_an_unreadable_icon_before_copying_anything(src, tmp_path):
    out = tmp_path / "out"
    bad = write(src / "art" / "a.png", "not a picture")
    with pytest.raises(StagingError, match="cannot be used"):
        stage(settings(src, out, icon_path=bad, icon_is_ico=False), quiet)
    assert not (out / "disc").exists()


def test_refuses_a_missing_icon_before_copying_anything(src, tmp_path):
    out = tmp_path / "out"
    with pytest.raises(StagingError):
        stage(settings(src, out, icon_path=src / "art" / "gone.ico"), quiet)
    assert not (out / "disc").exists()


def test_names_the_disc_for_linux_when_asked(src, tmp_path):
    st, _ = stage(settings(src, tmp_path / "out", linux_info=True), quiet)
    assert (st / ".xdg-volume-info").read_bytes() == xdg_volume_info("ALPHA", "ALPHA.png")
    assert Image.open(st / "ALPHA.png").size == (256, 256)


def test_removes_the_linux_name_file_when_rebuilt_without_it(src, tmp_path):
    out = tmp_path / "out"
    write(out / "disc" / ".xdg-volume-info", "[Volume Info]\nName=OLD\n")
    write(out / "disc" / "ALPHA.png", "old png")
    shutil.copytree(src / "Alpha", out / "disc", dirs_exist_ok=True)
    st, _ = stage(settings(src, out, games=[game_info(out / "disc")], linux_info=False), quiet)
    assert not (st / ".xdg-volume-info").exists()
    assert not (st / "ALPHA.png").exists()


# ---- against what Windows DiscWright staged for the same disc ---------------------
#
# Needs the real GOG download (DISCWRIGHT_GOG_DIR) and somewhere to stage on the
# same filesystem as the installers, so they are hard-linked rather than copied
# (DISCWRIGHT_STAGE_DIR). On the machine this started on the installers are on C:
# behind junctions in F:\DWdemo, and the reference is F:\DWdemo\out\disc, staged
# by Windows DiscWright from the settings in F:\DWdemo\out\discproject.json.

GOG = os.environ.get("DISCWRIGHT_GOG_DIR")
SCRATCH = os.environ.get("DISCWRIGHT_STAGE_DIR")

# Files the modules still to come will write. Listed rather than ignored, so the
# test has to be tightened when each arrives instead of passing quietly.
NOT_PORTED_YET = {"AUTORUN/bg.png", "AUTORUN/menu.hta"}

# Files an image encoder makes. The picture must match; the bytes cannot, since
# two libraries compress the same picture differently. See tests/test_icons.py.
SAME_PICTURE = {"ALANWAKE.png"}


def _fingerprint(p: Path) -> tuple[int, str]:
    # Whole file under 64 MB; for the multi-gigabyte installer parts, the size
    # plus the first and last 16 MB, since reading 16 GB across the WSL bridge
    # would take longer than the rest of the suite together.
    size = p.stat().st_size
    h = hashlib.sha256()
    with open(p, "rb") as f:
        if size <= 64 * MB:
            h.update(f.read())
        else:
            h.update(f.read(16 * MB))
            f.seek(size - 16 * MB)
            h.update(f.read(16 * MB))
    return size, h.hexdigest()


@pytest.mark.skipif(not (GOG and SCRATCH), reason="DISCWRIGHT_GOG_DIR and DISCWRIGHT_STAGE_DIR not set")
def test_stages_what_windows_staged_for_the_same_disc():
    gog = Path(GOG)
    reference = gog / "out" / "disc"
    out = Path(SCRATCH) / "alanwake"
    if out.exists():
        shutil.rmtree(out)
    s = DiscSettings(
        games=[game_info(gog / "Alan Wake")], label="ALAN WAKE", out_dir=out,
        icon_path=gog / "artwork" / "alanwake-icon.ico", icon_is_ico=True, menu=True,
        bg_path=gog / "artwork" / "alanwake-background.jpg", show_title=True, title_text="ALAN WAKE",
        buttons=["Play", "Install", "Manual", "Extras", "Exit"],
        manual_path=gog / "media" / "AlanWake_manual" / "alan_wake_manual" / "Alan Wake manual.pdf",
        extras_path=gog / "media" / "DiscExtras", linux_info=True)
    try:
        st, _ = stage(s, quiet)
        ours, theirs = set(listing(st)), set(listing(reference))
        assert theirs - ours == NOT_PORTED_YET
        assert ours - theirs == set()
        for rel in sorted(ours - SAME_PICTURE):
            assert _fingerprint(st / rel) == _fingerprint(reference / rel), rel
        # The Linux icon is compared against the source icon's own 256px frame,
        # not against the Windows file, because the Windows file is wrong. Windows
        # DiscWright 0.7.3 turns this icon into noise: reproduced fresh, not only
        # on this disc. The same code converts an icon Windows wrote itself
        # correctly, so it is something in how this game's own icon is laid out
        # (256px frame first, stored as a PNG, then 48, 32 and 16). Do not copy it;
        # when Windows is fixed, this can go back to comparing with the reference.
        for rel in SAME_PICTURE:
            a = Image.open(st / rel).convert("RGBA")
            with Image.open(s.icon_path) as src:
                src.size = max(src.info["sizes"])
                b = src.convert("RGBA")
            assert a.size == b.size == (256, 256)
            box = (1, 1, a.width - 1, a.height - 1)
            flat = ImageChops.difference(a.crop(box), b.crop(box)).tobytes()
            assert sum(flat) / len(flat) <= 3.0, rel
    finally:
        shutil.rmtree(out, ignore_errors=True)
