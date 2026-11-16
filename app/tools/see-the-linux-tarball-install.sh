#!/bin/bash
# Unpack the Linux download somewhere fresh, install it into a home folder
# with nothing in it, start it the way the application menu would, and take
# it out again.
#
#   bash tools/make-the-linux-tarball.sh
#   bash tools/see-the-linux-tarball-install.sh              on the screen in DISPLAY
#   xvfb-run -a bash tools/see-the-linux-tarball-install.sh  with no screen, as CI does
#
# Needs xdotool and xprop, and desktop-file-validate where there is one.
#
# The home folder has a space in its name on purpose. A path with a space in
# a menu entry is two arguments unless it is quoted, and then nothing opens.
#
# Every line it prints starts "ok:" or "FAILED:", and it ends by saying how
# many of each. It returns nought only when nothing failed.

set -u
cd "$(dirname "$0")/.."

tarball=artifacts/DiscWright-linux-x64.tar.gz
id=com.discwright.DiscWright
oks=0
fails=0

ok() { echo "ok: $*"; oks=$((oks + 1)); }
no() { echo "FAILED: $*"; fails=$((fails + 1)); }
check() { if "${@:2}" > /dev/null 2>&1; then ok "$1"; else no "$1"; fi; }

if [ ! -f "$tarball" ]; then
  echo "FAILED: there is no $tarball. Run tools/make-the-linux-tarball.sh first."
  exit 1
fi

room=$(mktemp -d)
started=
trap '[ -n "$started" ] && kill "$started" 2> /dev/null; rm -rf "$room"' EXIT

home="$room/a home with a space"
mkdir -p "$home" "$room/unpacked"

# A PATH with what the script uses and no xorriso on it, for asking what it
# says on a machine without one, whatever this machine has.
mkdir "$room/bin"
for tool in sh cp mv rm chmod sed mkdir dirname; do
  ln -s "$(command -v $tool)" "$room/bin/$tool"
done

install() { env -i HOME="$home" PATH="$1" sh "$2" "${@:3}"; }

# ------------------------------------------------------------ what is in it

tar -C "$room/unpacked" -xzf "$tarball"
here="$room/unpacked/DiscWright"

check "it unpacks to one folder called DiscWright" test "$(ls "$room/unpacked")" = DiscWright

for each in DiscWright install.sh $id.desktop README.txt LICENSE.txt THIRD-PARTY-NOTICES.txt; do
  check "it holds $each" test -f "$here/$each"
done

# The one file has other people's work packed into it, and each licence asks
# that its notice goes where a copy goes. Named one by one, so that leaving
# any of them out of the notices is a line that fails here.
notices="$here/THIRD-PARTY-NOTICES.txt"

for theirs in "Microsoft.NETCore.App.Runtime.linux-x64" "Avalonia 1" "Avalonia.Skia" "Avalonia.X11" \
              "Avalonia.Fonts.Inter" "SkiaSharp 3" "SkiaSharp.NativeAssets.Linux" "HarfBuzzSharp 8" \
              "HarfBuzzSharp.NativeAssets.Linux" "MicroCom.Runtime" "Tmds.DBus.Protocol"; do
  check "the notices name $theirs" grep -qF "$theirs" "$notices"
done

check "the notices carry .NET's own licence" grep -qF "Copyright (c) .NET Foundation and Contributors" "$notices"
check "and .NET's notices of what it carries in turn" grep -qF "THIRD-PARTY-NOTICES.TXT" "$notices"
# The MIT text is given once, at the end, for the packages that state the
# licence and carry no text of it. Looked for there and not anywhere, since
# .NET's own licence higher up has the same sentences in it.
check "and the MIT licence once more at the end, for those that carry no text of it" \
  sh -c "sed -n '/^The MIT licence\$/,\$p' '$notices' | grep -qF 'THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND'"
