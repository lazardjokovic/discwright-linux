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
- **The staging comparison** also needs `DISCWRIGHT_STAGE_DIR`: somewhere to
  stage on the same filesystem as the real installers, so they are hard-linked
  rather than copied. On the original machine the installers are on `C:` behind
  junctions in `F:\DWdemo`, so it was `/mnt/c/Users/lazar/AppData/Local/Temp/dwlinuxstage`.
  The reference is `F:\DWdemo\out\disc`, which Windows DiscWright staged from
  the settings in `F:\DWdemo\out\discproject.json`.
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
- **`main` is protected, the same as in the Windows repo.** Changes arrive only by
  a squash-merged PR, with both CI jobs (`pytest (3.10)`, `pytest (3.12)`)
  passing on a branch that is up to date with `main`. No force pushes, no
  deleting `main`, and `v*` release tags cannot be moved or deleted once pushed.
  If a CI job is renamed, the required check in the `main` ruleset has to be
  renamed with it, or every PR waits for a check that never reports.
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
- **A Windows bug found while porting, now fixed on both sides:** Windows
  DiscWright 0.7.2 cut a game's folder name at 48 characters and re-trimmed only
  whitespace, so a cut landing on a dot left a trailing dot. Windows drops
  trailing dots from folder names silently, so the folder and the menu's path
  disagreed and that game's Install button pointed at nothing. `layout.py` trims
  dots after the cut, and Windows DiscWright 0.7.3 does the same.
- **Porting can find bugs in the reference.** When the Windows app does something
  that cannot be right, measure it, fix it here, and fix it there too, as a
  separate PR in the Windows repo with a test that fails first. Do not copy a bug
  just because the reference has it.
- `stage.py` lays the disc out as a folder: installers hard-linked where
  possible, each entry's own manual and extras, the disc icons, the menu
  background, the disc-wide manual and extras, music, the menu, extra content
  behind the reserved-name guard, `autorun.inf` and `.xdg-volume-info`. An old
  disc folder is set aside, never wiped, because the build may be reading from
  it. It stages every one of the 12 files Windows staged for the Alan Wake demo:
  10 byte for byte and 2 as the same picture (`SAME_PICTURE` in
  `tests/test_stage.py`). The file lists must match exactly, so anything a later
  Windows version adds to the disc shows up there.
- One deliberate difference from Windows: settings that cannot be built (no
  icon, one that is not a readable image, a menu with no background or an
  unreadable one) are refused **before** anything is copied. Windows only finds
  out after the installers are in place.
- `icons.py` checks a picked icon or background, and makes the disc's two icons:
  a seven-frame `.ico` assembled by hand to match Windows' structure exactly
  (256px frame as PNG, the rest as 32-bit bitmaps), and a 256px PNG for Linux.
  Pillow is the first dependency. Bytes cannot match Windows', so tests check
  the structure exactly and the picture within a tolerance chosen by
  measurement (`tools/icon_diff.py`), ignoring the outermost pixel.
- **Two Windows icon bugs found while porting, both fixed in Windows 0.7.4:**
  the Linux PNG came out as noise (or failed, or blurred) for any icon with a
  256px PNG frame, which is most real game icons, and every icon frame had a
  see-through rim. The Alan Wake reference folder on the original machine was
  staged by 0.7.1, so `tests/test_stage.py` still compares the Linux PNG with
  the icon's own 256px frame; it can compare with the reference once that is
  restaged.
- `background.py` composes the menu background: the artwork scaled to cover
  760x480, darkened, a panel fading toward the buttons, an optional divider and
  an optional title. Tests compare it with what Windows 0.7.4 composed from the
  same two drawn pictures (`tests/fixtures/background`, made by
  `tools/background_sources.py` and
  `tools/windows/Make-BackgroundReference.ps1`), within tolerances measured by
  `tools/background_diff.py`. The title cannot match Windows' pixels: Windows
  draws it in Bahnschrift, which Linux does not have, so it is DejaVu Sans Bold
  here (or another common bold sans), placed by its ink where Windows' ink sits
  and held to the same height.
- **Two more Windows bugs found porting the background, both fixed in Windows
  0.7.5:**
  - **A light line along the top of the button panel.** GDI+ antialiases its
    rectangle fills with pixel centres on whole numbers, so a fill starting at 0
    covers half of pixel 0. The darkening and the panel are both drawn that
    way: the top row and left column get half the darkening, and so does the
    panel's first column. On bright artwork it shows as a one-pixel light line.
    `half_pixel_lines` in `tools/background_diff.py` names the lines; tests
    leave them out of the comparison, since the reference is 0.7.4's, and check
    this port darkens them fully. The divider's top pixel had the same defect.
  - **A long title runs off the menu.** Windows stops shrinking the title at
    12pt whether or not it fits, and nothing limits its length. "Warhammer
    40,000: Dawn of War - Game of the Year Edition" is 451px at 12pt with 416px
    of room: under the panel on the right, cut off at the menu's edge on the
    left. This port carries on down to 6pt when it has to.
