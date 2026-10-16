"""Kitchen icons: labels for storage chests and the kitchen corner, one bold symbol (or a small cluster) a category.

Drawn on a 26 x 26 canvas with numpy masks, every shape shaded the same way (a light rim to the top left, a dark
rim to the bottom right) from one shared palette, outlined in the cat's dark outline and set on a round parchment
plate, so the set reads as one family. pitch 1.25 (32 px = 1 m): an icon is about 1 m across, readable on a chest
from a few metres. Mostly stills; fire, the cauldron and the frying pan move.
"""
import numpy as np

import artlib as A

N = 26                                                  # the drawing canvas; the outline and the plate add 8
PITCH = 1.25
TAU = 2 * np.pi

# shade families: base, light, dark
FAM = {
    "or": ("#e72", "#fa4", "#b41"),                     # carrot, cloudberry
    "gr": ("#4a3", "#7d4", "#272"),                     # leaves, herbs
    "rd": ("#d23", "#f66", "#912"),                     # raspberry, red cap, jam
    "bl": ("#45c", "#79e", "#228"),                     # blueberry
    "tu": ("#eed", "#fff", "#bba"),                     # turnip, egg, salt, bone
    "pu": ("#a4a", "#d7c", "#727"),                     # turnip top
    "on": ("#c95", "#eb7", "#963"),                     # onion, straw
    "wd": ("#a63", "#c85", "#742"),                     # wood
    "br": ("#c82", "#ea4", "#853"),                     # bread crust
    "rm": ("#c34", "#e67", "#823"),                     # raw meat
    "ck": ("#a52", "#c74", "#631"),                     # cooked meat
    "sg": ("#b43", "#d65", "#722"),                     # sausage
    "st": ("#99a", "#ccd", "#667"),                     # steel, stone
    "ir": ("#445", "#667", "#223"),                     # cast iron
    "fs": ("#79b", "#ace", "#457"),                     # fish
    "hn": ("#fb2", "#fe7", "#c71"),                     # honey, flame core
    "cl": ("#b73", "#d95", "#852"),                     # clay pot
    "gl": ("#9cd", "#def", "#68a"),                     # glass
    "gn": ("#db5", "#fe8", "#a82"),                     # grain, barley
    "ye": ("#ec3", "#fe7", "#a81"),                     # yellow mushroom, yolk
    "sp": ("#7a4", "#ad6", "#462"),                     # stew, soup green
    "ro": ("#b46", "#d79", "#734"),                     # red onion
    "pl": ("#dc9", "#eda", "#b97"),                     # the plate
}
PAL = {"K": A.OUTLINE, "W": "#fff", "Y": "#fe6", "R": "#f53", "E": "#111", "S": "#dde", "T": "#aab",
       "M": "#fdd", "F": "#f82", "Fd": "#c41"}
for _k, (_b, _l, _d) in FAM.items():
    PAL[_k], PAL[_k + "L"], PAL[_k + "D"] = _b, _l, _d


def _shift(m, dx, dy):
    """m moved by (dx, dy): out[y, x] = m[y - dy, x - dx], False outside."""
    out = np.zeros_like(m)
    h, w = m.shape
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = m[ys, xs]
    return out


def body(c, m, fam, light=True, dark=True):
    """A shaded shape: the family's base, a light rim where it faces the top left, a dark rim to the bottom right."""
    c.paint(m, fam)
    if light:
        c.paint(m & ~_shift(m, 1, 1), fam + "L")
    if dark:
        c.paint(m & ~_shift(m, -1, -1), fam + "D")
    return m


def ring(m):
    """The pixels just outside m (4 neighbours)."""
    near = _shift(m, 1, 0) | _shift(m, -1, 0) | _shift(m, 0, 1) | _shift(m, 0, -1)
    return near & ~m


def rell(c, cx, cy, a, b, ang=0.0):
    """An ellipse with half axes a (along ang degrees, clockwise on screen) and b."""
    t = np.radians(ang)
    dx, dy = c.px - cx, c.py - cy
    u = dx * np.cos(t) + dy * np.sin(t)
    v = -dx * np.sin(t) + dy * np.cos(t)
    return (u / a) ** 2 + (v / b) ** 2 <= 1


def leaf(c, x, y, ang, n=4.0, w=1.6):
    """A pointed leaf from (x, y) heading ang degrees (0 = right, 90 = up), n long."""
    t = np.radians(ang)
    cx, cy = x + n / 2 * np.cos(t), y - n / 2 * np.sin(t)
    return rell(c, cx, cy, n / 2, w, -ang)


