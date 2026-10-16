"""Water animals: seals, fish, the sea serpent and a sea turtle as 8-bit pixel sprites, the cat's family
(concepts/anim2.py).

Seals, fish and the turtle use the cat's pitch (5.6 cm a pixel), so they stand next to the cat at their true
sizes (the fish a little generous, as the game's are); the sea serpent is drawn at a coarser pitch, only its head
and a few coils, and it is still the largest piece. Drawn with artlib.Canvas masks, outlined like the cat; most
face left. The animal is outlined first and the water painted over it afterwards, unoutlined, so what is under
the surface is simply gone; the water is a thin band, to keep the texts small. Every motion is a whole cycle
over the loop (phases k / n), and the first frame is a good still for players without the mod.
"""
import numpy as np

import artlib as A
from concepts.lib_animals import Pose, rell

TAU = 2 * np.pi
WATER = {"Wd": "#259", "Wa": "#39d", "Wf": "#eff", "Bb": "#aef"}


# ---- helpers ---------------------------------------------------------------------------------------------------

def ink(c):
    """Every empty pixel next to the drawing (4 neighbours) becomes the outline, in place (keep a 1 px margin)."""
    filled = c.g != ""
    pad = np.pad(filled, 1)
    near = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    c.g[~filled & near] = "K"
    return c


def done(c, pal):
    """An inked canvas -> '#rgb' rows."""
    return A.colours(c.g, {**WATER, **pal, "K": A.OUTLINE})


def sea(c, wy, t, deep, amp=0.8, x0=0):
    """A band of water from a rolling surface near wy down to row deep, right of column x0: everything drawn
    below the surface is hidden, then the water is painted (unoutlined); its crests roll left over the loop."""
    crest = wy + amp * np.sin(c.px * 0.42 + TAU * t) + 0.5 * amp * np.sin(c.px * 0.9 - 2 * TAU * t)
    below = (c.py >= crest) & (c.xx >= x0)
    c.g[below] = ""
    water = below & (c.py < deep)
    c.paint(water, "Wd")
    c.paint(water & (c.py < crest + 1.2), "Wa")
    c.paint(water & (c.py < crest + 1.2) & (np.sin(c.px * 0.42 + TAU * t) > 0.8), "Wf")


def ring(c, x, y, r):
    """A ripple on the water: a flat ring around (x, y), r 0..2 as it spreads."""
    rx, ry = 2 + 2.2 * r, 0.8 + 0.4 * r
    c.paint(c.ellipse(x, y, rx, ry) & ~c.ellipse(x, y, rx - 1.3, max(ry - 0.9, 0.3)), "Wf")


def bubbles(c, pts):
    """Bubbles [(x, y, big)] into the empty pixels: a small ring when big, else one pixel."""
    for x, y, big in pts:
        m = c.disc(x, y, 1.1) & ~c.disc(x - 0.2, y - 0.2, 0.4) if big else c.rect(int(x), int(y), int(x) + 1,
                                                                                     int(y) + 1)
        c.paint(m & (c.g == ""), "Bb")


def rise(n, t, x, y0, h, seed=0.0):
    """n bubbles rising from (x, y0) over h rows, spread evenly over the loop: [(x, y, big)]."""
    out = []
    for i in range(n):
        f = (t + i / n) % 1
        out.append((x + np.sin(TAU * (f * 1.5 + seed)), y0 - h * f, f > 0.55))
    return out


def path(c, x, y, ang, n, step, curl, r0, r1, bulge=0.0):
    """Discs along a curving path from (x, y) heading ang degrees (0 = right, 90 = up), turning curl degrees a
    step, the radius going r0 -> r1 and bulge wider in the middle: a flipper or a fin. -> the mask."""
    m = np.zeros((c.h, c.w), bool)
    for i in range(n):
        s = i / max(n - 1, 1)
        r = r0 + (r1 - r0) * s + bulge * np.sin(np.pi * s)
        m |= c.disc(x, y, r)
        x, y = x + step * np.cos(np.radians(ang)), y - step * np.sin(np.radians(ang))
        ang += curl
    return m


def over(c, g, x=0, y=0):
    """The non-empty pixels of key grid g over canvas c, g's top left at (x, y) (clipped to the canvas)."""
    g = np.asarray(g, dtype=object)
    h, w = g.shape
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + w, c.w), min(y + h, c.h)
    src = g[y0 - y:y1 - y, x0 - x:x1 - x]
    view = c.g[y0:y1, x0:x1]
    m = src != ""
    view[m] = src[m]


def layer(c):
    """A blank canvas the size of c, for a part drawn over the rest with its own outline (ink it, then over)."""
    return A.Canvas(c.w, c.h)


def bezier(p0, p1, p2, n):
    """n points along a quadratic curve and the unit normals on its right-hand side (screen: y down)."""
    s = np.linspace(0, 1, n)[:, None]
    p0, p1, p2 = (np.asarray(p, float) for p in (p0, p1, p2))
    pts = (1 - s) ** 2 * p0 + 2 * (1 - s) * s * p1 + s ** 2 * p2
    d = 2 * (1 - s) * (p1 - p0) + 2 * s * (p2 - p1)
    nrm = np.c_[-d[:, 1], d[:, 0]] / np.hypot(d[:, 0], d[:, 1])[:, None]
    return pts, nrm


