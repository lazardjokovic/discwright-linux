"""Draw the window in a few states and save each as a PNG.

For looking at the window without a desktop, or without disturbing one: run it
under a virtual display,

    xvfb-run -a env GDK_BACKEND=x11 python tools/window_screenshots.py OUT_DIR [GOG_DIR]

GOG_DIR is a folder of GOG downloads (Alan Wake in it, and artwork/), as on the
machine this was written on; without it the shots show an empty form only.
"""

from __future__ import annotations

import sys
from pathlib import Path

import gi

gi.require_version("Gtk", "4.0")
from gi.repository import GLib, Gtk  # noqa: E402

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "src"))
from discwright.form import Form  # noqa: E402
from discwright.window import DiscWindow, DiscWrightApp  # noqa: E402


def settle(seconds: float = 0.6) -> None:
    """Let GTK lay out and draw before the picture is taken."""
    ctx = GLib.MainContext.default()
    end = GLib.get_monotonic_time() + int(seconds * 1e6)
    while GLib.get_monotonic_time() < end:
        ctx.iteration(False)


def shoot(win: Gtk.Window, path: Path) -> None:
    # A window that has not drawn yet snapshots as nothing, so wait until it has.
    for _ in range(20):
        settle(0.3)
        w, h = win.get_width(), win.get_height()
        paintable = Gtk.WidgetPaintable.new(win)
        snap = Gtk.Snapshot()
        paintable.snapshot(snap, w, h)
        node = snap.to_node()
        if node is not None:
            break
    else:
        raise RuntimeError(f"the window never drew, for {path.name}")
    texture = win.get_native().get_renderer().render_texture(node, None)
    texture.save_to_png(str(path))
    print(f"  {path.name}  {w}x{h}")


def states(gog: Path | None):
    yield "1-empty", Form(), None
    if gog is None:
        return
    f = Form()
    f.add_game(gog / "Alan Wake")
    f.icon = gog / "artwork" / "alanwake-icon.ico"
    f.background = gog / "artwork" / "alanwake-background.jpg"
    f.manual = gog / "media" / "AlanWake_manual" / "alan_wake_manual" / "Alan Wake manual.pdf"
    f.extras = gog / "media" / "DiscExtras"
    f.out_dir = Path("/tmp/dw-shots-out")
    yield "2-ready", f, 0
    f2 = Form(**{k: getattr(f, k) for k in ("games", "label", "icon", "background", "manual",
                                            "extras", "out_dir")})
    f2.menu = False
    yield "3-menu-off", f2, None
    f3 = Form(**{k: getattr(f, k) for k in ("games", "label", "icon", "background", "manual",
                                            "extras", "out_dir")})
    f3.bg_as_is = True
    f3.label = "Żółw: The Game"
    yield "4-as-is-and-a-label-windows-cannot-show", f3, None
    f4 = Form(**{k: getattr(f, k) for k in ("games", "label", "icon", "background", "manual",
                                            "extras", "out_dir")})
    f4.building = True
    yield "5-building", f4, None


def main() -> int:
    out = Path(sys.argv[1])
    gog = Path(sys.argv[2]) if len(sys.argv) > 2 else None
    out.mkdir(parents=True, exist_ok=True)
    app = DiscWrightApp()
    app.register(None)
    for name, form, selected in states(gog):
        win = DiscWindow(app, form)
        win.set_default_size(820, 1380)       # tall enough to show every step at once
        win.present()
        if selected is not None:
            win.selected = selected
            win.refresh()
        shoot(win, out / f"{name}.png")
        win.destroy()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
