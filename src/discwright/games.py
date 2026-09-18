"""A GOG download folder: which installer it holds, its parts, and the game's name.

Ported from Get-GameInfo and Set-InstallerFacts in DiscWright.ps1.

Two things that are free on Windows have to be done on purpose here:

- **File names are case-insensitive on Windows and not here.** GOG ships
  ``setup_*.exe``, but a folder that went through another filesystem can arrive as
  ``Setup_Game.EXE``, and Windows would still find it. So every match below
  compares case-folded names rather than globbing.
- **The name comes out of the installer's version resource**, which Windows reads
  for free. See pe.py.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

from .pe import product_name

GIB = 1 << 30
MIB = 1 << 20
KIB = 1 << 10


def format_size(n: float) -> str:
    """Sizes the way Windows DiscWright shows them: '7.79 GB', '3 MB', '12 KB'."""
    if n >= GIB:
        return f"{n / GIB:,.2f} GB"
    if n >= MIB:
        return f"{n / MIB:,.0f} MB"
    if n >= KIB:
        return f"{n / KIB:,.0f} KB"
    return f"{n:,.0f} bytes"


@dataclass
class GameInfo:
    ok: bool = False
    folder: Path | None = None
    setup_exe: Path | None = None
    files: list[Path] = field(default_factory=list)
    game_name: str | None = None
    # What the menu searches the registry for to find an installed copy. A second
    # field rather than a reuse of game_name on purpose: game_name is editable (it
    # is the name on the menu), while the registry holds whatever GOG registered.
    # Sharing one field meant renaming a game made Play stop finding it, with
    # nothing on screen saying why. Set once, here, and never written again.
    match_name: str | None = None
    msg: str = ""
    warning: str = ""
    total_bytes: int = 0
    max_file_bytes: int = 0
    missing_parts: list[int] = field(default_factory=list)
    kind: str = "Game"
    # An index into the disc's own list of entries, -1 for none. Names are not
    # stable enough to point with: two GOG folders can report the same name.
    parent_index: int = -1
    manual_path: Path | None = None
    extras_path: Path | None = None


def _setup_exes(folder: Path) -> list[Path]:
    found = [p for p in folder.iterdir()
             if p.is_file() and p.name.casefold().startswith("setup_")
             and p.name.casefold().endswith(".exe")]
    # Largest first: several installers in one folder is normal, base game plus
    # DLC, and the game is the big one. Name breaks a tie so the pick is stable.
    return sorted(found, key=lambda p: (-p.stat().st_size, p.name.casefold()))


def _fallback_name(exe: Path) -> str:
    # What Windows falls back to when the version resource is empty:
    # setup_the_witcher_1.5_(77554).exe -> "the witcher".
    name = re.sub(r"^setup_", "", exe.stem, flags=re.IGNORECASE).replace("_", " ")
    return re.sub(r"\s*\d.*$", "", name)


def game_info(folder: str | Path) -> GameInfo:
    folder = Path(folder)
    info = GameInfo()
    if not folder.is_dir():
        info.msg = "Folder not found."
        return info

    exes = _setup_exes(folder)
    if not exes:
        # Two folders in a project look alike and sit next to each other: the GOG
        # download, and the output folder DiscWright writes to. The second holds a
        # disc/ subfolder with the installer one level down, so picking it lands
        # here - and the generic message sends people looking for a problem with
        # their download instead of at which folder they picked. Name it instead.
        inner = next((p for p in folder.iterdir() if p.is_dir() and p.name.casefold() == "disc"), None)
        if inner is not None and _setup_exes(inner):
            info.msg = ("This looks like a disc DiscWright built, not a GOG download. "
                        "It wants the folder you downloaded from GOG; this one is where "
                        "the ISO gets written.")
        else:
            info.msg = 'No GOG "setup_*.exe" found in this folder.'
        return info

    exe = exes[0]
    _installer_facts(info, exe)
    if not info.missing_parts and len(exes) > 1:
        info.warning = f"{len(exes)} installers in this folder - using the largest, {exe.name}."
    return info


def _installer_facts(info: GameInfo, exe: Path) -> None:
    # Parts belong to ONE installer and are named "<installer>-1.bin", "-2.bin"...
    # Taking every setup_*.bin in the folder swept in the parts of a DLC or a
    # second game stored alongside. Match on the chosen exe's own name instead,
    # as plain strings: GOG names are full of brackets and dots.
    stem = exe.stem.casefold() + "-"
    parts = sorted(
        (p for p in exe.parent.iterdir()
         if p.is_file() and p.suffix.casefold() == ".bin" and p.name.casefold().startswith(stem)),
        key=lambda p: p.name.casefold(),
    )

    # A download that stopped early leaves a gap in the numbering. Building it
    # makes a clean-looking ISO that only fails when someone runs the installer
    # off the burned disc, the worst possible place to find out. Games with no
    # parts at all are normal; only a gap in an existing sequence is suspicious.
    numbers = sorted({int(m.group(1)) for p in parts if (m := re.search(r"-(\d+)$", p.stem))})
    if numbers:
        info.missing_parts = [n for n in range(1, numbers[-1] + 1) if n not in numbers]
    if info.missing_parts:
        info.warning = ("INCOMPLETE: installer part(s) "
                        + ", ".join(f"-{n}" for n in info.missing_parts)
                        + " are missing - the download looks unfinished.")

    name = product_name(exe)
    if not name or not name.strip():
        name = _fallback_name(exe)
    # Inno pads its version strings with trailing spaces; untrimmed they leak into
    # folder names ("...Edition                    Disc").
    name = name.strip()

    info.ok = True
    info.setup_exe = exe
    info.files = [exe, *parts]
    info.game_name = name
    info.match_name = name
    info.folder = exe.parent
    sizes = [p.stat().st_size for p in info.files]
    info.total_bytes = sum(sizes)
    info.max_file_bytes = max(sizes)
    plural = "file" if len(info.files) == 1 else "files"
    info.msg = f"Detected: {name}  ({len(info.files)} {plural}, {format_size(info.total_bytes)})"