# ---- seals -----------------------------------------------------------------------------------------------------

SEAL = {"B": "#99a", "D": "#667", "S": "#556", "L": "#ccd", "N": "#222", "P": "#111", "W": "#fff", "Wh": "#ddd",
        "R": "#887", "Rd": "#665", "Rl": "#aa9", "M": "#6a4"}

SEAL_HEAD = ["....BBBB...",
             "..BBBBBBBB.",
             ".BBBWPBBBBB",
             "BBBBPPBBBBB",
             "NNBBBBBBBBB",
             ".DDBBBBBBBB",
             "..LLLBBBBB.",
             "....LLBBB.."]


def seal_head(c, x, y, blink=False):
    """A harbour seal's round head facing left, its 11 x 8 box's top left at (x, y): a big dark eye, a short
    dog-like muzzle with a dark nose."""
    g = A.ascii(SEAL_HEAD, SEAL)
    if blink:
        g[2, 4:6] = "B"
        g[3, 4:6] = "S"
    over(c, g, x, y)


def whiskers(c, x, y):
    """White whiskers ahead of the nose of a head at (x, y), after ink."""
    for dy in (4, 6):
        if 0 <= x - 1 < c.w and c.g[y + dy, x - 1] in ("", "K"):
            c.set(x - 1, y + dy, "Wh")


def seal_rock(t, clap):
    """A harbour seal lying on a rock by the sea, facing left, its front up; clap 0 flippers apart .. 1 together
    with a clap; the tail flippers wave and the waves lap at the rock."""
    c = A.Canvas(46, 32)
    wy = 28
    c.paint(c.ellipse(24, 31, 19, 8.5) | c.ellipse(33, 28, 9, 6), "R")          # the rock
    c.paint(c.ellipse(30, 24.5, 7, 2.2) & c.ellipse(24, 31, 19, 8.5), "Rl")      # its sunlit top
    c.paint(c.ellipse(13, 28, 4, 2) | c.ellipse(26, 28.5, 3, 1.4), "Rd")
    c.paint(c.rect(38, 22, 41, 23) | c.rect(9, 24, 11, 25), "M")                 # a little weed
    tip = 1.5 * np.sin(TAU * t)                                                  # the tail flippers wave
    c.paint(c.triangle((36, 19), (42, 14 + tip), (43.5, 17 + tip)) | c.triangle((36, 19.5), (43.5, 19), (42, 22)),
            "D")
    c.paint(c.ellipse(27, 19.6, 10, 3.6) | c.line(20, 19.5, 37.5, 19, 3.6), "B")   # body along the rock
    c.paint(rell(c, 18.5, 15, 5, 6.6, 18), "B")                                # chest, raised
    c.paint(c.ellipse(26, 22, 9, 1.2), "L")                                      # pale belly
    for x, y in ((24, 17), (27, 18), (30, 17), (33, 18), (25, 20), (22, 13), (29, 20), (35, 20), (21, 17)):
        c.set(x, y, "S")
    hy = 2 - (clap > 0.9)
    seal_head(c, 10, hy)
    sx, sy = 16.5, 16.5
    c.paint(path(c, sx + 1, sy - 0.5, 180 - 26 * (1 - clap), 7, 1.1, 4 * (1 - clap), 1.3, 0.9), "S")  # far flipper
    ink(c)
    f = layer(c)                                                                 # near flipper, outlined over the chest
    f.paint(path(f, sx, sy + 1, 180 + 26 * (1 - clap), 7, 1.1, -4 * (1 - clap), 1.5, 1.0), "D")
    over(c, ink(f).g)
    whiskers(c, 10, hy)
    if clap > 0.9:                                                               # the clap
        for x0, y0, x1, y1 in ((7, 13, 6, 11), (6, 16, 4, 16), (7, 19, 6, 21)):
            c.paint(c.line(x0 + 0.5, y0 + 0.5, x1 + 0.5, y1 + 0.5, 1) & (c.g == ""), "W")
    sea(c, wy, t, 32)
    return done(c, SEAL)


def seal_dive(k):
    """A seal in the sea, facing left: k 0..8, the head up looking, a blink, the nose going under, the roll of
    its back, the hind flippers last, the rings, and the head coming up again."""
    c = A.Canvas(36, 18)
    wy = 12
    if k in (0, 1, 2):                                                           # head and neck out
        c.paint(c.line(16, 16, 15, 8 + (k == 2), 5.6), "B")
        c.paint(rell(c, 13.5, 12, 1.6, 3, 10) & (c.py > 10), "L")
        seal_head(c, 8, 2 + (k == 2), blink=(k == 1))
    elif k == 3:                                                                 # going under, nose first
        c.paint(rell(c, 15, 11, 6, 3.2, -20), "B")
        c.paint(c.rect(10, 9, 12, 11), "P")
    elif k == 4:                                                                 # the back rolling over
        c.paint(c.ellipse(19, 12, 8, 3.6), "B")
        for x, y in ((15, 10), (19, 9), (22, 10)):
            c.set(x, y, "S")
    elif k == 5:                                                                 # the hind flippers last
        c.paint(c.ellipse(24, 12.5, 4, 2.4), "B")
        c.paint(c.triangle((24, 11), (27, 4), (29.5, 6)) | c.triangle((24, 11), (30.5, 7.5), (30, 10.5)), "D")
    elif k == 8:                                                                 # up again: the top of the head
        c.paint(c.ellipse(13, 12, 3.4, 2.8), "B")
        c.paint(c.rect(10, 10, 12, 12), "P")
        c.set(10, 10, "W")
    ink(c)
    if k in (0, 1, 2):
        whiskers(c, 8, 2 + (k == 2))
    if k == 5:
        for x, y in ((31, 4), (32, 8), (26, 2)):
            c.set(x, y, "Wf")
    sea(c, wy, k / 9, 17)
    if k in (6, 7):
        ring(c, 25, wy + 1.5, k - 5)
    if k in (3, 8):
        ring(c, 13 if k == 8 else 16, wy + 1.5, 0.4)
    return done(c, SEAL)


