"""The disc icon: checking a picked image, and turning it into the two icons a disc
carries, a multi-size .ico for Windows and a 256px PNG for Linux.

Ported from Test-IconInput, Test-BgInput, Convert-ToIco, Get-DibBytes and
Convert-ToPng in DiscWright.ps1.

The .ico is assembled by hand rather than by Pillow's ICO writer, to match the
Windows one frame for frame: seven sizes, the 256px frame stored as a PNG and
the rest as 32-bit bitmaps. Pillow would store every frame as a PNG, which Vista
and later read but older shells render poorly or not at all.

The bytes will not match Windows', and cannot: two image libraries resample and
compress the same picture differently. What must match is the structure, which
tests check exactly, and the picture, which they check to within a small
difference per pixel.
"""

from __future__ import annotations

import io
import struct
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, IcoImagePlugin, UnidentifiedImageError

ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)
PNG_SIZE = 256


@dataclass
class ImageCheck:
    ok: bool = False
    is_ico: bool = False
    width: int = 0
    height: int = 0
    msg: str = ""


def check_icon(path: str | Path) -> ImageCheck:
    """Is this usable as the disc icon? Ported from Test-IconInput."""
    path = Path(path)
    r = ImageCheck()
    if not path.is_file():
        r.msg = "File not found."
        return r
    try:
        with Image.open(path) as im:
            if path.suffix.casefold() == ".ico":
                sizes = sorted(im.info.get("sizes", {im.size}))
                r.is_ico = True
                r.width, r.height = im.size
                r.ok = True
                listed = ", ".join(f"{w}x{h}" for w, h in sizes)
                r.msg = f"Valid .ico ({listed}). Will be used as-is."
                return r
            r.width, r.height = im.size
    except (UnidentifiedImageError, OSError) as e:
        r.msg = f"Not a readable image: {e}"
        return r
    if r.width < 64 or r.height < 64:
        r.msg = f"Image is only {r.width}x{r.height} - too small (min 64, 256+ recommended)."
        return r
    r.ok = True
    square = r.width == r.height
    r.msg = (f"Image {r.width}x{r.height}"
             + (" - good." if square else " (not square - will be cropped to a square icon)")
             + (" [under 256px: may look soft]" if r.width < 256 else ""))
    return r


def check_background(path: str | Path | None) -> ImageCheck:
    """Is this usable as the menu background? Ported from Test-BgInput, which
    exists because a background that was never checked used to fail only at
    build time, with a message naming no file at all."""
    r = ImageCheck()
    if path is None or not Path(path).is_file():
        r.msg = "File not found."
        return r
    try:
        with Image.open(path) as im:
            r.width, r.height = im.size
    except (UnidentifiedImageError, OSError):
        r.msg = ("That file cannot be read as an image. It may be corrupt, still "
                 "downloading, or a format that is not supported. PNG, JPG and BMP "
                 "always work.")
        return r
    if r.width < 200 or r.height < 150:
        r.msg = (f"The image is only {r.width}x{r.height}. The menu is 760x480, so this "
                 "would be stretched past recognition.")
        return r
    r.ok = True
    return r


def _open_source(path: Path) -> Image.Image:
    """The picture to work from, as RGBA. An .ico is asked for its largest frame:
    opened any other way it can yield a small one, which is how a 256px cover
    becomes a blurry 32px square nobody can explain."""
    im = Image.open(path)
    if isinstance(im, IcoImagePlugin.IcoImageFile):
        biggest = max(im.info.get("sizes", {im.size}))
        im.size = biggest
    im.load()
    return im.convert("RGBA")


def _square(im: Image.Image) -> Image.Image:
    """Centre-crop to a square, the same way for both icons, so they frame the
    artwork identically. Offsets are rounded the way PowerShell's [int] rounds:
    to even on a half."""
    side = min(im.size)
    left = round((im.width - side) / 2)
    top = round((im.height - side) / 2)
    return im.crop((left, top, left + side, top + side))


def _scaled(im: Image.Image, size: int) -> Image.Image:
    return im if im.size == (size, size) else im.resize((size, size), Image.Resampling.BICUBIC,
                                                        reducing_gap=3.0)


def _dib(im: Image.Image) -> bytes:
    """One frame as the 32-bit bitmap an .ico holds below 256px: a
    BITMAPINFOHEADER that reports twice the height (image plus mask), the pixels
    bottom-up in BGRA, then an all-zero AND mask padded to 32-bit rows. The alpha
    channel carries the transparency, so the mask is only there because the
    format requires one. Ported from Get-DibBytes."""
    w, h = im.size
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    rows = im.tobytes("raw", "BGRA")
    stride = w * 4
    pixels = b"".join(rows[y * stride:(y + 1) * stride] for y in range(h - 1, -1, -1))
    mask = bytes(((w + 31) // 32) * 4 * h)
    return header + pixels + mask


def convert_to_ico(src: str | Path, out: str | Path) -> None:
    """A multi-size .ico from any picture. Ported from Convert-ToIco."""
    master = _square(_open_source(Path(src)))
    frames = []
    for size in ICO_SIZES:
        frame = _scaled(master, size)
        if size >= 256:
            # From Vista on, the 256px frame is stored as a PNG. As a raw 32-bit
            # bitmap it is a quarter of a megabyte on its own.
            buf = io.BytesIO()
            frame.save(buf, "PNG")
            frames.append((size, buf.getvalue()))
        else:
            frames.append((size, _dib(frame)))

    header = struct.pack("<HHH", 0, 1, len(frames))
    directory = b""
    offset = 6 + 16 * len(frames)
    for size, data in frames:
        dim = 0 if size >= 256 else size      # 0 means 256 in an icon directory
        directory += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    _write(Path(out), header + directory + b"".join(d for _, d in frames))


def convert_to_png(src: str | Path, out: str | Path) -> None:
    """The same picture as a 256px PNG, for Linux. Ported from Convert-ToPng.

    Linux file managers cannot use the .ico: gvfs hands the file to GdkPixbuf,
    whose ICO support is for favicons rather than seven-frame icons. PNG every
    desktop reads. Both come from the same source, so they cannot disagree about
    what the game looks like.
    """
    im = _scaled(_square(_open_source(Path(src))), PNG_SIZE)
    buf = io.BytesIO()
    im.save(buf, "PNG")
    _write(Path(out), buf.getvalue())


def _write(path: Path, data: bytes) -> None:
    # Over a read-only file left by a previous build, the same as every copy.
    if path.exists():
        path.chmod(0o644)
        path.unlink()
    path.write_bytes(data)
