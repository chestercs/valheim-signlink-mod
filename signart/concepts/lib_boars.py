"""Boars: the Meadows boar's whole family as 8-bit pixel sprites, the cat's family (concepts/anim2.py) and the boar
and piglet of concepts/lib_animals.py.

Every sprite uses the cat's pitch (5.6 cm a pixel), so a boar stands next to the cat at its true size and a piglet
is small beside it. A creature is drawn on its own Canvas facing left and outlined in the cat's dark outline, then
pasted into the scene, so a piglet keeps its rim in front of its mother. Every piece moves; the first frame is a
good still for players without the mod.
"""
import numpy as np

import artlib as A

PAL = {"B": "#a75", "L": "#c97", "D": "#532", "S": "#d98", "T": "#ffe", "P": "#111", "F": "#432", "G": "#863",
       "b": "#a64", "s": "#dc8", "n": "#e9a", "d": "#532", "f": "#432",
       "Hr": "#f6a", "Hl": "#fce", "Y": "#fd3", "y": "#c91",
       "W": "#753", "w": "#a74", "O": "#f80", "R": "#e32", "Q": "#fe6",
       "C": "#f82", "g": "#5a3", "r": "#c24", "M": "#543", "m": "#765", "U": "#987",
       "X": "#c33", "x": "#f77", "Z": "#bdf", "E": "#bba", "e": "#887"}


def _grid(w, h):
    return np.full((h, w), "", dtype=object)


def _paste(dst, src, x, y):
    """Non-empty pixels of src over dst, src's top left at (x, y); what falls outside is dropped."""
    src = np.asarray(src, dtype=object)
    h, w = src.shape
    H, W = dst.shape
    x0, y0, x1, y1 = max(x, 0), max(y, 0), min(x + w, W), min(y + h, H)
    if x0 >= x1 or y0 >= y1:
        return dst
    part = src[y0 - y:y1 - y, x0 - x:x1 - x]
    m = part != ""
    dst[y0:y1, x0:x1][m] = part[m]
    return dst


def _ascii(rows, key_of):
    g = A.ascii(rows, {ch: 1 for ch in key_of})
    return np.vectorize(lambda ch: key_of.get(ch, "") if ch else "")(g).astype(object)


HEART = _ascii([".h.h.", "hlhhh", "hhhhh", ".hhh.", "..h.."], {"h": "Hr", "l": "Hl"})
STAR = _ascii(["...Y...", "..YYY..", "YYYYYYY", ".YYYYy.", "..YYy..", ".Yy.yy.", ".y...y."], {"Y": "Y", "y": "y"})


def _wave(n, k, phase=0.0):
    return np.sin(2 * np.pi * (k / n + phase))


class _Sk:
    """Local coordinates scaled by s and moved by (ox, oy): one drawing at any size."""

    def __init__(self, c, ox, oy, s):
        self.c, self.ox, self.oy, self.s = c, ox, oy, s

    def p(self, u, v):
        return self.ox + u * self.s, self.oy + v * self.s

    def ell(self, u, v, rx, ry):
        return self.c.ellipse(*self.p(u, v), rx * self.s, ry * self.s)

    def line(self, u0, v0, u1, v1, w):
        return self.c.line(*self.p(u0, v0), *self.p(u1, v1), w * self.s)

    def tri(self, a, b, d):
        return self.c.triangle(self.p(*a), self.p(*b), self.p(*d))

    def box(self, u0, v0, u1, v1):
        x0, y0 = self.p(u0, v0)
        x1, y1 = self.p(u1, v1)
        return self.c.rect(int(round(x0)), int(round(y0)), max(int(round(x1)), int(round(x0)) + 1),
                           max(int(round(y1)), int(round(y0)) + 1))

    def dot(self, u, v, key):
        x, y = self.p(u, v)
        self.c.set(int(np.floor(x)), int(np.floor(y)), key)


def _eye(k, u, v, eyes):
    """An eye at (u, v): open (a dot), shut (a short line), happy (a small arch), wide (white with a pupil)."""
    if eyes == "open":
        k.dot(u, v, "P")
    elif eyes == "shut":
        k.dot(u - 1, v + 0.5, "D")
        k.dot(u, v + 0.5, "D")
    elif eyes == "happy":
        k.dot(u - 1, v + 1, "P")
        k.dot(u, v, "P")
        k.dot(u + 1, v, "P")
        k.dot(u + 2, v + 1, "P")
    elif eyes == "wide":
        k.dot(u - 1, v, "T")
        k.dot(u, v, "P")
        k.dot(u - 1, v - 1, "T")
        k.dot(u, v - 1, "T")


