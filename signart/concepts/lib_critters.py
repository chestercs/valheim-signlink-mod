"""Critters: the small and tame animals of a Nordic world (forest, meadow, farm, river, mountain) as 8-bit pixel
sprites, the cat's family (concepts/anim2.py).

Every sprite uses the cat's pitch (5.6 cm a pixel), so each one stands next to the cat at its true size: a mouse
is a handful of pixels, the pony fills most of the 64 px limit. Drawn with artlib.Canvas masks, outlined like the
cat; most face left. Every piece moves, and its first frame is a good still for players without the mod.
"""
import numpy as np

import artlib as A
from concepts.lib_animals import Pose, finish, rell


def chain(c, x, y, ang, n, step, curl, r0, r1=None):
    """Discs along a curling path (a tail) from (x, y) heading ang degrees (90 = up), turning by curl a step
    (a number, or one per step); -> [mask of each disc]."""
    r1 = r0 if r1 is None else r1
    curls = np.broadcast_to(np.asarray(curl, float), (n,))
    out = []
    for i in range(n):
        r = r0 + (r1 - r0) * i / max(n - 1, 1)
        out.append((c.px - x) ** 2 + (c.py - y) ** 2 <= r * r)
        x, y = x + step * np.cos(np.radians(ang)), y - step * np.sin(np.radians(ang))
        ang += curls[i]
    return out


def union(masks):
    m = masks[0].copy()
    for k in masks[1:]:
        m |= k
    return m


# ---- fox -------------------------------------------------------------------------------------------------------

FOX = {"O": "#e62", "Od": "#b41", "W": "#ffe", "D": "#322", "P": "#111"}


def fox(tail, ear, blink):
    """A red fox sitting, facing left, its white-tipped brush laid out behind it and curling up; tail lifts
    the tip (0..1), ear flicks the near ear back."""
    c = A.Canvas(32, 30)
    c.paint(c.line(9.5, 19, 9.5, 28.5, 2.2), "D")                   # far front leg
    c.paint(c.ellipse(16, 24, 6, 5), "O")                           # haunch
    c.paint(c.ellipse(15.5, 28.5, 3.5, 1.2), "D")                   # hind foot
    curl = [2] * 6 + [14 + 10 * tail] * 6
    c.paint(union(chain(c, 18, 27, -8, 12, 1.15, curl, 2.9, 2.4)), "Od")   # the brush: a dark rim where it
    discs = chain(c, 18, 27, -8, 12, 1.15, curl, 2.3, 1.8)                 # crosses the haunch
    c.paint(union(discs), "O")
    c.paint(union(discs[9:]) | union(chain(c, 18, 27, -8, 12, 1.15, curl, 2.9, 2.4)[10:]), "W")
    c.paint(rell(c, 12.5, 18, 4.6, 8, -18), "O")                    # torso, leaning back
    c.paint(rell(c, 10.5, 18, 2.4, 5.5, -18), "W")                  # white chest
    c.paint(c.line(12.5, 21, 12.5, 28.5, 2.4), "D")                 # near front leg, black stocking
    c.paint(c.rect(10, 28, 14, 29), "D")
    c.paint(c.triangle((6, 6), (6.5, 0.5), (9.5, 4.5)), "Od")       # far ear
    if ear:
        c.paint(c.triangle((9, 6), (14.5, 1.5), (13, 7)), "O")
        c.paint(c.triangle((12.5, 2.5), (14.5, 1.5), (13.6, 3.6)), "D")
    else:
        c.paint(c.triangle((9, 6), (11.5, 0), (13.5, 6.5)), "O")
        c.paint(c.triangle((10.6, 1.8), (11.5, 0), (12.3, 1.8)), "D")
    c.paint(c.ellipse(10.5, 9, 4.6, 3.8), "O")                      # head
    c.paint(c.triangle((7.5, 7.5), (7.5, 12), (1, 10.8)), "O")      # long snout
    c.paint(c.ellipse(7.5, 11.6, 3.6, 1.4) | c.triangle((6, 11), (2, 11.2), (6, 12.6)), "W")   # white cheek and jaw
    c.paint(c.rect(0, 10, 2, 11), "P")                              # nose
    if blink:
        c.paint(c.rect(7, 8, 9, 9), "D")
    else:
        c.paint(c.rect(7, 7, 9, 9), "P")
        c.set(8, 7, "W")
    return finish(c, FOX)


# ---- hare ------------------------------------------------------------------------------------------------------

HARE = {"B": "#a86", "L": "#dcb", "W": "#fff", "E": "#e99", "D": "#543", "P": "#111"}


def hare(up, s, legs):
    """A brown hare facing left, long black-tipped ears; up px off the ground, s = 0 crouched .. 1 stretched in
    the leap, legs = 'down', 'reach' (front paws forward) or 'kick' (hind feet back)."""
    c = A.Canvas(26, 24)
    gy = 23
    by = gy - 5 - up
    hx, hy = 7 - 2 * s, by - 4 + s
    if legs == "kick":
        c.paint(c.line(18, by + 1, 24, by + 4, 2), "B")
        c.paint(c.line(22, by + 4, 25.5, by + 5, 1.4), "L")
    c.paint(rell(c, 13, by, 6 + 1.5 * s, 4 - 0.6 * s, -12 * s), "B")   # body
    c.paint(c.ellipse(17, by + 0.5, 4.5, 4), "B")                  # haunch
    c.paint(c.ellipse(13, by + 2.4, 5, 1.6), "L")                   # pale belly
    c.paint(c.disc(21.5, by - 2, 1.7), "W")                         # tail
    if legs == "down":
        c.paint(c.rect(13, gy - 1, 21, gy) | c.rect(17, gy - 2, 20, gy - 1), "B")   # hind foot flat
        c.paint(c.line(9, by + 2, 8.5, by + 5, 1.4) | c.line(10.5, by + 2, 10.5, by + 5, 1.4), "L")
    elif legs == "reach":
        c.paint(c.line(8.5, by + 2, 4, by + 4, 1.4), "L")
        c.paint(c.line(18, by + 3, 21, by + 5, 1.6), "B")
    else:
        c.paint(c.line(9, by + 2, 7, by + 4, 1.4), "L")
    for x1, y1 in ((hx + 3 + 5 * s, hy - 11 + 5 * s), (hx + 6 + 5 * s, hy - 10 + 6 * s)):   # ears, laid back in the leap
        c.paint(c.line(hx + 1, hy - 1, x1, y1, 2.2), "B")
        c.paint(c.line(x1 - 0.25 * (x1 - hx), y1 + 0.25 * (hy - y1), x1, y1, 2), "D")
    c.paint(c.line(hx + 1.6, hy - 2.4, hx + 3 + 5 * s, hy - 8 + 4 * s, 0.8), "E")
    c.paint(c.ellipse(hx, hy, 3.4, 2.8) | c.ellipse(hx - 2.2, hy + 0.8, 1.8, 1.6), "B")
    c.set(int(hx - 4), int(hy), "E")                                 # nose
    c.paint(c.rect(int(hx - 1), int(hy - 1), int(hx + 1), int(hy)), "P")
    c.set(int(hx - 1), int(hy - 1), "W")
    return finish(c, HARE)


