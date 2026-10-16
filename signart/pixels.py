"""Pixel pictures on signs: colour functions -> 12-bit colour grids -> glyph items -> sign texts.

One place for what the mosaics (concepts/row3-5) and the animations (concepts/anim1-2) share:

  sample(fn, W, H, ss)      supersampled RGB of a scene function fn(u, v) (u right, v down, 0..1)
  quantize(rgb, dither)     -> rows of '#rgb' (Bayer-dithered to 4 bits per channel by default)
  glyph_size(pitch, cover)  TMP size of a ■ that covers a pitch-wide cell with a hair of overlap (no seams)
  items(px, pitch, ...)     -> engine.Placed glyphs ('' pixels are left out)
  column_text(items, grid)  -> one sign text in the column encoding (engine.column_cmds, verified in gallery 9)
  sign_frames(pictures, ..) -> texts relative to the sign itself (live.py anim, any sign in any world)
"""
import numpy as np

import engine as E

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16 - 0.47
HEX = "0123456789abcdef"


def sample(fn, W, H, ss=3):
    """Mean colour of every pixel from ss x ss samples of fn(u, v) -> H x W x 3 floats."""
    jj, ii = np.meshgrid(np.arange(W * ss), np.arange(H * ss))
    u, v = (jj + 0.5) / (W * ss), (ii + 0.5) / (H * ss)
    return fn(u, v).reshape(H, ss, W, ss, 3).mean(axis=(1, 3))


def quantize(rgb, dither=True):
    """H x W x 3 floats -> rows of '#rgb' strings (12-bit colour: every colour tag is 6 characters)."""
    H, W = rgb.shape[:2]
    d = BAYER[np.arange(H)[:, None] % 4, np.arange(W)[None, :] % 4][..., None] if dither else 0.0
    q = np.clip(np.floor(np.clip(rgb, 0, 1) * 15 + 0.5 + d * 0.9), 0, 15).astype(int)
    return [["#" + "".join(HEX[c] for c in q[r, k]) for k in range(W)] for r in range(H)]


def glyph_size(pitch, glyph="■", cover=None):
    """TMP size whose glyph (ink plus faux bold) is pitch + min(0.3, 0.12 * pitch) wu wide: neighbours overlap
    a hair, so no background shows between them (gallery 4-7). cover = n makes it n pitches wide instead, so
    every point lies under n x n glyphs: pixels a few screen pixels small are drawn half transparent by the
    SDF shader, and stacked layers make them opaque again (vanilla_hq.py)."""
    w, _ = E.glyph_box(glyph)
    bold = E.db().metrics(glyph)[3]
    ink = pitch * cover if cover else pitch + min(0.3, 0.12 * pitch)
    return ink / (2 * w + 4 * bold)


def items(px, pitch, center_x, bottom, cell, layer=None, rows=None, size=None, font=E.FONT_PATH, glyph="■"):
    """Glyphs for the colour grid px (rows of '#rgb', '' = empty): pixel (r, c) is centred at
    (center_x + (c - (W-1)/2) * pitch, bottom + pitch/2 + (H-1-r) * pitch), all on sign (cell, layer)."""
    H, W = len(px), len(px[0])
    S = glyph_size(pitch, glyph) if size is None else size
    return [E.Placed(glyph, center_x + (c - (W - 1) / 2) * pitch, bottom + pitch / 2 + (H - 1 - r) * pitch, S,
                     color=E.parse_hex(px[r][c]), cell=cell, layer=layer, font=font)
            for r in (range(H) if rows is None else rows) for c in range(W) if px[r][c]]


def position_decimals(pitch):
    """Decimals for indent and voffset: 1 when pixel centres fall on the 0.1 wu grid (pitch a multiple of 0.2),
    else 2 (0.75, 0.5, 0.25 wu ...: rounding to 0.1 would move columns by up to a fifth of a pixel)."""
    return 1 if abs(pitch * 5 - round(pitch * 5)) < 1e-9 else 2


def column_text(glyphs, grid=E.DEFAULT_GRID, nd=1, default_step=None):
    cmds, lh = E.column_cmds(glyphs, grid, nd=nd, default_step=default_step)
    if cmds is None:
        raise ValueError("picture too tall for the column encoding (voffsets past 32767)")
    return E.encode_lines(cmds, lh, nd=nd, single=False)


def sign_frames(pictures, pitch, above=40.0):
    """One text per picture, relative to the sign itself: centred over it, its bottom `above` wu higher
    (40 = 1 m). Any standard sign shows it the same way."""
    gx, gy = E.DEFAULT_GRID.pos("A1")
    return [column_text(items(px, pitch, gx, gy + above, "A1")) for px in pictures]