def icon(c, plate=True):
    """The finished icon: outlined, on the shared plate (outlined again), as '#rgb' rows."""
    g = A.outline(c.g, "K")
    if plate:
        p = A.Canvas(N + 6, N + 6)                      # a pixel of room round the disc: a full circle
        body(p, p.disc((N + 6) / 2, (N + 6) / 2, (N + 4) / 2), "pl")
        m = g != ""
        pg = p.g
        pg[2:-2, 2:-2][m] = g[m]
        g = A.outline(pg, "K")
    return A.colours(g, PAL)


def berry(c, x, y, r, fam):
    body(c, c.disc(x, y, r), fam)
    c.set(int(x - r / 2), int(y - r / 2), "W")


# ---- the stills ----------------------------------------------------------------------------------------------

def spices():
    """A stone mortar with a wooden pestle and a sprig of herbs."""
    c = A.Canvas(N, N)
    for x, y, a in [(6, 12, 100), (6, 11, 140), (8, 12, 70), (5, 13, 160)]:
        body(c, leaf(c, x, y, a, 7, 1.7), "gr")
    c.paint(c.line(7, 15, 6, 6, 1.0), "grD")
    body(c, c.line(14, 14, 21, 2.5, 3.2), "wd")
    body(c, c.disc(21, 3, 2.2), "wd")
    bowl = (c.ellipse(13, 14, 11, 9) & (c.py > 14)) | c.ellipse(13, 14, 11, 2.6)
    body(c, bowl, "st")
    body(c, c.rect(8, 22, 18, 25), "st")
    c.paint(c.ellipse(13, 14, 9, 1.6), "irL")
    heap = c.ellipse(13, 14, 7, 3.5) & (c.py < 14.6)
    body(c, heap, "or", dark=False)
    c.paint(heap & c.ellipse(15, 13.5, 3, 2), "rd")
    c.paint(heap & c.ellipse(10, 13.5, 2.5, 1.8), "ye")
    for x, y in [(12, 12), (16, 13), (9, 14)]:
        c.set(x, y, "W")
    return c


def vegetables():
    """A carrot, a turnip and an onion together."""
    c = A.Canvas(N, N)
    for a in (60, 90, 120):                             # turnip leaves
        body(c, leaf(c, 18, 9, a, 7, 1.6), "gr")
    body(c, c.ellipse(18, 15, 6.5, 6), "tu")
    body(c, c.ellipse(18, 12, 6.5, 3.2) & c.ellipse(18, 15, 6.5, 6), "pu", dark=False)
    c.paint(c.line(18, 20, 18, 24, 1), "tuD")
    for a in (100, 130, 155):                           # carrot tops
        body(c, leaf(c, 6, 6, a + 0, 6, 1.4), "gr")
    body(c, c.triangle((3, 6.5), (9.5, 4.5), (14, 22)) | c.disc(6, 6, 3), "or")
    for y in (10, 13, 16):
        c.set(int(7 + (y - 6) * 0.35), y, "orD")
    for a in (80, 105):                                 # onion sprouts
        body(c, leaf(c, 6.5, 15, a, 4, 1.0), "gr", dark=False)
    onion = c.disc(6.5, 20.5, 4.8) | c.triangle((4, 18), (9, 18), (6.5, 14.5))
    c.paint(ring(onion), "K")
    body(c, onion, "ro")
    for dx in (-2.2, 2.2):
        c.paint(rell(c, 6.5 + dx, 20.5, 0.5, 3.8) & onion, "roD")
    c.paint(c.line(5.5, 25, 7.5, 25, 1), "roD")
    return c


def fruit():
    """Berries: raspberries, blueberries and a cloudberry with two leaves."""
    c = A.Canvas(N, N)
    body(c, leaf(c, 13, 13, 120, 11, 3), "gr")
    body(c, leaf(c, 13, 13, 60, 11, 3), "gr")
    c.paint(c.line(13, 13, 13, 5, 1), "grD")
    for x, y in [(7, 14), (10, 14), (8.5, 16.5), (5.5, 16.5), (11.5, 16.5), (7, 19), (10, 19), (8.5, 21.5)]:
        berry(c, x, y, 1.9, "rd")
    for x, y, r in [(17, 19, 3), (21.5, 17, 2.8), (21, 22, 2.8)]:
        berry(c, x, y, r, "bl")
    for x, y in [(15, 10), (18, 10), (16.5, 12.5), (19.5, 12.5), (13.5, 12.5)]:
        berry(c, x, y, 1.7, "or")
    return c


