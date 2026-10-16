"""Hens: Valheim's farm hens, their chicks and eggs as 8-bit pixel sprites, the cat's family (concepts/anim2.py).

A tame hen eats seeds and lays eggs, an egg kept warm hatches into a chick, and a chick grows into a hen; eggs are
food too. Drawn like lib_animals' hen and chick (same shapes, colours and the cat's pitch, so a hen stands next to
the cat at its true size): numpy masks on a Canvas, outlined automatically in the cat's dark outline. Scenes are
composed from finished sprites (each with its own outline), back to front. Every piece moves; the first frame is a
good still for players without the mod.
"""
import numpy as np

import artlib as A
from concepts import lib_animals as AN
from concepts.lib_friends import _grid, _paste, _rows, _zee, cat as front_cat

PAL = {
    "B": "#d96", "D": "#a53", "T": "#742", "R": "#e33", "Y": "#fc3", "O": "#f93", "P": "#111",   # the hen (AN.HEN)
    "Cy": "#fe5", "Cw": "#ec3", "Cm": "#c52",                                                 # the chick (AN.CHICK)
    "G": "#ec6",                                                                              # seeds
    "E": "#ffe", "Es": "#dcb", "Ec": "#986",                                                  # egg, its shade, cracks
    "W": "#fff", "K": A.OUTLINE, "Kx": "#213",
    "N": "#ca6", "Nd": "#864", "Nk": "#642",                                                  # nest straw
    "Wl": "#a63", "Wd": "#742",                                                               # wicker, logs
    "F1": "#fe6", "F2": "#f93", "F3": "#e43",                                                 # flames
    "I": "#667", "Id": "#334", "Ih": "#543",                                                  # the pan
    "Yk": "#fb2", "St": "#cde",                                                               # yolk, steam
    "Hg": "#bbc", "Hd": "#778", "Hn": "#eed",                                                 # helmet, horns
    "Bt": "#d33", "Bs": "#f99",                                                               # beetle
    "Rn": "#c33", "Rg": "#e62", "Ry": "#fa2",                                                 # rune paint, glow
    "Gr": "#5a3", "Gd": "#373", "Z": "#bdf", "X": "#fe4",                                     # grass, z marks, star
}


def _fin(c):
    """Canvas or key grid -> outlined colour grid, ready to paste."""
    g = c.g if isinstance(c, A.Canvas) else c
    return np.asarray(A.colours(A.outline(g, "K"), PAL), dtype=object)


def _col(g):
    """Key grid -> colour grid, no outline."""
    return np.asarray(A.colours(g, PAL), dtype=object)


def _ringed(c, mask, key):
    """Paint mask with key and a one-pixel outline ring around it (for a part drawn over another one)."""
    pad = np.pad(mask, 1)
    near = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    c.paint(near & ~mask, "K")
    c.paint(mask, key)


# ---- the hen ---------------------------------------------------------------------------------------------------