check "and says which those are" sh -c "grep -q '^For: .*Avalonia 12.*MicroCom.Runtime.*Tmds.DBus.Protocol' '$notices'"
check "and whose the typeface is, in its own words" grep -q "Copyright 20[0-9][0-9] The Inter Project Authors" "$notices"
check "and the licence the typeface is under" grep -qF "SIL Open Font License, Version 1.1" "$notices"
check "and Skia's own, which is not SkiaSharp's" grep -qi "Copyright.*Google" "$notices"

# Only what is in the Linux file. A notice for something that is not there
# is a list nobody can trust the rest of.
for absent in "NativeAssets.Win32" "NativeAssets.macOS" "NativeAssets.WebAssembly" "Angle.Windows" "BuildServices"; do
  check "the notices do not name $absent, which is not in the Linux file" sh -c "! grep -qF '$absent' '$notices'"
done

check "the program arrives able to run" test -x "$here/DiscWright"
check "the script arrives able to run" test -x "$here/install.sh"

owners=$(tar -tvzf "$tarball" | awk '{print $2}' | sort -u)
check "nothing in it is owned by whoever built it (found: $owners)" test "$owners" = "0/0"

loose=$(tar -tvzf "$tarball" | awk '$1 ~ /^.....w/ || $1 ~ /^........w/ {print $6}')
check "nothing in it can be written by anybody but its owner (found: $loose)" test -z "$loose"

for each in install.sh $id.desktop README.txt THIRD-PARTY-NOTICES.txt; do
  check "$each has no carriage returns in it" sh -c "! grep -q \"\$(printf '\\r')\" '$here/$each'"
done

for size in 16 24 32 48 64 128 256; do
  icon="$here/icons/hicolor/${size}x${size}/apps/$id.png"
  what=$(file -b "$icon" 2> /dev/null)
  case "$what" in
    "PNG image data, $size x $size, 8-bit/color RGBA"*) ok "the $size icon is a picture that size" ;;
    *) no "the $size icon is not a $size pixel PNG: $what" ;;
  esac
done

# ------------------------------------------------------------- putting it in

before=$(cd "$home" && find . | sort)

if install "$PATH" "$here/install.sh" > "$room/said.txt" 2>&1; then
  ok "install.sh ran"
else
  no "install.sh did not run: $(cat "$room/said.txt")"
fi

program="$home/.local/bin/DiscWright"
entry="$home/.local/share/applications/$id.desktop"

check "the program is in the home folder" test -x "$program"
check "and is the one that was in the download" cmp "$here/DiscWright" "$program"
check "a file of its own, and not a pointer at the folder that was unpacked" test ! -L "$program"
check "no half written copy is left beside it" test ! -e "$program.new"
check "the entry is in the application menu" test -f "$entry"
check "the 256 icon is where a desktop looks" test -f "$home/.local/share/icons/hicolor/256x256/apps/$id.png"
check "the 16 icon is too" test -f "$home/.local/share/icons/hicolor/16x16/apps/$id.png"

check "the entry starts the program by its whole path, quoted" \
  grep -qxF "Exec=\"$program\" %F" "$entry"
check "the entry is tried by the same path" grep -qxF "TryExec=$program" "$entry"
check "the entry wears the icon that was put in" grep -qxF "Icon=$id" "$entry"

if command -v desktop-file-validate > /dev/null 2>&1; then
  wrong=$(desktop-file-validate "$entry" 2>&1)
  if [ -z "$wrong" ]; then ok "the entry is one a desktop accepts"; else no "the entry is refused: $wrong"; fi
else
  no "desktop-file-validate is not on this machine, so the entry was not checked"
fi

check "it says where it is now" grep -q "application menu" "$room/said.txt"
check "it says how to take it out" grep -q -- "--uninstall" "$room/said.txt"

if command -v xorriso > /dev/null 2>&1; then
  check "with xorriso here it says nothing about xorriso" sh -c "! grep -q xorriso '$room/said.txt'"
fi