- `menu.py` writes `AUTORUN/menu.hta`. The menu is **copied, not ported**:
  `src/discwright/menu.hta.in` is the Windows template, lifted verbatim out of
  `New-MenuHta` by `tools/menu_template.py`, and `menu.py` fills its
  placeholders with the same escaping. The output matches Windows byte for byte
  on every case in `tests/fixtures/menu/cases.json` (references written by
  `tools/windows/Make-MenuReference.ps1`) and on the real Alan Wake disc. When
  the Windows menu changes, rerun `tools/menu_template.py`, then the reference
  script, then the tests. The tests parse the menu's script with Node where it
  is installed (CI has it, WSL does not); on Windows, `cscript //E:JScript`
  parses it with the real engine.
- **A Windows menu bug found while porting, fixed in Windows 0.7.5:**
  `New-MenuHta` filled its placeholders with eleven chained replaces, so text
  already filled in was filled in again. A game named `Game %%BTNS%% Edition`
  got the button list pasted inside its string literal, and the whole menu
  failed to compile under JScript (measured). Unlikely, since GOG names never
  hold `%%`, but a game can be renamed to anything. Both sides fill them in one
  pass now. One Windows quirk **is** matched on purpose, because it is harmless and
  keeps the bytes equal: the window's application name keeps the Kelvin sign
  and the dotted capital I, which .NET's case-insensitive `[A-Za-z]` matches,
  and they reach the file as `?`.
- `iso.py` writes the ISO with xorriso, and `build.py` is the whole build:
  stage, then ISO, then the old disc folder goes. The command is
  `xorriso -as mkisofs -input-charset UTF-8 -iso-level 3 -J -joliet-long -r -V <id>`.
  Every flag there was measured, and `-input-charset UTF-8` is not optional: under
  a C locale, without it, xorriso silently wrote an accented name into the
  Windows names as underscores.
- **Windows reads this disc through Joliet, which holds less than a folder
  does, and xorriso cuts or changes a name it cannot hold without saying so.**
  So `name_problems` checks every name on the staged disc before anything is
  written, and the build stops naming each file: over 103 characters, a
  character Windows forbids or cannot store (emoji included), a trailing dot or
  space, a Windows device name, or two names differing only in capitals. The
  measurements behind each rule are in `docs/xorriso-spike.md`. Files over 4 GiB
  are allowed: Windows 11 read them back whole.
- **xorriso's own reader is not a witness for what Windows sees.** Asked for the
  Joliet tree it reported a name whole that the record on the disc held cut to
  64 characters. `tools/joliet.py` reads the records themselves, and the tests
  use it.
- The whole disc has been built end to end and compared with the Windows one:
  see the end of `docs/xorriso-spike.md`. Ten of the twelve files byte for byte,
  the other two the same pictures, same label, same name in Explorer.
- `cli.py` is the tool: `discwright build`, with one option per step of the
  Windows window. An add-on belongs to the `--game` named before it, which is
  why both go through one argparse action that keeps the order they were typed
  in. Everything that cannot be built is refused before a file is copied, each
  message naming what to do. `--stage-only` stops after the disc folder.
- `project.py` reads and writes `discproject.json`, schema 8, the same file the
  Windows app writes: a build saves one beside its ISO, and
  `discwright build --project FILE` rebuilds from it. Every older schema opens,
  and a key a file does not carry reads back as what that disc behaved like
  before the key existed, not as today's default. The file is UTF-8 **with** a
  byte order mark and CRLF, because without the mark PowerShell 5.1 reads it in
  the machine's ANSI codepage and mangles any path with an accent. Checked both
  ways: the tests read a real Windows file of schema 8 and one of schema 5, and
  `tools/windows/Read-Project.ps1` had the real `Import-Project` read a file
  written here, with every accent arriving intact.
- **A Windows bug found while porting, fixed in Windows 0.7.6:** a rebuild
  over an existing disc sets the old disc folder aside and rewrites every
  setting that pointed inside it, then deletes that folder once the ISO is
  written. `Save-Project` runs in between, so the project it saves names files
  in a folder that no longer exists: reopening it loses the icon or background
  picked from the disc being rebuilt. `build.py` points those paths back at the
  new disc folder, where the same files are, before saving.
- **The window is two files on purpose.** `form.py` holds every rule the window
  follows, with no GTK in it: what can be used right now and the sentence saying
  why not, what stops a build and what only needs a yes. `window.py` only draws
  it and passes clicks back, so the rules are tested without a screen. Where the
  Windows app leaves BUILD clickable and refuses in a dialog, this greys it out
  and says what is missing. `Test-ComposedBg` is in the window: picking a
  760x480 background ticks "use as it is".
- **The window's tests** (`tests/test_window.py`) draw it for real and skip
  without GTK 4 or a display. One walks every clickable widget and demands it be
  greyed out during a build, rather than asking the form: a widget nobody tied
  to a rule is exactly the one a list of rules misses. It found three on its
  first run: Add file..., Add folder... and the extra-content list.
  Dialogs are recorded in tests, not shown. CI runs them under `xvfb-run`.
  `tools/window_screenshots.py` draws the window in several states to PNGs,
  under Xvfb, without touching the desktop.
- WSL shows the window on the Windows desktop through WSLg: `discwright window`.
  Installing PyGObject into the venv (`pip install -e ".[dev,gui]"`) needs
  `libgirepository-2.0-dev libcairo2-dev pkg-config python3-dev` from apt.
- Next: a release. Nothing has been published for Linux yet: no package, no
  version number but `0.1.0.dev0`.
- Not ported, and probably never: the target-disc sizing (`Get-MediaFit` and the
  media tiers), which is a window's live recommendation rather than anything the
  disc carries.