PUP = {"W": "#eef", "Ws": "#bcd", "N": "#222", "P": "#111", "Wt": "#fff", "I": "#cef", "Id": "#9cd", "Il": "#fff"}


def seal_pup(k):
    """A white seal pup on an ice floe, facing out: big black eyes, a blink, a flipper wave; the floe bobs."""
    c = A.Canvas(34, 23)
    wy = 18
    bob = (0, 0, 1, 1, 0, 0)[k]
    c.paint(c.rect(3, 17 + bob, 31, 22) | c.ellipse(17, 17 + bob, 14, 2.2), "I")   # the floe
    c.paint(c.rect(3, 19 + bob, 31, 22), "Id")
    c.paint(c.ellipse(26, 16.5 + bob, 3, 0.7), "Il")
    by = 12 + bob
    c.paint(c.ellipse(19, by + 1, 9, 4.2), "W")                                  # the plump body, lying
    c.paint(c.ellipse(19, by + 3.6, 7, 1.2), "Ws")
    c.paint(c.triangle((27, by + 2), (31.5, by - 1 + (k == 3)), (31.5, by + 3.5)), "Ws")   # hind flippers
    if k not in (3, 4):
        c.paint(path(c, 18, by + 2, -25, 5, 1.0, 0, 1.2, 0.9), "Ws")
    hx, hy = 10, by - 2
    c.paint(c.disc(hx, hy, 5), "W")                                              # the round head, facing out
    c.paint(c.ellipse(hx, hy + 2.4, 2.4, 1.6), "Ws")
    c.paint(c.rect(hx - 1, hy + 1, hx + 1, hy + 2), "N")                         # nose
    c.set(hx, hy + 3, "N")
    if k == 1:
        c.paint(c.rect(hx - 4, hy - 1, hx - 1, hy) | c.rect(hx + 1, hy - 1, hx + 4, hy), "N")
    else:
        c.paint(c.rect(hx - 4, hy - 2, hx - 1, hy + 1) | c.rect(hx + 1, hy - 2, hx + 4, hy + 1), "P")
        c.set(hx - 3, hy - 2, "Wt")
        c.set(hx + 2, hy - 2, "Wt")
    for x, y in ((hx - 5, hy + 2), (hx + 5, hy + 2)):                            # whiskers
        c.set(x, y, "Ws")
    ink(c)
    if k in (3, 4):                                                              # a flipper waves
        f = layer(c)
        f.paint(path(f, 20, by + 1, 60 + 35 * (k == 4), 5, 1.1, 8, 1.3, 0.9), "W")
        over(c, ink(f).g)
    sea(c, wy + 1.5 + bob, k / 6, 22, amp=0.6)
    return done(c, PUP)


# ---- fish ------------------------------------------------------------------------------------------------------

PERCH = {"B": "#ab4", "D": "#563", "L": "#eed", "Br": "#453", "F": "#e52", "Fd": "#b32", "G": "#9a8",
         "Gs": "#453", "E": "#fc3", "P": "#111", "We": "#4a5", "Wg": "#273"}


def perch_at(c, x, y, ang=0.0, sway=0.0, fin=0):
    """A European perch, its centre at (x, y), facing left when ang = 0 (Pose degrees): a humped back under the
    tall spiny first dorsal fin with its dark spot, dark bars down the golden-olive flank, red-orange belly fins
    and tail; sway turns the tail (degrees), fin flutters the belly fins."""
    h = Pose(c, x, y, ang)
    t = Pose(c, *h.p(7.5, 0), ang + sway)                                        # the tail, turning about its root
    c.paint(t.tri((-0.5, 0), (5, -4.2), (4.2, 0)) | t.tri((-0.5, 0), (4.2, 0), (5, 4.2)), "F")
    c.paint(t.tri((-0.5, 0), (5, -4.2), (3.4, -1)), "Fd")
    c.paint(h.ell(-2, -5.5, 3.8, 3.4) & ~h.ell(-2, 1, 9, 6.6) | h.tri((-5.5, -3), (1.6, -3), (-1.5, -8.8)), "G")
    for u in (-4.2, -2.4, -0.6):                                                 # spiny dorsal: spines, the spot
        c.paint(h.line(u, -3.5, u - 0.2, -8.6 + abs(u + 2.4) * 0.6, 0.9), "Gs")
    c.paint(h.dot(0.6, -4.6), "P")
    c.paint(h.tri((2.5, -2.8), (7.4, -1.2), (4.5, -5.6)), "F")                   # soft dorsal
    c.paint(h.tri((2.5, 2.6), (7, 1.2), (4.5, 5.4 + fin * 0.6)), "F")            # anal fin
    c.paint(h.ell(0, 0, 8.6, 4) | h.ell(-2.5, -0.8, 5.6, 4.2), "B")              # body, humped behind the head
    c.paint(h.ell(0.5, -2.8, 7, 1.4) | h.ell(-2.5, -3.8, 4.5, 1.2), "D")
    c.paint(h.ell(-0.5, 2.7, 6.5, 1.2), "L")
    for u in (-1.5, 1.5, 4.3):                                                   # the dark bars
        c.paint(h.line(u, -3.6, u + 0.4, 1.2, 1.3), "Br")
    c.paint(h.tri((-3.5, 2.6), (-0.5, 3.2), (-2.5 + fin, 6.6)), "F")             # pelvic fin
    c.paint(h.line(-4.6, -2.6, -4.6, 2.4, 1), "D")                               # gill cover edge
    c.paint(h.dot(-6.4, -1.4) | h.dot(-6.4, -0.4), "E")
    c.paint(h.dot(-6.9, -1.4) if ang == 0 else h.dot(-6.4, -0.9), "P")         # the pupil: ahead, or in it turned
    c.paint(h.line(-8.6, 0.6, -7, 0.8, 1), "D")


