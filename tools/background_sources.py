"""Make the two source pictures the menu background tests compose.

They are drawn rather than photographed so the repo carries no artwork it does
not own, and so they can be remade exactly. Smooth gradients show a scaling
error as a shift in colour; the rings and thin lines show one as a shift in
position. Each has a different shape, so between them the picture overflows the
760x480 menu sideways in one and vertically in the other.

    python tools/background_sources.py

The Windows app then composes both (tools/windows/Make-BackgroundReference.ps1)
and its output is what tests/test_background.py compares against.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "tests" / "fixtures" / "background"


def picture(w: int, h: int) -> Image.Image:
    im = Image.new("RGB", (w, h))
    px = im.load()
    for y in range(h):
        for x in range(w):
            px[x, y] = (40 + 180 * x // w, 60 + 150 * y // h, 200 - 120 * x // w)
    d = ImageDraw.Draw(im)
    for i in range(6):
        r = (i + 1) * min(w, h) // 14
        d.ellipse((w // 3 - r, h // 2 - r, w // 3 + r, h // 2 + r), outline=(250, 240, 200), width=3)
    for x in range(0, w, w // 12):
        d.line((x, 0, x + h // 3, h), fill=(20, 20, 30), width=1)
    d.rectangle((w - w // 5, h // 8, w - w // 12, h // 3), fill=(230, 60, 40))
    return im


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    picture(1000, 480).save(OUT / "wide.png")
    picture(400, 300).save(OUT / "narrow.png")
