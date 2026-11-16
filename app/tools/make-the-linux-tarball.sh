#!/bin/bash
# Put the Linux download together: the one file, the script that installs
# it, the entry for the application menu, the icons and a few words.
#
#   bash tools/make-the-linux-tarball.sh
#
# Leaves artifacts/DiscWright-linux-x64.tar.gz, which unpacks to one folder
# called DiscWright. No version in the name yet: what a Linux DiscWright's
# version is has not been decided, and nothing is released from here.
#
# The program inside is called DiscWright, and not DiscWright.LinuxApp as the
# project is. The name of a project is ours; the name of the file is what
# somebody types.

set -eu
cd "$(dirname "$0")/.."

published=artifacts/linux-tarball-published
folder=artifacts/linux-tarball/DiscWright
tarball=artifacts/DiscWright-linux-x64.tar.gz
id=com.discwright.DiscWright

rm -rf "$published" artifacts/linux-tarball "$tarball"
mkdir -p "$folder"

dotnet publish src/DiscWright.LinuxApp/DiscWright.LinuxApp.csproj \
    -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=none -p:DebugSymbols=false \
    -o "$published" --nologo -v q

cp "$published/DiscWright.LinuxApp" "$folder/DiscWright"
cp packaging/linux/install.sh packaging/linux/$id.desktop packaging/linux/README.txt "$folder/"
cp LICENSE "$folder/LICENSE.txt"

python3 tools/icons-out-of-the-ico.py src/DiscWright.App/DiscWright.ico "$folder/icons" "$id"

# Said by the files and not left to whoever unpacks it or to how the
# machine that built it was set up: a program and a script that arrive
# unable to run are the first thing somebody meets.
chmod -R u=rwX,go=rX "$folder"
chmod 755 "$folder/DiscWright" "$folder/install.sh"
chmod 644 "$folder/$id.desktop" "$folder/README.txt" "$folder/LICENSE.txt"

# No carriage returns in anything a Linux machine reads as text. A checkout
# on Windows can put them there, a menu entry with one is refused, and a
# script with one fails in words that do not mention line endings.
for each in install.sh $id.desktop README.txt; do
    if grep -q "$(printf '\r')" "$folder/$each"; then
        echo "FAILED: $each has carriage returns in it. Check out with LF, see .gitattributes." >&2
        exit 1
    fi
done

# Owned by nobody in particular, and in a fixed order, so the download does
# not carry the name of whoever built it.
tar --owner=0 --group=0 --numeric-owner --sort=name \
    -C artifacts/linux-tarball -czf "$tarball" DiscWright

echo "Made $tarball, $(du -h "$tarball" | cut -f1)."
