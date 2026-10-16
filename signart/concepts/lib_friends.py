"""Friends: the ginger pixel cat (concepts/anim2.py) and its companions, dogs and Valheim animals, often together.

Drawn like the cat and its family (lib_pets, lib_animals): numpy masks on a Canvas, outlined automatically in the
cat's dark outline, at the cat's pitch, so every friend stands next to the cat at its true size. Wherever the cat
sits facing us it is anim2's own frame; walking or nose to nose it is lib_pets' side cat in the cat's ginger.
Scenes are composed from finished sprites (each with its own outline), back to front. Every piece moves; the first
frame is a good still for players without the mod.
"""
import numpy as np

import artlib as A
from concepts import anim2
from concepts import lib_animals as AN
from concepts import lib_pets as PETS


def _wave(n, k, phase=0.0):
    return np.sin(2 * np.pi * (k / n + phase))


def _grid(w, h):
    return np.full((h, w), "", dtype=object)


def _paste(dst, src, x, y):
    """Non-empty pixels of src over dst, src's top left at (x, y); parts outside dst are cut off."""
    src = np.asarray(src, dtype=object)
    h, w = src.shape
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + w, dst.shape[1]), min(y + h, dst.shape[0])
    if x1 <= x0 or y1 <= y0:
        return dst
    s = src[y0 - y:y1 - y, x0 - x:x1 - x]
    view = dst[y0:y1, x0:x1]
    m = s != ""
    view[m] = s[m]
    return dst


def _finish(c, pal):
    """Canvas (or key grid) -> outlined '#rgb' rows as an object array, ready to paste."""
    g = c.g if isinstance(c, A.Canvas) else c
    return np.asarray(A.colours(A.outline(g, "K"), {**pal, "K": A.OUTLINE}), dtype=object)


def _rows(g):
    return [list(r) for r in np.asarray(g, dtype=object)]


def _zee(g, x, y, size, col):
    """A small 'z' of colour col with its top left at (x, y), straight into a colour grid."""
    pts = [(x + i, y) for i in range(size)] + [(x + i, y + size - 1) for i in range(size)]
    pts += [(x + size - 1 - i, y + i) for i in range(1, size - 1)]
    for px, py in pts:
        if 0 <= py < g.shape[0] and 0 <= px < g.shape[1]:
            g[py, px] = col


HEART = [".hh.hh.", "hhhhhhh", "hhhhhhh", ".hhhhh.", "..hhh..", "...h..."]


def _heart():
    h = A.ascii(HEART, {"h": 1})
    h[1, 1] = "l"
    return _finish(h, {"h": "#e35", "l": "#fbc"})


# ---- the cat ----------------------------------------------------------------------------------------------------

def cat(t=0.0, blink=False, look=None, rise=0):
    """anim2's ginger cat as a colour grid (30 x 27: one empty row on top for breathing). look 'up' or 'right'
    moves the pupils; rise 1 lifts the head a pixel (a breath in)."""
    g = anim2.frame(t, blink=blink)
    if look and not blink:
        for x in (8, 15):                                   # the eyes' left columns (padded coordinates)
            g[8:11, x:x + 2] = "G"
            if look == "up":
                g[8:10, x:x + 2] = "P"
                g[8, x] = "Wt"
            elif look == "right":
                g[8:11, x + 1] = "P"
                g[8, x] = "Wt"
    g = np.pad(g, ((1, 0), (0, 0)), constant_values="")
    if rise:
        r = 15                                              # the chin line: rows above it move up
        g = np.vstack([g[1:r + 1], g[r:]])
    return np.asarray(anim2.colours(g), dtype=object)


GINGER = {"F": "#e73", "S": "#b42", "X": "#e73", "H": "#fa5", "E": "#5d5", "P": "#111", "N": "#f8a"}


def side_ginger(**kw):
    """The cat seen from the side (lib_pets.side_cat) in anim2's ginger, facing right, 42 x 28."""
    return np.asarray(A.colours(PETS.side_cat(**kw), {**GINGER, "K": A.OUTLINE}), dtype=object)


# ---- 1. sled dog --------------------------------------------------------------------------------------------------

HUSKY = {"F": "#889", "D": "#556", "W": "#eef", "S": "#bbc", "E": "#4bf", "P": "#124", "N": "#211", "I": "#fbc",
         "M": "#622", "T": "#e56"}


