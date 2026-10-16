"""The art library's pets: the pixel cat's family and friends (concepts/anim2.py), cats and dogs side by side.

Drawn like the cat: numpy masks on a Canvas, outlined automatically in the cat's dark outline, at the cat's pitch,
so a kitten is small next to it and a hound sits taller. Every piece moves; the first frame is a good still for
players without the mod. Empty pixels are not written: the sprites stand on their own over whatever is behind.
"""
import numpy as np

import artlib as A
from concepts import anim2


def _wave(n, k, phase=0.0):
    return np.sin(2 * np.pi * (k / n + phase))


def _rell(c, cx, cy, a, b, ang=0.0):
    """An ellipse with half axes a (along ang, degrees, screen coordinates: positive turns clockwise) and b."""
    t = np.radians(ang)
    dx, dy = c.px - cx, c.py - cy
    u = dx * np.cos(t) + dy * np.sin(t)
    v = -dx * np.sin(t) + dy * np.cos(t)
    return (u / a) ** 2 + (v / b) ** 2 <= 1


def _chain(c, x, y, ang, n, step, curl, r0, r1=None):
    """Discs along a curling path (a tail): from (x, y) heading ang degrees (90 = up), turning curl a step."""
    r1 = r0 if r1 is None else r1
    m = np.zeros((c.h, c.w), bool)
    for i in range(n):
        r = r0 + (r1 - r0) * i / max(n - 1, 1)
        m |= (c.px - x) ** 2 + (c.py - y) ** 2 <= r * r
        x, y = x + step * np.cos(np.radians(ang)), y - step * np.sin(np.radians(ang))
        ang += curl
    return m


def _rot(x, y, ang):
    t = np.radians(ang)
    return x * np.cos(t) - y * np.sin(t), x * np.sin(t) + y * np.cos(t)


def _zee(c, x, y, size, key):
    """A small 'z' (size 4 or 5) with its top left at (x, y)."""
    for i in range(size):
        c.set(x + i, y, key)
        c.set(x + i, y + size - 1, key)
    for i in range(1, size - 1):
        c.set(x + size - 1 - i, y + i, key)


def _paste(dst, src, x, y):
    """Non-empty pixels of src over dst, src's top left at (x, y)."""
    src = np.asarray(src, dtype=object)
    h, w = src.shape
    view = dst[y:y + h, x:x + w]
    m = src != ""
    view[m] = src[m]
    return dst


def _edge(c, mask):
    """The cat's outline round mask where it lies over the drawing already there, so a part in front reads apart."""
    pad = np.pad(mask, 1)
    c.paint((pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]) & ~mask & (c.g != ""), "K")


def _grid(w, h):
    return np.full((h, w), "", dtype=object)


def _out(c):
    return A.outline(c.g if isinstance(c, A.Canvas) else c, "K")


def _sprite(g, pal):
    return A.colours(g, {**pal, "K": A.OUTLINE})


# ---- a cat seen from the side (side_cat: lib_friends; cat_side: black cat, Siamese) ----------------------------

def side_cat(points=False, stretch=0.0, walk=None, tail=0.0, blink=False, head_dn=0.0):
    """A cat facing right on a 40 x 26 canvas, outlined (42 x 28). Keys: F fur, S far legs, X points (ears, mask,
    legs, tail when points), E eye, P pupil, N nose, H a highlight along the back.
    stretch 0..1 bends it into a play-bow stretch; walk is the gait phase (0..1) or None; tail swings it (-1..1);
    head_dn lowers the head (0..1, to a bowl)."""
    c = A.Canvas(40, 26)
    s = stretch

    def lerp(a, b):
        return a + (b - a) * s
    bob = 0.0 if walk is None else 0.5 * abs(np.sin(2 * np.pi * walk))
    bx, by, ba = lerp(19, 20), lerp(14.5, 15.5) - bob, lerp(0, 16)
    chx, chy = lerp(27, 29), lerp(13.5, 19) - bob
    hax, hay = lerp(12, 11.5), lerp(14, 10.5) - bob
    hx, hy = lerp(31.5, 34.5) + 1.2 * head_dn, lerp(8, 15.5) - bob + 9 * head_dn
    leg_k = "X" if points else "S"
    near_k = "X" if points else "F"

    def leg(x0, y0, x1, y1, key, phase):
        if walk is not None:
            p = 2 * np.pi * (walk + phase)
            x1 += 2.4 * np.cos(p)
            y1 -= max(0.0, 1.6 * np.sin(p))
        c.paint(c.line(x0, y0, x1, y1, 2.6), key)
        c.paint(c.disc(x1 + 0.6, y1 - 0.4, 1.4), key)
    # tail first (behind), far legs, body, near legs, head
    a0 = lerp(115, 95) + 25 * tail
    c.paint(_chain(c, hax - 4.2, hay - 2, a0, 12, 0.85, lerp(-11, -3) - 9 * tail, 1.35, 1.15), "X" if points else "F")
    leg(lerp(25, 27.5), lerp(16, 19.5) - bob, lerp(24.5, 35), 24.0, leg_k, 0.5)
    leg(lerp(11, 11), lerp(17, 13) - bob, lerp(10, 10.5), 24.0, leg_k, 0.0)
    c.paint(_rell(c, bx, by, 10, 4.8, ba), "F")
    c.paint(c.ellipse(chx, chy, 4, 4.6), "F")
    c.paint(c.ellipse(hax, hay, 4.8, 5.2), "F")
    c.paint(_rell(c, bx - 1, by - 3.8, 6, 0.8, ba) & _rell(c, bx, by, 10, 4.8, ba), "H")
    leg(lerp(27.5, 29.5), lerp(16, 20) - bob, lerp(27.5, 37.5), 24.0, near_k, 0.0)
    leg(lerp(13.5, 13), lerp(17, 13) - bob, lerp(14, 14), 24.0, near_k, 0.5)
    # head
    c.paint(c.triangle((hx - 3.9, hy - 2.2), (hx - 2.9, hy - 7.2), (hx + 0.1, hy - 3.6)), "X" if points else "F")
    c.paint(c.ellipse(hx, hy, 4.6, 4.2), "F")
    c.paint(c.ellipse(hx + 1.5, hy + 1.5, 3.4, 2.8), "F")
    c.paint(c.triangle((hx - 0.4, hy - 3.6), (hx + 2.4, hy - 7.6), (hx + 3.8, hy - 2.4)), "X" if points else "F")
    c.paint(c.triangle((hx + 0.6, hy - 3.6), (hx + 2.3, hy - 6.2), (hx + 3.0, hy - 3.0)), "N")
    if points:
        c.paint(c.ellipse(hx + 2.6, hy + 1.2, 2.6, 2.6) & c.ellipse(hx + 1.5, hy + 1.5, 3.4, 2.8), "X")
    ex, ey = int(round(hx + 1.3)), int(round(hy - 1.2))
    if blink:
        c.set(ex, ey + 1, "K").set(ex + 1, ey + 1, "K")
    else:
        c.paint(c.rect(ex, ey, ex + 2, ey + 2), "E")
        c.set(ex + 1, ey, "P").set(ex + 1, ey + 1, "P")
    nx, ny = int(round(hx + 4.2)), int(round(hy + 0.6))
    c.set(nx, ny, "N")
    c.set(nx - 1, ny + 2, "K")
    return _out(c)


