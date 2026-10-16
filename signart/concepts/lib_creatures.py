"""The art library's creatures: Valheim's monsters from the Meadows to the Plains as cheerful 8-bit sprites.

Drawn like the cat (concepts/anim2.py): numpy masks on a Canvas, outlined automatically in the cat's dark outline,
at the cat's pitch, so each one stands next to the cat at its true size (a greyling is small, a troll fills the
64 px limit). Empty pixels are not written: the sprites stand on their own over whatever is behind.
"""
import numpy as np

import artlib as A


def _sprite(c, pal):
    """A Canvas -> an outlined '#rgb' grid ('K' inside the drawing is the outline colour too)."""
    return A.colours(A.outline(c.g, "K"), {**pal, "K": A.OUTLINE})


def _wave(n, k):
    return np.sin(2 * np.pi * k / n)


# ---- Meadows and Black Forest ---------------------------------------------------------------------------------

GREYLING = {"B": "#864", "b": "#542", "T": "#431", "M": "#6a3", "E": "#fd5", "R": "#889", "r": "#667",
            "m": "#5a3"}


def greyling(k, n=6):
    """A greyling, a little greydwarf of mossy bark with glowing yellow eyes, peeks over a mossy stone and waves a
    twig hand; it blinks once a loop."""
    c = A.Canvas(26, 22)
    a = np.radians(65 + 40 * _wave(n, k))                     # the waving arm swings about the shoulder
    sx, sy = 17, 11
    hx, hy = sx + 7 * np.cos(a), sy - 7 * np.sin(a)
    for tx, ty in ((7, 1), (11, 0), (15, 2)):                  # twigs on the head
        c.paint(c.line(12, 6, tx, ty, 1.2), "T")
    c.paint(c.line(sx, sy, hx, hy, 2.4), "b")
    for d in (-35, 0, 35):                                     # twig fingers
        f = a + np.radians(d)
        c.paint(c.line(hx, hy, hx + 2.2 * np.cos(f), hy - 2.2 * np.sin(f), 1.0), "T")
    c.paint(c.ellipse(12, 15, 5, 5), "B")
    c.paint(c.ellipse(12, 9, 5.5, 4.8), "B")
    c.paint(c.ellipse(12, 5.6, 4.2, 1.6), "M")
    c.paint(c.rect(9, 12, 15, 13), "b")                        # a dark mouth line under the face
    if k == n - 1:
        c.paint(c.rect(9, 9, 11, 10), "K").paint(c.rect(13, 9, 15, 10), "K")
    else:
        c.paint(c.rect(9, 8, 11, 10), "E").paint(c.rect(13, 8, 15, 10), "E")
    stone = c.ellipse(12, 21, 12, 6.5) & (c.yy >= 15)
    c.paint(stone, "R")
    c.paint(stone & (c.yy >= 19), "r")
    c.paint(c.ellipse(9, 15.5, 6, 1.6) & stone, "m")
    c.paint(c.line(5, 14, 8, 15, 2.0), "b")                    # the other hand grips the stone
    return _sprite(c, GREYLING)


GREYDWARF = {"B": "#864", "b": "#532", "M": "#5a3", "m": "#383", "E": "#6bf", "e": "#dff", "W": "#ccb",
             "T": "#431", "R": "#99a", "r": "#667"}