def mushrooms():
    """A big red cap with white spots and a small yellow one beside it."""
    c = A.Canvas(N, N)
    body(c, c.rect(18, 15, 22, 25), "tu")
    body(c, c.ellipse(20, 15, 5.5, 4.5) & (c.py < 16.5), "ye")
    body(c, c.rect(7, 13, 13, 25) | c.ellipse(10, 24, 4, 1.5), "tu")
    body(c, c.ellipse(10, 13, 10, 9) & (c.py < 14.5), "rd")
    for x, y in [(6, 9), (11, 7), (14, 11), (4, 12), (9, 12)]:
        c.paint(c.disc(x, y, 1.1), "W")
    c.paint(c.rect(1, 14, 20, 15) & c.ellipse(10, 13, 10, 9), "rdD")
    return c


def meat():
    """A raw steak: red with a white fat edge, marbling and a round bone."""
    c = A.Canvas(N, N)
    shape = rell(c, 13, 13, 12, 9, -20) | c.disc(7, 18, 6)
    c.paint(shape, "M")
    body(c, rell(c, 13.5, 12.5, 10.5, 7.5, -20) | c.disc(7.5, 17.5, 4.8), "rm")
    for x0, y0, x1, y1 in [(9, 10, 14, 8), (14, 14, 19, 11), (8, 15, 11, 13)]:
        c.paint(c.line(x0, y0, x1, y1, 0.9), "M")
    body(c, c.disc(18, 15, 2.8), "tu")
    c.paint(c.disc(18, 15, 1.1), "tuD")
    return c


def cooked():
    """A roast drumstick: brown meat on a white bone with two knobs."""
    c = A.Canvas(N, N)
    body(c, c.line(15, 11, 21, 5, 2.6), "tu")
    for x, y in [(20, 3.5), (22.8, 6)]:
        body(c, c.disc(x, y, 2.2), "tu")
    body(c, rell(c, 10, 16, 9, 7, -40), "ck")
    c.paint(rell(c, 8, 13, 3, 1.4, -40), "ckL")
    c.set(6, 14, "Y")
    for x0, y0 in [(7, 20), (11, 19), (13, 15)]:
        c.paint(c.line(x0, y0, x0 + 2, y0 - 2, 0.9), "ckD")
    return c


def sausages():
    """A curved string of three linked sausages."""
    c = A.Canvas(N, N)
    for cx, cy, ang in [(5.5, 8, -65), (10.5, 17.5, -35), (19.5, 20.5, 0)]:
        body(c, rell(c, cx, cy, 7, 3.6, ang), "sg")
        c.paint(rell(c, cx - 1.2, cy - 1.6, 3.5, 0.7, ang), "sgL")
    for x, y in [(8, 14.2), (15.3, 20)]:
        body(c, c.disc(x, y, 1.4), "on", dark=False)
    body(c, c.line(2.5, 0.5, 2.5, 2.5, 1.2), "on", dark=False)
    body(c, c.line(24.5, 20, 25.5, 20, 1.2), "on", dark=False)
    return c


def fish():
    """A fish seen from the side: tail, fins, scales, an eye."""
    c = A.Canvas(N, N)
    body(c, c.triangle((5.5, 14), (0.5, 7), (0.5, 21)), "fs")
    body(c, c.triangle((10, 9), (16, 9), (11, 4)) | c.triangle((11, 19), (15, 19), (11, 23)), "fs")
    body(c, c.ellipse(14.5, 14, 11, 6.2), "fs")
    c.paint(c.ellipse(14.5, 16.5, 9, 3) & c.ellipse(14.5, 14, 10, 5.2), "tu")
    c.paint(c.line(19, 9.5, 19, 18.5, 1) & c.ellipse(14.5, 14, 11, 6.2), "fsD")
    for x, y in [(10, 12), (13, 12), (16, 12), (11.5, 14), (14.5, 14)]:
        c.set(int(x), int(y), "fsL")
    c.paint(c.disc(22, 12.5, 1.5), "W")
    c.set(22, 12, "E")
    c.set(25, 15, "fsD")
    return c


def eggs():
    """Three eggs in a straw nest."""
    c = A.Canvas(N, N)
    for cx, cy, ang in [(5.5, 10, 0), (20.5, 10, 0), (13, 8, 0)]:
        egg = c.ellipse(cx, cy, 3.9, 5.2) | c.ellipse(cx, cy + 1.2, 4.2, 4)
        c.paint(ring(egg), "K")                         # a dark line keeps overlapping eggs apart
        body(c, egg, "tu")
        c.set(int(cx - 1), int(cy - 2), "W")
    body(c, c.ellipse(13, 17, 12.5, 6) & (c.py > 13), "on")
    for k in range(7):
        x = 2 + 3.4 * k
        c.paint(c.line(x, 15, x + 3, 21, 0.9) & c.ellipse(13, 17, 11.5, 5), "onD")
        c.paint(c.line(x + 3, 15, x, 20, 0.9) & c.ellipse(13, 17, 11.5, 5), "onL")
    return c