def hen(c, ox=0.0, oy=0.0, fluff=0.0, sit=False, head=(0.0, 0.0), peck=0.0, eye="open", beak=False,
        wing="fold", step=None, run=None, chest=0.0, helmet=False, tail=0.0):
    """lib_animals' hen facing left drawn into c with its top left at (ox, oy) (an 18 x 17 box when standing).
    fluff puffs the body up (px); sit hides the legs and lowers the body; head moves the head (dx, dy); peck tilts
    it down (0..1); eye open, blink, happy, wide, squint; beak opens it; wing fold, half, up, down; step lifts one
    foot (-1 back, 1 front); run 0..1 is a running stride; chest puffs her front out; helmet puts on a little
    Viking helmet; tail lifts the tail (px)."""
    X = lambda x: ox + x
    Yy = lambda y: oy + y
    by = 2.0 if sit else 0.0
    if run is not None:                                         # a running stride: legs scissor
        s = np.sin(2 * np.pi * run)
        for side in (-1, 1):
            fx = 10 + 3.2 * s * side
            fy = 16 - max(0.0, 1.6 * s * side)
            c.paint(c.line(X(10 + 0.5 * side), Yy(12), X(fx), Yy(fy), 1), "O")
            c.paint(c.rect(int(X(fx)) - 2, int(Yy(fy)), int(X(fx)) + 1, int(Yy(fy)) + 1), "O")
    elif not sit:
        lift_b = 1 if step == -1 else 0
        lift_f = 1 if step == 1 else 0
        c.paint(c.line(X(9), Yy(12), X(8.5), Yy(16 - lift_f), 1), "O")
        c.paint(c.line(X(11), Yy(12), X(11.5), Yy(16 - lift_b), 1), "O")
        c.paint(c.rect(int(X(7)), int(Yy(16 - lift_f)), int(X(10)), int(Yy(17 - lift_f))), "O")
        c.paint(c.rect(int(X(11)), int(Yy(16 - lift_b)), int(X(13)), int(Yy(17 - lift_b))), "O")
    c.paint(c.triangle((X(12), Yy(9 + by)), (X(17.5), Yy(2.5 + by - tail)), (X(16), Yy(10 + by))), "T")
    body = c.ellipse(X(10.5 - 0.3 * chest), Yy(9.5 + by), 6 + fluff + 0.5 * chest, 4 + fluff)
    if sit:
        body |= c.ellipse(X(10.5), Yy(12.5), 6.8 + fluff, 2.6)
    c.paint(body, "B")
    if fluff >= 0.9:                                            # ruffled tufts round the back
        for a in (-150, -115, -80, -45, -10, 25):
            r = np.radians(a)
            tx, ty = 10.5 + (6 + fluff) * np.cos(r) * 1.12, 9.5 + by + (4 + fluff) * np.sin(r) * 1.18
            c.paint(c.disc(X(tx), Yy(ty), 0.75), "B")
    wy = 9 + by
    if wing == "fold":
        c.paint(c.ellipse(X(12), Yy(wy), 3.5, 2.2), "D")
    elif wing == "half":
        c.paint(AN.rell(c, X(12.5), Yy(wy - 1), 4.0, 2.0, -25), "D")
    elif wing == "up":
        c.paint(AN.rell(c, X(14), Yy(wy - 3.5), 1.9, 4.2, 35), "D")
        c.paint(AN.rell(c, X(12), Yy(wy - 3), 1.6, 3.8, 15), "D")
    elif wing == "down":
        c.paint(AN.rell(c, X(13), Yy(wy + 1.5), 4.2, 1.8, 35), "D")
    hx, hy = 6 + head[0] - 2 * peck, 5 + head[1] + 8 * peck + (1 if sit else 0)
    c.paint(c.line(X(7.5 - 0.5 * chest), Yy(8 + by), X(hx + 0.5), Yy(hy), 3), "B")
    h = AN.Pose(c, X(hx), Yy(hy), -70 * peck)
    c.paint(h.ell(0, 0, 2.4, 2.2), "B")
    if beak:
        c.paint(h.tri((-2, -0.2), (-4.4, -0.6), (-2, 0.8)) | h.tri((-2, 1.0), (-4.0, 2.0), (-2, 1.6)), "Y")
    else:
        c.paint(h.tri((-2, 0), (-4.2, 0.6), (-2, 1.4)), "Y")
    if helmet:
        c.paint(h.ell(0.3, -2.0, 2.5, 1.9) & ~h.ell(0.3, 0.4, 4, 1.6), "Hg")
        c.paint(h.line(-2.0, -1.6, 2.6, -1.6, 1), "Hd")
        c.paint(h.line(-2.0, -2.6, -3.2, -4.2, 1), "Hn")
        c.paint(h.line(2.6, -2.6, 3.8, -4.2, 1), "Hn")
    else:
        c.paint(h.ell(0, -2.4, 1.6, 0.9), "R")                   # comb
    c.paint(h.ell(-1.6, 2, 0.6, 0.9), "R")                       # wattle
    ex, ey = h.p(-0.6, -0.7)
    ex, ey = int(np.floor(ex)), int(np.floor(ey))
    if eye == "open":
        c.set(ex, ey, "P")
    elif eye == "wide":
        c.set(ex, ey, "P").set(ex + 1, ey, "W").set(ex, ey - 1, "W").set(ex + 1, ey - 1, "W")
    elif eye in ("blink", "squint"):
        c.set(ex, ey, "D").set(ex + 1, ey, "D")
    elif eye == "happy":
        c.set(ex, ey, "P").set(ex + 1, ey - 1, "P")
    return c




# ---- the chick -------------------------------------------------------------------------------------------------

