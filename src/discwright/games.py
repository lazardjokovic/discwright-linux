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
from typing import Sequence

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
    # Where this entry came from: "GOG" for a folder holding a GOG installer,
    # "Files" for a folder of game files GOG never packaged. It decides what the
    # disc carries - an installer and its numbered parts, which have no shape to
    # keep, or the folder exactly as it stands - so it is written into the
    # project file rather than guessed again on open.
    source: str = "GOG"


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


def folder_executables(folder: str | Path, limit: int = 25,
                       files: Sequence[Path] | None = None) -> list[Path]:
    """Every executable under a folder, largest first, for the question asked
    about a folder that is not a GOG download: which of these installs the game?

    Ported from Get-FolderExecutables. Biggest first because an installer is
    nearly always the largest .exe in a game folder, while a crash handler or an
    uninstaller is among the smallest; deepest-first would bury the obvious one.
    Capped, because a game folder can hold hundreds of tools and nobody picks
    from a list that long.

    ``files`` is an already-read listing, for a caller that needs the whole list
    anyway. Walking a 20 GB game folder twice costs real seconds on a cold cache,
    and the ordering rule stays here rather than being copied to the caller.
    """
    folder = Path(folder)
    if files is None:
        if not folder.is_dir():
            return []
        files = list(_all_files(folder))
    exes = [p for p in files if p.suffix.casefold() == ".exe"]
    # Name breaks a tie, so which executable is offered first cannot depend on
    # the order the filesystem happened to hand them over in.
    exes.sort(key=lambda p: (-p.stat().st_size, p.name.casefold()))
    return exes


def gog_subfolders(folder: str | Path) -> list[Path]:
    """The subfolders of this one that are GOG downloads in their own right.

    Picking the folder that HOLDS the downloads, rather than one game inside it,
    is the likeliest way to reach the folder question by mistake: it has no
    setup_*.exe of its own, so it is not a GOG download, and taking it whole
    would put every game somebody owns on one disc entry named after the folder.

    Nothing refuses it. A real game folder can have a setup_*.exe buried
    somewhere under it too, and refusing that would be worse than the mistake
    being guarded against, so this only reports what is there. Immediate
    children only: on Windows it runs before a dialog is shown.
    """
    folder = Path(folder)
    if not folder.is_dir():
        return []
    hits = [p for p in folder.iterdir() if p.is_dir() and _setup_exes(p)]
    return sorted(hits, key=lambda p: p.name.casefold())


def folder_info(folder: str | Path, installer_path: str | Path | None = None) -> GameInfo:
    """A folder that is not a GOG download: an unpacked archive, an itch.io
    download, an installed game, anything portable.

    Ported from Get-FolderInfo. Everything in the folder goes on the disc,
    subfolders and all, which is the difference from a GOG download: there the
    disc carries one installer and its numbered parts in a flat folder, here it
    carries whatever shape the game already has. A game whose data/ folder was
    flattened onto the disc root is a broken game.

    ``installer_path`` is the executable the menu's Install button runs, chosen
    in the dialog on Windows or named on the command line here, or nothing for a
    folder that only holds files. Nothing is not a failure: the menu offers the
    folder instead, and the disc is still a disc.
    """
    folder = Path(folder)
    info = GameInfo(source="Files")
    if not folder.is_dir():
        info.msg = "Folder not found."
        return info

    files = sorted(_all_files(folder), key=lambda p: str(p).casefold())
    if not files:
        info.msg = "This folder has no files in it."
        return info

    info.folder = folder.resolve()
    info.files = files
    info.total_bytes = sum(p.stat().st_size for p in files)
    info.max_file_bytes = max(p.stat().st_size for p in files)

    exe = Path(installer_path) if installer_path else None
    if exe is not None and exe.is_file():
        info.setup_exe = exe
        # The name the installer gives itself, as for a GOG download, and the
        # folder's own name when it gives none. A portable game's exe usually
        # carries the game's name; an unpacked archive usually does not.
        name = (product_name(exe) or "").strip()
        info.game_name = name or info.folder.name
    else:
        info.game_name = info.folder.name
    # Nothing registers these the way GOG's installers do, so Play has nothing to
    # look for. Set anyway rather than left empty: a disc whose files a GOG
    # installer once put there is still findable, and an empty match makes the
    # menu search for nothing and light Play up for every game it finds.
    info.match_name = info.game_name
    info.ok = True
    return info


def _all_files(folder: Path) -> list[Path]:
    """Every file under a folder, at any depth.

    A symlink to a file is one of them, and goes on the disc as the bytes it
    points at, which is what Windows does with a junction. A symlink to a folder
    is not walked into: rglob does not follow one, and a game folder linking back
    to its own parent would otherwise be walked until the path ran out.
    """
    return [p for p in folder.rglob("*") if p.is_file()]


def add_on_name(file_name: str) -> str:
    """The name an add-on gets on the menu, from its file name.

    Not from its version resource, which is where a game's name comes from:
    every GOG patch reports the base game's own name there, so four Hollow Knight
    patches would all be called "Hollow Knight".
    """
    raw = Path(file_name).stem
    name = raw
    # patch_<game>_<from>_to_<to>: the version being moved TO is what tells two
    # patches apart, and it belongs at the front, where the menu button, which
    # clips at about twenty characters, will not cut it off.
    if m := re.match(r"^patch_.+_to_(.+)$", name, flags=re.IGNORECASE):
        name = "Update " + m.group(1)
    elif m := re.match(r"^setup_(.+)$", name, flags=re.IGNORECASE):
        name = m.group(1)
    name = re.sub(r"\s+", " ", name.replace("_", " ")).strip()
    # A file name that is nothing but underscores leaves an empty label, and a
    # button with no text is a button nobody can identify.
    return name or raw.strip() or "Add-on"


def add_on_info(exe_path: str | Path) -> GameInfo:
    """One add-on installer: DLC, an expansion, a patch or a mod.

    Picked as a file rather than a folder, because an add-on usually sits in the
    same folder as the game it belongs to and pointing at the folder would just
    find the game again. Any .exe will do: GOG names its patches patch_*, and a
    mod is named whatever its author chose.
    """
    path = Path(exe_path)
    info = GameInfo(kind="AddOn")
    if not path.is_file():
        info.msg = "File not found."
        return info
    if path.suffix.casefold() != ".exe":
        info.msg = (f"An add-on has to be an installer (.exe). '{path.name}' is not one - "
                    "loose files belong in the disc's extra content.")
        return info
    _installer_facts(info, path)
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

    if info.kind == "AddOn":
        name = add_on_name(exe.name)
    else:
        name = product_name(exe)
        if not name or not name.strip():
            name = _fallback_name(exe)
        # Inno pads its version strings with trailing spaces; untrimmed they leak
        # into folder names ("...Edition                    Disc").
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