# ---- owl -------------------------------------------------------------------------------------------------------

OWL = {"B": "#975", "D": "#642", "L": "#dcb", "F": "#edc", "Y": "#fa2", "P": "#111", "Bk": "#cb8", "Br": "#753",
       "Bd": "#542", "G": "#6a3", "T": "#ec5"}


def owl(turn, blink):
    """A tawny owl on a branch, facing out; turn (-1..1) swivels its face, blink shuts its big orange eyes."""
    c = A.Canvas(24, 27)
    c.paint(c.line(0, 23.5, 24, 22.5, 2.6), "Br")                  # branch
    c.paint(c.line(0, 24.5, 24, 23.5, 1), "Bd")
    c.paint(c.line(19, 22.5, 22, 19.5, 1.2), "Br")
    c.paint(c.ellipse(22.5, 18.5, 1.8, 1.2) | c.ellipse(2, 20.5, 1.6, 1.2), "G")
    c.paint(c.ellipse(12, 16, 6.5, 7.5), "B")                       # body
    c.paint(c.ellipse(12, 17.5, 4.2, 5), "L")                       # belly
    for x, y in ((10, 15), (13, 15), (11, 17), (14, 18), (9, 18), (12, 20), (10, 21), (13, 22)):
        c.set(x, y, "D")
    c.paint(rell(c, 6.5, 16.5, 2.2, 5.5, 12) | rell(c, 17.5, 16.5, 2.2, 5.5, -12), "D")   # folded wings
    for x in (9, 10, 13, 14):                                        # talons
        c.set(x, 22, "T")
    f = 1.6 * turn
    c.paint(c.ellipse(12, 8, 6.8, 5.6), "B")                        # head
    c.paint(c.triangle((5.5 + f * 0.5, 5), (6 + f * 0.5, 0.5), (9 + f * 0.5, 4)) |
            c.triangle((18.5 + f * 0.5, 5), (18 + f * 0.5, 0.5), (15 + f * 0.5, 4)), "B")   # ear tufts
    c.paint(c.disc(9.3 + f, 8, 2.8) | c.disc(14.7 + f, 8, 2.8), "F")   # facial disc
    if blink:
        c.paint(c.rect(int(8 + f), 8, int(11 + f), 9) | c.rect(int(13 + f), 8, int(16 + f), 9), "D")
    else:
        for ex in (9.3, 14.7):
            x = int(np.floor(ex + f - 1))
            c.paint(c.rect(x, 7, x + 2, 9), "Y")
            c.paint(c.rect(x + (1 if turn >= 0 else 0), 7, x + (2 if turn >= 0 else 1), 9), "P")
            c.set(x, 7, "F")
    bx = int(np.floor(12 + f))
    c.paint(c.rect(bx - 1, 10, bx + 1, 11) | c.rect(bx - 1, 11, bx, 12), "Bk")   # beak
    return finish(c, OWL)


# ---- squirrel --------------------------------------------------------------------------------------------------

SQUIRREL = {"R": "#c53", "D": "#932", "W": "#fed", "N": "#a72", "Nd": "#642", "P": "#111", "C": "#dc9"}


def squirrel(flick, bite, crumbs):
    """A red squirrel sitting up, facing left, a nut in its paws and its bushy tail up behind; flick whips the
    tail tip over, bite raises the nut to its teeth, crumbs fall."""
    c = A.Canvas(24, 22)
    curl = [-3] * 7 + [-24 - 10 * flick] * 5                         # straight up the back, then over at the top
    c.paint(union(chain(c, 13.5, 19, 80, 12, 1.35, curl, 2.2, 3.0)), "R")
    c.paint(union(chain(c, 14.3, 19, 80, 12, 1.35, curl, 0.7, 1.4)), "D")   # tail shade
    c.paint(c.ellipse(11, 18, 4, 3), "R")                           # haunch
    c.paint(c.rect(6, 20, 12, 21), "D")                              # foot
    c.paint(rell(c, 8.5, 14, 3.6, 5.5, -12), "R")                    # body, upright
    c.paint(rell(c, 7.2, 15, 2, 4, -12), "W")                       # cream belly
    by = -1 if bite else 0
    c.paint(c.disc(6, 7 + 0.5 * bite, 3.2), "R")                    # head, dipping to bite
    c.paint(c.ellipse(3.4, 8.4 + 0.5 * bite, 2, 1.6), "R")
    c.paint(c.ellipse(4, 9.6 + 0.5 * bite, 1.8, 0.8), "W")
    c.paint(c.triangle((6.5, 4.5), (8.5, 0), (9.5, 5)), "R")        # ear and tuft
    c.paint(c.rect(8, 0, 9, 2), "D")
    c.set(1, int(8 + 0.5 * bite), "P")
    c.paint(c.rect(5, int(6 + 0.5 * bite), 6, int(8 + 0.5 * bite)), "P")
    ny = 11 + by
    c.paint(c.rect(3, ny, 5, ny + 2), "N")                          # the nut in its paws
    c.paint(c.rect(3, ny - 1, 5, ny), "Nd")
    c.paint(c.rect(5, ny + 1, 7, ny + 2), "R")
    if crumbs:
        for x, y in ((3, 15 + crumbs), (5, 13 + 2 * crumbs), (2, 17 + crumbs)):
            c.set(x, min(y, 21), "C")
    return finish(c, SQUIRREL)


# ---- hedgehog --------------------------------------------------------------------------------------------------

HEDGEHOG = {"S": "#654", "T": "#cb9", "F": "#db9", "D": "#432", "P": "#111"}


