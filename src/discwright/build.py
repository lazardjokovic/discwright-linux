"""A whole build: the disc staged as a folder, then written as an ISO.

Ported from Invoke-Build in DiscWright.ps1, whose two halves are stage.py and
iso.py. Saving the project file beside the ISO, which Invoke-Build also does,
arrives with the project file module.
"""

from __future__ import annotations

import shutil
from pathlib import Path

from .iso import IsoError, Log, Progress, build_iso, iso_path
from .settings import DiscSettings
from .stage import stage
from .text import volume_label


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
    # The old disc folder goes only now. If anything above failed, everything in
    # it is still on disk, including installers a rebuild was reading from it.
    if aside is not None:
        shutil.rmtree(aside, ignore_errors=True)
    return iso
