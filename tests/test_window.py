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


@pytest.fixture(scope="module")
def app():
    # One per process: a second application registering the same id on the
    # session bus is refused.
    a = DiscWrightApp()
    a.register(None)
    return a


@pytest.fixture
def window(app):
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


def usable(widget):
    """Every control a person could click or type into, found by walking the
    whole window rather than by asking the form: a widget nobody registered with
    a rule is exactly the one a list of rules misses."""
    interactive = (Gtk.Button, Gtk.CheckButton, Gtk.Entry, Gtk.DropDown, Gtk.ListBox)
    found = []
    child = widget.get_first_child()
    while child is not None:
        if isinstance(child, interactive) and child.is_sensitive() and child.get_visible():
            found.append(child)
        found.extend(usable(child))
        child = child.get_next_sibling()
    return found


def test_locks_everything_while_a_build_runs(window, src):
    window.form.add_game(src / "Alpha")
    window.form.building = True
    window.refresh()
    for name in window.form.controls():
        assert not any(s for s, _ in shown_state(window, name)), name
    # The window's own title-bar buttons (close, maximise) are the only things
    # left to click, and they are not in the window's content.
    left = [w for w in usable(window.get_child())]
    left += [w for w in usable(window.get_titlebar())
             if not isinstance(w.get_parent(), Gtk.WindowControls)
             and not isinstance(w.get_parent().get_parent(), Gtk.WindowControls)]
    assert left == [], [getattr(w, "get_label", lambda: type(w).__name__)() for w in left]


# ---- building from the window ---------------------------------------------------------

import shutil  # noqa: E402

from gi.repository import GLib  # noqa: E402

needs_xorriso = pytest.mark.skipif(shutil.which("xorriso") is None, reason="xorriso is not installed")


def run_until(condition, seconds=60):
    ctx = GLib.MainContext.default()
    end = GLib.get_monotonic_time() + seconds * 1_000_000
    while not condition() and GLib.get_monotonic_time() < end:
        ctx.iteration(False)
    assert condition(), "timed out"


@pytest.fixture
def recorded(window, monkeypatch):
    """Dialogs recorded rather than shown, so a test run puts nothing on screen."""
    seen = {"alerts": [], "asks": []}
    monkeypatch.setattr(window, "alert", lambda h, d: seen["alerts"].append((h, d)))

    def ask(heading, detail, buttons, then):
        seen["asks"].append((heading, detail, buttons))
        then(seen.get("answer", 0))
    monkeypatch.setattr(window, "ask", ask)
    return seen


def ready(window, src, tmp_path):
    f = window.form
    f.add_game(src / "Alpha")
    f.icon = src / "art" / "alpha.ico"
    f.background = src / "art" / "alpha-bg.png"
    f.out_dir = tmp_path / "out"
    window.refresh()


@needs_xorriso
def test_builds_a_disc_from_the_window(window, src, tmp_path, recorded):
    ready(window, src, tmp_path)
    window.on_build()
    assert window.form.building and not window.btn_build.get_sensitive()
    run_until(lambda: not window.form.building)
    assert recorded["alerts"][-1][0] == "The disc is built"
    assert (tmp_path / "out" / "alpha.iso").is_file()
    assert (tmp_path / "out" / "discproject.json").is_file()
    assert window.progress.get_fraction() == 1.0
    log = window.log_view.get_buffer()
    assert "DONE." in log.get_text(log.get_start_iter(), log.get_end_iter(), False)
    # Unlocked again, and the button now says it will replace what is there.
    assert window.btn_build.get_sensitive() and window.btn_build.get_label() == "REBUILD ISO"


def test_says_why_a_build_stopped_and_unlocks(window, src, tmp_path, recorded, monkeypatch):
    import discwright.window as window_module
    from discwright.iso import IsoError

    def fail(*a, **k):
        raise IsoError("xorriso is not installed, and it is what writes the ISO.")
    monkeypatch.setattr(window_module, "build", fail)
    ready(window, src, tmp_path)
    window.on_build()
    run_until(lambda: not window.form.building)
    heading, detail = recorded["alerts"][-1]
    assert heading == "The disc was not built" and "xorriso is not installed" in detail
    assert window.btn_build.get_sensitive()


