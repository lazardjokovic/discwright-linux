"""checksums.sha256: what every other file on the disc should hash to.

Asked for as being able to restore a disc back to its original bin and exe
structure, matching the original hash values. The structure already comes back
byte for byte; what was missing was any way to prove it.

sha256sum format, so nothing from DiscWright is needed to read it, which is the
whole point of a file meant to still be useful in twenty years. Paths use
forward slashes for the same reason, and because Windows accepts them in a path
just as happily.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

CHECKSUM_FILE = "checksums.sha256"


def checksum_file_name() -> str:
    return CHECKSUM_FILE


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def checksum_list(stage_dir: Path, label: str) -> bytes:
    """The list, as the bytes of checksums.sha256.

    Everything on the disc except this file, which cannot hold its own hash.
    That includes the menu and the icon: a disc verifies whole, and a game
    restored out of it still has its own lines to check against.
    """
    out = stage_dir / CHECKSUM_FILE
    files = sorted((p for p in stage_dir.rglob("*") if p.is_file() and p != out),
                   key=lambda p: str(p).casefold())
    lines = [
        f"# DiscWright checksum list for {label}",
        f"# {len(files)} files, SHA-256, sha256sum format.",
        "#",
        "# To check a copy, from the folder holding these files:",
        f"#   sha256sum -c {CHECKSUM_FILE}",
        "# or in PowerShell, with no extra tools:",
        f"#   Get-Content {CHECKSUM_FILE} | Where-Object {{ $_ -notmatch '^#' }} | ForEach-Object {{",
        r"#     $h, $f = $_ -split ' \*', 2",
        "#     $a = (Get-FileHash $f -Algorithm SHA256).Hash",
        '#     "{0}  {1}" -f $(if ($a -eq $h) { "OK  " } else { "FAIL" }), $f }',
        "#",
    ]
    for f in files:
        rel = f.relative_to(stage_dir).as_posix()
        lines.append(f"{_sha256(f)} *{rel}")
    # Newlines, not carriage returns. Every other file the build writes is CRLF,
    # and this one cannot be: sha256sum -c takes the carriage return as part of
    # the filename and reports every line as a missing file. Found with the real
    # tool on the Windows side, which is the only way it would have shown up.
    return ("\n".join(lines) + "\n").encode("ascii")