def cat_side(points=False, stretch=0.0, walk=None, tail=0.0, blink=False, rim=False):
    """A cat standing side on, facing right, on a 40 x 26 canvas, outlined (42 x 28): a long back rising to the
    hips, a deep chest, a round head on a short neck with two pricked ears, legs about as long as the body is deep
    (the hind ones bent at the hock), a long tail held up in a question mark. Keys: F fur, S far legs, X points
    (ears, mask, legs, tail when points), H a light rim along the back (rim), E eye, P pupil, N nose, inner ear.
    stretch 0..1 bows it into a stretch: forelegs flat out in front, chest down, rump up; walk is the gait phase
    (0..1) or None; tail swings it (-1..1)."""
    c = A.Canvas(40, 26)
    s = stretch

    def L(a, b):
        return a + (b - a) * s
    G = 25.5                                                                    # the ground under the paws
    bob = 0.0 if walk is None else 0.5 * abs(np.sin(2 * np.pi * walk))
    hx, hy = 12.0, L(12.2, 10.6) - bob                                          # the hip
    sx, sy = L(26.5, 27.5), L(12.8, 19.5) - bob                                 # the shoulder
    px = "X" if points else "F"

    def leg(top, knee, foot, key, phase, w0, w1):
        """A leg from top through knee to the paw at foot; walking swings the paw and lifts it."""
        fx, fy = foot
        kx, ky = knee
        if walk is not None:
            p = 2 * np.pi * (walk + phase)
            fx += 2.6 * np.cos(p)
            kx += 1.3 * np.cos(p)
            lift = max(0.0, 1.8 * np.sin(p))
            fy -= lift
            ky -= 0.5 * lift
        m = c.line(top[0], top[1], kx, ky, w0) | c.line(kx, ky, fx, fy - 0.8, w1)
        m |= c.ellipse(fx + 0.6, fy - 0.7, 1.5, 0.95)
        if key != "S":                                                          # a dark edge against the far legs
            pad = np.pad(m, 1)
            near = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
            c.paint(near & ~m & (c.g == "S"), "K")
        c.paint(m, key)
    # the tail behind everything: up from the rump, the tip curling forward
    c.paint(_chain(c, hx - 3.6, hy - 2.2, L(118, 100) + 22 * tail, 13, 0.92, L(-7, -4) - 5 * tail, 1.3, 1.05), px)
    # far legs (darker), a little offset
    leg((sx - 1.5, sy + 1.5), (L(sx - 1.6, sx + 3), L(G - 4, G - 1.5)), (L(sx - 1.4, sx + 9.5), G), "S", 0.5,
        2.4, 2.0)
    leg((hx + 1.5, hy + 2), (hx - 0.2, G - 4.2), (hx + 1.6, G), "S", 0.0, 3.0, 2.0)
    # body: the back from hip to shoulder, the rump, the chest, the neck
    mx, my = (hx + sx) / 2, (hy + sy) / 2
    ang = np.degrees(np.arctan2(sy - hy, sx - hx))
    c.paint(_rell(c, mx, my + 0.6, np.hypot(sx - hx, sy - hy) / 2 + 2.5, 3.9, ang), "F")
    c.paint(c.ellipse(hx - 0.3, hy + 0.4, 4.3, 4.4), "F")
    c.paint(c.ellipse(sx + 1.0, sy + 1.0, 3.6, 4.2), "F")
    hdx, hdy = L(32.3, 35.2), L(7.2, 15.6) - bob                               # the head
    c.paint(c.line(sx - 0.4, sy - 0.6, hdx - 1.4, hdy + 0.4, 4.6), "F")
    # near legs
    leg((sx + 0.6, sy + 1.8), (L(sx + 0.8, sx + 4.5), L(G - 4, G - 1.6)), (L(sx + 1.0, sx + 11.5), G), px, 0.0,
        2.6, 2.2)
    leg((hx + 3.0, hy + 2.5), (hx + 1.2, G - 4.2), (hx + 3.0, G), px, 0.5, 3.4, 2.1)
    # the head: far ear, skull, muzzle, near ear
    c.paint(c.triangle((hdx - 3.7, hdy - 1.4), (hdx - 2.9, hdy - 6.6), (hdx - 0.5, hdy - 3.4)), px)
    c.paint(c.ellipse(hdx, hdy, 4.3, 3.8), "F")
    c.paint(c.ellipse(hdx + 2.7, hdy + 1.3, 2.2, 1.8), "F")
    c.paint(c.triangle((hdx - 1.0, hdy - 3.4), (hdx + 1.2, hdy - 7.4), (hdx + 2.8, hdy - 2.6)), px)
    if points:                                                                  # the dark mask over the muzzle
        c.paint((c.ellipse(hdx + 2.4, hdy + 0.8, 2.8, 2.6) | c.ellipse(hdx + 2.7, hdy + 1.3, 2.2, 1.8))
                & (c.ellipse(hdx, hdy, 4.3, 3.8) | c.ellipse(hdx + 2.7, hdy + 1.3, 2.2, 1.8)), "X")
    c.paint(c.triangle((hdx - 0.2, hdy - 3.4), (hdx + 1.1, hdy - 6.0), (hdx + 2.0, hdy - 3.0)), "N")
    if rim:                                                                     # the top pixel of every column
        top = (c.g == "F") & np.vstack([np.ones((1, c.w), bool), c.g[:-1] == ""])
        c.paint(top & (c.px > hx - 4) & (c.px < hdx - 2), "H")
    ex, ey = int(round(hdx + 0.8)), int(round(hdy - 1.4))
    if blink:
        c.set(ex, ey + 1, "K").set(ex + 1, ey + 1, "K")
    else:
        c.paint(c.rect(ex, ey, ex + 2, ey + 2), "E")
        c.set(ex + 1, ey, "P").set(ex + 1, ey + 1, "P")
    nx, ny = int(round(hdx + 4.3)), int(round(hdy + 0.2))
    c.set(nx, ny, "N")
    c.set(nx - 1, ny + 2, "K")
    return _out(c)


