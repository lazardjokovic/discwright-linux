#!/bin/bash
# Start both Linux downloads on another system than this one: an older one,
# or another family, that has never had .NET on it.
#
#   bash tools/make-the-linux-tarball.sh
#   bash tools/make-the-linux-appimage.sh
#   bash tools/see-the-linux-downloads-on-another-system.sh debian:12
#   bash tools/see-the-linux-downloads-on-another-system.sh ubuntu:22.04
#
# Needs podman or docker, and the network: the system is a container of the
# bare distribution, given only a screen that is not a screen (xvfb), xdotool
# to look for the window, and file. The downloads are lent to it read only.
# Debian and Ubuntu images only: what is put in is asked of apt.
#
# What it answers, that nothing run on the machine that built them can:
#
# - Does the one file start where .NET has never been?
# - Does it start against an older C library than the one it was built on?
# - On the bare system, which lacks a library every desktop has, does
#   DiscWright say so in a sentence, or die with forty lines and a core dump?
# - Does the xorriso inside the AppImage run there, where there is no
#   xorriso at all, and keep an accent in a name?
#
# A container has no FUSE, so the AppImage is unpacked first, which is the
# way round every AppImage carries. Mounted, it is run by
# see-the-linux-appimage-run.sh on the machine that built it.
#
# Every line it prints starts "ok:" or "FAILED:", and it ends by saying how
# many of each. It returns nought only when nothing failed.

set -u
cd "$(dirname "$0")/.."

