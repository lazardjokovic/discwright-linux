"""A whole build: the disc staged as a folder, then written as an ISO.

Ported from Invoke-Build in DiscWright.ps1, whose two halves are stage.py and
iso.py. Saving the project file beside the ISO, which Invoke-Build also does,
arrives with the project file module.
"""

from __future__ import annotations

import shutil
from pathlib import Path

from .iso import IsoError, Log, Progress, build_iso, iso_path
from .project import PROJECT_FILE, save_project
from .settings import DiscSettings
from .stage import stage
from .text import volume_label


def _back_from_aside(s: DiscSettings, aside: Path, stage_dir: Path) -> None:
    """Point the settings at the new disc folder again.

    A rebuild over an existing disc sets the old folder aside and rewrites every
    path that pointed inside it, because the build reads from there: an icon, a
    manual, the installers themselves. That folder is deleted once the ISO is
    written, so a project saved with those paths would name a folder that no
    longer exists. Each file is in the new disc folder under the same name, so
    the paths go back to it before the project is saved.
    """
    def back(path):
        if path is None:
            return None
        try:
            return stage_dir / Path(path).resolve().relative_to(aside.resolve())
        except (ValueError, OSError):
            return path

    for attr in ("icon_path", "bg_path", "music_file", "manual_path", "extras_path"):
        setattr(s, attr, back(getattr(s, attr)))
    s.extra_items = [back(p) for p in s.extra_items]
    for g in s.games:
        g.folder = back(g.folder)
        g.setup_exe = back(g.setup_exe)
        g.manual_path = back(g.manual_path)
        g.extras_path = back(g.extras_path)
        g.files = [back(f) for f in g.files]


def build(s: DiscSettings, log: Log = print, progress: Progress | None = None) -> Path:
    """Stage the disc and write its ISO. Returns the ISO's path.

    Windows DiscWright writes UDF, and ISO9660 and Joliet beside it only when
    "readable on Windows XP and older" is ticked. This always writes ISO9660 and
    Joliet, so legacy_fs changes nothing here: the disc is already the older
    kind, including for a game whose installer parts IMAPI refuses in ISO9660.
    """
    out = iso_path(s.out_dir, s.label)
    if out is None:
        # Asked before staging, so nothing is copied for a build with nowhere to go.
        raise IsoError("The disc needs a label with at least one letter or digit, and an "
                       "output folder, to name its ISO.")
    stage_dir, aside = stage(s, log)
    iso = build_iso(stage_dir, out, volume_label(s.label), log, progress)
    if aside is not None:
        _back_from_aside(s, aside, stage_dir)
    save_project(s, s.out_dir)
    log(f"Saved {PROJECT_FILE} - open it to rebuild this disc.")
    # The old disc folder goes only now. If anything above failed, everything in
    # it is still on disk, including installers a rebuild was reading from it.
    if aside is not None:
        shutil.rmtree(aside, ignore_errors=True)
    return iso
