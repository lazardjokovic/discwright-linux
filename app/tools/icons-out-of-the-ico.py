#!/usr/bin/env python3
"""The app's icon as the PNGs a Linux desktop wants, one for each size.

    python3 tools/icons-out-of-the-ico.py src/DiscWright.App/DiscWright.ico <folder> <name>

writes <folder>/hicolor/<n>x<n>/apps/<name>.png for every frame in the icon.

Made out of the one icon the Windows app wears, each time the download is
put together, so there is no second set of pictures in the repository to
fall behind the first. The Python port kept such a set, and the icon has
changed since.

The standard library and nothing else: a frame is a bitmap with its bottom
row first and blue before red, and a PNG is a header and the rows squeezed
with zlib. A frame that is a PNG already is written as it stands.
"""

import struct
import sys
import zlib
from pathlib import Path

PNG = b"\x89PNG\r\n\x1a\n"


def chunk(kind: bytes, data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def png_of(size: int, bgra_bottom_up: bytes) -> bytes:
    row = size * 4
    rows = bytearray()

    for at in range(size):
        line = bgra_bottom_up[(size - 1 - at) * row:(size - at) * row]
        rgba = bytearray(line)
        rgba[0::4] = line[2::4]
        rgba[2::4] = line[0::4]
        rows += b"\x00" + rgba

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)

    return PNG + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(bytes(rows), 9)) + chunk(b"IEND", b"")


def frames(ico: bytes):
    reserved, kind, count = struct.unpack_from("<HHH", ico, 0)

    if reserved != 0 or kind != 1:
        raise SystemExit("That is not an icon file.")

    for at in range(count):
        width, _, _, _, _, _, length, offset = struct.unpack_from("<BBBBHHII", ico, 6 + 16 * at)
        size = width or 256
        frame = ico[offset:offset + length]

        if frame.startswith(PNG):
            yield size, frame
            continue

        header, wide, high, _, bits, squeezed = struct.unpack_from("<IiiHHI", frame, 0)

        if wide != size or high != size * 2 or bits != 32 or squeezed != 0:
            raise SystemExit(f"The {size} pixel frame is not a 32 bit bitmap, and this reads no other kind.")

        yield size, png_of(size, frame[header:header + size * size * 4])


def main() -> None:
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)

    ico, folder, name = Path(sys.argv[1]).read_bytes(), Path(sys.argv[2]), sys.argv[3]
    written = []

    for size, png in frames(ico):
        where = folder / "hicolor" / f"{size}x{size}" / "apps"
        where.mkdir(parents=True, exist_ok=True)
        (where / f"{name}.png").write_bytes(png)
        written.append(size)

    if not written:
        raise SystemExit("The icon has no frames in it.")

    print("Icons written: " + ", ".join(str(size) for size in sorted(written)))


if __name__ == "__main__":
    main()
