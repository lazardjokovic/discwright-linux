"""The discwright command.

Only --version for now. The build command arrives when the disc it builds can be
shown to match the Windows one; see docs/xorriso-spike.md.
"""

from __future__ import annotations

import argparse

from . import __version__


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="discwright",
        description="Turn GOG offline installers into burnable game discs.",
    )
    parser.add_argument("--version", action="version", version=f"DiscWright {__version__}")
    parser.parse_args(argv)
    parser.print_help()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
