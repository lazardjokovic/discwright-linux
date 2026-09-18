"""Everything a build needs to know, in one place.

The fields mirror the Windows app's build settings and its project file
(discproject.json, schema 8), so a project saved on either machine can be built
on the other. Reading and writing that file comes in its own module.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

from .games import GameInfo


@dataclass
class DiscSettings:
    games: list[GameInfo]
    label: str
    out_dir: Path
    icon_path: Path | None = None
    icon_is_ico: bool = True
    menu: bool = True
    bg_path: Path | None = None
    bg_as_is: bool = False
    panel_side: str = "Right"
    divider: bool = False
    show_title: bool = False
    title_text: str = ""
    window_border: bool = True
    button_style: str = "Minimal"
    music_file: Path | None = None
    buttons: list[str] = field(default_factory=lambda: ["Play", "Install", "Exit"])
    # The disc-wide manual and extras. A game can also carry its own, on its
    # GameInfo; one that has none falls back to these.
    manual_path: Path | None = None
    extras_path: Path | None = None
    # Loose files and folders copied to the disc root under their own names.
    extra_items: list[Path] = field(default_factory=list)
    media_key: str = ""
    linux_info: bool = False
    legacy_fs: bool = False
