#!/bin/sh
# Put DiscWright in the application menu, for the person who installed it.
#
#   ./packaging/desktop/install.sh                  # find discwright on PATH
#   ./packaging/desktop/install.sh ~/.venvs/discwright/bin/discwright
#   ./packaging/desktop/install.sh --uninstall
#
# Everything goes under $HOME: no root, nothing touched outside this account,
# and the same command removes it again. The window is how DiscWright is used,
# and a command somebody has to remember the path of is not a window they will
# open, so this exists.
#
# POSIX sh on purpose: this runs on whatever the distribution ships.
set -eu

APP_ID=com.discwright.DiscWright
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DATA=${XDG_DATA_HOME:-$HOME/.local/share}
APPS=$DATA/applications
ICONS=$DATA/icons/hicolor

refresh() {
    # Both are optional: the entry works without them, it may just take a
    # desktop restart to notice. Their failure is not this script's failure.
    [ -d "$APPS" ] && command -v update-desktop-database >/dev/null 2>&1 \
        && update-desktop-database "$APPS" >/dev/null 2>&1 || true
    [ -d "$ICONS" ] && command -v gtk-update-icon-cache >/dev/null 2>&1 \
        && gtk-update-icon-cache -q -t -f "$ICONS" >/dev/null 2>&1 || true
}

if [ "${1-}" = "--uninstall" ]; then
    rm -f "$APPS/$APP_ID.desktop"
    for size in 16 24 32 48 64 128 256; do
        rm -f "$ICONS/${size}x${size}/apps/$APP_ID.png"
    done
    refresh
    echo "Removed DiscWright from the application menu."
    exit 0
fi

# Which discwright this entry should start. Named first, found on PATH second,
# and if neither, say so rather than writing an entry that does nothing when
# it is clicked - which is the worst way to find out.
EXEC=${1-}
if [ -z "$EXEC" ]; then
    EXEC=$(command -v discwright || true)
fi
if [ -z "$EXEC" ] || [ ! -x "$EXEC" ]; then
    echo "Cannot find the discwright command." >&2
    echo >&2
    echo "Give it the path, if it is in a virtual environment:" >&2
    echo "  $0 ~/.venvs/discwright/bin/discwright" >&2
    echo >&2
    echo "The README's \"Installing it\" says how to install it in the first place." >&2
    exit 1
fi
case $EXEC in
    /*) ;;
    *) EXEC=$(CDPATH= cd -- "$(dirname -- "$EXEC")" && pwd)/$(basename -- "$EXEC") ;;
esac

mkdir -p "$APPS"
# Quoted in the entry, because a path with a space in it is otherwise read as
# two arguments and nothing opens.
sed -e "s|^Exec=.*|Exec=\"$EXEC\" window|" \
    -e "s|^TryExec=.*|TryExec=$EXEC|" \
    "$HERE/$APP_ID.desktop" > "$APPS/$APP_ID.desktop"
chmod 644 "$APPS/$APP_ID.desktop"

for size in 16 24 32 48 64 128 256; do
    src=$HERE/icons/hicolor/${size}x${size}/apps/$APP_ID.png
    [ -f "$src" ] || continue
    mkdir -p "$ICONS/${size}x${size}/apps"
    cp "$src" "$ICONS/${size}x${size}/apps/$APP_ID.png"
done

refresh

echo "DiscWright is in the application menu, starting $EXEC."
echo "To take it out again:  $0 --uninstall"