def bread():
    """A golden loaf with a scored top and a round bun with a cross in front."""
    c = A.Canvas(N, N)
    loaf = (c.ellipse(12, 11, 11.5, 7) & (c.py < 14)) | (c.ellipse(12, 14, 11.5, 3) & (c.py >= 14))
    body(c, loaf, "br")
    for x in (5, 10, 15, 20):
        c.paint(c.line(x + 1, 6.5, x - 1, 11, 1.2), "gnL")
    bun = c.ellipse(18, 19.5, 7, 5.5)
    c.paint(ring(bun), "K")
    body(c, bun, "br")
    c.paint(bun & (np.abs(c.px - 18) < 0.6) & (c.py < 22), "gnL")
    c.paint(bun & (np.abs(c.py - 18) < 0.6) & (np.abs(c.px - 18) < 4), "gnL")
    return c


def grain():
    """A tied flour sack with a dusting of flour and an ear of barley."""
    c = A.Canvas(N, N)
    sack = c.ellipse(12, 17, 9.5, 8) | c.triangle((7, 9), (17, 9), (12, 12))
    body(c, sack, "tu")
    body(c, c.ellipse(12, 6, 5, 3.5) | c.triangle((9, 3), (8, 8), (11, 6)), "tu")
    c.paint(c.rect(8, 8, 17, 10), "wdD")
    c.paint(c.ellipse(12, 18, 3.6, 2.4), "S")
    c.set(12, 18, "T")
    c.paint(c.line(22, 25, 19, 3, 1), "gnD")
    for k in range(6):
        y = 4 + 2.4 * k
        x = 19 + (y - 3) * 3 / 22
        for s in (-1, 1):
            body(c, leaf(c, x, y + 1, 90 + s * 40, 3.2, 1.1), "gn", dark=False)
    return c


def honey():
    """A clay honey pot, honey dripping over its rim, a dipper and a bee."""
    c = A.Canvas(N, N)
    body(c, c.ellipse(12, 17, 9, 8), "cl")
    body(c, c.ellipse(12, 9.5, 7, 2.5), "cl")
    c.paint(c.ellipse(12, 9.3, 5.4, 1.4), "hnD")
    drip = c.ellipse(12, 11.3, 7, 1.6) | c.line(7, 11, 7, 15, 1.8) | c.disc(7, 15.3, 1.2) | c.line(15, 11, 15, 13, 1.6)
    body(c, drip, "hn", dark=False)
    body(c, c.line(14, 9, 20, 2, 1.6), "wd", light=False)
    body(c, c.ellipse(20.5, 1.8, 1.6, 1.3), "wd", light=False)
    c.paint(c.rect(4, 19, 21, 20) & c.ellipse(12, 17, 9, 8), "clD")
    body(c, c.ellipse(22, 14, 2.6, 1.8), "hn", light=False)
    c.paint(c.line(21.5, 12.5, 21.5, 15.5, 0.8) | c.line(23, 12.5, 23, 15.5, 0.8), "E")
    c.paint(c.ellipse(21, 11.5, 1.4, 1) | c.ellipse(23.5, 11.5, 1.4, 1), "S")
    return c


def sweets():
    """A slice of layered berry cake with cream and a raspberry on top."""
    c = A.Canvas(N, N)
    top = c.triangle((1, 13), (14, 7), (25, 12))
    side = c.triangle((1, 13), (25, 12), (25, 23)) | c.triangle((1, 13), (1, 22), (25, 23))
    c.paint(side & ~c.rect(0, 0, 1, 26), "br")
    body(c, side & (c.py > 21), "brD", light=False, dark=False)
    for y0, k in [(14.5, "tu"), (18.5, "rd")]:
        c.paint(side & (np.abs(c.py - (y0 + (c.px - 1) * 0.0)) < 1.1), k)
    body(c, top, "tu")
    for x, y in [(4, 12.5), (8, 12), (12, 12), (16, 12.5), (20, 13), (23.5, 13.5)]:
        c.paint(c.disc(x, y, 1.6), "W")
    berry(c, 14, 7.5, 2.5, "rd")
    c.paint(leaf(c, 14, 5.5, 60, 3, 0.9), "gr")
    return c


