"""The window, drawn for real where GTK 4 and a display are there.

The rules it shows live in form.py and are tested there without a screen. What
this checks is that the window shows them: every control greyed out that the
form says cannot be used, with the form's reason on its tooltip.
"""

import os

import pytest

gi = pytest.importorskip("gi")
try:
    gi.require_version("Gtk", "4.0")
    from gi.repository import Gtk
except (ValueError, ImportError):
    pytest.skip("GTK 4 is not installed", allow_module_level=True)
if not (os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY")) or not Gtk.init_check():
    pytest.skip("no display to draw on", allow_module_level=True)

from discwright.form import Form  # noqa: E402
from discwright.window import DiscWindow, DiscWrightApp  # noqa: E402

from test_stage import src  # noqa: E402,F401  (a fixture)


@pytest.fixture
def window():
    app = DiscWrightApp()
    app.register(None)
    w = DiscWindow(app, Form())
    yield w
    w.destroy()


def shown_state(w, name):
    return [(x.get_sensitive(), x.get_tooltip_text()) for x in w.widgets[name]]


def test_greys_out_what_the_form_says_cannot_be_used(window):
    for name, state in window.form.controls().items():
        for sensitive, tip in shown_state(window, name):
            assert sensitive == state.enabled, name
            assert tip == (None if state.enabled else state.why), name


def test_build_starts_greyed_with_the_first_thing_missing(window):
    assert not window.btn_build.get_sensitive()
    assert window.btn_build.get_tooltip_text() == "Add a game first (step 1)."


def test_follows_the_form_as_it_changes(window, src):
    window.form.add_game(src / "Alpha")
    window.refresh()
    assert window.label_entry.get_text() == "alpha"
    assert window.games_list.get_row_at_index(0) is not None
    window.form.menu = False
    window.refresh()
    assert not any(s for s, _ in shown_state(window, "background"))


def test_locks_everything_while_a_build_runs(window, src):
    window.form.add_game(src / "Alpha")
    window.form.building = True
    window.refresh()
    for name in window.form.controls():
        assert not any(s for s, _ in shown_state(window, name)), name
    assert not window.label_entry.get_sensitive()
