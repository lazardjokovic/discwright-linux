"""Text rules every file on the disc shares.

Ported from DiscWright.ps1 (Remove-ControlChars, Get-VolumeLabel). The Windows
app is the reference: when the two disagree, this file is the one that is wrong.
"""

from __future__ import annotations

import re
import unicodedata

_CONTROL = re.compile(r"[\x00-\x1f\x7f]")
_NOT_VOLUME_ID = re.compile(r"[^A-Za-z0-9_]")


def remove_control_chars(value: str | None) -> str | None:
    """Drop every control character.

    Everything a build writes goes into a line-based or a quoted format.
    autorun.inf is one directive per line, so a label carrying a newline would
    write further directives of its own::

        label=My Game
        open=Extras\\payload.exe        <- came from inside the "label"

    and in the menu's JScript a newline is an unterminated string literal, so
    the whole menu fails to parse. Project files get passed around, so this is a
    filter on what a file may carry, not on what may be typed.
    """
    if not value:
        return value
    return _CONTROL.sub("", value)


def volume_label(label: str | None) -> str:
    """The volume id written into the ISO: at most 16 characters of A-Z, a-z,
    0-9 and underscore.

    This is not what Windows shows for the drive. Explorer shows the full label
    from autorun.inf; the volume id is what is left when nothing reads that file.
    """
    base = _NOT_VOLUME_ID.sub("_", label or "").strip("_")
    # Trimmed again after cutting, not only before: cutting at 16 can land on a
    # folded space and leave 'THE WITCHER ENH EDITION' as 'THE_WITCHER_ENH_'.
    if len(base) > 16:
        base = base[:16].rstrip("_")
    return base or "DISC"


def encode_ansi(value: str) -> bytes:
    """Encode for Windows AutoRun, which reads autorun.inf in the system ANSI
    codepage and has no Unicode mode at all.

    Windows DiscWright writes with the machine's ANSI codepage, which is
    Windows-1252 on English and most Western European installs, and Windows'
    best-fit mapping turns what 1252 cannot hold into the nearest letter: a
    Polish z-acute arrives as a plain z rather than a question mark.

    Python has no best-fit codec, so this approximates it: a character 1252
    cannot hold is decomposed and its accents dropped, and only what still does
    not fit becomes a question mark. Windows' tables map a few more things than
    this does; measure against a real Windows build before relying on anything
    unusual.
    """
    out = bytearray()
    for ch in value:
        try:
            out += ch.encode("cp1252")
            continue
        except UnicodeEncodeError:
            pass
        base = "".join(c for c in unicodedata.normalize("NFKD", ch)
                       if not unicodedata.combining(c))
        try:
            out += base.encode("cp1252") if base else b"?"
        except UnicodeEncodeError:
            out += b"?"
    return bytes(out)
