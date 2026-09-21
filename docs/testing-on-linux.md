# Testing on a real Linux machine

Everything up to now ran in WSL on the Windows machine, where the Windows app
and its reference discs live. This is the list for a real Linux install (Arch,
on the same PC, dual booted), before the first release. **Not a release: report
what you find, and change nothing about versions or tags.**

What only a real Linux desktop can show, and WSL never could:

- a Linux desktop mounting a DiscWright disc and showing its **name and icon**
  from `.xdg-volume-info` (WSL has no automounter)
- a fresh install on a distro other than Ubuntu, with a newer Python and Pillow
  than CI tests
- the window on the desktop's own GTK and file dialogs, not WSLg's
- the title font on a system that may not have DejaVu installed

## 1. Install

```sh
sudo pacman -S --needed git python python-pillow python-gobject gtk4 xorriso \
                        ttf-dejavu nodejs github-cli
git clone https://github.com/lazardjokovic/discwright-linux
cd discwright-linux
python -m venv --system-site-packages .venv      # Arch's own Pillow and PyGObject
.venv/bin/pip install -e ".[dev]"
```

`--system-site-packages` because Arch's PyGObject is already built against its
GTK; building PyGObject from pip needs its headers, which are more to install
for no gain. `nodejs` lets the five tests that parse the menu's script run;
`github-cli` is only for opening pull requests (`gh auth login` once).

Note what Python and Pillow this is (`.venv/bin/python -V`,
`.venv/bin/python -c "import PIL; print(PIL.__version__)"`): CI tests 3.10 and
3.12, so a newer Python here is new ground.

## 2. The suite, without the real games

```sh
.venv/bin/python -m pytest -q -rs
```

Everything should pass or skip. The skips should be only the real-data tests,
which need the next step. The window's tests draw on the real display.

## 3. The suite, with the real games

The GOG downloads are on the Windows partitions. On Windows they were reached
through `F:\DWdemo`, but four of its folders are **junctions** to `C:`, and a
Linux NTFS driver does not follow those. The real folders are:

| name the tests expect | where it really is, on the Windows `C:` partition |
| --- | --- |
| `Alan Wake` | `Program Files (x86)/GOG Galaxy/Games/Offline Installers/alan_wake` |
| `Dead Space` | `.../Offline Installers/dead_space` |
| `Hollow Knight` | `.../Offline Installers/hollow_knight` |
| `The Witcher` | `.../Offline Installers/the_witcher` |

`artwork`, `media` and `out` are real folders in `DWdemo` on the `F:` partition.
So build a folder of symlinks with the names the tests expect. With `C` and `F`
mounted at `$WINC` and `$WINF` (wherever they mount here):

```sh
GOG="$WINC/Program Files (x86)/GOG Galaxy/Games/Offline Installers"
mkdir -p ~/dwdemo && cd ~/dwdemo
ln -sfn "$GOG/alan_wake"     "Alan Wake"
ln -sfn "$GOG/dead_space"    "Dead Space"
ln -sfn "$GOG/hollow_knight" "Hollow Knight"
ln -sfn "$GOG/the_witcher"   "The Witcher"
ln -sfn "$WINF/DWdemo/artwork" artwork
ln -sfn "$WINF/DWdemo/media"   media
ln -sfn "$WINF/DWdemo/out"     out
cd -
```

The staging comparison also needs somewhere to stage on the **same filesystem as
the installers**, so they are hard-linked rather than copied:

```sh
DISCWRIGHT_GOG_DIR=~/dwdemo DISCWRIGHT_STAGE_DIR="$WINC/dwlinuxstage" \
    .venv/bin/python -m pytest -q -rs
```

Two things that may get in the way:

- **Windows Fast Startup** leaves NTFS partitions hibernated, and Linux then
  mounts them read-only. If the staging directory cannot be written, either turn
  Fast Startup off in Windows, or stage on the Linux disk instead: the installers
  are then copied rather than linked (about 8 GB for Alan Wake), slower but just
  as correct.
- **Hard links on NTFS.** If the kernel's NTFS driver refuses them, staging falls
  back to copying on its own. Worth noting which happened: the log says
  `linked` or `copying` for each installer.

Then the mutation check, which breaks the code on purpose and needs every
break caught:

```sh
.venv/bin/python tools/mutate.py
```

## 4. By hand: the checks only a person can make

Build the real Alan Wake disc from the window, the way somebody would:

```sh
.venv/bin/discwright window
```

Add `~/dwdemo/Alan Wake`, the icon `~/dwdemo/artwork/alanwake-icon.ico`, the
background `~/dwdemo/artwork/alanwake-background.jpg`, the manual from
`~/dwdemo/media/AlanWake_manual/...`, the extras folder
`~/dwdemo/media/DiscExtras`, label `ALAN WAKE`, an output folder, and leave
**Name the disc on Linux too** ticked. Build.

Look for, and write down what actually happens:

1. **The window.** Do the file dialogs open (they are the desktop's own)? Does
   greying out behave, with tooltips saying why? Do progress and the log move
   during the build, and does it end in a dialog naming the ISO?
2. **The menu background's title**, if ticked: `AUTORUN/bg.png` in the disc
   folder. Is the title drawn in a real bold face, or in Pillow's small built-in
   font? The second means none of DejaVu, Liberation, Noto or FreeSans bold was
   found.
3. **The disc on this desktop.** Open the ISO in the file manager, or
   `udisksctl loop-setup -f "ALAN WAKE.iso"`. The drive should show **ALAN WAKE**
   with the game's icon, not a volume id like `ALAN_WAKE` and a generic disc.
   This is the check nothing has ever made. Note the desktop: `.xdg-volume-info`
   is read by GNOME's gvfs, and KDE may ignore it entirely. If it does, that is
   something to document, not a bug in the disc.
4. **Rebuild from the project**: `discwright build --project <out>/discproject.json`
   rebuilds the same disc from the command line.

Then, **back on Windows**, the Linux-built ISO:

5. Mount it. **This PC** should show the game's icon and **ALAN WAKE**.
6. Double-click the drive. The menu should open; Install and Manual should work.

An ISO built in WSL from the same settings is already waiting for checks 5 and
6: `F:\DWdemo\linux-built\ALAN WAKE.iso`.

## 5. Report

Write the results into `docs/xorriso-spike.md`, under "Still to check", as
measured facts: what was run, on what (distro, desktop, Python, Pillow, GTK),
and what happened. Fix anything that fails the way the rest of this repo is
fixed: a test that fails first, then the fix, in a pull request. The release
waits until the owner has seen the results.
