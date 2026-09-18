"""Are the edge pixels of each icon frame opaque?

The source picture is fully opaque except for one half-transparent band along the
top, so every other edge pixel of a correct icon should have alpha 255. GDI+
samples past the edge of the source while scaling and blends with transparency,
so Windows' frames were suspected of a faint see-through border. This counts it.

    python tools/icon_edges.py
"""

from __future__ import annotations

import sys
import tempfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "src"))
sys.path.insert(0, str(ROOT / "tools"))
from discwright.icons import convert_to_ico, convert_to_png  # noqa: E402
from icon_diff import ico_frames  # noqa: E402

FIX = ROOT / "tests" / "fixtures" / "icons"


def edge_alpha(im: Image.Image) -> tuple[int, int]:
    """Lowest alpha on the left, right and bottom edges, and how many of those
    pixels are not fully opaque. The top edge is skipped: the source is meant to
    be half-transparent there."""
    a = im.getchannel("A")
    w, h = im.size
    edge = [a.getpixel((0, y)) for y in range(h)] + [a.getpixel((w - 1, y)) for y in range(h)]
    edge += [a.getpixel((x, h - 1)) for x in range(w)]
    return min(edge), sum(1 for v in edge if v < 255)


def main() -> None:
    with tempfile.TemporaryDirectory() as t:
        ico, png = Path(t) / "ours.ico", Path(t) / "ours.png"
        convert_to_ico(FIX / "source.png", ico)
        convert_to_png(FIX / "source.png", png)
        ours, theirs = ico_frames(ico), ico_frames(FIX / "windows-0.7.3" / "source.ico")
        print("                 this tool           Windows")
        for s in sorted(theirs):
            (om, oc), (tm, tc) = edge_alpha(ours[s]), edge_alpha(theirs[s])
            print(f"  ico {s:3d}px   min alpha {om:3d}, {oc:3d} soft   min alpha {tm:3d}, {tc:3d} soft")
        (om, oc) = edge_alpha(Image.open(png).convert("RGBA"))
        (tm, tc) = edge_alpha(Image.open(FIX / "windows-0.7.3" / "source.png").convert("RGBA"))
        print(f"  png 256px   min alpha {om:3d}, {oc:3d} soft   min alpha {tm:3d}, {tc:3d} soft")


if __name__ == "__main__":
    main()
