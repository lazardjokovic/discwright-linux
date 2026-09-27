"""The application menu entry: the file, its icons, and the script that installs it.

The window is how DiscWright is used, so being startable only by typing a path
into a terminal is a gap in the same way a missing feature is. What these check
is that the entry is valid, that it points at the command that was really found,
and that it can be taken away again.
"""

import os
import shutil
import subprocess
from pathlib import Path

import pytest

PACKAGING = Path(__file__).parent.parent / "packaging" / "desktop"
APP_ID = "com.discwright.DiscWright"
DESKTOP = PACKAGING / f"{APP_ID}.desktop"
INSTALL = PACKAGING / "install.sh"
SIZES = (16, 24, 32, 48, 64, 128, 256)


def entries(path: Path) -> dict:
    out = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("[") or line.startswith("#") or not line.strip():
            continue
        key, _, value = line.partition("=")
        out[key] = value
    return out


def test_the_entry_says_what_a_desktop_needs_to_show_it():
    e = entries(DESKTOP)
    assert e["Type"] == "Application"
    assert e["Name"] == "DiscWright"
    assert e["Terminal"] == "false"
    assert e["Icon"] == APP_ID
    # The window registers this id, so the desktop can put this entry's icon and
    # name on the running window instead of a generic one.
    assert e["StartupWMClass"] == APP_ID
    from discwright.window import APP_ID as registered          # noqa: F401
    assert registered == APP_ID


def test_it_opens_the_window_rather_than_a_build():
    assert entries(DESKTOP)["Exec"].endswith(" window")


def test_it_files_itself_where_a_disc_tool_belongs():
    # DiscBurning is an additional category, and the menu specification says it
    # belongs with AudioVideo, so a desktop that groups by category files this
    # where somebody would look for it.
    cats = entries(DESKTOP)["Categories"].split(";")
    assert "DiscBurning" in cats and "AudioVideo" in cats


@pytest.mark.parametrize("size", SIZES)
def test_it_ships_the_icon_at_the_sizes_a_desktop_asks_for(size):
    icon = PACKAGING / "icons" / "hicolor" / f"{size}x{size}" / "apps" / f"{APP_ID}.png"
    assert icon.is_file()
    from PIL import Image
    with Image.open(icon) as im:
        assert im.size == (size, size)
        assert im.mode in ("RGBA", "P")


@pytest.mark.skipif(shutil.which("desktop-file-validate") is None,
                    reason="desktop-file-validate is not installed")
def test_a_desktop_validator_accepts_it():
    run = subprocess.run(["desktop-file-validate", str(DESKTOP)],
                         capture_output=True, text=True)
    assert run.returncode == 0, run.stdout + run.stderr


@pytest.mark.skipif(os.name != "posix", reason="the installer is a shell script")
class TestInstalling:

    def run(self, home: Path, *args, expect=0):
        env = dict(os.environ, HOME=str(home), XDG_DATA_HOME=str(home / ".local" / "share"))
        run = subprocess.run(["sh", str(INSTALL), *args], capture_output=True, text=True, env=env)
        assert run.returncode == expect, run.stdout + run.stderr
        return run

    def fake_command(self, tmp_path: Path, name="discwright") -> Path:
        exe = tmp_path / "venv" / "bin" / name
        exe.parent.mkdir(parents=True, exist_ok=True)
        exe.write_text("#!/bin/sh\nexit 0\n")
        exe.chmod(0o755)
        return exe

    def test_installs_the_entry_and_every_icon(self, tmp_path):
        home = tmp_path / "home"
        exe = self.fake_command(tmp_path)
        self.run(home, str(exe))
        data = home / ".local" / "share"
        assert (data / "applications" / f"{APP_ID}.desktop").is_file()
        for size in SIZES:
            assert (data / "icons" / "hicolor" / f"{size}x{size}" / "apps" / f"{APP_ID}.png").is_file()

    def test_points_the_entry_at_the_command_it_was_given(self, tmp_path):
        # Installed into a virtual environment, discwright is not on PATH, so an
        # entry saying "discwright window" opens nothing when it is clicked.
        home = tmp_path / "home"
        exe = self.fake_command(tmp_path)
        self.run(home, str(exe))
        e = entries(home / ".local" / "share" / "applications" / f"{APP_ID}.desktop")
        assert e["Exec"] == f'"{exe}" window'
        assert e["TryExec"] == str(exe)

    def test_quotes_a_path_with_a_space_in_it(self, tmp_path):
        home = tmp_path / "home"
        exe = tmp_path / "my venv" / "bin" / "discwright"
        exe.parent.mkdir(parents=True)
        exe.write_text("#!/bin/sh\nexit 0\n")
        exe.chmod(0o755)
        self.run(home, str(exe))
        e = entries(home / ".local" / "share" / "applications" / f"{APP_ID}.desktop")
        assert e["Exec"] == f'"{exe}" window'

    def test_refuses_rather_than_writing_an_entry_that_does_nothing(self, tmp_path):
        # The worst way to find out the path was wrong is a menu item that does
        # nothing when it is clicked.
        home = tmp_path / "home"
        run = self.run(home, str(tmp_path / "nowhere" / "discwright"), expect=1)
        assert "Cannot find the discwright command" in run.stderr
        assert not (home / ".local" / "share" / "applications").exists()

    def test_takes_itself_out_again(self, tmp_path):
        home = tmp_path / "home"
        exe = self.fake_command(tmp_path)
        self.run(home, str(exe))
        self.run(home, "--uninstall")
        data = home / ".local" / "share"
        assert not (data / "applications" / f"{APP_ID}.desktop").exists()
        for size in SIZES:
            assert not (data / "icons" / "hicolor" / f"{size}x{size}" / "apps" / f"{APP_ID}.png").exists()

    def test_uninstalling_what_was_never_installed_is_not_a_failure(self, tmp_path):
        self.run(tmp_path / "home", "--uninstall")