def hedgehog(step, sniff):
    """A hedgehog waddling, facing left: a dome of light-tipped spines on little feet; step (0..3) swaps the feet
    and bobs it, sniff lifts the nose."""
    c = A.Canvas(20, 14)
    bob = 1 if step % 2 else 0
    gy = 13
    for i, x in enumerate((6, 9, 12, 15)):                          # feet, the pairs alternate
        lift = 1 if (i + step) % 2 == 0 and step % 2 else 0
        c.paint(c.rect(x, gy - 2 - lift, x + 2, gy - lift), "D")
    cy = 8 - bob
    dome = c.ellipse(11.5, cy, 7.5, 5.5) & (c.py < cy + 3)
    c.paint(dome, "S")
    for y in range(int(cy - 6), int(cy + 3)):                       # spine tips, in a staggered pattern
        for x in range(3, 20):
            if dome[y, x] and (x + 2 * y) % 4 == 0:
                c.set(x, y, "T")
    for a in range(200, 361, 16):                                    # spines poking out of the edge
        x = 11.5 + 8.3 * np.cos(np.radians(a))
        y = cy + 6.2 * np.sin(np.radians(a))
        c.set(int(x), int(y), "S")
    c.paint(c.ellipse(11, cy + 3, 6, 1.6), "F")                     # belly
    nose = sniff
    c.paint(c.ellipse(5, cy + 1.5, 3.2, 2.4), "F")                  # face
    c.paint(c.triangle((4, cy), (0.5, cy + 2 - nose), (4, cy + 3.6)), "F")   # pointed snout
    c.set(0, int(cy + 1.5 - nose), "P")
    c.set(5, int(cy), "P")
    return finish(c, HEDGEHOG)


# ---- frog ------------------------------------------------------------------------------------------------------

FROG = {"G": "#5b3", "D": "#372", "L": "#ce7", "Th": "#ee9", "Y": "#fd4", "P": "#111", "Lp": "#4a4", "Ld": "#283",
        "Fl": "#f9c", "Wa": "#6be"}


def frog(puff, up):
    """A frog on a lily pad facing left; puff swells its throat (0..2), up lifts it into a leap (px)."""
    c = A.Canvas(24, 22)
    c.paint(c.ellipse(12, 19.5, 11, 2.4) & ~c.triangle((12, 19.5), (19, 22), (22, 22)), "Lp")   # pad, notched
    c.paint(c.ellipse(12, 20.5, 9, 1), "Ld")
    c.paint(c.ellipse(21, 18, 1.6, 1.4), "Fl")                     # a flower bud on the pad
    c.set(0, 21, "Wa")
    c.set(23, 21, "Wa")
    by = 16 - up
    if up >= 4:                                                      # leaping: legs trailing out behind
        c.paint(c.line(14, by, 21, by + 3, 2) | c.line(20, by + 3, 23, by + 2, 1.2), "D")
        c.paint(rell(c, 10, by - 1, 5.5, 3, -18), "G")
        c.paint(c.line(6, by + 1, 3, by + 3, 1.3), "G")
        hx, hy = 5.5, by - 3.5
    else:
        c.paint(c.ellipse(15.5, by + 0.5, 3.5, 2.6), "D")           # folded hind leg
        c.paint(c.rect(13, by + 2, 19, by + 3), "D")
        c.paint(rell(c, 11, by - 1, 5.5, 3.4, -15), "G")            # body
        c.paint(c.line(7.5, by, 7, by + 2.5, 1.4), "G")             # front leg
        c.paint(c.rect(6, int(by + 2), 9, int(by + 3)), "G")
        hx, hy = 7, by - 3
    for x, y in ((11, by - 3), (13, by - 2), (10, by)):              # spots
        c.set(x, int(y), "D")
    c.paint(c.ellipse(hx, hy + 0.5, 3.6, 2.4), "G")                 # head
    if puff and up < 4:
        c.paint(c.disc(hx - 0.5, hy + 2.6, 1.2 + 0.8 * puff), "Th")  # throat sac
    c.paint(c.ellipse(hx, hy + 1.6, 3, 0.9) & ~c.disc(hx - 0.5, hy + 2.6, 1.2 + 0.8 * puff), "L")
    c.paint(c.disc(hx + 0.5, hy - 1.6, 1.6), "G")                   # eye bump
    c.paint(c.rect(int(hx), int(hy - 2), int(hx) + 1, int(hy)), "P")
    c.set(int(hx) - 1, int(hy - 2), "Y")
    c.set(int(hx - 3), int(hy + 1), "D")                            # mouth line
    return finish(c, FROG)


# ---- goat ------------------------------------------------------------------------------------------------------

GOAT = {"W": "#eed", "S": "#bba", "D": "#776", "H": "#987", "Hd": "#654", "Y": "#ec4", "P": "#111", "G": "#6b3",
        "N": "#e9a"}


def goat(jaw, blink):
    """A white farm goat facing left, swept-back horns and a little beard, chewing a grass stalk (jaw 0..2)."""
    c = A.Canvas(30, 30)
    for x0, x1, sh in ((11, 10.5, "S"), (13, 13, "W"), (22, 22.5, "S"), (24, 24.5, "W")):
        c.paint(c.line(x0, 18, x1, 28, 1.8), sh)
        c.paint(c.rect(int(x1) - 1, 28, int(x1) + 1, 30), "Hd")
    c.paint(c.ellipse(18, 16, 9, 5), "W")                           # body
    c.paint(c.ellipse(18, 19, 7, 2), "S")
    c.paint(c.line(26, 12, 28, 9.5, 1.6), "W")                      # short upright tail
    c.paint(c.line(12, 15, 9, 8, 4.4), "W")                         # neck
    h = Pose(c, 7.5, 7.5, 20)
    for k, d in enumerate(np.linspace(0, 1, 7)):                    # horns: curving back
        c.paint(h.ell(1.5 + 4.5 * d, -3 - 2 * d + 2.5 * d * d, 1.1, 1.1), "H")
    c.paint(h.ell(0, 0, 3.4, 2.6) | h.ell(-3.6, 1.2, 2.3, 1.8), "W")   # head and muzzle
    c.paint(h.line(1.5, -0.8, 5.5, 0.6, 1.6), "S")                  # ear, sideways
    c.paint(h.ell(-5.4, 0.8, 0.8, 0.8), "N")                        # nose
    jx, jy = h.p(-3.2, 2.8 + 0.5 * jaw)
    c.paint(c.ellipse(jx, jy, 2, 0.9), "W")                         # lower jaw, chewing
    bx, by = h.p(-1.5, 3)
    c.paint(c.triangle((bx - 1.5, by), (bx + 1.5, by), (bx - 0.2 + 0.3 * jaw, by + 4.5)), "S")   # beard
    gx, gy = h.p(-5.5, 2.4 + 0.3 * jaw)
    c.paint(c.line(gx, gy, gx - 3, gy + 1.5 - jaw, 1), "G")          # grass stalk
    c.set(int(gx - 3), int(gy - jaw), "G")
    ex, ey = h.p(0, -0.6)
    if blink:
        c.paint(c.rect(int(ex) - 1, int(ey), int(ex) + 1, int(ey) + 1), "D")
    else:
        c.paint(c.rect(int(ex) - 1, int(ey), int(ex) + 1, int(ey) + 1), "Y")
        c.set(int(ex) - 1, int(ey), "P")
    return finish(c, GOAT)