def greydwarf(k, n=4):
    """A greydwarf, mossy bark with twigs on its head, a stone set in its chest and glowing blue eyes, walks in
    place: legs and long arms swing, the body bobs."""
    c = A.Canvas(24, 33)
    s = _wave(n, k)
    bob = 1 if k % 2 else 0
    hy = 22 + bob
    for side, d in ((-1, s), (1, -s)):                         # legs: thick bark stumps, feet swing
        x = 12 + 2.5 * side
        c.paint(c.line(x, hy, x + 3 * d, 30.5, 2.6), "b")
        c.paint(c.ellipse(x + 3 * d + side * 0.5, 31, 2.2, 1.2), "b")
    for side, d in ((-1, -s), (1, s)):                         # long arms, opposite to the legs
        x0 = 12 + 6 * side
        c.paint(c.line(x0, 14 + bob, x0 + side * 1.5 + 3 * d, 25 + bob, 2.4), "B")
        c.paint(c.line(x0 + side * 1.5 + 3 * d, 25 + bob, x0 + side * 2 + 3 * d, 27 + bob, 1.2), "b")
    c.paint(c.ellipse(12, 17 + bob, 6.5, 7), "B")
    for x in (9, 12, 15):                                      # bark grooves
        c.paint(c.rect(x, 15 + bob, x + 1, 21 + bob), "b")
    c.paint(c.ellipse(12.5, 18.5 + bob, 2.6, 2.2), "R")        # the stone in its chest
    c.paint(c.ellipse(12.5, 19.5 + bob, 2.2, 1.2) & c.ellipse(12.5, 18.5 + bob, 2.6, 2.2), "r")
    c.paint(c.ellipse(12, 7.5 + bob, 6, 5.8), "B")
    c.paint(c.ellipse(12, 3.8 + bob, 6.6, 2.8), "M")           # moss cap and shoulders
    c.paint(c.ellipse(6.2, 12 + bob, 2.6, 1.8) | c.ellipse(17.8, 12 + bob, 2.6, 1.8), "M")
    c.paint(c.rect(5, 3 + bob, 7, 5 + bob) | c.rect(17, 3 + bob, 19, 5 + bob), "m")
    c.paint(c.rect(8, 7 + bob, 11, 9 + bob) | c.rect(13, 7 + bob, 16, 9 + bob), "E")
    c.set(9, 7 + bob, "e").set(14, 7 + bob, "e")
    c.paint(c.rect(9, 11 + bob, 15, 12 + bob), "b")            # mouth
    c.paint(c.rect(10, 11 + bob, 11, 12 + bob) | c.rect(13, 11 + bob, 14, 12 + bob), "W")
    t = A.Canvas(24, 36)                                       # 3 more rows on top for the twigs
    t.g[3:] = c.g
    for x0, x1, y1 in ((8.5, 5, 1), (12, 12.5, 0), (15.5, 19, 1.5), (10, 8, 0.5)):   # twigs sprouting from the head
        t.paint(t.line(x0, 5 + bob, x1, y1 + bob, 1.1) & (t.g == ""), "T")
    t.set(4, 1 + bob, "T").set(20, 2 + bob, "T")
    return _sprite(t, GREYDWARF)


SHAMAN = {"B": "#764", "b": "#532", "M": "#5a3", "m": "#383", "E": "#7f4", "e": "#dfc", "P": "#9d4", "p": "#6a3",
          "Q": "#cf8", "A": "#dcb", "a": "#a98"}


def shaman(k):
    """A greydwarf shaman, mossy and hunched under pale antlers, its eyes glowing green, casts a puff of poison: it
    grows from its hands and fades."""
    c = A.Canvas(36, 28)
    r = (4, 3, 0, 2.5)[k]
    if r:                                                      # the puff: a cloud of discs
        for dx, dy, f in ((0, 0, 0.7), (-3, 1, 0.55), (3, 0.8, 0.6), (-1, -2.6, 0.55), (2, -2, 0.5)):
            c.paint(c.disc(28 + dx * r / 3, 14 + dy * r / 3, r * f), "p" if k == 1 else "P")
        c.paint(c.disc(27, 13, r * 0.4), "Q")
    c.paint(c.line(11, 21, 10, 26.5, 2.4) | c.line(15, 21, 16, 26.5, 2.4), "b")   # legs
    c.paint(c.ellipse(13, 17, 6, 6), "B")
    c.paint(c.rect(11, 14, 12, 20) | c.rect(14, 14, 15, 20), "b")
    lift = (2, 1, 0, 1)[k]                                     # both arms reach out to cast
    c.paint(c.line(15, 14, 22, 14 - lift, 2.2) | c.line(12, 15, 21, 16 - lift, 2.2), "B")
    c.paint(c.ellipse(13, 8, 5.5, 5), "B")
    c.paint(c.ellipse(12, 4.5, 7, 3.2) | c.triangle((5, 6), (2, 13), (8, 9)), "M")   # leafy hood
    c.paint(c.rect(6, 3, 8, 5) | c.rect(14, 2, 16, 4), "m")
    c.paint(c.ellipse(12.5, 19, 2, 1.5) | c.ellipse(16, 15, 1.5, 1), "m")   # moss on the body
    c.paint(c.rect(13, 7, 15, 9) | c.rect(17, 7, 19, 9), "E")
    c.set(13, 7, "e").set(17, 7, "e")
    c.paint(c.rect(14, 11, 18, 12), "b")
    t = A.Canvas(36, 34)                                       # 6 more rows on top for the antlers
    t.g[6:] = c.g
    free = t.g == ""
    for side in (-1, 1):                                       # two branching antlers, the shaman's mark
        bx = 12 + 3 * side
        tip = (bx + 4 * side, 0.5)
        t.paint(t.line(bx, 8, *tip, 1.3) & free, "A")
        t.paint(t.line(bx + 1.5 * side, 4.5, bx + 0.2 * side, 0.5, 1.0) & free, "A")
        t.paint(t.line(bx + 2.6 * side, 3, bx + 6.5 * side, 2.5, 1.0) & free, "A")
        t.paint(t.line(bx + 0.7 * side, 6.5, bx - 1 * side, 4.5, 1.0) & free, "a")
    return _sprite(t, SHAMAN)


