"""The discwright command: a disc built from the command line.

This is the Linux app. The Windows one is a window of numbered steps, and the
options below are those steps: the games, the label, the icon, the menu, the
extra content, the output folder. What each one does, and why it is there, is in
the module that carries it out.

    discwright build --game ~/GOG/Alan_Wake --icon cover.png \\
                     --background art.jpg --out ~/discs/alanwake

Anything that would make an unusable disc is refused before a file is copied,
with a message naming what to do about it.
"""

from __future__ import annotations

import argparse
import shlex
import sys
from pathlib import Path

from . import __version__
from .build import build
from .games import (GameInfo, add_on_info, folder_executables, folder_info,
                    format_size, game_info, gog_subfolders)
from .iso import IsoError
from .project import read_project, settings_from_project
from .settings import DiscSettings
from .stage import StagingError, stage

BUTTONS = ["Play", "Install", "Manual", "Extras", "Exit"]


class _Entry(argparse.Action):
    """--game and --add-on in the order they were typed, so an add-on belongs to
    the game before it, the way the window files it under the game it is for."""

    def __call__(self, parser, namespace, value, option_string=None):
        entries = getattr(namespace, "entries", None) or []
        entries.append((self.dest, value))
        namespace.entries = entries


def _buttons(value: str) -> list[str]:
    chosen: list[str] = []
    for raw in value.split(","):
        name = raw.strip().casefold()
        if not name:
            continue
        match = next((b for b in BUTTONS if b.casefold() == name), None)
        if match is None:
            raise argparse.ArgumentTypeError(
                f"{raw.strip()!r} is not a button. Choose from: " + ", ".join(BUTTONS))
        if match not in chosen:
            chosen.append(match)
    return chosen


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="discwright",
        description="Turn GOG offline installers into burnable game discs.")
    p.add_argument("--version", action="version", version=f"DiscWright {__version__}")
    sub = p.add_subparsers(dest="command")

    sub.add_parser("window", help="open the DiscWright window",
                   description="Open the DiscWright window: the same steps as the Windows app.")

    b = sub.add_parser("build", help="build a disc and write its ISO",
                       description="Build a disc from one or more GOG downloads.")
    b.add_argument("--project", metavar="FILE",
                   help="rebuild the disc a discproject.json describes, saved by a build here "
                        "or by DiscWright on Windows. Everything comes from the file; only "
                        "--out, --stage-only and --quiet go with it.")
    b.add_argument("--game", metavar="DIR", action=_Entry,
                   help="a GOG download folder. Repeat for a disc holding several games.")
    b.add_argument("--files", metavar="DIR", action=_Entry,
                   help="a folder of game files that is not a GOG download: an unpacked "
                        "archive, an itch.io download, an installed game, anything portable. "
                        "It goes on the disc as it stands, subfolders and all.")
    b.add_argument("--installer", metavar="EXE", action=_Entry,
                   help="the executable that installs the --files folder named before it. "
                        "Without one the menu opens that folder instead of offering Install.")
    b.add_argument("--add-on", metavar="EXE", action=_Entry, dest="add_on",
                   help="an add-on installer (DLC, an expansion, a patch, a mod) for the game "
                        "named before it.")
    b.add_argument("--label", metavar="TEXT",
                   help="what Explorer shows for the disc. Defaults to the first game's name.")
    b.add_argument("--icon", metavar="FILE", help="the disc's icon: an .ico, or any picture.")
    b.add_argument("--out", metavar="DIR", help="where the ISO and the disc folder go.")

    m = b.add_argument_group("the autorun menu")
    m.add_argument("--no-menu", action="store_true",
                   help="no menu: the disc just opens as a folder.")
    m.add_argument("--background", metavar="FILE", help="the menu's artwork. A menu needs one.")
    m.add_argument("--background-as-is", action="store_true",
                   help="use the picture as it is, already 760x480 and composed.")
    m.add_argument("--panel-side", choices=["right", "left"], default="right",
                   help="which side the buttons sit on. Pick the side away from the artwork's "
                        "focal point, or the buttons cover it (default: right).")
    m.add_argument("--divider", action="store_true",
                   help="draw a line between the artwork and the buttons.")
    m.add_argument("--title", nargs="?", const="", metavar="TEXT",
                   help="draw a title on the artwork. On its own, the disc's label. Cover art "
                        "usually carries the game's own logo already.")
    m.add_argument("--buttons", type=_buttons, default=list(BUTTONS), metavar="LIST",
                   help="which buttons the menu has, comma separated "
                        "(default: play,install,manual,extras,exit).")
    m.add_argument("--music", metavar="FILE", help="played while the menu is open.")
    m.add_argument("--no-window-border", action="store_true",
                   help="no outline round the menu window.")
    m.add_argument("--bordered-buttons", action="store_true", help="draw a box round each button.")

    c = b.add_argument_group("what else goes on the disc")
    c.add_argument("--manual", metavar="FILE", help="a manual, for the menu's Manual button.")
    c.add_argument("--extras", metavar="DIR",
                   help="a folder of extras, for the menu's Extras button.")
    c.add_argument("--extra", metavar="PATH", action="append", default=[],
                   help="a file or folder copied to the disc root under its own name. Repeatable.")
    c.add_argument("--no-linux-name", action="store_true",
                   help="do not write the files that give the disc its name and icon on a Linux "
                        "desktop.")

    b.add_argument("--stage-only", action="store_true",
                   help="lay the disc out as a folder and stop, without writing the ISO.")
    b.add_argument("--quiet", action="store_true", help="only errors.")
    return p