# ---- the boar ----------------------------------------------------------------------------------------------------

GALLOP = (((-4, 1), (-3, 2), (4, 1), (5, 2)),         # stretched out, flying
          ((-1, 0), (1, 1), (1, 2), (2, 1)),          # front feet land
          ((3, 0), (2, 1), (-3, 0), (-2, 1)),         # gathered, hind feet under the belly
          ((0, 2), (-1, 1), (2, 0), (3, 0)))          # hind feet push off


def boar(t=None, s=1.0, feet=None, head=0.0, eyes="open", tail=0.0, ear=0.0, bob=0.0, chew=0, back=False):
    """A boar facing left, outlined: high bristly shoulders, a wedge head with a long snout and a tusk, a darker
    mane. t is the walk phase (0..1, None stands); feet sets each foot's (dx, lift) instead (front near, front
    far, hind near, hind far; GALLOP); head drops the snout by px (negative lifts it); eyes open, shut, happy or
    wide; tail swings the tail (-1..1); ear lays the ear back (0..1); bob lifts the body; chew opens the mouth by
    px; back walks backwards (the feet lift on the way back)."""
    W, H = int(np.ceil(35 * s)), int(np.ceil(26 * s))
    c = A.Canvas(W, H)
    k = _Sk(c, 0, 4 * s, s)                                    # 4 local rows of room above for the head and mane
    by = -bob
    amp, lift = 1.8, 1.2
    legs = ((11, 10.5, "B", 0.0), (14, 14, "F", 0.5), (24, 24.5, "B", 0.5), (27, 27.5, "F", 0.0))
    for i, (hx, fx, key, ph) in sorted(enumerate(legs), key=lambda l: l[1][2] == "B"):   # far legs first
        fy = 20.0
        if feet is not None:
            fx += feet[i][0]
            fy -= feet[i][1] + bob
        elif t is not None:
            a = 2 * np.pi * (t + ph)
            sgn = -1 if back else 1
            fx -= sgn * amp * np.sin(a)
            fy -= lift * max(0.0, np.cos(a))
        c.paint(k.line(hx, 13 + by, fx, fy, 2), key)
        c.paint(k.box(fx - 1, fy, fx + 1, fy + 2), "F")
    body = k.ell(20, 12 + by, 10, 5.2) | k.ell(15, 9.5 + by, 6.5, 4.8)
    c.paint(body, "B")
    c.paint(k.ell(19, 14.6 + by, 7, 2.6), "L")
    x0, x1 = int(9 * s), int(28 * s)
    for x in range(x0, x1):                                   # the mane: tall at the shoulders, sloping down
        col = np.nonzero(body[:, x])[0]
        if not len(col):
            continue
        top = col.min()
        u = x / s
        h = (3 if u < 17 else (2 if u < 23 else 1)) * s
        h = int(round(h)) if x % 2 else int(round(h)) - 1
        c.paint(c.rect(x, top - h, x + 1, top + 1), "D")
    tx = 32 + 0.8 * tail
    c.paint(k.line(30, 10 + by, tx, 12.5 + by - abs(tail), 1.1), "D")
    c.paint(k.box(tx - 0.2, 12.6 + by - abs(tail), tx + 0.8, 13.6 + by - abs(tail)), "F")
    dy = head
    hb = by * 0.6
    c.paint(k.tri((13, 6.5 + hb), (13, 16 + dy * 0.5 + hb), (1.5, 14 + dy + hb))
            | k.ell(9.5, 11 + dy * 0.4 + hb, 4, 4.2), "B")
    c.paint(k.box(0, 13 + dy + hb, 3, 16 + dy + hb), "S")                         # snout
    k.dot(0, 13 + dy + hb, "P")
    if chew:
        c.paint(k.box(2, 16 + dy + hb, 7, 16 + dy + hb + chew), "D")
    c.paint(k.line(4, 16 + dy + hb, 5.5, 12.5 + dy + hb, 1.2) | k.line(3.8, 16 + dy + hb, 4.6, 16 + dy + hb, 1),
            "T")
    ey = 10 + dy * 0.6 + hb
    ex, ey2 = 13.5 + 2 * ear, 4 + 4 * ear
    c.paint(k.tri((10, 7.5 + dy * 0.3 + hb), (ex - 1, ey2 + dy * 0.3 + hb), (13.5, 8 + dy * 0.3 + hb)), "D")
    _eye(k, 8, ey, eyes)
    return A.outline(c.g, "K")