TROLL = {"S": "#69b", "s": "#478", "L": "#9bd", "C": "#753", "c": "#532", "W": "#a73", "w": "#fc8", "H": "#224",
         "E": "#ff6", "T": "#fff"}


def troll(k):
    """A blue troll swings its log club: raised behind, overhead, down in a smash, and back up. A big round head
    sits low between the shoulders: a pointed ear, heavy brow, small yellow eyes, a bulbous nose, an underbite with
    two tusks; the free arm hangs long, its fist down by the knee."""
    ang = (120, 150, 60, -25, -25, 60)[k]
    c = A.Canvas(62, 62)
    a = np.radians(ang)
    sx, sy = 26, 28                                            # the club shoulder (the troll faces right)
    hx, hy = sx + 11 * np.cos(a), sy - 11 * np.sin(a)
    c.paint(c.line(21, 52, 20, 58, 7) | c.ellipse(19, 59, 5, 2.5), "s")      # legs and feet
    c.paint(c.line(33, 52, 35, 58, 7) | c.ellipse(36, 59, 5, 2.5), "s")
    c.paint(c.line(14, 30, 9, 52, 5.5) | c.ellipse(9, 54, 4, 3.5), "s")       # the long back arm hangs to the knee
    c.paint(c.ellipse(24, 38, 14, 15), "S")                    # body
    c.paint(c.ellipse(26, 43, 9, 9), "L")                      # belly
    c.paint(c.rect(13, 49, 36, 53) | c.triangle((20, 52), (32, 52), (26, 57)), "C")   # loincloth
    c.paint(c.rect(13, 49, 36, 50), "c")

    def club_arm():
        tip = (hx + 18 * np.cos(a), hy - 18 * np.sin(a))       # the log club in the fist
        c.paint(c.line(hx, hy, *tip, 4.5), "W")
        c.paint(c.line(hx + 6 * np.cos(a), hy - 6 * np.sin(a), tip[0], tip[1], 6.5), "W")
        c.paint(c.disc(*tip, 2.6), "w")
        c.paint(c.line(sx, sy, hx, hy, 8) | c.disc(hx, hy, 4.4), "s")   # the arm, a dark rim round it
        c.paint(c.line(sx, sy, hx, hy, 6) | c.disc(hx, hy, 3.4), "S")
        c.paint(c.disc(hx, hy, 3.4) & ~c.disc(hx - 0.8, hy - 0.8, 2.6), "s")

    if ang > 0:                                                # raised: the arm goes behind the head
        club_arm()
    c.paint(c.ellipse(30, 22, 9.5, 8.5), "S")                  # the big round head, low between the shoulders
    c.paint(c.triangle((23, 19), (15, 16.5), (22.5, 25)), "S")   # a pointed ear, swept back
    c.paint(c.line(21.5, 21, 18, 19, 1.0), "s")
    c.paint(c.ellipse(28, 14.5, 4, 2) | c.triangle((25, 15), (26, 10), (29, 14)), "H")   # a tuft of dark hair
    c.paint(c.ellipse(33, 19, 6.5, 1.6), "s")                  # the heavy brow
    c.paint(c.rect(29, 20, 31, 21) | c.rect(34, 20, 36, 21), "E")   # small eyes
    c.set(30, 20, "K").set(35, 20, "K")
    c.paint(c.ellipse(35.5, 23.5, 3.6, 2.8), "L")              # the wide bulbous nose
    c.paint(c.rect(34, 25, 35, 26) | c.rect(37, 25, 38, 26), "s")
    c.paint(c.ellipse(32, 28.5, 6.5, 2.5) & (c.yy >= 28), "L")  # the jutting lower jaw
    c.paint(c.rect(26, 27, 38, 28), "s")                       # mouth
    c.paint(c.rect(28, 25, 29, 28) | c.rect(36, 26, 37, 28), "T")   # two tusks pointing up
    if ang <= 0:
        club_arm()
    return _sprite(c, TROLL)


