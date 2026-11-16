#!/bin/bash
# Publish the Linux DiscWright as the one file people would download, start
# that file, and see a window called DiscWright appear.
#
# The suite cannot answer this. It runs the app out of its build folder, and
# the published file is put together differently: on 2026-11-16 two native
# Skia libraries of one name went into it, the wrong one won, and the app died
# before its window opened while every test passed. So this starts the real
# thing and looks for the real window.
#
#   bash tools/see-the-published-app-start.sh              on the screen in DISPLAY
#   xvfb-run -a bash tools/see-the-published-app-start.sh  with no screen, as CI does
#
# Needs xdotool. Leaves the published file in artifacts/linux-app.

set -u
cd "$(dirname "$0")/.."

out=artifacts/linux-app
said=artifacts/linux-app-said.txt
rm -rf "$out" && mkdir -p "$out"

if ! dotnet publish src/DiscWright.LinuxApp/DiscWright.LinuxApp.csproj \
    -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$out" --nologo -v q; then
  echo "FAILED: it did not publish."
  exit 1
fi

app="$out/DiscWright.LinuxApp"
echo "Published: $(du -h "$app" | cut -f1) in one file."

"$app" > "$said" 2>&1 &
started=$!

# Twenty seconds, in half seconds. It opens in under one on the test machine,
# and the first run of a new file unpacks its native libraries first.
for _ in $(seq 40); do
  if xdotool search --name '^DiscWright$' > /dev/null 2>&1; then
    echo "A window called DiscWright is open."
    kill "$started" 2> /dev/null
    exit 0
  fi

  if ! kill -0 "$started" 2> /dev/null; then
    echo "FAILED: it stopped before any window opened, and said:"
    cat "$said"
    exit 1
  fi

  sleep 0.5
done

echo "FAILED: twenty seconds and no window called DiscWright. It said:"
cat "$said"
kill "$started" 2> /dev/null
exit 1