def _inside(path: Path, folder: Path) -> bool:
    try:
        path.resolve().relative_to(folder.resolve())
    except (ValueError, OSError):
        return False
    return True


def _no_gog_advice(folder: Path) -> str:
    """What to type instead, for a folder with no GOG installer in it.

    The Windows app asks this as a question in a dialog, listing the folder's
    executables largest first. A command cannot ask, and taking any folder
    handed to --game would put a mis-picked home directory on a disc, so it
    says what the dialog would have offered and lets the next command answer.
    """
    # Quoted where a shell would need it: these lines are meant to be pasted
    # back, and a game folder with a space in its name is the normal case.
    quoted = shlex.quote(str(folder))
    lines = []
    downloads = gog_subfolders(folder)
    if downloads:
        lines.append(f"  {len(downloads)} GOG download(s) sit in subfolders of it, starting with "
                     f'"{downloads[0].name}" - name one of those instead.')
    files = [p for p in folder.rglob("*") if p.is_file()] if folder.is_dir() else []
    if not files:
        return "\n".join(lines)
    lines.append(f"  To put it on a disc as it stands ({len(files)} file(s), "
                 f"{format_size(sum(p.stat().st_size for p in files))}):")
    lines.append(f"    --files {quoted}")
    exes = folder_executables(folder, files=files)
    if exes:
        lines.append("  To have the menu install it, name the executable that does, "
                     "largest first here:")
        lines += [f"    --files {quoted} --installer {shlex.quote(str(e))}" for e in exes[:3]]
    return "\n".join(lines)


def _read_entry(kind: str, value: str, installer: str | None):
    if kind == "game":
        return game_info(value)
    if kind == "files":
        return folder_info(value, installer)
    return add_on_info(value)


def _entries(args, out) -> list[GameInfo] | None:
    """The games and add-ons, read from what was named, each reported the way the
    window reports it. None when one of them cannot be used."""
    entries: list[GameInfo] = []
    last_game = -1
    ok = True
    typed = list(getattr(args, "entries", None) or [])
    for i, (kind, value) in enumerate(typed):
        if kind == "installer":
            # Read with the folder it belongs to, not on its own.
            if i == 0 or typed[i - 1][0] != "files":
                print(f"{value}: --installer names the executable inside the --files folder "
                      "before it, so put a --files first.", file=sys.stderr)
                ok = False
            continue
        installer = typed[i + 1][1] if kind == "files" and i + 1 < len(typed) \
            and typed[i + 1][0] == "installer" else None
        if installer is not None and not _inside(Path(installer), Path(value)):
            # The dialog on Windows can only offer executables from the folder
            # itself. One from anywhere else is not on the disc, so the menu's
            # Install button would point at nothing.
            print(f"{installer}: an installer has to be inside the folder it installs, "
                  f"or it does not go on the disc.", file=sys.stderr)
            ok = False
            continue
        info = _read_entry(kind, value, installer)
        if not info.ok:
            print(f"{value}: {info.msg}", file=sys.stderr)
            if kind == "game" and info.msg.startswith("No GOG"):
                advice = _no_gog_advice(Path(value))
                if advice:
                    print(advice, file=sys.stderr)
            ok = False
            continue
        if kind in ("game", "files"):
            last_game = len(entries)
        elif last_game < 0:
            print(f"{value}: an add-on belongs to a game, so name a --game before it.",
                  file=sys.stderr)
            ok = False
            continue
        else:
            info.parent_index = last_game
        what = "add-on" if info.kind == "AddOn" else "game"
        files = f"{len(info.files)} file" + ("s" if len(info.files) != 1 else "")
        out(f"{what}: {info.game_name}  ({files}, {format_size(info.total_bytes)})")
        if info.warning:
            out(f"  {info.warning}")
        entries.append(info)
    return entries if ok else None


