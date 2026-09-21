"""Break the code on purpose and check the suite notices.

A test that has never failed has not shown it can. Each mutation below is a
plausible real mistake; the run passes only if every one of them makes at least
one test fail. Files are restored afterwards whatever happens.

    python tools/mutate.py
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src" / "discwright"
TIMEOUT = 300  # seconds; the whole suite takes a few

MUTATIONS = [
    ("case-sensitive installer match", "games.py",
     'p.name.casefold().startswith("setup_")', 'p.name.startswith("setup_")'),
    ("padding left on the name", "games.py",
     "name = name.strip()", "name = name"),
    ("parts from any installer in the folder", "games.py",
     "p.name.casefold().startswith(stem)", "p.name.casefold().startswith('setup_')"),
    ("add-on named from its version resource", "games.py",
     'if info.kind == "AddOn":', 'if False:'),
    ("patch named by the version it moves FROM", "games.py",
     'r"^patch_.+_to_(.+)$"', 'r"^patch_(.+)_to_.+$"'),
    ("add-on extension checked case-sensitively", "games.py",
     'path.suffix.casefold() != ".exe"', 'path.suffix != ".exe"'),
    ("trailing dot left after the folder-name cut", "layout.py",
     'n = n[:48].strip(" .")', "n = n[:48].strip()"),
    ("reserved names compared case-sensitively", "layout.py",
     "return folded in {r.casefold() for r in reserved}", "return name in reserved"),
    ("add-on promoted when its parent is another add-on skipped", "layout.py",
     'or entries[p].kind == "AddOn"', ""),
    ("one game with patches wrapped in a Games folder", "layout.py",
     "if len(games) <= 1:", "if len(games) < 1:"),
    ("folders numbered by entry instead of by game", "layout.py",
     "n = games.index(index) + 1", "n = index + 1"),
    ("disc-wide manual copied with no Manual button", "stage.py",
     'if "Manual" in s.buttons and s.manual_path:', "if s.manual_path:"),
    ("extra content allowed to overwrite a reserved name", "stage.py",
     "if is_reserved_name(item.name, ico_name):", "if False:"),
    ("an entry's own manual and extras left behind", "stage.py",
     "if g.manual_path or g.extras_path:", "if False:"),
    ("installers copied instead of hard-linked", "stage.py",
     "os.link(src, dest)", "raise OSError('no links')"),
    ("an unusable icon found out only after the copy", "stage.py",
     "if not icon.ok:", "if False:"),
    ("paths inside the old disc folder not followed", "stage.py",
     "setattr(s, attr, _moved(getattr(s, attr), stage_dir, aside))", "pass"),
    ("icon bitmap written RGBA instead of BGRA", "icons.py",
     'im.tobytes("raw", "BGRA")', 'im.tobytes("raw", "RGBA")'),
    ("icon bitmap written top-down", "icons.py",
     "for y in range(h - 1, -1, -1)", "for y in range(h)"),
    ("icon bitmap header without the doubled height", "icons.py",
     "40, w, h * 2, 1, 32", "40, w, h, 1, 32"),
    ("256px frame stored as a bitmap", "icons.py",
     "if size >= 256:", "if size > 256:"),
    ("crop taken from the left edge", "icons.py",
     "left = round((im.width - side) / 2)", "left = 0"),
    ("Linux icon never written", "stage.py",
     "convert_to_png(s.icon_path, stage_dir / png_name)", "pass"),
    ("background panel on the wrong side", "background.py",
     'left = panel_side.casefold() == "left"', 'left = panel_side.casefold() == "right"'),
    ("background never darkened", "background.py",
     "DARKEN = (0, 0, 0, 70)", "DARKEN = (0, 0, 0, 0)"),
    ("panel gradient running the wrong way", "background.py",
     "c1, c2 = (PANEL_NEAR, PANEL_FAR) if left else (PANEL_FAR, PANEL_NEAR)",
     "c1, c2 = (PANEL_FAR, PANEL_NEAR) if left else (PANEL_NEAR, PANEL_FAR)"),
    ("divider never drawn", "background.py",
     "    if divider:\n", "    if False:\n"),
    ("background cropped from its corner", "background.py",
     "art.crop((-ox, -oy, -ox + WIDTH, -oy + HEIGHT))", "art.crop((0, 0, WIDTH, HEIGHT))"),
    ("top row and left column left undarkened, as on Windows", "background.py",
     'canvas.alpha_composite(Image.new("RGBA", (WIDTH, HEIGHT), DARKEN))',
     'canvas.alpha_composite(Image.new("RGBA", (WIDTH - 1, HEIGHT - 1), DARKEN), (1, 1))'),
    ("title drawn when it was not asked for", "background.py",
     "if show_title and title and title.strip():", "if title and title.strip():"),
    ("title never shrunk to fit", "background.py",
     "if fits or size <= TITLE_FLOOR_PT:", "if True:"),
    ("title shrinking stopped at 12pt, as on Windows", "background.py",
     "TITLE_FLOOR_PT = 6.0", "TITLE_FLOOR_PT = 12.0"),
    ("title placed by the font's origin, not its ink", "background.py",
     "x = tx + round(em / 6) - box[0]", "x = tx"),
    ("background title ignoring the title box", "stage.py",
     'bg_title = s.title_text if (s.title_text or "").strip() else s.label', "bg_title = s.label"),
    ("background title blank when the title box is", "stage.py",
     'bg_title = s.title_text if (s.title_text or "").strip() else s.label', "bg_title = s.title_text"),
    ("background as-is composed anyway", "stage.py",
     "if s.bg_as_is:", "if False:"),
    ("a menu without a background found out only after the copy", "stage.py",
     "if s.bg_path is None:", "if False:"),
    ("an unusable background found out only after the copy", "stage.py",
     "if not bg.ok:", "if False:"),
    ("backslash left unescaped in the menu's script", "menu.py",
     '.replace("\\\\", "\\\\\\\\").replace(\'"\', \'\\\\"\')', '.replace(\'"\', \'\\\\"\')'),
    ("< left as it is in the menu's script", "menu.py",
     '.replace("<", "\\\\x3c")', ""),
    ("control characters kept in the menu's script", "menu.py",
     "    s = remove_control_chars(s)\n    if not s:\n        return \"\"\n    s = s.replace(\"\\\\\"",
     "    if not s:\n        return \"\"\n    s = s.replace(\"\\\\\""),
    ("menu escapes counted in code points, not UTF-16 units", "menu.py",
     'data = ch.encode("utf-16-le")', 'data = ch.encode("utf-32-le")[:2]'),
    ("menu entities counted in UTF-16 units", "menu.py",
     'f"&#{ord(ch)};"', 'f"&#{ord(ch) & 0xFFFF};"'),
    ("match name not falling back to the game's name", "menu.py",
     'match = g.get("match_name") or g["name"]', 'match = g.get("match_name") or ""'),
    ("menu placeholders filled in one after another", "menu.py",
     'html = re.sub(r"%%([A-Z]+)%%", lambda m: values.get(m.group(1), m.group(0)), template)',
     "html = template\n    for k, v in values.items():\n        html = html.replace('%%' + k + '%%', v)"),
    ("menu written with LF line ends", "menu.py",
     'html.replace("\\n", "\\r\\n")', "html"),
    ("menu buttons always on the right", "menu.py",
     'panel_left = 20 if panel_side.casefold() == "left" else 490', "panel_left = 490"),
    ("menu window name keeping only what .NET keeps without the two extras", "menu.py",
     "or c in _NET_ALSO_KEEPS)", "or False)"),
    ("menu given no music", "stage.py",
     "music_file=music_name,", 'music_file="",'),
    ("menu given the disc-wide manual even without a Manual button", "stage.py",
     '        manual_name = ""\n        if "Manual" in s.buttons and s.manual_path:',
     '        manual_name = Path(s.manual_path).name if s.manual_path else ""\n'
     '        if "Manual" in s.buttons and s.manual_path:'),
    ("ISO written without telling xorriso the names are UTF-8", "iso.py",
     '"-input-charset", "UTF-8",\n', ""),
    ("ISO written without Joliet's long names", "iso.py",
     '"-J", "-joliet-long", "-r",', '"-J", "-r",'),
    ("Joliet name limit off by one", "iso.py",
     "JOLIET_MAX_NAME = 103", "JOLIET_MAX_NAME = 104"),
    ("names differing only in capitals let through", "iso.py",
     "            if key in seen:", "            if False:"),
    ("a name with a trailing dot let through", "iso.py",
     'if name.endswith((".", " ")):', 'if name.endswith(" "):'),
    ("an emoji in a name let through", "iso.py",
     "if any(ord(c) > 0xFFFF for c in name):", "if False:"),
    ("device names let through", "iso.py",
     "in WINDOWS_DEVICES:", "in set():"),
    ("ISO written in place, so a failure destroys the previous one", "iso.py",
     'tmp = out.with_name(out.name + ".partial")', "tmp = out"),
    ("a failed xorriso taken for a success", "iso.py",
     "if proc.wait() != 0:", "if proc.wait() == 12345:"),
    ("ISO named without the label's characters folded", "iso.py",
     'name = re.sub(r"[^A-Za-z0-9_\\- ]", "_", label or "").strip()', 'name = (label or "").strip()'),
    ("old disc folder removed before the ISO exists", "build.py",
     "    stage_dir, aside = stage(s, log)\n    iso = build_iso(",
     "    stage_dir, aside = stage(s, log)\n    shutil.rmtree(aside, ignore_errors=True) if aside else None\n    iso = build_iso("),
    ("add-on filed under the wrong game", "cli.py",
     "info.parent_index = last_game", "info.parent_index = -1"),
    ("label not falling back to the first game's name", "cli.py",
     'label = args.label if (args.label or "").strip() else entries[0].game_name',
     'label = args.label or ""'),
    ("an .ico rebuilt instead of used as it is", "cli.py",
     'icon_is_ico=icon.suffix.casefold() == ".ico",', "icon_is_ico=False,"),
    ("menu buttons put on the wrong side", "cli.py",
     'panel_side="Left" if args.panel_side == "left" else "Right",', 'panel_side="Right",'),
    ("a titled disc left untitled", "cli.py",
     "show_title=args.title is not None,", "show_title=bool(args.title),"),
    ("--no-menu ignored", "cli.py",
     "menu=not args.no_menu,", "menu=True,"),
    ("--no-linux-name ignored", "cli.py",
     "linux_info=not args.no_linux_name,", "linux_info=True,"),
    ("--stage-only writing the ISO anyway", "cli.py",
     "if args.stage_only:", "if False:"),
    ("--quiet still printing", "cli.py",
     'out = (lambda _m: None) if args.quiet else print', "out = print"),
    ("a build that cannot run reported as a success", "cli.py",
     "        print(f\"\\n{e}\", file=sys.stderr)\n        return 1", "        return 0"),
    ("project file written without the mark PowerShell needs", "project.py",
     'path.write_bytes(b"\\xef\\xbb\\xbf" + text.encode("utf-8"))',
     'path.write_bytes(text.encode("utf-8"))'),
    ("project file written with LF line ends", "project.py",
     'json.dumps(doc, indent=4, ensure_ascii=False).replace("\\n", "\\r\\n") + "\\r\\n"',
     "json.dumps(doc, indent=4, ensure_ascii=False)"),
    ("project file no longer opening in the oldest app", "project.py",
     '"SourceFolder": _text(games[0].folder) if games else None,', '"SourceFolder": None,'),
    ("add-on's parent not written", "project.py",
     '"Parent": int(g.parent_index),', '"Parent": -1,'),
    ("add-on's parent not read", "project.py",
     'parent_index=int(g.get("Parent", -1)),', "parent_index=-1,"),
    ("match name dropped on the way back", "project.py",
     "        if e.match_name:\n            info.match_name = e.match_name\n", ""),
    ("edited name dropped on the way back", "project.py",
     "        if e.name:\n            info.game_name = e.name\n", ""),
    ("an older file's buttons read as today's default", "project.py",
     'button_style=str(j.get("ButtonStyle") or "Bordered"),',
     'button_style=str(j.get("ButtonStyle") or "Minimal"),'),
    ("an older file's panel read as the other side", "project.py",
     'panel_side=str(j.get("PanelSide") or "Right"),',
     'panel_side=str(j.get("PanelSide") or "Left"),'),
    ("add-on re-read from its folder, which finds the game again", "project.py",
     'if e.kind == "AddOn" and setup is not None:', "if False:"),
    ("a project that cannot be parsed treated as an empty one", "project.py",
     "        if not isinstance(j, dict):\n            return None\n", ""),
    ("no project saved by a build", "build.py",
     "    save_project(s, s.out_dir)", "    pass"),
    ("project saved naming the folder the build is about to delete", "build.py",
     "        _back_from_aside(s, aside, stage_dir)", "        pass"),
    ("parents not shifted when an entry is removed", "layout.py",
     "                e.parent_index = new_index[e.parent_index]", "                pass"),
    ("an orphaned add-on deleted instead of promoted", "layout.py",
     '                e.kind, e.parent_index = "Game", -1', "                continue"),
    ("the label not named after the first game", "form.py",
     "        if not self.label.strip():\n            self.label = info.game_name or \"\"",
     "        if False:\n            self.label = info.game_name or \"\""),
    ("a label the user typed cleared with the last game", "form.py",
     "if not self.games and self.label_is_seeded():", "if not self.games:"),
    ("an add-on offered with no game to belong to", "form.py",
     "        if not has_game:\n            c[\"add_on\"]", "        if False:\n            c[\"add_on\"]"),
    ("an add-on filed under a guessed game", "form.py",
     "return parents[0] if len(parents) == 1 else None", "return parents[0] if parents else None"),
    ("menu options left usable with the menu off", "form.py",
     "            c[name] = Control(self.menu, menu_off)", "            c[name] = Control(True, menu_off)"),
    ("painted options left usable with the picture used as it is", "form.py",
     "composes = self.menu and not self.bg_as_is", "composes = self.menu"),
    ("title text usable with the title off", "form.py",
     "c[\"title_text\"] = Control(composes and self.show_title,", "c[\"title_text\"] = Control(composes,"),
    ("controls left usable during a build", "form.py",
     "        if self.building:\n", "        if False:\n"),
    ("BUILD usable with something missing", "form.py",
     'c["build"] = Control(not missing,', 'c["build"] = Control(True,'),
    ("a menu with no background let through to the build", "form.py",
     "            if self.background is None:\n                out.append(",
     "            if False:\n                out.append("),
    ("a label Windows cannot show built without asking", "form.py",
     "        if shown != self.label:\n", "        if False:\n"),
    ("rename touching the name the registry is searched for", "form.py",
     "        self.games[index].game_name = name", "        self.games[index].game_name = self.games[index].match_name = name"),
    ("buttons handed to the build in the order they were ticked", "form.py",
     "buttons=[b for b in BUTTONS if b in self.buttons],", "buttons=list(self.buttons),"),
    ("wrong resource type read", "pe.py",
     "_RT_VERSION = 16", "_RT_VERSION = 14"),
    ("LF instead of CRLF in autorun.inf", "autorun.py",
     'encode_ansi("\\r\\n".join(lines) + "\\r\\n")', 'encode_ansi("\\n".join(lines) + "\\n")'),
    ("backslash not doubled for GKeyFile", "xdg.py",
     'value.replace("\\\\", "\\\\\\\\")', "value"),
]


def main() -> int:
    survived = []
    for name, file, before, after in MUTATIONS:
        path = SRC / file
        original = path.read_text(encoding="utf-8")
        if before not in original:
            print(f"  NOT APPLICABLE  {name}  (text not found in {file})", flush=True)
            survived.append(name)
            continue
        # A mutation that hangs the suite is reported by name, with the test it
        # hung in, rather than holding CI until the job's six-hour limit.
        try:
            path.write_text(original.replace(before, after, 1), encoding="utf-8")
            run = subprocess.run([sys.executable, "-m", "pytest", "-v", "-x", "--no-header", "-p", "no:cacheprovider"],
                                 cwd=ROOT, capture_output=True, text=True, timeout=TIMEOUT)
            verdict = "caught " if run.returncode != 0 else "MISSED "
        except subprocess.TimeoutExpired as e:
            verdict = "HUNG   "
            out = e.stdout.decode() if isinstance(e.stdout, bytes) else (e.stdout or "")
            print("\n".join("      " + line for line in out.splitlines()[-3:]), flush=True)
        finally:
            path.write_text(original, encoding="utf-8")
        print(f"  {verdict}  {name}", flush=True)
        if verdict != "caught ":
            survived.append(name)
    print()
    print("every mutation caught" if not survived else f"{len(survived)} not caught")
    return 1 if survived else 0


if __name__ == "__main__":
    raise SystemExit(main())
