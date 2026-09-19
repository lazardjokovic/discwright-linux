import sys
from pathlib import Path

import pytest
from PIL import Image

from discwright.background import HEIGHT, PANEL_WIDTH, WIDTH, compose_background

FIX = Path(__file__).parent / "fixtures" / "background"

# The comparison tool doubles as the test's reader, so the test and the tool that
# chose its tolerances compose and compare the same way.
sys.path.insert(0, str(Path(__file__).parent.parent / "tools"))
from background_diff import (  # noqa: E402
    CASES, WINDOWS, compose, difference, half_pixel_lines, outside_title, title_ink, untitled, without,
)

# Measured with tools/background_diff.py against what Windows DiscWright 0.7.4
# composed from the same pictures, leaving out the title band and the lines
# Windows only half darkens (see half_pixel_lines):
#
#   a picture scaled down or not at all: mean at most 0.11, worst at most 5
#   a picture scaled up (narrow.png, 1.9x): mean at most 1.31, worst at most 79,
#   on the one-pixel lines, where two bicubic filters legitimately differ
#
# The worst-pixel limit is what catches a missing divider, which is three
# columns of a 760-column picture and hardly moves the mean. A reversed panel,
# a missing darkening or the wrong crop moves the mean by tens.
MEAN_LIMIT = 3.0
WORST_LIMIT = {"wide.png": 16, "narrow.png": 128}

# Where Windows' title ink sits, and how tall: the fonts differ (Bahnschrift is
# Windows only), so the title is held to its place and size, not its pixels.
PLACE_LIMIT = 2          # pixels, for the ink's top-left corner
HEIGHT_LIMIT = 0.15      # of the ink's height


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


@pytest.fixture(scope="module")
def made(tmp_path_factory):
    t = tmp_path_factory.mktemp("bg")
    return {n: load(compose(n, t / f"{n}.png")) for n in CASES}


@pytest.mark.parametrize("name", sorted(CASES))
def test_shows_what_the_windows_background_shows(made, name):
    src, _, side, _, _ = CASES[name]
    skip = half_pixel_lines(side)
    mean, worst = difference(without(outside_title(made[name]), skip),
                             without(outside_title(load(WINDOWS / f"{name}.png")), skip))
    assert mean <= MEAN_LIMIT and worst <= WORST_LIMIT[src], (mean, worst)


@pytest.mark.parametrize("name", [n for n, c in CASES.items() if c[4]])
def test_puts_the_title_where_windows_puts_it(made, name):
    plain = untitled(name)
    ours = title_ink(made[name], made[plain])
    theirs = title_ink(load(WINDOWS / f"{name}.png"), load(WINDOWS / f"{plain}.png"))
    assert ours and theirs
    assert abs(ours[0] - theirs[0]) <= PLACE_LIMIT and abs(ours[1] - theirs[1]) <= PLACE_LIMIT, (ours, theirs)
    h_ours, h_theirs = ours[3] - ours[1], theirs[3] - theirs[1]
    assert abs(h_ours - h_theirs) <= HEIGHT_LIMIT * h_theirs, (ours, theirs)


def test_is_the_size_of_the_menu(made):
    for im in made.values():
        assert im.size == (WIDTH, HEIGHT)


def test_draws_no_title_unless_asked(made):
    assert made["wide-right-no-title"].tobytes() == made["wide-right"].tobytes()


def test_draws_no_title_when_there_is_nothing_to_draw(tmp_path):
    compose_background(FIX / "wide.png", "   ", tmp_path / "a.png", show_title=True)
    compose_background(FIX / "wide.png", "", tmp_path / "b.png", show_title=False)
    assert load(tmp_path / "a.png").tobytes() == load(tmp_path / "b.png").tobytes()


# A title Windows DiscWright 0.7.4 draws past its room: at its 12pt minimum this
# one is 451px wide in 416px, so it runs under the panel on the right, or off the
# edge of the menu on the left.
LONG_TITLE = "Warhammer 40,000: Dawn of War - Game of the Year Edition"


