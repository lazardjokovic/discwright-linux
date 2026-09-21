"""Where everything goes on the disc.

Ported from the layout helpers in DiscWright.ps1: Get-DiscIconName,
Test-ReservedDiscName, Get-GameFolderName, Get-EntryParentIndex,
Get-DiscGameIndexes, Get-DiscEntryFolder, Get-DiscEntrySetup,
Get-DiscEntryExtras and Get-MenuGames.

Pure rules, no files touched. Paths returned here are paths **on the disc**,
relative to its root and written with backslashes, because the menu that uses
them runs on Windows. The staging copy turns them into real paths.

Everything that compares names here ignores case, as Windows does. A disc is
read on Windows as well as Linux, and two names that differ only in case are the
same name there.
"""

from __future__ import annotations

import hashlib
import re
import unicodedata
from pathlib import PureWindowsPath
from typing import Sequence

from .games import GameInfo

PROJECT_FILE = "discproject.json"


def ascii_fold(value: str | None) -> str:
    """Accents stripped down to the plain letter underneath: a Polish z-acute
    becomes z, a German U-umlaut becomes U. Alphabets with no Latin equivalent
    (Cyrillic, Greek, CJK) come back untouched, which is what callers test for.
    """
    if not value:
        return ""
    kept = "".join(c for c in unicodedata.normalize("NFD", value)
                   if unicodedata.category(c) != "Mn")
    return unicodedata.normalize("NFC", kept)


def disc_icon_name(label: str | None) -> str:
    """The icon's file name at the disc root, named after the disc.

    Every disc used to ship its icon as disc.ico. Explorer caches icon bitmaps by
    path, so E:\\disc.ico was the same cache key for every disc that ever went
    through that drive letter, and a new disc would be drawn with the previous
    one's icon. Naming it after the disc gives each disc its own key.
    """
    if not label or not label.strip():
        return "disc.ico"
    # Fold before stripping, or an accented letter is deleted outright: the
    # Polish spelling of "Wiedzmin" used to come out as "Wiedmin".
    name = re.sub(r"[^A-Za-z0-9]", "", ascii_fold(label))[:40]
    if not name:
        # A label with no Latin letters or digits at all fell back to "disc.ico",
        # the shared cache key this rule exists to avoid. Hash the label instead,
        # so two Cyrillic discs in the same drive still get different names.
        name = "disc" + hashlib.md5(label.encode("utf-8")).digest()[:4].hex()
    return name + ".ico"


def is_reserved_name(name: str, icon_name: str = "disc.ico") -> bool:
    """Names the build owns at the disc root, which extra content may not use.

    disc.ico stays reserved even when the icon is named after the disc, so extra
    content cannot collide with discs built before it was.
    """
    folded = name.casefold()
    if folded.startswith("setup_"):
        return True
    png = str(PureWindowsPath(icon_name).with_suffix(".png"))
    reserved = {"autorun.inf", ".xdg-volume-info", "disc.ico", "disc.png", icon_name, png,
                "AUTORUN", "Extras", "Games", "Add-ons", PROJECT_FILE}
    return folded in {r.casefold() for r in reserved}


def game_folder_name(index: int, name: str | None) -> str:
    """Folder name for one entry: '01 - Hollow Knight'.

    Numbered, so the menu's order and the disc's own listing agree when someone
    browses it by hand. Folded to plain ASCII for the same reason the volume id
    is: a disc legible in every file manager is worth more than an exact title.
    """
    n = re.sub(r"[^A-Za-z0-9 _\-.]", " ", ascii_fold(name or ""))
    n = re.sub(r"\s+", " ", n).strip(" .")
    if len(n) > 48:
        # Trimmed of dots again after cutting, not only before. Windows silently
        # drops a trailing dot from a folder name, so a cut that lands on one
        # makes the folder on the disc and the path the menu uses disagree, and
        # that game's Install button points at nothing. Measured: the Windows app
        # 0.7.2 still cuts without re-trimming.
        n = n[:48].strip(" .")
    return f"{index:02d} - {n or 'Game'}"


def entry_parent_index(entries: Sequence[GameInfo], index: int) -> int:
    """The game an add-on belongs to, or -1 for an entry the menu shows as a game.

    An add-on whose parent is missing, is itself, or is another add-on is
    promoted to a game rather than dropped: a disc showing an unexpected entry is
    recoverable, one that silently leaves out an installer somebody paid for and
    burned is not. The menu and the layout both ask this, and they must get the
    same answer, so it is decided here and only here.
    """
    e = entries[index]
    if e.kind != "AddOn":
        return -1
    p = e.parent_index
    if p < 0 or p >= len(entries) or p == index or entries[p].kind == "AddOn":
        return -1
    return p


