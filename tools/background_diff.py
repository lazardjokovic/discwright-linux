"""How far apart are this tool's menu backgrounds and the Windows app's?

Composes every case the Windows app composed (tools/windows/
Make-BackgroundReference.ps1) and prints, for each, the mean and worst
per-channel difference, and where the title's ink sits in both. Used to choose
the tolerances in tests/test_background.py from a measurement rather than a
guess, and worth re-running if Pillow is upgraded.

    python tools/background_diff.py
"""

from __future__ import annotations

import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "src"))
from discwright.background import compose_background  # noqa: E402

FIX = ROOT / "tests" / "fixtures" / "background"
WINDOWS = FIX / "windows-0.7.4"

# name: (source picture, title, panel side, divider, title shown). The Windows
# script composes the same list; keep the two in step.
CASES = {
    "wide-right": ("wide.png", "", "Right", False, False),
    "wide-left": ("wide.png", "", "Left", False, False),
    "wide-left-divider": ("wide.png", "", "Left", True, False),
    "narrow-right": ("narrow.png", "", "Right", False, False),
    "narrow-left": ("narrow.png", "", "Left", False, False),
    "wide-right-title": ("wide.png", "ALAN WAKE", "Right", False, True),
    "wide-left-long-title": ("wide.png", "THE WITCHER ENHANCED EDITION DIRECTORS CUT", "Left", False, True),
    "wide-right-no-title": ("wide.png", "ALAN WAKE", "Right", False, False),
}

# The band the title can occupy: the top of the menu, across the artwork side.
TITLE_BAND_HEIGHT = 80


def compose(name: str, out: Path) -> Path:
    src, title, side, divider, show = CASES[name]
    compose_background(FIX / src, title, out, side, divider, show)
    return out


def untitled(name: str) -> str:
    """The case with the same picture and panel and no title, to find the ink by."""
    src, _, side, divider, _ = CASES[name]
    return next(n for n, c in CASES.items() if c == (src, "", side, divider, False))


def title_ink(titled: Image.Image, plain: Image.Image) -> tuple[int, int, int, int] | None:
    """Where the title's bright ink sits: pixels much lighter than the same pixel
    without a title. The shadow only darkens, so it is not counted."""
    a, b = titled.convert("L"), plain.convert("L")
    lighter = ImageChops.subtract(a, b).point(lambda v: 255 if v > 60 else 0)
    return lighter.getbbox()


def outside_title(im: Image.Image) -> Image.Image:
    """The picture with the title band blanked, so two pictures whose titles are
    in different fonts can still be compared everywhere else."""
    im = im.copy()
    im.paste((0, 0, 0, 0), (0, 0, im.width, TITLE_BAND_HEIGHT))
    return im


def half_pixel_lines(side: str) -> list[tuple[int, int, int, int]]:
    """Where Windows DiscWright 0.7.4 and earlier darken only half as much as
    they should: the top row and the left column of the whole picture, and the
    first column of the button panel. GDI+ antialiases its rectangle fills with
    pixel centres on whole numbers, so a rectangle starting at 0 covers half of
    pixel 0. On bright artwork that is a visible light line along the top of the
    panel. This port darkens them fully; comparisons leave them out rather than
    hold it to Windows' defect."""
    panel_start = 290 if side.casefold() == "left" else 470
    return [(0, 0, 760, 1), (0, 0, 1, 480), (panel_start, 0, panel_start + 1, 480)]


def without(im: Image.Image, boxes) -> Image.Image:
    im = im.copy()
    for box in boxes:
        im.paste((0, 0, 0, 0), box)
    return im


def difference(a: Image.Image, b: Image.Image) -> tuple[float, int]:
    flat = ImageChops.difference(a, b).tobytes()
    return sum(flat) / len(flat), max(flat)


def main() -> None:
    with tempfile.TemporaryDirectory() as t:
        ours = {n: Image.open(compose(n, Path(t) / f"{n}.png")).convert("RGBA") for n in CASES}
        theirs = {n: Image.open(WINDOWS / f"{n}.png").convert("RGBA") for n in CASES}
        print("                         whole picture       without Windows' half-pixel lines")
        print("                                                  and the title band")
        for n, c in CASES.items():
            m1, w1 = difference(ours[n], theirs[n])
            skip = half_pixel_lines(c[2])
            m2, w2 = difference(without(outside_title(ours[n]), skip),
                                without(outside_title(theirs[n]), skip))
            print(f"  {n:22s} mean {m1:5.2f} worst {w1:3d}      mean {m2:5.2f} worst {w2:3d}")
        print("\n  title ink (left, top, right, bottom)")
        for n, c in CASES.items():
            if c[4]:
                plain = untitled(n)
                print(f"  {n:22s} ours {title_ink(ours[n], ours[plain])}"
                      f"   windows {title_ink(theirs[n], theirs[plain])}")


if __name__ == "__main__":
    main()
