# Can xorriso build the disc Windows DiscWright builds?

**Yes, as far as Windows can tell.** Measured on 2026-09-18, before any of the
Linux tool's ISO code was written, because the answer decides what that code is.

## The question

Windows DiscWright builds its ISO with IMAPI, the image writer built into Windows,
and writes UDF 2.50. Linux has no IMAPI. The usual Linux tool, xorriso, writes
ISO9660 with Joliet and Rock Ridge rather than UDF. If Windows treated such a disc
differently (no icon, no label, no menu, or a file it could not read), the Linux
tool would need a different writer before it could promise the same disc.

## What was done

One staged disc folder, written by Windows DiscWright 0.7.2 for the Alan Wake demo:
the game's three installer parts, the `AUTORUN` folder with the menu, `Extras`,
`autorun.inf`, the icon, and the Linux name files. The largest file is
4,294,040,574 bytes, the part that made IMAPI refuse the "readable on Windows XP
and older" option in 0.7.2, because IMAPI caps ISO9660 files at 2 GiB.

That same folder became two ISOs. One is the ISO Windows DiscWright built. The
other came from xorriso 1.5.6 on Ubuntu 24.04 under WSL2:

```sh
xorriso -as mkisofs -iso-level 3 -J -joliet-long -r -V ALAN_WAKE \
        -o alanwake-linux.iso out/disc
```

Both were mounted on Windows 11, every file on each was hashed, and each disc was
read the way Windows reads it. The script is `tools/windows/Compare-Isos.ps1`.

## Result

| | built on Windows (IMAPI) | built on Linux (xorriso) |
| --- | --- | --- |
| filesystems, from the volume recognition sequence | UDF (BEA01 NSR03 TEA01) | ISO9660 (CD001) |
| Windows mounts it as | UDF | CDFS |
| volume label | ALAN_WAKE | ALAN_WAKE |
| name Explorer shows for the drive | DVD Drive (E:) ALAN WAKE | DVD Drive (E:) ALAN WAKE |
| files | 12 | 12 |

**Every file identical, byte for byte**, including the 4,294,040,574-byte part.

Explorer showing `ALAN WAKE`, with the space, is the proof Windows read the Linux
disc's `autorun.inf`: the volume id alone would read `ALAN_WAKE`.

## What it means

- **xorriso is the writer.** No need for mkudffs, loop mounts or root.
- **The Linux disc reaches further back than the Windows one.** It is ISO9660,
  which Windows XP and older read, and it holds a 4 GiB GOG installer part, which
  IMAPI will not put into ISO9660. So a Linux-built disc of a big GOG game can be
  readable on XP, where the Windows tool now has to say no.
- **It is not bit-identical at the filesystem level**, and does not need to be.
  "The same disc" means the same files, the same identity and the same behaviour
  on both systems. That is what was measured.

## Still to check

- **The icon and the menu.** Explorer read the label, so it read `autorun.inf`,
  and the icon it names is on the disc byte-identical. Neither the icon being drawn
  nor a double-click opening the menu was looked at. Check both once, by hand.
- **Files over 4 GiB.** `-iso-level 3` splits a larger file into several extents.
  No GOG installer part needs that, since GOG splits one byte under 4 GiB, but a
  video dropped into the disc's extra content could. Whether Windows' CDFS driver
  reads a multi-extent file needs measuring before the tool allows one.
- **A Linux desktop.** WSL has no desktop automounter, so `.xdg-volume-info` being
  shown as the drive's name and icon has not been seen here. It is byte-identical to
  the one Windows writes, which has been.
