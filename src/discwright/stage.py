"""Staging: laying the disc out as a folder, file by file, before it becomes an ISO.

Ported from the first half of Invoke-Build in DiscWright.ps1: the installers,
each entry's own manual and extras, the disc-wide manual and extras, music,
extra content, autorun.inf and .xdg-volume-info.

Not yet here, each waiting on its own module: turning a PNG or JPG into the disc
icon, the PNG copy of the icon a Linux desktop shows, composing the menu
background, and writing the menu itself. stage() says so in the log and raises
when a setting needs one of them, rather than building a disc that quietly lacks
it.
"""

from __future__ import annotations

import os
import shutil
import uuid
from pathlib import Path, PureWindowsPath
from typing import Callable

from .autorun import autorun_inf
from .layout import disc_entry_extras, disc_entry_folder, disc_icon_name, is_reserved_name
from .settings import DiscSettings
from .xdg import xdg_volume_info

Log = Callable[[str], None]


class StagingError(Exception):
    pass


def _on_disc(stage: Path, disc_path: str) -> Path:
    """A path on the disc, written with backslashes, as a real path under stage."""
    return stage.joinpath(*PureWindowsPath(disc_path).parts) if disc_path else stage


def _same(a: Path | None, b: Path | None) -> bool:
    if a is None or b is None:
        return False
    try:
        return a.exists() and b.exists() and os.path.samefile(a, b)
    except OSError:
        return False


def _inside(path: Path | None, folder: Path) -> bool:
    if path is None:
        return False
    try:
        Path(path).resolve().relative_to(folder.resolve())
        return True
    except ValueError:
        return False


def _moved(path: Path | None, old: Path, new: Path) -> Path | None:
    if path is None or not _inside(path, old):
        return path
    return new / Path(path).resolve().relative_to(old.resolve())


def _replace_file(src: Path, dest: Path) -> None:
    """Copy src over dest, even when dest is read-only. Windows clears the
    read-only attribute around every copy for the same reason: a file brought in
    from a burned disc or a read-only share keeps that bit, and the next rebuild
    would fail on it."""
    if dest.exists() or dest.is_symlink():
        dest.chmod(0o644)
        dest.unlink()
    shutil.copyfile(src, dest)


def _link_or_copy(src: Path, dest: Path, log: Log) -> None:
    # A hard link costs nothing however big the installer is, and needs both ends
    # on one filesystem, so it is tried per file and a copy is the fallback.
    try:
        os.link(src, dest)
        log(f"  linked {src.name}")
    except OSError:
        log(f"  copying {src.name} ...")
        shutil.copyfile(src, dest)


def _copy_tree_contents(src: Path, dest: Path) -> None:
    dest.mkdir(parents=True, exist_ok=True)
    shutil.copytree(src, dest, dirs_exist_ok=True, copy_function=shutil.copyfile)


