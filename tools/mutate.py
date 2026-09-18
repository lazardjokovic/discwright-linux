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
