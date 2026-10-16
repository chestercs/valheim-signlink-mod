"""pixels.py and the animation frames: every pixel lands where its generator aimed it."""
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import engine as E  # noqa: E402
import pixels as P  # noqa: E402

pytestmark = pytest.mark.skipif(not os.path.exists(E.GLYPHS_PATH), reason="run extract_glyphs.py first")


def _lands(text, aimed, cell="A1", tol=0.15):
    back = E.layout_sign(text, cell)
    got = sorted((round(q.x, 3), round(q.y, 3)) for q in back)
    want = sorted((p.x, p.y) for p in aimed)
    assert len(got) == len(want)
    return max(abs(a - c) + abs(b - d) for (a, b), (c, d) in zip(got, want)) < tol


def test_quantize_and_glyph_size():
    rgb = np.zeros((4, 4, 3))
    rgb[..., 0] = 1.0
    px = P.quantize(rgb, dither=False)
    assert len(px) == 4 and {c for row in px for c in row} == {"#f00"}
    w, _ = E.glyph_box("■")
    bold = E.db().metrics("■")[3]
    assert abs(P.glyph_size(5.0) * (2 * w + 4 * bold) - 5.3) < 1e-9     # pitch + 0.3 wu overlap


def test_sign_frames_skip_empty_pixels_and_land_where_aimed():
    px = [["#f00", "", "#0f0"], ["#00f", "#fff", ""], ["#ff0", "#0ff", "#f0f"]]
    (text,) = P.sign_frames([px], 4.0, above=40)
    gx, gy = E.DEFAULT_GRID.pos("A1")
    assert _lands(text, P.items(px, 4.0, gx, gy + 40, "A1"))


def test_cat_frames():
    from concepts import anim2
    g = anim2.frames(2)
    texts = anim2.cat(n=2)
    gx, gy = E.DEFAULT_GRID.pos("A1")
    for grid, text in zip(g, texts):
        assert _lands(text, P.items(anim2.colours(grid), 7.0, gx, gy + 60, "A1"))


def test_small_fjord_frame():
    from concepts import anim1
    (text,) = anim1.fjord(n=1, W=12, H=8, width=60.0)
    gx, gy = E.DEFAULT_GRID.pos("A1")
    assert _lands(text, P.items(anim1.picture(12, 8, 0.0), 5.0, gx, gy + 40, "A1"))