def piglet(t=None, up=0.0, eyes="open", head=0.0, tail=0, run=False):
    """A striped piglet facing left, outlined: a round body with pale stripes, a round head with a pink snout, a
    curly tail. t is the gait phase (None stands), up hops it px off the ground (legs tucked), head drops the head,
    tail curls the tail one way or the other."""
    c = A.Canvas(18, 16)
    by = 10 - up
    for i, x0 in enumerate((6, 8, 12, 14)):
        far = i in (1, 3)
        lx, ly = x0, by + 3
        if up:
            c.paint(c.rect(lx, int(by) + 3, lx + 1, int(by) + 4), "f")
            continue
        fx, fy = lx + 0.5, 15.0
        if t is not None:
            a = 2 * np.pi * (t + (0.5 if i in (1, 2) else 0.0))
            fx -= (1.6 if run else 1.0) * np.sin(a)
            fy -= max(0.0, np.cos(a)) * (1.2 if run else 0.8)
        c.paint(c.line(lx + 0.5, ly, fx, fy, 1.2), "f" if far else "b")
        c.set(int(np.floor(fx)), int(np.floor(fy)) - 1 if fy > 14.5 else int(np.floor(fy)), "f")
    body = c.ellipse(10.5, by, 5.5, 4)
    c.paint(body, "b")
    for y in (by - 2, by + 0.5):                                                 # pale stripes
        c.paint(body & c.rect(7, int(y), 17, int(y) + 1), "s")
    hy = by - 1 + head
    c.paint(c.disc(5, hy, 3.4), "b")
    c.paint(c.rect(1, int(hy) - 1, 3, int(hy) + 1), "n")
    c.set(1, int(hy) - 1, "d")
    c.paint(c.triangle((5, hy - 3.5), (7.5, hy - 6), (8, hy - 3)), "d")
    k = _Sk(c, 0, 0, 1)
    _eye(k, 4, hy - 2, eyes)
    curl = ((16, -2), (17, -3), (16, -4), (15, -3)) if tail >= 0 else ((16, -2), (17, -1), (17, -3), (16, -4))
    for x, y in curl:                                                            # curly tail
        c.set(x, int(by) + y, "n")
    return A.outline(c.g, "K")


def piglet_lying(breath=0, eyes="shut", head=0.0):
    """A piglet lying on its belly, facing left, legs tucked under it, chin on the ground; breath swells the back."""
    c = A.Canvas(19, 12)
    body = c.ellipse(11, 8.5 - 0.5 * breath, 6.5, 3.5 + 0.5 * breath) & (c.py < 11.5)
    c.paint(body, "b")
    for y in (6, 8):
        c.paint(body & c.rect(9, y, 18, y + 1), "s")
    c.paint(c.rect(7, 11, 9, 12) | c.rect(14, 11, 16, 12), "f")
    hy = 8 + head
    c.paint(c.ellipse(5, hy, 3.6, 3.1), "b")
    c.paint(c.rect(1, int(hy), 3, int(hy) + 2), "n")
    c.set(1, int(hy), "d")
    c.paint(c.triangle((5, hy - 2.5), (8, hy - 5), (8.5, hy - 2)), "d")
    _eye(_Sk(c, 0, 0, 1), 4, hy - 1, eyes)
    for x, y in ((17, 6), (18, 5), (17, 4)):
        c.set(x, y, "n")
    return A.outline(c.g, "K")