SKELETON = {"W": "#edc", "S": "#ba8", "D": "#213", "R": "#fb2", "I": "#99a", "H": "#653"}


def skeleton(k):
    """A skeleton of old yellowed bone, its eyes glowing amber, rattles: its bones jitter a pixel apart and its jaw
    clacks open."""
    up = (0, 1, 0, 1)[k]                                       # skull and arms hop a pixel against the ribs
    jit = (0, 1, 0, -1)[k]
    jaw = (0, 2, 0, 1)[k]
    c = A.Canvas(25, 36)
    sy = 2 - up
    c.paint(c.line(11.5, 13, 11.5, 26, 1.0), "W")              # spine
    for y, w in ((15, 4), (17, 4), (19, 3), (21, 2)):          # ribs, a gap between each
        c.paint(c.rect(int(11.5 - w), y, int(11.5 + w) + 1, y + 1), "W")
    c.paint(c.rect(8, 25, 15, 27), "W")                        # pelvis
    c.paint(c.rect(10, 25, 13, 26), "S")
    c.paint(c.line(9.5, 27, 8.5 + jit, 33, 1.0) | c.line(13.5, 27, 14.5 - jit, 33, 1.0), "W")   # legs
    c.paint(c.rect(6 + jit, 33, 9 + jit, 34) | c.rect(14 - jit, 33, 17 - jit, 34), "W")
    c.paint(c.line(6.5, 14 + up, 4.5 - jit, 23 + up, 1.0), "W")    # arms
    c.paint(c.line(16.5, 14 + up, 19.5, 19.5 + up, 1.0), "W")
    c.paint(c.line(19.5, 19.5 + up, 21.5, 13.5 + up, 1.0), "W")
    c.paint(c.line(21.5, 13 + up, 23.5, 1.5 + up, 1.0), "I")   # a rusty sword raised
    c.paint(c.rect(20, 13 + up, 24, 14 + up), "H")
    c.paint(c.ellipse(11.5 + jit * 0.6, 4.5 + sy, 5.5, 5.2), "W")   # skull
    x = 11 + (1 if jit > 0 else 0)
    c.paint(c.rect(x - 4, 4 + sy, x - 1, 7 + sy) | c.rect(x + 1, 4 + sy, x + 4, 7 + sy), "D")   # sockets
    c.set(x - 3, 5 + sy, "R").set(x + 2, 5 + sy, "R")
    c.paint(c.rect(x, 7 + sy, x + 1, 8 + sy), "D")
    c.paint(c.rect(x - 3, 9 + sy + jaw, x + 4, 11 + sy + jaw), "W")   # the jaw drops away from the skull
    c.paint(c.rect(x - 2, 9 + sy + jaw, x - 1, 10 + sy + jaw) | c.rect(x + 1, 9 + sy + jaw, x + 2, 10 + sy + jaw), "S")
    return _sprite(c, SKELETON)


# ---- Swamp ----------------------------------------------------------------------------------------------------

DRAUGR = {"G": "#8a6", "g": "#463", "I": "#889", "i": "#556", "R": "#a63", "r": "#742", "E": "#7fe", "T": "#554",
          "t": "#443", "h": "#675", "j": "#453", "H": "#753", "A": "#bbc"}


