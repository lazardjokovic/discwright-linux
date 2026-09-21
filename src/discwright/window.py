"""The DiscWright window, in GTK4.

The same six numbered steps as the Windows app, top to bottom: the installers,
the label, the icon, the menu, the extra content, the output folder. Nothing
here decides anything. form.py holds the state and the rules; this draws them,
greys out whatever cannot be used right now with a tooltip saying why, and
passes clicks back. Every problem is shown in a dialog, never only in the log.

Needs GTK 4.10 or newer, for FileDialog and AlertDialog: Ubuntu 24.04 and
Fedora 39 have it.
"""

from __future__ import annotations

import subprocess
import threading
from pathlib import Path

import gi

gi.require_version("Gtk", "4.0")
from gi.repository import Gio, GLib, Gtk, Pango  # noqa: E402

from . import __version__  # noqa: E402
from .build import build  # noqa: E402
from .form import BUTTONS, Form  # noqa: E402
from .games import format_size  # noqa: E402
from .icons import check_background, check_icon  # noqa: E402
from .iso import IsoError, iso_path  # noqa: E402
from .stage import StagingError  # noqa: E402
from .text import encode_ansi, volume_label  # noqa: E402

APP_ID = "com.discwright.DiscWright"


def _file_filter(name: str, patterns: list[str]) -> Gtk.FileFilter:
    f = Gtk.FileFilter()
    f.set_name(name)
    for p in patterns:
        f.add_pattern(p)
        f.add_pattern(p.upper())
    return f


PICTURES = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.ico"]