def boar_lying(eyes="happy", breath=0, ear=0.0):
    """A boar lying on its belly facing left, legs folded under, the head resting low; breath swells the back."""
    c = A.Canvas(36, 18)
    k = _Sk(c, 0, 0, 1)
    body = (k.ell(20, 10.5 - 0.5 * breath, 11, 5 + 0.5 * breath) | k.ell(15, 9 - 0.5 * breath, 6.5, 4.8)) \
        & (c.py < 16)
    c.paint(body, "B")
    c.paint(k.ell(20, 15, 9, 1.5) & body, "L")
    for x in range(9, 30):
        col = np.nonzero(body[:, x])[0]
        top = col.min()
        h = 3 if x < 17 else (2 if x < 24 else 1)
        c.paint(c.rect(x, top - (h if x % 2 else h - 1), x + 1, top + 1), "D")
    c.paint(c.rect(10, 15, 14, 17) | c.rect(25, 15, 28, 17), "F")                # folded legs, hooves showing
    c.paint(k.line(31, 9, 33, 11, 1.1), "D")
    c.set(33, 12, "F")
    c.paint(k.tri((13, 6), (13, 16), (1.5, 15)) | k.ell(9.5, 11.5, 4, 4), "B")
    c.paint(c.rect(0, 13, 3, 16), "S")
    c.set(0, 13, "P")
    c.paint(k.line(4, 16, 5.5, 12.5, 1.2) | k.line(3.8, 16, 4.6, 16, 1), "T")
    c.paint(k.tri((10, 8), (13.5 + 2 * ear, 4 + 4 * ear), (13.5, 8.5)), "D")
    _eye(k, 8, 10.5, eyes)
    return A.outline(c.g, "K")


def boar_side(t, roll=0):
    """A boar lying on its side, head at the left in profile resting low: the back up, the pale belly below,
    legs stretched out and kicking a little (t, 0..1), eyes shut with joy, mud spots on its flank; roll (-1..1)
    rocks it, showing more back or more belly."""
    c = A.Canvas(40, 22)
    k = _Sk(c, 0, 0, 1)
    for hx, hy, fx, fy, key, ph in ((13, 13, 7, 18, "F", 0.5), (15, 14, 10, 19.5, "B", 0.0),
                                    (26, 13, 33, 17, "F", 0.0), (28, 14, 35, 19, "B", 0.5)):
        a = 2 * np.pi * (t + ph)
        fx, fy = fx + 1.2 * np.sin(a), fy - 0.8 * max(0.0, np.cos(a))
        c.paint(k.line(hx, hy - roll * 0.5, fx, fy, 2.2), key)
        c.paint(k.box(fx - 1, fy - 1, fx + 1, fy + 1), "F")
    body = k.ell(21, 11 - roll * 0.5, 11.5, 5.5)
    c.paint(body, "B")
    c.paint(body & k.ell(21, 15.5 - roll, 9.5, 2.6), "L")                         # the belly, more of it rolled up
    for x in range(16, 31):                                                      # the mane along the back
        col = np.nonzero(body[:, x])[0]
        top = col.min()
        h = (2 if x < 22 else 1) + (roll < 0)
        c.paint(c.rect(x, top - (h if x % 2 else h - 1), x + 1, top + 1), "D")
    c.paint(k.line(32, 10 - roll * 0.5, 35, 8 - roll, 1.1), "D")                 # tail
    c.set(35, int(7 - roll), "F")
    hy = 12 + roll * 0.5
    c.paint(k.tri((14, hy - 6), (14, hy + 4), (2, hy + 3)) | k.ell(10, hy, 4.4, 4.4), "B")   # head in profile
    c.paint(k.box(0, hy + 1, 3, hy + 4), "S")                                    # snout
    k.dot(0, hy + 1, "P")
    c.paint(k.line(4.5, hy + 4, 6, hy + 0.5, 1.2) | k.line(4.3, hy + 4, 5.1, hy + 4, 1), "T")   # tusk
    c.paint(k.tri((9.5, hy - 3), (13, hy - 8.5), (13.5, hy - 2.5)), "D")       # ear
    _eye(k, 8, hy - 2, "happy")
    for x, y in ((17, 9), (22, 7), (26, 10), (19, 12), (29, 12), (24, 13)):      # mud spots on the flank
        c.set(x, int(y - roll * 0.5), "M")
    return A.outline(c.g, "K")


# ---- pieces ------------------------------------------------------------------------------------------------------

def _frames(fn, n):
    return [A.colours(fn(k), {**PAL, "K": A.OUTLINE}) for k in range(n)]


# ---- 1. tame boar, petted ----------------------------------------------------------------------------------------

def petted(k):
    """A tamed boar stands with its eyes shut happily, tail wagging, while pink hearts rise over its back."""
    g = _grid(40, 38)
    up = [0, 1, 1, 0, 0, 0][k]
    _paste(g, boar(eyes="happy" if k not in (5,) else "open", tail=_wave(6, k) * 1.2, head=-up, bob=up * 0.5),
           2, 12)
    for i, (x, ph) in enumerate(((12, 0), (22, 2), (30, 4))):
        step = (k + ph) % 6
        if step < 5:
            _paste(g, A.outline(HEART, "K"), x + [0, 1, 0, -1, 0][step], 14 - 3 * step)
    return g


