""".xdg-volume-info: the Linux half of the disc's identity.

autorun.inf is a Windows file and no Linux desktop reads it. GIO (GNOME, and
anything else built on gvfs) reads .xdg-volume-info at the root of a mounted
volume instead, and shows the name and icon it names. Ported from
New-XdgVolumeInfo in DiscWright.ps1.
"""

from __future__ import annotations

from .text import remove_control_chars


def _escape(value: str) -> str:
    # GKeyFile escapes with backslashes, so a literal backslash has to be doubled
    # or it silently eats the character after it.
    return value.replace("\\", "\\\\")


def xdg_volume_info(label: str, png_name: str) -> bytes:
    """The exact bytes of .xdg-volume-info.

    UTF-8 with no BOM, and LF. GKeyFile treats a BOM as part of the first group
    name, so a BOM makes the whole file parse as nothing.
    """
    label = remove_control_chars(label) or ""
    png_name = remove_control_chars(png_name) or ""
    lines = [
        "[Volume Info]",
        f"Name={_escape(label)}",
        f"IconFile={_escape(png_name)}",
    ]
    return ("\n".join(lines) + "\n").encode("utf-8")