# ---- a front-facing cat like anim2's (white cat, kitten) ------------------------------------------------------

def _front_head(c, hx, hy, s, eye="G", eyes="open", tabby=False):
    """The cat's head (anim2) around (hx, hy) at scale s: ears, muzzle, eyes, nose and the small w mouth."""
    c.paint(c.triangle((hx - 7.5 * s, hy - 2.5 * s), (hx - 6.5 * s, hy - 8.9 * s), (hx - 1.9 * s, hy - 5.3 * s)), "F")
    c.paint(c.triangle((hx + 7.5 * s, hy - 2.5 * s), (hx + 6.5 * s, hy - 8.9 * s), (hx + 1.9 * s, hy - 5.3 * s)), "F")
    c.paint(c.ellipse(hx, hy, 7.6 * s, 6 * s), "F")
    c.paint(c.triangle((hx - 6.3 * s, hy - 4.3 * s), (hx - 5.9 * s, hy - 7.3 * s), (hx - 3.5 * s, hy - 5.3 * s)), "N")
    c.paint(c.triangle((hx + 6.3 * s, hy - 4.3 * s), (hx + 5.9 * s, hy - 7.3 * s), (hx + 3.5 * s, hy - 5.3 * s)), "N")
    x0 = int(np.floor(hx))                                  # the head's two middle columns are x0 and x0 + 1
    yc = int(np.floor(hy))
    if tabby:
        c.paint(c.rect(x0 - 1, yc - 5, x0 + 3, yc - 4), "D")
        c.set(x0 - 2, yc - 4, "D").set(x0 + 3, yc - 4, "D").set(x0, yc - 3, "D").set(x0 + 1, yc - 3, "D")
    c.paint(c.ellipse(hx, hy + 2.7 * s, 3.4 * s, 2.3 * s), "W")
    ey = yc - 2
    for x in (x0 - 4, x0 + 3):
        if eyes == "open":
            c.paint(c.rect(x, ey, x + 2, ey + 3), eye)
            c.set(x + 1, ey + 1, "P").set(x + 1, ey + 2, "P").set(x, ey, "W")
        elif eyes == "happy":                               # closed, smiling: a small arch
            c.set(x, ey + 2, "K").set(x + 1, ey + 1, "K").set(x + 2, ey + 2, "K")
        else:
            c.set(x, ey + 2, "K").set(x + 1, ey + 2, "K")
    c.set(x0, yc + 1, "N").set(x0 + 1, yc + 1, "N")
    c.set(x0 - 1, yc + 2, "K").set(x0 + 2, yc + 2, "K").set(x0, yc + 3, "K").set(x0 + 1, yc + 3, "K")
    return x0, yc