# ---- 2. charging boar --------------------------------------------------------------------------------------------

def charge(k):
    """A boar charges at full gallop, head down and tusk forward, kicking up dust clouds behind it."""
    g = _grid(56, 30)
    bob = [2, 0, 0, 1][k]
    c = A.Canvas(56, 30)
    m = np.zeros((30, 56), bool)
    for i in range(3):                                                         # dust clouds rolling away behind
        ph = ((k + i * 4 / 3) % 4) / 4
        x = 34 + 18 * ph
        r = 1.6 + 2.6 * ph
        m |= c.disc(x, 27 - r * 0.7, r) | c.disc(x + r * 0.8, 27.5 - r * 0.4, r * 0.7)
    dust = _grid(56, 30)
    dust[m] = "E"
    dust[m & (c.py > 26)] = "e"
    _paste(g, A.outline(dust, "K")[1:-1, 1:-1], 0, 0)
    _paste(g, boar(feet=GALLOP[k], head=1.5, bob=bob, ear=1.0, tail=1.0), 4, 3)
    for i, y in enumerate((11, 15)):                                        # speed lines
        x = 41 + 3 * i + (k % 2) * 2
        g[y - bob, x:x + 4] = "e"
    return g


# ---- 3. boar afraid of a torch -----------------------------------------------------------------------------------

def torch_fear(k):
    """A torch stands burning at the left; the boar faces it with wide eyes and its ear laid back, backing away
    step by step, then shuffling back up; the flame flickers."""
    g = _grid(56, 34)
    tc = A.Canvas(10, 34)
    tc.paint(tc.rect(4, 12, 6, 34), "W")
    tc.paint(tc.rect(3, 10, 7, 13), "w")
    fl = [0, 1, 0, -1][k % 4]
    flame = tc.ellipse(5 + fl * 0.5, 7, 3, 4) | tc.triangle((3, 6), (5 + fl, -1), (7, 6))
    tc.paint(flame, "R")
    tc.paint(tc.ellipse(5 + fl * 0.3, 8, 2, 2.6) | tc.triangle((4, 7), (5 + fl * 0.6, 2), (6, 7)), "O")
    tc.paint(tc.ellipse(5, 9, 1, 1.4), "Q")
    _paste(g, A.outline(tc.g, "K")[1:-1], 0, 0)
    for x, y in (((1, 3), (9, 1)) if k % 2 else ((9, 4), (1, 0))):                # sparks
        g[y, x] = "Q"
    xo = [0, 3, 5, 5, 4, 3, 2, 1][k]
    t = [0, 0.5, 0.0, None, 0.25, 0.5, 0.75, 0.0][k]
    fright = k in (1, 2, 3)
    _paste(g, boar(t, eyes="wide", ear=1.0, head=-2 if fright else 0.5, back=fright, tail=-1 if fright else 0,
                   bob=1 if k == 1 else 0), 13 + xo, 9)
    if fright:                                                                 # a startled mark over its head
        _paste(g, A.outline(_ascii(["Q", "Q", "Q", ".", "Q"], {"Q": "Q"}), "K"), 22 + xo, 1 + (k == 1))
    return g


# ---- 4. mother and piglets ---------------------------------------------------------------------------------------

def mother(k):
    """A mother boar lies resting with her eyes shut happily; three striped piglets snuggle against her side,
    wriggling in turn, their tails curling."""
    g = _grid(56, 30)
    _paste(g, boar_lying(eyes="open" if k == 0 else "happy", breath=k % 2), 1, 2)
    for i, x in enumerate((13, 24, 35)):
        ph = (k + 2 * i) % 6
        _paste(g, piglet_lying(eyes="shut", head=[0, -1, -1, 0, 0, 0][ph], breath=ph % 2), x,
               15 - (ph in (1, 2)))
    return g


# ---- 5. a piglet is born -----------------------------------------------------------------------------------------