def draugr():
    """A draugr: a green, mossy undead viking with long lank hair and beard, glowing cyan eyes, rags and a rusty
    shoulder plate, an axe at the ready."""
    c = A.Canvas(28, 36)
    c.paint(c.line(10, 24, 9, 33, 3) | c.line(15, 24, 16, 33, 3), "t")          # legs
    c.paint(c.rect(6, 33, 11, 35) | c.rect(14, 33, 19, 35), "i")               # boots
    c.paint(c.line(6, 15, 4, 25, 2.6), "G")                    # the left arm hangs
    c.paint(c.ellipse(12.5, 19, 6.5, 7), "T")                  # tunic and rags
    c.paint(c.triangle((6, 22), (19, 22), (12.5, 29)) | c.rect(7, 22, 18, 26), "T")
    for x in (8, 11, 14, 17):                                  # ragged hem
        c.paint(c.rect(x, 26, x + 1, 28), "t")
    c.paint(c.rect(6, 21, 19, 22), "H")                        # belt
    c.paint(c.ellipse(12.5, 9, 5.2, 5), "G")                   # head
    c.paint(c.ellipse(12.5, 6.2, 6, 3.4) & (c.yy < 7), "h")    # lank hair over the scalp, down to the shoulders
    for x, y1, key in ((6, 16, "h"), (7, 14, "j"), (17, 15, "j"), (18, 17, "h")):
        c.paint(c.rect(x, 5, x + 1, y1), key)
    c.paint(c.rect(12, 3, 13, 6) | c.rect(9, 4, 10, 6) | c.rect(15, 4, 16, 6), "j")
    c.paint(c.rect(9, 8, 11, 9) | c.rect(14, 8, 16, 9), "E")
    c.paint(c.rect(10, 12, 15, 13), "g")
    for x, y1, key in ((9, 14, "j"), (10, 16, "h"), (11, 17, "h"), (12, 16, "j"), (13, 18, "h"), (14, 16, "h"),
                       (15, 14, "j")):                         # a straggly beard
        c.paint(c.rect(x, 13, x + 1, y1), key)
    c.paint(c.line(19, 15, 21, 21, 2.6), "G")                  # the axe arm
    c.paint(c.ellipse(18.5, 14.5, 2.6, 1.8), "R")              # a rusty shoulder plate
    c.paint(c.ellipse(18.5, 15.5, 2.4, 0.8) & c.ellipse(18.5, 14.5, 2.6, 1.8), "r")
    c.paint(c.line(22, 26, 23, 6, 1.4), "H")                   # the axe
    c.paint(c.triangle((22, 5), (27, 3), (27, 12)) | c.rect(22, 5, 25, 10), "A")
    c.paint(c.rect(22, 6, 24, 9), "I")
    c.paint(c.disc(21.5, 21, 1.6), "G")
    return _sprite(c, DRAUGR)


BLOB = {"B": "#9c3", "b": "#6a2", "H": "#dfa", "D": "#352", "S": "#dec", "s": "#ab8"}


def blob(k):
    """A blob, green ooze with a skull and a bone afloat inside, bounces: it squashes on the ground, stretches up,
    flies, and lands with a wobble."""
    rx, ry, lift = ((9, 7, 0), (10.5, 5.5, 0), (7.5, 8.5, 2), (8, 7.5, 6), (7.5, 8.5, 2), (10, 6, 0))[k]
    c = A.Canvas(24, 26)
    by = 25 - lift
    cy = by - ry
    body = c.ellipse(12, cy, rx, ry)
    c.paint(body, "B")
    c.paint(body & ~c.ellipse(12, cy - 1.5, rx - 0.5, ry - 1), "b")       # the darker underside
    c.paint(c.ellipse(12 - rx * 0.45, cy - ry * 0.5, 1.6, 1.0), "H")      # the shine
    for dx, dy in ((0.35, 0.35), (-0.15, 0.45)):                          # bubbles in the ooze
        c.paint(c.disc(12 + dx * rx, cy + dy * ry, 0.8), "b")
    c.paint(c.line(12 + rx * 0.25, cy + ry * 0.55, 12 + rx * 0.62, cy + ry * 0.2, 1.0), "s")   # a bone
    y0 = int(round(cy - 3))
    for dy, row in enumerate((".SSS.", "SSSSS", "SDSDS", "SSDSS", ".S.S.")):   # the skull inside
        for dx, ch in enumerate(row):
            if ch != ".":
                c.set(10 + dx, y0 + dy, ch)
    return _sprite(c, BLOB)


LEECH = {"L": "#446", "l": "#335", "S": "#88a", "R": "#c33", "r": "#a23", "W": "#354", "w": "#586", "E": "#fd5"}


def leech(k, n=4):
    """A leech, dark plates over red, wiggles at the surface of murky swamp water; the ripples roll with it."""
    c = A.Canvas(36, 16)
    ph = 2 * np.pi * k / n
    pts = []
    for i in range(40):                                          # humps travel along the body, in and out of the water
        x = 7 + i * 0.65
        y = 9.5 - 3.2 * np.sin(x * 0.38 + ph) - (2.5 if i < 4 else 0) * (4 - i) / 4
        r = 1.8 if i < 34 else 1.8 - (i - 33) * 0.2
        pts.append((x, y, r))
    for x, y, r in pts:
        c.paint(c.disc(x, y, r), "L")
    for i, (x, y, r) in enumerate(pts):
        if i % 4 == 2:                                          # segment rings, red between the dark plates
            c.paint(c.disc(x, y - 0.8, 0.7), "S")
            c.paint(c.disc(x + 0.6, y + 0.9, 0.6), "r")
    hx, hy, _ = pts[0]
    c.paint(c.disc(hx - 1, hy, 2.3), "l")                       # the head rears up
    c.paint(c.disc(hx - 2.4, hy + 0.5, 1.0), "R")               # the sucker mouth
    c.set(int(hx) - 1, int(hy) - 2, "E")
    c.paint(c.rect(0, 11, 36, 16), "W")
    for x0 in range(-9, 36, 9):                                  # ripples roll along
        x = x0 + int(9 * k / n)
        c.paint(c.rect(x, 11, x + 4, 12), "w")
    return _sprite(c, LEECH)