# ---- 1. kitten and yarn ---------------------------------------------------------------------------------------

KITTEN = {"F": "#99a", "D": "#667", "W": "#fff", "G": "#5be", "P": "#111", "N": "#f8a", "Y": "#d35", "y": "#912"}


def kitten(k):
    """A grey tabby kitten sits and bats a ball of yarn: the paw strikes, the ball rolls off and back."""
    paws = [(16.5, 11), (19.4, 16.5), (17, 12.5), (13.5, 20.5), (13.5, 20.5), (15.5, 12.5)]
    balls = [22.8, 22.8, 25.5, 27.5, 26, 24]
    c = A.Canvas(32, 23)
    bx, by, r = balls[k], 19.2, 3.4
    ball = c.disc(bx, by, r)
    c.paint(ball, "Y")                                  # the ball's strands turn as it rolls
    o = (bx * 1.4) % 3.0 - 1.5
    c.paint(ball & (c.ellipse(bx + o, by, 1.9, r) & ~c.ellipse(bx + o, by, 0.9, r - 1)), "y")
    c.paint(ball & c.line(bx - r, by + 1 - o, bx + r, by - 1 - o, 0.9), "y")
    c.paint(_chain(c, 5.5, 21, 175 + 15 * _wave(6, k), 9, 0.9, -14, 1.2, 1.0), "F")
    c.paint(c.ellipse(9.5, 17.6, 5, 4.9), "F")
    c.paint(c.ellipse(9.5, 18.4, 2.3, 3), "W")
    c.paint(c.rect(5, 16, 7, 17), "D").paint(c.rect(12, 16, 14, 17), "D")
    c.paint(c.rect(6, 21, 9, 23), "W")
    _front_head(c, 9.5, 8.6, 0.85, eye="G", tabby=True)
    px, py = paws[k]                                    # the batting paw, outlined inside the sprite
    arm = c.line(12.2, 15.2, px, py, 2.3) | c.disc(px, py, 1.5)
    _edge(c, arm)
    c.paint(arm, "F")
    c.paint(c.disc(px, py, 1.5), "W")
    return _out(c)


# ---- 2. black cat ---------------------------------------------------------------------------------------------

BLACK = {"F": "#445", "S": "#334", "H": "#778", "E": "#fd3", "P": "#111", "N": "#d79"}


def black_cat(k):
    """A black cat with yellow eyes stands and flicks its long tail, then stretches low into a play bow and back;
    a lighter rim along its back keeps the shape on dark backgrounds."""
    s = [0, 0, 0, 0.5, 1, 1, 0.5, 0][k]
    tail = [-0.4, 0.6, -0.4, 0.2, 0.5, -0.1, 0.2, 0.6][k]
    return cat_side(stretch=s, tail=tail, blink=k == 5, rim=True)


# ---- 3. calico asleep -----------------------------------------------------------------------------------------

CALICO = {"F": "#fed", "O": "#e83", "B": "#334", "N": "#f8a", "Z": "#bdf", "W": "#fff"}


def calico(k):
    """A calico cat sleeps curled up: a round body with a big orange patch over the back and a black one on the
    hip, its tail wrapped round the front and its head (one black ear, one orange) laid on the tail with the eyes
    shut: the body rises and falls with each breath, small z marks float up."""
    c = A.Canvas(36, 26)
    G = 25.6                                                                   # the ground
    ry = [8.4, 8.8, 9.2, 8.8][k]
    by = G - ry
    body = c.ellipse(21.5, by, 12.6, ry) & (c.py < G - 1)                      # the ball
    c.paint(body, "F")
    c.paint(body & _rell(c, 26.0, by - ry + 2.2, 8.4, 5.2, 12), "O")           # an orange saddle over the back
    c.paint(body & c.ellipse(32.5, by + 1.5, 4.2, 4.6), "B")                   # a black patch on the hip
    tail = c.line(6.5, G - 1.6, 31.0, G - 1.6, 2.5)                            # round the front, under the chin
    tail |= _chain(c, 31.0, G - 1.6, 10, 6, 0.9, 16, 1.3)
    tail |= _chain(c, 6.5, G - 1.6, 180, 6, 0.9, -24, 1.25, 1.0)
    _edge(c, tail)
    c.paint(tail, "O")
    c.paint(tail & (c.px < 5.5), "B")                                          # a dark tip
    hd = A.Canvas(36, 26)                                                      # the head laid on the tail
    hx, hy = 11.5, G - 8.0 + 0.4 * (ry - 8.4)
    _front_head(hd, hx, hy, 0.86, eyes="closed")
    fur = hd.g == "F"
    hd.paint(fur & (hd.px < hx - 0.5) & (hd.py < hy - 1.4), "B")             # one black ear, one orange
    hd.paint(fur & (hd.px > hx + 1.5) & (hd.py < hy - 0.4), "O")
    _edge(c, hd.g != "")
    c.g[hd.g != ""] = hd.g[hd.g != ""]
    c.paint(c.ellipse(19.5, G - 3.4, 2.0, 1.2) & ~(hd.g != ""), "W")           # a front paw over the tail
    z = A.Canvas(36, 31)                                                       # room above the cat for the z marks
    z.g = np.pad(c.g, ((5, 0), (0, 0)), constant_values="")
    for st in (k % 4, (k + 2) % 4):
        if st < 3:
            x, y, size = [(15, 8, 4), (19, 4, 4), (23, 0, 5)][st]
            _zee(z, x, y, size, "Z")
    return _out(z)


