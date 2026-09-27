# Testing on a real Linux machine

Everything up to now ran in WSL on the Windows machine, where the Windows app
and its reference discs live. This is the list for a real Linux install on the
same PC, before the first release. **Not a release: report what you find, and
change nothing about versions or tags.**

Written for Arch, which is no longer on the machine; **Kubuntu** is what goes on
next, so the install below has a block for each. Kubuntu also answers a question
Arch would not have: it is KDE, and KDE's file manager may ignore
`.xdg-volume-info`, which is the file that puts the disc's name on the desktop.

What only a real Linux desktop can show, and WSL never could:

- a Linux desktop mounting a DiscWright disc and showing its **name and icon**
  from `.xdg-volume-info` (WSL has no automounter)
- a fresh install on a distro other than Ubuntu, with a newer Python and Pillow
  than CI tests
- the window on the desktop's own GTK and file dialogs, not WSLg's
- the title font on a system that may not have DejaVu installed

## 1. Install

This is the developer's install, with the tests. For the ordinary one, the
README's **Installing it** is the tested recipe.

**Kubuntu, Ubuntu, Debian**

```sh
sudo apt install git python3-venv python3-gi gir1.2-gtk-4.0 python3-pil xorriso \
                 fonts-dejavu-core nodejs gh
git clone https://github.com/lazardjokovic/discwright-linux
cd discwright-linux
python3 -m venv --system-site-packages .venv     # the distro's Pillow and PyGObject
.venv/bin/pip install -e ".[dev]"
```

**Arch**

```sh
sudo pacman -S --needed git python python-pillow python-gobject gtk4 xorriso \
                        ttf-dejavu nodejs github-cli
git clone https://github.com/lazardjokovic/discwright-linux
cd discwright-linux
python -m venv --system-site-packages .venv
.venv/bin/pip install -e ".[dev]"
```

`--system-site-packages` because the distribution's PyGObject is already built
against its GTK; building PyGObject from pip needs its headers, which are more to
install for no gain. Get this wrong and the window reports GTK missing on a
machine that has it. The DejaVu font is what the menu's title is drawn in here,
since Windows draws it in Bahnschrift and Linux has no such font. `nodejs` lets
the five tests that parse the menu's script run; `gh` is only for opening pull
requests (`gh auth login` once).

Note what Python and Pillow this is (`.venv/bin/python -V`,
`.venv/bin/python -c "import PIL; print(PIL.__version__)"`): CI tests 3.10 and
3.12, so a newer Python here is new ground.

## 2. The suite, without the real games

```sh
.venv/bin/python -m pytest -q -rs
```

Everything should pass or skip. The skips should be only the real-data tests,
which need the next step. The window's tests draw on the real display.

**Or run the lot in one go**, which also writes down what this machine is, so
the answers arrive together rather than as remembered impressions:

```sh
tools/linux-report.sh              # the machine, the suite, and a disc built end to end
tools/linux-report.sh --mutate     # and the mutation check, about twenty minutes
```

It writes `~/discwright-report-<host>-<date>.txt`, and leaves a real ISO in
`~/discwright-check/out/` for the checks further down that need eyes. It reports
rather than judges: a missing `node` or `desktop-file-validate` is written down,
not treated as a failure.

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
So build a folder of symlinks with the names the tests expect. Open both
partitions once in Dolphin and it mounts them, usually under
`/media/$USER/<label>` or `/run/media/$USER/<uuid>`; `lsblk -o NAME,LABEL,MOUNTPOINT`
says where they went. With those two paths in `$WINC` and `$WINF`:

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
5. **A folder of game files**, which is what 0.8.0 added and no Linux desktop has
   seen. **Add game...** and pick a folder that is not a GOG download: an
   installed game works, and the Windows side was proven against one of 4.9 GB.
   The question should appear, listing that folder's executables largest first,
   with *No installer* already chosen and the file count and size underneath.
   - Leave it on *No installer* and add it. Build. On the disc, the game's
     subfolders should be there as they were, and `AUTORUN/menu.hta` should
     carry **Open Folder**.
   - Point it at the folder holding your GOG downloads instead, if you have one
     on this machine. It should warn, in orange with a warning icon, that
     downloads sit in subfolders and name the first, and **Cancel** should add
     nothing.
6. **The application menu.** Install the entry:

   ```sh
   packaging/desktop/install.sh .venv/bin/discwright
   ```

   Then look for DiscWright in KDE's application menu. The icon should be the
   real one, not a generic cog, and clicking it should open the window. While it
   is open, check the task manager entry: it should carry the same icon and name,
   which is what `StartupWMClass` is for, and is the part most likely to be
   wrong on a desktop other than the one it was written on.
   `packaging/desktop/install.sh --uninstall` takes it away again.

Then, **back on Windows**, the Linux-built ISO:

7. Mount it. **This PC** should show the game's icon and **ALAN WAKE**.
8. Double-click the drive. The menu should open; Install and Manual should work.
9. If the disc from check 5 came across too, mount that one and press **Open
   Folder**: Explorer should open that game's folder on the disc.

An ISO built in WSL from the same settings is already waiting for checks 7 and
8: `F:\DWdemo\linux-built\ALAN WAKE.iso`.

## 5. What KDE may do differently

Kubuntu is KDE, and everything above was written on GNOME's assumptions. Three
places where the answer may simply be different, and different is a fact to
write down rather than a bug to fix in a hurry:

- **`.xdg-volume-info`.** GNOME reads it through gvfs. KDE mounts with Solid and
  may ignore the file, in which case the disc shows its volume id (`ALAN_WAKE`)
  and a generic icon. If so, say so: the disc is still correct for Windows and
  for GNOME, and what KDE wants instead is worth finding out before anything is
  changed.
- **File dialogs.** A GTK 4 application on KDE goes through the desktop portal,
  so **Add game...** may open Plasma's file dialog rather than GTK's. That it
  opens at all, and returns the folder picked, is the check.
- **The window's own look.** GTK 4 on Plasma uses whatever GTK theme is set, not
  Breeze, so it may look out of place. Worth a screenshot, not worth a fix.

## 6. Report

Attach the report `tools/linux-report.sh` wrote, then add what only a person
saw: what the desktop showed for the disc, whether the menu entry appeared with
its icon, and what the folder question looked like. Screenshots beat sentences
for all three.

Write the results into `docs/xorriso-spike.md`, under "Still to check", as
measured facts: what was run, on what (distro, desktop, Python, Pillow, GTK),
and what happened. Fix anything that fails the way the rest of this repo is
fixed: a test that fails first, then the fix, in a pull request. The release
waits until the owner has seen the results.