SURTLING = {"R": "#c32", "r": "#812", "D": "#622", "C": "#fc4", "Y": "#ffc", "F": "#f52", "O": "#fa3", "E": "#ffd"}


def surtling(k):
    """A surtling, a glowing red ember imp: its head fire flickers and its cracks glow."""
    c = A.Canvas(20, 24)
    fl = ((0, 0), (1, -1), (-1, 1), (0, -2))[k]
    c.paint(c.triangle((4 + fl[0], 12), (10 + fl[1], -0.5 + abs(fl[1])), (16 + fl[0], 12)), "F")   # flames
    c.paint(c.triangle((6, 12), (9 + fl[0], 2 + abs(fl[0])), (12, 12)), "O")
    c.paint(c.triangle((9, 12), (13 + fl[1], 3 + abs(fl[1])), (15, 12)), "O")
    c.paint(c.triangle((8, 12), (10 + fl[1], 6), (12, 12)), "Y")
    c.paint(c.line(5, 15, 2, 19 + fl[0], 2), "D")              # charred arms and legs
    c.paint(c.line(15, 15, 18, 19 - fl[0], 2), "D")
    c.paint(c.line(8, 20, 7, 23, 2) | c.line(12, 20, 13, 23, 2), "D")
    c.paint(c.ellipse(10, 15, 5.5, 5.5), "R")
    c.paint(c.ellipse(10, 18.5, 4.5, 2) & c.ellipse(10, 15, 5.5, 5.5), "r")
    c.paint(c.rect(7, 13, 9, 15) | c.rect(11, 13, 13, 15), "E")
    c.paint(c.rect(9, 17, 11, 18), "C" if k % 2 else "Y")
    c.set(6, 17, "C").set(13, 19, "C").set(14, 16, "C" if k % 2 == 0 else "R")
    return _sprite(c, SURTLING)


# ---- Plains ---------------------------------------------------------------------------------------------------

SQUITO = {"B": "#243", "b": "#354", "g": "#5a4", "A": "#f73", "a": "#b32", "E": "#cf4", "e": "#ffc", "N": "#ccb",
          "M": "#bdf", "m": "#8ab", "L": "#354"}


def deathsquito(k):
    """A deathsquito, dark green with big glowing eyes and an orange abdomen, hovers with its long needle out; the
    wings beat up and down and blur, the body bobs."""
    wing, bob = (("up", 0), ("mid", 1), ("down", 1), ("blur", 0))[k]
    c = A.Canvas(32, 24)
    y = 11 + bob
    c.paint(c.line(3, y + 1, 12, y + 1, 1.0) | c.line(0, y + 1.5, 3, y + 1, 0.9), "N")   # the long needle
    for x0, dx in ((14, -2), (16, 0), (18, 3)):                  # dangling legs
        c.paint(c.line(x0, y + 2, x0 + dx, y + 9, 1.0), "L")
    abd = c.ellipse(23, y + 2.5, 7, 3.2)
    c.paint(abd, "A")
    for x in (20, 23, 26):                                       # stripes on the glowing abdomen
        c.paint(abd & c.rect(x, 0, x + 1, 24), "a")
    c.paint(c.ellipse(16.5, y + 0.5, 3.5, 3), "B")              # thorax, green spotted
    c.set(16, y - 1, "g").set(18, y + 1, "g").set(15, y + 2, "g")
    c.paint(c.disc(12.5, y + 0.5, 2.3), "b")                    # head
    c.paint(c.rect(11, y - 1, 14, y + 1), "E")                  # the big glowing eyes
    c.set(12, y - 1, "e")
    if wing == "blur":
        c.paint(c.ellipse(18, y - 4, 8, 6) & (c.yy < y - 1), "m")
        c.paint(c.ellipse(18, y - 4, 6, 4) & (c.yy < y - 1), "M")
    else:
        tip = {"up": (21, y - 11), "mid": (27, y - 6), "down": (26, y + 1)}[wing]
        c.paint(c.line(17, y - 1, tip[0], tip[1], 4.5), "M")
        c.paint(c.line(17, y - 1, tip[0] - 4, tip[1] + 1, 3.0), "M")
        c.paint(c.line(17, y - 1, tip[0], tip[1], 1.0), "m")
    return _sprite(c, SQUITO)


