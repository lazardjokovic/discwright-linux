#!/bin/bash
# Take the Linux AppImage apart to see what is in it, put the xorriso it
# carries through DiscWright's own tests, then start it with no xorriso
# anywhere on the PATH but its own.
#
#   bash tools/make-the-linux-tarball.sh
#   bash tools/make-the-linux-appimage.sh
#   xvfb-run -a bash tools/see-the-linux-appimage-run.sh
#
# Needs xdotool, xprop and file, desktop-file-validate where there is one,
# and the Linux tests built (tests/DiscWright.LinuxTests), since they are
# what the xorriso inside is held to.
#
# What it cannot do is press Build it: nothing here presses a button on a
# real window. That was done by hand, and is said in the pull request.
#
# Every line it prints starts "ok:" or "FAILED:", and it ends by saying how
# many of each. It returns nought only when nothing failed.

set -u
cd "$(dirname "$0")/.."
. packaging/linux/what-the-appimage-carries.sh

appimage=$PWD/artifacts/DiscWright-x86_64.AppImage
id=com.discwright.DiscWright
oks=0
fails=0

ok() { echo "ok: $*"; oks=$((oks + 1)); }
no() { echo "FAILED: $*"; fails=$((fails + 1)); }
check() { if "${@:2}" > /dev/null 2>&1; then ok "$1"; else no "$1"; fi; }

if [ ! -f "$appimage" ]; then
  echo "FAILED: there is no $appimage. Run tools/make-the-linux-appimage.sh first."
  exit 1
fi

room=$(mktemp -d)
started=
trap '[ -n "$started" ] && kill "$started" 2> /dev/null; sleep 1; rm -rf "$room"' EXIT

# ------------------------------------------------------------ what it is

check "it arrives able to run" test -x "$appimage"

what=$(file -b "$appimage")
case "$what" in
  ELF*static*) ok "it is a program that leans on no library to start" ;;
  *) no "it is not a statically linked program: $what" ;;
esac

(cd "$room" && "$appimage" --appimage-extract > /dev/null 2>&1)
in="$room/squashfs-root"
check "it can be taken apart" test -d "$in"

# ------------------------------------------------------------ what is in it

check "AppRun is in it and able to run" test -x "$in/AppRun"
check "AppRun has no carriage returns in it" sh -c "! grep -q \"\$(printf '\\r')\" '$in/AppRun'"
check "DiscWright is in it and able to run" test -x "$in/usr/bin/DiscWright"
check "xorriso is in it and able to run" test -x "$in/usr/bin/xorriso"

if [ -f artifacts/linux-tarball/DiscWright/DiscWright ]; then
  check "the DiscWright in it is the one in the tarball" cmp artifacts/linux-tarball/DiscWright/DiscWright "$in/usr/bin/DiscWright"
else
  no "the tarball's folder is not here, so the DiscWright inside was not compared with it"
fi

# The whole reason to build xorriso here: it must lean on nothing of the
# system's, or the AppImage runs only where it was built.
leans=$(ldd "$in/usr/bin/xorriso" 2>&1)
case "$leans" in
  *"not a dynamic executable"*) ok "the xorriso in it leans on no library of the system's" ;;
  *) no "the xorriso in it leans on: $(echo "$leans" | tr '\n' ' ')" ;;
esac

says=$("$in/usr/bin/xorriso" -version 2>&1 | grep "xorriso version")
check "the xorriso in it is $XORRISO_VERSION (it says: $says)" test "$says" = "xorriso version   :  $XORRISO_VERSION"

entry="$in/$id.desktop"
check "the entry is at the top, where an AppImage keeps it" test -f "$entry"
check "and where a desktop looks" cmp "$entry" "$in/usr/share/applications/$id.desktop"
check "the entry starts DiscWright by name" grep -qxF "Exec=DiscWright %F" "$entry"
check "the entry does not ask whether DiscWright is on the PATH" sh -c "! grep -q '^TryExec=' '$entry'"
check "the entry has no carriage returns in it" sh -c "! grep -q \"\$(printf '\\r')\" '$entry'"

