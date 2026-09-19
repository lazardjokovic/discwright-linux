# DiscWright for Linux

Turn a GOG offline installer into a real game disc, on Linux: the game's own icon
and title when the disc goes into a Windows PC, a menu on double-click, and the
game's name and icon on a Linux desktop.

This is the Linux version of [DiscWright](https://github.com/lazardjokovic/discwright),
which does the same on Windows. It builds **the same disc**, so a disc made on
either machine works on both.

## Status

**Early, and not usable yet.** What exists is the part of the disc that has been
proven to match the Windows version byte for byte:

- the volume id and the text rules every file on the disc shares
- `autorun.inf`, which gives Windows the disc's icon, label and menu
- `.xdg-volume-info`, which gives a Linux desktop the disc's name and icon
- reading a GOG download folder: the installer, its parts, and the game's name
- reading add-ons: DLC, expansions, GOG patches and mods
- the disc layout: where every game, add-on, manual and extra goes
- staging: laying the disc out as a folder, ready to become an ISO
- icons: the disc icon from any picture, and the PNG a Linux desktop shows
- the menu background: the artwork cropped to the menu, with its panel and title
- the menu: the same one Windows puts on the disc, byte for byte
- the ISO itself, written with xorriso, which Windows reads as the same disc

Needs Python 3.10 or newer and Pillow.

A whole disc has been built and compared with one Windows built from the same
settings: same label, same name in Explorer, ten of its twelve files byte for byte
and the other two the same pictures. See [docs/xorriso-spike.md](docs/xorriso-spike.md).

Next is the command line that ties it together, and a window after that. Follow
along in the commits, or use the Windows version today.

## Developing

```sh
python3 -m venv ~/.venvs/discwright
~/.venvs/discwright/bin/pip install -e ".[dev]"
~/.venvs/discwright/bin/python -m pytest
```

Building an ISO needs `xorriso` (`sudo apt install xorriso`).

## License

MIT