# ---- 4. white cat washing ---------------------------------------------------------------------------------------

WHITE = {"F": "#eef", "S": "#bcd", "W": "#fff", "G": "#5bf", "P": "#124", "N": "#f8a", "T": "#e57"}


def white_cat(k):
    """A white cat with blue eyes sits and washes: it lifts a front paw to its mouth, licks it twice with its eyes
    shut contentedly, and puts it down again."""
    eyes, paw, lean, tongue = [("open", None, 0, False), ("open", (9.0, 17.0), 0, False),
                               ("happy", (9.0, 15.8), 1, True), ("happy", (9.0, 15.0), 1, False),
                               ("happy", (9.0, 15.8), 1, True), ("open", (9.0, 17.0), 0, False)][k]
    c = A.Canvas(28, 24)
    c.paint(_chain(c, 17.4, 21.2, 10 + 25 * _wave(6, k), 13, 0.65, 12, 1.25), "F")
    c.paint(c.ellipse(11.5, 17.5, 6.3, 6), "F")
    c.paint(c.ellipse(11.5, 21.5, 6, 2.2) & c.ellipse(11.5, 17.5, 6.3, 6) & (c.xx >= 14), "S")
    c.paint(c.rect(13, 22, 16, 24), "W")
    if paw is None:
        c.paint(c.rect(7, 22, 10, 24), "W")
    x0, yc = _front_head(c, 11.5 - lean, 9.5 + lean, 1.0, eye="G", eyes=eyes)
    if paw:                                             # the foreleg bent up from the elbow, outlined over the fur
        px, py = paw
        arm = c.line(8.4, 21.2, px, py, 2.4) | c.ellipse(px, py, 1.6, 1.3)
        _edge(c, arm)
        c.paint(arm, "F")
        c.paint(arm & (c.px < 8.2), "S")
        c.set(int(px), int(py) + 1, "N")
    if tongue:                                          # the tongue reaches down to the paw
        c.set(x0 - 1, yc + 3, "T").set(x0 - 1, yc + 4, "T")
    return _out(c)


# ---- 5. Siamese walking -----------------------------------------------------------------------------------------

SIAMESE = {"F": "#edc", "S": "#321", "X": "#532", "E": "#4bf", "P": "#124", "N": "#a66"}


def siamese(k):
    """A Siamese cat, cream with dark points and blue eyes, walks in place: a four-frame diagonal walk cycle."""
    return cat_side(points=True, walk=k / 4, tail=0.3 * _wave(4, k))


# ---- dogs sitting, seen from the side (hound, wolf cub) ---------------------------------------------------------