def chick(c, ox=0.0, oy=0.0, peck=0.0, step=None, flap=False, peep=False, eye="open", bob=0):
    """lib_animals' chick facing left drawn into c, its 11 x 11 box's top left at (ox, oy). peck 0..1 puts the beak
    to the ground; step 0..1 is a running stride; flap lifts the wing; eye open or shut."""
    X = lambda x: ox + x
    Yy = lambda y: oy + y - bob
    if step is None:
        c.paint(c.rect(int(X(4)), int(oy + 9), int(X(5)), int(oy + 11)) |
                c.rect(int(X(6)), int(oy + 9), int(X(7)), int(oy + 11)), "O")
    else:
        s = np.sin(2 * np.pi * step)
        for side in (-1, 1):
            fx = 5.5 + 1.8 * s * side
            c.paint(c.line(X(5.5), oy + 9, X(fx), oy + 10.6 - max(0, s * side), 1), "O")
    c.paint(AN.rell(c, X(5.5), Yy(6.8 + 0.3 * peck), 3.6, 3, -22 * peck), "Cy")    # tips forward to peck
    hx, hy = 3.8 - 2.4 * peck, 3.6 + 4.0 * peck
    c.paint(c.disc(X(hx), Yy(hy), 2.4 - 0.2 * peck), "Cy")
    if flap:
        c.paint(c.line(X(6.5), Yy(6), X(9.5), Yy(2.5), 1.6), "Cw")
    else:
        c.paint(c.ellipse(X(6.6), Yy(7 + 0.3 * peck), 2, 1.3), "Cw")
    bx, by = int(np.floor(X(hx - 3.8))), int(np.floor(Yy(hy + 0.4)))
    if peck > 0.5:                                              # beak down at the ground
        x, y = int(np.floor(X(hx))), int(np.floor(Yy(hy)))
        c.set(x - 2, y + 1, "O").set(x - 3, y + 1, "O").set(x - 3, y + 2, "O")
    elif peep:
        c.paint(c.rect(bx, by - 1, bx + 2, by) | c.rect(bx, by + 1, bx + 2, by + 2), "O")
        c.set(bx + 1, by, "Cm")
    else:
        c.paint(c.rect(bx, by, bx + 2, by + 1), "O")
    ex, ey = int(np.floor(X(hx - 0.8))), int(np.floor(Yy(hy - 0.6)))
    if eye == "open":
        c.set(ex, ey, "P")
    else:
        c.set(ex, ey, "Cw").set(ex - 1, ey, "Cw")
    return c


def _chick(**kw):
    """A chick sprite, 13 x 12 before the outline (2 px of room in front for a pecking beak)."""
    c = A.Canvas(13, 12)
    return _fin(chick(c, ox=2, oy=1, **kw))


def chick_front(c, cx, cy, eyes="open", peep=False, wings=0, feet=True):
    """A chick facing us, its head centred on (cx, cy): eyes open, shut, look-l, look-r; wings 1 lifts them."""
    if feet:
        c.paint(c.rect(int(cx) - 2, int(cy) + 6, int(cx), int(cy) + 7) | c.rect(int(cx) + 1, int(cy) + 6,
                int(cx) + 3, int(cy) + 7), "O")
    c.paint(c.ellipse(cx + 0.5, cy + 3.6, 4.2, 2.9), "Cy")
    c.paint(c.ellipse(cx + 0.5, cy + 0.6, 3.4, 3.1), "Cy")
    c.paint(c.disc(cx + 0.2, cy - 2.7, 0.8), "Cy")             # a tuft
    wy = cy + 3 - 2 * wings
    c.paint(AN.rell(c, cx - 3.6, wy, 1.0, 1.8, 30 + 40 * wings) | AN.rell(c, cx + 4.6, wy, 1.0, 1.8, -30 - 40 * wings),
            "Cw")
    x, y = int(cx), int(cy)
    if eyes == "shut":
        c.set(x - 2, y, "Nd").set(x - 1, y, "Nd").set(x + 2, y, "Nd").set(x + 3, y, "Nd")
    else:
        lx, rx = {"open": (x - 2, x + 3), "look-l": (x - 2, x + 2), "look-r": (x - 1, x + 3)}[eyes]
        c.set(lx, y - 1, "P").set(lx, y, "P").set(rx, y - 1, "P").set(rx, y, "P")
    if peep:
        c.set(x, y + 1, "O").set(x + 1, y + 1, "O").set(x, y + 2, "Cm").set(x + 1, y + 2, "O")
    else:
        c.set(x, y + 1, "O").set(x + 1, y + 1, "O")
    return c


# ---- eggs, nests, shells -------------------------------------------------------------------------------------

def egg_mask(c, cx, cy, rx, ry, deg=0.0):
    """An egg (narrower on top) turned by deg about its bottom."""
    a = np.radians(deg)
    dx, dy = c.px - cx, c.py - (cy + ry)
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a) + ry
    k = 1 - 0.2 * np.clip(-v / ry, 0, 1)                         # narrower towards the top
    return (u / (rx * k)) ** 2 + (v / ry) ** 2 <= 1