def perch(k):
    """A perch hovering among water weed, facing left: the tail sways, the fins flutter, it bobs, bubbles rise
    from its mouth."""
    c = A.Canvas(34, 30)
    t = k / 6
    for x0, ph, hgt in ((3, 0, 20), (28, 0.4, 16), (31, 0.7, 11)):              # water weed swaying
        for i in range(hgt):
            dx = int(round(1.4 * np.sin(TAU * (t + ph) + i * 0.25) * i / hgt))
            c.set(x0 + dx, 29 - i, "We")
            if i % 4 == 2:
                c.set(x0 + dx + (1 if i % 8 == 2 else -1), 29 - i, "Wg")
    perch_at(c, 15, 17 + round(0.8 * np.sin(TAU * t + 1)), sway=14 * np.sin(TAU * t), fin=k % 2)
    ink(c)
    bubbles(c, rise(3, t, 5.5, 15, 12, seed=0.2))
    return done(c, PERCH)


def hooked(k):
    """A perch caught on a fishing hook, hanging head up from the line and wriggling; water drips from its tail."""
    c = A.Canvas(26, 40)
    swing = (0, 10, 0, -10)[k]
    bend = (0, -22, 0, 22)[k]
    mx, my = 13, 12
    a = np.radians(swing)
    perch_at(c, mx + 9 * np.sin(a), my + 9 * np.cos(a), ang=90 - swing, sway=bend, fin=k % 2)
    ink(c)
    c.paint(c.rect(12, 10, 13, 14) | c.rect(10, 13, 12, 14) | c.rect(10, 12, 11, 13), "Hk")   # the hook in the lip
    c.g[:10, 12] = "Ln"                                                          # the line, thin, unoutlined
    tx = int(round(mx + 20 * np.sin(a)))
    for i in range(2):                                                           # drips falling from the tail
        f = (k / 4 + i / 2) % 1
        y = int(33 + 6 * f)
        if c.g[y, tx] == "":
            c.set(tx, y, "Dr")
    return done(c, {**PERCH, "Ln": "#ddd", "Hk": "#99a", "Dr": "#8cf"})


PIKE = {"B": "#685", "D": "#353", "L": "#ddb", "Sp": "#cd8", "F": "#a64", "Fd": "#643", "E": "#ec3", "P": "#111",
        "T": "#fff", "M": "#933", "S": "#ccd", "Sd": "#68a", "R": "#594", "Rd": "#373"}


