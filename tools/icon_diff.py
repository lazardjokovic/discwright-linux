"""How far apart are this tool's icons and the Windows app's, frame by frame?

Prints the mean and worst per-channel difference for every frame of the .ico and
for the Linux PNG, against the files Windows DiscWright made from the same
source. Used to choose the tolerance in tests/test_icons.py from a measurement
rather than a guess, and worth re-running if Pillow is upgraded.

    python tools/icon_diff.py
"""

from __future__ import annotations

import io
import struct
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "src"))
from discwright.icons import convert_to_ico, convert_to_png  # noqa: E402

FIX = ROOT / "tests" / "fixtures" / "icons"


def ico_frames(path: Path) -> dict[int, Image.Image]:
    """Every frame of an .ico, decoded without Pillow's ICO reader, so the frames
    compared are the ones stored rather than Pillow's interpretation of them."""
    data = path.read_bytes()
    _, _, count = struct.unpack_from("<HHH", data, 0)
    out = {}
    for i in range(count):
        w, h, _, _, _, bpp, size, offset = struct.unpack_from("<BBBBHHII", data, 6 + 16 * i)
        w = w or 256
        blob = data[offset:offset + size]
        if blob[:8] == b"\x89PNG\r\n\x1a\n":
            out[w] = Image.open(io.BytesIO(blob)).convert("RGBA")
        else:
            pix = blob[40:40 + w * w * 4]
            rows = [pix[r * w * 4:(r + 1) * w * 4] for r in range(w)][::-1]
            out[w] = Image.frombytes("RGBA", (w, w), b"".join(rows), "raw", "BGRA")
    return out


def premultiplied(im: Image.Image) -> Image.Image:
    """Colour weighted by alpha: what a pixel actually contributes on screen. A
    half-transparent pixel's raw colour can differ between two libraries that
    composite differently and look identical once drawn."""
    r, g, b, a = im.split()
    mul = lambda c: ImageChops.multiply(c, a)  # noqa: E731  (c * a / 255)
    return Image.merge("RGBA", (mul(r), mul(g), mul(b), a))


def diff(a: Image.Image, b: Image.Image) -> tuple[float, int, tuple[int, int]]:
    d = ImageChops.difference(a, b)
    flat = d.tobytes()
    worst = max(flat)
    at = flat.index(worst) // 4
    return sum(flat) / len(flat), worst, (at % a.width, at // a.width)


def main() -> None:
    with tempfile.TemporaryDirectory() as t:
        ico, png = Path(t) / "ours.ico", Path(t) / "ours.png"
        convert_to_ico(FIX / "source.png", ico)
        convert_to_png(FIX / "source.png", png)
        ours, theirs = ico_frames(ico), ico_frames(FIX / "windows-0.7.3" / "source.ico")
        pairs = [(f"ico {s:3d}px", ours[s], theirs[s]) for s in sorted(theirs)]
        pairs.append(("png 256px", Image.open(png).convert("RGBA"),
                      Image.open(FIX / "windows-0.7.3" / "source.png").convert("RGBA")))
        print("                whole frame                 inside the outermost pixel")
        for name, a, b in pairs:
            m1, w1, at1 = diff(a, b)
            inner = (1, 1, a.width - 1, a.height - 1)
            m2, w2, at2 = diff(a.crop(inner), b.crop(inner))
            print(f"  {name}   mean {m1:5.2f} worst {w1:3d} at {at1}"
                  f"   mean {m2:5.2f} worst {w2:3d} at {at2}")


if __name__ == "__main__":
    main()
