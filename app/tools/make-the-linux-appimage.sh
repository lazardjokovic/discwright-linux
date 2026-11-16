#!/bin/bash
# Put the Linux AppImage together: DiscWright and the xorriso it needs, in
# one file that is downloaded, marked executable and run.
#
#   bash tools/make-the-linux-tarball.sh     first: the same published file
#   bash tools/make-the-linux-appimage.sh
#
# Leaves artifacts/DiscWright-x86_64.AppImage, and beside it the source of
# the xorriso inside, which has to be offered wherever the AppImage is.
#
# Needs musl-tools (sudo apt install musl-tools), a C compiler, make and
# curl. Three files are fetched from outside, each checked against the hash
# in packaging/linux/what-the-appimage-carries.sh and kept in
# artifacts/carried so the next run fetches nothing.
#
# The tarball asks the system for xorriso. This carries its own, built here
# from source and linked statically, so it leans on no library of the
# system's and runs on a distribution older than the one that built it.

set -eu
cd "$(dirname "$0")/.."
. packaging/linux/what-the-appimage-carries.sh

from=artifacts/linux-tarball/DiscWright
kept=artifacts/carried
appdir=artifacts/linux-appimage/DiscWright.AppDir
out=artifacts/DiscWright-x86_64.AppImage
id=com.discwright.DiscWright

stop() { echo "FAILED: $*" >&2; exit 1; }

[ -x "$from/DiscWright" ] && [ -f "$from/THIRD-PARTY-NOTICES.txt" ] \
    || stop "the tarball's folder is not there. Run tools/make-the-linux-tarball.sh first: the AppImage is made from the same published file."

mkdir -p "$kept"

# Fetched once and kept. A file that is there and is the right one is not
# fetched again, and one that is not the right one is never used.
fetch() {
    url=$1; sum=$2; to="$kept/$3"

    if [ -f "$to" ] && echo "$sum  $to" | sha256sum -c --status; then
        return
    fi

    rm -f "$to"
    curl -fsSL --retry 3 -o "$to" "$url" || stop "could not fetch $url"

    if ! echo "$sum  $to" | sha256sum -c --status; then
        found=$(sha256sum "$to" | cut -d' ' -f1)
        rm -f "$to"
        stop "$url is not the file this was measured with. Wanted $sum, and it is $found."
    fi
}

fetch "$XORRISO_URL" "$XORRISO_SHA256" "$XORRISO_FILE"
fetch "$APPIMAGETOOL_URL" "$APPIMAGETOOL_SHA256" "appimagetool-$APPIMAGETOOL_VERSION"
fetch "$RUNTIME_URL" "$RUNTIME_SHA256" "runtime-$RUNTIME_VERSION"
chmod 755 "$kept/appimagetool-$APPIMAGETOOL_VERSION"

# ------------------------------------------------------------------ xorriso

xorriso="$kept/xorriso-$XORRISO_VERSION-static"

if [ ! -x "$xorriso" ]; then
    command -v musl-gcc > /dev/null || stop "musl-gcc is not on this machine: sudo apt install musl-tools"

    work=$PWD/artifacts/xorriso-build
    rm -rf "$work" && mkdir -p "$work/kernel-headers"
    tar -xzf "$kept/$XORRISO_FILE" -C "$work"

    # musl-gcc looks only in musl's own headers. xorriso talks to the drive
    # through the kernel's, which belong to neither C library, so they are
    # lent to it from a folder of links.
    ln -s /usr/include/linux "$work/kernel-headers/linux"
    ln -s /usr/include/asm-generic "$work/kernel-headers/asm-generic"
    ln -s "/usr/include/$(gcc -print-multiarch)/asm" "$work/kernel-headers/asm"
    ln -s /usr/include/scsi "$work/kernel-headers/scsi"

    # -static at configure is not enough: libtool drops it, and what comes
    # out still wants musl's own loader, which runs on the machine that built
    # it and nowhere else. -all-static at make is what links everything in.
    (
        cd "$work/$XORRISO_UNPACKS_TO"
        # shellcheck disable=SC2086
        CC=musl-gcc CFLAGS="-O2 -isystem $work/kernel-headers" LDFLAGS=-static \
            ./configure $XORRISO_CONFIGURED > "$work/configure.log" 2>&1
        make -j"$(nproc)" LDFLAGS=-all-static > "$work/make.log" 2>&1
    ) || { tail -5 "$work/make.log" >&2; stop "xorriso did not build. The whole of it is in $work/make.log."; }

    strip -o "$xorriso" "$work/$XORRISO_UNPACKS_TO/xorriso/xorriso"
    cp "$work/$XORRISO_UNPACKS_TO/COPYING" "$kept/xorriso-$XORRISO_VERSION-COPYING"
    chmod 755 "$xorriso"