def pike(k):
    """A pike lurking in the reeds, facing left: a long olive body with pale spots, a flat duck-bill head and its
    fins far back; a minnow swims in, the pike opens its jaws, lunges and snaps, and settles back."""
    c = A.Canvas(58, 20)
    lunge = (0, 0, 1, 7, 7, 4, 2, 0)[k]
    jaw = (0, 0, 1, 2, 0, 0, 0, 0)[k]
    mx = (3, 4, 5, None, None, None, None, 1)[k]
    for x0, hgt, ph in ((50, 17, 0), (53, 13, 0.5), (56, 19, 0.2)):              # reeds behind
        lean = 1.0 * np.sin(TAU * k / 8 + ph * TAU)
        c.paint(c.line(x0 + 0.5, 20, x0 + 0.5 + lean, 20 - hgt, 1), "R" if hgt > 15 else "Rd")
    x, y = 22 - lunge, 10
    tw = 1.2 * np.sin(TAU * k / 8)
    c.paint(c.triangle((x + 25, y), (x + 31, y - 5 + tw), (x + 30, y + 0.5)) |
            c.triangle((x + 25, y), (x + 30, y - 0.5), (x + 31, y + 5 + tw)), "F")   # tail
    c.paint(c.triangle((x + 17, y - 2.5), (x + 23.5, y - 1.5), (x + 22, y - 6.5)), "F")   # dorsal, far back
    c.paint(c.triangle((x + 17, y + 2.5), (x + 23.5, y + 1.5), (x + 22, y + 6)), "F")     # anal fin below it
    c.paint(c.ellipse(x + 11, y, 12.5, 3.8) | c.line(x + 19, y, x + 27, y, 2.6), "B")   # the long body
    c.paint(c.ellipse(x + 11, y - 2.4, 11, 1.3), "D")
    c.paint(c.ellipse(x + 10, y + 2.8, 8.5, 1.0), "L")
    for i in range(7):                                                           # pale bean spots
        sx = x + 5 + 2.6 * i
        c.set(int(sx), int(y - 1 + (i % 2)), "Sp")
        if i % 3 == 1:
            c.set(int(sx) + 1, int(y + 1.5), "Sp")
    c.paint(c.triangle((x + 2, y + 2.5), (x + 6, y + 2.5), (x + 4.5, y + 5.5)), "Fd")   # pectoral
    c.paint(c.triangle((x + 10, y + 3), (x + 14, y + 3), (x + 13, y + 6)), "F")         # pelvic
    if jaw:                                                                      # the gape, the lower jaw dropped
        c.paint(c.triangle((x + 1, y + 1), (x - 9, y - 1), (x - 9, y + 1 + 2.5 * jaw)), "M")
        c.paint(c.line(x + 1, y + 2, x - 8.5, y + 1.5 + 2.5 * jaw, 1.4), "L")
    c.paint(c.ellipse(x - 1, y - 0.4, 4.5, 3.0) | c.ellipse(x - 5.5, y - 0.4 - 0.5 * jaw, 4, 1.5), "B")   # the head
    c.paint(c.ellipse(x - 5, y - 1.3 - 0.5 * jaw, 3.5, 0.6) | c.ellipse(x - 1, y - 2.3, 3.6, 0.8), "D")
    c.paint(c.line(x + 2.5, y - 2.5, x + 2.5, y + 2.5, 1), "D")                 # gill cover
    c.paint(c.rect(int(x - 3), int(y - 2), int(x - 1), int(y - 1)), "E")
    c.set(int(x - 3), int(y - 2), "P")
    if jaw:
        for i in range(4):
            c.set(int(x - 7 + 2 * i), int(y + 0.5 * jaw - 0.2 * i), "T")
            c.set(int(x - 7 + 2 * i), int(y + 2.5 * jaw - 0.6 * i), "T")
    else:
        c.paint(c.line(x - 9, y + 1, x + 1, y + 1.6, 1), "L")                    # closed jaw, pale
    if mx is not None:                                                           # the minnow, coming in
        c.paint(c.triangle((mx + 4, 10), (mx + 6.5, 8.2), (mx + 6.5, 11.8)), "Sd")
        c.paint(c.ellipse(mx + 2.4, 10, 2.8, 1.4), "S")
        c.set(int(mx + 1), 9, "P")
    ink(c)
    if k in (3, 4):
        bubbles(c, [(4, 4, True), (7, 1, False), (2, 8, False)] if k == 3 else [(5, 1, True), (3, 5, False)])
    return done(c, PIKE)


HERRING = {"B": "#36a", "S": "#bcd", "L": "#eef", "W": "#fff", "T": "#58b", "P": "#111"}

HERRING_FISH = (["...BBBBBB...TT",                                              # tail spread, tail turned
                 ".BPBBBBBBB.TT.",
                 "SSSSSSSSSSTT..",
                 ".SSSSSSSSS.TT.",
                 "...LLLLLL...TT"],
                ["...BBBBBB.....",
                 ".BPBBBBBBB..TT",
                 "SSSSSSSSSSTTT.",
                 ".SSSSSSSSS.TTT",
                 "...LLLLLL...T."])


def herring(c, x, y, wag, flash):
    """A herring facing left, its 14 x 5 box's top left at (x, y): a dark blue back, a silver flank (white in a
    flash), a forked tail wagging."""
    g = A.ascii(HERRING_FISH[wag], HERRING)
    if flash:
        g[g == "S"] = "W"
    over(c, g, x, y)


def shoal(k):
    """A shoal of herring swimming left in formation, each fish bobbing in its own time; a silver flash runs
    through the shoal."""
    c = A.Canvas(50, 32)
    t = k / 6
    spots = ((1, 11), (11, 3), (12, 18), (21, 10), (19, 25), (31, 2), (32, 16), (35, 24))
    for i, (x, y) in enumerate(spots):
        dy = int(np.sin(TAU * (t + i * 0.3)) > 0.2)
        herring(c, x, y + dy, (k + i) % 2, i == (k * 3) % 8)
    ink(c)
    bubbles(c, rise(2, t, 2, 9, 8, seed=0.1) + rise(2, t + 0.5, 25, 20, 9, seed=0.6))
    return done(c, HERRING)


SALMON = {"B": "#abc", "D": "#456", "R": "#d57", "L": "#eef", "Sp": "#223", "F": "#567", "P": "#111",
          "Rk": "#776", "Rd": "#665", "Rl": "#998", "M": "#6a4", "Fa": "#8ce", "Fb": "#dff"}


