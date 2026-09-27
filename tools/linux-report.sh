#!/bin/bash
# Everything a real Linux machine can be asked automatically, in one run, with
# the answers written down.
#
#   tools/linux-report.sh                       # the machine, the suite, a disc
#   tools/linux-report.sh --mutate              # and the mutation check (~20 min)
#   DISCWRIGHT_GOG_DIR=~/dwdemo tools/linux-report.sh    # with the real games
#
# It writes a report to ~/discwright-report-<host>-<date>.txt and prints where.
# The checks a person has to make with their eyes are in
# docs/testing-on-linux.md; this is the part that should not need a person.
#
# It never fails the run on a missing optional thing: the point is a written
# answer either way, not a green tick.
set -u

REPO=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
VENV=${DISCWRIGHT_VENV:-$REPO/.venv}
PY=$VENV/bin/python
REPORT=${DISCWRIGHT_REPORT:-$HOME/discwright-report-$(hostname -s 2>/dev/null || echo linux)-$(date +%Y%m%d-%H%M).txt}
MUTATE=no
[ "${1-}" = "--mutate" ] && MUTATE=yes

say() { printf '%s\n' "$*" | tee -a "$REPORT"; }
run() { printf '\n$ %s\n' "$*" >> "$REPORT"; "$@" >> "$REPORT" 2>&1; }

: > "$REPORT"
say "DiscWright for Linux: machine report"
say "written $(date -Is)"
say ""

# ---------------------------------------------------------------- the machine
say "== the machine"
say "  distro     : $( (. /etc/os-release && echo "$PRETTY_NAME") 2>/dev/null || echo unknown)"
say "  kernel     : $(uname -sr)"
say "  desktop    : ${XDG_CURRENT_DESKTOP:-unknown} (${XDG_SESSION_TYPE:-unknown} session)"
say "  portal     : $(pgrep -a xdg-desktop-portal 2>/dev/null | sed 's/.*\///' | tr '\n' ' ' || echo none)"
say "  python     : $("$PY" -V 2>&1 || echo "no venv at $VENV")"
say "  pillow     : $("$PY" -c 'import PIL; print(PIL.__version__)' 2>/dev/null || echo missing)"
say "  pygobject  : $("$PY" -c 'import gi; print(gi.__version__)' 2>/dev/null || echo missing)"
say "  gtk        : $("$PY" -c 'import gi; gi.require_version("Gtk","4.0"); from gi.repository import Gtk; print(f"{Gtk.get_major_version()}.{Gtk.get_minor_version()}.{Gtk.get_micro_version()}")' 2>/dev/null || echo missing)"
say "  xorriso    : $(xorriso --version 2>&1 | grep -m1 -o 'xorriso [0-9.]*' || echo missing)"
say "  node       : $(node --version 2>/dev/null || echo 'missing (5 menu tests skip)')"
say "  validator  : $(command -v desktop-file-validate >/dev/null && echo present || echo 'missing (1 test skips)')"
# The menu's title is drawn in whichever of these is found; without any, Pillow
# falls back to a tiny built-in font and the title looks wrong.
say "  bold fonts : $(fc-list 2>/dev/null | grep -ciE 'dejavu.*bold|liberation.*bold|noto.*bold|freesans.*bold' || echo 0) matching DejaVu/Liberation/Noto/FreeSans bold"
say ""

if [ ! -x "$PY" ]; then
    say "No virtual environment at $VENV."
    say "See the README's \"Installing it\", or docs/testing-on-linux.md for the developer install."
    exit 1
fi

# ------------------------------------------------------------------ the suite
say "== the suite"
if [ -n "${DISCWRIGHT_GOG_DIR-}" ]; then
    say "  with the real games from $DISCWRIGHT_GOG_DIR"
    [ -n "${DISCWRIGHT_STAGE_DIR-}" ] && say "  staging in $DISCWRIGHT_STAGE_DIR"
else
    say "  without the real games (set DISCWRIGHT_GOG_DIR to include them)"
fi
run "$PY" -m pytest -q -rs
tail -1 "$REPORT" | sed 's/^/  /'
SUITE=$(grep -Eo '[0-9]+ (passed|failed)[^,]*' "$REPORT" | tail -2 | tr '\n' ' ')
say "  result     : ${SUITE:-see the report}"
say ""

# ------------------------------------------------- a disc, end to end, for real
say "== a disc built end to end"
# Somewhere it will still be after the run: the ISO is the thing to mount for
# the check no script can make, and a temporary directory would take it away.
WORK=${DISCWRIGHT_CHECK_DIR:-$HOME/discwright-check}
rm -rf "$WORK"
mkdir -p "$WORK"
GAME="$WORK/src/Portable Game"      # a space in it on purpose: the usual case
mkdir -p "$GAME/data/textures"
head -c 3000000 /dev/urandom > "$GAME/PortableGame.exe"
head -c 40000 /dev/urandom > "$GAME/CrashHandler.exe"
echo "read me" > "$GAME/readme.txt"
echo "x=1" > "$GAME/data/config.ini"
echo "dds" > "$GAME/data/textures/wall.dds"
cp "$REPO/tests/fixtures/icons/source.png" "$WORK/icon.png" 2>/dev/null
cp "$REPO/tests/fixtures/background/wide.png" "$WORK/bg.png" 2>/dev/null

run "$VENV/bin/discwright" build --files "$GAME" --icon "$WORK/icon.png" \
    --background "$WORK/bg.png" --label "LINUX CHECK" --out "$WORK/out"
ISO=$(find "$WORK/out" -maxdepth 1 -name '*.iso' | head -1)
DISC=$WORK/out/disc
if [ -n "$ISO" ]; then
    say "  iso        : $(basename "$ISO"), $(du -h "$ISO" | cut -f1)"
else
    say "  iso        : NOT WRITTEN - see the report"
fi
say "  files kept : $( [ -f "$DISC/data/textures/wall.dds" ] && echo 'subfolders intact' || echo 'MISSING data/textures/wall.dds')"
say "  menu       : $(grep -c 'doOpenFolder' "$DISC/AUTORUN/menu.hta" 2>/dev/null || echo 0) Open Folder references"
say "  linux name : $(sed -n 's/^Name=//p' "$DISC/.xdg-volume-info" 2>/dev/null || echo 'no .xdg-volume-info')"
say "  linux icon : $(sed -n 's/^IconFile=//p' "$DISC/.xdg-volume-info" 2>/dev/null || echo none)"
say ""
say "  The ISO is at $ISO"
say "  (left there on purpose; the whole folder is $WORK, delete it when done)"
say "  Mount it and look at what the desktop shows: that is check 4 in"
say "  docs/testing-on-linux.md, and it is the one nothing here can answer."
say ""

# -------------------------------------------------------- the mutation check
if [ "$MUTATE" = yes ]; then
    say "== mutation check (every break has to be caught)"
    run "$PY" "$REPO/tools/mutate.py"
    say "  result     : $(grep -E 'every mutation caught|not caught' "$REPORT" | tail -1)"
    say ""
else
    say "== mutation check: not run (pass --mutate, it takes about twenty minutes)"
    say ""
fi

say "== what is left for a person"
say "  docs/testing-on-linux.md, the by-hand section: the disc on this desktop,"
say "  the menu entry in the application menu, the window's own dialogs, and the"
say "  folder question. Those need eyes, and this report is what they attach to."
say ""
say "Report: $REPORT"
echo
echo "Written to $REPORT"