# ---- pony ------------------------------------------------------------------------------------------------------

PONY = {"B": "#db8", "S": "#b96", "D": "#432", "M": "#fec", "Mk": "#322", "T": "#a87", "Mz": "#876", "P": "#111",
        "Wt": "#fff"}


def pony(head, tail, mane):
    """A sturdy Nordic dun pony facing left, its upright two-tone mane and dark dorsal stripe; head lifts it up
    and down (degrees), tail swishes it (-1..1), mane flips the mane over."""
    c = A.Canvas(46, 42)
    sw = 3 * tail
    for k, d in enumerate(np.linspace(0, 1, 10)):                    # tail
        x = 40 + 2 * d + sw * d * d
        y = 15 + 18 * d
        c.paint(c.disc(x, y, 1.8 + 0.6 * d), "T" if k < 7 else "Mk")
    for x0, x1, sh in ((14, 13, "S"), (17, 17, "B"), (31, 32, "S"), (34, 35, "B")):
        c.paint(c.line(x0, 24, x1, 38, 3), sh)
        c.paint(c.rect(int(x1) - 1, 36, int(x1) + 2, 39), "D")      # dark socks
        c.paint(c.rect(int(x1) - 2, 39, int(x1) + 2, 41), "Mk")     # hooves
    c.paint(c.ellipse(25, 20, 14, 8), "B")                          # body, barrel-round
    c.paint(c.ellipse(25, 25, 10, 2.8), "S")
    c.paint(c.line(19, 12.4, 38, 13.2, 1), "Mk")                    # dorsal stripe
    nx, ny = 11, 7 + head * 0.12                                     # the poll; the neck rises to it
    c.paint(c.triangle((nx + 1, ny - 1), (21, 13), (13, 25)) | c.line(15, 19, nx + 1, ny + 1, 6.5), "B")
    h = Pose(c, nx, ny, 118 - head)                                  # u runs down the face to the muzzle
    c.paint(h.ell(1.5, -1.2, 3.4, 3.6) | h.line(0, 0, 9, 0.6, 4.6), "B")   # cheek and the long face
    c.paint(h.ell(9.2, 0.4, 2.6, 2.6), "Mz")                        # dark muzzle
    c.paint(h.dot(9.6, 1.4), "P")                                    # nostril
    c.paint(h.dot(2.2, 1.0), "P")                                    # eye
    ex, ey = h.p(-0.8, -0.6)
    c.paint(c.triangle((ex - 1.5, ey + 0.5), (ex + 1.3, ey + 0.8), (ex + 0.5, ey - 4)), "B")   # ear
    lean = -1.6 if mane else 1.0
    for k in range(9):                                               # the stiff fjord mane, cream with a black core
        t = k / 8
        x0, y0 = nx + 1.5 + 8 * t, ny - 0.5 + (13 - ny) * t
        c.paint(c.line(x0, y0, x0 + lean, y0 - 2.8, 1.8), "M")
        c.paint(c.line(x0 + 0.2, y0, x0 + 0.2 + lean * 0.7, y0 - 2, 0.8), "Mk")
    fx, fy = h.p(0.5, 1.5)
    c.paint(c.line(fx, fy, fx - 1.5, fy + 1.5, 1.4), "M")           # forelock
    return finish(c, PONY)


# ---- sheep -----------------------------------------------------------------------------------------------------

SHEEP = {"W": "#eee", "Ws": "#ccb", "D": "#433", "E": "#544", "G": "#6b3", "P": "#111", "Pk": "#d99"}


def sheep(s, ear):
    """A woolly sheep facing left; s = 0 head up, 1 grazing; ear flicks an ear up."""
    c = A.Canvas(32, 24)
    for x in (10, 12, 22, 24):                                       # thin dark legs
        c.paint(c.rect(x, 17, x + 2, 23), "D")
    for a in range(0, 360, 30):                                      # the fleece: a cloud of curls
        c.paint(c.disc(18 + 9.5 * np.cos(np.radians(a)), 11.5 + 5 * np.sin(np.radians(a)), 2.6), "W")
    c.paint(c.ellipse(18, 11.5, 10, 5.5), "W")
    for x, y in ((12, 9), (16, 7), (21, 8), (25, 11), (14, 13), (19, 12), (23, 15), (10, 14)):
        c.paint(c.rect(x, y, x + 2, y + 1), "Ws")
    for x, y in ((3, 23), (5, 22), (6, 23), (1, 22)):
        c.paint(c.rect(x, y, x + 1, 24), "G")
    hx, hy = 7 - 1.5 * s, 8 + 10 * s
    c.paint(c.line(10, 10, hx + 1, hy, 4), "W")                     # neck
    h = Pose(c, hx, hy, -60 * s)
    c.paint(h.ell(-1, 0.5, 3, 2.4) | h.ell(-3.2, 1.4, 1.8, 1.6), "D")   # black face
    c.paint(h.ell(1, -1.5, 2.4, 1.6), "W")                          # topknot
    if ear:
        c.paint(h.line(1, -0.5, 3, -3, 1.4), "E")
    else:
        c.paint(h.line(1, 0, 4, 1, 1.4), "E")
    c.paint(h.dot(-1.6, -0.2), "W")
    c.paint(h.dot(-4.6, 1.8), "Pk")
    return finish(c, SHEEP)


