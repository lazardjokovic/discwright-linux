"""checksums.sha256, and the launcher that goes at the disc root with it.

Both are Windows 0.10.0 features, and both are compared against what the
Windows app itself produced rather than against this port's own output. A port
that agrees with itself proves nothing.
"""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

import pytest

from discwright.checksums import CHECKSUM_FILE, checksum_file_name, checksum_list
from discwright.layout import is_reserved_name
from discwright.menu import menu_launcher, menu_launcher_name

FIX = Path(__file__).parent / "fixtures"
WINDOWS_SUMS = FIX / "checksums" / "windows-0.10.0.txt"
WINDOWS_LAUNCHER = FIX / "menu" / "windows-0.10.0" / "launcher.hta"


def tree(root: Path) -> None:
    """The same four files the Windows reference list was made from."""
    (root / "AUTORUN").mkdir(parents=True)
    (root / "Games" / "01 - A").mkdir(parents=True)
    (root / "autorun.inf").write_bytes(b"one")
    (root / "AUTORUN" / "menu.hta").write_bytes(b"two")
    (root / "Games" / "01 - A" / "setup.exe").write_bytes(b"three")
    (root / "Start Here.hta").write_bytes(b"four")


# ---- the list ----------------------------------------------------------------

def test_writes_the_list_windows_writes(tmp_path):
    tree(tmp_path)
    assert checksum_list(tmp_path, "PARITY DISC") == WINDOWS_SUMS.read_bytes()


def test_does_not_try_to_list_itself(tmp_path):
    tree(tmp_path)
    (tmp_path / CHECKSUM_FILE).write_bytes(b"an older run left this here")
    body = checksum_list(tmp_path, "X").decode("ascii")
    hashed = [l for l in body.splitlines() if l and not l.startswith("#")]
    assert not [l for l in hashed if CHECKSUM_FILE in l]


def test_ends_its_lines_with_a_newline_alone(tmp_path):
    # Not a detail. sha256sum -c reads a carriage return as part of the filename
    # and then calls every line a missing file, which is what the first Windows
    # version of this did.
    tree(tmp_path)
    assert b"\r" not in checksum_list(tmp_path, "X")


def test_separates_folders_with_a_forward_slash(tmp_path):
    tree(tmp_path)
    body = checksum_list(tmp_path, "X").decode("ascii")
    assert "*Games/01 - A/setup.exe" in body
    assert chr(92) not in [l for l in body.splitlines() if l.startswith("#") is False][0]


def test_hashes_what_sha256sum_hashes(tmp_path):
    tree(tmp_path)
    body = checksum_list(tmp_path, "X").decode("ascii")
    import hashlib
    for line in (l for l in body.splitlines() if l and not l.startswith("#")):
        digest, rel = line.split(" *", 1)
        want = hashlib.sha256((tmp_path / rel).read_bytes()).hexdigest()
        assert digest == want


@pytest.mark.skipif(shutil.which("sha256sum") is None, reason="needs coreutils")
def test_the_real_tool_accepts_it(tmp_path):
    # The format claim is the whole purpose of the file, so it is checked with
    # the tool it names rather than by reading the text back.
    tree(tmp_path)
    (tmp_path / CHECKSUM_FILE).write_bytes(checksum_list(tmp_path, "REAL"))
    run = subprocess.run(["sha256sum", "-c", CHECKSUM_FILE],
                         cwd=tmp_path, capture_output=True, text=True)
    assert run.returncode == 0, run.stdout + run.stderr
    assert "FAILED" not in run.stdout

    # And it has to notice a file that changed by one byte.
    (tmp_path / "autorun.inf").write_bytes(b"onx")
    run = subprocess.run(["sha256sum", "-c", CHECKSUM_FILE],
                         cwd=tmp_path, capture_output=True, text=True)
    assert run.returncode != 0
    assert "autorun.inf: FAILED" in run.stdout


# ---- the launcher --------------------------------------------------------------

def test_writes_the_launcher_windows_writes():
    assert menu_launcher() == WINDOWS_LAUNCHER.read_bytes()


def test_the_launcher_carries_no_backslash():
    # The Windows version carried three and its generator ate all three. Built
    # from fromCharCode and BuildPath instead, there is nothing left to eat.
    assert bytes([92]) not in menu_launcher()


def test_the_launcher_is_ascii_with_crlf_like_every_other_disc_file():
    b = menu_launcher()
    assert all(c < 128 for c in b)
    assert b.replace(b"\r\n", b"").count(b"\n") == 0


def test_the_launcher_starts_the_menu_rather_than_copying_it():
    # Two menus would be two to keep in step.
    text = menu_launcher().decode("ascii")
    assert "mshta" in text
    assert "menu.hta" in text


def test_both_names_belong_to_the_disc():
    # So extra content cannot land on top of either.
    assert menu_launcher_name() == "Start Here.hta"
    assert checksum_file_name() == CHECKSUM_FILE
    assert is_reserved_name(menu_launcher_name(), "ALPHA.ico")
    assert is_reserved_name(checksum_file_name(), "ALPHA.ico")