def husky(k):
    """A grey and white sled dog sits facing us with blue eyes and a happy open mouth: the tail curled over its back
    wags, one ear flicks, it pants and blinks."""
    tail, flick, pant, blink = [(0, 0, 1, 0), (1, 0, 2, 0), (0, 1, 1, 0), (1, 0, 2, 0), (0, 0, 1, 1),
                                (1, 0, 2, 0)][k]
    c = A.Canvas(36, 36)
    cx = 16
    c.paint(c.ellipse(7.5, 31, 4.2, 4.5) | c.ellipse(24.5, 31, 4.2, 4.5), "F")     # haunches
    c.paint(c.ellipse(6.5, 34.5, 3, 1.4) | c.ellipse(25.5, 34.5, 3, 1.4), "W")     # hind paws
    c.paint(c.ellipse(cx, 26, 8.5, 8.5), "F")
    c.paint(c.ellipse(cx, 24, 4, 6.5), "W")                                        # chest
    c.paint(c.rect(11, 27, 14, 36) | c.rect(18, 27, 21, 36), "W")                  # front legs
    c.paint(c.rect(11, 35, 14, 36) | c.rect(18, 35, 21, 36), "S")                   # toes
    c.set(12, 35, "W").set(19, 35, "W")
    ta = 75 - 25 * tail                                     # the plume rises at the side and curls over
    plume = PETS._chain(c, 27.5, 33, ta, 11, 1.1, 12 + 6 * tail, 2.3, 1.7)
    c.paint(plume, "F")
    c.paint(plume & ~PETS._chain(c, 27.9, 33.4, ta, 11, 1.1, 12 + 6 * tail, 1.3, 0.8), "W")
    # head: tall pointed ears, the grey cap with a widow's peak, the white mask
    hy = 12
    for side in (-1, 1):
        f = flick if side < 0 else 0
        base_o, base_i = (cx + side * 8.2, hy - 1.5), (cx + side * 2.6, hy - 5.5)
        tip = (cx + side * (7.0 + 3.5 * f), hy - 11.5 + 4.5 * f)
        c.paint(c.triangle(base_o, tip, base_i), "F")
        c.paint(c.triangle((cx + side * 6.6, hy - 3.2), (tip[0] - side * 0.6, tip[1] + 2.6),
                           (cx + side * 3.8, hy - 5.2)), "I")
    c.paint(c.ellipse(cx, hy, 8.4, 6.8), "F")
    c.paint(c.ellipse(cx, hy + 2.6, 7.2, 4.6), "W")                                 # the mask
    c.paint(c.ellipse(cx - 3.6, hy - 2.4, 1.8, 1.2) | c.ellipse(cx + 3.6, hy - 2.4, 1.8, 1.2), "W")   # brows
    c.paint(c.triangle((cx - 2.2, hy - 7), (cx + 2.2, hy - 7), (cx, hy + 1)), "D")  # the peak
    c.paint(c.ellipse(cx, hy - 5.5, 4, 1.3), "D")
    ey = hy - 1
    for x in (cx - 5, cx + 3):
        if blink:
            c.set(x, ey + 1, "K").set(x + 1, ey + 1, "K")
        else:
            c.paint(c.rect(x, ey, x + 2, ey + 2), "E")
            c.set(x + (1 if x > cx else 0), ey + 1, "P")
            c.set(x + (0 if x > cx else 1), ey, "W")
    c.paint(c.rect(cx - 1, hy + 1, cx + 1, hy + 3), "N")                           # nose
    c.set(cx - 2, hy + 1, "N").set(cx + 1, hy + 1, "N")
    # the happy open mouth: a dark smile with the tongue out
    c.paint(c.ellipse(cx, hy + 5, 3.2, 1.6) & (c.py > hy + 4), "M")
    c.set(cx - 3, hy + 4, "K").set(cx + 2, hy + 4, "K")
    c.paint(c.rect(cx - 1, hy + 5, cx + 1, hy + 5 + pant) | c.ellipse(cx, hy + 5 + pant, 1.2, 1.0), "T")
    return _finish(c, HUSKY)


# ---- 2. dog with a bone ---------------------------------------------------------------------------------------------

BONEDOG = {"F": "#c84", "S": "#964", "W": "#fed", "R": "#742", "E": "#211", "N": "#211", "Wt": "#fff",
           "B": "#ffe", "Bs": "#bba", "T": "#e56"}


