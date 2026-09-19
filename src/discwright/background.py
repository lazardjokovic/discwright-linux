"""The menu background: the picked artwork, cropped to the menu's 760x480 and
darkened under the buttons, with the game's title on it if asked for.

Ported from New-Background in DiscWright.ps1.

Like the icons, the bytes cannot match Windows', since two libraries resample
and compress the same picture differently. The title cannot match at all: the
Windows app draws it in Bahnschrift, a font only Windows has. Tests compare the
picture within a measured tolerance, and the title by where it sits and how big
it is.
"""

from __future__ import annotations

import io
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from .icons import _write

WIDTH, HEIGHT = 760, 480
PANEL_WIDTH = 290

# The panel behind the buttons: darkest where the buttons are, fading toward the
# artwork. Colours as the Windows app has them, alpha first there.
PANEL_NEAR = (2, 5, 7, 225)
PANEL_FAR = (3, 8, 10, 120)
DIVIDER = (0, 190, 200, 200)
DARKEN = (0, 0, 0, 70)
TITLE = (235, 245, 248, 235)
TITLE_SHADOW = (0, 0, 0, 180)

# The Windows app sizes the title in points, starting at 30 and giving up at 12,
# on a screen of 96 pixels to the inch. Giving up there is a Windows bug: a title
# still too long at 12pt is drawn anyway, under the panel or off the edge of the
# menu, and GOG titles that long exist ("Warhammer 40,000: Dawn of War - Game of
# the Year Edition" is 451px at 12pt, with 416px of room). Here 12pt is where the
# shrinking would stop if the title fits, and it carries on down to 6pt if not.
TITLE_START_PT = 30.0
TITLE_FLOOR_PT = 6.0
TITLE_STEP_PT = 1.5
PX_PER_PT = 96 / 72

# Bahnschrift is not on Linux. These are bold sans faces one of which nearly
# every distribution installs; Pillow finds them by file name under the system's
# font folders.
TITLE_FONTS = ("DejaVuSans-Bold.ttf", "LiberationSans-Bold.ttf", "NotoSans-Bold.ttf",
               "FreeSansBold.ttf")


def _title_font(px: float) -> ImageFont.ImageFont | ImageFont.FreeTypeFont:
    for name in TITLE_FONTS:
        try:
            return ImageFont.truetype(name, round(px))
        except OSError:
            continue
    try:
        return ImageFont.load_default(round(px))   # Pillow 10.1 and later
    except TypeError:
        return ImageFont.load_default()


def compose_background(img_path: str | Path, title: str, out_png: str | Path,
                       panel_side: str = "Right", divider: bool = False,
                       show_title: bool = False) -> None:
    """Compose the menu background from any picture. Ported from New-Background.

    panel_side is "Right" (the default) or "Left": which edge the button column
    sits on. Pick the side opposite the focal point of the artwork, or the
    buttons cover it.
    """
    left = panel_side.casefold() == "left"
    px = 0 if left else WIDTH - PANEL_WIDTH           # panel x
    dx = PANEL_WIDTH if left else WIDTH - PANEL_WIDTH  # divider x

    with Image.open(img_path) as src:
        src.load()
        art = src.convert("RGBA")

    # Scaled to cover the whole menu and centred, so it overflows on one axis
    # and is cropped there. Rounded the way PowerShell's [int] rounds: to even
    # on a half. Pillow clamps at the picture's edge while resampling, so it has
    # no see-through rim for Windows' TileFlipXY to fix.
    scale = max(WIDTH / art.width, HEIGHT / art.height)
    sw, sh = round(art.width * scale), round(art.height * scale)
    ox, oy = round((WIDTH - sw) / 2), round((HEIGHT - sh) / 2)
    art = art.resize((sw, sh), Image.Resampling.BICUBIC, reducing_gap=3.0)
    canvas = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    canvas.alpha_composite(art.crop((-ox, -oy, -ox + WIDTH, -oy + HEIGHT)))

    canvas.alpha_composite(Image.new("RGBA", (WIDTH, HEIGHT), DARKEN))

    # Darkest under the buttons, fading toward the divider.
    c1, c2 = (PANEL_NEAR, PANEL_FAR) if left else (PANEL_FAR, PANEL_NEAR)
    band = Image.new("RGBA", (PANEL_WIDTH, 1))
    for i in range(PANEL_WIDTH):
        t = (i + 0.5) / PANEL_WIDTH
        band.putpixel((i, 0), tuple(round(a + (b - a) * t) for a, b in zip(c1, c2)))
    canvas.alpha_composite(band.resize((PANEL_WIDTH, HEIGHT), Image.Resampling.NEAREST), (px, 0))

    # The divider reads as a hard line drawn across the artwork; off by default.
    # A 2px antialiased pen centred on the panel's edge, as Windows draws it: the
    # middle column full strength, the one either side half.
    if divider:
        r, g, b, a = DIVIDER
        line = Image.new("RGBA", (3, HEIGHT))
        for i, strength in enumerate((a // 2, a, a // 2)):
            line.paste((r, g, b, strength), (i, 0, i + 1, HEIGHT))
        canvas.alpha_composite(line, (dx - 1, 0))

    # Title on the artwork is off by default: cover art usually carries the
    # game's own logo already, and a second title drawn over it just fights the
    # artwork.
    if show_title and title and title.strip():
        _draw_title(canvas, title, left)

    # Encoded in memory first, so a failure never leaves half a picture on the
    # disc, then written the way every other file is, over a read-only one left
    # by the last build.
    buf = io.BytesIO()
    canvas.save(buf, "PNG")
    _write(Path(out_png), buf.getvalue())


def _draw_title(canvas: Image.Image, title: str, left: bool) -> None:
    """The title on the artwork side, shrunk until it fits.

    Placed by its ink rather than by the font's metrics, because the fonts differ:
    the ink's top-left corner lands where Windows' does, measured off its output
    at a sixth of the font size right of the text origin and a tenth below it.
    The fit test is Windows' too: GDI+ measures a string with a sixth of the font
    size of padding at each end, so the ink must fit in the space less a third.

    The Linux fonts are wider than Bahnschrift, by about a fifth, so a long title
    comes out a step or two smaller here than on Windows.
    """
    tx = PANEL_WIDTH + 40 if left else 27
    ty = 27
    max_w = WIDTH - PANEL_WIDTH - 54
    size = TITLE_START_PT
    while True:
        em = size * PX_PER_PT
        font = _title_font(em)
        box = ImageDraw.Draw(canvas).textbbox((0, 0), title, font=font)
        fits = (box[2] - box[0]) + em / 3 <= max_w
        if fits or size <= TITLE_FLOOR_PT:
            break
        size -= TITLE_STEP_PT
    x = tx + round(em / 6) - box[0]
    y = ty + round(em / 10) - box[1]
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.text((x + 2, y + 2), title, font=font, fill=TITLE_SHADOW)
    canvas.alpha_composite(layer)
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).text((x, y), title, font=font, fill=TITLE)
    canvas.alpha_composite(layer)
