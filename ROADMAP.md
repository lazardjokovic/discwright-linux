# Roadmap

What is planned for DiscWright on Linux, what is being considered, and what is
deliberately out of scope. Ordered by intent rather than by date: this is a hobby
project and nothing here carries a delivery promise.

The Windows app has [its own roadmap](https://github.com/lazardjokovic/discwright/blob/main/ROADMAP.md),
and anything about the disc itself belongs there, because the disc is the same
one. What is here is about Linux: what a Linux machine can do with a disc, and
how this tool reaches the people who use one.

If what you want is missing, [open an issue](../../issues).

## Next

**The first release, once the app has been seen on a real Linux desktop.**
Everything the Windows app does to make a disc is ported and passes in WSL and
CI, but a desktop mounting a disc and showing its name and icon is the one thing
neither of those can show. `docs/testing-on-linux.md` is the list, and Kubuntu is
the machine. Nothing is tagged or released before that.

## Delivered

**The same disc as Windows, from Linux.** Every part of the Windows app's
disc-making, with the window and the command line: GOG downloads and their parts,
any folder of game files, add-ons filed under their game, the label and icon, the
menu byte for byte, `autorun.inf`, `.xdg-volume-info`, the ISO through xorriso,
and the project file, which opens on either machine.

## Considering

These are the two that decide **what a Linux machine can do with a disc**, rather
than what a Linux machine can make. Both are wanted only if the answer to "who is
this disc for" includes a Linux player, which is not yet established: the Windows
roadmap records the Linux request as asked for twice and never records why.
Asking the people who asked is the cheapest way to settle it, and it comes before
either of these is built.

- **GOG's Linux `.sh` installers, read as games.** GOG sells Linux builds for part
  of its catalogue as MojoSetup shell installers, `gog_some_game_1.2.3.sh`. Today
  one goes on a disc the way any other file does, through a folder of game files,
  and nothing knows it is an installer. Reading it as a game means detecting it
  the way `setup_*.exe` is detected, taking a name from it, and deciding what
  "install" means for a menu that does not run on Linux, which is the next item.

- **Something on the disc a Linux desktop can start.** The menu is an HTML
  Application, and `mshta` is Windows only, so a disc opened on Linux is a folder
  of files with a name and an icon. A Linux side would be **extra files on the
  same disc**, never a replacement: the menu has to stay byte for byte what
  Windows writes, or the two tools stop agreeing.

  The honest constraint is that Linux has no autorun, deliberately, so nothing
  will pop up when a disc goes in. The most it can be is something obvious to run
  once the disc is open, which is a smaller promise than the Windows menu makes
  and should be described as one.

- **Whatever KDE turns out to do with `.xdg-volume-info`.** GNOME reads it through
  gvfs. KDE mounts with Solid and may ignore it, in which case a disc shows its
  volume id and a generic icon there. The Kubuntu run answers this, and the answer
  decides whether there is anything to do.

- **Installing it without a clone.** Today it is git clone plus a virtual
  environment. PyPI would make it `pipx install discwright`; a Flatpak would carry
  GTK and Pillow with it and put it in the software centre. Worth doing once the
  first release exists, not before.

## Not planned

- **Burning.** DiscWright writes an ISO and stops, the same as on Windows. K3b,
  Brasero and `xorriso` itself all burn well, and there is no reason to write a
  worse one.
- **A different disc for Linux.** The disc is the Windows app's disc. A disc that
  behaves differently depending on which tool made it would be worse than either.
- **Anything that removes DRM.** GOG installers are DRM-free by design, which is
  the only reason a tool like this can exist.

## Not affiliated with GOG

An independent hobby project, **not affiliated with, endorsed by, or connected to
GOG.com, GOG sp. z o.o., or CD PROJEKT**. "GOG" is their trademark and is used
here only to describe what the tool reads.