def salmon_at(c, x, y, ang):
    """A spawning salmon, its centre at (x, y), facing left when ang = 0: dark spotted back, red flank, the
    hooked jaw."""
    h = Pose(c, x, y, ang)
    c.paint(h.tri((5.5, 0), (10, -3.4), (9, 0)) | h.tri((5.5, 0), (9, 0), (10, 3.4)), "F")
    c.paint(h.tri((-1.5, -2), (2.5, -2), (0.5, -4.8)), "F")
    c.paint(h.ell(0, 0, 7.4, 2.7), "B")
    c.paint(h.ell(0.5, -1.5, 6, 1.1), "D")
    c.paint(h.ell(0.5, 0.4, 5.4, 1.0), "R")
    c.paint(h.ell(0, 1.9, 5.2, 0.7), "L")
    for u in (-2.5, 0.5, 3.5):
        c.paint(h.dot(u, -1.4), "Sp")
    c.paint(h.line(-7.6, 0.4, -6, 1.6, 1), "D")                                  # the hooked jaw
    c.paint(h.dot(-5, -0.8), "P")


def salmon(k):
    """A salmon leaping up a waterfall: the falls pour over a rocky ledge into a pool; the salmon jumps from the
    pool, arcs up past the falls and into the river above, then the next one comes."""
    c = A.Canvas(42, 44)
    t = k / 8
    c.paint(c.rect(0, 12, 17, 44) | c.ellipse(8, 12, 9, 2.5), "Rk")              # the ledge, left
    for y0, x0, x1 in ((19, 0, 11), (20, 11, 17), (27, 4, 17), (34, 0, 9), (35, 9, 15)):   # strata, a crack
        c.paint(c.rect(x0, y0, x1, y0 + 1), "Rd")
    c.paint(c.rect(15, 13, 17, 44) | c.rect(6, 28, 7, 34), "Rd")                # the shaded edge by the falls
    c.paint(c.rect(1, 14, 7, 15) | c.rect(5, 21, 10, 22) | c.rect(10, 29, 14, 30) | c.rect(2, 36, 6, 37), "Rl")
    c.paint(c.rect(1, 11, 4, 12), "M")
    pos = {0: (27, 20, 52), 1: (22, 11, 25), 2: (14, 7, 0), 6: (34, 35, 80), 7: (31, 28, 66)}
    if k in pos:
        salmon_at(c, *pos[k])
    ink(c)
    fish = (c.g != "") & (c.g != "K") & (c.xx >= 17)
    falls = c.rect(17, 9, 23, 40) & ~fish & ~((c.g == "K") & (c.xx >= 18))
    c.paint(falls, "Fa")
    c.paint(falls & ((c.yy - 2 * k) % 5 == 0) & (c.xx % 2 == 1), "Fb")
    c.paint(c.rect(0, 8, 17, 11) & (c.g == ""), "Fa")                            # the river above
    c.paint(c.rect(0, 8, 17, 9) & (c.g == "Fa") & (c.xx % 4 != k % 4), "Fb")
    sea(c, 39, t, 44, amp=0.6, x0=17)
    for i, (x, y) in enumerate(((18, 38), (21, 37.5), (24, 38.5))):              # foam at the foot of the falls
        c.paint(c.disc(x + (k + i) % 2, y, 1.3), "Fb")
    if k == 2:
        for x, y in ((9, 6), (12, 5), (16, 6)):
            c.set(x, y, "Fb")
    if k == 5:
        ring(c, 34, 40.5, 0.6)
        for x, y in ((33, 36), (35, 35), (31, 37), (37, 37)):
            c.set(x, y, "Fb")
    return done(c, SALMON)


# ---- sea serpent -----------------------------------------------------------------------------------------------

SERPENT = {"B": "#364", "D": "#132", "U": "#ca7", "F": "#b43", "Fs": "#511", "Y": "#fd2", "P": "#111",
           "T": "#ffe", "M": "#511"}


def tube(c, pts, rs, back):
    """A scaly body along a path: discs of radius rs at pts, a dark back stripe on the `back` side (unit normals)
    and a pale belly on the other."""
    body = np.zeros((c.h, c.w), bool)
    belly = np.zeros_like(body)
    top = np.zeros_like(body)
    for (x, y), r, (nx, ny) in zip(pts, rs, back):
        body |= c.disc(x, y, r)
        belly |= c.disc(x - nx * r * 0.75, y - ny * r * 0.75, r * 0.45)
        top |= c.disc(x + nx * r * 0.7, y + ny * r * 0.7, r * 0.42)
    c.paint(body, "B")
    c.paint(body & top, "D")
    c.paint(body & belly, "U")


def crest(c, pts, rs, back, h, every, phase=0.0):
    """A webbed fin crest along a path on the back side: a saw of red webbing, a dark spine at each tooth."""
    seg = np.r_[0, np.cumsum(np.hypot(*np.diff(pts, axis=0).T))]
    fin = np.zeros((c.h, c.w), bool)
    spine = np.zeros_like(fin)
    for (x, y), r, (nx, ny), s in zip(pts, rs, back, seg):
        f = ((s + phase) / every) % 1
        hh = h * (0.3 + 0.7 * f)
        bx, by = x + nx * (r - 0.5), y + ny * (r - 0.5)
        fin |= c.line(bx, by, bx + nx * hh, by + ny * hh, 1.3)
        if f > 0.85:
            spine |= c.line(bx, by, bx + nx * (hh + 0.6), by + ny * (hh + 0.6), 1.0)
    c.paint(fin, "F")
    c.paint(spine, "Fs")