def soup():
    """A wooden bowl of stew with carrot and meat chunks and a spoon."""
    c = A.Canvas(N, N)
    body(c, c.line(16, 12, 23, 1.5, 2.2), "wd")
    body(c, c.ellipse(23, 1.8, 2, 1.5), "wd")
    bowl = (c.ellipse(13, 14, 12.5, 10) & (c.py > 14)) | c.ellipse(13, 14, 12.5, 3)
    body(c, bowl, "wd")
    c.paint(c.ellipse(13, 13.8, 10.5, 2.2), "ck")
    for x, y, k in [(8, 13, "or"), (12, 14, "rm"), (17, 13, "or"), (10, 15, "gr"), (15, 15, "ckL")]:
        c.paint(c.rect(x, y, x + 2, y + 1), k)
    c.paint(c.ellipse(13, 21, 10, 0.8) & bowl, "wdL")
    body(c, c.rect(9, 23, 17, 26), "wd")
    for x, y in [(7, 7), (12, 5), (18, 7)]:
        c.paint(c.line(x, y + 4, x + 1, y, 1) & ~bowl, "S")
    return c


def drinks():
    """A wooden tankard with iron bands and mead foaming over the top."""
    c = A.Canvas(N, N)
    body(c, c.ellipse(20.5, 14, 5, 6) & ~c.ellipse(20.5, 14, 2.5, 3.5), "wd")
    mug = c.rect(3, 7, 19, 25)
    body(c, mug, "wd")
    for x in (7, 11, 15):
        c.paint(c.rect(x, 9, x + 1, 24), "wdD")
    for y in (9, 21):
        body(c, c.rect(2, y, 20, y + 2), "ir")
    foam = c.ellipse(11, 6, 9.5, 3.5) | c.disc(5, 5, 3) | c.disc(11, 3.5, 3.5) | c.disc(16.5, 5, 3) | \
        c.ellipse(4, 10, 1.5, 3)
    body(c, foam, "tu")
    c.set(10, 2, "W")
    return c


def cookware():
    """A black cooking pot with a steel lid and, in front of it, a frying pan with a wooden handle."""
    c = A.Canvas(N, N)
    body(c, c.ellipse(11, 12, 9, 6) & (c.py > 8), "ir")
    body(c, c.ellipse(11, 7.5, 9.5, 2.2), "ir")
    body(c, c.ellipse(11, 6.5, 7.5, 2), "st")
    body(c, c.ellipse(11, 4.2, 2, 1.4), "st")
    for s in (-1, 1):
        body(c, c.ellipse(11 + s * 10, 10, 1.5, 1.8), "ir")
    body(c, c.line(1, 25, 8, 21, 2.6), "wd")
    pan = c.ellipse(16, 18.5, 9, 5.5)
    c.paint(ring(pan), "K")
    body(c, pan, "ir")
    c.paint(c.ellipse(16, 18, 7, 3.8), "irD")
    c.paint(c.ellipse(14, 16.5, 3, 0.8), "irL")
    return c


def knives():
    """A knife and a wooden ladle crossed."""
    c = A.Canvas(N, N)
    body(c, c.line(4, 22, 18, 7, 2), "wd")
    body(c, c.ellipse(20.5, 4.5, 4.5, 3.6), "wd")
    c.paint(c.ellipse(20.5, 4.3, 3, 2.2), "wdD")
    blade = c.triangle((2, 2), (17.5, 11.5), (11.5, 17.5)) | c.triangle((2, 2), (17.5, 11.5), (14, 9))
    body(c, blade, "st")
    c.paint(c.line(4, 4, 15.5, 11, 0.8), "W")
    body(c, c.line(15, 15, 23, 23.5, 3), "wd")
    body(c, c.line(12, 17, 17, 12, 1.6), "ir")
    for x, y in [(18, 18), (21, 21)]:
        c.set(x, y, "st")
    return c


def firewood():
    """A stack of split logs, end grain facing: three, two and one."""
    c = A.Canvas(N, N)
    for cx, cy in [(5, 20), (13, 20), (21, 20), (9, 13), (17, 13), (13, 6)]:
        body(c, c.disc(cx, cy, 4.3), "wd")
        body(c, c.disc(cx, cy, 3), "on", dark=False)
        c.paint(c.disc(cx, cy, 1.7) & ~c.disc(cx, cy, 0.9), "onD")
        c.set(int(cx), int(cy), "onD")
    return c