# ---- duck and ducklings ----------------------------------------------------------------------------------------

DUCK = {"B": "#a75", "D": "#753", "L": "#dc9", "Bl": "#36c", "O": "#f93", "P": "#111", "Y": "#fe5", "Yd": "#a83",
        "Wa": "#6be", "Wl": "#bef"}


def duck(c, x, wy, bob):
    """A mallard hen swimming left, her body cut by the water at wy."""
    y = wy - 2 + bob
    c.paint(c.ellipse(x + 8, y, 6.5, 3.4) | c.triangle((x + 12, y - 1), (x + 16, y - 3.5), (x + 14, y + 1)), "B")
    c.paint(c.ellipse(x + 9.5, y - 0.8, 3.6, 1.6), "D")             # wing
    c.paint(c.rect(int(x + 10), int(y), int(x + 13), int(y + 1)), "Bl")   # blue speculum
    c.paint(c.line(x + 4, y - 1, x + 3.5, y - 5, 2.4), "B")         # neck
    c.paint(c.disc(x + 3.5, y - 6.5, 2.4), "B")
    c.paint(c.rect(int(x - 1), int(y - 6), int(x + 2), int(y - 5)), "O")   # bill
    c.set(int(x + 3), int(y - 7), "P")
    c.paint(c.rect(int(x + 2), int(y - 7), int(x + 6), int(y - 6)) & ~c.rect(int(x + 3), int(y - 7), int(x + 4),
                                                                                int(y - 6)), "D")   # eye stripe


def duckling(c, x, wy, bob):
    y = wy - 1 + bob
    c.paint(c.ellipse(x + 4, y, 3.2, 2), "Y")
    c.paint(c.ellipse(x + 4.6, y - 0.8, 2, 1), "Yd")
    c.paint(c.disc(x + 1.8, y - 3, 1.6), "Y")
    c.set(int(x), int(y - 3), "O")
    c.set(int(x + 1), int(y - 4), "P")


def ducks(k):
    """A mallard hen leads three ducklings left across the water, each bobbing in turn, ripples trailing."""
    c = A.Canvas(60, 16)
    wy = 13
    birds = [(1, duck), (21, duckling), (32, duckling), (43, duckling)]
    for i, (x, fn) in enumerate(birds):
        bob = 1 if (k + i) % 4 in (1, 2) else 0
        fn(c, x, wy, bob)
        tail = x + (17 if fn is duck else 7)
        d = (k + i) % 2                                              # the wake: two ripples drifting back
        c.paint(c.rect(tail + 1 + d, wy, tail + 3 + d, wy + 1) & (c.g == ""), "Wl")
        c.paint(c.rect(tail + 3 - d, wy + 1, tail + 5 - d, wy + 2) & (c.g == ""), "Wa")
    c.paint(c.rect(0, wy, 60, wy + 1) & (c.g != "") & (c.g != "Wl"), "Wa")   # the waterline across each bird
    c.g[(c.py > wy + 1) & (c.g != "Wa")] = ""
    return finish(c, DUCK)


# ---- bat -------------------------------------------------------------------------------------------------------

BAT = {"B": "#654", "Wg": "#432", "Wm": "#865", "E": "#e99", "Wt": "#fff", "P": "#111", "St": "#778", "Sd": "#556"}


def bat(open_, blink):
    """A cave bat hanging upside down from a rock, facing out; open_ spreads its wings (0 folded .. 2 wide),
    blink shuts its eyes."""
    c = A.Canvas(30, 22)
    c.paint(c.rect(4, 0, 26, 2) | c.ellipse(15, 1, 8, 2.4) | c.ellipse(9, 1.6, 3, 2), "St")   # the cave roof
    c.paint(c.rect(8, 0, 23, 1), "Sd")
    c.paint(c.line(13.5, 2, 14, 5, 1) | c.line(16.5, 2, 16, 5, 1), "P")   # feet
    if open_:
        span = 6 + 5 * open_
        for sx in (-1, 1):
            tip = (15 + sx * span, 6 + (3 if open_ == 1 else 1))
            elbow = (15 + sx * span * 0.55, 4.5)
            c.paint(c.triangle((15, 6), elbow, (15, 15)) | c.triangle(elbow, tip, (15, 14)), "Wm")
            for f in (0.35, 0.7):                                    # wing fingers and scallops
                fx = 15 + sx * span * f
                c.paint(c.line(elbow[0], elbow[1], fx, 14 - 6 * f, 1), "Wg")
            c.paint(c.line(15, 6, elbow[0], elbow[1], 1.2) | c.line(elbow[0], elbow[1], *tip, 1.2), "Wg")
    c.paint(c.ellipse(15, 9.5, 3.6, 5), "B")                        # body
    if not open_:
        c.paint(c.ellipse(15, 10, 4.6, 5.4) & ~c.rect(13, 0, 17, 22), "Wg")   # wings folded round it
    c.paint(c.disc(15, 15.5, 2.8), "B")                              # head, at the bottom
    c.paint(c.triangle((12.5, 16.5), (12, 21), (14.4, 17.5)) | c.triangle((17.5, 16.5), (18, 21), (15.6, 17.5)), "B")
    c.set(12, 19, "E")
    c.set(17, 19, "E")
    if blink:
        c.paint(c.rect(13, 15, 15, 16) | c.rect(16, 15, 18, 16), "Wg")
    else:
        c.set(13, 15, "P")
        c.set(16, 15, "P")
        c.set(13, 16, "Wt")
        c.set(16, 16, "Wt")
    c.set(14, 14, "E")                                               # little nose
    c.set(15, 14, "E")
    return finish(c, BAT)


# ---- mouse -----------------------------------------------------------------------------------------------------

MOUSE = {"Wd": "#a74", "Wk": "#753", "H": "#211", "M": "#998", "Ml": "#ccb", "E": "#f9a", "P": "#111", "Wh": "#eee",
         "C": "#fd4", "Cd": "#ca2"}