def egg(c, cx, cy, rx, ry, deg=0.0, crack=0, ring=False):
    """An egg centred on (cx, cy); crack 1 a small crack, 2 a zigzag across; ring outlines it over what is behind."""
    m = egg_mask(c, cx, cy, rx, ry, deg)
    if ring:
        _ringed(c, m, "E")
    else:
        c.paint(m, "E")
    sh = egg_mask(c, cx + 0.9, cy + 0.6, rx, ry, deg) & ~egg_mask(c, cx - 0.6, cy - 0.5, rx, ry, deg)
    c.paint(m & sh, "Es")
    hx, hy = cx - rx * 0.45, cy - ry * 0.45
    c.set(int(hx), int(hy), "W")
    if crack:                                                   # a zigzag crack across the middle
        x0, x1 = (int(cx) - 1, int(cx) + 3) if crack == 1 else (int(cx - rx), int(cx + rx) + 1)
        zig = c.rect(x0, 0, x1, c.h) & (c.yy == int(cy - 1) + (c.xx % 2))
        c.paint(m & zig, "Ec")
    return m


def shell_bottom(c, cx, cy, rx, ry, cut, deg=0.0):
    """The lower part of an egg below a zigzag at height cut (relative to cy)."""
    m = egg_mask(c, cx, cy, rx, ry, deg) & (c.py > cy + cut + (c.xx % 2))
    c.paint(m, "E")
    c.paint(m & (c.px > cx + rx * 0.35), "Es")
    return m


def shell_cap(c, cx, cy, rx, ry, deg=0.0, ring=False):
    """The top of an egg (a cap with a zigzag rim) centred on (cx, cy), turned by deg."""
    a = np.radians(deg)
    dx, dy = c.px - cx, c.py - cy
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a)
    m = ((u / (rx * 0.82)) ** 2 + (v / ry) ** 2 <= 1) & (v < 0.6 + (np.floor(u + 50) % 2))
    if ring:
        _ringed(c, m, "E")
    else:
        c.paint(m, "E")
    c.paint(m & (u > rx * 0.3), "Es")
    return m


def nest(c, cx, cy, rx, ry=3.0):
    """A straw nest's front rim centred on (cx, cy)."""
    m = c.ellipse(cx, cy, rx, ry) & (c.py > cy - ry * 0.5)
    c.paint(m, "N")
    straw = m & (((c.xx * 2 + c.yy * 3) % 5) == 0)
    c.paint(straw, "Nd")
    c.paint(m & (c.py > cy + ry * 0.55) & ((c.xx % 3) == 1), "Nk")
    for x0 in np.arange(cx - rx + 1, cx + rx - 1, 3.2):          # straws sticking out of the rim
        c.paint(c.line(x0, cy - ry * 0.4, x0 + 1.2, cy - ry * 0.4 - 1.2, 1) & (c.py < cy - ry * 0.3), "N")
    return m


# ---- 1. a hen lays an egg ----------------------------------------------------------------------------------------

def laying(k):
    """The hen fluffs up, squints and shakes, an egg pops out under her with a little star, and she stands tall
    and proud over it with her eyes shut happily."""
    st = ["proud", "proud2", "calm", "fluff", "fluff2", "pop"][k]
    c = A.Canvas(26, 22)
    ox, oy = 3, 4
    has_egg = st in ("proud", "proud2", "pop")
    if st == "calm":
        hen(c, ox, oy)
    elif st == "fluff":
        hen(c, ox, oy, fluff=1.0, eye="squint", head=(0.5, 1))
    elif st == "fluff2":
        hen(c, ox + 1, oy, fluff=1.4, eye="squint", head=(0.5, 1.5), tail=1)
    elif st == "pop":
        hen(c, ox, oy - 1, eye="wide", wing="half", head=(0, -1), tail=1)
    else:
        hen(c, ox, oy - 1, eye="happy", chest=1.2, head=(-0.3, -1.5), tail=1.5,
            wing="half" if st == "proud2" else "fold")
    g = _grid(28, 24)
    if has_egg:                                                 # the egg lies behind her feet, under her tail
        e = A.Canvas(7, 8)
        egg(e, 3.5, 4, 2.7, 3.4)
        _paste(g, _fin(e), 17, 12)
    _paste(g, _fin(c), 0, 0)
    if st in ("pop", "proud2"):
        sp = A.ascii(["..x..", ".xxx.", "xxxxx", ".xxx.", "..x.."] if st == "pop" else [".x.", "xxx", ".x."],
                     {"x": 1})
        _paste(g, _col(np.where(sp != "", "X", "")), 22 if st == "pop" else 23, 7 if st == "pop" else 8)
    return g