def bone_dog(k):
    """A brown and white dog lies facing left with a bone held up between its front paws, gnawing the top: the
    round head with its floppy ear dips and the jaw works; the tail wags behind it."""
    dy, jaw, tail = [(0, 0, 0), (1, 1, 1), (0, 0, 2), (1, 1, 1)][k]
    c = A.Canvas(46, 24)
    ta = [30, 65, 100][tail]                                                        # the wagging tail
    tl = PETS._chain(c, 40.5, 15.5, ta, 9, 1.0, -8, 1.5, 1.0)
    c.paint(tl, "F")
    c.paint(tl & ~PETS._chain(c, 40.5, 15.5, ta, 7, 1.0, -8, 1.7, 1.2), "W")
    c.paint(c.line(21, 20, 9, 20, 2.6), "S")                                         # far front leg
    c.paint(c.ellipse(7.5, 20.2, 2.2, 1.5), "S")
    body = c.ellipse(28, 16.5, 11.5, 5.5)
    c.paint(body, "F")
    c.paint(body & c.ellipse(27, 21.5, 9, 2.2), "W")                                  # pale belly
    haunch = c.ellipse(35, 15.5, 6, 6)
    c.paint(haunch, "F")
    c.paint(haunch & ~c.ellipse(34.4, 16, 5.0, 5.2) & (c.px > 34), "S")
    c.paint(c.ellipse(32, 21.8, 5, 1.4), "F")                                         # hind paw
    c.paint(c.ellipse(28, 21.9, 1.8, 1.1), "W")
    c.paint(c.ellipse(20.5, 16.5, 3.6, 4.6), "W")                                    # white chest
    # the bone, held up between the paws
    bx0, by0, bx1, by1 = 7.5, 20.5, 9.8, 14.4 + dy
    bone = c.line(bx0, by0, bx1, by1, 1.8) | c.disc(bx0 - 0.9, by0 + 0.4, 1.2) | c.disc(bx0 + 0.6, by0 + 0.9, 1.2) \
        | c.disc(bx1 - 0.6, by1 - 0.8, 1.2) | c.disc(bx1 + 0.9, by1 - 0.4, 1.2)
    c.paint(bone, "B")
    c.paint(bone & c.line(bx0 + 0.7, by0, bx1 + 0.7, by1, 0.7), "Bs")
    c.paint(c.line(22, 21.6, 10, 21.6, 2.4), "F")                                    # near front leg, over it
    c.paint(c.ellipse(9.2, 21.4, 2.4, 1.5), "F")
    # head: round, a blunt white muzzle on the bone's top, a floppy ear
    hx, hy = 16, 9 + dy
    c.paint(c.ellipse(hx + 2.5, hy + 3.5, 3.5, 3.5), "F")                            # neck
    c.paint(c.ellipse(hx, hy, 5.2, 4.8), "F")
    c.paint(c.ellipse(hx - 0.5, hy - 2.5, 0.9, 2.2), "W")                            # the blaze
    c.paint(c.ellipse(hx - 5.2, hy + 2.0, 3.6, 2.4), "W")                            # blunt muzzle
    c.paint(c.rect(int(hx) - 9, int(hy), int(hx) - 7, int(hy) + 2), "N")             # nose
    if jaw:
        c.paint(c.rect(int(hx) - 8, int(hy) + 3, int(hx) - 4, int(hy) + 4), "E")     # the jaw open on the bone
        c.set(int(hx) - 7, int(hy) + 4, "T")
    else:
        c.paint(c.rect(int(hx) - 7, int(hy) + 3, int(hx) - 4, int(hy) + 4), "E")
    c.paint(c.rect(int(hx) - 3, int(hy) - 1, int(hx) - 1, int(hy) + 1), "E")        # the eye
    c.set(int(hx) - 3, int(hy) - 1, "Wt")
    c.paint(AN.rell(c, hx + 3.6, hy + 2.2, 4.2, 2.2, 78), "R")                      # the floppy ear
    return _finish(c, BONEDOG)


# ---- 3. puppy chasing its tail ----------------------------------------------------------------------------------

def _chase_side(c, ph):
    """The puppy from the side, facing right, its head turned back over its rump after its tail; ph swaps the
    running legs."""
    sp = 1.5 if ph else -1.5
    for x0, x1, key in ((9, 7 - sp, "S"), (17, 19 + sp, "S"), (11, 10 + sp, "F"), (19, 21 - sp, "F")):
        c.paint(c.line(x0, 15, x1, 19.5, 2.0), key)
        c.paint(c.rect(int(x1) - 1, 19, int(x1) + 2, 21), "W")
    c.paint(c.ellipse(14, 14, 7, 4), "F")
    c.paint(c.ellipse(15, 16.5, 4.5, 1.3), "W")
    tail = PETS._chain(c, 7, 13, 150, 7, 1.2, -25, 1.3, 0.9)                    # the tail, curled up to the nose
    hx, hy = 15, 7                                         # the head on the back, facing the tail
    c.paint(c.ellipse(17, 11, 3.2, 3), "F")
    c.paint(c.ellipse(hx, hy, 4.3, 3.8), "F")
    c.paint(c.ellipse(hx - 3.6, hy + 1.2, 2.4, 1.8), "W")
    c.paint(AN.rell(c, hx + 1.8, hy + 0.8, 3.2, 1.6, 75), "R")                   # the floppy ear
    c.set(hx - 2, hy - 1, "E").set(hx - 6, hy + 0, "N")
    c.set(hx - 4, hy + 2, "T")
    c.paint(tail, "F")


def _chase_front(c, ph):
    """The puppy facing us mid-turn, its eyes on the tail whipping round at its side."""
    c.paint(PETS._chain(c, 19, 16, 40 if ph else 20, 6, 0.9, 18, 1.2, 0.9), "F")
    c.paint(c.ellipse(14, 16, 5.6, 4.6), "F")
    c.paint(c.ellipse(14, 16.5, 2.6, 3), "W")
    for x in (10, 15):
        c.paint(c.rect(x, 19 - (1 if (x > 12) == bool(ph) else 0), x + 3, 21), "W")
    hy = 8
    th = np.radians(90 - 60 * 0.6)
    for side in (-1, 1):                                   # ears flung out by the turn
        ex, ey = 14 + side * 5.5, hy - 2
        cx, cy = ex + side * 3.0 * np.cos(th), ey + 3.0 * np.sin(th)
        c.paint(AN.rell(c, cx, cy, 3.4, 1.9, np.degrees(th) if side > 0 else 180 - np.degrees(th)), "R")
    c.paint(c.ellipse(14, hy, 6.4, 5.4), "F")
    c.paint(c.ellipse(14, hy + 2.6, 3.4, 2.4), "W")
    c.paint(c.ellipse(10.5, hy - 1.2, 2.4, 2.0), "R")
    for x in (11, 16):                                     # eyes glancing at the tail
        c.paint(c.rect(x, hy - 1, x + 2, hy + 1), "E")
        c.set(x, hy - 1, "W")
    c.paint(c.rect(13, hy + 1, 15, hy + 2), "N")
    c.paint(c.rect(13, hy + 3, 15, hy + 5), "T")