def _puff(r, holes=0.0):
    """A round red cloud of r px, with holes (0..1) eaten out of it as it clears."""
    c = A.Canvas(22, 20)
    m = np.zeros((20, 22), bool)
    for dx, dy, f in ((0, 0, 1.0), (-0.6, -0.3, 0.7), (0.6, -0.35, 0.7), (-0.5, 0.4, 0.6), (0.55, 0.4, 0.6),
                      (0, -0.7, 0.55)):
        m |= c.disc(11 + dx * r, 11 + dy * r, f * r * 0.65)
    if holes:
        rng = np.random.default_rng(3)
        n = rng.random((20, 22))
        m &= n > holes
    c.paint(m, "X")
    c.paint(m & c.disc(11 - r * 0.3, 11 - r * 0.35, r * 0.4), "x")
    return A.outline(c.g, "K")


def born(k):
    """A tamed mother boar stands by as a little red cloud puffs up beside her and clears, showing a new striped
    piglet; a heart pops over its head."""
    g = _grid(58, 32)
    _paste(g, boar(eyes="happy" if k in (0, 1, 7) else "open", tail=_wave(8, k)), 22, 4)
    stage = ["pig", "pig", "none", "puff1", "puff2", "puff3", "clear", "pig"][k]
    if stage in ("pig", "clear"):
        _paste(g, A.flip(piglet(tail=1, eyes="open")), 2, 14)
    if stage == "puff1":
        _paste(g, _puff(4), 0, 9)
    elif stage == "puff2":
        _paste(g, _puff(8), 0, 9)
    elif stage == "puff3":
        _paste(g, _puff(10), 0, 9)
    elif stage == "clear":                                                     # the cloud breaks into wisps
        for dx, dy in ((-7, -5), (6, -6), (-8, 3), (7, 2)):
            _paste(g, _puff(3), dx, 9 + dy)
    if k in (0, 1):
        _paste(g, A.outline(HEART, "K"), 6, 7 - k)
    return g


# ---- 6. piglets at play ------------------------------------------------------------------------------------------

def play(k):
    """Two piglets chase each other: the leader hops along, the follower runs after and bumps its tail, then they
    bounce apart."""
    g = _grid(44, 22)
    n = 8
    t = k / n
    lead_up = [0, 2, 4, 2, 0, 0, 0, 0][k]
    lx = 2 + [0, 0, 0, 0, 0, 1, 2, 1][k]
    fx = 22 - [0, 1, 2, 3, 4, 2, 0, 0][k]
    fup = [0, 0, 0, 0, 0, 2, 3, 1][k]
    _paste(g, piglet(t=None if lead_up else t, up=lead_up, eyes="happy" if lead_up else "open", tail=1), lx, 4)
    _paste(g, piglet(t=None if fup else (t * 2) % 1, up=fup, run=True, eyes="open", tail=-1), fx, 4)
    if k in (4, 5):                                                              # the bump
        for x, y in ((21, 6), (22, 4), (24, 5)):
            g[y, x] = "Q"
    return g


# ---- 7. piglet asleep --------------------------------------------------------------------------------------------

def _zee(g, x, y, size):
    for i in range(size):
        g[y, x + i] = "Z"
        g[y + size - 1, x + i] = "Z"
    for i in range(1, size - 1):
        g[y + i, x + size - 1 - i] = "Z"


def asleep(k):
    """A piglet sleeps curled on its belly, eyes shut, its back rising and falling, z marks drifting up."""
    g = _grid(30, 26)
    _paste(g, piglet_lying(breath=[0, 1, 1, 0][k], eyes="shut"), 1, 12)
    z = _grid(30, 26)
    for kk in (k, (k + 2) % 4):                                                  # two z marks drifting up
        _zee(z, 13 + 2 * kk, 11 - 3 * kk, 4 if kk < 2 else 5)
    _paste(g, A.outline(z, "K")[1:-1, 1:-1], 0, 0)
    return g


# ---- 8. boar at the trough ---------------------------------------------------------------------------------------

def trough(k):
    """A boar eats from a wooden feeding trough heaped with carrots and raspberries: its head dips in, it lifts
    a mouthful and chews, crumbs falling."""
    g = _grid(52, 38)
    dip = [0, 4, 7, 7, 3, 0][k]
    _paste(g, boar(head=dip, chew=1 if k in (4, 5) else 0, eyes="happy" if k in (2, 3, 4) else "open",
                   tail=_wave(6, k)), 15, 4)
    c = A.Canvas(24, 11)
    c.paint(c.rect(0, 0, 24, 3) & (c.ellipse(12, 2.5, 12, 2.2)), "M")          # the heap's shade
    for x, key in ((1, "C"), (4, "r"), (6, "C"), (9, "r"), (11, "C"), (14, "r"), (16, "C"), (19, "r"), (21, "C")):
        if key == "C":
            c.paint(c.rect(x, 2, x + 2, 4), "C")
            c.set(x + 1, 1, "g").set(x, 0 if x % 2 else 1, "g")
        else:
            c.paint(c.rect(x, 2, x + 2, 4), "r")
            c.set(x, 2, "x")
    c.paint(c.rect(0, 4, 24, 9), "W")
    c.paint(c.rect(1, 5, 23, 6), "w")
    c.paint(c.rect(1, 9, 3, 11) | c.rect(21, 9, 23, 11), "W")
    _paste(g, A.outline(c.g, "K"), 0, 25)
    if k in (4, 5):                                                            # crumbs from the mouthful
        g[[24, 26][k - 4], 26] = "C"
        g[[25, 27][k - 4], 28] = "r"
    return g


