# DiscWright for Linux

Read this first. It carries the decisions and working rules from where this repo
was started, so work can continue on any machine.

## What this is

A Linux version of [DiscWright](https://github.com/lazardjokovic/discwright), which
turns a GOG offline installer into a burnable game disc: the game's own icon and
title in Windows Explorer, a menu on double-click, and the game's name and icon on
a Linux desktop.

Related repositories:

- `lazardjokovic/discwright` - the Windows app, a PowerShell/WinForms script. **It
  is the reference.** When the two tools disagree about what a disc contains, this
  repo is the one that is wrong.
- `lazardjokovic/discwright.com` - the website. Its `site/index.html` names the
  current Windows version in one place and has to be bumped on every release.

## Decisions already made

Made on 2026-09-18, with the reasons, so they are not reopened by accident:

1. **Python.** Present on every Linux desktop, readable by the people who will run
   it, and GTK4 gives it a native window later. Standard library only where
   possible; add a dependency only for a real need (Pillow will be one, for icons).
2. **Command line first.** A command that takes GOG folders and settings and
   writes the finished ISO. The window comes later, on top of it. The disc is the
   hard part and the part that has to be proven.
3. **The same disc as Windows.** Not a Linux-flavoured disc. A disc burned on
   either machine must behave identically on both: AutoRun menu, icon and label on
   Windows; name and icon on Linux. GOG's Linux `.sh` installers are a later
   feature on top of that, not part of the first milestone.

## How the port is done

- **Port the Windows function, and its reasoning.** Every function in
  `DiscWright.ps1` carries a comment explaining a failure it was written to avoid.
  Keep those in the docstring; they are why the code looks the way it does.
- **Golden files from a real Windows build.** `tests/fixtures/windows-0.7.2/` holds
  files Windows DiscWright 0.7.2 wrote, copied byte for byte off a real disc. Tests
  compare against them. `.gitattributes` keeps git from touching their line
  endings: `autorun.inf` is CRLF on purpose.
- **Port the Windows test cases too.** The Windows suite is
  `tests/DiscWright.Tests.ps1` in the other repo. Porting a function means porting
  its tests, so both tools agree on the same inputs.

## Facts about the disc, already measured

- **Volume id:** at most 16 of `A-Z a-z 0-9 _`, trimmed of underscores after the
  cut, `DISC` if nothing survives. Not what Windows shows; Explorer shows the full
  label from `autorun.inf`.
- **`autorun.inf`:** CRLF with a trailing CRLF, system ANSI codepage
  (Windows-1252 in practice), no BOM. AutoRun has no Unicode mode.
- **`.xdg-volume-info`:** UTF-8, no BOM, LF. A BOM makes GKeyFile read nothing.
- **Filesystems.** Windows DiscWright writes UDF 2.50 through IMAPI, plus ISO9660
  and Joliet when "readable on Windows XP and older" is ticked. IMAPI refuses any
  file over **2 GiB** in ISO9660 (measured; 0.7.2 fixed the app's wrong 4 GiB
  assumption). That 2 GiB is IMAPI's limit, not the format's: ISO9660 itself holds
  a file up to one byte under 4 GiB, and GOG splits its installers one byte under
  that.
- **xorriso writes the disc.** Measured in `docs/xorriso-spike.md`: an ISO built by
  xorriso from the folder Windows DiscWright staged mounts on Windows with the same
  label, Explorer reads its `autorun.inf`, and all 12 files are byte-identical,
  including a 4 GiB installer part. It is ISO9660 + Joliet rather than UDF, which
  also makes it readable on Windows XP, something the Windows tool cannot offer
  for a game that came in parts. Command:
  `xorriso -as mkisofs -iso-level 3 -J -joliet-long -r -V <volume id> -o <iso> <folder>`.

## Working here

Developed on Windows through **WSL2 (Ubuntu 24.04)** so far; works the same on real
Linux.

```sh
python3 -m venv ~/.venvs/discwright
~/.venvs/discwright/bin/pip install -e ".[dev]"
~/.venvs/discwright/bin/python -m pytest
```

xorriso is needed for anything that builds an ISO: `sudo apt install xorriso`.

- **Real GOG downloads.** Tests marked for them are skipped unless
  `DISCWRIGHT_GOG_DIR` points at a folder holding one GOG download per subfolder
  (`Alan Wake`, `Dead Space`, `Hollow Knight`, `The Witcher` on the original
  machine, where it was `F:\DWdemo`, `/mnt/f/DWdemo` from WSL). Their expected
  values are what Windows DiscWright 0.7.2 said about the same folders.
- **`tools/mutate.py`** breaks the code in ways a real mistake would and fails if
  any breakage gets past the suite. CI runs it. Add a mutation when adding a rule
  worth protecting.
- **`tools/wsl-test.sh`** runs the suite from WSL when the repo lives on the
  Windows side, with the virtual environment kept on the Linux side.

What WSL cannot show: a Linux desktop mounting the disc and displaying its name
and icon, because WSL has no desktop automounter. That needs a real Linux machine
or VM, once per change to the disc's identity files.

## Working rules

These are the owner's, carried over from the Windows project. Follow them here.

- **Test everything before a merge, and audit for gaps honestly.** When asked
  whether something is tested, list what the change touches and what proves each
  part. Prefer closing a gap over noting it. Say which claims rest on a local run
  and which on CI.
- **Prove a test can fail.** Break the code on purpose and watch the matching test
  fail before trusting a green run.
- **Say when the machine is in use and when it is free**, if a run takes over the
  desktop or runs long.
- **One PR at a time.** Merge, wait, update the next branch, then merge it.
  Merging two at once left one behind main.
- **Commits** end with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>` and
  nothing else: no session links. **PR descriptions** end with
  `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- **Keep the owner's real name out of anything public.** The published identity is
  the project's own: the winget publisher is `DiscWright`, not a person.
- **Thank people when crediting them**, in the same sentence as what they did, and
  link every mention of their handle with the platform named. Ask before naming
  anyone publicly.
- **Text written for the owner to send to other people** (Reddit, forums, email)
  uses no dashes between clauses, neither ` - ` nor `—`. Commas and full stops
  instead. It has to sound like him.
- **A release is not done until every place that names the version says so**,
  including the website.

## Where things stand

- Ported and tested against Windows output: `text.py` (control characters, volume
  id, ANSI encoding), `autorun.py`, `xdg.py`.
- `games.py` reads a GOG download folder: the installer, its parts, gaps in the
  part numbering, and the game's name. `pe.py` reads that name out of the
  installer's version resource with the standard library. Both agree with Windows
  on the four real GOG downloads. Matching is case-insensitive on purpose, since
  Windows gets that for free and Linux does not.
- Add-ons (DLC, patches, mods) are read by `add_on_info` in `games.py`. Any
  `.exe` is accepted, not just `setup_*`, and the name comes from the file name
  rather than the version resource, because every GOG patch reports the base
  game's name there. A patch is named by the version it moves **to**, at the
  front, so two patches of one game do not read the same on a clipped button.
  Agrees with Windows on all seven real GOG patches.
- Which game an add-on belongs to (`parent_index`) is set by the caller.
- `layout.py` holds the rules for where everything goes on the disc: the icon's
  name, reserved names at the root, numbered game folders, add-ons filed under
  their game, and the menu's view of all of it. Pure rules, no files touched.
  Paths on the disc are written with backslashes, because the menu runs on
  Windows. Name comparisons ignore case, because the disc is read on Windows too.
- **A Windows bug found while porting:** Windows DiscWright 0.7.2 cuts a game's
  folder name at 48 characters and re-trims only whitespace, so a cut landing on
  a dot leaves a trailing dot. Windows drops trailing dots from folder names
  silently, so the folder and the menu's path disagree and that game's Install
  button points at nothing. Measured. `layout.py` trims dots after the cut; the
  Windows app needs the same one-line fix in `Get-GameFolderName`.
- Next: the staging copy itself (from `Invoke-Build`), compared file by file
  against the folder Windows DiscWright staged for the same games.
- The ISO writer is decided: xorriso. Open items from the spike are listed at the
  end of `docs/xorriso-spike.md`.
- After staging: the project file
  (`discproject.json`, schema 8, shared with Windows), reading the game's name out
  of a GOG installer, icons, the menu background, and the menu itself.
