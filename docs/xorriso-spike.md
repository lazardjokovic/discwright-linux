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

## Measured afterwards: what Windows reads back

Measured on 2026-09-19 while writing `iso.py`, by building ISOs with xorriso 1.5.6
and mounting them on Windows 11. xorriso printed **no warning** for most of these:
it changed or cut the name and carried on.

| on the Linux side | what Windows 11 read |
| --- | --- |
| a file of 4,294,967,297 bytes, and one of 4.5 GiB | both whole, byte for byte |
| a file name of up to 104 characters | whole |
| a file name of 105 or more | cut to 104 |
| a folder name of up to 103 characters | whole |
| a folder name of 104 or more | cut to 103 |
| `:` `*` `?` `\` in a name | replaced with `_` |
| `"` `<` `>` in a name | kept, and Windows cannot open the file |
| `\|` in a name | kept, and it opened |
| a trailing dot | dropped |
| a trailing space | kept |
| an emoji, or anything else outside UCS-2 | replaced (xorriso warns, and exits 5) |
| `Same.txt` and `same.txt` in one folder | both names open the first file |
| ten folders deep, and a 180-character path | fine |
| `Café` under a C locale, without `-input-charset UTF-8` | written as `Caf__` |

So:

- **Files over 4 GiB are allowed.** `-iso-level 3` stores them in several extents
  and Windows reads them whole. Windows XP has not been tried.
- **Every name is checked before anything is written** (`name_problems` in
  `iso.py`), and the build stops naming each file Windows would not get back as it
  is. A manual renamed on the way is a Manual button that opens nothing. The limit
  is 103 for files and folders alike; Windows' own device names (CON, NUL, COM1...)
  are refused too, from Windows' documented rules rather than a measurement.
- **`-input-charset UTF-8` is always passed.** Without it the result depends on the
  locale the tool happens to run under.
- **xorriso's own reader is not a witness for Joliet.** Asked to read the Joliet
  tree (`-rockridge off -joliet on`), it showed a name whole that the Joliet
  record on the disc held cut to 64 characters. `tools/joliet.py` reads the
  records themselves, and the tests use it.

## The whole disc, end to end

The Linux tool staged the Alan Wake demo and wrote its ISO (7.79 GB, 15 minutes
through WSL's view of the Windows drive), and `tools/windows/Compare-Isos.ps1`
compared it with the ISO Windows DiscWright 0.7.1 built from the same settings:

| | built on Windows | built on Linux |
| --- | --- | --- |
| Windows mounts it as | UDF | CDFS |
| volume label | ALAN_WAKE | ALAN_WAKE |
| name Explorer shows for the drive | DVD Drive (E:) ALAN WAKE | DVD Drive (E:) ALAN WAKE |
| files | 12 | 12 |

Ten files byte for byte, including every installer part, the menu and
`autorun.inf`. The two that differ are pictures: `ALANWAKE.png`, which 0.7.1 wrote
as noise (fixed in 0.7.4), and `AUTORUN/bg.png`, whose title is drawn in a
different font.

## Still to check

- **The icon and the menu, by hand.** Explorer read the label, so it read
  `autorun.inf`. Neither the icon being drawn nor a double-click opening the menu
  was looked at. Check both once on a Linux-built ISO.
- **A Linux desktop.** WSL has no desktop automounter, so `.xdg-volume-info` being
  shown as the drive's name and icon has not been seen here. It is byte-identical to
  the one Windows writes, which has been.
- **Windows XP** reading a Linux-built disc, and a file over 4 GiB on it.
