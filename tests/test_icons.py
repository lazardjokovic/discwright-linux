import io
import shutil
import struct
import sys
from pathlib import Path

import pytest
from PIL import Image, ImageChops

from discwright.icons import ICO_SIZES, check_background, check_icon, convert_to_ico, convert_to_png

FIX = Path(__file__).parent / "fixtures" / "icons"
WINDOWS = FIX / "windows-0.7.3"
SOURCE = FIX / "source.png"      # 400x300: gradient, a disc, a black square, a half-transparent band

# The comparison tools double as the test's reader, so the test and the tool that
# chose its tolerance read an .ico the same way.
sys.path.insert(0, str(Path(__file__).parent.parent / "tools"))
from icon_diff import ico_frames  # noqa: E402

# Measured with tools/icon_diff.py against what Windows DiscWright 0.7.3 made from
# the same source, inside the outermost pixel: mean at most 1.8, worst at most 26,
# on the hard edge of the black square, where two resampling filters legitimately
# differ. The margin is generous; a wrong crop, the wrong frame or swapped colour
# channels moves the mean by tens (tools/mutate.py checks exactly that).
MEAN_LIMIT = 3.0
WORST_LIMIT = 40


def inner_difference(a: Image.Image, b: Image.Image) -> tuple[float, int]:
    # The outermost pixel is left out on purpose: Windows' frames are see-through
    # there (see test_keeps_the_edges_opaque_where_the_source_is), so comparing it
    # would test Windows' defect rather than this code.
    box = (1, 1, a.width - 1, a.height - 1)
    flat = ImageChops.difference(a.crop(box), b.crop(box)).tobytes()
    return sum(flat) / len(flat), max(flat)


@pytest.fixture
def made(tmp_path):
    ico, png = tmp_path / "ours.ico", tmp_path / "ours.png"
    convert_to_ico(SOURCE, ico)
    convert_to_png(SOURCE, png)
    return ico, png


# ---- the .ico's structure, exactly as Windows writes it ------------------------

def directory(path: Path) -> list[tuple]:
    data = path.read_bytes()
    reserved, kind, count = struct.unpack_from("<HHH", data, 0)
    assert (reserved, kind) == (0, 1)
    out = []
    for i in range(count):
        w, h, colours, res, planes, bpp, size, offset = struct.unpack_from("<BBBBHHII", data, 6 + 16 * i)
        blob = data[offset:offset + size]
        out.append((w, h, colours, res, planes, bpp, blob[:8] == b"\x89PNG\r\n\x1a\n",
                    None if blob[:8] == b"\x89PNG\r\n\x1a\n" else (size, blob[:40])))
    return out


def test_has_the_same_seven_frames_as_the_windows_icon(made):
    ico, _ = made
    ours, theirs = directory(ico), directory(WINDOWS / "source.ico")
    # Everything but the PNG frame's compressed size and bytes: dimensions,
    # colour count, planes, bit depth, which frame is a PNG, and for every bitmap
    # frame its exact length and 40-byte header.
    assert ours == theirs
    assert [d[0] or 256 for d in ours] == list(ICO_SIZES)


def test_stores_only_the_256px_frame_as_a_png(made):
    ico, _ = made
    assert [d[6] for d in directory(ico)] == [False] * 6 + [True]


# ---- the pictures, against Windows' ------------------------------------------------

@pytest.mark.parametrize("size", ICO_SIZES)
def test_every_frame_shows_what_the_windows_frame_shows(made, size):
    ico, _ = made
    mean, worst = inner_difference(ico_frames(ico)[size], ico_frames(WINDOWS / "source.ico")[size])
    assert mean <= MEAN_LIMIT and worst <= WORST_LIMIT, (mean, worst)