image="${1:?which system, as a container image: debian:12, ubuntu:22.04}"
case "$image" in */*) ;; *) image="docker.io/library/$image" ;; esac

runs=$(command -v podman || command -v docker) || { echo "FAILED: neither podman nor docker is on this machine."; exit 1; }

for each in artifacts/DiscWright-linux-x64.tar.gz artifacts/DiscWright-x86_64.AppImage; do
  [ -f "$each" ] || { echo "FAILED: there is no $each. Make both downloads first."; exit 1; }
done

lent=$(mktemp -d)
trap 'rm -rf "$lent"' EXIT
cp artifacts/DiscWright-linux-x64.tar.gz artifacts/DiscWright-x86_64.AppImage "$lent/"
chmod 755 "$lent" && chmod 644 "$lent"/*

cat > "$lent/inside.sh" <<'EOF'
#!/bin/bash
oks=0; fails=0
ok() { echo "ok: $*"; oks=$((oks + 1)); }
no() { echo "FAILED: $*"; fails=$((fails + 1)); }

. /etc/os-release
echo "The system: $PRETTY_NAME, with the C library at $(ldd --version 2>&1 | head -1 | grep -o '[0-9.]*$')."

if command -v dotnet > /dev/null 2>&1; then no "this system has .NET on it, so it answers nothing about one that has not"; else ok ".NET has never been on this system"; fi
if command -v xorriso > /dev/null 2>&1; then no "this system has a xorriso of its own"; else ok "there is no xorriso on this system"; fi

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq > /dev/null 2>&1
if ! apt-get install -y -qq --no-install-recommends xvfb xdotool file > /tmp/apt.log 2>&1; then
  no "the screen could not be put in: $(tail -2 /tmp/apt.log | tr '\n' ' ')"
  echo; echo "$oks ok, $fails failed."; exit 1
fi

Xvfb :7 -screen 0 1024x768x24 > /dev/null 2>&1 &
sleep 2
export DISPLAY=:7 HOME=/root

mkdir /tmp/t && tar -C /tmp/t -xzf /lent/DiscWright-linux-x64.tar.gz
program=/tmp/t/DiscWright/DiscWright

# .NET wants libicu and says so itself, in its own sentence, before anything
# of DiscWright's runs. Where the bare system lacks it, it is put in first,
# so that what DiscWright says for its own part can be seen. The package has
# a number in its name that differs from one release to the next.
icu=$(apt-cache pkgnames libicu | grep -E '^libicu[0-9]+$' | sort | tail -1)

if ldconfig -p | grep -q "libicuuc.so"; then
  ok "libicu, which .NET wants, is here to begin with"
elif apt-get install -y -qq --no-install-recommends "$icu" > /tmp/apt-icu.log 2>&1; then
  ok "libicu, which .NET wants and asks for itself, was not here and was put in ($icu)"
else
  no "libicu could not be put in: $(tail -2 /tmp/apt-icu.log | tr '\n' ' ')"
fi

# ------------------------------------ on the bare system, which lacks things

if ldconfig -p | grep -q "libfontconfig.so.1"; then
  no "this system has fontconfig to begin with, so what DiscWright says without it was not seen"
else
  timeout 30 "$program" > /tmp/out.txt 2> /tmp/err.txt
  code=$?

  if [ "$code" -eq 1 ]; then ok "without fontconfig it stops with a code of one"; else no "without fontconfig it returned $code, and one was wanted"; fi

  if grep -q "^DiscWright cannot start: this system has no libfontconfig.so.1" /tmp/err.txt; then
    ok "and says which library the system has not got"
  else
    no "and did not say which library: $(head -c 300 /tmp/err.txt | tr '\n' ' ')"
  fi

  if grep -q "sudo apt install libfontconfig1" /tmp/err.txt; then ok "and the line to type"; else no "and not the line to type"; fi

  lines=$(wc -l < /tmp/err.txt)
  if [ "$lines" -le 6 ] && ! grep -q "Unhandled exception\|   at " /tmp/err.txt; then
    ok "in $lines lines, with no exception written out"
  else
    no "in $lines lines: $(grep -m2 'Unhandled exception\|   at ' /tmp/err.txt | tr '\n' ' ')"
  fi

  # What install.sh says on the same system, before anything is started.
  HOME=/tmp/home sh /tmp/t/DiscWright/install.sh > /tmp/installed.txt 2>&1
  if grep -q "this system is missing:.*libfontconfig.so.1" /tmp/installed.txt; then
    ok "install.sh says the same before anything is started"
  else
    no "install.sh did not say fontconfig was missing: $(tail -4 /tmp/installed.txt | tr '\n' ' ')"
  fi
fi

# ---------------------- with what a desktop has, by the names DiscWright gives

# Put in by the very line DiscWright and install.sh tell somebody to type,
# so a wrong package name in that line fails here.
said=$(sed -n 's/^  Ubuntu, Debian, Mint:  sudo apt install //p' /tmp/installed.txt | tail -1)

if [ -z "$said" ]; then
  no "install.sh named nothing to install, so nothing was"
elif apt-get install -y -qq --no-install-recommends $said > /tmp/apt2.log 2>&1; then
  ok "what it said to install can be installed ($said)"
else
  no "what it said to install could not be: $said: $(tail -2 /tmp/apt2.log | tr '\n' ' ')"
fi

see() {
  what="$1"; shift
  "$@" > /tmp/said.txt 2>&1 &
  started=$!

  for _ in $(seq 60); do
    if xdotool search --name '^DiscWright$' > /dev/null 2>&1; then
      ok "$what: a window called DiscWright opens"
      kill $started 2> /dev/null; pkill -x DiscWright 2> /dev/null; sleep 1
      return
    fi
    kill -0 $started 2> /dev/null || break
    sleep 0.5
  done

  no "$what: no window. It said: $(head -c 400 /tmp/said.txt | tr '\n' ' ')"
  kill $started 2> /dev/null; pkill -x DiscWright 2> /dev/null
}

see "the tarball's program" "$program"

cp /lent/DiscWright-x86_64.AppImage /tmp/a.AppImage && chmod +x /tmp/a.AppImage
see "the AppImage, unpacked first" env APPIMAGE_EXTRACT_AND_RUN=1 /tmp/a.AppImage

# ------------------------------------------------ the xorriso in the AppImage

(cd /tmp && /tmp/a.AppImage --appimage-extract > /dev/null 2>&1)
xorriso=/tmp/squashfs-root/usr/bin/xorriso

if "$xorriso" -version 2>&1 | grep -q "xorriso version"; then
  ok "the xorriso inside the AppImage runs here ($("$xorriso" -version 2>&1 | grep 'xorriso version' | tr -s ' '))"
else
  no "the xorriso inside the AppImage does not run here: $("$xorriso" -version 2>&1 | head -2 | tr '\n' ' ')"
fi

mkdir -p /tmp/disc/Café && echo hello > /tmp/disc/Café/a.txt

# Asked the way DiscWright asks, under the plainest locale there is, which
# is where an accent was once written into the names as underscores.
if LC_ALL=C "$xorriso" -as mkisofs -input-charset UTF-8 -iso-level 3 -J -joliet-long -r -V TEST -o /tmp/t.iso /tmp/disc > /tmp/x.log 2>&1 \
    && file -b /tmp/t.iso | grep -q "ISO 9660"; then
  ok "and makes an image"
else
  no "and did not make an image: $(tail -2 /tmp/x.log | tr '\n' ' ')"
fi

# The name as Windows reads it is two bytes a letter: C, a, f, then the e
# with its accent as 00 E9.
if grep -q -a -P '\x00C\x00a\x00f\x00\xe9' /tmp/t.iso 2> /dev/null; then
  ok "with the accent in the names Windows reads"
else
  no "and the accent is not in the names Windows reads"
fi

echo
echo "$oks ok, $fails failed."
[ "$fails" -eq 0 ]
EOF

"$runs" run --rm --network host -v "$lent":/lent:ro "$image" bash /lent/inside.sh 2>&1 \
  | grep -v "^Trying to pull\|^Getting image\|^Copying \|^Writing manifest\|^Resolved \|^Storing signatures\|^[0-9a-f]\{64\}$"
exit "${PIPESTATUS[0]}"