# ---- 2. an egg hatches -----------------------------------------------------------------------------------------

def hatching(k):
    """The egg wobbles, cracks, the top pops off and a chick stands up in the bottom shell wearing the top as a
    hat; it peeps and blinks."""
    st = ["out", "out2", "egg", "left", "right", "crack", "crack2", "pop"][k]
    c = A.Canvas(20, 22)
    cx, cy, rx, ry = 10, 13.5, 5.4, 6.8
    if st == "egg":
        egg(c, cx, cy, rx, ry)
    elif st == "left":
        egg(c, cx, cy, rx, ry, deg=-14)
    elif st == "right":
        egg(c, cx, cy, rx, ry, deg=14, crack=1)
    elif st == "crack":
        egg(c, cx, cy, rx, ry, crack=2)
    elif st == "crack2":
        egg(c, cx, cy, rx, ry, deg=-8, crack=2)
    else:
        chick_front(c, cx - 0.5, cy - (2 if st == "pop" else 3.5), eyes="open" if st != "out2" else "shut",
                    peep=st == "out2", wings=1 if st == "out2" else 0, feet=False)
        shell_bottom(c, cx, cy, rx, ry, 1.0)
        g = _fin(c)
        cap = A.Canvas(14, 9)
        shell_cap(cap, 7, 6, rx * 0.95, ry * 0.6, deg={"pop": -25, "out": 12, "out2": -10}[st])
        capg = _fin(cap)
        px, py = {"pop": (-4, -3), "out": (3, 0), "out2": (2, 0)}[st]
        _paste(g, capg, px, py)
        if st == "pop":
            for x, y in ((2, 6), (17, 5), (3, 11), (17, 10)):
                g[int(y), int(x)] = PAL["Es"]
        return g
    return _fin(c)


# ---- 3. a hen brooding on a nest ---------------------------------------------------------------------------------

def brooding(k):
    """A hen sits snug on a straw nest, two eggs peeking out under her; she breathes slowly and blinks."""
    breath, eye = [(0, "open"), (1, "open"), (1, "open"), (0, "open"), (0, "blink"), (0, "open")][k]
    g = _grid(30, 22)
    eggs = A.Canvas(28, 20)
    for x in (4.5, 24.5):                                       # eggs peeking out at her sides
        egg(eggs, x, 13.6, 2.5, 3.0)
    _paste(g, _fin(eggs), 0, 0)
    c = A.Canvas(28, 20)
    hen(c, 5, 1 - breath, sit=True, fluff=0.3, eye=eye)
    _paste(g, _fin(c), 0, 0)
    n = A.Canvas(28, 20)
    nest(n, 14, 17.5, 13, 3.0)
    _paste(g, _fin(n), 0, 0)
    return g


# ---- 4. three chicks pecking at seeds ------------------------------------------------------------------------------

def pecking(k):
    """Three chicks peck at scattered seeds, each in its own rhythm."""
    g = _grid(46, 14)
    for x, y in ((0, 13), (2, 12), (15, 13), (17, 12), (30, 13), (32, 12), (44, 13)):
        g[y, x] = PAL["G"]
    for i, x in enumerate((0, 15, 30)):
        p = [0, 0.5, 1, 1, 0.5, 0][(k + 2 * i) % 6]
        _paste(g, _chick(peck=p, eye="open" if (k + i) % 6 != 5 else "shut"), x, 0)
    return g


# ---- 5. a chick chases a beetle -----------------------------------------------------------------------------------

def beetle(leg):
    """A red spotted beetle facing left, 10 x 8: a round shell with a seam, spots and a shine, a grey head with a
    feeler; leg 0/1 steps."""
    c = A.Canvas(10, 8)
    for i, x in enumerate((4, 7)):                              # legs, alternating
        dx = (1 if (i + leg) % 2 else -1)
        c.set(x + dx, 7, "Hd")
    c.paint(c.ellipse(6, 4, 3.8, 3.0) & (c.py < 7), "Bt")
    c.paint(c.disc(1.8, 5.0, 1.5), "Hd")
    c.set(1, 2, "Hd").set(0, 1, "Hd")                           # a feeler
    c.paint(c.rect(6, 1, 7, 7), "Kx")                            # the seam
    c.set(4, 4, "P").set(8, 4, "P").set(4, 2, "Bs").set(5, 2, "Bs")
    return _fin(c)


