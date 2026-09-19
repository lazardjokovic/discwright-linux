"""The ISO: the staged disc folder, written as one image file by xorriso.

Ported from the second half of Invoke-Build and from Get-IsoPath in
DiscWright.ps1. Windows writes UDF through IMAPI; Linux has no IMAPI, and
xorriso writes ISO9660 with Joliet and Rock Ridge instead. docs/xorriso-spike.md
measured that Windows treats the result as the same disc: same label, the same
autorun.inf read, every file identical.

What it does not share with UDF is how names are stored. Windows reads this disc
through Joliet, which holds less than a Windows folder can, and xorriso does not
refuse what it cannot hold: it cuts or changes the name, quietly. A manual whose
name was changed on the way is a Manual button that opens nothing. So every name
on the disc is checked first, and the build stops before writing anything if
one would not reach Windows as it is. Every rule below was measured by building
an ISO and mounting it on Windows 11; docs/xorriso-spike.md has the results.
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess
from pathlib import Path
from typing import Callable

Log = Callable[[str], None]
Progress = Callable[[float], None]

# Joliet names longer than this come back cut. Windows 11 read files of up to
# 104 characters and folders of up to 103 whole; one limit for both is simpler
# and loses nothing a real disc needs.
JOLIET_MAX_NAME = 103

# Characters no Windows file name can hold. xorriso turns some of them into "_"
# and writes the rest as they are, and Windows cannot open a file with one.
WINDOWS_FORBIDDEN = set('\\/:*?"<>|')

# Names Windows keeps for devices, with or without an extension: a file called
# CON.txt is a file on Linux and the console on Windows. From Windows' own
# naming rules rather than measured here.
WINDOWS_DEVICES = {"con", "prn", "aux", "nul"} | {f"{p}{n}" for p in ("com", "lpt") for n in range(1, 10)}


class IsoError(Exception):
    pass


def iso_path(out_dir: str | Path, label: str | None) -> Path | None:
    """The ISO this build writes: the label, with anything but letters, digits,
    underscore, hyphen and space made an underscore. None when there is not
    enough to name one. Ported from Get-IsoPath."""
    if not out_dir or not str(out_dir).strip():
        return None
    name = re.sub(r"[^A-Za-z0-9_\- ]", "_", label or "").strip()
    if not name:
        return None
    return Path(out_dir) / (name + ".iso")


def _utf16_units(name: str) -> int:
    return len(name.encode("utf-16-le")) // 2


def name_problems(stage: str | Path) -> list[str]:
    """Every name under stage that would not reach Windows as it is, each as a
    sentence naming the file. Empty when the disc is fine."""
    stage = Path(stage)
    problems = []
    for folder, dirs, files in os.walk(stage):
        here = Path(folder)
        seen: dict[str, str] = {}
        for name in sorted(dirs) + sorted(files):
            rel = (here / name).relative_to(stage).as_posix()
            bad = sorted({c for c in name if c in WINDOWS_FORBIDDEN or ord(c) < 32})
            if bad:
                shown = " ".join(repr(c) if ord(c) < 32 else c for c in bad)
                problems.append(f"{rel}: has {shown} in its name, which Windows does not allow.")
            if any(ord(c) > 0xFFFF for c in name):
                problems.append(f"{rel}: has a character in its name, such as an emoji, that the "
                                "disc's Windows names cannot hold.")
            if name.split(".")[0].rstrip(" ").casefold() in WINDOWS_DEVICES:
                problems.append(f"{rel}: Windows keeps the name {name.split('.')[0]} for a device "
                                "and cannot open a file called that.")
            if name.endswith((".", " ")):
                problems.append(f"{rel}: ends with a {'dot' if name.endswith('.') else 'space'}, "
                                "which Windows drops.")
            if _utf16_units(name) > JOLIET_MAX_NAME:
                problems.append(f"{rel}: the name is {_utf16_units(name)} characters long; Windows "
                                f"reads names on this disc up to {JOLIET_MAX_NAME}.")
            key = name.casefold()
            if key in seen:
                problems.append(f"{rel}: differs from {seen[key]} only in capitals, and Windows "
                                "sees one file where there are two.")
            else:
                seen[key] = name
    return problems


def xorriso_command(stage: Path, out: Path, volume_id: str) -> list[str]:
    # -input-charset UTF-8 because xorriso otherwise takes the locale's: under a
    # C locale it wrote "Cafe" with an acute accent into the Joliet names as
    # "Caf__", with no warning. -iso-level 3 lets a file over 4 GiB be stored in
    # pieces, which Windows 11 reads back whole (measured). -joliet-long raises
    # Joliet's 64-character limit to 103.
    return ["xorriso", "-as", "mkisofs", "-input-charset", "UTF-8",
            "-iso-level", "3", "-J", "-joliet-long", "-r",
            "-V", volume_id, "-o", str(out), str(stage)]


_UPDATE = re.compile(r"UPDATE\s*:\s*([0-9.]+)% done")


def build_iso(stage: str | Path, out: str | Path, volume_id: str,
              log: Log = print, progress: Progress | None = None) -> Path:
    """Write stage as an ISO at out, and return out.

    Written beside out under a temporary name and renamed over it only once
    xorriso has finished, so a failed build leaves the previous ISO as it was.
    """
    stage, out = Path(stage), Path(out)
    problems = name_problems(stage)
    if problems:
        shown = problems[:10]
        more = f"\n...and {len(problems) - 10} more." if len(problems) > 10 else ""
        raise IsoError("Some names on the disc would not reach Windows as they are, so its menu "
                       "could not find them. Rename these and build again:\n\n"
                       + "\n".join(shown) + more)
    if shutil.which("xorriso") is None:
        raise IsoError("xorriso is not installed, and it is what writes the ISO. "
                       "On Debian or Ubuntu: sudo apt install xorriso")

    tmp = out.with_name(out.name + ".partial")
    if tmp.exists():
        tmp.unlink()
    log(f"Building ISO with xorriso (volume id {volume_id})...")
    tail: list[str] = []
    proc = subprocess.Popen(xorriso_command(stage, tmp, volume_id),
                            stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
                            text=True, encoding="utf-8", errors="replace")
    assert proc.stderr is not None
    for line in proc.stderr:
        m = _UPDATE.search(line)
        if m:
            if progress:
                progress(float(m.group(1)))
            continue
        tail = (tail + [line.rstrip()])[-12:]
    if proc.wait() != 0:
        if tmp.exists():
            tmp.unlink()
        raise IsoError("xorriso could not write the ISO. It said:\n\n" + "\n".join(tail))
    if progress:
        progress(100.0)
    os.replace(tmp, out)
    log(f"DONE.  ISO: {out}  ({out.stat().st_size / 1024 ** 3:.2f} GB)")
    return out