if command -v desktop-file-validate > /dev/null 2>&1; then
  wrong=$(desktop-file-validate "$entry" 2>&1)
  if [ -z "$wrong" ]; then ok "the entry is one a desktop accepts"; else no "the entry is refused: $wrong"; fi
else
  no "desktop-file-validate is not on this machine, so the entry was not checked"
fi

icon=$(file -b "$in/$id.png" 2> /dev/null)
case "$icon" in
  "PNG image data, 256 x 256, 8-bit/color RGBA"*) ok "the icon at the top is the 256 one" ;;
  *) no "the icon at the top is not a 256 pixel PNG: $icon" ;;
esac

check "the icon a file manager shows for the file points at it" test "$(readlink "$in/.DirIcon")" = "$id.png"
check "the other sizes are where a desktop looks" test -f "$in/usr/share/icons/hicolor/16x16/apps/$id.png"

# -------------------------------------------------- whose everything in it is

doc="$in/usr/share/doc"

check "DiscWright's own licence is in it" test -s "$doc/DiscWright/LICENSE.txt"
check "the notices of what is packed into DiscWright are in it" grep -qF "Microsoft.NETCore.App.Runtime.linux-x64" "$doc/DiscWright/THIRD-PARTY-NOTICES.txt"

# xorriso is under the GPL, which asks for two things to go with a copy: the
# licence, and how to get the exact source it was built from.
check "xorriso's licence is in it" grep -q "GNU GENERAL PUBLIC LICENSE" "$doc/xorriso/COPYING"
check "and it is version 3" grep -q "Version 3, 29 June 2007" "$doc/xorriso/COPYING"

where="$doc/xorriso/WHERE-THE-SOURCE-IS.txt"
check "it says where the source of that xorriso is" grep -qF "$XORRISO_URL" "$where"
check "and names the file by its hash" grep -qF "sha256 $XORRISO_SHA256" "$where"
check "and says how it was built" grep -qF -- "$XORRISO_CONFIGURED" "$where"
check "and which version it is" grep -qF "GNU xorriso $XORRISO_VERSION" "$where"

source="artifacts/$XORRISO_FILE"
if [ -f "$source" ]; then
  found=$(sha256sum "$source" | cut -d' ' -f1)
  check "the source is beside the AppImage, and is the file it names ($found)" test "$found" = "$XORRISO_SHA256"
else
  no "the source of the xorriso inside is not beside the AppImage, at $source"
fi

check "the notice of the C library linked into that xorriso is in it" grep -qi "musl" "$doc/musl/copyright"

loose=$(find "$in" \( -type f -o -type d \) -perm /022 | head -3 | tr '\n' ' ')
check "nothing in it can be written by anybody but its owner (found: $loose)" test -z "$loose"

# ------------------------------ the xorriso inside, through DiscWright's tests

tests=tests/DiscWright.LinuxTests/bin/Release/net10.0/DiscWright.LinuxTests.dll

if [ -f "$tests" ]; then
  # First on the PATH, as AppRun puts it, so these are run by the xorriso
  # that is inside and not by the machine's.
  PATH="$in/usr/bin:$PATH" dotnet test tests/DiscWright.LinuxTests/DiscWright.LinuxTests.csproj \
    -c Release --no-build --nologo \
    --filter "FullyQualifiedName~ARealImageFromXorriso|FullyQualifiedName~AWholeDiscWithoutWindows|FullyQualifiedName~ADiscNamedForLinux" \
    > "$room/tests.txt" 2>&1
  code=$?
  passed=$(grep -o "Passed: *[0-9]*" "$room/tests.txt" | grep -o "[0-9]*" | tail -1)

  if [ "$code" -eq 0 ] && [ "${passed:-0}" -ge 30 ]; then
    ok "the xorriso inside makes images that pass DiscWright's own tests ($passed of them)"
  elif ! command -v dotnet > /dev/null 2>&1; then
    # Said for what it is. This line once failed with nothing after the
    # colon, and read as though the xorriso were at fault.
    no "dotnet is not on the PATH, so the xorriso inside was not put through DiscWright's own tests"
  else
    no "the xorriso inside did not pass DiscWright's own tests (returned $code, ${passed:-0} passed): $(grep -E 'Failed!|Passed!|error' "$room/tests.txt" | head -3 | tr '\n' ' ') $(tail -1 "$room/tests.txt")"
  fi
