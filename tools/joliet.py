"""List the file names in an ISO's Joliet tree: the names Windows reads.

Read straight out of the directory records rather than asked of xorriso, whose
reader was measured to report a name in full while the Joliet record on the
disc held it cut to 64 characters. Windows reads the record.

    python tools/joliet.py some.iso
"""

from __future__ import annotations

import struct
import sys
from pathlib import Path

SECTOR = 2048


def _records(data: bytes):
    pos = 0
    while pos < len(data):
        length = data[pos]
        if length == 0:
            # Records never cross a sector; the rest of this one is padding.
            pos = (pos // SECTOR + 1) * SECTOR
            continue
        yield data[pos:pos + length]
        pos += length


def names(iso: str | Path) -> list[str]:
    """Every file in the Joliet tree, as a path with forward slashes."""
    with open(iso, "rb") as f:
        def read(lba: int, size: int) -> bytes:
            f.seek(lba * SECTOR)
            return f.read(size)

        for n in range(16, 32):
            vd = read(n, SECTOR)
            if vd[:6] == b"\x02CD001" and vd[88:90] == b"%/":
                root = vd[156:190]
                break
            if vd[0] == 255:
                raise ValueError("no Joliet descriptor")
        else:
            raise ValueError("no Joliet descriptor")

        out: list[str] = []

        def walk(record: bytes, prefix: str) -> None:
            lba, size = struct.unpack_from("<I", record, 2)[0], struct.unpack_from("<I", record, 10)[0]
            for r in _records(read(lba, size)):
                name_len = r[32]
                raw = r[33:33 + name_len]
                if raw in (b"\x00", b"\x01"):
                    continue                      # . and ..
                name = raw.decode("utf-16-be")
                if r[25] & 2:
                    walk(r, prefix + name + "/")
                else:
                    # A file over 4 GiB is several records of one name, one per
                    # extent, the last without the multi-extent flag.
                    if r[25] & 0x80:
                        continue
                    out.append(prefix + name.split(";")[0])

        walk(root, "")
        return out


if __name__ == "__main__":
    for n in names(sys.argv[1]):
        print(n)