def stage(s: DiscSettings, log: Log = print) -> tuple[Path, Path | None]:
    """Lay the disc out in <out_dir>/disc.

    Returns the staging folder and, when an old one was in the way, the folder it
    was set aside to. The caller removes that only once the ISO has been written:
    if the build fails, everything that was in the old folder is still on disk.
    """
    # Settings that cannot be built are refused before anything is copied, not
    # after several gigabytes of installers have been. The Windows app checks the
    # icon only once the payload is in place.
    if s.icon_path is None:
        raise StagingError("A disc needs an icon.")
    if not s.icon_is_ico:
        raise StagingError("Building an icon from a PNG or JPG is not ported yet; use an .ico for now.")
    if not Path(s.icon_path).is_file():
        raise StagingError(f"The icon is not there: {s.icon_path}")

    out = Path(s.out_dir)
    stage_dir = out / "disc"
    aside: Path | None = None
    games = list(s.games)

    # Rebuilding a disc folder in place: the installers already live in the stage.
    # One entry, deliberately, not one game: a game carrying add-ons still needs
    # them copied into Add-ons/, and this branch copies nothing.
    in_place = len(games) == 1 and games[0].folder is not None and _same(games[0].folder, stage_dir)

    if in_place:
        log("Rebuilding in place - installer files left untouched.")
        (stage_dir / "AUTORUN").mkdir(parents=True, exist_ok=True)
    else:
        if stage_dir.exists():
            # Set aside, never wiped. The build reads out of the old folder: an
            # icon or manual picked from inside it and, after opening a built disc,
            # the installers themselves. Wiping first and copying afterwards was a
            # build that ate its own source. A rename within one filesystem is
            # instant however much it holds.
            aside = out / ("disc.previous-" + uuid.uuid4().hex[:8])
            try:
                stage_dir.rename(aside)
            except OSError as e:
                raise StagingError(
                    f"The old disc folder cannot be replaced - something is still using it:\n\n"
                    f"{stage_dir}\n\nClose anything showing that folder, unmount the ISO if "
                    f"it is mounted, then build again.\n\nThe system said: {e}") from e
            log(f"Set the old disc folder aside as {aside.name} - it goes once the ISO is written.")
            for attr in ("icon_path", "bg_path", "music_file", "manual_path", "extras_path"):
                setattr(s, attr, _moved(getattr(s, attr), stage_dir, aside))
            s.extra_items = [_moved(p, stage_dir, aside) for p in s.extra_items]
            for g in games:
                if not _inside(g.folder, stage_dir):
                    continue
                g.folder = _moved(g.folder, stage_dir, aside)
                g.manual_path = _moved(g.manual_path, stage_dir, aside)
                g.extras_path = _moved(g.extras_path, stage_dir, aside)
                g.setup_exe = _moved(g.setup_exe, stage_dir, aside)
                g.files = [_moved(f, stage_dir, aside) for f in g.files]
                log(f"  kept the installer for {g.game_name}")

        log("Preparing staging folder...")
        (stage_dir / "AUTORUN").mkdir(parents=True, exist_ok=True)

        # Where each entry goes is layout.py's decision, not this loop's: the menu
        # asks the same functions the same question.
        for i, g in enumerate(games):
            rel = disc_entry_folder(games, i)
            dest_dir = _on_disc(stage_dir, rel)
            if rel:
                dest_dir.mkdir(parents=True, exist_ok=True)
                what = "add-on" if g.kind == "AddOn" else "game"
                log(f"  {what} : {g.game_name} -> {dest_dir.name}")
            for f in g.files:
                _link_or_copy(f, dest_dir / f.name, log)

            # This entry's own manual and extras, beside its installer, so on a
            # two-game disc each game's Manual button opens its own manual.
            if g.manual_path or g.extras_path:
                gx = _on_disc(stage_dir, disc_entry_extras(games, i))
                gx.mkdir(parents=True, exist_ok=True)
                if g.extras_path and Path(g.extras_path).is_dir():
                    _copy_tree_contents(Path(g.extras_path), gx)
                    log(f"    extras for {g.game_name}")
                if g.manual_path and Path(g.manual_path).is_file():
                    _replace_file(Path(g.manual_path), gx / Path(g.manual_path).name)
                    log(f"    manual for {g.game_name}: {Path(g.manual_path).name}")

    # The icon, named after the disc so Explorer's per-path icon cache cannot show
    # a previous disc's icon for the same drive letter.
    ico_name = disc_icon_name(s.label)
    ico_out = stage_dir / ico_name
    if _same(Path(s.icon_path), ico_out):
        log("Icon already in place.")
    else:
        _replace_file(Path(s.icon_path), ico_out)
        log("Icon copied (.ico used as-is).")
    log(f"Disc icon: {ico_name}")

    png_name = None
    if s.linux_info:
        # Windows writes .xdg-volume-info only once the PNG it points at exists,
        # and a disc that shows a generic icon on Linux is still a working disc.
        log("  the Linux icon is not ported yet - this disc will show a generic one on Linux.")

    # A rebuild whose label changed would otherwise leave the old icon behind.
    for stale in list(stage_dir.glob("*.ico")) + list(stage_dir.glob("*.png")):
        if stale.name not in (ico_name, png_name):
            stale.unlink()
            log(f"  removed old icon: {stale.name}")

    if s.menu:
        if s.bg_path is not None:
            log("  composing the menu background is not ported yet.")
        if "Manual" in s.buttons and s.manual_path:
            (stage_dir / "Extras").mkdir(exist_ok=True)
            dest = stage_dir / "Extras" / Path(s.manual_path).name
            if not _same(Path(s.manual_path), dest):
                _replace_file(Path(s.manual_path), dest)
        if "Extras" in s.buttons and s.extras_path and Path(s.extras_path).is_dir():
            if _same(Path(s.extras_path), stage_dir / "Extras"):
                log("Extras already in place.")
            else:
                _copy_tree_contents(Path(s.extras_path), stage_dir / "Extras")
        if s.music_file and Path(s.music_file).is_file():
            dest = stage_dir / "AUTORUN" / ("music" + Path(s.music_file).suffix)
            if not _same(Path(s.music_file), dest):
                _replace_file(Path(s.music_file), dest)
        # The menu window's own icon resolves next to menu.hta, not at the disc
        # root, so it needs a copy inside AUTORUN or the taskbar shows mshta's.
        menu_ico = stage_dir / "AUTORUN" / ico_name
        if not _same(ico_out, menu_ico):
            _replace_file(ico_out, menu_ico)
        for stale in (stage_dir / "AUTORUN").glob("*.ico"):
            if stale.name != ico_name:
                stale.unlink()
        log("  writing the menu is not ported yet.")

    if s.extra_items:
        log("Adding extra content...")
    for item in s.extra_items:
        if item is None:
            continue
        item = Path(item)
        if not item.exists():
            log(f"  MISSING, skipped: {item}")
            continue
        if is_reserved_name(item.name, ico_name):
            log(f"  SKIPPED (reserved disc name): {item.name}")
            continue
        dest = stage_dir / item.name
        if _same(item, dest):
            log(f"  already in place: {item.name}")
        elif item.is_dir():
            _copy_tree_contents(item, dest)
            log(f"  folder: {item.name}")
        else:
            _replace_file(item, dest)
            log(f"  file:   {item.name}")

    log("Writing autorun.inf...")
    (stage_dir / "autorun.inf").write_bytes(autorun_inf(s.label, ico_name, s.menu))

    # Windows never reads this file and Linux never reads autorun.inf, so the two
    # sit side by side and the disc introduces itself on either machine.
    xdg = stage_dir / ".xdg-volume-info"
    if png_name:
        xdg.write_bytes(xdg_volume_info(s.label, png_name))
    elif xdg.exists():
        # Rebuilt without it: leaving it would point Linux at a PNG just removed.
        xdg.unlink()
        log("Removed .xdg-volume-info - this disc is no longer named for Linux.")

    return stage_dir, aside