def sit_dog(c, ox, oy, z, wolf=False, head=0.0, tail=0.0, pant=0, blink=False, ears=0.0, mouth=False, hz=1.0):
    """A dog sitting, facing right; its 34 x 36 design scaled by z with its top left at (ox, oy).
    Keys: F fur, S far side, B pale (chest, muzzle, paws), R ears (hound), E eye, C collar, Gs stud, T tongue.
    head turns the head about the neck (degrees, negative = up), tail wags (-1..1), pant 0..2 the tongue's
    length, hz scales the head (a cub's is big)."""
    def P(x, y):
        return ox + z * x, oy + z * y

    def ell(x, y, a, b, ang=0.0):
        X, Y = P(x, y)
        return _rell(c, X, Y, a * z, b * z, ang)

    nx, ny = 19.0, 14.0                                                         # the neck: the head turns here

    def H(dx, dy):
        rx, ry = _rot(3.0 + dx * hz, -4.0 + dy * hz, head)
        return P(nx + rx, ny + ry)

    def hell(dx, dy, a, b, ang=0.0):
        X, Y = H(dx, dy)
        return _rell(c, X, Y, a * z * hz, b * z * hz, ang + head)

    def hset(dx, dy, key):
        X, Y = H(dx, dy)
        c.set(int(np.floor(X)), int(np.floor(Y)), key)
    # tail on the ground behind, then far leg, body, near leg
    tx, ty = P(6, 31.5)
    if wolf:
        c.paint(_chain(c, tx, ty, 170 + 25 * tail, 10, 0.9 * z, -3, 2.0 * z, 1.4 * z), "F")
        c.paint(_chain(c, tx, ty, 170 + 25 * tail, 10, 0.9 * z, -3, 2.0 * z, 1.4 * z) & c.disc(
            tx + 8 * z * np.cos(np.radians(170 + 25 * tail)), ty - 8 * z * np.sin(np.radians(170 + 25 * tail)),
            2.2 * z), "B")
    else:
        c.paint(_chain(c, tx - 1.5 * z, ty - 1 * z, 152 + 30 * tail, 10, 1.0 * z, -5, 1.5 * z, 1.0 * z), "F")
    x0, y0 = P(19, 25)
    x1, y1 = P(18.5, 33.5)
    c.paint(c.line(x0, y0, x1, y1, 3.0 * z), "S")
    c.paint(ell(18.5, 34.2, 2.2, 1.4), "S")
    c.paint(ell(16.5, 20 - pant * 0.15, 10.5, 5.8, -65), "F")
    c.paint(ell(11, 28, 7, 6.5), "F")
    c.paint(ell(20, 20, 2.8, 5.5, -10), "B")
    c.paint(ell(15.5, 33.8, 4, 1.6), "B")                                       # the hind paw
    x0, y0 = P(21, 24)
    x1, y1 = P(21.5, 33.5)
    fore = c.line(x0, y0, x1, y1, 3.0 * z)
    _edge(c, fore & (c.py > y0 + 2 * z))                                        # the foreleg apart from the chest
    c.paint(fore, "F")
    c.paint(ell(22.5, 34.0, 2.4, 1.5), "B")
    if not wolf:
        c.paint(ell(18.6, 15.2, 4.4, 1.6, -65), "C")                            # the collar and its stud
        X, Y = P(20.6, 15.6)
        c.set(int(X), int(Y), "Gs")
    # head
    if wolf:                                                                    # pricked ears, the far one darker
        e = ears
        c.paint(c.triangle(H(-4.6, -1.5), H(-4.0 - e, -8.6), H(-0.6, -3.6)), "S")
        c.paint(c.triangle(H(-2.6, -2.8), H(-0.8 - e, -9.4), H(2.4, -2.6)), "F")
        c.paint(c.triangle(H(-1.4, -3.2), H(-0.7 - e, -7.6), H(1.2, -3.0)), "N")
    if pant:                                                                    # the tongue hangs out under the jaw
        c.paint(hell(4.6, 3.6 + 0.5 * pant, 1.3, 1.0 + 0.6 * pant), "T")
    c.paint(hell(0, 0, 5.2, 4.5), "F")
    if wolf:
        c.paint(hell(0.8, 2.8, 3.0, 1.6), "B")                                  # pale cheek
        c.paint(hell(5.0, 1.4, 3.6, 1.9, 6), "F")
        c.paint(hell(5.2, 2.4, 3.2, 1.0, 6), "B")
    else:
        c.paint(hell(5.2, 1.8, 3.9, 2.7, 4), "F")
    if mouth:                                                                   # howling: the jaw drops open
        c.paint(hell(4.6, 4.2, 3.2, 1.1, 24), "B")
        c.paint(hell(5.4, 3.2, 1.8, 0.7, 14), "K")
    else:
        mx0, my0 = H(3.4, 3.2)
        mx1, my1 = H(8.6, 2.8)
        c.paint(c.line(mx0, my0, mx1, my1, 0.9), "K")
    if not wolf:
        c.paint(hell(-2.8, 1.8, 4.2, 2.1, 98), "R")                             # the floppy ear
        c.paint(hell(1.8, -2.7, 0.9, 0.6), "B")                                 # a tan brow spot
    hset(9.3, 0.4, "K")
    hset(9.3, 1.3, "K")
    ex, ey = (int(np.floor(v)) for v in H(1.5, -0.5))                          # the eye: a 2 x 2 block at any angle
    if blink:
        c.set(ex, ey + 1, "K").set(ex + 1, ey + 1, "K")
    elif wolf:
        c.paint(c.rect(ex, ey, ex + 2, ey + 2), "E")
        c.set(ex + 1, ey + 1, "P")
    else:
        c.paint(c.rect(ex, ey, ex + 2, ey + 2), "E")
        c.set(ex, ey, "W")
    return H(9.3, 0.8)                                                          # the nose, for sounds


# ---- 6. hound ---------------------------------------------------------------------------------------------------

HOUND = {"F": "#b74", "S": "#853", "B": "#eb7", "R": "#642", "E": "#211", "W": "#fff", "C": "#c33", "Gs": "#fd4",
         "T": "#e56", "N": "#f8a"}


def hound(k):
    """A Viking hound sits in a red collar, wagging its tail and panting with its tongue out."""
    c = A.Canvas(34, 36)
    sit_dog(c, 0, 0, 1.0, tail=_wave(4, k), pant=[1, 2, 1, 2][k], hz=1.15)
    return _out(c)


# ---- 7. puppy ---------------------------------------------------------------------------------------------------

PUPPY = {"F": "#fed", "S": "#dcb", "R": "#a63", "E": "#211", "W": "#fff", "T": "#e56", "N": "#211", "h": "#324"}


