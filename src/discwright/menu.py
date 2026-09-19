"""The autorun menu: AUTORUN/menu.hta, the window a disc opens on Windows.

Ported from New-MenuHta, ConvertTo-JsString, ConvertTo-HtmlText and the two
escapes under them in DiscWright.ps1.

The menu itself is not ported, it is copied: menu.hta.in beside this file is
the Windows app's template, lifted out verbatim by tools/menu_template.py. What
this module does is what New-MenuHta does to that template: fill in the disc's
own games, buttons and names, escaped for wherever each one lands. The result
matches what Windows writes for the same disc byte for byte.
"""

from __future__ import annotations

import re
from importlib.resources import files
from typing import Sequence

from .text import remove_control_chars

# The Windows app keeps [A-Za-z0-9] with a case-insensitive .NET regex, which
# also matches these two, because they lower-case to ASCII there: the Kelvin
# sign to k, and the dotted capital I to i. They then reach the file as "?".
_NET_ALSO_KEEPS = "\u212a\u0130"


def _ascii_escape(s: str) -> str:
    """For JS string literals: \\uXXXX per UTF-16 unit, which is how JScript
    spells a surrogate pair anyway."""
    out = []
    for ch in s:
        if ord(ch) <= 0x7F:
            out.append(ch)
        else:
            data = ch.encode("utf-16-le")
            for i in range(0, len(data), 2):
                out.append("\\u%04x" % int.from_bytes(data[i:i + 2], "little"))
    return "".join(out)


def _ascii_entity(s: str) -> str:
    """For HTML markup: numeric entities, counted in code points, since half a
    surrogate pair is not a character an entity can name."""
    return "".join(ch if ord(ch) <= 0x7F else f"&#{ord(ch)};" for ch in s)


def html_text(s: str | None) -> str:
    """Text that lands in HTML markup: the title and the icon's name."""
    s = remove_control_chars(s)
    if not s:
        return ""
    s = s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")
    return _ascii_entity(s)


def js_string(s: str | None) -> str:
    """Text that lands inside a JS string literal. HTML entities are not decoded
    inside <script>, so these are backslash-escaped.

    Control characters go first, and by removal rather than escaping: a newline
    inside a JS string literal ends the literal, JScript refuses the whole file,
    and one bad name takes the entire menu with it. And < and > are escaped so a
    name cannot close the script block it sits in.
    """
    s = remove_control_chars(s)
    if not s:
        return ""
    s = s.replace("\\", "\\\\").replace('"', '\\"').replace("<", "\\x3c").replace(">", "\\x3e")
    return _ascii_escape(s)


def _games_js(games: Sequence[dict]) -> str:
    parts = []
    for g in games:
        add_ons = ",".join('{n:"' + js_string(a["name"]) + '",s:"' + js_string(a["setup"]) + '"}'
                           for a in g.get("add_ons", []))
        match = g.get("match_name") or g["name"]
        parts.append('{n:"' + js_string(g["name"]) + '",m:"' + js_string(match)
                     + '",s:"' + js_string(g["setup"])
                     + '",man:"' + js_string(g.get("manual") or "")
                     + '",ext:"' + js_string(g.get("extras") or "") + '",a:[' + add_ons + ']}')
    return "[" + ",".join(parts) + "]"


def menu_hta(label: str, games: Sequence[dict], buttons: Sequence[str], *,
             music_file: str = "", manual_file: str = "", panel_side: str = "Right",
             icon_name: str = "", window_border: bool = True,
             button_style: str = "Minimal") -> bytes:
    """The menu, as the bytes of menu.hta.

    games is layout.menu_games(): each game's name, the name it registers under,
    its installer and its own manual and extras as paths on the disc, and its
    add-ons. music_file and manual_file are bare names, of the files in AUTORUN
    and Extras. The window's panel of buttons is not laid out here: the menu
    renders it from this data when it runs, because which buttons show depends
    on the screen it is showing.
    """
    template = files("discwright").joinpath("menu.hta.in").read_text(encoding="ascii")
    panel_left = 20 if panel_side.casefold() == "left" else 490
    # Quirks-mode box model: width includes padding and border, so dropping the
    # 1px outline does not change a button's footprint.
    stage_border = "border:1px solid #00a6b0;" if window_border else "border:0;"
    btn_border = ("border:0;border-left:5px solid #00bec8;" if button_style.casefold() == "minimal"
                  else "border:1px solid #16545a;border-left:5px solid #00bec8;")
    # singleinstance="yes" keys off applicationname, so a name shared with another
    # disc means a second menu never opens: Windows refocuses the one running.
    app_name = "DiscMenu_" + "".join(c for c in (label or "") if c.isascii() and c.isalnum()
                                     or c in _NET_ALSO_KEEPS)
    # The taskbar icon is cached by path too, so it follows the disc's icon name.
    icon_file = icon_name or "disc.ico"
    values = {
        "APPNAME": app_name,
        "ICONFILE": html_text(icon_file),
        "PREVIEW": "false",
        "STAGEBORDER": stage_border,
        "BTNBORDER": btn_border,
        "TITLE": html_text(label),
        "PANELLEFT": str(panel_left),
        "GAMES": _games_js(games),
        "BTNS": "[" + ",".join('"' + js_string(b) + '"' for b in buttons) + "]",
        "MANUAL": js_string(manual_file),
        "MUSIC": js_string(music_file),
    }
    # One pass over the template, so text filled in is never filled in again.
    # Windows chains eleven replaces, and a game named "Game %%BTNS%% Edition"
    # gets the button list pasted into its name there, which ends the string
    # literal and stops the whole menu compiling.
    html = re.sub(r"%%([A-Z]+)%%", lambda m: values.get(m.group(1), m.group(0)), template)
    # Windows writes it with Set-Content -Encoding ASCII: CRLF, a CRLF at the end,
    # and a question mark for anything outside ASCII. The escapes above leave
    # nothing outside it except _NET_ALSO_KEEPS in the application name.
    return (html.replace("\n", "\r\n")).encode("ascii", errors="replace")
