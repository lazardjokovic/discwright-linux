#!/bin/bash
# Run one Python script from the repo, in the WSL virtual environment, after
# making sure the package and its dependencies are installed. For tools like
# tools/icon_diff.py when the repo lives on the Windows side:
#
#     wsl.exe -- bash tools/wsl-run.sh tools/icon_diff.py
set -e
cd "$(dirname "$0")/.."
venv="${DISCWRIGHT_VENV:-$HOME/.venvs/discwright}"
if [ ! -x "$venv/bin/python" ]; then
    python3 -m venv "$venv"
fi
"$venv/bin/pip" install -q -e ".[dev]"
"$venv/bin/python" "$@"