def _chase_back(c, ph):
    """The puppy from behind mid-turn: its rump, the tail up, its head peeking round after it."""
    for x in (9, 16):
        c.paint(c.rect(x, 19 - (1 if (x > 12) == bool(ph) else 0), x + 3, 21), "W")
    c.paint(c.ellipse(14, 15, 6.4, 5.4), "F")
    c.paint(c.ellipse(16.5, 14.5, 2.2, 2.2), "R")                                  # a patch on the back
    c.paint(PETS._chain(c, 15.5, 10.5, 75, 8, 1.1, 24, 1.3, 0.9), "F")          # the tail, up and over
    hx, hy = 7, 10
    c.paint(AN.rell(c, hx + 2.6, hy + 2.4, 3.0, 1.6, 70), "R")
    c.paint(c.ellipse(hx, hy, 4, 3.7), "F")
    c.paint(c.ellipse(hx - 1.6, hy + 1.4, 2.2, 1.6), "W")
    c.set(int(hx), int(hy) - 1, "E").set(int(hx) - 3, int(hy), "N")


def spin_puppy(k, n=8):
    """The cream puppy (lib_pets) spins on the spot after its own tail: from the side, facing us, from the other
    side, from behind, the legs scrambling; swish marks circle on the ground."""
    view = k % 4
    c = A.Canvas(28, 22)
    if view == 3:
        _chase_back(c, k // 4)
    elif view == 1:
        _chase_front(c, k // 4)
    else:
        _chase_side(c, k // 4)
    g = c.g if view != 2 else A.flip(c.g)
    g = A.outline(g, "K")
    yy, xx = np.mgrid[0:g.shape[0], 0:g.shape[1]]
    a = np.arctan2((yy + 0.5 - 21.5) / 2.0, xx + 0.5 - 15)
    ring = np.abs(((xx + 0.5 - 15) / 13.5) ** 2 + ((yy + 0.5 - 21.5) / 2.2) ** 2 - 1) < 0.22
    dash = ((a - 2 * np.pi * k / n) % (np.pi / 2)) < 0.7                   # dashes run round with the spin
    g[ring & dash & (g == "")] = "M"
    return np.asarray(A.colours(g, {**PETS.PUPPY, "M": "#bdf", "K": A.OUTLINE}), dtype=object)


# ---- 4. the cat and a piglet napping --------------------------------------------------------------------------------

PIG = {"B": "#a64", "S": "#dc8", "N": "#e9a", "P": "#111", "D": "#532", "F": "#432"}


def sleep_piglet(breath):
    """A striped piglet lying asleep facing right, legs tucked, chin on the ground; breath 0..1 swells its side."""
    c = A.Canvas(25, 15)
    ry = 4.6 + 0.6 * breath
    for x, y in ((1, 8), (0, 7), (1, 6), (2, 7)):                                   # curly tail
        c.set(x, y + int(round(5 - ry)), "N")
    body = c.ellipse(10.5, 14.6 - ry, 8.6, ry)
    c.paint(body, "B")
    for y in (14.6 - ry - 2, 14.6 - ry + 0.5):                                     # pale stripes
        c.paint(body & c.rect(3, int(y), 17, int(y) + 1), "S")
    c.paint(c.ellipse(11, 14.4, 4, 0.9) | c.ellipse(16.5, 14.4, 2.2, 0.9), "F")      # tucked legs
    c.paint(c.ellipse(19.5, 10.5, 4.0, 3.8), "B")
    c.paint(c.ellipse(22.6, 12.2, 2.0, 1.6), "N")                                  # snout on the ground
    c.set(23, 12, "D")
    c.paint(c.triangle((16.0, 8.4), (15.0, 4.6), (19.6, 7.0)), "D")                 # the ear
    c.set(20, 10, "P").set(21, 10, "P")                                             # closed eye
    return _finish(c, PIG)


def nap(k):
    """The cat dozes sitting up, eyes shut, and the piglet sleeps curled at its side, back to back, the cat's tail
    over its rump: they breathe out of step, the cat's head rising, the piglet's side swelling, z marks drifting up
    from both."""
    g = _grid(50, 33)
    pig = sleep_piglet([0, 1, 1, 0][k])
    _paste(g, pig, 50 - pig.shape[1], 33 - pig.shape[0])
    _paste(g, cat(0.62, blink=True, rise=[1, 1, 0, 0][k]), 0, 6)
    for st, (x0, y0) in ((k % 4, (18, 0)), ((k + 2) % 4, (40, 6))):
        if st < 3:
            dx, dy, size = [(0, 6, 3), (3, 3, 4), (6, 0, 4)][st]
            _zee(g, x0 + dx, y0 + dy, size, "#bdf")
    return g


# ---- 5. the cat and a fawn -------------------------------------------------------------------------------------------

def reach_fawn(reach, ear):
    """lib_animals' spotted fawn facing left with its neck stretched forward and down by reach (0..1)."""
    c = A.Canvas(26, 22)
    ox = 4
    for x0, x1 in ((7, 6.5), (9, 9), (15, 15.5), (17, 17.5)):
        c.paint(c.line(x0 + ox, 13, x1 + ox, 20, 1.4), "D" if x0 in (9, 17) else "B")
        c.paint(c.rect(int(x1 + ox) - 1, 20, int(x1 + ox) + 1, 22) & (c.px > x1 + ox - 1), "H")
    c.paint(c.ellipse(12.5 + ox, 12, 6.5, 3.6), "B")
    c.paint(c.ellipse(12.5 + ox, 14.2, 4.5, 1.2), "W")
    for x, y in ((10, 10), (13, 9), (16, 10), (11, 12), (14, 11), (17, 12)):
        c.set(x + ox, y, "W")
    c.paint(c.line(19 + ox, 10, 20.5 + ox, 10.5, 1.6), "W")
    hx, hy = 6 + ox - 4 * reach, 6 + 2.5 * reach
    c.paint(c.line(8 + ox, 11, hx, hy, 3.2), "B")
    h = AN.Pose(c, hx, hy, -12 * reach)
    c.paint(h.ell(-1.4, -1.2, 2.8, 2.3) | h.ell(-3.6, -0.2, 1.6, 1.2), "B")
    c.paint(h.line(0.5, -2.5, 3.5, -5.2 if ear else -3.5, 1.7), "E")
    c.paint(h.line(-0.8, -2.8, 0.2, -5.5, 1.5), "E")
    c.paint(h.dot(-4.8, -1.0), "P")
    c.paint(h.dot(-2.0, -1.8) | h.dot(-2.0, -0.8), "P")
    c.paint(h.dot(-2.0, -1.8), "W")
    return _finish(c, AN.DEER)


def noses(k):
    """The cat, seen from the side, and a fawn meet nose to nose: the fawn stretches its neck down to the cat, they
    touch, a heart rises, the fawn flicks an ear and draws back; the cat's tail swings."""
    reach, lean, blink, heart, ear = [(1, 1, 1, 1, 0), (1, 1, 0, 2, 1), (0.5, 0, 0, 0, 0), (0, 0, 0, 0, 1),
                                      (0.5, 0, 0, 0, 0), (1, 1, 1, 0, 0)][k]
    g = _grid(62, 34)
    fawn = reach_fawn(reach, ear)
    _paste(g, fawn, 62 - fawn.shape[1], 34 - fawn.shape[0])
    _paste(g, side_ginger(tail=0.6 * _wave(6, k), blink=bool(blink), head_dn=0.12 * lean), 0, 6)
    if heart:
        _paste(g, _heart(), 33, 4 if heart == 1 else 0)
    return g


# ---- 6. the cat and a raven ------------------------------------------------------------------------------------------

RAVEN = {"B": "#334", "S": "#447", "D": "#223", "Bk": "#99a", "E": "#fff", "L": "#556"}


def raven(pose):
    """A raven facing left: pose 'perch', 'crouch' or 'hop' (in the air, wings up)."""
    c = A.Canvas(20, 18)
    up = pose == "hop"
    cr = 1 if pose == "crouch" else 0
    by = 9.5 + cr
    if not up:
        c.paint(c.line(9, by + 3, 8.5, 16, 1) | c.line(11, by + 3, 11.5, 16, 1), "L")
        c.paint(c.rect(7, 16, 10, 17) | c.rect(11, 16, 13, 17), "L")
    else:
        c.paint(c.line(9.5, by + 3, 9, by + 5, 1) | c.line(11, by + 3, 11.8, by + 5, 1), "L")
    c.paint(c.triangle((12.5, by - 1), (19.5, by + 3.5), (17, by + 5)), "D")         # tail
    c.paint(AN.rell(c, 10.5, by, 6, 3.9, 18), "B")
    c.paint(AN.rell(c, 11.5, by - 0.4, 4.2, 2.3, 18), "S")
    if up:                                                                          # wings raised
        c.paint(c.triangle((8.5, by - 1.5), (14.5, by - 1), (16.5, by - 8.5)), "S")
        c.paint(c.triangle((10, by - 1.5), (14, by - 1), (12.5, by - 7.5)), "D")
    hx, hy = 6.2, by - 3.6 + cr
    c.paint(c.disc(hx, hy, 2.9), "B")
    c.paint(c.ellipse(hx + 0.6, hy + 2.6, 1.8, 1.4), "D")                          # shaggy throat
    c.paint(c.triangle((hx - 1.6, hy - 1.2), (hx - 6.2, hy + 0.6), (hx - 1.6, hy + 1.4)), "Bk")   # heavy beak
    c.set(int(hx), int(hy) - 1, "E")
    return _finish(c, RAVEN)


def perch(k):
    """A raven perches on the cat's head; the cat looks up at it. The raven crouches, hops up with its wings
    raised and lands again; the cat's tail swings and it blinks once."""
    pose, dy = [("perch", 0), ("crouch", 0), ("hop", -4), ("hop", -3), ("perch", 0), ("perch", 0)][k]
    g = _grid(30, 41)
    _paste(g, cat(k / 6, blink=k == 5, look="up"), 0, 14)
    r = raven(pose)
    _paste(g, r, 6, 4 + dy)                                   # the feet on the crown, between the ears
    return g


# ---- 7. hen and chicks following the cat ----------------------------------------------------------------------------

def walk_hen(ph):
    """lib_animals' hen facing left, walking: ph 0..1 is the step; the head bobs."""
    c = A.Canvas(18, 17)
    s = np.sin(2 * np.pi * ph)
    bob = 0.5 * abs(s)
    for side in (-1, 1):
        fx = 10 + side * 1.0 + 1.6 * s * side
        lift = max(0.0, s * side)
        c.paint(c.line(10 + side, 12 - bob, fx, 16 - lift, 1), "O")
        c.paint(c.rect(int(fx) - 2, int(16 - lift), int(fx) + 1, int(17 - lift)), "O")
    c.paint(c.triangle((12, 9 - bob), (17.5, 2.5 - bob), (16, 10 - bob)), "T")
    c.paint(c.ellipse(10.5, 9.5 - bob, 6, 4), "B")
    c.paint(c.ellipse(12, 9 - bob, 3.5, 2.2), "D")
    hx, hy = 6 - 0.8 * s, 5 - bob
    c.paint(c.line(7.5, 8 - bob, hx + 0.5, hy, 3), "B")
    h = AN.Pose(c, hx, hy, 0)
    c.paint(h.ell(0, 0, 2.4, 2.2), "B")
    c.paint(h.tri((-2, 0), (-4.2, 0.6), (-2, 1.4)), "Y")
    c.paint(h.ell(0, -2.4, 1.6, 0.9), "R")
    c.paint(h.ell(-1.6, 2, 0.6, 0.9), "R")
    c.paint(h.dot(-0.6, -0.7), "P")
    return _finish(c, AN.HEN)


def walk_chick(ph):
    """lib_animals' chick facing left, walking: ph 0..1 is the step."""
    c = A.Canvas(11, 11)
    s = np.sin(2 * np.pi * ph)
    bob = 1 if s > 0.5 else 0
    c.paint(c.rect(4 + (1 if s > 0 else 0), 9, 5 + (1 if s > 0 else 0), 11), "O")
    c.paint(c.rect(6 - (1 if s < 0 else 0), 9, 7 - (1 if s < 0 else 0), 11), "O")
    c.paint(c.ellipse(5.5, 6.8 - bob, 3.6, 3), "Y")
    c.paint(c.disc(3.8, 3.6 - bob, 2.4), "Y")
    c.paint(c.ellipse(6.6, 7 - bob, 2, 1.3), "W")
    c.paint(c.rect(0, 4 - bob, 2, 5 - bob), "O")
    c.set(3, 3 - bob, "P")
    return _finish(c, AN.CHICK)


def parade(k, n=4):
    """The cat walks ahead and a hen with two chicks follow it in a line on one ground, all walking in place."""
    g = _grid(64, 28)
    ph = k / n
    _paste(g, A.flip(side_ginger(walk=ph, tail=0.3 * _wave(n, k))), -3, 0)
    hen = walk_hen(ph + 0.5)
    _paste(g, hen, 29, 27 - hen.shape[0])
    for i in (1, 0):                                         # the chicks, the last one drawn first
        ch = walk_chick(ph + 0.25 * (i + 1))
        _paste(g, ch, 43 + 8 * i, 27 - ch.shape[0])
    return g


# ---- 8. the cat and a neck -------------------------------------------------------------------------------------------

def stare(k):
    """The cat and a Neck stare at each other: the Neck, its tail curled up, blinks and flicks its tongue, the cat's
    tail swishes."""
    tongue, blink = [(0, False), (0, False), (0, True), (0, False), (2, False), (4, False), (2, False),
                     (0, False)][k]
    g = _grid(64, 27)
    nk = np.asarray(AN.neck(tongue, blink, curl=9, tail=5), dtype=object)
    _paste(g, nk, 22, 27 - nk.shape[0])
    _paste(g, cat(k / 8, look="right"), 0, 0)
    return g


# ---- 9. piglet and puppy tug of war -----------------------------------------------------------------------------

def side_puppy(lean, tail):
    """The cream puppy (lib_pets) from the side, facing right, a rope end in its mouth; lean pulls it back (px)."""
    c = A.Canvas(25, 18)
    dx = -lean
    c.paint(PETS._chain(c, 5 + dx, 9, 120 - 40 * tail, 5, 0.9, -10, 1.1, 0.8), "F")
    for x0, x1, key in ((7, 5.5, "S"), (16, 18.5, "S"), (9, 7.5, "F"), (18, 20.5, "F")):
        c.paint(c.line(x0 + dx, 12, x1 + dx * 0.3, 16.5, 2.0), key)
        c.paint(c.rect(int(x1 + dx * 0.3) - 1, 16, int(x1 + dx * 0.3) + 2, 18), "W")
    c.paint(c.ellipse(12 + dx, 11, 6.5, 3.8), "F")
    c.paint(c.ellipse(13 + dx, 13.5, 4, 1.3), "W")
    hx, hy = 19 + dx, 7
    c.paint(c.ellipse(hx, hy, 4.4, 4), "F")
    c.paint(c.ellipse(hx + 3.6, hy + 1.6, 2.4, 1.8), "W")
    c.paint(AN.rell(c, hx - 1.6, hy + 0.5, 3.4, 1.7, 75), "R")                     # floppy ear
    c.set(int(hx + 1), int(hy - 1), "E").set(int(hx + 6), int(hy + 0.5), "N")
    return _finish(c, PETS.PUPPY)


def tug_piglet(lean):
    """lib_animals' piglet facing left with a rope end in its mouth, pulled back by lean (px), braced."""
    c = A.Canvas(20, 13)
    dx = 1 + lean
    by = 7
    for x0, x1 in ((6, 5), (8, 7), (12, 13), (14, 15)):
        c.paint(c.line(x0 + dx, by + 2, x1 + dx * 0.3, 12, 1.2), "F")
    body = c.ellipse(10.5 + dx, by, 5.5, 4)
    c.paint(body, "B")
    for y in (by - 2, by + 0.5):
        c.paint(body & c.rect(7 + dx, int(y), 17 + dx, int(y) + 1), "S")
    c.paint(c.disc(5 + dx, by - 0.5, 3.4), "B")
    c.paint(c.rect(1 + dx, by - 1, 3 + dx, by + 1), "N")
    c.set(1 + dx, by - 1, "D")
    c.set(4 + dx, by - 3, "P")
    c.paint(c.triangle((5 + dx, by - 3.5), (7.5 + dx, by - 6), (8 + dx, by - 3)), "D")
    for x, y in ((16, -2), (17, -3), (16, -4), (15, -3)):
        c.set(x + dx, by + y, "N")
    return _finish(c, PIG)


def tug(k):
    """A boar piglet and the puppy play tug of war with a rope: they pull it back and forth, the red marker in
    the middle swinging from side to side; the puppy's tail wags."""
    s = [0, -1, -2, -1, 0, 1, 2, 1][k]                      # the rope's shift: negative toward the puppy
    W, H = 58, 20
    g = _grid(W, H)
    pup = side_puppy(max(0, -s), k % 2)
    pig = tug_piglet(max(0, s))
    px0, gx0 = 0, W - pig.shape[1]
    _paste(g, pup, px0, H - pup.shape[0])
    _paste(g, pig, gx0, H - pig.shape[0])
    y = 10
    x0 = px0 + 25 - max(0, -s)                              # the puppy's mouth
    x1 = gx0 + 2 + max(0, s)                                # the piglet's mouth
    for x in range(x0, x1 + 1):
        g[y + (1 if x0 + 3 < x < x1 - 3 else 0), x] = "#a85" if x % 2 else "#cb7"
    mx = (x0 + x1) // 2 + s
    g[y:y + 3, mx] = "#d33"
    return g


# ---- 10. the cat riding a lox ----------------------------------------------------------------------------------------

def small_lox(jaw, tail):
    """lib_animals' lox, drawn more compactly (54 x 36) for the cat to ride: facing left, chewing."""
    rng = np.random.default_rng(7)
    c = A.Canvas(54, 36)
    for x0, sh in ((15, "N"), (21, "M"), (36, "N"), (42, "M")):
        c.paint(c.rect(x0, 25, x0 + 5, 34), sh if sh == "M" else "B")
        c.paint(c.rect(x0, 34, x0 + 5, 36), "N")
    c.paint(c.ellipse(32, 16.5, 19, 10), "B")
    c.paint(c.ellipse(26, 10, 11, 6), "F")                                          # the shoulder hump
    for x in range(13, 51):
        bot = 16.5 + 10 * np.sqrt(max(0.0, 1 - ((x + 0.5 - 32) / 19) ** 2))
        c.paint(c.rect(x, int(bot) - 3, x + 1, int(bot) + int(rng.integers(1, 3))), "F" if x % 3 else "M")
    for k in range(8):
        x, y = 20 + 4 * k, 14 + (k * 5) % 7
        c.paint(c.line(x, y, x + 1, y + 3, 1), "M")
    tx = 50 + tail
    c.paint(c.line(50, 13, tx, 22, 1.6), "F")
    c.paint(c.rect(int(tx) - 1, 21, int(tx) + 1, 25), "M")
    c.paint(c.ellipse(11.5, 21, 7.5, 6.8), "H")
    c.paint(c.ellipse(15, 15, 6.5, 4.5), "F")
    c.paint(c.ellipse(5, 24, 4.2, 3.6), "J")
    c.paint(c.rect(1, 22, 3, 24), "N")
    c.paint(c.ellipse(5.5, 28 + jaw, 3.4, 1.5), "J")
    c.paint(c.rect(2, 27, 8, 28 + jaw), "N")
    if jaw:
        c.paint(c.rect(1, 26 + jaw, 4, 27 + jaw), "G")
    for k in range(13):
        t = k / 12
        ang = np.radians(-40 - 250 * t)
        r = 5.6 - 2.2 * t
        c.paint(c.disc(12.5 + r * np.cos(ang), 19.5 + r * np.sin(ang), 1.7 - 0.8 * t), "T")
    c.paint(c.rect(11, 19, 13, 21), "P")
    c.set(11, 19, "T")
    return _finish(c, AN.LOX)


def ride(k):
    """The cat sits on a lox's back and rides along: the lox chews, the cat's tail sways and it blinks."""
    jaw = [0, 1, 2, 1][k % 4]
    g = _grid(56, 55)
    lx = small_lox(jaw, [0, 1, 1, 0][k % 4])
    _paste(g, lx, 0, 55 - lx.shape[0])
    _paste(g, cat(k / 4, blink=k == 3), 22, 0)
    return g


# ---- extras ----------------------------------------------------------------------------------------------------------

def duet(k):
    """The hound and a wolf cub sit side by side and howl together: heads up, mouths open, sound arcs rising from
    both, then they lower their heads and start again."""
    head, mouth, sound = [(-60, True, 2), (-60, True, 1), (-25, False, 0), (0, False, 0), (-25, False, 0),
                          (-60, True, 1)][k]
    W, H = 60, 44
    hound = A.Canvas(34, 36)
    nose = [PETS.sit_dog(hound, 0, 0, 1.0, head=head, tail=0.4 * _wave(6, k), mouth=mouth, hz=1.15)]
    cub = A.Canvas(34, 36)
    nose.append(PETS.sit_dog(cub, 0, 8, 0.78, wolf=True, head=head - 5 if head else 0, tail=0.3 * _wave(6, k),
                             mouth=mouth, hz=1.35))
    g = _grid(W, H)
    _paste(g, _finish(hound, PETS.HOUND), 0, H - 38)
    _paste(g, _finish(cub, PETS.CUB), 26, H - 38)
    arcs = A.Canvas(W, H)                                   # sound arcs from both noses, over empty pixels only
    for (x, y), ox in zip(nose, (0, 26)):
        x, y = x + ox + 1, y + H - 38 + 1
        for i in range(sound):
            r = 3.0 + 3.0 * i
            ring = arcs.disc(x, y, r + 0.6) & ~arcs.disc(x, y, r - 0.6)
            a = np.degrees(np.arctan2(y - arcs.py, arcs.px - x))
            g[ring & (a > 15) & (a < 105) & (g == "")] = "#bdf"
    return g


SPARK = ["...w...", "...w...", "..www..", "wwwwwww", "..www..", "...w...", "...w..."]


def photo(k):
    """A friends' photo: the hen, the cat with a chick on its head, and a piglet pose in a row; the chick peeps and
    flaps, the camera flashes and the cat blinks, the piglet hops for joy."""
    flap, flash, hop = [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)][k]
    W, H = 58, 40
    g = _grid(W, H)
    _paste(g, cat(k / 4, blink=bool(flash)), 15, H - 27)
    ch = np.asarray(AN.chick(bool(flap), bool(flap)), dtype=object)
    _paste(g, ch, 24, H - 27 + 6 - ch.shape[0])
    hen = A.flip(np.asarray(AN.hen(0), dtype=object))
    _paste(g, hen, 0, H - hen.shape[0])
    pig = np.asarray(AN.piglet(2 * hop, bool(hop)), dtype=object)
    _paste(g, pig, W - pig.shape[1], H - pig.shape[0])
    if flash:
        sp = A.ascii(SPARK, {"w": 1})
        _paste(g, np.where(sp == "w", "#fff", ""), 2, 2)
    return g


# ---- pieces ----------------------------------------------------------------------------------------------------------

def _anim(name, fn, n, fps):
    return A.Piece(name, "pixel", frames=[_rows(fn(k)) for k in range(n)], fps=fps)


def pieces():
    return [
        _anim("Sled dog (moving, ear flick)", husky, 6, 4),
        _anim("Dog with a bone (moving, chewing)", bone_dog, 4, 4),
        _anim("Puppy (moving, chasing its tail)", spin_puppy, 8, 8),
        _anim("Cat and piglet (moving, napping)", nap, 4, 2),
        _anim("Cat and fawn (moving, touching noses)", noses, 6, 3),
        _anim("Cat and raven (moving, hopping)", perch, 6, 4),
        _anim("Cat, hen and chicks (moving, walking)", parade, 4, 5),
        _anim("Cat and neck (moving, staring)", stare, 8, 4),
        _anim("Piglet and puppy (moving, tug of war)", tug, 8, 5),
        _anim("Cat on a lox (moving, riding)", ride, 4, 3),
        _anim("Hound and wolf cub (moving, howling)", duet, 6, 3),
        _anim("Friends (moving, photo)", photo, 4, 3),
    ]
