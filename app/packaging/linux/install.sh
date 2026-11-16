#!/bin/sh
# Put DiscWright on this computer, for the person running this, and in the
# application menu.
#
#   ./install.sh              put it in
#   ./install.sh --uninstall  take it out again
#
# Everything goes under your home folder: no root, nothing touched outside
# this account, and the same script removes what it added.
#
#   ~/.local/bin/DiscWright                                  the program
#   ~/.local/share/applications/com.discwright.DiscWright.desktop
#   ~/.local/share/icons/hicolor/<size>/apps/com.discwright.DiscWright.png
#
# Taking it out also clears ~/.net/DiscWright, which is not put there by this
# script: the program is one file, and the first time it runs it unpacks the
# two libraries it draws with into that folder. They are parts of the
# program and not anything of yours, so they go when it goes.
#
# POSIX sh on purpose: this runs on whatever the distribution ships.
set -eu

APP_ID=com.discwright.DiscWright
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DATA=${XDG_DATA_HOME:-$HOME/.local/share}
BIN=$HOME/.local/bin
APPS=$DATA/applications
ICONS=$DATA/icons/hicolor
PROGRAM=$BIN/DiscWright
UNPACKED=${DOTNET_BUNDLE_EXTRACT_BASE_DIR:-$HOME/.net}/DiscWright
SIZES="16 24 32 48 64 128 256"

refresh() {
    # Both are optional: the entry works without them, it may just take the
    # desktop a moment longer to notice. Their failure is not this script's.
    if [ -d "$APPS" ] && command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "$APPS" >/dev/null 2>&1 || true
    fi
    if [ -d "$ICONS" ] && command -v gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q -t -f "$ICONS" >/dev/null 2>&1 || true
    fi
}

if [ "${1-}" = "--uninstall" ]; then
    rm -f "$PROGRAM" "$APPS/$APP_ID.desktop"
    rm -rf "$UNPACKED"
    # The folder above it too, when DiscWright was the only thing in it.
    rmdir "$(dirname -- "$UNPACKED")" 2>/dev/null || true
    for size in $SIZES; do
        rm -f "$ICONS/${size}x${size}/apps/$APP_ID.png"
    done
    refresh
    echo "DiscWright is off this computer."
    # Said, because it is somebody's own: what they chose and what it wrote
    # down while working is not this script's to delete.
    if [ -d "$DATA/DiscWright" ]; then
        echo "Its settings and logs are still in $DATA/DiscWright. Delete that folder too if you want them gone."
    fi
    exit 0
fi

if [ -n "${1-}" ]; then
    echo "Not something this script knows: $1" >&2
    echo "  $0              put DiscWright in" >&2
    echo "  $0 --uninstall  take it out again" >&2
    exit 2
fi

# The program is beside this script in the folder that was unpacked. Said
# plainly when it is not, which is what running a copy of this script from
# somewhere else looks like.
if [ ! -f "$HERE/DiscWright" ]; then
    echo "The DiscWright program is not beside this script, in $HERE." >&2
    echo "Unpack the whole download and run install.sh from inside it." >&2
    exit 1
fi

mkdir -p "$BIN" "$APPS"

# Copied and not pointed at: the folder this was unpacked into is likely to
# be Downloads, and an entry that stops working when that folder is tidied
# away is the worst way to find out.
#
# To a name of its own first and then moved over, so a DiscWright that is
# running while a newer one goes in keeps the file it started from.
cp "$HERE/DiscWright" "$PROGRAM.new"
chmod 755 "$PROGRAM.new"
mv -f "$PROGRAM.new" "$PROGRAM"

# Quoted in the entry, because a home folder with a space in its name is
# otherwise read as two arguments and nothing opens.
sed -e "s|^Exec=.*|Exec=\"$PROGRAM\" %F|" \
    -e "s|^TryExec=.*|TryExec=$PROGRAM|" \
    "$HERE/$APP_ID.desktop" > "$APPS/$APP_ID.desktop"
chmod 644 "$APPS/$APP_ID.desktop"

for size in $SIZES; do
    src=$HERE/icons/hicolor/${size}x${size}/apps/$APP_ID.png
    [ -f "$src" ] || continue
    mkdir -p "$ICONS/${size}x${size}/apps"
    cp "$src" "$ICONS/${size}x${size}/apps/$APP_ID.png"
done

refresh

echo "DiscWright is in the application menu."
echo "To take it out again:  $0 --uninstall"

# The one thing it needs that is not in the download. Said now, when it is a
# line to type, and not after a disc has been laid out and cannot be made
# into an image. DiscWright says the same itself if it is still missing.
if ! command -v xorriso >/dev/null 2>&1; then
    echo
    echo "One more thing: DiscWright makes its disc images with xorriso, and it is not on this computer yet."
    echo "  Ubuntu, Debian, Mint:  sudo apt install xorriso"
    echo "  Fedora:                sudo dnf install xorriso"
    echo "  Arch, Manjaro:         sudo pacman -S libisoburn"
fi

# And four libraries of the system's that it draws its window through and
# finds typefaces with. Every desktop has them. A system with no desktop on
# it may not, and there DiscWright says the same itself when it is started
# (WhatTheSystemLacks in the program holds the same four, and a test holds
# the two lists together). Measured in bare containers of Debian 12 and
# Ubuntu 22.04 and 20.04, which lacked the first.
#
# Asked of ldconfig, which lists what the system can load. Where it cannot
# be asked, or lists nothing, as on a system whose C library is not glibc,
# nothing is said: no answer is not the answer "missing".
if command -v ldconfig >/dev/null 2>&1; then
    LDCONFIG=ldconfig
elif [ -x /sbin/ldconfig ]; then
    LDCONFIG=/sbin/ldconfig
else
    LDCONFIG=
fi

LOADABLE=
if [ -n "$LDCONFIG" ]; then
    LOADABLE=$("$LDCONFIG" -p 2>/dev/null || true)
fi

if [ -n "$LOADABLE" ]; then
    lacking=
    debian=
    fedora=
    arch=

    # The file, and what brings it on Debian, on Fedora and on Arch.
    while read -r file on_debian on_fedora on_arch; do
        case "$LOADABLE" in
            *"$file"*) ;;
            *)
                lacking="$lacking $file"
                debian="$debian $on_debian"
                fedora="$fedora $on_fedora"
                arch="$arch $on_arch"
                ;;
        esac
    done <<EOF
libfontconfig.so.1 libfontconfig1 fontconfig fontconfig
libX11.so.6 libx11-6 libX11 libx11
libICE.so.6 libice6 libICE libice
libSM.so.6 libsm6 libSM libsm
EOF

    if [ -n "$lacking" ]; then
        echo
        echo "And this: DiscWright draws its window with a few libraries every desktop has, and this system is missing:$lacking"
        echo "  Ubuntu, Debian, Mint:  sudo apt install$debian"
        echo "  Fedora:                sudo dnf install$fedora"
        echo "  Arch, Manjaro:         sudo pacman -S$arch"
    fi
fi