def puppy(k):
    """A cream puppy with brown ears bounces on the spot: the ears flop up as it lands and down as it rises."""
    lift = [0, 3, 5, 3][k]
    flop = [-0.2, -0.6, 0.2, 0.8][k]                     # 1 = ears up and out, -1 = hanging
    c = A.Canvas(26, 27)
    by = 26 - lift
    squash = 1 if lift == 0 else 0
    c.paint(c.ellipse(13, by - 5.5 + squash * 0.5, 5.6 + squash, 5.2 - squash * 0.5), "F")
    for x in (9, 15):                                   # front paws: tucked on the ground, dangling in the air
        c.paint(c.rect(x, by - 3 + (1 if lift else 0), x + 3, by), "W")
    c.paint(c.ellipse(13, by - 5, 2.6, 3), "W")
    hy = by - 13.5
    c.paint(c.ellipse(13, hy, 7, 6), "F")
    c.paint(c.ellipse(13, hy + 2.8, 3.6, 2.6), "W")
    c.paint(c.ellipse(9.5, hy - 1.2, 2.6, 2.2), "R")    # a brown patch round one eye
    th = np.radians(90 - 60 * (flop + 1))               # 90 = hanging down, -30 = flung out and up
    for side in (-1, 1):
        ex, ey = 13 + side * 5.5, hy - 3
        cx, cy = ex + side * 3.2 * np.cos(th), ey + 3.2 * np.sin(th)
        ang = np.degrees(th) if side > 0 else 180 - np.degrees(th)
        c.paint(_rell(c, cx, cy, 3.8, 2.0, ang), "R")
    for x in (9, 15):
        c.paint(c.rect(x, int(hy) - 1, x + 2, int(hy) + 1), "E")
        c.set(x, int(hy) - 1, "W")
    c.paint(c.rect(12, int(hy) + 1, 14, int(hy) + 2), "N")
    c.set(11, int(hy) + 3, "K").set(14, int(hy) + 3, "K").set(12, int(hy) + 4, "K").set(13, int(hy) + 4, "K")
    if lift >= 3:
        c.paint(c.rect(12, int(hy) + 4, 14, int(hy) + 6), "T")
    c.paint(_chain(c, 18, by - 3.5, 30 + 30 * flop, 5, 0.9, 10, 1.1, 0.9), "F")
    g = _out(c)
    w = 4 - lift * 0.5                                   # a shadow on the floor, smaller as the puppy rises
    yy, xx = np.mgrid[0:g.shape[0], 0:g.shape[1]]
    sh = (((xx + 0.5 - 14) / (w + 2)) ** 2 + ((yy + 0.5 - 28) / 0.6) ** 2 <= 1) & (g == "")
    g[sh] = "h"
    return g


# ---- 8. old dog -------------------------------------------------------------------------------------------------

OLD = {"F": "#c94", "S": "#a63", "H": "#eb6", "B": "#edb", "M": "#bba", "R": "#743", "E": "#211", "W": "#fff",
       "N": "#211"}


def old_dog(k):
    """An old golden dog lies with its chin on its front paws: a rounded haunch with the hind foot tucked forward,
    a dip in the back to the shoulders, a big head with a long dark floppy ear, a grey muzzle and droopy eyes, its
    tail lying behind: it breathes slowly, one ear twitches, it blinks."""
    ear_up, shut = [(0, 0), (0, 0), (1, 0), (0, 0), (0, 1), (0, 0)][k]
    br = [0, 0.3, 0.6, 0.3, 0, 0][k]
    c = A.Canvas(46, 26)
    G = 25.0                                                                    # the ground

    def top(m):
        return m & np.vstack([np.ones((1, c.w), bool), ~m[:-1]])
    c.paint(_chain(c, 6.5, 21.5, 200, 9, 1.0, 5, 1.5, 1.0), "F")                # the tail lying behind
    c.paint(c.line(30, G - 1.6, 40.5, G - 1.4, 2.4) | c.ellipse(41.6, G - 1.4, 2.0, 1.2), "S")   # far foreleg
    body = _rell(c, 20, 19.6 - br / 2, 11.5, 5.0 + br / 2, -3)                   # the barrel, low
    body |= c.ellipse(27.5, 18.4, 5.2, 6.2)                                      # the chest and shoulders
    c.paint(body, "F")
    c.paint(body & c.ellipse(20, G + 0.8, 11, 2.6), "S")                        # the belly in shade
    c.paint(top(body) & (c.px > 14) & (c.px < 31), "H")                         # light along the back
    haunch = c.ellipse(11.0, 18.8 - br / 2, 6.2, 6.0)                          # the thigh, up above the back
    _edge(c, haunch & (c.px > 11))
    c.paint(haunch, "F")
    c.paint(top(haunch), "H")
    hind = c.ellipse(15.8, G - 1.4, 5.0, 1.4)                                   # the hind foot tucked forward
    _edge(c, hind)
    c.paint(hind, "F")
    c.paint(c.ellipse(19.8, G - 1.4, 1.6, 1.1), "B")
    neck = c.line(28.0, 16.0, 33.0, 14.5, 6.6)
    c.paint(neck, "F")
    c.paint(top(neck) & (c.g == "F"), "H")
    fore = c.line(30.5, G - 2.4, 41.0, G - 1.8, 2.8)                           # near foreleg, the paw out front
    _edge(c, fore)
    c.paint(fore, "F")
    paw = c.ellipse(42.4, G - 1.8, 2.2, 1.4)
    _edge(c, paw)
    c.paint(paw, "B")
    head = c.ellipse(35.2, 13.6, 5.6, 5.2) | _rell(c, 40.8, 17.9, 4.8, 2.6, 18)  # skull, the muzzle on the paws
    _edge(c, head)
    c.paint(head, "F")
    c.paint(top(head), "H")
    c.paint(head & (c.px > 39.4) & (c.py > 14.0), "M")                          # grey muzzle
    c.paint(head & c.ellipse(37.8, 11.2, 1.8, 0.9), "M")                        # grey brow
    c.set(44, 16, "N").set(45, 16, "N").set(45, 17, "N").set(44, 17, "N")       # the nose
    c.paint(c.line(41.0, 19.4, 43.6, 18.9, 0.9), "K")                           # the mouth
    if ear_up:                                                                  # twitched up and out
        ear = _rell(c, 31.8, 11.6, 4.6, 2.1, 50)
    else:                                                                       # the long ear hangs to the paws
        ear = _rell(c, 32.2, 15.4, 5.8, 2.1, 98)
    _edge(c, ear)
    c.paint(ear, "R")
    if shut:
        c.set(37, 14, "K").set(38, 14, "K").set(36, 13, "K")
    else:                                                                       # droopy: the lid half down
        c.set(37, 13, "K").set(38, 13, "K").set(37, 14, "E").set(38, 14, "W")
    return _out(c)


