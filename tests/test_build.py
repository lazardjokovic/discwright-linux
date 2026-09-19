import shutil
import subprocess

import pytest

from discwright import build as build_module
from discwright.build import build
from discwright.iso import IsoError

from test_stage import quiet, settings, src, write  # noqa: F401  (src is a fixture)

needs_xorriso = pytest.mark.skipif(shutil.which("xorriso") is None, reason="xorriso is not installed")


@needs_xorriso
def test_builds_the_iso_from_the_staged_disc(src, tmp_path):
    iso = build(settings(src, tmp_path / "out"), quiet)
    assert iso == tmp_path / "out" / "ALPHA.iso"
    listing = subprocess.run(["xorriso", "-indev", str(iso), "-find", "/", "-type", "f"],
                             capture_output=True, text=True, check=True).stdout
    for name in ("autorun.inf", "ALPHA.ico", "AUTORUN/menu.hta", "AUTORUN/bg.png", "setup_alpha_1.0.exe"):
        assert f"'/{name}'" in listing, name


@needs_xorriso
def test_removes_the_old_disc_folder_once_the_iso_is_written(src, tmp_path):
    out = tmp_path / "out"
    write(out / "disc" / "old.txt", "OLD")
    build(settings(src, out), quiet)
    assert list(out.glob("disc.previous-*")) == []


def test_keeps_the_old_disc_folder_when_the_iso_fails(src, tmp_path, monkeypatch):
    # The build may be reading from it: installers, an icon, a manual picked
    # from inside the disc being rebuilt. It goes only once the ISO exists.
    out = tmp_path / "out"
    write(out / "disc" / "old.txt", "OLD")

    def fail(*a, **k):
        raise IsoError("no")
    monkeypatch.setattr(build_module, "build_iso", fail)
    with pytest.raises(IsoError):
        build(settings(src, out), quiet)
    kept = list(out.glob("disc.previous-*"))
    assert len(kept) == 1 and (kept[0] / "old.txt").read_text() == "OLD"


def test_refuses_a_label_that_names_no_iso_before_staging(src, tmp_path):
    out = tmp_path / "out"
    with pytest.raises(IsoError, match="label"):
        build(settings(src, out, label="   "), quiet)
    assert not (out / "disc").exists()