def scraps():
    """Leftovers: a gnawed bone, a curl of carrot peel and a little fly."""
    c = A.Canvas(N, N)
    body(c, c.line(4, 20, 17, 11, 2.6), "tu")
    for x, y in [(2.8, 19), (4.5, 22), (16, 9.5), (18.5, 11.5)]:
        body(c, c.disc(x, y, 2), "tu")
    c.paint(c.disc(10, 15.5, 1.1), "M")
    t = np.linspace(0, 2.2 * TAU, 40)
    for k, a in enumerate(t):
        x, y = 18 + (1 + 0.6 * a) * np.cos(a), 20 + (1 + 0.6 * a) * 0.6 * np.sin(a)
        c.paint(c.disc(x, y, 0.9), "or" if k % 6 else "orD")
    body(c, leaf(c, 22, 16, 70, 4, 1.2), "gr")
    c.paint(c.ellipse(8, 5, 1.8, 1.2), "E")
    c.paint(c.ellipse(6.5, 3.4, 1.3, 0.9) | c.ellipse(9.5, 3.4, 1.3, 0.9), "S")
    c.set(12, 6, "T")
    c.set(14, 5, "T")
    return c


def preserves():
    """Two glass jars with cloth-tied lids: red berry jam and white salt."""
    c = A.Canvas(N, N)
    for x0, fill, cloth in [(1, "rd", "rd"), (14, "tu", "bl")]:
        jar = c.rect(x0, 9, x0 + 11, 25) | c.ellipse(x0 + 5.5, 24, 5.5, 1.5)
        body(c, jar, "gl")
        body(c, c.rect(x0 + 1, 12, x0 + 10, 24), fill)
        c.paint(c.rect(x0 + 1, 12, x0 + 2, 23), "glL")
        body(c, c.ellipse(x0 + 5.5, 6, 7, 2.8) | c.rect(x0 - 0.5, 6, x0 + 11.5, 9), cloth)
        c.paint(c.rect(x0, 8, x0 + 11, 9), "wdD")
        body(c, c.rect(x0 + 2, 15, x0 + 9, 20), "pl")
    for x in (5, 6, 7):
        c.set(x, 17, "rdD")
    for x in (18, 19, 20):
        c.set(x, 17, "T")
    return c


def food_all():
    """A mix of everything: bread, a carrot, a drumstick and berries."""
    c = A.Canvas(N, N)
    body(c, c.ellipse(16, 8, 9, 6) & (c.py < 11) | c.rect(7, 8, 25, 11), "br")
    for x in (12, 16, 20):
        c.paint(c.line(x, 4.5, x - 2, 8, 1), "gnL")
    for a in (110, 140):
        body(c, leaf(c, 4, 4, a, 5, 1.3), "gr")
    body(c, c.triangle((1.5, 4.5), (7, 3.5), (9, 19)), "or")
    body(c, c.line(17, 18, 22, 13, 2.2), "tu")
    body(c, c.disc(21.8, 12, 1.8) | c.disc(23.5, 14, 1.8), "tu")
    body(c, rell(c, 14, 20, 7, 5, -40), "ck")
    for x, y in [(4, 22), (7, 23), (5.5, 20)]:
        berry(c, x, y, 1.8, "bl")
    return c


def feast():
    """A whole roast boar on a wooden platter: a long snout with a tusk, a floppy ear, a bristly back, an apple in
    its mouth and a curly tail."""
    c = A.Canvas(N, N)
    body(c, c.ellipse(13, 22, 13, 3.8), "wd")
    c.paint(c.ellipse(13, 21.5, 11, 2.2), "wdL")
    for x in (11, 19):                                  # folded legs
        body(c, c.ellipse(x, 19.5, 2.2, 1.6), "ck")
    tail = c.disc(24.2, 12.5, 1.7) & ~c.disc(24.2, 12.5, 0.7)
    c.paint(tail & ~((c.px < 24.2) & (c.py > 12.5)), "ckD")
    for x in (10, 12.5, 15, 17.5):                      # the bristly back
        c.paint(c.triangle((x - 1.5, 11), (x + 1.5, 11), (x - 0.5, 8.3)), "ckD")
    body(c, c.ellipse(15.5, 15, 8, 5), "ck")
    c.paint(rell(c, 15, 12, 5, 0.9, -3), "ckL")
    head = c.ellipse(8, 14.5, 4.4, 4) | rell(c, 4.2, 15.8, 3.6, 2.1, 12)
    c.paint(ring(head) & (c.px < 12), "K")
    body(c, head, "ck")
    body(c, c.ellipse(1.6, 15.3, 1.1, 1.8), "rm", light=False)   # the pig's snout end
    c.set(1, 15, "rmD")
    body(c, c.triangle((7, 11.5), (11, 11.5), (4.5, 7.5)), "ck")   # a floppy ear, tipped forward
    c.paint(c.line(6.5, 10, 9, 11, 0.8), "ckD")
    c.paint(c.line(6.5, 13.2, 8.5, 13.2, 0.8), "E")               # a closed eye
    berry(c, 4, 20, 2.4, "rd")                                    # the apple in its mouth
    c.paint(c.line(4, 18, 5, 16.6, 0.8), "wdD")
    c.set(6, 17, "W")                                             # a tusk
    c.set(6, 16, "W")
    c.g = np.roll(c.g, -2, axis=0)                                # centred on the plate (the top rows are empty)
    return c