def mouse(out, whisk, nib):
    """A mouse at its hole in a log wall, a cheese wedge on the floor; out (0 peeking .. 2 out at the cheese),
    whisk twitches the whiskers, nib takes a nibble."""
    c = A.Canvas(26, 16)
    c.paint(c.rect(0, 0, 11, 16), "Wd")                              # the log wall
    for y in (3, 7, 11, 15):
        c.paint(c.rect(0, y, 11, y + 1), "Wk")
    c.paint(c.ellipse(6, 15, 3.6, 4.6) & (c.py < 16), "H")          # the hole
    c.paint(c.triangle((17, 15.9), (25.9, 15.9), (25.9, 10)) | c.rect(17, 14, 26, 16) & (c.px > 17), "C")   # cheese
    c.paint(c.rect(17, 15, 26, 16), "Cd")
    c.set(22, 13, "Cd")
    c.set(24, 12, "Cd")
    c.set(20, 14, "Cd")
    if nib or out == 2:                                              # the bite already taken
        c.paint(c.disc(17.8, 13.2, 1.4), "")
    hx = 7.5 + 3.5 * out
    hy = 13
    if out:
        c.paint(c.ellipse(hx - 3.5, 13.6, 3.6, 2.4) & ~c.rect(0, 0, 3, 16), "M")   # body leaving the hole
        c.paint(c.rect(int(hx - 4), 15, int(hx - 2), 16) | c.rect(int(hx), 15, int(hx + 1), 16), "Ml")
    c.paint(c.ellipse(hx, hy - 0.5 * nib, 2.2, 1.8) | c.triangle((hx, hy - 1.5), (hx + 3.5, hy + 0.4 - nib),
                                                                  (hx, hy + 1.5)), "M")   # head, pointed snout
    c.paint(c.disc(hx - 1, hy - 2.4, 1.3), "E")                      # big round ear
    c.set(int(hx + 3), int(hy - nib), "E")                           # nose
    c.set(int(hx + 1), int(hy - 1), "P")
    for dy in (-1, 1):
        y0 = hy - nib * 0.5
        c.set(int(hx + 4), int(y0 + dy * (1 + whisk)), "Wh")
    if out == 0:
        c.g[:, :3] = np.where(c.g[:, :3] == "M", "Wd", c.g[:, :3])
    return finish(c, MOUSE)


# ---- snail -----------------------------------------------------------------------------------------------------

SNAIL = {"Sh": "#d94", "Sd": "#852", "Sl": "#fc7", "Bd": "#bb9", "Bk": "#887", "P": "#111", "Tr": "#9bc"}


def snail(s, eyes):
    """A garden snail creeping left: a coiled orange shell on a soft body; s stretches the foot forward (0..1),
    eyes sways the eye stalks (-1..1)."""
    c = A.Canvas(20, 13)
    gy = 12
    front = 2.5 - 2 * s
    c.paint(c.ellipse((front + 17) / 2, gy - 1.3, (17 - front) / 2, 1.5), "Bd")   # the foot
    c.paint(c.rect(int(front) + 1, gy - 1, 17, gy), "Bk")
    c.paint(c.disc(front + 1.8, gy - 3, 2.2) | c.rect(int(front) + 1, gy - 4, int(front) + 5, gy - 1), "Bd")
    for dx, lean in ((1.0, -1.5), (2.8, -0.5)):                     # eye stalks with dark tips
        x0, y0 = front + dx, gy - 4.5
        x1, y1 = x0 + lean + eyes, y0 - 4
        c.paint(c.line(x0, y0, x1, y1, 1.1), "Bd")
        c.set(int(x1), int(y1), "P")
    sx = 11.5 - 0.6 * s
    c.paint(c.disc(sx, gy - 6, 5), "Sh")                            # shell
    for th in np.linspace(0, 3.2 * np.pi, 40):                      # its spiral
        r = 0.6 + 0.42 * th
        c.set(int(sx + r * np.cos(th)), int(gy - 6 - r * np.sin(th)), "Sd")
    c.paint(c.ellipse(sx - 2, gy - 9, 1.6, 0.8), "Sl")             # shine
    if s < 0.5:
        c.set(19, gy - 1, "Tr")                                      # a glint of slime behind
    return finish(c, SNAIL)


# ---- butterfly -------------------------------------------------------------------------------------------------

BUTTERFLY = {"O": "#f82", "Ob": "#c51", "Wt": "#fff", "Bd": "#322", "G": "#5a3", "Gd": "#382", "Pt": "#fff",
             "Y": "#fd3"}


def butterfly(w, bob):
    """A butterfly fluttering over a meadow daisy, seen from above; w is how wide its wings are open (0..1),
    bob lifts it."""
    c = A.Canvas(20, 26)
    c.paint(c.line(10, 25.5, 10, 18, 1.2), "G")                      # the daisy
    c.paint(rell(c, 7.5, 22.5, 2.2, 0.9, -30) | rell(c, 12.5, 23.5, 2.2, 0.9, 30), "Gd")
    for a in range(0, 360, 45):
        c.paint(c.disc(10 + 2.8 * np.cos(np.radians(a)), 17 + 2.2 * np.sin(np.radians(a)), 1.3), "Pt")
    c.paint(c.ellipse(10, 17, 1.4, 1), "Y")
    y = 6 - bob
    for sx in (-1, 1):
        fw, hw = 4.6 * w + 0.6, 3.2 * w + 0.5
        c.paint(rell(c, 10 + sx * fw * 0.85, y - 1.6, fw, 3, -24 * sx), "O")      # forewing, swept up
        c.paint(rell(c, 10 + sx * hw * 0.75, y + 2.6, hw, 2.2, 28 * sx), "O")     # hindwing, rounded
        if w > 0.3:
            c.paint(c.ellipse(10 + sx * (fw * 1.25), y - 2.6, fw * 0.45, 1.2), "Ob")   # dark wing tips
            c.set(int(10 + sx * fw * 1.2), int(y - 1), "Wt")
            c.set(int(10 + sx * hw * 0.9), int(y + 3), "Ob")
    c.paint(c.rect(9, int(y) - 3, 11, int(y) + 4), "Bd")            # body
    c.paint(c.line(9.5, y - 3, 7.5, y - 6, 0.9) | c.line(10.5, y - 3, 12.5, y - 6, 0.9), "Bd")   # antennae
    return finish(c, BUTTERFLY)


# ---- bear cub --------------------------------------------------------------------------------------------------

BEAR = {"B": "#864", "D": "#643", "L": "#ca8", "P": "#111", "Wt": "#fff", "N": "#322"}