def test_the_linux_png_shows_what_the_windows_one_shows(made):
    _, png = made
    ours = Image.open(png)
    assert ours.size == (256, 256) and ours.mode == "RGBA"
    mean, worst = inner_difference(ours, Image.open(WINDOWS / "source.png").convert("RGBA"))
    assert mean <= MEAN_LIMIT and worst <= WORST_LIMIT, (mean, worst)


def test_keeps_the_edges_opaque_where_the_source_is(made):
    # Beyond Windows. GDI+ samples past the edge of the source while scaling and
    # blends with transparency, so every edge pixel of Windows' frames is partly
    # see-through: a faint rim round every icon. The source is opaque everywhere
    # but a band across the top, so the other three edges must be too.
    ico, png = made
    for im in [*ico_frames(ico).values(), Image.open(png).convert("RGBA")]:
        a = im.getchannel("A")
        w, h = im.size
        below_band = range(h // 5, h)
        edges = ([a.getpixel((0, y)) for y in below_band] + [a.getpixel((w - 1, y)) for y in below_band]
                 + [a.getpixel((x, h - 1)) for x in range(w)])
        assert min(edges) == 255, im.size


def test_crops_a_wide_picture_from_its_middle(made):
    # The source's black square sits in the middle of a 400x300 picture, so a
    # centred square crop puts it in the middle of the icon too.
    _, png = made
    im = Image.open(png).convert("RGB")
    assert sum(im.getpixel((128, 128))) < 90          # the black square
    assert sum(im.getpixel((20, 128))) > 150           # the gradient beside it


def test_takes_the_largest_frame_of_an_ico_source(tmp_path):
    # An .ico opened carelessly yields a small frame and a blurry 256px PNG.
    png = tmp_path / "from-ico.png"
    convert_to_png(WINDOWS / "source.ico", png)
    from_ico = Image.open(png).convert("RGBA")
    from_png = Image.open(WINDOWS / "source.png").convert("RGBA")
    mean, _ = inner_difference(from_ico, from_png)
    assert mean <= MEAN_LIMIT


def test_replaces_a_read_only_icon_left_by_the_last_build(tmp_path):
    out = tmp_path / "old.ico"
    out.write_bytes(b"OLD")
    out.chmod(0o444)
    convert_to_ico(SOURCE, out)
    assert out.read_bytes()[:4] == b"\0\0\1\0"


# ---- checking a picked image, ported from Test-IconInput and Test-BgInput ---------

def test_accepts_an_ico_as_it_is():
    r = check_icon(WINDOWS / "source.ico")
    assert r.ok and r.is_ico and "256x256" in r.msg


def test_accepts_a_picture_and_says_it_will_be_cropped():
    r = check_icon(SOURCE)
    assert r.ok and not r.is_ico and "cropped" in r.msg


def test_refuses_a_picture_too_small_to_be_an_icon(tmp_path):
    small = tmp_path / "small.png"
    Image.new("RGB", (40, 40)).save(small)
    assert not check_icon(small).ok


def test_warns_about_a_picture_under_256px(tmp_path):
    soft = tmp_path / "soft.png"
    Image.new("RGB", (100, 100)).save(soft)
    r = check_icon(soft)
    assert r.ok and "soft" in r.msg


def test_refuses_something_that_is_not_an_image(tmp_path):
    fake = tmp_path / "fake.png"
    fake.write_text("not a picture")
    assert not check_icon(fake).ok
    assert not check_background(fake).ok


def test_refuses_a_missing_file():
    assert not check_icon(FIX / "gone.png").ok
    assert not check_background(None).ok


def test_refuses_a_background_the_menu_would_stretch_past_recognition(tmp_path):
    tiny = tmp_path / "tiny.png"
    Image.new("RGB", (160, 120)).save(tiny)
    assert not check_background(tiny).ok
    assert check_background(SOURCE).ok


def test_accepts_an_ico_whatever_the_case_of_its_extension(tmp_path):
    shouty = tmp_path / "ICON.ICO"
    shutil.copy(WINDOWS / "source.ico", shouty)
    assert check_icon(shouty).is_ico
