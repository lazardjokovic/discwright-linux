"""The window's state and rules, with no GTK in it.

Everything the window decides lives here: what has been picked, which controls
can be used right now and why not, what stops a build and what only needs a
yes. The GTK code in window.py draws this and passes clicks back; it decides
nothing itself. That split is what lets the rules be tested without a screen.

The rules are the Windows app's, from Update-ActionButtons, Update-GameButtons
and the checks at the top of its BUILD handler, with one change of policy: where
Windows leaves BUILD clickable and refuses in a dialog, this greys it out and
says why, because nothing that cannot be done right now should stay clickable.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

from .games import (GameInfo, add_on_info, folder_executables, folder_info, format_size,
                    game_info, gog_subfolders)
from .icons import check_background, check_icon
from .iso import iso_path
from .layout import remove_entry
from .project import read_project, settings_from_project
from .settings import DiscSettings
from .text import encode_ansi, remove_control_chars

BUTTONS = ["Play", "Install", "Manual", "Extras", "Exit"]


@dataclass
class Control:
    """Whether a control can be used now, and if not, the sentence saying why."""
    enabled: bool
    why: str = ""


@dataclass
class FolderQuestion:
    """What to ask about a folder that is not a GOG download.

    The Windows app asks it in a dialog (Show-FolderInstallerDialog). The
    wording lives here rather than in the widget, so both halves of this port
    say what Windows says and the sentences can be read without a screen.

    Nothing is refused for a folder that reaches this point: it holds files, so
    it can go on a disc. The question is only what the menu does with it.
    """
    folder: Path
    executables: list[Path] = field(default_factory=list)
    file_count: int = 0
    total_bytes: int = 0
    # GOG downloads sitting in subfolders of the folder that was picked, which
    # is the likeliest way to arrive here by mistake.
    downloads: list[Path] = field(default_factory=list)

    title = "No GOG installer in this folder"

    @property
    def explain(self) -> str:
        return (f"{self.folder.name} holds no setup_*.exe, so it is not a GOG download.\n"
                "Everything in it goes on the disc either way. "
                "What should the menu do with it?")

    @property
    def warning(self) -> str:
        """Empty unless the folder is where the downloads live. A warning and not
        a refusal: a real game folder can have a setup_*.exe buried under it."""
        if not self.downloads:
            return ""
        return (f"{len(self.downloads)} GOG download(s) sit in subfolders of this one, "
                f'starting with "{self.downloads[0].name}".\n'
                "If you meant one of those, Cancel and pick that folder instead.")

    @property
    def note(self) -> str:
        """What is about to go on the disc. A game folder reads as a few files and
        a few GB; the folder holding every download somebody owns reads as tens of
        GB, which is the mis-pick showing itself before anything is added."""
        counted = f"{self.file_count} file(s), {format_size(self.total_bytes)}.  "
        if self.executables:
            return counted + f"{len(self.executables)} executable(s), largest first."
        return counted + "No executables at all, so there is nothing to install."

    @property
    def choices(self) -> list[str]:
        """The answers, in the order they are offered. The safe one comes first
        and is the one selected: a wrong installer is a menu button that runs the
        wrong program, while no installer is only one button fewer."""
        return ["No installer: put the files on the disc and let the menu open the folder"] + [
            f"Install with {e.name}   ({format_size(e.stat().st_size)})" for e in self.executables]

    def installer_for(self, choice: int) -> Path | None:
        """The installer a chosen answer names, or None for "no installer"."""
        if choice <= 0 or choice > len(self.executables):
            return None
        return self.executables[choice - 1]


@dataclass
class Form:
    games: list[GameInfo] = field(default_factory=list)
    label: str = ""
    # The name this app typed into the label itself, from the first game, so it
    # can tell its own guess from something the user wrote. Only its own guess is
    # ever replaced or cleared.
    label_seeded_from: str | None = None
    icon: Path | None = None
    menu: bool = True
    background: Path | None = None
    bg_as_is: bool = False
    panel_side: str = "Right"
    divider: bool = False
    show_title: bool = False
    title_text: str = ""
    buttons: list[str] = field(default_factory=lambda: list(BUTTONS))
    music_on: bool = False
    music: Path | None = None
    manual: Path | None = None
    extras: Path | None = None
    extra_items: list[Path] = field(default_factory=list)
    out_dir: Path | None = None
    linux_info: bool = True
    window_border: bool = True
    button_style: str = "Minimal"
    building: bool = False

    # ---- games ----------------------------------------------------------------------

    def add_game(self, folder: str | Path) -> str | None:
        """Add a GOG download folder. Returns what went wrong, or None."""
        info = game_info(folder)
        if not info.ok:
            return info.msg
        return self._add(info)

    def folder_question(self, folder: str | Path) -> FolderQuestion | None:
        """What to ask about a folder the GOG reader would not take, or None
        when there is nothing to ask.

        Nothing to ask covers three cases, and each is a different answer: the
        folder is a GOG download after all, so add_game takes it; it is not
        there, or it holds no files at all, so add_game refuses it with its own
        message. A dialog offering a choice between none of nought executables
        is not a question, and on Windows it was what hung the test suite.
        """
        folder = Path(folder)
        info = game_info(folder)
        if info.ok or not info.msg.startswith("No GOG"):
            return None
        files = [p for p in folder.rglob("*") if p.is_file()] if folder.is_dir() else []
        if not files:
            return None
        return FolderQuestion(
            folder=folder,
            executables=folder_executables(folder, files=files),
            file_count=len(files),
            total_bytes=sum(p.stat().st_size for p in files),
            downloads=gog_subfolders(folder),
        )

    def add_files(self, folder: str | Path, installer: str | Path | None = None) -> str | None:
        """Add a folder of game files, with the installer the question named, or
        none. Returns what went wrong, or None."""
        info = folder_info(folder, installer)
        if not info.ok:
            return info.msg
        return self._add(info)

    def _add(self, info: GameInfo) -> None:
        self.games.append(info)
        # The first game names the disc, unless the user already has.
        if not self.label.strip():
            self.label = info.game_name or ""
            self.label_seeded_from = self.label
        return None

    def add_on(self, exe: str | Path, parent_index: int) -> str | None:
        """Add an add-on installer under the game at parent_index."""
        if not (0 <= parent_index < len(self.games)) or self.games[parent_index].kind == "AddOn":
            return "An add-on belongs to a game on this disc. Pick the game it is for."
        info = add_on_info(exe)
        if not info.ok:
            return info.msg
        info.parent_index = parent_index
        self.games.append(info)
        return None

    def remove(self, index: int, with_add_ons: bool = False) -> None:
        self.games = remove_entry(self.games, index, with_add_ons)
        # The label was only ever this app's guess from the first game: with no
        # games left it describes nothing, so it goes. A label the user typed
        # stays, whatever happens to the list.
        if not self.games and self.label_is_seeded():
            self.label = ""
            self.label_seeded_from = None

    def label_is_seeded(self) -> bool:
        return self.label_seeded_from is not None and self.label.strip() == self.label_seeded_from.strip()

    def set_label(self, text: str) -> None:
        self.label = remove_control_chars(text) or ""

    def parents(self) -> list[int]:
        """The entries an add-on can belong to: the games, not other add-ons."""
        return [i for i, g in enumerate(self.games) if g.kind != "AddOn"]

    def parent_for(self, selected: int | None) -> int | None:
        """The game a new add-on would go under: the one picked in the list, or
        the game an add-on picked there belongs to, or the only game there is.
        None when that is not clear, so the question is put to the user rather
        than answered for them."""
        if selected is not None and 0 <= selected < len(self.games):
            g = self.games[selected]
            return selected if g.kind != "AddOn" else (g.parent_index if g.parent_index >= 0 else None)
        parents = self.parents()
        return parents[0] if len(parents) == 1 else None

    def rename(self, index: int, name: str) -> str | None:
        """Change the name an entry has on the menu. The name the menu looks for
        in the registry stays what the installer said, or Play stops finding an
        installed game that was renamed here."""
        name = (remove_control_chars(name) or "").strip()
        if not name:
            return "A game needs a name on the menu."
        self.games[index].game_name = name
        return None

    def total_bytes(self) -> int:
        return sum(g.total_bytes for g in self.games)

    # ---- which controls can be used, and why not ------------------------------------

    def controls(self, selected: int | None = None) -> dict[str, Control]:
        busy = "A build is running."
        if self.building:
            return {name: Control(False, busy) for name in CONTROL_NAMES}

        menu_off = "The autorun menu is switched off. Tick it at the top of step 4."
        as_is = ("Painted into the picture. To change it, pick the original artwork "
                 "and untick \"Use as it is\".")
        c: dict[str, Control] = {}
        has_game = bool(self.parents())
        c["add_game"] = Control(True)
        if not has_game:
            c["add_on"] = Control(False, "An add-on belongs to a game. Add the game first.")
        else:
            c["add_on"] = Control(self.parent_for(selected) is not None,
                                  "Pick the game in the list that the add-on is for.")
        c["remove"] = Control(selected is not None, "Pick an entry in the list first.")
        c["rename"] = Control(selected is not None, "Pick an entry in the list first.")
        for name in ("background", "bg_as_is", "buttons", "music_on", "window_border", "button_style"):
            c[name] = Control(self.menu, menu_off)
        # Painted into bg.png, which is skipped when the picture is used as it is.
        composes = self.menu and not self.bg_as_is
        why = menu_off if not self.menu else as_is
        for name in ("panel_side", "divider", "show_title"):
            c[name] = Control(composes, why)
        c["title_text"] = Control(composes and self.show_title,
                                  why if not composes else "Tick \"Title on the artwork\" first.")
        c["music"] = Control(self.menu and self.music_on,
                             menu_off if not self.menu else "Tick \"Music\" first.")
        c["manual"] = Control(self.menu and "Manual" in self.buttons,
                              menu_off if not self.menu else "Tick the Manual button first.")
        c["extras"] = Control(self.menu and "Extras" in self.buttons,
                              menu_off if not self.menu else "Tick the Extras button first.")
        c["add_extra"] = Control(True)
        c["remove_extra"] = Control(bool(self.extra_items), "There is no extra content to remove.")
        missing = self.missing()
        c["build"] = Control(not missing, missing[0] if missing else "")
        c["new_disc"] = Control(self.is_dirty(), "Nothing to clear: this is already a new disc.")
        c["show_folder"] = Control(bool(self.out_dir and (self.out_dir / "disc").is_dir()),
                                   "Nothing has been built in the output folder yet.")
        return c

    def is_dirty(self) -> bool:
        return bool(self.games or self.icon or self.background or self.extra_items
                    or (self.label.strip() and not self.label_is_seeded()))

    # ---- what stops a build, and what only needs a yes ------------------------------

    def missing(self) -> list[str]:
        """Everything that stops a build, in step order, as sentences. Empty when
        it can build. The first is shown on the greyed-out BUILD button."""
        out = []
        if not self.games:
            out.append("Add a game first (step 1).")
        if not self.label.strip():
            out.append("Give the disc a label (step 2).")
        elif iso_path("x", self.label) is None:
            out.append("The label needs at least one letter or digit to name the ISO (step 2).")
        if self.icon is None:
            out.append("Choose a disc icon (step 3).")
        else:
            chk = check_icon(self.icon)
            if not chk.ok:
                out.append(f"The icon cannot be used: {chk.msg} (step 3)")
        if self.menu:
            if self.background is None:
                out.append("The menu needs a background image (step 4), or switch the menu off.")
            else:
                chk = check_background(self.background)
                if not chk.ok:
                    out.append(f"The background cannot be used: {chk.msg} (step 4)")
            if not self.buttons:
                out.append("The menu has no buttons. Tick at least one (step 4).")
            if self.music_on and self.music is None:
                out.append("Choose the music, or untick it (step 4).")
        if self.out_dir is None:
            out.append("Choose an output folder (step 6).")
        return out

    def warnings(self) -> list[str]:
        """What a build goes ahead with after a yes: the Windows app confirms
        these rather than refusing, because either can be a false alarm."""
        out = []
        shown = encode_ansi(self.label).decode("cp1252", errors="replace")
        if shown != self.label:
            out.append("Windows cannot show this disc label in full. AutoRun reads it in the "
                       "Windows-1252 codepage and has no Unicode mode.\n\n"
                       f"You typed:  {self.label}\nThis PC will show:  {shown}")
        for g in self.games:
            if g.missing_parts:
                parts = ", ".join(f"-{n}.bin" for n in g.missing_parts)
                out.append(f"{g.game_name} looks incomplete: installer parts {parts} are missing. "
                           "A disc built from it looks fine and fails when the installer runs. "
                           "Re-download the game from GOG first.")
        return out

    def summary(self) -> str:
        n = len([g for g in self.games if g.kind != "AddOn"])
        a = len(self.games) - n
        if not self.games:
            return "No games yet."
        parts = [f"{n} game{'s' if n != 1 else ''}"]
        if a:
            parts.append(f"{a} add-on{'s' if a != 1 else ''}")
        return ", ".join(parts) + f", {format_size(self.total_bytes())}"

    # ---- to and from the rest of the app ---------------------------------------------

    def to_settings(self) -> DiscSettings:
        return DiscSettings(
            games=list(self.games), label=self.label.strip(), out_dir=Path(self.out_dir or "."),
            icon_path=self.icon, icon_is_ico=bool(self.icon and self.icon.suffix.casefold() == ".ico"),
            menu=self.menu, bg_path=self.background if self.menu else None, bg_as_is=self.bg_as_is,
            panel_side=self.panel_side, divider=self.divider, show_title=self.show_title,
            title_text=self.title_text, window_border=self.window_border,
            button_style=self.button_style,
            music_file=self.music if (self.menu and self.music_on) else None,
            buttons=[b for b in BUTTONS if b in self.buttons],
            manual_path=self.manual if "Manual" in self.buttons else None,
            extras_path=self.extras if "Extras" in self.buttons else None,
            extra_items=list(self.extra_items), linux_info=self.linux_info)

    @classmethod
    def from_project(cls, path: str | Path) -> tuple[Form | None, list[str]]:
        """A form holding what a project file describes. None and the reasons
        when it cannot be opened; a form and a list of problems when some of its
        files have gone, so the rest is still there to fix."""
        p = read_project(path)
        if p is None:
            return None, [f"{path} is not a disc project file."]
        s, problems = settings_from_project(p)
        f = cls(games=s.games, label=s.label, icon=s.icon_path, menu=s.menu,
                background=s.bg_path, bg_as_is=s.bg_as_is, panel_side=s.panel_side,
                divider=s.divider, show_title=s.show_title, title_text=s.title_text,
                buttons=list(s.buttons), music_on=s.music_file is not None, music=s.music_file,
                manual=s.manual_path, extras=s.extras_path, extra_items=list(s.extra_items),
                out_dir=s.out_dir, linux_info=s.linux_info, window_border=s.window_border,
                button_style=s.button_style)
        return f, problems


CONTROL_NAMES = ("add_game", "add_on", "remove", "rename", "background", "bg_as_is", "buttons",
                 "music_on", "window_border", "button_style", "panel_side", "divider",
                 "show_title", "title_text", "music", "manual", "extras", "add_extra", "remove_extra",
                 "build", "new_disc", "show_folder")
