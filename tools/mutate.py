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
            print(f"  NOT APPLICABLE  {name}  (text not found in {file})")
            survived.append(name)
            continue
        try:
            path.write_text(original.replace(before, after, 1), encoding="utf-8")
            run = subprocess.run([sys.executable, "-m", "pytest", "-q", "-x", "--no-header", "-p", "no:cacheprovider"],
                                 cwd=ROOT, capture_output=True, text=True)
            caught = run.returncode != 0
        finally:
            path.write_text(original, encoding="utf-8")
        print(f"  {'caught ' if caught else 'MISSED '}  {name}")
        if not caught:
            survived.append(name)
    print()
    print("every mutation caught" if not survived else f"{len(survived)} not caught")
    return 1 if survived else 0


if __name__ == "__main__":
    raise SystemExit(main())
