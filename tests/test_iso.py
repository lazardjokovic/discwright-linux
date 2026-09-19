import hashlib
import os
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

from discwright import iso as iso_module
from discwright.iso import IsoError, build_iso, iso_path, name_problems

sys.path.insert(0, str(Path(__file__).parent.parent / "tools"))
import joliet  # noqa: E402

HAVE_XORRISO = shutil.which("xorriso") is not None
needs_xorriso = pytest.mark.skipif(not HAVE_XORRISO, reason="xorriso is not installed")


def write(path: Path, data: bytes | str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data.encode() if isinstance(data, str) else data)
    return path


def tree_hashes(root: Path) -> dict[str, str]:
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob("*") if p.is_file()}


# ---- the ISO's name: the Windows tests for Get-IsoPath, ported ------------------------

def test_names_the_iso_after_the_label(tmp_path):
    assert iso_path(tmp_path, "ALAN WAKE") == tmp_path / "ALAN WAKE.iso"


def test_makes_characters_a_file_name_cannot_take_underscores(tmp_path):
    assert iso_path(tmp_path, "The Witcher: Enhanced") == tmp_path / "The Witcher_ Enhanced.iso"


def test_names_nothing_without_an_output_folder_or_a_label(tmp_path):
    assert iso_path("", "ALAN WAKE") is None
    assert iso_path(tmp_path, "") is None
    assert iso_path(tmp_path, "   ") is None


def test_still_names_a_label_made_only_of_symbols(tmp_path):
    assert iso_path(tmp_path, "***") == tmp_path / "___.iso"


# ---- names Windows would not get back as they are -------------------------------------
#
# Every rule measured by building an ISO with xorriso and mounting it on Windows
# 11 (docs/xorriso-spike.md), except the device names, which are Windows' own
# documented rule.

@pytest.mark.parametrize("name,says", [
    ("Art: book.pdf", "does not allow"),
    ("what?.txt", "does not allow"),
    ("star*.txt", "does not allow"),
    ('say "hi".txt', "does not allow"),
    ("less<more>.txt", "does not allow"),
    ("pipe|.txt", "does not allow"),
    ("back\\slash.txt", "does not allow"),
    ("bell\x07.txt", "does not allow"),
    ("Game \U0001F3AE.txt", "emoji"),
    ("trailing dot.", "dot"),
    ("trailing space ", "space"),
    ("CON.txt", "device"),
    ("nul", "device"),
    ("com1.log", "device"),
    ("x" * 100 + ".txt", "104 characters"),
])
def test_refuses_a_name_windows_would_not_get_back(tmp_path, name, says):
    write(tmp_path / "Extras" / name, "x")
    problems = name_problems(tmp_path)
    assert len(problems) == 1 and says in problems[0], problems
    assert problems[0].startswith("Extras/")


def test_refuses_two_names_that_differ_only_in_capitals(tmp_path):
    write(tmp_path / "Readme.txt", "a")
    write(tmp_path / "README.txt", "b")
    problems = name_problems(tmp_path)
    assert len(problems) == 1 and "capitals" in problems[0]


def test_checks_folder_names_too(tmp_path):
    write(tmp_path / ("d" * 104) / "f.txt", "x")
    write(tmp_path / "Maps: Europe" / "f.txt", "x")
    assert len(name_problems(tmp_path)) == 2


@pytest.mark.parametrize("name", [
    "x" * 99 + ".txt",                  # 103, the limit
    "Café ™ Żółw.txt",   # outside ASCII, inside what Joliet holds
    "setup_the_witcher_enhanced_edition_1.5_(a).exe",
    "comet.txt",                        # starts like a device name, is not one
    "con game.txt",
    ".hidden",
])
def test_accepts_names_windows_gets_back_whole(tmp_path, name):
    write(tmp_path / name, "x")
    assert name_problems(tmp_path) == []


# ---- writing it -----------------------------------------------------------------------

@pytest.fixture
def disc(tmp_path):
    root = tmp_path / "disc"
    write(root / "autorun.inf", b"[autorun]\r\nlabel=ALPHA\r\n")
    write(root / "AUTORUN" / "menu.hta", "<html></html>")
    write(root / "Games" / "01 - Café ™" / "setup_cafe.exe", os.urandom(70_000))
    write(root / "Extras" / "Deep" / "Er" / "Still" / "notes.txt", "deep")
    # Past Joliet's own 64 characters, inside the 103 that -joliet-long allows:
    # GOG installer names get this long.
    write(root / ("setup_" + "a_very_long_game_title_" * 2 + "enhanced_edition_1.5_(12345).exe"), "long")
    write(root / ".xdg-volume-info", "[Volume Info]\nName=ALPHA\n")
    return root