@pytest.mark.parametrize("side", ["Right", "Left"])
def test_keeps_even_a_very_long_title_on_the_artwork(tmp_path, side):
    titled, plain = tmp_path / "t.png", tmp_path / "p.png"
    compose_background(FIX / "wide.png", LONG_TITLE, titled, side, show_title=True)
    compose_background(FIX / "wide.png", "", plain, side)
    ink = title_ink(load(titled), load(plain))
    assert ink is not None
    if side == "Right":
        assert ink[2] <= WIDTH - PANEL_WIDTH, ink
    else:
        assert ink[0] >= PANEL_WIDTH and ink[2] <= WIDTH - 1, ink


def test_draws_a_title_outside_plain_ascii(tmp_path):
    # GOG titles carry trademark signs and accents. A font without them would
    # draw empty boxes, which still count as ink, so this only proves the title
    # is drawn at all; the glyphs are for a person to judge.
    titled, plain = tmp_path / "t.png", tmp_path / "p.png"
    compose_background(FIX / "wide.png", "Über Alles™", titled, show_title=True)
    compose_background(FIX / "wide.png", "", plain)
    assert title_ink(load(titled), load(plain)) is not None


# ---- the edges, which Windows got wrong twice ------------------------------------

@pytest.mark.parametrize("w,h", [(1000, 480), (400, 300), (760, 480)],
                         ids=["overflows sideways", "overflows vertically", "fits exactly"])
def test_keeps_every_edge_opaque(tmp_path, w, h):
    # Windows 0.7.3 and earlier left the outermost pixels partly see-through.
    src = tmp_path / "src.png"
    Image.new("RGB", (w, h), (200, 120, 60)).save(src)
    compose_background(src, "", tmp_path / "bg.png")
    im = load(tmp_path / "bg.png")
    edges = ([(x, 0) for x in range(WIDTH)] + [(x, HEIGHT - 1) for x in range(WIDTH)]
             + [(0, y) for y in range(HEIGHT)] + [(WIDTH - 1, y) for y in range(HEIGHT)])
    assert all(im.getpixel(p)[3] == 255 for p in edges)


@pytest.mark.parametrize("side", ["Right", "Left"])
def test_darkens_the_first_row_and_column_as_much_as_the_rest(tmp_path, side):
    # Windows 0.7.4 and earlier darken the top row, the left column and the
    # panel's first column only half as much: a light line along the top of the
    # button panel on any bright picture. See half_pixel_lines.
    src = tmp_path / "src.png"
    Image.new("RGB", (760, 480), (220, 220, 220)).save(src)
    compose_background(src, "", tmp_path / "bg.png", side)
    im = load(tmp_path / "bg.png")
    for x in range(WIDTH):
        assert im.getpixel((x, 0)) == im.getpixel((x, 1)), x
    # Across a row the panel's gradient moves a level or two per column, so
    # neighbours are held to that; a half-darkened column is off by tens.
    step = lambda a, b: max(abs(i - j) for i, j in zip(a, b))  # noqa: E731
    assert step(im.getpixel((0, 240)), im.getpixel((1, 240))) <= 2
    # The panel ends or starts here: the column belongs wholly to one side, so
    # it matches one neighbour to within the gradient's step, not neither.
    start = PANEL_WIDTH if side == "Left" else WIDTH - PANEL_WIDTH
    here, before, after = (im.getpixel((start + d, 240)) for d in (0, -1, 1))
    assert min(step(here, before), step(here, after)) <= 2, (before, here, after)


def test_replaces_a_read_only_background_left_by_the_last_build(tmp_path):
    out = tmp_path / "bg.png"
    out.write_bytes(b"stale")
    out.chmod(0o444)
    compose_background(FIX / "wide.png", "", out)
    assert load(out).size == (WIDTH, HEIGHT)


def test_takes_a_jpeg_and_a_picture_with_transparency(tmp_path):
    jpg = tmp_path / "a.jpg"
    Image.open(FIX / "wide.png").convert("RGB").save(jpg, quality=90)
    compose_background(jpg, "", tmp_path / "a.png")
    pal = tmp_path / "p.png"
    Image.open(FIX / "wide.png").convert("P").save(pal, transparency=0)
    compose_background(pal, "", tmp_path / "p-out.png")
    assert load(tmp_path / "a.png").size == load(tmp_path / "p-out.png").size == (WIDTH, HEIGHT)