FULING = {"G": "#9b4", "g": "#693", "C": "#864", "c": "#542", "W": "#eed", "E": "#f82", "H": "#753", "Y": "#ff6",
          "O": "#fa3", "F": "#e42", "T": "#fff", "Q": "#ffd"}


def fuling(k):
    """A fuling, a yellow-green goblin with wide pointed ears, amber eyes and a bone necklace, holds up a torch; the
    tall flame flickers,
    changing shape (it leans, splits into tongues) and colour, and throws a spark or two."""
    c = A.Canvas(29, 33)
    c.paint(c.line(9, 23, 8, 30, 2.2) | c.line(14, 23, 15, 30, 2.2), "G")   # legs
    c.paint(c.rect(6, 30, 10, 32) | c.rect(14, 30, 18, 32), "g")
    c.paint(c.line(7, 15, 5, 23, 2.0), "G")                     # the free arm
    c.paint(c.ellipse(11.5, 19, 5, 5.5), "G")                   # body
    c.paint(c.rect(7, 21, 17, 24) | c.triangle((8, 24), (15, 24), (11.5, 27)), "C")   # loincloth
    c.paint(c.rect(7, 21, 17, 22), "c")
    for x in (8, 10, 12, 14, 16):                                # the bone necklace
        c.set(x, 15 + (1 if 9 < x < 15 else 0), "W")
    c.paint(c.ellipse(11.5, 10, 5.5, 4.5), "G")                 # head
    c.paint(c.triangle((7.5, 7.5), (0, 6.5), (7.5, 12)) | c.triangle((15.5, 7.5), (23, 6.5), (15.5, 12)), "G")   # ears
    c.paint(c.line(6, 9.5, 2.5, 8, 1.0) | c.line(17, 9.5, 20.5, 8, 1.0), "g")
    c.paint(c.rect(8, 8, 15, 9), "g")                           # brow
    c.paint(c.rect(9, 9, 10, 10) | c.rect(13, 9, 14, 10), "E")
    c.paint(c.ellipse(11.5, 11.3, 1.7, 2.0), "g")               # a big nose
    c.paint(c.rect(9, 13, 14, 14), "c")
    c.set(10, 13, "T").set(13, 13, "T")                          # fangs
    c.paint(c.line(16, 16, 22, 13, 2.2), "G")                   # the torch arm
    c.paint(c.line(23.5, 16, 24.5, 5, 1.5), "H")                # the torch
    c.paint(c.ellipse(24.5, 5.5, 2, 1.2), "c")
    c.paint(c.disc(23, 13.5, 1.6), "G")
    t = A.Canvas(29, 37)                                       # 4 more rows on top for the tall flame
    t.g[4:] = c.g
    tips, tongues, core, sparks = (
        ((24.5, 0.5), [(21.5, 4.5)], (24.5, 3.5, "Y"), [(27, 1)]),
        ((26, 1.5), [(22, 3)], (25, 4.5, "Q"), [(21, 0), (28, 3)]),
        ((24, 0), [(22, 2), (27, 1.5)], (24.5, 3, "Y"), [(26, 0)]),
        ((23, 2.5), [(27, 3.5)], (24, 5, "Q"), [(22, 0), (27, 0)]),
    )[k]
    t.paint(t.ellipse(24.5, 8.5, 3.6, 2.2), "F")               # the flame: red tips, orange, a yellow core
    t.paint(t.triangle((21, 8.5), tips, (28, 8.5)), "F")
    for g in tongues:
        t.paint(t.triangle((22.5, 8.5), g, (26.5, 8.5)), "F")
    t.paint(t.ellipse(24.5, 8.3, 2.7, 1.7) | t.triangle((22, 8), (core[0], core[1] - 1.5), (27, 8)), "O")
    t.paint(t.ellipse(core[0], 7.2, 1.3, 1.5) | t.triangle((23.5, 7.5), (core[0], core[1] + 0.5), (25.5, 7.5)),
            core[2])
    for x, y in sparks:
        t.set(x, y, "Y")
    return _sprite(t, FULING)


# ---- Mountain -------------------------------------------------------------------------------------------------