def disc_game_indexes(entries: Sequence[GameInfo]) -> list[int]:
    """The entries shown as games, in menu order. What the folder numbers count."""
    return [i for i in range(len(entries)) if entry_parent_index(entries, i) < 0]


def disc_entry_folder(entries: Sequence[GameInfo], index: int) -> str:
    """Where an entry's installer sits on the disc; '' is the root itself.

    One game keeps the flat layout every disc has always had, installer at the
    root. Two or more and each moves into a numbered folder under Games\\,
    because a flat root holding a dozen setup files is unreadable. An add-on goes
    inside its own game's folder, under Add-ons\\, numbered within that game. The
    numbers count games, not entries, so they do not skip past the patches.

    A game with add-ons and no second game is still one game, so it keeps the
    flat root, with Add-ons\\ beside the installer.
    """
    parent = entry_parent_index(entries, index)
    if parent < 0:
        games = disc_game_indexes(entries)
        if len(games) <= 1:
            return ""
        n = games.index(index) + 1
        return str(PureWindowsPath("Games", game_folder_name(n, entries[index].game_name)))

    n = 0
    for j in range(len(entries)):
        if entry_parent_index(entries, j) != parent:
            continue
        n += 1
        if j == index:
            break
    base = disc_entry_folder(entries, parent)
    under = PureWindowsPath(base, "Add-ons") if base else PureWindowsPath("Add-ons")
    return str(under / game_folder_name(n, entries[index].game_name))


def disc_entry_setup(entries: Sequence[GameInfo], index: int) -> str:
    """The installer's path on the disc, as the menu will launch it."""
    folder = disc_entry_folder(entries, index)
    name = entries[index].setup_exe.name
    return str(PureWindowsPath(folder, name)) if folder else name


def disc_entry_extras(entries: Sequence[GameInfo], index: int) -> str:
    """Where an entry's own manual and extras go: beside its installer. A disc
    holding one game keeps the flat Extras\\ at the root."""
    folder = disc_entry_folder(entries, index)
    return str(PureWindowsPath(folder, "Extras")) if folder else "Extras"


def entry_add_ons(entries: Sequence[GameInfo], index: int) -> list[int]:
    """The entries whose stored parent is this one. Not the same question as the
    menu's: this counts add-ons the menu may have promoted away."""
    return [i for i, e in enumerate(entries) if e.parent_index == index]


def remove_entry(entries: Sequence[GameInfo], index: int,
                 with_add_ons: bool = False) -> list[GameInfo]:
    """The entries with one taken out, and optionally its add-ons. Ported from
    Remove-GameEntry.

    Parents are stored as positions, so taking an entry out renumbers every one
    after it. Removing the second of four would otherwise silently re-point an
    add-on that belonged to the fourth at the third: a disc that builds cleanly
    with the DLC filed under the wrong game. So the shift is made here, in one
    place, rather than left to whoever removes.

    An add-on whose own parent is removed becomes a game of its own. The
    alternative, deleting it too, throws away an installer somebody chose
    without asking them.
    """
    if index < 0 or index >= len(entries):
        return list(entries)
    drop = {index}
    if with_add_ons:
        drop.update(entry_add_ons(entries, index))
    # Old position to new, built before anything is rewritten: with more than one
    # entry going, "one less if the parent sat after the removed row" no longer
    # holds.
    new_index = {}
    for i in range(len(entries)):
        if i not in drop:
            new_index[i] = len(new_index)
    kept = []
    for i, e in enumerate(entries):
        if i in drop:
            continue
        if e.parent_index >= 0:
            if e.parent_index in drop:
                e.kind, e.parent_index = "Game", -1
            else:
                e.parent_index = new_index[e.parent_index]
        kept.append(e)
    return kept


def menu_games(entries: Sequence[GameInfo]) -> list[dict]:
    """The menu's view of the disc: games in order, each with its add-ons, and
    every path taken from the layout, so the menu and the disc cannot disagree."""
    out = []
    for i, e in enumerate(entries):
        if entry_parent_index(entries, i) >= 0:
            continue
        add_ons = [{"name": entries[j].game_name, "setup": disc_entry_setup(entries, j)}
                   for j in range(len(entries)) if entry_parent_index(entries, j) == i]
        # An entry's own manual and extras, as paths on the disc. Empty means it
        # has none of its own and the menu falls back to the disc-wide ones.
        manual = extras = ""
        if e.manual_path or e.extras_path:
            extras = disc_entry_extras(entries, i)
            if e.manual_path:
                manual = str(PureWindowsPath(extras, PureWindowsPath(str(e.manual_path)).name))
        out.append({
            "name": e.game_name,
            # A project from before match names existed has none; the game's name
            # is the best guess left.
            "match_name": e.match_name or e.game_name,
            "setup": disc_entry_setup(entries, i),
            "add_ons": add_ons,
            "manual": manual,
            "extras": extras,
        })
    return out
