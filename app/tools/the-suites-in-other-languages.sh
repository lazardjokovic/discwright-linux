#!/bin/bash
# Run the Linux suite and the window's tests with the machine set to other
# languages: where a decimal point is a comma, and where a capital i has a
# dot on it.
#
#   bash tools/the-suites-in-other-languages.sh
#   bash tools/the-suites-in-other-languages.sh sr_RS.UTF-8 ja_JP.UTF-8
#
# German, Polish and Turkish unless told otherwise. A language the machine
# has not got is said, with the line that makes it:
#
#   sudo locale-gen de_DE.UTF-8 pl_PL.UTF-8 tr_TR.UTF-8
#
# Every test anybody runs is run in whatever language the machine is in,
# which for CI and most developers is English. So a size written "1,3 KB"
# where the test wanted "1.3 KB", or a name put into capitals the Turkish
# way, passes everywhere it is looked at. The first run of this, on
# 2026-11-16, found four tests that wanted a full stop.
#
# It compares each language with English on the same machine, so what is
# on the waiting list and fails everywhere is not counted against any of
# them. Not in CI: it is the suite three more times.
#
# Returns nought only when no language fails a test English passes.

set -u
cd "$(dirname "$0")/.."

# The runner's own words stay in English, so its last line can be read. The
# tests themselves still run in the language asked for.
export DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033

shared=tests/DiscWright.LinuxTests/DiscWright.LinuxTests.csproj
window=tests/DiscWright.LinuxAppTests/DiscWright.LinuxAppTests.csproj
said=$(mktemp -d)
trap 'rm -rf "$said"' EXIT

if [ $# -gt 0 ]; then languages="$*"; else languages="de_DE.UTF-8 pl_PL.UTF-8 tr_TR.UTF-8"; fi

for each in $languages; do
  if ! locale -a | grep -qix "$(echo "$each" | sed 's/UTF-8/utf8/')"; then
    echo "FAILED: this machine has no $each. Make it with: sudo locale-gen $each"
    exit 1
  fi
done

dotnet build $shared -c Release --nologo -v q > /dev/null 2>&1 || { echo "FAILED: the shared tests did not build."; exit 1; }
dotnet build $window -c Release --nologo -v q > /dev/null 2>&1 || { echo "FAILED: the window's tests did not build."; exit 1; }

failing() { grep -E "^\s+Failed " "$1" | awk '{print $2}' | LC_ALL=C sort -u; }

run() {
  LANG=$1 LC_ALL=$1 dotnet test $shared -c Release --no-build --nologo > "$said/shared-$1" 2>&1
  LANG=$1 LC_ALL=$1 env -u DISPLAY dotnet test $window -c Release --no-build --nologo > "$said/window-$1" 2>&1
  failing "$said/shared-$1" > "$said/shared-$1.failed"
  failing "$said/window-$1" > "$said/window-$1.failed"

  # A run that printed no last line did not run at all, which is not the
  # same as running and failing nothing.
  grep -qE "Passed!|Failed!" "$said/shared-$1" || { echo "FAILED: the shared tests did not run in $1."; exit 1; }
  grep -qE "Passed!|Failed!" "$said/window-$1" || { echo "FAILED: the window's tests did not run in $1."; exit 1; }
}

run en_US.UTF-8
echo "In English: $(wc -l < "$said/shared-en_US.UTF-8.failed") shared tests fail, which is the waiting list, and $(wc -l < "$said/window-en_US.UTF-8.failed") of the window's."

worse=0

for each in $languages; do
  run "$each"
  only=$(LC_ALL=C comm -13 "$said/shared-en_US.UTF-8.failed" "$said/shared-$each.failed"; LC_ALL=C comm -13 "$said/window-en_US.UTF-8.failed" "$said/window-$each.failed")

  if [ -z "$only" ]; then
    echo "ok: in $each nothing fails that passes in English."
  else
    worse=$((worse + 1))
    echo "FAILED: in $each these fail and in English they pass:"
    echo "$only" | sed 's/^/    /'
  fi
done

[ "$worse" -eq 0 ]
