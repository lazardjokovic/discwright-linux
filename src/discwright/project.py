"""discproject.json: the settings of a disc, saved beside its ISO.

Ported from Save-Project and Import-Project in DiscWright.ps1. The file is
shared with the Windows app: one saved there opens here and the other way
round, so the same disc can be rebuilt on either machine.

The schema is numbered separately from the app, and every older number still
opens. What each one added, from the Windows source:

    1  one game, in SourceFolder
    2  the Games array
    3  Kind and Parent, so a game can carry add-ons
    4  each entry's own Manual and Extras
    5  MediaKey, the disc it was planned for
    6  MatchName, the name the menu looks for in the registry
    7  LinuxInfo, the disc's name and icon for a Linux desktop
    8  LegacyFs, ISO9660 and Joliet beside UDF

A key a file does not carry reads back as what the disc behaved like before that
key existed, rather than as today's default, so reopening an old project and
rebuilding produces the disc it produced before.

Written as UTF-8 **with** a byte order mark and CRLF line ends, which is what
Windows PowerShell 5.1 writes. The mark is not decoration there: without it,
PowerShell 5.1 reads the file in the machine's ANSI codepage, and a path with an
accent in it comes back mangled.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path, PureWindowsPath

from .games import GameInfo, add_on_info, game_info
from .settings import DiscSettings

PROJECT_FILE = "discproject.json"
SCHEMA = 8


@dataclass
class ProjectEntry:
    """One game or add-on as the file names it, before anything is read off disk."""
    folder: str | None = None
    kind: str = "Game"
    parent_index: int = -1
    setup: str | None = None
    name: str | None = None
    match_name: str | None = None
    manual: str | None = None
    extras: str | None = None


@dataclass
class Project:
    entries: list[ProjectEntry] = field(default_factory=list)
    label: str = ""
    icon_path: str | None = None
    icon_is_ico: bool = False
    menu: bool = False
    bg_path: str | None = None
    bg_as_is: bool = False
    panel_side: str = "Right"
    divider: bool = False
    show_title: bool = False
    title_text: str = ""
    window_border: bool = False
    # Absent in a file old enough not to have it, and those discs had bordered
    # buttons. New projects write what the disc was built with.
    button_style: str = "Bordered"
    music_file: str | None = None
    buttons: list[str] = field(default_factory=list)
    manual_path: str | None = None
    extras_path: str | None = None
    extra_items: list[str] = field(default_factory=list)
    media_key: str = ""
    linux_info: bool = False
    legacy_fs: bool = False
    out_dir: str | None = None
    schema: int = 0
    app_version: str = ""


def _text(value) -> str | None:
    return None if value is None else str(value)


def save_project(s: DiscSettings, out_dir: str | Path) -> Path:
    """Write discproject.json into out_dir, and return its path."""
    out_dir = Path(out_dir)
    games = list(s.games)
    doc = {
        # Version is this file's schema; AppVersion is the DiscWright that wrote
        # it. Two things, deliberately two keys.
        "Version": SCHEMA,
        "AppVersion": _app_version(),
        "SavedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S"),
        # Schema 1 knew one game and stored it here. Both keys are still written,
        # pointing at the first game, so this file still opens in a 0.1.x app.
        "SourceFolder": _text(games[0].folder) if games else None,
        "GameName": games[0].game_name if games else None,
        "Games": [{
            "Folder": _text(g.folder),
            "GameName": g.game_name,
            "MatchName": g.match_name,
            # The installer itself, not just its folder: an add-on shares a
            # folder with the game it belongs to, so re-detecting from the
            # folder would find the game again.
            "Setup": _text(g.setup_exe),
            "Kind": "AddOn" if g.kind == "AddOn" else "Game",
            "Parent": int(g.parent_index),
            "Manual": _text(g.manual_path),
            "Extras": _text(g.extras_path),
        } for g in games],
        "Label": s.label,
        "IconPath": _text(s.icon_path),
        "IconIsIco": bool(s.icon_is_ico),
        "Menu": bool(s.menu),
        "BgPath": _text(s.bg_path),
        "BgAsIs": bool(s.bg_as_is),
        "PanelSide": s.panel_side,
        "Divider": bool(s.divider),
        "ShowTitle": bool(s.show_title),
        "TitleText": s.title_text,
        "WindowBorder": bool(s.window_border),
        "ButtonStyle": s.button_style,
        "MusicFile": _text(s.music_file),
        "Buttons": list(s.buttons),
        "ManualPath": _text(s.manual_path),
        "ExtrasPath": _text(s.extras_path),
        "ExtraItems": [_text(p) for p in s.extra_items],
        "MediaKey": str(s.media_key or ""),
        "LinuxInfo": bool(s.linux_info),
        "LegacyFs": bool(s.legacy_fs),
        "OutDir": str(out_dir),
    }
    path = out_dir / PROJECT_FILE
    text = json.dumps(doc, indent=4, ensure_ascii=False).replace("\n", "\r\n") + "\r\n"
    path.write_bytes(b"\xef\xbb\xbf" + text.encode("utf-8"))
    return path


def _app_version() -> str:
    from . import __version__
    return __version__


def read_project(path: str | Path) -> Project | None:
    """Read a project file of any schema. None when it cannot be read at all,
    which is how the Windows app treats a file it cannot parse."""
    try:
        raw = Path(path).read_bytes().decode("utf-8-sig")
        j = json.loads(raw)
        if not isinstance(j, dict):
            return None
    except (OSError, ValueError):
        return None

    entries: list[ProjectEntry] = []
    for g in j.get("Games") or []:
        if not isinstance(g, dict) or not g.get("Folder"):
            continue
        entries.append(ProjectEntry(
            folder=str(g["Folder"]),
            kind="AddOn" if g.get("Kind") == "AddOn" else "Game",
            parent_index=int(g.get("Parent", -1)),
            setup=_text(g.get("Setup")),
            name=_text(g.get("GameName")),
            # Schema 6. Without it the name re-detected from the folder stands,
            # and that name is the registered one, so an old project renamed
            # under the old code repairs itself on open.
            match_name=_text(g.get("MatchName")),
            # Schema 4. Without them the entry has none of its own and the menu
            # falls back to the disc-wide manual and Extras, as those discs did.
            manual=_text(g.get("Manual")),
            extras=_text(g.get("Extras")),
        ))
    if not entries and j.get("SourceFolder"):
        entries = [ProjectEntry(folder=str(j["SourceFolder"]), name=_text(j.get("GameName")))]

    return Project(
        entries=entries,
        label=str(j.get("Label") or ""),
        icon_path=_text(j.get("IconPath")),
        icon_is_ico=bool(j.get("IconIsIco")),
        menu=bool(j.get("Menu")),
        bg_path=_text(j.get("BgPath")),
        bg_as_is=bool(j.get("BgAsIs")),
        panel_side=str(j.get("PanelSide") or "Right"),
        divider=bool(j.get("Divider")),
        show_title=bool(j.get("ShowTitle")),
        title_text=str(j.get("TitleText") or ""),
        window_border=bool(j.get("WindowBorder")),
        button_style=str(j.get("ButtonStyle") or "Bordered"),
        music_file=_text(j.get("MusicFile")),
        buttons=[str(b) for b in (j.get("Buttons") or [])],
        manual_path=_text(j.get("ManualPath")),
        extras_path=_text(j.get("ExtrasPath")),
        extra_items=[str(p) for p in (j.get("ExtraItems") or []) if p],
        media_key=str(j.get("MediaKey") or ""),
        linux_info=bool(j.get("LinuxInfo")),
        legacy_fs=bool(j.get("LegacyFs")),
        out_dir=_text(j.get("OutDir")),
        schema=int(j.get("Version") or 0),
        app_version=str(j.get("AppVersion") or ""),
    )


def _local(path: str | None) -> Path | None:
    """A path out of a project file, as this machine's. A project saved on
    Windows names Windows paths, and nothing here can guess where those folders
    are on a Linux machine; the path is kept as it was, and whatever reads it
    reports it missing by its own name."""
    if not path:
        return None
    return Path(PureWindowsPath(path).as_posix()) if "\\" in path else Path(path)


def settings_from_project(p: Project) -> tuple[DiscSettings, list[str]]:
    """The settings a project describes, with its games read off disk again, and
    a list of anything that could not be read. The folders are re-read rather
    than trusted, so a game that has moved or been deleted is noticed on open
    instead of halfway through a build."""
    games: list[GameInfo] = []
    problems: list[str] = []
    for e in p.entries:
        folder = _local(e.folder)
        setup = _local(e.setup)
        if e.kind == "AddOn" and setup is not None:
            info = add_on_info(setup)
        else:
            info = game_info(folder) if folder else GameInfo(msg="No folder.")
        if not info.ok:
            problems.append(f"{e.folder}: {info.msg}")
            continue
        info.kind = e.kind
        info.parent_index = e.parent_index
        # The name shown is the one the file carries, because it can be edited.
        # The name matched keeps whatever was detected when the file has none.
        if e.name:
            info.game_name = e.name
        if e.match_name:
            info.match_name = e.match_name
        info.manual_path = _local(e.manual)
        info.extras_path = _local(e.extras)
        games.append(info)

    s = DiscSettings(
        games=games,
        label=p.label,
        out_dir=_local(p.out_dir) or Path("."),
        icon_path=_local(p.icon_path),
        icon_is_ico=p.icon_is_ico,
        menu=p.menu,
        bg_path=_local(p.bg_path),
        bg_as_is=p.bg_as_is,
        panel_side=p.panel_side,
        divider=p.divider,
        show_title=p.show_title,
        title_text=p.title_text,
        window_border=p.window_border,
        button_style=p.button_style,
        music_file=_local(p.music_file),
        buttons=list(p.buttons),
        manual_path=_local(p.manual_path),
        extras_path=_local(p.extras_path),
        extra_items=[q for q in (_local(x) for x in p.extra_items) if q is not None],
        media_key=p.media_key,
        linux_info=p.linux_info,
        legacy_fs=p.legacy_fs,
    )
    return s, problems