def bear_cub(arm, blink):
    """A brown bear cub sitting, facing out, waving one paw; arm is the paw's angle (degrees above level)."""
    c = A.Canvas(30, 27)
    c.paint(c.ellipse(14, 19, 8, 7), "B")                           # body
    c.paint(c.ellipse(14, 20, 4.5, 4.6), "L")                       # belly
    c.paint(c.ellipse(8.5, 24.5, 3, 2.2) | c.ellipse(19.5, 24.5, 3, 2.2), "D")   # feet, soles out
    c.paint(c.ellipse(8.5, 24.8, 1.8, 1.3) | c.ellipse(19.5, 24.8, 1.8, 1.3), "L")
    c.paint(c.line(8, 15, 9, 21, 3), "D")                            # resting arm
    a = np.radians(arm)
    hx, hy = 20.5 + 7 * np.cos(a), 16 - 7 * np.sin(a)
    c.paint(c.line(19.5, 16, hx, hy, 3.2), "B")                     # waving arm
    c.paint(c.disc(hx, hy, 1.9), "D")
    c.paint(c.disc(8, 3.6, 2.4) | c.disc(20, 3.6, 2.4), "B")        # round ears
    c.paint(c.disc(8, 3.8, 1.1) | c.disc(20, 3.8, 1.1), "L")
    c.paint(c.ellipse(14, 9, 7, 6), "B")                            # head
    c.paint(c.ellipse(14, 11.5, 3.2, 2.4), "L")                     # muzzle
    c.paint(c.rect(13, 10, 15, 11), "N")                             # nose
    c.set(14, 12, "N")
    if blink:
        c.paint(c.rect(10, 8, 12, 9) | c.rect(16, 8, 18, 9), "N")
    else:
        c.paint(c.rect(10, 7, 11, 9) | c.rect(17, 7, 18, 9), "P")
        c.set(10, 7, "Wt")
        c.set(17, 7, "Wt")
    return finish(c, BEAR)


# ---- beaver ----------------------------------------------------------------------------------------------------

BEAVER = {"B": "#864", "D": "#532", "L": "#a86", "T": "#f93", "P": "#111", "Tl": "#543", "Tx": "#432",
          "W": "#a75", "Wd": "#753", "Wc": "#edb", "N": "#211"}


def beaver(lean, chips):
    """A beaver gnawing a tree trunk, facing left, its flat tail on the ground; lean pushes the head in (0..1),
    chips flies wood chips off (0..2)."""
    c = A.Canvas(32, 22)
    gy = 21
    c.paint(c.rect(0, 0, 8, gy), "W")                                # the trunk
    c.paint(c.rect(0, 0, 1, gy) | c.rect(5, 2, 6, 7) | c.rect(3, 17, 4, 21) | c.rect(6, 18, 7, 21), "Wd")   # bark
    c.paint(c.triangle((8, 7.5), (8, 17.5), (2.5, 12.5)), "Wc")      # the gnawed notch, pale wood
    c.g[c.triangle((8, 10.5), (8, 14.5), (5, 12.5))] = ""
    c.paint(c.rect(1, gy - 1, 3, gy), "Wc")                          # chips at its foot
    c.paint(c.ellipse(26, gy - 1.2, 5, 1.6), "Tl")                  # flat tail
    for x in (23, 25, 27, 29):
        c.set(x, gy - 2, "Tx")
    c.paint(c.ellipse(18, gy - 6, 7, 5.5), "B")                     # body
    c.paint(c.ellipse(16, gy - 3, 4, 2), "L")
    c.paint(c.rect(18, gy - 1, 22, gy), "D")                         # hind foot
    hx = 11 - lean
    c.paint(c.line(15, gy - 6, hx + 0.5, gy - 7, 2), "D")            # forepaw on the trunk
    c.paint(c.disc(hx + 1.5, gy - 10, 3.6) | c.ellipse(hx - 1, gy - 9, 2.4, 2), "B")   # head
    c.paint(c.ellipse(hx - 1.2, gy - 8.4, 1.8, 1.2), "L")           # muzzle
    c.set(int(hx - 3), int(gy - 10), "N")                            # nose
    c.paint(c.rect(int(hx - 2), int(gy - 7), int(hx), int(gy - 5)), "T")   # big orange teeth
    c.paint(c.disc(hx + 3.5, gy - 13, 1.2), "D")                     # ear
    c.set(int(hx), int(gy - 12), "P")
    if chips:
        for dx, dy in ((-4, -5), (-2, -8), (-5, -2)) if chips == 1 else ((-6, -3), (-3, -10), (-7, 0), (-1, -11)):
            c.set(int(hx + dx + 2), int(gy - 9 + dy * 0.6), "Wc")
    return finish(c, BEAVER)


# ---- moose -----------------------------------------------------------------------------------------------------

MOOSE = {"B": "#654", "D": "#322", "S": "#543", "L": "#a98", "H": "#765", "A": "#edc", "Ad": "#cb9", "P": "#111",
         "Mz": "#543", "Mt": "#987", "Wt": "#fff", "G": "#6a3"}


