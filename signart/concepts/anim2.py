"""Animation 2 (2026-10-08): an 8-bit pixel cat for the friends' demo. It sits, swings its tail and blinks.

Drawn procedurally on a 28 x 24 grid (ellipses, triangles, a tail of discs along a curl), outlined
automatically: every empty pixel next to the cat becomes the dark outline. Empty pixels are not written,
so the cat stands on its own over whatever is behind. Frames are single-sign texts in the column encoding,
positioned relative to the sign (centred over it, its bottom `above` wu higher).
"""
import numpy as np

import pixels as P

DW, DH = 28, 24          # drawing canvas; frames are padded by 1 for the outline
PAL = {"K": "#213", "O": "#e73", "D": "#b42", "L": "#fa5", "Wt": "#fff", "G": "#5d5", "P": "#111", "N": "#f8a"}


def _ellipse(cx, cy, rx, ry):
    yy, xx = np.mgrid[0:DH, 0:DW]
    return ((xx + 0.5 - cx) / rx) ** 2 + ((yy + 0.5 - cy) / ry) ** 2 <= 1


def _triangle(a, b, c):
    yy, xx = np.mgrid[0:DH, 0:DW]
    px, py = xx + 0.5, yy + 0.5

    def side(p, q):
        return (q[0] - p[0]) * (py - p[1]) - (q[1] - p[1]) * (px - p[0])
    s1, s2, s3 = side(a, b), side(b, c), side(c, a)
    return ((s1 >= 0) & (s2 >= 0) & (s3 >= 0)) | ((s1 <= 0) & (s2 <= 0) & (s3 <= 0))


def _tail(t):
    """Discs along a curling tail; its base swings with t (one swing per loop)."""
    yy, xx = np.mgrid[0:DH, 0:DW]
    m = np.zeros((DH, DW), bool)
    a0 = np.radians(10 + 30 * np.sin(2 * np.pi * t))
    x, y, a = 17.4, 21.2, a0
    for _ in range(13):
        m |= (xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2 <= 1.25 ** 2
        x, y = x + 0.65 * np.cos(a), y - 0.65 * np.sin(a)
        a += np.radians(12)
    return m


def frame(t, blink=False):
    """One frame as a H x W grid of colour keys ('' = empty)."""
    g = np.full((DH, DW), "", dtype=object)
    fur = _ellipse(11.5, 17.5, 6.3, 6) | _ellipse(11.5, 9.5, 7.6, 6)
    ears = _triangle((4, 7), (5, 0.6), (9.6, 4.2)) | _triangle((19, 7), (18, 0.6), (13.4, 4.2))
    tail = _tail(t)
    g[tail] = "O"
    g[fur | ears] = "O"
    g[_triangle((5.2, 5.2), (5.6, 2.2), (8, 4.2)) | _triangle((17.8, 5.2), (17.4, 2.2), (15, 4.2))] = "N"
    for x0, x1, y in ((10, 13, 4), (9, 10, 5), (13, 14, 5), (11, 12, 6)):          # tabby marks on the head
        g[y, x0:x1 + 1] = "D"
    for y in (15, 17):                                                              # stripes on the flanks
        g[y, 6:8] = "D"
        g[y, 16:18] = "D"
    g[_ellipse(11.5, 12.2, 3.4, 2.3)] = "Wt"                                       # muzzle
    g[_ellipse(11.5, 19.2, 3.2, 3.6)] = "Wt"                                       # chest
    g[22:24, 7:10] = "Wt"                                                           # paws
    g[22:24, 13:16] = "Wt"
    g[23, 10:13] = "O"
    if blink:
        g[9, 7:10] = "K"
        g[9, 14:17] = "K"
    else:
        for x in (7, 14):
            g[7:10, x:x + 2] = "G"
            g[8:10, x + 1] = "P"
            g[7, x] = "Wt"                                                          # catchlight
    g[11, 11:13] = "N"                                                              # nose
    g[12, 10] = "K"                                                                 # mouth: a small w
    g[12, 13] = "K"
    g[13, 11:13] = "K"
    g = np.pad(g, 1, constant_values="")
    filled = g != ""
    pad = np.pad(filled, 1)
    near = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    g[~filled & near] = "K"                                                         # outline
    return g


def frames(n=8):
    """n frames: one tail swing per loop, a blink on the last two frames."""
    return [frame(k / n, blink=k >= n - 2) for k in range(n)]


def colours(g):
    """Colour keys -> rows of '#rgb' ('' stays empty)."""
    return [[PAL[k] if k else "" for k in row] for row in g]


def cat(n=8, pitch=7.0, above=60.0):
    """Frames for live.py anim: the cat, 30 x 26 pixels of `pitch` wu (7 = 17.5 cm: 5.25 m wide), centred
    over the sign, its bottom `above` wu (40 = 1 m) higher."""
    return P.sign_frames([colours(g) for g in frames(n)], pitch, above)


def cat_slow(pitch=7.0, above=60.0):
    """Three frames for vanilla viewers (they re-read a sign every 2 s, so play it at 0.4 fps: 2.5 s each):
    tail up, tail down, blink."""
    return P.sign_frames([colours(frame(0.25)), colours(frame(0.75)), colours(frame(0.5, blink=True))], pitch, above)


def cat_slow_block(grid, sign="B2-1"):
    """cat_slow on one sign of a gallery block, for live.py anim-block."""
    return {sign: cat_slow()}