def serpent_head(c, x, y, s, ang, jaw):
    """The serpent's head, its skull at (x, y) and s times the base size, facing left when ang = 0: a long
    armoured snout, a yellow eye under a dark brow, a frill of fins swept back; jaw opens the mouth (degrees)."""
    h = Pose(c, x, y, ang)
    roots = ((1.0, -2.6), (3.0, -2.0), (4.2, -0.4), (4.0, 1.6))
    tips = ((4.5, -6.2), (7.5, -4.4), (8.5, -1.0), (6.8, 2.8))
    for i in range(len(roots) - 1):                                              # the frill: webs, then spines
        c.paint(h.tri([v * s for v in roots[i]], [v * s for v in tips[i]], [v * s for v in roots[i + 1]]) |
                h.tri([v * s for v in tips[i]], [v * s for v in tips[i + 1]], [v * s for v in roots[i + 1]]), "F")
    for r, tp in zip(roots, tips):
        c.paint(h.line(r[0] * s, r[1] * s, tp[0] * s, tp[1] * s, 1), "Fs")
    hinge = (0.5 * s, 1.3 * s)
    lj = Pose(c, *h.p(*hinge), ang - jaw)
    if jaw:
        a = np.radians(jaw)
        c.paint(h.tri(hinge, (-9.5 * s, 1.0 * s), (hinge[0] - 9 * s * np.cos(a), hinge[1] + 9 * s * np.sin(a))), "M")
    c.paint(lj.ell(-4.8 * s, 0, 4.8 * s, 1.2 * s), "U")                           # the lower jaw
    if jaw:
        for i in range(4):
            c.paint(lj.dot(-8 * s + 2.2 * s * i, -1.0 * s), "T")
    c.paint(h.ell(0, 0, 4.4 * s, 3 * s) | h.ell(-5 * s, 0, 4.8 * s, 1.7 * s), "B")   # skull and long snout
    c.paint(h.ell(-2 * s, -2 * s, 3.4 * s, 0.9 * s) | h.ell(1.5 * s, -2.3 * s, 2.8 * s, 0.9 * s), "D")   # brow
    c.paint(h.ell(-1.2 * s, -0.8 * s, 1.1 * s, 0.7 * s), "Y")
    c.paint(h.dot(-1 * s, -0.8 * s), "P")
    c.paint(h.dot(-9 * s, -0.6 * s), "P")                                         # nostril
    for u in (2.2, 3.6):
        c.paint(h.line(u * s, -0.8 * s, u * s + 0.5, 1.6 * s, 0.9), "D")         # gill slits
    if jaw:
        for i in range(4):
            c.paint(h.dot(-8.5 * s + 2.2 * s * i, 1.4 * s), "T")


def neck_of(p0, p1, p2, n, r0, r1):
    """A neck along a quadratic curve: points, radii r0 -> r1 and the normals on its back (facing right)."""
    pts, nrm = bezier(p0, p1, p2, n)
    nrm *= np.where(nrm[:, :1] >= 0, 1, -1)                                      # the back faces right
    return pts, np.linspace(r0, r1, n), nrm


def serpent_coils(k):
    """The sea serpent swimming left through the waves: its head reared up on the neck, two scaly coils with
    fin crests arching in and out of the water and rolling back along it; the jaws part now and then."""
    c = A.Canvas(64, 36)
    t = k / 8
    wy = 29
    xs = np.arange(25, 63.5, 0.5)
    ys = wy + 3 - 9 * np.sin(TAU * (xs - 25) / 19 - TAU * t + 0.6)
    dy = np.gradient(ys, xs)
    back = np.c_[dy, -np.ones_like(dy)] / np.hypot(dy, 1)[:, None]
    rs = np.where(xs > 54, 3.0 - 1.6 * (xs - 54) / 9, 3.0)
    up = ys < wy + 3
    pts = np.c_[xs, ys]
    crest(c, pts[up], rs[up], back[up], 3.0, 3.0, phase=-19 * t)
    tube(c, pts, rs, back)
    sway = np.sin(TAU * t)
    hx, hy = 14 + 0.6 * sway, 9 + 0.8 * np.cos(TAU * t)
    npts, nrs, nb = neck_of((21, wy + 5), (23, 6), (hx + 4, hy), 30, 3.4, 2.8)
    crest(c, npts, nrs, nb, 3.4, 3.0)
    tube(c, npts, nrs, nb)
    serpent_head(c, hx, hy, 1.15, -6 + 3 * sway, (0, 0, 0, 0, 10, 18, 10, 0)[k])
    ink(c)
    sea(c, wy, t, 34, amp=1.0)
    return done(c, SERPENT)


def serpent_rise(k):
    """The sea serpent's head and neck reared high out of the sea, facing left, swaying; it opens its jaws wide
    and roars, and the sea foams round its neck."""
    c = A.Canvas(48, 58)
    t = k / 8
    wy = 52
    sway = np.sin(TAU * t)
    jaw = (0, 0, 6, 14, 20, 20, 12, 4)[k]
    hx, hy = 19 + 1.2 * sway, 13 + 1.0 * np.cos(TAU * t)
    npts, nrs, nb = neck_of((32, wy + 6), (42 - 2 * sway, 22), (hx + 6, hy + 1), 44, 5.0, 3.8)
    crest(c, npts, nrs, nb, 5.0, 4.0)
    tube(c, npts, nrs, nb)
    serpent_head(c, hx, hy, 1.7, -12 + 5 * sway - jaw * 0.3, jaw)
    ink(c)
    sea(c, wy, t, 57, amp=1.0)
    for i in range(4):                                                           # foam round the neck
        a = TAU * (i / 4 + t / 2)
        c.paint(c.disc(32 + 7 * np.cos(a), wy + 1 + 0.8 * np.sin(a), 1.1), "Wf")
    return done(c, SERPENT)