def test_asks_before_building_with_a_label_windows_cannot_show(window, src, tmp_path, recorded,
                                                              monkeypatch):
    import discwright.window as window_module
    started = []
    monkeypatch.setattr(window_module, "build", lambda *a, **k: started.append(1) or tmp_path)
    ready(window, src, tmp_path)
    window.form.label = "Żółw"
    recorded["answer"] = 0                      # Cancel
    window.on_build()
    assert recorded["asks"] and "cannot show this disc label" in recorded["asks"][0][1]
    assert not window.form.building and started == []


# ---- the question a folder that is not a GOG download asks ----------------------------

def rows_of(listbox):
    out, i = [], 0
    while (row := listbox.get_row_at_index(i)) is not None:
        out.append(row.get_child().get_label())
        i += 1
    return out


def labels_in(widget, found=None):
    found = [] if found is None else found
    child = widget.get_first_child()
    while child is not None:
        if isinstance(child, Gtk.Label):
            found.append(child.get_label())
        labels_in(child, found)
        child = child.get_next_sibling()
    return found


def list_in(widget):
    child = widget.get_first_child()
    while child is not None:
        if isinstance(child, Gtk.ListBox):
            return child
        if (found := list_in(child)) is not None:
            return found
        child = child.get_next_sibling()
    return None


def buttons_in(widget, found=None):
    found = {} if found is None else found
    child = widget.get_first_child()
    while child is not None:
        if isinstance(child, Gtk.Button) and child.get_label():
            found[child.get_label()] = child
        buttons_in(child, found)
        child = child.get_next_sibling()
    return found


@pytest.fixture
def loose(tmp_path):
    d = tmp_path / "Portable Game"
    (d / "data").mkdir(parents=True)
    with open(d / "PortableGame.exe", "wb") as f:
        f.truncate(3 << 20)
    with open(d / "CrashHandler.exe", "wb") as f:
        f.truncate(64 << 10)
    (d / "readme.txt").write_text("read me")
    (d / "data" / "config.ini").write_text("x=1")
    return d


@pytest.fixture
def shown(window, monkeypatch):
    """The dialog built but not presented, so a run puts nothing on screen."""
    seen = []
    monkeypatch.setattr(window, "show_window", lambda w: seen.append(w))
    return seen


def test_shows_the_question_the_form_wrote(window, loose, shown):
    q = window.form.folder_question(loose)
    win = window.ask_about_folder(q)
    assert shown == [win]
    assert win.get_title() == "No GOG installer in this folder"
    body = win.get_child()
    assert q.explain in labels_in(body)
    assert q.note in labels_in(body)
    assert rows_of(list_in(body)) == q.choices
    win.destroy()


def test_starts_on_the_answer_that_cannot_go_wrong(window, loose, shown):
    win = window.ask_about_folder(window.form.folder_question(loose))
    chooser = list_in(win.get_child())
    assert chooser.get_selected_row().get_index() == 0
    win.destroy()


def test_adds_the_folder_when_add_is_clicked(window, loose, shown):
    win = window.ask_about_folder(window.form.folder_question(loose))
    buttons_in(win.get_child())["Add"].emit("clicked")
    assert len(window.form.games) == 1
    assert window.form.games[0].source == "Files"
    assert window.form.games[0].setup_exe is None


def test_adds_it_with_the_installer_that_was_picked(window, loose, shown):
    win = window.ask_about_folder(window.form.folder_question(loose))
    body = win.get_child()
    chooser = list_in(body)
    chooser.select_row(chooser.get_row_at_index(1))
    buttons_in(body)["Add"].emit("clicked")
    assert window.form.games[0].setup_exe == loose / "PortableGame.exe"


def test_adds_nothing_when_cancelled(window, loose, shown):
    # Cancel is not the same answer as "no installer": that one adds the folder.
    win = window.ask_about_folder(window.form.folder_question(loose))
    buttons_in(win.get_child())["Cancel"].emit("clicked")
    assert window.form.games == []


def test_warns_when_the_folder_is_where_the_downloads_live(window, tmp_path, shown):
    shelf = tmp_path / "shelf"
    for name in ("game one", "game two"):
        (shelf / name).mkdir(parents=True)
        with open(shelf / name / "setup_a_game_1.0.exe", "wb") as f:
            f.truncate(1 << 20)
    q = window.form.folder_question(shelf)
    win = window.ask_about_folder(q)
    assert q.warning in labels_in(win.get_child())
    win.destroy()


def test_leaves_that_warning_out_for_an_ordinary_game_folder(window, loose, shown):
    win = window.ask_about_folder(window.form.folder_question(loose))
    assert not any("sit in subfolders" in text for text in labels_in(win.get_child()))
    win.destroy()
