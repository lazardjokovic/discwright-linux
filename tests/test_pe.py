import os
from pathlib import Path

import pytest

from discwright.pe import product_name, version_strings

# Two executables compiled by Windows' own C# compiler, so their version
# resources come from a real Windows toolchain rather than from assumptions this
# reader shares. What Windows reads from them (FileVersionInfo) was recorded on
# Windows and is the expected value below.
PE = Path(__file__).parent / "fixtures" / "pe"


def test_reads_the_product_name_windows_reads_padding_and_all():
    assert product_name(PE / "with-product.exe") == "Fixture Game: Directors Cut   "


def test_reads_the_other_strings_too():
    strings = version_strings(PE / "with-product.exe")
    assert strings["ProductVersion"] == "1.2.3 (GOG)"
    assert strings["FileVersion"] == "1.2.3.4"


def test_an_executable_with_no_product_name_gives_nothing_usable():
    # Windows reads an empty string here. None or "" both mean "fall back".
    assert not product_name(PE / "no-product.exe")


def test_something_that_is_not_an_executable_gives_none(tmp_path):
    text = tmp_path / "setup_fake.exe"
    text.write_text("not a program")
    assert version_strings(text) is None


def test_a_truncated_executable_gives_none_rather_than_raising(tmp_path):
    cut = tmp_path / "cut.exe"
    cut.write_bytes((PE / "with-product.exe").read_bytes()[:700])
    assert version_strings(cut) is None


def test_an_empty_file_gives_none(tmp_path):
    empty = tmp_path / "empty.exe"
    empty.write_bytes(b"")
    assert version_strings(empty) is None


# Real GOG installers, against what Windows reads from them. Too big and not ours
# to commit, so this runs only where they are: point DISCWRIGHT_GOG_DIR at a
# folder holding one GOG download per subfolder.
GOG = os.environ.get("DISCWRIGHT_GOG_DIR")

WINDOWS_READS = {
    "Alan Wake": "Alan Wake",
    "Dead Space": "Dead Space",
    "Hollow Knight": "Hollow Knight",
    "The Witcher": "The Witcher - Enhanced Edition",
}


@pytest.mark.skipif(not GOG, reason="DISCWRIGHT_GOG_DIR not set")
@pytest.mark.parametrize("folder,expected", WINDOWS_READS.items())
def test_real_gog_installers_read_as_windows_reads_them(folder, expected):
    exe = max(Path(GOG, folder).glob("setup_*.exe"), key=lambda p: p.stat().st_size)
    raw = product_name(exe)
    # Inno pads every one of these to 60 characters, and so does Windows.
    assert raw.rstrip() == expected
    assert len(raw) == 60