# ---- sea turtle ------------------------------------------------------------------------------------------------

TURTLE = {"S": "#864", "Sd": "#532", "Sl": "#a85", "Sr": "#cb8", "K2": "#9a8", "Kd": "#675", "Ks": "#bca",
          "P": "#111", "U": "#dc9"}


def turtle(k):
    """A sea turtle swimming left: a low streamlined shell with its plates, a beaked head, long front flippers
    beating like wings, small hind flippers paddling; it rises and sinks with each stroke, bubbles trail behind."""
    c = A.Canvas(42, 34)
    t = k / 6
    y = 15 + round(1.2 * np.sin(TAU * t - 0.6))
    beat = np.cos(TAU * t)                                                       # 1 up, -1 down
    up = 70 * (beat + 1) / 2 - 60 * (1 - beat) / 2                              # the flipper's angle: 70 .. -60
    curl = -6 if up > 0 else 6
    sx, sy = 13, y + 2
    c.paint(path(c, sx + 3, sy - 1, up + 15, 9, 1.2, curl, 1.2, 0.6, bulge=0.8), "Kd")     # far front flipper
    c.paint(path(c, 31, y + 2.5, -25 - 12 * beat, 6, 1.0, 6, 1.4, 0.8), "Kd")   # hind flipper
    c.paint(c.ellipse(8, y + 1.5, 4, 2.8) | c.ellipse(4.8, y + 2.2, 2.6, 1.8), "K2")   # head
    c.paint(c.line(3, y + 3.2, 6.5, y + 3.4, 1), "Kd")                           # beak line
    c.paint(c.rect(6, y, 8, y + 2), "P")
    c.set(6, y, "Ks")
    c.paint(c.rect(9, y - 1, 11, y) | c.rect(9, y + 3, 11, y + 4), "Kd")         # scales
    c.paint(c.line(10, y + 2, 14, y + 2.6, 3.6), "K2")                           # neck
    c.paint(c.ellipse(23, y + 3.6, 11, 1.8), "U")                                # plastron
    shell = c.ellipse(23, y + 3.6, 12.5, 7.6) & (c.py < y + 3.6)
    c.paint(shell, "S")
    c.paint(shell & ~c.ellipse(23, y + 3.6, 11.5, 6.6), "Sr")                      # the rim
    for cx, cy in ((18, y - 0.5), (23, y - 2), (28, y - 0.5), (20.5, y + 2), (25.5, y + 2), (15, y + 2.2),
                   (31, y + 2.2)):
        c.paint(c.ellipse(cx, cy, 2.1, 1.2) & shell & c.ellipse(23, y + 3.6, 11.5, 6.6), "Sl")
    c.paint(shell & ~c.ellipse(23, y + 3.6, 11.5, 6.6) & (c.xx % 3 == 0), "Sd")
    ink(c)
    f = layer(c)                                                                 # near front flipper, a long paddle
    f.paint(path(f, sx, sy, up, 11, 1.2, curl, 1.4, 0.6, bulge=1.0), "K2")
    f.paint(path(f, sx, sy, up, 5, 1.2, curl, 0.5, 0.5) & ~f.disc(sx, sy, 1.5), "Ks")
    over(c, ink(f).g)
    bubbles(c, rise(3, t, 38, y + 2, 12, seed=0.3))
    return done(c, TURTLE)


# ---- pieces ----------------------------------------------------------------------------------------------------

def pieces():
    return [
        A.Piece("Seal (moving, clapping)",
                frames=[seal_rock(k / 8, c) for k, c in enumerate((1, 0.5, 0, 0.5, 1, 0.5, 0, 0.5))], kind="pixel",
                fps=6),
        A.Piece("Seal (moving, diving)", frames=[seal_dive(k) for k in range(9)], kind="pixel", fps=4),
        A.Piece("Seal pup (moving, on the ice)", frames=[seal_pup(k) for k in range(6)], kind="pixel", fps=4),
        A.Piece("Salmon (moving, leaping a waterfall)", frames=[salmon(k) for k in range(8)], kind="pixel", fps=5),
        A.Piece("Perch (moving, swimming)", frames=[perch(k) for k in range(6)], kind="pixel", fps=5),
        A.Piece("Pike (moving, snapping)", frames=[pike(k) for k in range(8)], kind="pixel", fps=5),
        A.Piece("Herring shoal (moving)", frames=[shoal(k) for k in range(6)], kind="pixel", fps=5),
        A.Piece("Fish on a hook (moving, wriggling)", frames=[hooked(k) for k in range(4)], kind="pixel", fps=5),
        A.Piece("Sea serpent (moving, coils in the waves)", frames=[serpent_coils(k) for k in range(8)],
                kind="pixel", fps=5, pitch=3),
        A.Piece("Sea serpent (moving, roaring)", frames=[serpent_rise(k) for k in range(8)], kind="pixel", fps=5,
                pitch=3),
        A.Piece("Sea turtle (moving, swimming)", frames=[turtle(k) for k in range(6)], kind="pixel", fps=5),
    ]