fi

# Asked of the file itself each time, kept or just built.
file -b "$xorriso" | grep -q "statically linked" \
    || stop "the xorriso that was built leans on the system's libraries: $(file -b "$xorriso")"
"$xorriso" -version 2>&1 | grep -q "xorriso version   :  $XORRISO_VERSION\$" \
    || stop "the xorriso that was built does not say it is $XORRISO_VERSION."

# musl is linked into that xorriso, and is under a licence that asks for its
# notice to go with it.
musl=/usr/share/doc/musl/copyright
[ -f "$musl" ] || stop "musl's own notice is not at $musl, and it has to go in beside the xorriso built with it."

# ---------------------------------------------------------------- the folder

rm -rf artifacts/linux-appimage "$out"
mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons" \
    "$appdir/usr/share/doc/DiscWright" "$appdir/usr/share/doc/xorriso" "$appdir/usr/share/doc/musl"

cp "$from/DiscWright" "$appdir/usr/bin/DiscWright"
cp "$xorriso" "$appdir/usr/bin/xorriso"
cp packaging/linux/AppRun "$appdir/AppRun"

cp -r "$from/icons/hicolor" "$appdir/usr/share/icons/hicolor"
cp "$from/icons/hicolor/256x256/apps/$id.png" "$appdir/$id.png"
ln -s "$id.png" "$appdir/.DirIcon"

# The tarball's entry, without the line that asks whether DiscWright is on
# the PATH. Inside an AppImage it never is, and a desktop that believes
# that line hides the entry.
grep -v '^TryExec=' packaging/linux/$id.desktop > "$appdir/$id.desktop"
cp "$appdir/$id.desktop" "$appdir/usr/share/applications/$id.desktop"

cp "$from/LICENSE.txt" "$from/THIRD-PARTY-NOTICES.txt" "$appdir/usr/share/doc/DiscWright/"
cp "$kept/xorriso-$XORRISO_VERSION-COPYING" "$appdir/usr/share/doc/xorriso/COPYING"
cp "$musl" "$appdir/usr/share/doc/musl/copyright"

cat > "$appdir/usr/share/doc/xorriso/WHERE-THE-SOURCE-IS.txt" <<EOF
The xorriso inside this AppImage
================================

This AppImage carries GNU xorriso $XORRISO_VERSION, at usr/bin/xorriso. DiscWright
runs it to write a disc image. It is a separate program from DiscWright,
with its own authors and its own licence.

GNU xorriso is free software under the GNU General Public License, version
3 or later. The licence is beside this file, in COPYING. You may run it,
study it, change it and pass it on, under those terms.

The xorriso here was built, with nothing in it changed, from

    $XORRISO_URL
    sha256 $XORRISO_SHA256

configured with

    $XORRISO_CONFIGURED

and linked statically against musl libc, whose own notice is in
usr/share/doc/musl. The script that does all of it is
tools/make-the-linux-appimage.sh in DiscWright's source, which is linked
from https://discwright.com

To read everything this AppImage holds, run it with --appimage-extract.
EOF

chmod -R u=rwX,go=rX "$appdir"
chmod 755 "$appdir/AppRun" "$appdir/usr/bin/DiscWright" "$appdir/usr/bin/xorriso"

for each in AppRun $id.desktop; do
    if grep -q "$(printf '\r')" "$appdir/$each"; then
        stop "$each has carriage returns in it. Check out with LF, see .gitattributes."
    fi
done

# --------------------------------------------------------------- the AppImage

# The tool is an AppImage itself. Unpacked and run, and not mounted, so
# making this needs no FUSE, which a build machine often lacks.
if ! APPIMAGE_EXTRACT_AND_RUN=1 ARCH=x86_64 "$kept/appimagetool-$APPIMAGETOOL_VERSION" \
        --runtime-file "$kept/runtime-$RUNTIME_VERSION" "$appdir" "$out" > artifacts/linux-appimage/appimagetool.log 2>&1; then
    tail -5 artifacts/linux-appimage/appimagetool.log >&2
    stop "appimagetool did not make the AppImage."
fi

chmod 755 "$out"

# Beside it, because whoever is given the AppImage has to be able to get
# the source of the xorriso inside from the same place.
cp "$kept/$XORRISO_FILE" "artifacts/$XORRISO_FILE"

echo "Made $out, $(du -h "$out" | cut -f1), and artifacts/$XORRISO_FILE beside it."