def chase(k):
    """A chick runs flapping after a beetle that scurries ahead of it; grass tufts slide past behind."""
    g = _grid(40, 16)
    for x0 in (2, 22):                                          # grass tufts slide past behind
        x = (x0 + 5 * k) % 40
        for dx, h in ((0, 2), (1, 3), (2, 2)):
            for y in range(15 - h, 15):
                if 0 <= x + dx < 40:
                    g[y, x + dx] = PAL["Gr" if dx != 1 else "Gd"]
    _paste(g, beetle(k % 2), 2 - (k % 2), 6)
    _paste(g, _chick(step=k / 4, flap=k % 2 == 1, peep=k in (1, 2), bob=k % 2), 16, 3)
    for i, (x, y, n) in enumerate(((31, 6, 3), (32, 9, 4), (30, 12, 3))):   # speed lines
        x += 2 * ((k + i) % 2)
        for dx in range(n):
            g[y, x + dx] = PAL["St"]
    return g


# ---- 6. chicks hide under the hen -----------------------------------------------------------------------------------

def head_peek(c, x, y):
    """A chick's head peeking (a ringed disc with an eye and beak) centred on (x, y), facing left."""
    _ringed(c, c.disc(x, y, 2.3), "Cy")
    c.paint(c.rect(int(x) - 3, int(y), int(x) - 1, int(y) + 1), "O")
    c.set(int(x) - 1, int(y) - 1, "P")


def under_wings(k):
    """A hen sits fluffed up and three chicks peek out from under her chest, her wing and her tail, popping in and
    out one by one; she blinks."""
    c = A.Canvas(30, 20)
    hen(c, 6, 1, sit=True, fluff=0.6, wing="fold", eye="blink" if k == 5 else "open")
    # how far each head is out (0 hidden, 1 half, 2 all): under her chest, wing, tail
    show = [(2, 2, 2), (2, 1, 2), (1, 0, 2), (0, 1, 1), (1, 2, 0), (2, 2, 1)][k]
    for (x, y), r in zip(((7.5, 18.6), (17, 7.6), (25, 18.6)), show):
        if r:
            head_peek(c, x, y - (1.0 if r == 2 else 0))
    c.paint(c.ellipse(16.5, 15.5, 7.8, 2.4) & ~c.ellipse(16.5, 11.5, 7, 3.5), "B")   # her fluffy front over them
    c.paint(c.ellipse(18, 9.5 + 1, 3.6, 2.2) & (c.py > 8.9), "D")                     # the wing over the back one
    return _fin(c)


# ---- 7. a basket of eggs --------------------------------------------------------------------------------------------