def moose(jaw, ear, blink):
    """A bull moose in a mountain forest, facing left: a high hump, broad pale palm antlers, a long heavy head with
    a bulbous overhanging nose, and the bell hanging under its throat; chewing (jaw 0..1), ear flicks an ear,
    blink."""
    c = A.Canvas(58, 52)
    for x0, sh in ((20, "S"), (24, "B"), (42, "S"), (46, "B")):      # long legs, pale below
        c.paint(c.rect(x0, 28, x0 + 3, 44), sh)
        c.paint(c.rect(x0, 42, x0 + 3, 50), "L")
        c.paint(c.rect(x0 - 1, 50, x0 + 3, 52), "D")
    c.paint(c.ellipse(36, 23, 15, 7.5), "B")                        # body
    c.paint(c.ellipse(36, 28, 10, 2.2), "S")                         # belly shade
    c.paint(c.ellipse(27, 18, 8, 6.5), "B")                         # shoulder hump
    c.paint(c.line(50.5, 19, 52.5, 24, 2), "D")                     # stub tail
    c.paint(c.triangle((16, 14), (24, 13), (22, 30)) | c.line(21, 22, 17, 17, 7), "B")   # neck
    c.paint(c.line(14, 21, 13.5, 28, 2.2) | c.ellipse(13.4, 29, 1.6, 2), "D")   # the bell under the throat
    for k, (cx, ang) in enumerate(((8, 16), (24.5, -16))):           # broad palm antlers, the far one darker
        key = "Ad" if k == 0 else "A"
        c.paint(rell(c, cx, 8, 7.5, 3.2, ang), key)
        for t in np.linspace(-1, 1, 5):                              # tines along the top edge
            x, y = cx + 6 * t, 8 - 3 - ang / 16 * 2 * t
            c.paint(c.line(x, y + 1, x + 0.8 * t, y - 2.4, 1.2), key)
        c.paint(c.line(cx + (4 if k == 0 else -4), 9.5, 16, 13, 2.2), key)   # beam down to the skull
    c.paint(c.line(16, 15.5, 7, 21.5, 6.4) | c.ellipse(15.5, 16.5, 3.4, 3.2), "H")   # long heavy head
    c.paint(c.ellipse(4.6, 23, 4, 3.4), "H")                        # bulbous nose, overhanging the lip
    c.paint(c.ellipse(3.6, 23.4, 2.8, 2.4), "Mt")                   # its lighter tip
    c.paint(c.rect(1, 23, 3, 25), "P")                               # nostril
    c.paint(c.ellipse(7, 26.8 + jaw, 2.6, 1.2), "Mz")               # lower lip, chewing
    if ear:                                                          # the ear, behind the antler
        c.paint(c.line(18.5, 14, 24, 11.5, 2.2), "H")
    else:
        c.paint(c.line(18.5, 14, 21.5, 9.5, 2.2), "H")
        c.paint(c.line(19.5, 13, 21, 10.5, 0.9), "D")
    if blink:
        c.paint(c.rect(12, 16, 14, 17), "D")
    else:
        c.paint(c.rect(12, 15, 14, 17), "P")
        c.set(12, 15, "Wt")                                          # catchlight
    c.paint(c.rect(3, 50, 5, 52) | c.rect(5, 49, 6, 52) | c.rect(52, 50, 53, 52) | c.rect(54, 49, 55, 52), "G")
    return finish(c, MOOSE)


# ---- pieces ----------------------------------------------------------------------------------------------------

def pieces():
    return [
        A.Piece("Fox (moving, tail and ears)",
                frames=[fox(0, False, False), fox(0.5, False, False), fox(1, False, False), fox(1, True, False),
                        fox(0.5, False, False), fox(0, False, True)], kind="pixel", fps=4),
        A.Piece("Hare (moving, hopping)",
                frames=[hare(0, 0, "down"), hare(2, 1, "kick"), hare(5, 0.7, "reach"), hare(3, 0.3, "reach"),
                        hare(0, 0, "down")], kind="pixel", fps=6),
        A.Piece("Owl (moving, blink and head turn)",
                frames=[owl(0, False), owl(0, False), owl(0, True), owl(-1, False), owl(-1, False), owl(0, False),
                        owl(1, False), owl(1, False)], kind="pixel", fps=4),
        A.Piece("Squirrel (moving, nibbling)",
                frames=[squirrel(0, 0, 0), squirrel(0.5, 1, 0), squirrel(1, 1, 1), squirrel(0.5, 0, 2),
                        squirrel(0, 1, 0), squirrel(0, 0, 1)], kind="pixel", fps=5),
        A.Piece("Hedgehog (moving, waddling)",
                frames=[hedgehog(0, 0), hedgehog(1, 0), hedgehog(2, 0), hedgehog(3, 0), hedgehog(0, 1),
                        hedgehog(0, 2), hedgehog(0, 1)], kind="pixel", fps=5),
        A.Piece("Frog (moving, croak and jump)",
                frames=[frog(0, 0), frog(1, 0), frog(2, 0), frog(1, 0), frog(0, 0), frog(0, 5), frog(0, 8),
                        frog(0, 4), frog(0, 0)], kind="pixel", fps=5),
        A.Piece("Goat (moving, chewing)",
                frames=[goat(0, False), goat(1, False), goat(2, False), goat(1, False), goat(0, True)],
                kind="pixel", fps=4),
        A.Piece("Pony (moving, mane and tail)",
                frames=[pony(0, 0, False), pony(8, 0.5, False), pony(14, 1, True), pony(6, 0.5, True),
                        pony(-4, -0.5, False), pony(0, -1, False)], kind="pixel", fps=4),
        A.Piece("Sheep (moving, grazing)",
                frames=[sheep(0, False), sheep(0, True), sheep(0.5, False), sheep(1, False), sheep(1, True),
                        sheep(1, False), sheep(0.5, False)], kind="pixel", fps=4),
        A.Piece("Duck and ducklings (moving, swimming)", frames=[ducks(k) for k in range(4)], kind="pixel", fps=4),
        A.Piece("Bat (moving, hanging and flapping)",
                frames=[bat(0, False), bat(0, False), bat(0, True), bat(1, False), bat(2, False), bat(1, False),
                        bat(2, False), bat(1, False)], kind="pixel", fps=5),
        A.Piece("Mouse (moving, peeking and nibbling)",
                frames=[mouse(2, 1, 0), mouse(2, 0, 1), mouse(1, 1, 0), mouse(0, 0, 0), mouse(1, 0, 0),
                        mouse(1, 1, 0), mouse(2, 0, 1)], kind="pixel", fps=5),
        A.Piece("Snail (moving, creeping)",
                frames=[snail(0, 0), snail(0.5, -1), snail(1, -1), snail(1, 0), snail(0.5, 1), snail(0, 1)],
                kind="pixel", fps=3),
        A.Piece("Butterfly (moving, fluttering)",
                frames=[butterfly(1, 0), butterfly(0.5, 1), butterfly(0.1, 2), butterfly(0.5, 1), butterfly(1, 0),
                        butterfly(0.5, 0), butterfly(0.1, 1), butterfly(0.5, 1)], kind="pixel", fps=8),
        A.Piece("Bear cub (moving, waving)",
                frames=[bear_cub(20, False), bear_cub(42, False), bear_cub(65, False), bear_cub(42, False),
                        bear_cub(20, False), bear_cub(42, False), bear_cub(65, False), bear_cub(42, True)],
                kind="pixel", fps=5),
        A.Piece("Beaver (moving, gnawing)",
                frames=[beaver(0, 0), beaver(1, 1), beaver(0, 2), beaver(1, 1)], kind="pixel", fps=5),
        A.Piece("Moose (moving, chewing)",
                frames=[moose(0, False, False), moose(1, False, False), moose(0, False, False), moose(1, True, False),
                        moose(0, False, True)], kind="pixel", fps=3),
    ]