# ---- the moving ones -----------------------------------------------------------------------------------------

def flames(c, cx, base, t, k=1.0):
    """A three-tone flame on (cx, base), its tongues swaying and stretching with t (0..1)."""
    s = np.sin(TAU * t)
    s2 = np.sin(TAU * (t + 0.33))
    s3 = np.sin(TAU * (t + 0.66))
    outer = c.ellipse(cx, base - 4 * k, 7 * k, 5 * k) | \
        c.triangle((cx - 6 * k, base - 5 * k), (cx - 3 * k + s2, base - (14 + 2 * s2) * k), (cx, base - 6 * k)) | \
        c.triangle((cx - 4 * k, base - 6 * k), (cx + s, base - (19 + 2 * s) * k), (cx + 4 * k, base - 6 * k)) | \
        c.triangle((cx, base - 6 * k), (cx + 3 * k + s3, base - (13 + 2 * s3) * k), (cx + 6 * k, base - 5 * k))
    mid = c.ellipse(cx, base - 3.5 * k, 5 * k, 3.6 * k) | \
        c.triangle((cx - 3.5 * k, base - 5 * k), (cx + 0.6 * s, base - (13 + 1.5 * s2) * k), (cx + 3.5 * k, base - 5 * k))
    core = c.ellipse(cx, base - 3 * k, 2.8 * k, 2.4 * k) | \
        c.triangle((cx - 2 * k, base - 4 * k), (cx + 0.4 * s3, base - (8 + s) * k), (cx + 2 * k, base - 4 * k))
    c.paint(outer, "Fd")
    c.paint(mid, "F")
    c.paint(core, "hn")
    c.paint(c.ellipse(cx, base - 2.5 * k, 1.4 * k, 1.4 * k), "Y")


def fire(t):
    """Fire and fuel: two crossed logs, flames swaying and stretching, a spark rising."""
    c = A.Canvas(N, N)
    flames(c, 13, 20, t)
    for x0, x1 in [(2, 24), (24, 2)]:
        body(c, c.line(x0, 25, x1, 19, 3.2), "wd")
    for x, y in [(2, 25), (24, 25)]:
        body(c, c.disc(x, y - 1, 1.8), "on", dark=False)
    for x0, p0 in [(8, 0.0), (18, 0.5)]:
        p = (p0 + t) % 1
        y, x = int(round(12 - 11 * p)), int(round(x0 + np.sin(TAU * p)))
        if c.g[y, x] == "":
            c.set(x, y, "Y" if p < 0.5 else "F")
    return c


def wisp(c, x0, y0, p, n=5, key="S"):
    """A rising wavy line of steam, its foot at (x0, y0) moving up with p (0..1), shortening as it goes."""
    m = np.zeros((c.h, c.w), bool)
    y = y0 - 8 * p
    for i in range(max(2, int(round(n * (1 - 0.5 * p))))):
        yy = y - i
        x = x0 + 0.7 * np.sin(TAU * (yy / 5 + p))
        m |= c.disc(x, yy, 0.6)
    return m