def basket(k):
    """A wicker basket heaped with eggs: one egg wobbles, cracks open at the top and a chick peeks out, looks left
    and right, ducks back in and the shell closes over it."""
    st = ["peek", "look", "duck", "shut", "still", "wob1", "wob2", "pop"][k]
    g = _grid(30, 30)
    c = A.Canvas(28, 26)
    handle = c.ellipse(14, 14, 11, 11.5) & ~c.ellipse(14, 14, 9.6, 10.1) & (c.py < 14)
    c.paint(handle, "Wd")
    c.paint(handle & (((c.xx + c.yy) % 3) == 0), "Wl")
    _paste(g, _fin(c), 0, 2)
    for x, y in ((6.5, 12.5), (21.5, 12.5)):                    # the other eggs, each with its own outline
        e = A.Canvas(28, 26)
        egg(e, x, y, 2.8, 3.4)
        _paste(g, _fin(e), 0, 2)
    s = A.Canvas(14, 20)                                        # the lively egg
    ex, ey, rx, ry = 7, 13, 3.6, 4.5
    if st == "still":
        egg(s, ex, ey, rx, ry)
    elif st == "wob1":
        egg(s, ex, ey, rx, ry, deg=-14)
    elif st == "wob2":
        egg(s, ex, ey, rx, ry, deg=14, crack=1)
    elif st == "shut":
        egg(s, ex, ey, rx, ry, crack=2)
    else:
        low = {"peek": 0, "look": 0, "duck": 2, "pop": 0}[st]
        eyes = {"peek": "look-l", "look": "look-r", "duck": "open", "pop": "open"}[st]
        chick_front(s, ex - 0.5, ey - 1.5 + low, eyes=eyes, feet=False)
        shell_bottom(s, ex, ey, rx, ry, 1.0)
        if st == "pop":
            shell_cap(s, ex - 3.5, ey - 9.5, rx, ry * 0.6, deg=-30, ring=True)
        else:
            shell_cap(s, ex + 0.5, ey - 6.0 + low, rx, ry * 0.6, deg={"peek": 10, "look": -10, "duck": 0}[st],
                      ring=True)
    _paste(g, _fin(s), 6, -1)
    c = A.Canvas(28, 26)
    body = c.triangle((2, 16), (26, 16), (23, 25.5)) | c.triangle((2, 16), (23, 25.5), (5, 25.5))
    c.paint(body, "Wl")
    c.paint(body & ((((c.xx // 2) + (c.yy // 2)) % 2) == 0), "Wd")
    c.paint(c.rect(1, 15, 27, 17), "Wd")
    c.paint(c.rect(1, 15, 27, 16) & ((c.xx % 2) == 0), "Wl")
    _paste(g, _fin(c), 0, 2)
    return g


# ---- 8. a fried egg ----------------------------------------------------------------------------------------------

def fried(k):
    """A fried egg sizzles in a little iron pan over a small fire: the flames flicker, bubbles pop at the white's
    edge, steam curls up."""
    c = A.Canvas(36, 30)
    # fire under the pan: two crossed logs and flames
    c.paint(c.line(8, 29, 22, 26, 2) | c.line(9, 26, 23, 29, 2), "Wd")
    fl = [(12, 2.4, 6), (16, 2.8, 8), (20, 2.2, 5)]
    for i, (x, w, h) in enumerate(fl):
        hh = h + (1.5 if (k + i) % 2 else 0)
        c.paint(c.triangle((x - w, 27), (x + w, 27), (x + (0.6 if k % 2 else -0.6), 27 - hh)), "F3")
        c.paint(c.triangle((x - w * 0.55, 27), (x + w * 0.55, 27), (x, 27 - hh * 0.6)), "F2")
        c.paint(c.disc(x, 26, 0.9), "F1")
    # the pan
    c.paint(c.line(26, 19, 34, 16, 1.8), "Ih")
    pan = c.ellipse(16, 19, 11, 4.6)
    c.paint(pan, "Id")
    c.paint(c.ellipse(16, 18.4, 9.8, 3.4), "I")
    c.paint(c.rect(6, 21, 27, 24) & c.ellipse(16, 20.5, 10.5, 3.6), "Id")
    # the egg
    white = c.ellipse(15, 18.2, 6, 2.6) | c.disc(19.5, 18.4, 2.2) | c.disc(11, 17.6, 2.0)
    c.paint(white, "E")
    c.paint(c.ellipse(15.5, 17.6, 2.3, 1.6), "Yk")
    c.set(14, 17, "W")
    for i, (x, y) in enumerate(((9, 19), (21, 17), (13, 20), (18, 20), (22, 19))):
        if (k + i) % 3 == 0:
            c.set(x, y, "W")
    g = _fin(c)
    for j, x0 in enumerate((10, 17, 23)):                       # steam wisps rising
        for i in range(4):
            y = 13 - ((k + 2 * j) % 4) - 3 * i
            x = x0 + (1 if ((y // 2) % 2) else 0)
            if 0 <= y < 30 and i < 3:
                g[y, x] = PAL["St"]
                if y - 1 >= 0:
                    g[y - 1, x] = PAL["St"]
    for i, (x, y) in enumerate(((27, 12), (6, 10))):
        if (k + i) % 2:
            g[y, x] = PAL["F1"]
    return g


# ---- 9. a chick asleep in an eggshell ---------------------------------------------------------------------------------

def sleepy(k):
    """A chick sleeps in the bottom half of its eggshell, breathing slow; z marks float up."""
    rise = [0, 1, 1, 0][k]
    c = A.Canvas(26, 24)
    chick_front(c, 10, 11 - rise, eyes="shut", feet=False)
    shell_bottom(c, 10.5, 14, 6.0, 7.5, 1.5)
    g = _fin(c)
    for st, (x0, y0) in ((k % 4, (17, 0)), ((k + 2) % 4, (19, 2))):
        if st < 3:
            dx, dy, size = [(0, 7, 4), (2, 4, 4), (4, 0, 5)][st]
            _zee(g, x0 + dx - 2, y0 + dy, size, PAL["Z"])
    return g


# ---- 10. a hen in a panic ---------------------------------------------------------------------------------------------

def feather(c, x, y, flip=False):
    """A loose feather, 4 px long, tumbling (flip turns it the other way)."""
    c.paint(AN.rell(c, x, y, 2.3, 1.0, 30 if flip else -30), "B")
    c.paint(c.line(x - 1.6, y + (1.0 if not flip else -1.0), x + 1.2, y + (-0.6 if not flip else 0.6), 0.6), "D")


def panic(k):
    """A hen runs flat out in a panic, beak open, eyes wide, wings flapping, feathers flying off behind her and dust
    kicking up."""
    c = A.Canvas(34, 22)
    hen(c, 3, 3 - (k % 2), run=k / 4, eye="wide", beak=True, wing="up" if k % 2 == 0 else "down",
        head=(-1, 0.5), tail=1)
    g = _fin(c)
    f = A.Canvas(34, 22)
    for i, (x, y) in enumerate(((22, 5), (25, 11))):           # feathers drift off behind her
        ph = (k + 2 * i) % 4
        feather(f, x + 2 * ph, y - ph + (i * 2 if ph > 1 else 0), flip=ph % 2 == 1)
    fg = _fin(f)
    m = (fg != "") & (g == "")
    g[m] = fg[m]
    for i, (x, y, n) in enumerate(((24, 15, 4), (26, 18, 5), (22, 21, 4))):   # speed lines, no outline
        x += 2 * ((k + i) % 2)
        for dx in range(n):
            if g[y, x + dx] == "":
                g[y, x + dx] = PAL["St"]
    if k % 2 == 0:                                             # a "!" over her head
        for y in (0, 1, 2):
            g[y, 4] = PAL["R"]
        g[4, 4] = PAL["R"]
    return g


# ---- extras ---------------------------------------------------------------------------------------------------------

def viking(k):
    """A hen in a little horned Viking helmet struts on the spot, head bobbing, feet stepping."""
    c = A.Canvas(20, 22)
    head = [(-1, 0), (0.5, 0.5), (-1, 0), (0.5, 0.5)][k]
    hen(c, 1, 4, helmet=True, head=head, step=[1, 0, -1, 0][k], chest=1.0, eye="open" if k != 2 else "happy")
    return _fin(c)


def guard(k):
    """The ginger cat sits behind a nest of eggs keeping watch: its tail swings, it blinks, an egg wobbles."""
    g = _grid(38, 35)
    _paste(g, front_cat(k / 6, blink=k == 4, look="right" if k in (2, 3) else None), 4, 0)
    c = A.Canvas(36, 33)
    for x, d in ((11, 0), (24, 0), (17.5, [0, 0, 0, -12, 12, 0][k])):
        egg(c, x, 28.0 if x != 17.5 else 27.2, 2.6, 3.2, deg=d)
    nest(c, 18, 30.5, 13, 2.6)
    _paste(g, _fin(c), 0, 0)
    return g


RUNE = ["x..x", "x.x.", "xx..", "x..x", "x.x.", "xx..", "x...", "x...", "x..."]        # fehu


def rune_egg(k):
    """A big egg on a nest with a painted fehu rune that glows and dims; tiny sparkles blink round it."""
    c = A.Canvas(22, 24)
    egg(c, 11, 11.5, 6.5, 8.5)
    nest(c, 11, 20.5, 10, 2.8)
    g = _fin(c)
    col = PAL[["Rn", "Rg", "Ry", "Rg"][k]]
    for y, r in enumerate(RUNE):
        for x, ch in enumerate(r):
            if ch == "x":
                g[6 + y, 9 + x] = col
    for i, (x, y) in enumerate(((2, 4), (20, 7), (3, 14), (19, 1))):
        if (k + i) % 2 == 0:
            for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                g[y + dy, x + dx] = PAL["Ry"]
    return g


# ---- pieces ---------------------------------------------------------------------------------------------------------

def _anim(name, fn, n, fps):
    return A.Piece(name, "pixel", frames=[_rows(fn(k)) for k in range(n)], fps=fps)


def pieces():
    return [
        _anim("Hen laying an egg (moving)", laying, 6, 3),
        _anim("Egg hatching (moving)", hatching, 8, 4),
        _anim("Brooding hen (moving, breathing)", brooding, 6, 3),
        _anim("Chicks pecking (moving)", pecking, 6, 5),
        _anim("Chick and beetle (moving, chasing)", chase, 4, 6),
        _anim("Hen with chicks (moving, peeking)", under_wings, 6, 3),
        _anim("Egg basket (moving, chick peeking)", basket, 8, 4),
        _anim("Fried egg (moving, sizzling)", fried, 4, 5),
        _anim("Sleeping chick (moving, in a shell)", sleepy, 4, 2),
        _anim("Panicking hen (moving, running)", panic, 4, 8),
        _anim("Viking hen (moving, strutting)", viking, 4, 4),
        _anim("Cat guarding eggs (moving)", guard, 6, 3),
        _anim("Rune egg (moving, glowing)", rune_egg, 4, 3),
    ]