# ---- 9. boar in the mud ------------------------------------------------------------------------------------------

def mud(k):
    """A boar wallows on its side in a mud puddle, eyes shut with joy: it rocks back and forth, its legs kick a
    little, mud splashes fly up and its flank is spotted with mud."""
    g = _grid(52, 30)
    roll = [0, 1, 0, -1][k]
    _paste(g, boar_side(k / 4, roll=roll), 5, 2 + (roll > 0))
    c = A.Canvas(52, 30)
    c.paint(c.ellipse(26, 26, 25, 3.5), "M")
    c.paint(c.ellipse(14, 25, 5, 0.8) | c.ellipse(38, 27, 5, 0.8), "m")
    _paste(g, A.outline(c.g, "K")[1:-1, 1:-1], 0, 0)
    for side, x0 in ((-1, 4), (1, 47)):                                          # splashes flying up
        h = [3, 6, 4, 1][(k + (side > 0) * 2) % 4]
        for dx, dy in ((0, 0), (side * 2, 2), (-side, 3)):
            x, y = x0 + dx, 21 - h + dy
            g[y, x] = "M"
            g[y - 1, x] = "m"
    return g


# ---- 10. two-star boar -------------------------------------------------------------------------------------------

STARPAL = {**PAL, "B": "#753", "L": "#986", "D": "#321", "S": "#c87", "F": "#211"}


def two_star(k):
    """A two-star boar, bigger and darker than a plain one, stands proud with its snout up and tail swinging;
    two gold stars bob over its head in turn."""
    g = _grid(50, 46)
    up = [0, 1, 1, 0][k]
    _paste(g, boar(s=1.3, head=-1.5 - up, tail=_wave(4, k)), 2, 10)
    for i, x in enumerate((7, 18)):
        _paste(g, A.outline(STAR, "K"), x, 3 + (1 if (k + 2 * i) % 4 < 2 else 0))
    return g


# ---- 11. piglet with a carrot ------------------------------------------------------------------------------------

