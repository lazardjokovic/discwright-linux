"""autorun.inf: how Windows learns the disc's icon, label and menu.

Ported from New-AutorunInf in DiscWright.ps1. tests/test_autorun.py compares the
result byte for byte against a file Windows DiscWright 0.7.2 wrote.
"""

from __future__ import annotations

from .text import encode_ansi, remove_control_chars


def autorun_inf(label: str, icon_name: str, menu: bool) -> bytes:
    """The exact bytes of autorun.inf.

    CRLF line endings with a trailing CRLF, in the ANSI codepage and with no
    byte order mark: AutoRun reads the file in the system codepage and does not
    understand a BOM. See encode_ansi for what that costs a label.
    """
    label = remove_control_chars(label) or ""
    icon_name = remove_control_chars(icon_name) or ""

    lines = ["[autorun]"]
    if menu:
        lines.append("shellexecute=AUTORUN\\menu.hta")
    lines.append(f"icon={icon_name}")
    lines.append(f"label={label}")
    if menu:
        lines.append(f"action=Run {label}")
    lines += ["", "[Content]", "MusicFiles=false", "PictureFiles=false", "VideoFiles=false"]
    return encode_ansi("\r\n".join(lines) + "\r\n")
