---
name: goldens
description: Regenerate the reference files this port is tested against, which Windows DiscWright itself produces - the menu template, the reference menus, the project file and the background. Use when the Windows app changes something the port copies, or when a golden-file test fails after a port.
---

# Regenerating the goldens

This port is not tested against its own idea of a disc. It is tested against
files Windows DiscWright really wrote, and when the Windows app changes
something the port copies, those files have to be made again **on the Windows
machine, by the Windows app**, not edited here.

Needs both repositories side by side and Windows PowerShell 5.1. The suite
itself runs in WSL; only the regeneration is a Windows job.

## What is a golden, and which tool makes it

| File | Made by | Changes when |
|---|---|---|
| `src/discwright/menu.hta.in` | `tools/menu_template.py` | the `$tpl` here-string in `DiscWright.ps1` changes |
| `tests/fixtures/menu/windows-<ver>/*.hta` | `tools/windows/Make-MenuReference.ps1` | the same, or `cases.json` gains a case |
| `tests/fixtures/projects/windows-<ver>.json` | `tools/windows/Make-ProjectReference.ps1` | the project file's **schema** moves |
| `tests/fixtures/background/*` | `tools/background_sources.py`, then `tools/windows/Make-BackgroundReference.ps1` | the background composition changes |
| `tests/fixtures/icons/windows-*` | copied off a real build | the icon builder changes |
| `tests/fixtures/windows-0.7.2/alanwake` | copied off a real disc | rarely; it is a whole staged disc |

## The menu, which is the usual one

```powershell
# 1. Has it changed at all?
python tools\menu_template.py C:\path\to\discwright\DiscWright.ps1 --check

# 2. Lift it, verbatim
python tools\menu_template.py C:\path\to\discwright\DiscWright.ps1

# 3. Have the Windows app write the reference menus
powershell -ExecutionPolicy Bypass -File tools\windows\Make-MenuReference.ps1 `
    -DiscWright C:\path\to\discwright\DiscWright.ps1
```

Then, and this is the part that is easy to miss:

- **`tests/fixtures/menu/cases.json` must carry every field `New-MenuHta` reads.**
  When 0.8.0 added the folder each game sits in, every case needed a `Folder`,
  and a case was added whose game has no installer at all. A missing field does
  not fail loudly; it produces a reference with an empty value in it.
- **Point `tests/test_menu.py` at the new folder** and delete the old one. A
  reference directory for a version the app no longer is, is worse than none.
- The menus must then match **byte for byte**. They are compared as checksums,
  because diffing two whole menus never finishes on a runner.

## The project file

```powershell
powershell -ExecutionPolicy Bypass -File tools\windows\Make-ProjectReference.ps1 `
    -DiscWright C:\path\to\discwright\DiscWright.ps1 `
    -GogFolder 'F:\DWdemo\Alan Wake'
```

It writes one project holding both kinds of entry, a GOG download and a folder
of game files, so both values of `Source` come from Windows rather than from
this port's idea of them. `-GogFolder` is optional; without it a stand-in is
built, which is enough for the schema and names a folder that never existed.

- **Only regenerate when the schema moves.** The version in the filename is
  provenance, not a reason: a file written by 0.8.1 with schema 9 proves nothing
  that the 0.8.0 one did not. Replace rather than accumulate.
- Then point `tests/test_project.py` at it.

## Afterwards, every time

```sh
~/.venvs/discwright/bin/python -m pytest -q      # or tools/wsl-test.sh
~/.venvs/discwright/bin/python tools/mutate.py   # about twenty minutes
```

- **Do not commit while `mutate.py` is running.** It holds a deliberately broken
  file for each mutation, and `git add` has captured one: a commit went out with
  the cap on `folder_executables` removed. Wait for it to finish.
- Say which claims rest on a local run and which on CI, as always.

## If a golden and the port disagree

The Windows app is the reference and this port is what is wrong, **unless the
Windows behaviour cannot be right**. That has happened four times, each fixed in
both places with a test that failed first: a folder name cut on a dot, an icon
that came out as noise, a light line along the menu panel, and project paths
after a rebuild. If it looks like one of those, measure it before copying it.