DRAKE = {"B": "#bdf", "b": "#7ac", "L": "#eff", "M": "#8bd", "m": "#69b", "H": "#fff", "E": "#24a", "C": "#eef",
         "X": "#aef"}


def drake(k):
    """A pale icy-blue drake with long swept-back horns and ice crystals along its back flies left; its wings flap up,
    out and down, the body rising against the beat."""
    wing = ("up", "mid", "down", "mid")[k]
    bob = (1, 0, -1, 0)[k]
    c = A.Canvas(62, 42)
    y = 22 + bob
    c.paint(c.line(40, y, 50, y + 3, 4) | c.line(50, y + 3, 60, y + 1, 2.2), "B")   # tail
    c.paint(c.triangle((58, y - 1), (61.5, y - 3), (61.5, y + 4)), "b")
    c.paint(c.line(36, y + 3, 38, y + 9, 2.2) | c.line(28, y + 3, 30, y + 8, 2.2), "b")   # tucked legs
    root = (31, y - 3)
    if wing == "up":
        tips = [(18, 1), (29, 0), (40, 2)]
    elif wing == "mid":
        tips = [(10, y - 11), (24, y - 16), (42, y - 13)]
    else:
        tips = [(14, y + 15), (25, y + 17), (38, y + 14)]
    far = c.triangle(root, (tips[0][0] + 6, tips[0][1]), (tips[2][0] + 8, tips[2][1]))
    c.paint(far, "m")                                            # the far wing behind
    c.paint(c.ellipse(32, y, 11, 5.5), "B")                     # body
    c.paint(c.ellipse(30, y + 2.5, 8, 2.5) & c.ellipse(32, y, 11, 5.5), "L")
    c.paint(c.line(24, y - 1, 14, y - 7, 4.2), "B")             # neck
    c.paint(c.line(23, y + 1, 15, y - 5, 1.2), "L")
    c.paint(c.ellipse(10, y - 8, 4.6, 3.2), "B")                # head
    c.paint(c.line(7, y - 7, 3, y - 6, 2.4), "B")               # snout
    c.paint(c.line(12, y - 10, 18, y - 12, 1.2) | c.line(18, y - 12, 21, y - 11, 1.0), "C")   # long swept-back horns
    c.paint(c.line(10, y - 10.5, 15, y - 14, 1.0) | c.line(15, y - 14, 18, y - 14.5, 1.0), "C")
    c.paint(c.rect(9, y - 9, 11, y - 7), "E")
    c.set(9, y - 9, "H")
    for x in range(26, 42, 3):                                   # ice crystals along the back
        c.set(x, y - 6, "X").set(x, y - 7, "X")
    wing_m = np.zeros_like(far)
    for a, b in zip([root] + tips, tips):
        wing_m |= c.triangle(root, a, b)
    c.paint(wing_m, "M")                                         # the near wing, its bones from the root
    for t in tips:
        c.paint(c.line(root[0], root[1], t[0], t[1], 1.2), "B")
    return _sprite(c, DRAKE)


def pieces():
    return [
        A.Piece("Greyling (moving, peek and wave)", "pixel", frames=[greyling(k) for k in range(6)], fps=5),
        A.Piece("Greydwarf (moving, walk)", "pixel", frames=[greydwarf(k) for k in range(4)], fps=5),
        A.Piece("Greydwarf shaman (moving, poison puff)", "pixel", frames=[shaman(k) for k in range(4)], fps=4),
        A.Piece("Troll (moving, club swing)", "pixel", frames=[troll(k) for k in range(6)], fps=4),   # 4: fits the play budget (0.20.167)
        A.Piece("Skeleton (moving, rattle)", "pixel", frames=[skeleton(k) for k in range(4)], fps=6),
        A.Piece("Draugr", "pixel", frames=[draugr()]),
        A.Piece("Blob (moving, bounce)", "pixel", frames=[blob(k) for k in range(6)], fps=6),
        A.Piece("Leech (moving, wiggle)", "pixel", frames=[leech(k) for k in range(4)], fps=4),
        A.Piece("Surtling (moving, flicker)", "pixel", frames=[surtling(k) for k in range(4)], fps=6),
        A.Piece("Deathsquito (moving, buzz)", "pixel", frames=[deathsquito(k) for k in range(4)], fps=8),
        A.Piece("Fuling (moving, torch)", "pixel", frames=[fuling(k) for k in range(4)], fps=5),
        A.Piece("Drake (moving, flap)", "pixel", frames=[drake(k) for k in range(4)], fps=4),
    ]