install "$room/bin" "$here/install.sh" > "$room/said-without.txt" 2>&1
check "without xorriso it says what to type on Ubuntu" grep -q "sudo apt install xorriso" "$room/said-without.txt"
check "and on Fedora" grep -q "sudo dnf install xorriso" "$room/said-without.txt"
check "and on Arch" grep -q "sudo pacman -S libisoburn" "$room/said-without.txt"
check "and it is in all the same" test -x "$program"

# ----------------------------------------------- started the way a menu would

HOME="$home" "$program" > "$room/app-said.txt" 2>&1 &
started=$!
window=

for _ in $(seq 40); do
  window=$(xdotool search --name '^DiscWright$' 2> /dev/null | tail -1)
  [ -n "$window" ] && break
  kill -0 "$started" 2> /dev/null || break
  sleep 0.5
done

if [ -n "$window" ]; then
  ok "started from where it was put, a window called DiscWright opens"

  # What the window calls itself, against what the entry says it will. When
  # these two part, the desktop does not know the running window for this
  # entry's.
  #
  # A window gives two names, WM_CLASS = "instance", "class". The class is
  # the one a desktop matches an entry on, and the one that does not move
  # when the file is renamed. The first is the file's name, and reading
  # that one here by mistake passed a wrong entry once.
  class=$(xprop -id "$window" WM_CLASS 2> /dev/null | sed 's/.*", "\([^"]*\)".*/\1/')
  wanted=$(sed -n 's/^StartupWMClass=//p' "$entry")
  check "the window's class is what the entry says ($class, and the entry says $wanted)" test "$class" = "$wanted"
else
  no "no window called DiscWright opened. It said: $(cat "$room/app-said.txt")"
fi

kill "$started" 2> /dev/null
wait "$started" 2> /dev/null
started=

check "it kept what it wrote under the home folder it was given" test -d "$home/.local/share/DiscWright"

# -------------------------------------------------------------- over itself

install "$PATH" "$here/install.sh" > /dev/null 2>&1
check "put in a second time, it is still one program" cmp "$here/DiscWright" "$program"

# ----------------------------------------------------- what it will not take

install "$PATH" "$here/install.sh" --remove > "$room/said-wrong.txt" 2>&1
check "a word it does not know is refused with a code of two" test $? -eq 2
check "and it says which word" grep -q -- "--remove" "$room/said-wrong.txt"
check "and nothing was taken out" test -x "$program"

mkdir "$room/alone"
cp "$here/install.sh" "$room/alone/"
install "$PATH" "$room/alone/install.sh" > "$room/said-alone.txt" 2>&1
check "run without the program beside it, it stops with a code of one" test $? -eq 1
check "and says to unpack the whole download" grep -q "Unpack the whole download" "$room/said-alone.txt"

# ------------------------------------------------------------ taking it out

install "$PATH" "$here/install.sh" --uninstall > "$room/said-out.txt" 2>&1
check "install.sh --uninstall ran" test $? -eq 0

check "the program is gone" test ! -e "$program"
check "the entry is gone" test ! -e "$entry"

left=$(cd "$home" && find . -name "$id*" | sort)
check "no icon is left (found: $left)" test -z "$left"

# What somebody chose and what it logged are theirs, and it says so.
check "the settings and logs are still there" test -d "$home/.local/share/DiscWright"
check "and it says where they are" grep -qF "$home/.local/share/DiscWright" "$room/said-out.txt"

# The program is one file that unpacks two libraries into ~/.net the first
# time it runs. Parts of the program, so they go with it.
check "what the program unpacked of itself is gone" test ! -e "$home/.net/DiscWright"
check "and the folder that was only for that" test ! -e "$home/.net"

# Left out: its own settings and logs, said above, and ~/.cache, which is
# the graphics driver's and the desktop's and nothing DiscWright writes.
after=$(cd "$home" && find . -type f | grep -v "^./.local/share/DiscWright/" | grep -v "^./.cache/" | grep -v "mimeinfo.cache\|icon-theme.cache" | sort)
check "nothing else it put in is left (found: $after)" test -z "$after"

echo
echo "$oks ok, $fails failed."
[ "$fails" -eq 0 ]
