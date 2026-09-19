"""Copy the menu's template out of the Windows app, verbatim.

The menu is one HTML Application, a few hundred lines of markup, CSS and JScript
with a dozen %%PLACEHOLDERS%%, held in New-MenuHta in DiscWright.ps1 as a
PowerShell here-string. This port does not rewrite it: it carries the same text
in src/discwright/menu.hta.in, lifted out of the Windows script by this tool, so
a disc built on either machine runs the same menu.

    python tools/menu_template.py path/to/DiscWright.ps1          # write it
    python tools/menu_template.py path/to/DiscWright.ps1 --check  # compare only

Run it whenever the Windows menu changes, then regenerate the references with
tools/windows/Make-MenuReference.ps1 and run the tests.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

TEMPLATE = Path(__file__).resolve().parent.parent / "src" / "discwright" / "menu.hta.in"


def extract(script: str) -> str:
    """The here-string assigned to $tpl inside New-MenuHta, with LF line ends."""
    script = script.replace("\r\n", "\n")
    start = script.index("function New-MenuHta(")
    open_tag = script.index("$tpl = @'\n", start) + len("$tpl = @'\n")
    close_tag = script.index("\n'@\n", open_tag)
    return script[open_tag:close_tag] + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("script", type=Path)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    text = extract(args.script.read_text(encoding="utf-8-sig"))
    if args.check:
        same = TEMPLATE.read_text(encoding="ascii") == text
        print("the template matches" if same else "the template differs from the Windows one")
        return 0 if same else 1
    TEMPLATE.write_text(text, encoding="ascii", newline="\n")
    print(f"wrote {TEMPLATE}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