def _settings(args, entries: list[GameInfo], label: str) -> DiscSettings:
    icon = Path(args.icon)
    return DiscSettings(
        games=entries,
        label=label,
        out_dir=Path(args.out),
        icon_path=icon,
        icon_is_ico=icon.suffix.casefold() == ".ico",
        menu=not args.no_menu,
        bg_path=Path(args.background) if args.background else None,
        bg_as_is=args.background_as_is,
        panel_side="Left" if args.panel_side == "left" else "Right",
        divider=args.divider,
        show_title=args.title is not None,
        title_text=args.title or "",
        window_border=not args.no_window_border,
        button_style="Bordered" if args.bordered_buttons else "Minimal",
        music_file=Path(args.music) if args.music else None,
        buttons=args.buttons,
        manual_path=Path(args.manual) if args.manual else None,
        extras_path=Path(args.extras) if args.extras else None,
        extra_items=[Path(p) for p in args.extra],
        linux_info=not args.no_linux_name,
    )


def _from_project(args, out) -> DiscSettings | None:
    project = read_project(args.project)
    if project is None:
        print(f"{args.project}: not a disc project file.", file=sys.stderr)
        return None
    s, problems = settings_from_project(project)
    for p in problems:
        print(p, file=sys.stderr)
    if problems:
        # A project written on Windows names Windows paths, and a game that has
        # moved is the same failure. Either way there is nothing to build from.
        print("The project names files this machine cannot find.", file=sys.stderr)
        return None
    if args.out:
        s.out_dir = Path(args.out)
    out(f"project: {args.project}  (schema {project.schema}, written by DiscWright "
        f"{project.app_version or 'unknown'})")
    for g in s.games:
        what = "add-on" if g.kind == "AddOn" else "game"
        out(f"{what}: {g.game_name}  ({len(g.files)} file"
            f"{'s' if len(g.files) != 1 else ''}, {format_size(g.total_bytes)})")
    return s


def _run_build(args) -> int:
    out = (lambda _m: None) if args.quiet else print
    if args.project:
        for name, value in (("--game or --add-on", getattr(args, "entries", None)),
                            ("--icon", args.icon), ("--label", args.label)):
            if value:
                print(f"{name} cannot be given with --project: the file already says. "
                      "Only --out, --stage-only and --quiet go with it.", file=sys.stderr)
                return 1
        s = _from_project(args, out)
        if s is None:
            return 1
        return _build_it(args, s, out)

    entries = _entries(args, out)
    if entries is None:
        return 1
    if not entries:
        print("Name at least one --game: the folder a GOG download came in.", file=sys.stderr)
        return 1
    if not args.icon:
        print("A disc needs an --icon: the picture Explorer shows for the drive.", file=sys.stderr)
        return 1
    if not args.out:
        print("Say where the disc goes with --out.", file=sys.stderr)
        return 1

    label = args.label if (args.label or "").strip() else entries[0].game_name
    if not (args.label or "").strip():
        out(f"label: {label}  (the first game's name; --label sets your own)")
    return _build_it(args, _settings(args, entries, label), out)


def _build_it(args, s: DiscSettings, out) -> int:
    # One line, rewritten in place. xorriso reports often and a build of a full
    # game takes minutes, so the number has to move without filling the terminal.
    last = [-5.0]

    def progress(percent: float) -> None:
        if percent - last[0] < 5 and percent != 100.0:
            return
        last[0] = percent
        print(f"\r  {percent:5.1f}%", end="\n" if percent == 100.0 else "", flush=True)

    try:
        if args.stage_only:
            stage_dir, _ = stage(s, out)
            out(f"DONE.  The disc folder is {stage_dir}")
        else:
            build(s, out, None if args.quiet else progress)
    except (StagingError, IsoError) as e:
        print(f"\n{e}", file=sys.stderr)
        return 1
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if getattr(args, "command", None) == "window":
        return _run_window()
    if getattr(args, "command", None) != "build":
        parser.print_help()
        return 0
    return _run_build(args)


def _run_window() -> int:
    # GTK is only needed for the window, so it is imported only here: the command
    # line works on a machine without it, a server or a container.
    try:
        from .window import run
    except (ImportError, ValueError) as e:
        print("The window needs GTK 4 and PyGObject, which are not installed:\n"
              f"  {e}\n\n"
              "On Debian or Ubuntu:  sudo apt install gir1.2-gtk-4.0 python3-gi\n"
              "On Fedora:            sudo dnf install gtk4 python3-gobject\n\n"
              "Everything the window does, discwright build does from the command line.",
              file=sys.stderr)
        return 1
    return run()


if __name__ == "__main__":
    raise SystemExit(main())
