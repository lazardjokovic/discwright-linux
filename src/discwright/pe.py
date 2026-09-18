"""Read the version strings out of a Windows executable.

GOG installers are Inno Setup programs, and the game's name is in their version
resource as ProductName. Windows DiscWright reads it with
FileInfo.VersionInfo.ProductName; Python has nothing built in for this, so the
PE format is walked here by hand rather than adding a dependency for one field.

Only reads: the headers, the section table and the resource section. A GOG
installer with no .bin parts can be gigabytes, and none of that is loaded.
"""

from __future__ import annotations

import struct
from pathlib import Path

_RT_VERSION = 16
_RESOURCE_DIRECTORY = 2          # index into the optional header's data directories
_MAX_RESOURCE_SECTION = 64 << 20  # a version resource is tiny; refuse anything absurd


class _Blob:
    def __init__(self, data: bytes):
        self.data = data

    def u16(self, at: int) -> int:
        return struct.unpack_from("<H", self.data, at)[0]

    def u32(self, at: int) -> int:
        return struct.unpack_from("<I", self.data, at)[0]


def version_strings(path: str | Path) -> dict[str, str] | None:
    """Every string in the executable's version resource, or None when it has
    none or is not a Windows executable at all.

    Values are returned as stored, including the trailing spaces Inno Setup pads
    them with. Trimming is the caller's decision, the same as on Windows.

    When there is more than one string table (one per language), the first is
    used. Windows picks by the language list in VarFileInfo; GOG installers carry
    a single table, so the two agree on everything this is used for.
    """
    try:
        with open(path, "rb") as f:
            return _read(f)
    except (OSError, struct.error, ValueError, UnicodeDecodeError):
        return None


def product_name(path: str | Path) -> str | None:
    strings = version_strings(path)
    return None if strings is None else strings.get("ProductName")


def _read(f) -> dict[str, str] | None:
    head = _Blob(f.read(4096))
    if head.data[:2] != b"MZ":
        return None
    pe = head.u32(0x3C)
    if pe + 24 > len(head.data) or head.data[pe:pe + 4] != b"PE\0\0":
        return None
    sections = head.u16(pe + 6)
    opt_size = head.u16(pe + 20)
    opt = pe + 24
    magic = head.u16(opt)
    if magic == 0x10B:      # PE32
        count_at, dirs_at = opt + 92, opt + 96
    elif magic == 0x20B:    # PE32+
        count_at, dirs_at = opt + 108, opt + 112
    else:
        return None
    if head.u32(count_at) <= _RESOURCE_DIRECTORY:
        return None
    rsrc_rva = head.u32(dirs_at + 8 * _RESOURCE_DIRECTORY)
    if rsrc_rva == 0:
        return None

    # Find the section holding the resource directory, and read just that one.
    table = opt + opt_size
    for i in range(sections):
        s = table + 40 * i
        va, raw_size, raw_ptr = head.u32(s + 12), head.u32(s + 16), head.u32(s + 20)
        vsize = head.u32(s + 8) or raw_size
        if va <= rsrc_rva < va + max(vsize, raw_size):
            if raw_size > _MAX_RESOURCE_SECTION:
                return None
            f.seek(raw_ptr)
            section = _Blob(f.read(raw_size))
            return _from_resources(section, rsrc_rva - va, va)
    return None


def _from_resources(sec: _Blob, root: int, section_va: int) -> dict[str, str] | None:
    def entries(directory: int):
        named, ids = sec.u16(directory + 12), sec.u16(directory + 14)
        for i in range(named + ids):
            e = directory + 16 + 8 * i
            yield sec.u32(e), sec.u32(e + 4)

    def first_child(directory: int, want_id: int | None = None) -> int | None:
        for name, target in entries(directory):
            if want_id is not None and (name & 0x80000000 or name != want_id):
                continue
            return target
        return None

    # Type -> name -> language -> data. Offsets inside the tree are relative to
    # the start of the resource directory, with the top bit marking a subdirectory.
    level = first_child(root, _RT_VERSION)
    for _ in range(2):
        if level is None or not level & 0x80000000:
            return None
        level = first_child(root + (level & 0x7FFFFFFF))
    if level is None or level & 0x80000000:
        return None
    leaf = root + level
    data_rva, size = sec.u32(leaf), sec.u32(leaf + 4)
    start = data_rva - section_va
    if start < 0 or start + size > len(sec.data):
        return None
    return _version_info(sec.data[start:start + size])


def _align4(n: int) -> int:
    return (n + 3) & ~3


def _block(data: bytes, at: int):
    """One VS_VERSIONINFO-style block: (key, value bytes, value is text, children, end)."""
    length, value_len, kind = struct.unpack_from("<HHH", data, at)
    end = min(at + length, len(data))
    key_at = at + 6
    key_end = key_at
    while key_end + 1 < end and data[key_end:key_end + 2] != b"\0\0":
        key_end += 2
    key = data[key_at:key_end].decode("utf-16-le")
    value_at = _align4(key_end + 2)
    # A text value's length is counted in WCHARs, a binary one's in bytes.
    value_bytes = value_len * 2 if kind == 1 else value_len
    value = data[value_at:min(value_at + value_bytes, end)]
    children = []
    child = _align4(value_at + value_bytes)
    while child + 6 <= end:
        clen = struct.unpack_from("<H", data, child)[0]
        if clen == 0:
            break
        children.append(child)
        child = _align4(child + clen)
    return key, value, kind == 1, children, end


def _version_info(data: bytes) -> dict[str, str] | None:
    key, _, _, children, _ = _block(data, 0)
    if key != "VS_VERSION_INFO":
        return None
    for child in children:
        ckey, _, _, tables, _ = _block(data, child)
        if ckey != "StringFileInfo" or not tables:
            continue
        _, _, _, strings, _ = _block(data, tables[0])
        out: dict[str, str] = {}
        for s in strings:
            name, value, _, _, _ = _block(data, s)
            text = value.decode("utf-16-le", errors="replace")
            out[name] = text.split("\0", 1)[0]
        return out
    return {}