def carrot_pig(k):
    """A piglet holds a carrot in its snout and nibbles it: its head bobs, the carrot jiggles, orange crumbs fall
    and the piglet shuts its eyes happily."""
    g = _grid(28, 20)
    bob = [0, 1, 0, 1, 0, 0][k]
    pig = piglet(head=bob, eyes="happy" if k in (2, 3) else "open", tail=1 if k % 2 else -1)
    car = _ascii(["......g.g",
                  ".......g.",
                  "....CCCC.",
                  "..CCwCC..",
                  "CCC......"] if k % 2 == 0 else
                 ["......g.g",
                  ".......g.",
                  ".....CCC.",
                  "..CCCwC..",
                  ".CCC.....",
                  "C........"], {"C": "C", "w": "w", "g": "g"})
    _paste(g, A.outline(car, "K"), 1, 8 + bob)
    _paste(g, pig, 8, 3)
    if k in (1, 3):
        g[17 + k // 2, 4 + k] = "C"
    if k in (2, 4):
        g[19, 7] = "C"
    return g


# ---- 12. the family walks ----------------------------------------------------------------------------------------

def family(k):
    """A boar family walks in a line, the mother first and two striped piglets trotting after her, all in step
    in place, heads nodding."""
    g = _grid(64, 30)
    t = k / 4
    _paste(g, boar(t, tail=_wave(4, k) * 0.6, head=[0, 0.5, 0, 0.5][k]), 0, 0)
    for i, x in enumerate((31, 45)):
        ph = (t + 0.25 + i * 0.5) % 1
        _paste(g, piglet(ph, tail=1, head=[0, 1][(k + i) % 2]), x, 12)
    return g


# ---- extras ------------------------------------------------------------------------------------------------------

def flower_crown(k):
    """A tamed boar wearing a crown of dandelions, blue and pink flowers stands content: it blinks, its tail
    swings and one flower nods."""
    g = _grid(38, 30)
    b = boar(eyes="shut" if k == 3 else "happy" if k in (1, 2) else "open", tail=_wave(4, k), head=-0.5)
    _paste(g, b, 0, 2)
    cr = _ascii(["..a.......",
                 ".aaa.b.c..",
                 "ggaggbgccg",
                 ".gggggggg."], {"a": "Y", "b": "Z", "c": "Hr", "g": "g"})
    if k % 2:
        cr[0, 2], cr[0, 3] = "", "Y"
    _paste(g, A.outline(cr, "K"), 4, 10)
    return g


# ---- a still: the boar trophy ------------------------------------------------------------------------------------

def trophy():
    """A boar's head trophy seen from the front, mounted on a round wooden plaque: bristly brow, small ears,
    a pink snout and two tusks curling up."""
    p = A.Canvas(28, 30)
    p.paint(p.ellipse(14, 15, 13, 14), "F")
    p.paint(p.ellipse(14, 15, 11.5, 12.5), "W")
    p.paint(p.ellipse(10, 9, 5, 2.5) & p.ellipse(14, 15, 11.5, 12.5), "w")      # a gleam on the wood
    g = A.outline(p.g, "K")
    c = A.Canvas(28, 30)
    c.paint(c.triangle((7, 11), (4, 4), (11, 8)) | c.triangle((21, 11), (24, 4), (17, 8)), "D")
    c.paint(c.ellipse(14, 14, 7, 7.5) | c.ellipse(14, 18, 8, 5), "B")
    c.paint(c.triangle((5.5, 6.5), (5, 5), (8, 8)) | c.triangle((22.5, 6.5), (23, 5), (20, 8)), "S")
    for x in range(9, 20):                                                       # the bristly brow
        c.paint(c.rect(x, 6 + (x % 2), x + 1, 10), "D")
    c.paint(c.ellipse(14, 22.5, 4, 2.6), "L")
    c.paint(c.ellipse(14, 20, 3.6, 2.6), "S")
    c.set(13, 20, "F").set(15, 20, "F")
    c.paint(c.line(10, 22, 8.5, 17.5, 1.4) | c.line(18, 22, 19.5, 17.5, 1.4), "T")
    c.set(10, 13, "P").set(17, 13, "P").set(10, 12, "D").set(17, 12, "D")
    return _paste(g, A.outline(c.g, "K"), 0, 0)


def pieces():
    return [
        A.Piece("Tame boar (moving, petted)", "pixel", frames=_frames(petted, 6), fps=4),
        A.Piece("Charging boar (moving)", "pixel", frames=_frames(charge, 4), fps=8),
        A.Piece("Boar and torch (moving, backing away)", "pixel", frames=_frames(torch_fear, 8), fps=5),
        A.Piece("Mother boar (moving, piglets snuggling)", "pixel", frames=_frames(mother, 6), fps=3),
        A.Piece("Piglet born (moving, red puff)", "pixel", frames=_frames(born, 8), fps=4),
        A.Piece("Piglets at play (moving, chasing)", "pixel", frames=_frames(play, 8), fps=6),
        A.Piece("Sleeping piglet (moving)", "pixel", frames=_frames(asleep, 4), fps=2),
        A.Piece("Boar at the trough (moving, eating)", "pixel", frames=_frames(trough, 6), fps=4),
        A.Piece("Muddy boar (moving, rolling)", "pixel", frames=_frames(mud, 4), fps=5),
        A.Piece("Two-star boar (moving, proud)", "pixel",
                frames=[A.colours(two_star(k), {**STARPAL, "K": A.OUTLINE}) for k in range(4)], fps=3),
        A.Piece("Piglet with carrot (moving, nibbling)", "pixel", frames=_frames(carrot_pig, 6), fps=5),
        A.Piece("Boar family (moving, walking)", "pixel", frames=_frames(family, 4), fps=5),
        A.Piece("Flower crown boar (moving, blinking)", "pixel", frames=_frames(flower_crown, 4), fps=3),
        A.Piece("Boar trophy", "pixel", frames=[A.colours(trophy(), {**PAL, "K": A.OUTLINE})]),
    ]