else
  no "the Linux tests are not built, so the xorriso inside was not put through them"
fi

# ------------------------------------- started, with no other xorriso to find

# What AppRun and the AppImage's own start need, and nothing else: no
# xorriso on this PATH, whatever the machine has.
mkdir "$room/bin" "$room/home"
for tool in sh dirname readlink fusermount fusermount3; do
  found=$(command -v $tool 2> /dev/null) && ln -s "$found" "$room/bin/$tool"
done

start() {
  env -i HOME="$room/home" PATH="$room/bin" DISPLAY="${DISPLAY-}" XAUTHORITY="${XAUTHORITY-}" "$@" "$appimage" \
    > "$room/app-said.txt" 2>&1 &
  started=$!
  window=

  for _ in $(seq 40); do
    window=$(xdotool search --name '^DiscWright$' 2> /dev/null | tail -1)
    [ -n "$window" ] && break
    kill -0 "$started" 2> /dev/null || break
    sleep 0.5
  done
}

how="mounted"
start

if [ -z "$window" ]; then
  # A machine with FUSE switched off cannot mount it, which is the
  # machine's matter and has a way round that is part of every AppImage.
  kill "$started" 2> /dev/null; wait "$started" 2> /dev/null
  how="unpacked first, because this machine would not mount it: $(head -2 "$room/app-said.txt" | tr '\n' ' ')"
  start APPIMAGE_EXTRACT_AND_RUN=1
fi

if [ -n "$window" ]; then
  ok "started with no xorriso on the PATH, a window called DiscWright opens ($how)"

  pid=$(xdotool getwindowpid "$window")
  its=$(tr '\0' '\n' < "/proc/$pid/environ" | sed -n 's/^PATH=//p')
  first=${its%%:*}

  check "what is running is the DiscWright inside it ($(readlink "/proc/$pid/exe"))" \
    sh -c "readlink /proc/$pid/exe | grep -q '/usr/bin/DiscWright\$'"

  # The xorriso DiscWright would find is the first one on its PATH, and it
  # has to be the one that was put inside, byte for byte.
  check "the first place it looks for xorriso is inside itself ($first)" test -x "$first/xorriso"
  check "and the xorriso there is the one that was put in" cmp "$first/xorriso" "$in/usr/bin/xorriso"
  check "and there is no other on its PATH ($its)" sh -c "! env PATH='${its#*:}' sh -c 'command -v xorriso'"

  class=$(xprop -id "$window" WM_CLASS 2> /dev/null | sed 's/.*", "\([^"]*\)".*/\1/')
  wanted=$(sed -n 's/^StartupWMClass=//p' "$entry")
  check "the window's class is what the entry says ($class, and the entry says $wanted)" test "$class" = "$wanted"
else
  no "no window called DiscWright opened. It said: $(cat "$room/app-said.txt")"
fi

kill "$started" 2> /dev/null
wait "$started" 2> /dev/null
started=
sleep 1

# An AppImage mounts itself under /tmp while it runs, in a folder named
# after its first letters.
left=$(grep -c "\.mount_DiscWr" /proc/mounts)
check "it is not left mounted once it has stopped" test "$left" -eq 0

echo
echo "$oks ok, $fails failed."
[ "$fails" -eq 0 ]