def joliet_names(image: Path) -> set[str]:
    """The names as Windows reads them, out of the Joliet records themselves.
    Not asked of xorriso: its reader showed a name whole that the record held
    cut to 64 characters."""
    return set(joliet.names(image))


def volume_ids(image: Path) -> tuple[str, str]:
    """The volume id in the ISO9660 primary descriptor, and in Joliet's."""
    data = image.read_bytes()[32768:32768 + 2048 * 16]
    pvd = data[40:72].decode("ascii").rstrip()
    for off in range(0, len(data), 2048):
        if data[off:off + 6] == b"\x02CD001":
            return pvd, data[off + 40:off + 72].decode("utf-16-be").rstrip()
    raise AssertionError("no Joliet descriptor")


@needs_xorriso
def test_writes_every_file_whole(disc, tmp_path):
    out = build_iso(disc, tmp_path / "ALPHA.iso", "ALPHA", log=lambda m: None)
    got = tmp_path / "got"
    subprocess.run(["xorriso", "-osirrox", "on", "-indev", str(out), "-extract", "/", str(got)],
                   check=True, capture_output=True)
    assert tree_hashes(got) == tree_hashes(disc)


@needs_xorriso
def test_gives_windows_every_name_as_it_is(disc, tmp_path):
    out = build_iso(disc, tmp_path / "ALPHA.iso", "ALPHA", log=lambda m: None)
    assert joliet_names(out) == set(tree_hashes(disc))


@needs_xorriso
def test_keeps_names_outside_ascii_under_a_c_locale(disc, tmp_path, monkeypatch):
    # Without -input-charset, xorriso takes the locale's charset, and under C it
    # wrote "Cafe" with an accent into the Joliet names as "Caf__", silently.
    monkeypatch.setenv("LC_ALL", "C")
    monkeypatch.setenv("LANG", "C")
    out = build_iso(disc, tmp_path / "ALPHA.iso", "ALPHA", log=lambda m: None)
    assert "Café ™".encode("utf-16-be") in out.read_bytes()


@needs_xorriso
def test_writes_the_volume_id_windows_shows_when_nothing_reads_autorun(disc, tmp_path):
    out = build_iso(disc, tmp_path / "x.iso", "Alan_Wake", log=lambda m: None)
    assert volume_ids(out) == ("Alan_Wake", "Alan_Wake")


@needs_xorriso
def test_reports_progress_up_to_the_end(disc, tmp_path):
    seen = []
    build_iso(disc, tmp_path / "x.iso", "X", log=lambda m: None, progress=seen.append)
    assert seen and seen[-1] == 100.0
    assert seen == sorted(seen)


@needs_xorriso
def test_replaces_the_previous_iso(disc, tmp_path):
    out = write(tmp_path / "x.iso", "old")
    build_iso(disc, out, "X", log=lambda m: None)
    assert out.read_bytes()[32769:32774] == b"CD001"
    assert not (tmp_path / "x.iso.partial").exists()


@needs_xorriso
def test_leaves_the_previous_iso_alone_when_xorriso_fails(disc, tmp_path, monkeypatch):
    out = write(tmp_path / "x.iso", "old")
    real = iso_module.xorriso_command
    monkeypatch.setattr(iso_module, "xorriso_command",
                        lambda stage, o, v: real(stage, o, v) + ["-no-such-option"])
    with pytest.raises(IsoError, match="could not write"):
        build_iso(disc, out, "X", log=lambda m: None)
    assert out.read_text() == "old"
    assert not (tmp_path / "x.iso.partial").exists()


def test_refuses_a_bad_name_before_writing_anything(disc, tmp_path):
    write(disc / "Extras" / "Art: book.pdf", "x")
    with pytest.raises(IsoError, match="Art: book.pdf"):
        build_iso(disc, tmp_path / "x.iso", "X", log=lambda m: None)
    assert list(tmp_path.glob("x.iso*")) == []


def test_says_how_to_get_xorriso_when_it_is_missing(disc, tmp_path, monkeypatch):
    monkeypatch.setattr(iso_module.shutil, "which", lambda name: None)
    with pytest.raises(IsoError, match="apt install xorriso"):
        build_iso(disc, tmp_path / "x.iso", "X", log=lambda m: None)