def cauldron(t):
    """A black cauldron over small flames, its green stew bubbling: bubbles swell and pop, steam rises."""
    c = A.Canvas(N, N)
    for x, ph in [(6, 0.0), (13, 0.5), (20, 0.25)]:
        flames(c, x, 25, (t + ph) % 1, 0.6)
    pot = c.ellipse(13, 12, 11.5, 7.5) & (c.py > 7)
    body(c, pot, "ir")
    for s in (-1, 1):
        body(c, c.rect(13 + s * 7 - 1, 17, 13 + s * 7 + 1, 21), "ir")
    body(c, c.ellipse(13, 7, 12, 2.6), "ir")
    c.paint(c.ellipse(13, 7, 10, 1.6), "sp")
    c.paint(c.ellipse(10, 6.6, 5, 0.7), "spL")
    c.paint(c.ellipse(13, 11, 9, 0.8) & pot, "irL")
    for x0, p0 in [(8, 0.0), (17.5, 0.5), (13, 0.75)]:   # a bubble swells, pops into a ring, is gone
        p = (p0 + t) % 1
        if p < 0.5:
            r = 1.2 + 1.6 * p / 0.5
            bub = c.ellipse(x0, 7, r, r * 0.9) & (c.py < 7.6)
            c.paint(ring(bub) & (c.py < 7.6), "spD")
            c.paint(bub, "grL")
            c.set(int(x0 - r / 2), int(7 - r * 0.6), "W")
        elif p < 0.75:
            c.paint(c.ellipse(x0, 6.5, 3, 1.2) & ~c.ellipse(x0, 6.5, 2, 0.6), "spL")
    for x0, p0 in [(9, 0.0), (17, 0.5)]:
        c.paint(wisp(c, x0, 5, (p0 + t) % 1, 5) & (c.g == ""), "S" if (p0 + t) % 1 < 0.5 else "T")
    return c


def frying(t):
    """A frying pan with an egg and a sausage: the sausage jiggles, fat jumps, steam rises."""
    c = A.Canvas(N, N)
    body(c, c.line(19, 18, 25.5, 24.5, 2.8), "wd")
    pan = c.ellipse(11.5, 16, 11, 7)
    body(c, pan, "ir")
    c.paint(c.ellipse(11.5, 15.6, 9, 5.2), "irD")
    egg = c.ellipse(7.5, 15.5, 4.8, 3.2) | c.disc(5, 16.5, 2.2)
    body(c, egg, "tu", dark=False)
    body(c, c.disc(7.5, 15, 1.8), "ye", dark=False)
    j = 0.5 * np.sin(TAU * t * 2)
    body(c, rell(c, 15.5, 16.5 + j, 4.6, 2, -20), "sg")
    c.paint(rell(c, 15, 15.6 + j, 2.4, 0.5, -20), "sgL")
    for x0, p0 in [(6, 0.0), (12, 0.33), (18, 0.66)]:          # steam wisps
        p = (p0 + t) % 1
        c.paint(wisp(c, x0, 9, p, 5) & (c.g == ""), "S" if p < 0.6 else "T")
    for x0, p0, s in [(3, 0.1, -1), (21, 0.6, 1), (14, 0.85, 1)]:   # fat drops
        p = (p0 + t) % 1
        y = int(round(10 - 5 * np.sin(np.pi * p)))
        x = int(round(x0 + 3 * p * s))
        if 0 <= y < N and 0 <= x < N and c.g[y, x] == "":
            c.set(x, y, "Y")
    return c


def _anim(fn, n, plate=True):
    return [icon(fn(k / n), plate) for k in range(n)]


def _still(fn, plate=True):
    return [icon(fn(), plate)]


STILLS = [
    ("Kitchen (spices)", spices), ("Kitchen (vegetables)", vegetables), ("Kitchen (fruit and berries)", fruit),
    ("Kitchen (mushrooms)", mushrooms), ("Kitchen (meat, raw)", meat), ("Kitchen (meat, cooked)", cooked),
    ("Kitchen (sausages)", sausages), ("Kitchen (fish)", fish), ("Kitchen (eggs)", eggs),
    ("Kitchen (bread and baking)", bread), ("Kitchen (grain and flour)", grain), ("Kitchen (honey)", honey),
    ("Kitchen (sweets)", sweets), ("Kitchen (soup and stew)", soup), ("Kitchen (drinks)", drinks),
    ("Kitchen (cookware)", cookware), ("Kitchen (knives and utensils)", knives), ("Kitchen (firewood)", firewood),
    ("Kitchen (leftovers)", scraps), ("Kitchen (salt and preserves)", preserves), ("Kitchen (food, all)", food_all),
    ("Kitchen (feast)", feast),
]


def pieces():
    out = [A.Piece(name, "pixel", frames=_still(fn), pitch=PITCH) for name, fn in STILLS]
    out += [
        A.Piece("Kitchen (fire, moving)", "pixel", frames=_anim(fire, 6), fps=6, pitch=PITCH),
        A.Piece("Kitchen (cauldron, moving)", "pixel", frames=_anim(cauldron, 4), fps=5, pitch=PITCH),
        A.Piece("Kitchen (frying pan, moving)", "pixel", frames=_anim(frying, 4), fps=6, pitch=PITCH),
    ]
    return out