# ---- 9. wolf cub ------------------------------------------------------------------------------------------------

CUB = {"F": "#99a", "S": "#678", "B": "#dde", "E": "#fc3", "P": "#211", "W": "#fff", "N": "#d79", "T": "#e56",
       "M": "#bdf"}


def wolf_cub(k):
    """A grey wolf cub with a pale belly sits, lifts its head and howls (sound marks go up), then sits again."""
    head, mouth, sound = [(0, False, 0), (0, False, 0), (-25, False, 0), (-60, True, 1), (-60, True, 2),
                          (-20, False, 0)][k]
    c = A.Canvas(34, 36)
    nx, ny = sit_dog(c, 0, 8, 0.78, wolf=True, head=head, tail=0.3 * _wave(6, k), blink=k == 1,
                     ears=0.8 * (k == 2), mouth=mouth, hz=1.35)
    for i in range(sound):                                  # arcs rising from the open mouth
        r = 4.5 + 3.0 * i
        ring = c.disc(nx, ny, r + 0.6) & ~c.disc(nx, ny, r - 0.6)
        a = np.degrees(np.arctan2(ny - c.py, c.px - nx))
        c.paint(ring & (a > 15) & (a < 105), "M")
    return _out(c)


# ---- 10. cat and hound ------------------------------------------------------------------------------------------

PAIR = {**HOUND, **{k: v for k, v in anim2.PAL.items() if k != "K"}, "Hr": "#e35", "Hl": "#fbc"}
HEART = [".hh.hh.", "hhhhhhh", "hhhhhhh", ".hhhhh.", "..hhh..", "...h..."]


def pair(k):
    """The cat (anim2) and the hound sit together: the hound leans down and nuzzles the cat, who closes its eyes
    happily; a heart floats up; both tails swing."""
    head = [0, 22, 42, 42, 22, 0][k]
    W = 56
    g = _grid(W, 38)
    dog = A.Canvas(34, 36)
    sit_dog(dog, 0, 0, 1.0, head=head, tail=_wave(6, k), pant=0 if head else 1, hz=1.15)
    dog = A.flip(_out(dog))                                                    # facing left, toward the cat
    _paste(g, dog, W - dog.shape[1], 0)
    _paste(g, anim2.frame(k / 6, blink=k in (2, 3)), 0, 10)
    if k in (2, 3, 4):                                                         # a heart rises between them
        hc = np.where(A.ascii(HEART, {"h": 1}) == "h", "Hr", "")
        hc[1, 1] = "Hl"
        _paste(g, A.outline(hc, "K"), 18, [8, 4, 0][k - 2])
    return g



# ---- pieces -----------------------------------------------------------------------------------------------------

def _frames(fn, n, pal):
    return [_sprite(fn(k), pal) for k in range(n)]


def pieces():
    return [
        A.Piece("Kitten (moving, batting yarn)", "pixel", frames=_frames(kitten, 6, KITTEN), fps=6),
        A.Piece("Black cat (moving, stretch and tail)", "pixel", frames=_frames(black_cat, 8, BLACK), fps=4),
        A.Piece("Calico cat (moving, asleep)", "pixel", frames=_frames(calico, 4, CALICO), fps=2),
        A.Piece("White cat (moving, washing)", "pixel", frames=_frames(white_cat, 6, WHITE), fps=4),
        A.Piece("Siamese cat (moving, walking)", "pixel", frames=_frames(siamese, 4, SIAMESE), fps=6),
        A.Piece("Hound (moving, tail and panting)", "pixel", frames=_frames(hound, 4, HOUND), fps=6),
        A.Piece("Puppy (moving, bouncing)", "pixel", frames=_frames(puppy, 4, PUPPY), fps=6),
        A.Piece("Old dog (moving, ear and blink)", "pixel", frames=_frames(old_dog, 6, OLD), fps=3),
        A.Piece("Wolf cub (moving, howling)", "pixel", frames=_frames(wolf_cub, 6, CUB), fps=3),
        A.Piece("Cat and hound (moving, nuzzling)", "pixel", frames=_frames(pair, 6, PAIR), fps=3),
    ]