class DiscWindow(Gtk.ApplicationWindow):
    def __init__(self, app: Gtk.Application, form: Form | None = None):
        super().__init__(application=app, title=f"DiscWright {__version__}")
        self.set_default_size(820, 900)
        self.form = form or Form()
        self.selected: int | None = None
        self.widgets: dict[str, list[Gtk.Widget]] = {}
        self._refreshing = False

        header = Gtk.HeaderBar()
        self.set_titlebar(header)
        self.btn_new = self._button("New disc", self.on_new, "new_disc")
        self.btn_open = self._button("Open project...", self.on_open_project)
        self.btn_folder = self._button("Show disc folder", self.on_show_folder, "show_folder")
        for b in (self.btn_new, self.btn_open):
            header.pack_start(b)
        header.pack_end(self.btn_folder)

        body = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=12)
        body.set_margin_top(12)
        body.set_margin_bottom(12)
        body.set_margin_start(16)
        body.set_margin_end(16)
        body.append(self._step_games())
        body.append(self._step_label())
        body.append(self._step_icon())
        body.append(self._step_menu())
        body.append(self._step_extra())
        body.append(self._step_output())
        scroller = Gtk.ScrolledWindow(vexpand=True, hscrollbar_policy=Gtk.PolicyType.NEVER)
        scroller.set_child(body)

        self.progress = Gtk.ProgressBar(show_text=True, text="")
        self.log_view = Gtk.TextView(editable=False, cursor_visible=False, monospace=True,
                                     wrap_mode=Gtk.WrapMode.WORD_CHAR)
        log_scroll = Gtk.ScrolledWindow(min_content_height=130)
        log_scroll.set_child(self.log_view)
        self.btn_build = self._button("BUILD ISO", self.on_build, "build")
        self.btn_build.add_css_class("suggested-action")
        bottom = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        bottom.set_margin_start(16)
        bottom.set_margin_end(16)
        bottom.set_margin_bottom(12)
        row = Gtk.Box(spacing=8)
        self.progress.set_hexpand(True)
        row.append(self.progress)
        row.append(self.btn_build)
        bottom.append(row)
        bottom.append(log_scroll)

        outer = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        outer.append(scroller)
        outer.append(Gtk.Separator())
        outer.append(bottom)
        self.set_child(outer)
        self.refresh()

    # ---- building blocks -----------------------------------------------------------

    def _register(self, name: str | None, *widgets: Gtk.Widget) -> None:
        if name:
            self.widgets.setdefault(name, []).extend(widgets)

    def _button(self, text: str, handler, name: str | None = None) -> Gtk.Button:
        b = Gtk.Button(label=text)
        b.connect("clicked", lambda *_: handler())
        self._register(name, b)
        return b

    def _check(self, text: str, attr: str, name: str | None = None) -> Gtk.CheckButton:
        c = Gtk.CheckButton(label=text, active=bool(getattr(self.form, attr)))

        def toggled(btn):
            if not self._refreshing:
                setattr(self.form, attr, btn.get_active())
                self.refresh()
        c.connect("toggled", toggled)
        self._register(name or attr, c)
        return c

    def _step(self, number: int, title: str, child: Gtk.Widget) -> Gtk.Frame:
        frame = Gtk.Frame()
        label = Gtk.Label(xalign=0)
        label.set_markup(f"<b>{number})  {GLib.markup_escape_text(title)}</b>")
        frame.set_label_widget(label)
        child.set_margin_top(8)
        child.set_margin_bottom(10)
        child.set_margin_start(10)
        child.set_margin_end(10)
        frame.set_child(child)
        return frame

    def _file_row(self, caption: str, attr: str, name: str, filters=None,
                  folder: bool = False) -> Gtk.Box:
        """A caption, the chosen path, and a Browse... button."""
        row = Gtk.Box(spacing=8)
        lab = Gtk.Label(label=caption, xalign=0, width_chars=16)
        shown = Gtk.Label(xalign=0, hexpand=True, ellipsize=Pango.EllipsizeMode.MIDDLE,
                          selectable=True)
        btn = Gtk.Button(label="Browse...")
        btn.connect("clicked", lambda *_: self._choose(attr, filters, folder))
        row.append(lab)
        row.append(shown)
        row.append(btn)
        self._register(name, lab, shown, btn)
        setattr(self, f"_shown_{attr}", shown)
        return row

    # ---- the six steps ---------------------------------------------------------------

    def _step_games(self) -> Gtk.Frame:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        self.games_list = Gtk.ListBox(selection_mode=Gtk.SelectionMode.SINGLE)
        self.games_list.set_placeholder(Gtk.Label(label="No games yet. Add a GOG download folder."))
        self.games_list.connect("row-selected", self.on_row_selected)
        frame = Gtk.Frame()
        frame.set_child(self.games_list)
        box.append(frame)
        row = Gtk.Box(spacing=8)
        row.append(self._button("Add game...", self.on_add_game, "add_game"))
        row.append(self._button("Add-on...", self.on_add_on, "add_on"))
        row.append(self._button("Rename...", self.on_rename, "rename"))
        row.append(self._button("Remove", self.on_remove, "remove"))
        self.summary = Gtk.Label(xalign=1, hexpand=True)
        self.summary.add_css_class("dim-label")
        row.append(self.summary)
        box.append(row)
        return self._step(1, "Installers on this disc", box)

    def _step_label(self) -> Gtk.Frame:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4)
        self.label_entry = Gtk.Entry(placeholder_text="The name This PC shows for the disc")
        self.label_entry.connect("changed", self.on_label_changed)
        self.label_preview = Gtk.Label(xalign=0, wrap=True)
        self.label_preview.add_css_class("dim-label")
        box.append(self.label_entry)
        box.append(self.label_preview)
        return self._step(2, "Disc label (shown in This PC)", box)

    def _step_icon(self) -> Gtk.Frame:
        box = Gtk.Box(spacing=12)
        self.icon_picture = Gtk.Picture(can_shrink=True, content_fit=Gtk.ContentFit.CONTAIN)
        self.icon_picture.set_size_request(72, 72)
        right = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4, hexpand=True)
        right.append(self._file_row("Icon", "icon", "icon",
                                    [_file_filter("Icons and pictures", PICTURES)]))
        self.icon_msg = Gtk.Label(xalign=0, wrap=True)
        self.icon_msg.add_css_class("dim-label")
        right.append(self.icon_msg)
        box.append(self.icon_picture)
        box.append(right)
        return self._step(3, "Disc icon", box)

    def _step_menu(self) -> Gtk.Frame:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        box.append(self._check("Autorun menu: opens when the disc goes into a Windows PC", "menu", "menu"))
        box.append(self._file_row("Background image", "background", "background",
                                  [_file_filter("Pictures", PICTURES[:-1])]))
        box.append(self._check("Use the picture as it is (already 760x480 and composed)", "bg_as_is"))

        look = Gtk.Box(spacing=12)
        side_label = Gtk.Label(label="Buttons on the")
        self.side = Gtk.DropDown.new_from_strings(["Right", "Left"])
        self.side.connect("notify::selected", self.on_side_changed)
        self._register("panel_side", side_label, self.side)
        look.append(side_label)
        look.append(self.side)
        look.append(self._check("Divider line", "divider"))
        look.append(self._check("Outline round the window", "window_border"))
        self.bordered = Gtk.CheckButton(label="Box round each button")
        self.bordered.connect("toggled", self.on_bordered)
        self._register("button_style", self.bordered)
        look.append(self.bordered)
        box.append(look)

        title = Gtk.Box(spacing=8)
        title.append(self._check("Title on the artwork", "show_title"))
        self.title_entry = Gtk.Entry(hexpand=True, placeholder_text="Blank: the disc label")
        self.title_entry.connect("changed", self.on_title_changed)
        self._register("title_text", self.title_entry)
        title.append(self.title_entry)
        box.append(title)

        btns = Gtk.Box(spacing=12)
        btn_label = Gtk.Label(label="Buttons:")
        self._register("buttons", btn_label)
        btns.append(btn_label)
        self.button_checks = {}
        for b in BUTTONS:
            c = Gtk.CheckButton(label=b)
            c.connect("toggled", self.on_button_toggled, b)
            self._register("buttons", c)
            self.button_checks[b] = c
            btns.append(c)
        box.append(btns)

        box.append(self._check("Music while the menu is open", "music_on"))
        box.append(self._file_row("Music file", "music", "music",
                                  [_file_filter("Music", ["*.mp3", "*.wma", "*.wav", "*.mid"])]))
        box.append(self._file_row("Manual", "manual", "manual",
                                  [_file_filter("Manuals", ["*.pdf", "*.txt", "*.htm", "*.html"])]))
        box.append(self._file_row("Extras folder", "extras", "extras", folder=True))
        return self._step(4, "Autorun menu", box)

    def _step_extra(self) -> Gtk.Frame:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        hint = Gtk.Label(xalign=0, wrap=True,
                         label="Copied to the root of the disc under their own names.")
        hint.add_css_class("dim-label")
        box.append(hint)
        self.extra_list = Gtk.ListBox(selection_mode=Gtk.SelectionMode.SINGLE)
        self.extra_list.set_placeholder(Gtk.Label(label="Nothing extra."))
        self._register("add_extra", self.extra_list)
        frame = Gtk.Frame()
        frame.set_child(self.extra_list)
        box.append(frame)
        row = Gtk.Box(spacing=8)
        row.append(self._button("Add file...", lambda: self._choose_extra(False), "add_extra"))
        row.append(self._button("Add folder...", lambda: self._choose_extra(True), "add_extra"))
        row.append(self._button("Remove", self.on_remove_extra, "remove_extra"))
        box.append(row)
        box.append(self._check("Name the disc on Linux too: its name and icon on a Linux desktop",
                               "linux_info", "linux_info"))
        return self._step(5, "Extra content", box)

    def _step_output(self) -> Gtk.Frame:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4)
        box.append(self._file_row("Output folder", "out_dir", "out_dir", folder=True))
        self.iso_label = Gtk.Label(xalign=0, wrap=True)
        self.iso_label.add_css_class("dim-label")
        box.append(self.iso_label)
        return self._step(6, "Output folder", box)

    # ---- drawing the form ------------------------------------------------------------

    def refresh(self) -> None:
        """Draw the form as it is: every value, and whether each control can be
        used, with the reason on its tooltip when it cannot."""
        self._refreshing = True
        try:
            f = self.form
            self._fill_games()
            self.summary.set_text(f.summary())
            if self.label_entry.get_text() != f.label:
                self.label_entry.set_text(f.label)
            self._draw_label_preview()
            for attr in ("icon", "background", "music", "manual", "extras", "out_dir"):
                value = getattr(f, attr)
                getattr(self, f"_shown_{attr}").set_text(str(value) if value else "(none)")
            self._draw_icon()
            self.side.set_selected(1 if f.panel_side == "Left" else 0)
            self.bordered.set_active(f.button_style == "Bordered")
            if self.title_entry.get_text() != f.title_text:
                self.title_entry.set_text(f.title_text)
            for b, c in self.button_checks.items():
                c.set_active(b in f.buttons)
            self._fill_extras()
            iso = iso_path(f.out_dir, f.label) if f.out_dir else None
            self.iso_label.set_text(f"Writes {iso.name} and a disc folder here." if iso else "")
            self.btn_build.set_label("REBUILD ISO" if iso and iso.exists() else "BUILD ISO")

            for name, state in f.controls(self.selected).items():
                for w in self.widgets.get(name, []):
                    w.set_sensitive(state.enabled)
                    w.set_tooltip_text(None if state.enabled else state.why)
            # Controls with no rule of their own follow the build lock.
            for name in ("menu", "linux_info", "icon", "out_dir"):
                for w in self.widgets.get(name, []):
                    w.set_sensitive(not f.building)
            self.btn_open.set_sensitive(not f.building)
            self.label_entry.set_sensitive(not f.building)
            self.games_list.set_sensitive(not f.building)
            for b in self.button_checks.values():
                b.set_sensitive(f.menu and not f.building)
        finally:
            self._refreshing = False

    def _fill_games(self) -> None:
        while (row := self.games_list.get_row_at_index(0)) is not None:
            self.games_list.remove(row)
        for i, g in enumerate(self.form.games):
            if g.kind == "AddOn":
                parent = self.form.games[g.parent_index].game_name if g.parent_index >= 0 else "?"
                text = f"      add-on: {g.game_name}   (for {parent}, {format_size(g.total_bytes)})"
            else:
                files = f"{len(g.files)} file" + ("s" if len(g.files) != 1 else "")
                text = f"{g.game_name}   ({files}, {format_size(g.total_bytes)})"
            lab = Gtk.Label(label=text, xalign=0)
            lab.set_margin_top(4)
            lab.set_margin_bottom(4)
            lab.set_margin_start(6)
            if g.missing_parts or g.warning:
                lab.set_tooltip_text(g.warning or "Installer parts are missing.")
            self.games_list.append(lab)
        if self.selected is not None and self.selected < len(self.form.games):
            self.games_list.select_row(self.games_list.get_row_at_index(self.selected))
        else:
            self.selected = None

    def _fill_extras(self) -> None:
        while (row := self.extra_list.get_row_at_index(0)) is not None:
            self.extra_list.remove(row)
        for p in self.form.extra_items:
            lab = Gtk.Label(label=str(p), xalign=0)
            lab.set_margin_start(6)
            self.extra_list.append(lab)

    def _draw_label_preview(self) -> None:
        label = self.form.label.strip()
        if not label:
            self.label_preview.set_text("")
            return
        shown = encode_ansi(label).decode("cp1252", errors="replace")
        text = f"Volume id: {volume_label(label)}"
        if shown != label:
            text += f"   This PC will show: {shown}"
        self.label_preview.set_text(text)

    def _draw_icon(self) -> None:
        icon = self.form.icon
        if icon is None:
            self.icon_picture.set_filename(None)
            self.icon_msg.set_text("An .ico is used as it is; any other picture is made into one.")
            return
        chk = check_icon(icon)
        self.icon_msg.set_text(chk.msg)
        self.icon_picture.set_filename(str(icon) if chk.ok else None)

    # ---- choosing files --------------------------------------------------------------

    def _choose(self, attr: str, filters, folder: bool) -> None:
        dialog = Gtk.FileDialog(modal=True)
        if filters:
            store = Gio.ListStore.new(Gtk.FileFilter)
            for flt in filters:
                store.append(flt)
            dialog.set_filters(store)

        def done(dlg, result):
            try:
                gfile = dlg.select_folder_finish(result) if folder else dlg.open_finish(result)
            except GLib.Error:
                return                          # cancelled
            self._chosen(attr, Path(gfile.get_path()))
        if folder:
            dialog.select_folder(self, None, done)
        else:
            dialog.open(self, None, done)

    def _chosen(self, attr: str, path: Path) -> None:
        # A picture that cannot be used is refused now, in a dialog, not left to
        # fail at build time with a message naming no file at all.
        if attr == "icon":
            chk = check_icon(path)
            if not chk.ok:
                return self.alert("This icon cannot be used", f"{path}\n\n{chk.msg}")
        if attr == "background":
            chk = check_background(path)
            if not chk.ok:
                return self.alert("This background cannot be used", f"{path}\n\n{chk.msg}")
            # 760x480 is almost certainly a background composed earlier: composing
            # it again would darken it twice and stamp the title twice. Ported from
            # Test-ComposedBg.
            self.form.bg_as_is = (chk.width, chk.height) == (760, 480)
        setattr(self.form, attr, path)
        self.refresh()

    def _choose_extra(self, folder: bool) -> None:
        dialog = Gtk.FileDialog(modal=True)

        def done(dlg, result):
            try:
                gfile = dlg.select_folder_finish(result) if folder else dlg.open_finish(result)
            except GLib.Error:
                return
            self.form.extra_items.append(Path(gfile.get_path()))
            self.refresh()
        if folder:
            dialog.select_folder(self, None, done)
        else:
            dialog.open(self, None, done)

    # ---- the games list --------------------------------------------------------------

    def on_row_selected(self, _box, row) -> None:
        if self._refreshing:
            return
        self.selected = row.get_index() if row is not None else None
        self.refresh()

    def on_add_game(self) -> None:
        dialog = Gtk.FileDialog(modal=True, title="The folder a GOG download came in")

        def done(dlg, result):
            try:
                folder = Path(dlg.select_folder_finish(result).get_path())
            except GLib.Error:
                return
            err = self.form.add_game(folder)
            if err:
                return self.alert("This folder cannot be added", f"{folder}\n\n{err}")
            self.refresh()
        dialog.select_folder(self, None, done)

    def on_add_on(self) -> None:
        parent = self.form.parent_for(self.selected)
        if parent is None:
            return
        dialog = Gtk.FileDialog(modal=True, title=f"An add-on for {self.form.games[parent].game_name}")
        store = Gio.ListStore.new(Gtk.FileFilter)
        store.append(_file_filter("Installers", ["*.exe"]))
        dialog.set_filters(store)

        def done(dlg, result):
            try:
                exe = Path(dlg.open_finish(result).get_path())
            except GLib.Error:
                return
            err = self.form.add_on(exe, parent)
            if err:
                return self.alert("This add-on cannot be added", f"{exe}\n\n{err}")
            self.refresh()
        dialog.open(self, None, done)

    def on_rename(self) -> None:
        if self.selected is None:
            return
        index = self.selected
        g = self.form.games[index]
        win = Gtk.Window(transient_for=self, modal=True, title="Name on the menu",
                         default_width=420, resizable=False)
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        for m in ("set_margin_top", "set_margin_bottom", "set_margin_start", "set_margin_end"):
            getattr(box, m)(14)
        entry = Gtk.Entry(text=g.game_name or "")
        note = Gtk.Label(xalign=0, wrap=True,
                         label=f"Play still looks for \"{g.match_name}\", the name the installer "
                               "registers, so renaming does not stop it finding the game.")
        note.add_css_class("dim-label")
        row = Gtk.Box(spacing=8, halign=Gtk.Align.END)
        cancel = Gtk.Button(label="Cancel")
        ok = Gtk.Button(label="Rename")
        ok.add_css_class("suggested-action")
        row.append(cancel)
        row.append(ok)
        for w in (entry, note, row):
            box.append(w)
        win.set_child(box)

        def apply(*_):
            err = self.form.rename(index, entry.get_text())
            if err:
                self.alert("Cannot rename", err)
                return
            win.close()
            self.refresh()
        cancel.connect("clicked", lambda *_: win.close())
        ok.connect("clicked", apply)
        entry.connect("activate", apply)
        win.present()

    def on_remove(self) -> None:
        if self.selected is None:
            return
        index = self.selected
        g = self.form.games[index]
        kids = [i for i, e in enumerate(self.form.games) if e.parent_index == index]

        def remove(with_add_ons: bool) -> None:
            self.form.remove(index, with_add_ons)
            self.selected = None
            self.refresh()
        if not kids:
            return remove(False)
        # Its add-ons either go with it, or stay as games of their own. Asked,
        # because deleting an installer somebody chose is not a default.
        self.ask(f"Remove {g.game_name}?",
                 f"It has {len(kids)} add-on{'s' if len(kids) != 1 else ''} on this disc.",
                 ["Cancel", "Keep the add-ons as games", "Remove them too"],
                 lambda choice: None if choice == 0 else remove(choice == 2))

    def on_remove_extra(self) -> None:
        row = self.extra_list.get_selected_row()
        index = row.get_index() if row is not None else len(self.form.extra_items) - 1
        if 0 <= index < len(self.form.extra_items):
            del self.form.extra_items[index]
        self.refresh()

    # ---- the rest of the form --------------------------------------------------------

    def on_label_changed(self, entry) -> None:
        if not self._refreshing:
            self.form.set_label(entry.get_text())
            self.refresh()

    def on_title_changed(self, entry) -> None:
        if not self._refreshing:
            self.form.title_text = entry.get_text()

    def on_side_changed(self, dropdown, _pspec) -> None:
        if not self._refreshing:
            self.form.panel_side = "Left" if dropdown.get_selected() == 1 else "Right"

    def on_bordered(self, check) -> None:
        if not self._refreshing:
            self.form.button_style = "Bordered" if check.get_active() else "Minimal"

    def on_button_toggled(self, check, name: str) -> None:
        if self._refreshing:
            return
        if check.get_active() and name not in self.form.buttons:
            self.form.buttons.append(name)
        elif not check.get_active() and name in self.form.buttons:
            self.form.buttons.remove(name)
        self.refresh()

    def on_new(self) -> None:
        def clear(choice: int) -> None:
            if choice == 1:
                # The output folder is kept: where discs go does not change when
                # you start the next one.
                self.form = Form(out_dir=self.form.out_dir)
                self.selected = None
                self.refresh()
        self.ask("Start a new disc?", "Everything on this form is cleared. The output folder is kept.",
                 ["Cancel", "Clear"], clear)

    def on_open_project(self) -> None:
        dialog = Gtk.FileDialog(modal=True, title="A disc project (discproject.json)")
        store = Gio.ListStore.new(Gtk.FileFilter)
        store.append(_file_filter("Disc projects", ["discproject.json", "*.json"]))
        dialog.set_filters(store)

        def done(dlg, result):
            try:
                path = Path(dlg.open_finish(result).get_path())
            except GLib.Error:
                return
            form, problems = Form.from_project(path)
            if form is None:
                return self.alert("This project cannot be opened", "\n".join(problems))
            self.form = form
            self.selected = None
            self.refresh()
            if problems:
                self.alert("Some of this project's files are missing",
                           "Opened, without these:\n\n" + "\n".join(problems))
        dialog.open(self, None, done)

    def on_show_folder(self) -> None:
        disc = self.form.out_dir / "disc" if self.form.out_dir else None
        if disc and disc.is_dir():
            subprocess.Popen(["xdg-open", str(disc)])

    # ---- building --------------------------------------------------------------------

    def on_build(self) -> None:
        if self.form.missing():
            return self.alert("The disc cannot be built yet", "\n".join(self.form.missing()))
        warnings = self.form.warnings()
        if warnings:
            self.ask("Build anyway?", "\n\n".join(warnings), ["Cancel", "Build anyway"],
                     lambda choice: choice == 1 and self._start_build())
        else:
            self._start_build()

    def _start_build(self) -> None:
        settings = self.form.to_settings()
        self.form.building = True
        self.log_view.get_buffer().set_text("")
        self.progress.set_fraction(0)
        self.progress.set_text("Staging...")
        self.refresh()

        def log(msg: str) -> None:
            GLib.idle_add(self._append_log, msg)

        def progress(percent: float) -> None:
            GLib.idle_add(self._set_progress, percent)

        def work() -> None:
            try:
                iso = build(settings, log, progress)
                GLib.idle_add(self._build_done, iso, None)
            except (StagingError, IsoError, OSError) as e:
                GLib.idle_add(self._build_done, None, str(e))
        threading.Thread(target=work, daemon=True).start()

    def _append_log(self, msg: str) -> bool:
        buf = self.log_view.get_buffer()
        buf.insert(buf.get_end_iter(), msg + "\n")
        self.log_view.scroll_to_iter(buf.get_end_iter(), 0, False, 0, 0)
        return False

    def _set_progress(self, percent: float) -> bool:
        self.progress.set_fraction(percent / 100)
        self.progress.set_text(f"Writing the ISO: {percent:.0f}%")
        return False

    def _build_done(self, iso: Path | None, error: str | None) -> bool:
        self.form.building = False
        self.refresh()
        if error:
            self.progress.set_text("Stopped")
            self._append_log("ERROR: " + error)
            self.alert("The disc was not built", error)
        else:
            self.progress.set_fraction(1)
            self.progress.set_text("Done")
            self.alert("The disc is built", f"{iso}\n\n{format_size(iso.stat().st_size)}. "
                                            "The project is saved beside it, to open and rebuild later.")
        return False

    # ---- dialogs ---------------------------------------------------------------------

    def alert(self, heading: str, detail: str) -> None:
        Gtk.AlertDialog(message=heading, detail=detail, modal=True).show(self)

    def ask(self, heading: str, detail: str, buttons: list[str], then) -> None:
        dialog = Gtk.AlertDialog(message=heading, detail=detail, modal=True, buttons=buttons,
                                 cancel_button=0, default_button=len(buttons) - 1)

        def done(dlg, result):
            try:
                then(dlg.choose_finish(result))
            except GLib.Error:
                then(0)
        dialog.choose(self, None, done)


class DiscWrightApp(Gtk.Application):
    def __init__(self, form: Form | None = None):
        super().__init__(application_id=APP_ID, flags=Gio.ApplicationFlags.NON_UNIQUE)
        self._form = form

    def do_activate(self):
        DiscWindow(self, self._form).present()


def run(argv: list[str] | None = None) -> int:
    return DiscWrightApp().run(argv or [])
