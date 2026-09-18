#!/bin/bash
# Run the suite from WSL on a Windows machine, where the repo sits on the Windows
# side and the virtual environment on the Linux side. Pass DISCWRIGHT_GOG_DIR to
# include the tests against real GOG downloads, e.g. /mnt/f/DWdemo.
set -e
cd "$(dirname "$0")/.."
venv="${DISCWRIGHT_VENV:-$HOME/.venvs/discwright}"
if [ ! -x "$venv/bin/python" ]; then
    python3 -m venv "$venv"
fi
"$venv/bin/pip" install -q -e ".[dev]"
"$venv/bin/python" -m pytest "$@"
