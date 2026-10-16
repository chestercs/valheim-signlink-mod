"""Animals: Valheim's wildlife and tame animals as 8-bit pixel sprites, the cat's family (concepts/anim2.py).

Every sprite uses the cat's pitch (5.6 cm a pixel), so each one stands next to the cat at its true size. Drawn
with artlib.Canvas masks (ellipses, lines, triangles), outlined like the cat; most face left. Heads that turn
(deer, wolf, hen) are drawn in their own frame and rotated about the neck, so a pose is one angle.
"""
import numpy as np

import artlib as A


def finish(c, pal):
    """Canvas -> outlined '#rgb' rows."""
    return A.colours(A.outline(c.g), {**pal, "K": A.OUTLINE})


def rell(c, cx, cy, rx, ry, deg):
    """An ellipse turned by deg (screen degrees, y down)."""
    a = np.radians(deg)
    dx, dy = c.px - cx, c.py - cy
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a)
    return (u / rx) ** 2 + (v / ry) ** 2 <= 1


class Pose:
    """Local coordinates turned by deg and moved to (x, y): draw a part once, place it at any angle."""

    def __init__(self, c, x, y, deg):
        self.c, self.x, self.y, self.a = c, x, y, np.radians(deg)

    def p(self, u, v):
        ca, sa = np.cos(self.a), np.sin(self.a)
        return self.x + u * ca - v * sa, self.y + u * sa + v * ca

    def ell(self, u, v, rx, ry, deg=0):
        x, y = self.p(u, v)
        return rell(self.c, x, y, rx, ry, np.degrees(self.a) + deg)

    def line(self, u0, v0, u1, v1, w=1.0):
        return self.c.line(*self.p(u0, v0), *self.p(u1, v1), w)

    def tri(self, a, b, d):
        return self.c.triangle(self.p(*a), self.p(*b), self.p(*d))

    def dot(self, u, v):
        x, y = self.p(u, v)
        return self.c.rect(int(np.floor(x)), int(np.floor(y)), int(np.floor(x)) + 1, int(np.floor(y)) + 1)


# ---- boar ------------------------------------------------------------------------------------------------------

BOAR = {"B": "#a75", "L": "#c97", "D": "#532", "S": "#d98", "T": "#ffe", "P": "#111", "G": "#863", "F": "#432"}


def boar(dy, dirt):
    """A Meadows boar facing left: high bristly shoulders, a wedge head carried low with a long snout and a tusk;
    the head drops by dy to snuffle, dirt flying from the snout when dirt > 0."""
    c = A.Canvas(34, 22)
    for x0, x1 in ((11, 10.5), (14, 14), (24, 24.5), (27, 27.5)):   # thin legs, hooves dark
        c.paint(c.line(x0, 13, x1, 20, 2), "F" if x0 in (14, 27) else "B")
        c.paint(c.rect(int(x1) - 1, 20, int(x1) + 1, 22), "F")
    c.paint(c.ellipse(20, 12, 10, 5.2) | c.ellipse(15, 9.5, 6.5, 4.8), "B")    # body, high shoulders
    c.paint(c.ellipse(19, 14.6, 7, 2.6), "L")                       # round pale belly
    for x in range(9, 28):                                          # the mane: tall at the shoulders, sloping down
        col = np.nonzero(c.g[:, x] != "")[0]
        top = col.min() if len(col) else 12
        h = 3 if x < 17 else (2 if x < 23 else 1)
        c.paint(c.rect(x, top - (h if x % 2 else h - 1), x + 1, top + 1), "D")
    c.paint(c.line(30, 10, 32, 12.5, 1.1), "D")                     # small tail with a tuft
    c.set(32, 13, "F")
    head = c.triangle((13, 6.5), (13, 16 + dy * 0.5), (1.5, 14 + dy)) | c.ellipse(9.5, 11 + dy * 0.4, 4, 4.2)
    c.paint(head, "B")
    c.paint(c.rect(0, int(13 + dy), 3, int(16 + dy)), "S")        # snout
    c.set(0, int(13 + dy), "P")
    c.paint(c.line(4, 16 + dy, 5.5, 12.5 + dy, 1.2) | c.line(3.8, 16 + dy, 4.6, 16 + dy, 1), "T")   # tusk
    c.paint(c.triangle((10, 7.5 + dy * 0.3), (12.5, 3.5 + dy * 0.3), (13.5, 8 + dy * 0.3)), "D")    # ear
    c.set(8, int(10 + dy * 0.6), "P")
    if dirt:
        for x, y in ((0, 19), (3, 20), (1, 17) if dirt == 1 else (2, 18), (5, 21)):
            c.set(x, y - dirt + 1, "G")
    return finish(c, BOAR)


def piglet(up, tuck):
    """A striped boar piglet facing left: round body, round head, pink snout, curly tail; up px above the
    ground, legs tucked in the air."""
    c = A.Canvas(18, 16)
    by = 10 - up
    leg = 1 if tuck else 2
    for x0 in (6, 8, 12, 14):
        c.paint(c.rect(x0, int(by) + 3, x0 + 1, int(by) + 3 + leg), "F")
    body = c.ellipse(10.5, by, 5.5, 4)
    c.paint(body, "B")
    for y in (by - 2, by + 0.5):                                    # pale stripes
        c.paint(body & c.rect(7, int(y), 17, int(y) + 1), "S")
    c.paint(c.disc(5, by - 1, 3.4), "B")
    c.paint(c.rect(1, int(by) - 1, 3, int(by) + 1), "N")
    c.set(1, int(by) - 1, "D")
    c.set(4, int(by) - 3, "P")
    c.paint(c.triangle((5, by - 3.5), (7.5, by - 6), (8, by - 3)), "D")
    for x, y in ((16, -2), (17, -3), (16, -4), (15, -3)):            # curly tail
        c.set(x, int(by) + y, "N")
    return finish(c, {"B": "#a64", "S": "#dc8", "N": "#e9a", "P": "#111", "D": "#532", "F": "#432"})


# ---- deer ------------------------------------------------------------------------------------------------------

DEER = {"B": "#b74", "D": "#853", "W": "#edb", "A": "#dc9", "P": "#111", "H": "#432", "G": "#5a3", "E": "#c97"}


def deer(s, ear):
    """A Meadows stag facing left; s = 0 head up, 1 grazing; ear flicks the ear back."""
    c = A.Canvas(36, 36)
    for x0, x1 in ((14, 13), (17, 16), (26, 27), (29, 30)):
        c.paint(c.line(x0, 21, x1, 34, 2), "D" if x0 in (17, 29) else "B")
        c.paint(c.rect(x1 - 1, 34, x1 + 1, 36), "H")
    c.paint(c.ellipse(22, 19, 10, 5.4), "B")
    c.paint(c.ellipse(22, 22.3, 7, 2), "W")
    c.paint(c.ellipse(32, 16.5, 1.8, 2), "W")                       # white tail
    for x, y in ((6, 35), (7, 34), (9, 33), (10, 35), (11, 34)):    # grass
        c.paint(c.rect(x, y, x + 1, 36), "G")
    jx, jy = 11 + 1 * s, 9 + 17 * s
    c.paint(c.line(16, 17, jx, jy, 4.4), "B")
    h = Pose(c, jx, jy, -82 * s)
    c.paint(h.ell(-2.6, 0.2, 3.4, 2.4) | h.ell(-5.4, 1, 2.2, 1.5), "B")
    c.paint(h.dot(-7.3, 0.6), "P")
    c.paint(h.dot(-2.6, -0.9), "P")
    c.paint(h.ell(-5.6, 2.2, 1.6, 0.6), "W")
    for seg in ((-1, -2, -1.5, -8), (-1.3, -5, -4, -7), (-1.5, -8, -3.5, -10.5), (-1.5, -8, 1, -10.5),
                (0, -2, 1.5, -7), (1.5, -7, 3.5, -9), (1, -5, 3.5, -5.5)):
        c.paint(h.line(*seg, 1.1), "A")
    if ear:
        c.paint(h.line(0.5, -0.8, 4, 0.5, 1.6), "E")
    else:
        c.paint(h.line(0.5, -1, 3.2, -3.4, 1.6), "E")
    return finish(c, DEER)


def fawn(ear, tail):
    """A spotted deer fawn facing left, big ears and eyes; ear twitches an ear, tail flicks the white tail up."""
    c = A.Canvas(22, 22)
    for x0, x1 in ((7, 6.5), (9, 9), (15, 15.5), (17, 17.5)):
        c.paint(c.line(x0, 13, x1, 20, 1.4), "D" if x0 in (9, 17) else "B")
        c.paint(c.rect(int(x1) - 1, 20, int(x1) + 1, 22) & (c.px > x1 - 1), "H")
    c.paint(c.ellipse(12.5, 12, 6.5, 3.6), "B")
    c.paint(c.ellipse(12.5, 14.2, 4.5, 1.2), "W")
    for x, y in ((10, 10), (13, 9), (16, 10), (11, 12), (14, 11), (17, 12)):    # spots
        c.set(x, y, "W")
    c.paint(c.line(19, 10, 20.5, 8 if tail else 10.5, 1.6), "W")
    c.paint(c.line(8, 11, 6, 6, 3.2), "B")
    c.paint(c.ellipse(4.6, 4.8, 2.8, 2.3) | c.ellipse(2.4, 5.8, 1.6, 1.2), "B")
    c.paint(c.line(6.5, 3.5, 9.5, 0.8 if ear else 2.5, 1.7), "E")
    c.paint(c.line(5.2, 3.2, 6.2, 0.5, 1.5), "E")
    c.set(1, 5, "P")
    c.paint(c.rect(4, 4, 5, 6), "P")
    c.set(4, 4, "W")
    return finish(c, DEER)


# ---- neck ------------------------------------------------------------------------------------------------------

NECK = {"B": "#7a5", "D": "#463", "S": "#563", "L": "#cc8", "F": "#b63", "E": "#fd3", "M": "#311", "W": "#eed",
        "R": "#563", "T": "#e46"}
NECK_BLUE = {**NECK, "B": "#7ac", "D": "#468", "S": "#57a", "L": "#dee", "F": "#36a"}       # one star
NECK_PURPLE = {**NECK, "B": "#a8c", "D": "#647", "S": "#759", "L": "#ede", "F": "#839"}     # two stars
NECK_BOX = (41, 17)                                                # w, h of neck_body at s=1


def _bezier(p0, p1, p2, n):
    t = np.linspace(0, 1, n)[:, None]
    return (1 - t) ** 2 * np.array(p0, float) + 2 * (1 - t) * t * np.array(p1, float) + t ** 2 * np.array(p2, float)


def neck_body(c, ox=0, oy=0, s=1.0, head=0, reach=0, jaw=0, eyes="open", tongue=0, step=0, tail=0.0, curl=0,
              fin=1, breath=0.0, legs=True):
    """A Neck drawn into canvas c, its NECK_BOX box's top left at (ox, oy) (s scales it), facing left, the feet
    on the box's last row. head raises the head (px), reach pushes it forward (px, < 0 pulls it back); jaw opens the
    mouth (px); eyes open, up or closed; tongue: px of forked tongue out of the mouth; step -1..1 moves the legs;
    tail lifts its tip and curl draws it in (px); fin 0..2: how high the crest's spines stand; breath swells the body
    (px); legs=False tucks the legs under (lying down).
    Keys: B skin, D far legs, S spots, L belly, R the crest's ridge, F its spines, E eyes, M mouth, W teeth,
    T tongue."""
    oy += 0 if legs else 2.8 * s                                    # lying: the belly on the ground

    def pt(x, y):
        return ox + x * s, oy + y * s

    def ell(x, y, rx, ry):
        return c.ellipse(*pt(x, y), rx * s, ry * s)

    def line(x0, y0, x1, y1, w):
        return c.line(*pt(x0, y0), *pt(x1, y1), w * s)

    def chain(p0, p1, p2, w0, w1, n=10):
        m = np.zeros(c.g.shape, bool)
        for (x, y), w in zip(_bezier(p0, p1, p2, n), np.linspace(w0, w1, n)):
            m |= c.disc(*pt(x, y), w * s / 2)
        return m

    def leg(x, d, hind):
        """Shoulder or hip at x: the elbow sticks out, the foot's toes point forward."""
        kx = x + d + (1.4 if hind else -1.2)
        return (line(x, 11.5, kx, 14.6, 2.2) | line(kx, 14.6, kx - 0.5, 16.2, 1.5)
                | line(kx - 2.8, 16.5, kx + 0.4, 16.5, 1.0))

    hx, hy = 5.5 - reach, 3.4 - head                                # the head's centre
    if legs:
        for x, d, hind in ((17.5, step, False), (26.5, -step, True)):  # far legs
            c.paint(leg(x, d, hind), "D")
    skin = chain((25, 11.2), (33, 14.6), (40.5 - curl, 12.6 - tail), 3.6, 1.0, 16)    # the long tapering tail
    skin |= ell(19.5, 11 - breath / 2, 8, 3.1 + breath / 2)                                 # the low body
    neck_m = chain((14.5, 10.4), (12.6, 5.4 - head * 0.5), (hx + 2.6, hy + 0.4), 3.4, 2.4)
    skin |= neck_m                                                  # the long neck
    skin |= ell(hx + 0.4, hy, 3.4, 1.9) | ell(hx - 2.5, hy + 0.5, 2.3, 1.3)               # flat head, snout
    c.paint(skin, "B")
    c.paint(skin & ell(19.5, 13.6, 7, 1.3), "L")                    # the pale belly
    c.paint(neck_m & ~np.roll(skin, 1, axis=1) & ~c.rect(0, 0, c.w, int(round(oy + (hy + 1.6) * s))), "L")   # throat
    for x, y in ((17, 9.2), (21, 9.4), (24.5, 10), (19, 11.2), (13, 8.6), (28, 11.6), (31.5, 12.8)):
        c.set(int(pt(x, y)[0]), int(pt(x, y)[1]), "S")
    if legs:
        for x, d, hind in ((15, step, False), (24, -step, True)):    # near legs
            c.paint(leg(x, d, hind), "B")
    x0, x1 = int(pt(hx + 1.6, 0)[0]), int(pt(30, 0)[0])            # the crest: spines on the neck, back, tail root
    for x in range(x0, x1):
        ys = np.nonzero(skin[:, x])[0]
        if not len(ys):
            continue
        top = ys.min()
        c.g[top, x] = "R"
        if (x - x0) % 2 == 0:
            for k in range(1, fin + 1):
                if top - k >= 0:
                    c.g[top - k, x] = "F"
    my = int(round(oy + (hy + 0.9) * s))                            # the mouth's row
    mx0, mx1 = int(round(pt(hx - 4.4, 0)[0])), int(round(pt(hx + 1.8, 0)[0]))
    if jaw:
        c.paint(c.line(mx1, my + 0.5, mx0 - 0.2, my + jaw + 1.5, 1.6), "B")                # the lower jaw
        c.paint(c.triangle((mx1, my), (mx0 - 0.5, my), (mx0 - 0.5, my + jaw + 0.9)), "M")
        for x in (mx0 + 1, mx0 + 3):
            c.set(x, my, "W")
        c.set(mx0, my + jaw, "W")
    else:
        c.paint(c.rect(mx0, my, mx1, my + 1), "M")
        for x in range(mx0 + 1, mx1 - 1, 2):
            c.set(x, my, "W")
    ex, ey = int(round(pt(hx + 0.3, 0)[0])), int(round(oy + (hy - 0.8) * s))
    for x in (ex - 1, ex + 1):                                      # two beady eyes on this side
        c.set(x, ey, "S" if eyes == "closed" else "E")
    if eyes == "up":
        c.set(ex - 1, ey, "B").set(ex - 1, ey - 1, "E").set(ex + 1, ey - 1, "E").set(ex + 1, ey, "B")
    if tongue:
        c.paint(c.rect(mx0 - tongue, my, mx0, my + 1), "T")
        c.set(mx0 - tongue - 1, my - 1, "T").set(mx0 - tongue - 1, my + 1, "T")


def neck(tongue, blink, pal=None, **kw):
    """A Neck facing left (neck_body), its tongue out by tongue px, the eyes shut when blink; kw go to neck_body."""
    c = A.Canvas(NECK_BOX[0] + 7, NECK_BOX[1] + 3)
    neck_body(c, 6, 2, tongue=tongue, eyes="closed" if blink else "open", **kw)
    return finish(c, pal or NECK)


# ---- lox -------------------------------------------------------------------------------------------------------

LOX = {"B": "#754", "F": "#532", "M": "#432", "H": "#987", "T": "#edc", "P": "#111", "N": "#322", "G": "#6a3",
       "J": "#876"}


def lox(jaw, tail):
    """A Plains lox facing left, a shaggy giant with curling tusks, chewing (jaw 0..2 px open)."""
    rng = np.random.default_rng(7)
    c = A.Canvas(60, 46)
    for x0, sh in ((17, "N"), (25, "M"), (40, "N"), (48, "M")):
        c.paint(c.rect(x0, 30, x0 + 6, 44), sh if sh == "M" else "B")
        c.paint(c.rect(x0, 44, x0 + 6, 46), "N")
    c.paint(c.ellipse(36, 20, 21, 13), "B")
    c.paint(c.ellipse(30, 12, 13, 8), "F")                          # the shoulder hump
    for x in range(14, 58):                                         # the long fur hanging from the belly
        bot = 20 + 13 * np.sqrt(max(0.0, 1 - ((x + 0.5 - 36) / 21) ** 2))
        c.paint(c.rect(x, int(bot) - 4, x + 1, int(bot) + int(rng.integers(1, 4))), "F" if x % 3 else "M")
    for k in range(9):                                              # fur strands on the flank
        x, y = 22 + 4 * k, 16 + (k * 5) % 9
        c.paint(c.line(x, y, x + 1, y + 4, 1), "M")
    tx = 56 + tail
    c.paint(c.line(56, 15, tx, 26, 1.6), "F")
    c.paint(c.rect(int(tx) - 1, 25, int(tx) + 1, 29), "M")
    c.paint(c.ellipse(13, 26, 8.5, 7.5), "H")                       # head, carried low
    c.paint(c.ellipse(17, 19, 7.5, 5), "F")                         # mane over the brow
    c.paint(c.ellipse(5.5, 29, 4.5, 4), "J")                        # muzzle
    c.paint(c.rect(1, 27, 3, 29), "N")                              # nostril
    c.paint(c.ellipse(6, 33.5 + jaw, 3.6, 1.6), "J")                 # lower lip, chewing
    c.paint(c.rect(2, 32, 9, 33 + jaw), "N")
    if jaw:
        c.paint(c.rect(1, 31 + jaw, 4, 32 + jaw), "G")
    for k in range(14):                                             # a horn curling round the eye like a ram's
        t = k / 13
        ang = np.radians(-40 - 250 * t)
        r = 6.5 - 2.5 * t
        c.paint(c.disc(14 + r * np.cos(ang), 24 + r * np.sin(ang), 1.9 - 0.9 * t), "T")
    c.paint(c.rect(12, 23, 14, 25), "P")
    c.set(12, 23, "T")
    return finish(c, LOX)


# ---- wolf ------------------------------------------------------------------------------------------------------

WOLF = {"B": "#bbc", "W": "#eef", "D": "#778", "P": "#111", "Y": "#fd4", "M": "#622", "S": "#cef"}


def wolf(a, mouth, waves):
    """A Mountain wolf sitting, facing left; its head a degrees up (55 = howling), mouth open, sound waves."""
    c = A.Canvas(32, 38)
    c.yy, c.py = c.yy - 4, c.py - 4                                  # 4 rows of room for the howl on top
    c.paint(c.line(22, 31.5, 29, 29, 4) | c.ellipse(29.5, 27.5, 2.2, 3), "D")   # bushy tail
    c.paint(c.ellipse(30, 26, 1.6, 1.8), "W")
    c.paint(c.ellipse(21, 28, 6.5, 5.5), "B")                       # haunch
    c.paint(rell(c, 17.5, 23.5, 5, 8, -20), "B")                    # torso, leaning back
    c.paint(c.line(13.5, 24, 13, 33, 2.6) | c.line(16.5, 25, 16.5, 33, 2.6), "B")
    c.paint(c.rect(11, 32, 15, 34) | c.rect(15, 32, 19, 34), "W")
    c.paint(rell(c, 14, 21.5, 2.6, 5, -15), "W")                     # chest ruff
    c.paint(c.rect(21, 25, 23, 26) | c.rect(24, 28, 26, 29), "D")
    jx, jy = 15, 13
    c.paint(c.line(18, 19, jx + 1, jy + 1, 5.5), "B")
    h = Pose(c, jx, jy, a)
    c.paint(h.tri((-0.5, -3), (3.6, -2.5), (2.6, -8.5)), "B")       # ear
    c.paint(h.line(1.6, -3.5, 2.2, -6, 1), "D")
    c.paint(h.ell(-0.5, 0, 4.6, 4) | h.line(-3, 0.4, -10, 0.8, 3.4), "B")
    c.paint(h.ell(-1, 2.8, 3.2, 1.4), "W")                           # pale cheek
    if mouth:
        c.paint(h.line(-3, 3, -9, 4.4, 2), "W")                     # lower jaw, dropped
        c.paint(h.tri((-3.5, 2), (-10, 2.4), (-9, 3.6)), "M")
    else:
        c.paint(h.line(-4, 2, -9.5, 2.2, 1), "W")
    c.paint(h.dot(-10.6, 0.2), "P")
    c.paint(h.dot(-3.4, -1.6) | h.dot(-2.4, -1.6), "Y")
    if waves:                                                       # the howl: arcs ahead of the muzzle
        ox, oy = h.p(-11, 1.5)
        d = np.arctan2(-np.sin(np.radians(a)), -np.cos(np.radians(a)))
        ang = np.arctan2(c.py - oy, c.px - ox)
        cone = np.abs((ang - d + np.pi) % (2 * np.pi) - np.pi) < np.radians(40)
        rr = np.hypot(c.px - ox, c.py - oy)
        for r in ((2.5, 5) if waves == 2 else (2.5,)):
            c.paint(cone & (np.abs(rr - r) < 0.6) & (c.g == ""), "S")
    return finish(c, WOLF)


# ---- hen and chick ---------------------------------------------------------------------------------------------

HEN = {"B": "#d96", "D": "#a53", "T": "#742", "R": "#e33", "Y": "#fc3", "O": "#f93", "P": "#111", "G": "#ec6"}


def hen(s):
    """A hen facing left; s = 0 upright, 1 beak on the ground."""
    c = A.Canvas(18, 17)
    c.paint(c.line(9, 12, 8.5, 16, 1) | c.line(11, 12, 11.5, 16, 1), "O")
    c.paint(c.rect(7, 16, 10, 17) | c.rect(11, 16, 13, 17), "O")
    c.paint(c.triangle((12, 9), (17.5, 2.5), (16, 10)), "T")       # tail
    c.paint(c.ellipse(10.5, 9.5, 6, 4), "B")
    c.paint(c.ellipse(12, 9, 3.5, 2.2), "D")                         # wing
    c.set(1, 15, "G")
    c.set(3, 16, "G")
    c.set(0, 16, "G")
    hx, hy = 6 - 2 * s, 5 + 8 * s
    c.paint(c.line(7.5, 8, hx + 0.5, hy, 3), "B")
    h = Pose(c, hx, hy, -70 * s)
    c.paint(h.ell(0, 0, 2.4, 2.2), "B")
    c.paint(h.tri((-2, 0), (-4.2, 0.6), (-2, 1.4)), "Y")
    c.paint(h.ell(0, -2.4, 1.6, 0.9), "R")                           # comb
    c.paint(h.ell(-1.6, 2, 0.6, 0.9), "R")                           # wattle
    c.paint(h.dot(-0.6, -0.7), "P")
    return finish(c, HEN)


CHICK = {"Y": "#fe5", "W": "#ec3", "O": "#f93", "P": "#111", "M": "#c52"}


def chick(flap, peep):
    """A chick facing left; flap lifts its little wing, peep opens the beak."""
    c = A.Canvas(11, 11)
    c.paint(c.rect(4, 9, 5, 11) | c.rect(6, 9, 7, 11), "O")
    c.paint(c.ellipse(5.5, 6.8, 3.6, 3), "Y")
    c.paint(c.disc(3.8, 3.6, 2.4), "Y")
    if flap:
        c.paint(c.line(6.5, 6, 9.5, 2.5, 1.6), "W")
    else:
        c.paint(c.ellipse(6.6, 7, 2, 1.3), "W")
    if peep:
        c.paint(c.rect(0, 3, 2, 4) | c.rect(0, 5, 2, 6), "O")
        c.set(1, 4, "M")
    else:
        c.paint(c.rect(0, 4, 2, 5), "O")
    c.set(3, 3, "P")
    return finish(c, CHICK)


# ---- birds -----------------------------------------------------------------------------------------------------

GULL = {"W": "#fff", "G": "#bbc", "D": "#99a", "K2": "#334", "Y": "#fc3", "P": "#111"}


def gull(tip, bob):
    """A seagull flying left; tip is the near wing tip (dx, dy) from the shoulder."""
    c = A.Canvas(28, 22)
    y = 11 + bob
    c.paint(c.triangle((19, y - 0.5), (25, y - 1.5), (25, y + 1.5)), "G")      # tail
    c.paint(c.ellipse(14, y, 6.5, 2.6), "W")
    c.paint(c.disc(7.5, y - 1, 2.4), "W")
    c.paint(c.triangle((5.5, y - 1.5), (2, y - 0.5), (5.5, y + 0.2)), "Y")
    c.set(7, int(y) - 2, "P")
    ex, ey = 14 + tip[0] * 0.5, y - 0.5 + tip[1] * 0.5               # the elbow, halfway
    tx, ty = 14 + tip[0], y - 0.5 + tip[1]
    c.paint(c.triangle((10.5, y - 0.5), (18, y - 0.5), (ex + 1.5, ey)) | c.line(14, y - 0.5, ex, ey, 3.4), "G")
    c.paint(c.triangle((14, y - 0.5), (18.5, y - 0.5), (tx + 1, ty)), "G")
    c.paint(c.line(ex, ey, tx, ty, 2.4), "G")
    c.paint(c.line(ex + 0.3 * (tx - ex), ey + 0.3 * (ty - ey), ex + 0.6 * (tx - ex), ey + 0.6 * (ty - ey), 2.4)
            & ~c.line(14, y - 0.5, ex, ey, 3.4), "D")
    c.paint(c.line(ex + 0.65 * (tx - ex), ey + 0.65 * (ty - ey), tx, ty, 2.4), "K2")
    return finish(c, GULL)


CROW = {"B": "#335", "S": "#558", "N": "#223", "Bk": "#556", "P": "#eee", "R": "#c33", "Wt": "#ddd"}


def crow(up, crouch, caw):
    """A crow facing left; up px off the ground, crouched, or cawing (head up, beak open)."""
    c = A.Canvas(24, 22)
    c.xx, c.px = c.xx - 4, c.px - 4                                  # 4 columns of room for the caw on the left
    gy = 21
    by = gy - 6.5 - up + crouch
    leg = 0 if up else (2 if crouch else 3)
    if up:
        c.paint(c.line(10, by + 3, 11.5, by + 4.5, 1), "N")
    else:
        c.paint(c.rect(9, int(by) + 3, 10, gy) | c.rect(11, int(by) + 3, 12, gy), "N")
        c.paint(c.rect(7, gy - 1, 10, gy) | c.rect(11, gy - 1, 13, gy), "N")
    c.paint(c.triangle((13, by - 1), (19.5, by + 3.5), (17.5, by + 4.5)), "N")  # tail
    c.paint(rell(c, 10.5, by, 5.5, 3.6, 20), "B")
    c.paint(rell(c, 11.5, by - 0.5, 4, 2.2, 20), "S")                # wing sheen
    hx, hy = (6, by - 4.5) if caw else (6.5, by - 3)
    c.paint(c.disc(hx, hy, 2.6), "B")
    c.paint(c.rect(int(hx), int(hy) - 1, int(hx) + 1, int(hy)), "P")
    if caw:
        c.paint(c.triangle((hx - 1.5, hy - 1), (hx - 5.5, hy - 3.5), (hx - 1.5, hy)), "Bk")
        c.paint(c.triangle((hx - 1.5, hy + 0.5), (hx - 5, hy + 1.5), (hx - 1.5, hy + 1.5)), "Bk")
        for deg in (-35, 0, 35):                                    # the caw
            a = np.radians(deg)
            c.paint(c.line(hx - 6.5 - 1.5 * np.cos(a), hy - 1.5 + 2.5 * np.sin(a),
                           hx - 6.5 - 3 * np.cos(a), hy - 1.5 + 4 * np.sin(a), 1.1), "Wt")
    else:
        c.paint(c.triangle((hx - 1.5, hy - 0.8), (hx - 5, hy + 0.8), (hx - 1.5, hy + 1.4)), "Bk")
    return finish(c, CROW)


# ---- fish ------------------------------------------------------------------------------------------------------

FISH = {"S": "#bcd", "H": "#eef", "D": "#578", "R": "#e86", "P": "#111", "Wa": "#39d", "L": "#8cf", "Wt": "#fff"}


def fish(t, splash=0, ring=0, at=28):
    """A fish leaping over a pond, left to right: t in 0..1 along the arc (None: under water); a splash of drops
    (1, 2) or a spreading ring (1, 2) on the water at x = at."""
    c = A.Canvas(40, 34)
    wy = 28
    if t is not None:
        x = 12 + 16 * t
        y = wy - 15 * 4 * t * (1 - t)
        ang = np.degrees(np.arctan2(-15 * 4 * (1 - 2 * t), 16))
        f = Pose(c, x, y, ang)
        c.paint(f.tri((-6.5, 0), (-11.5, -4), (-11.5, 4)) & ~f.tri((-9.5, 0), (-12, -2), (-12, 2)), "R")  # tail fin
        c.paint(f.tri((-2.5, -2.5), (3, -2.8), (-3.5, -6)), "R")      # fin on top
        c.paint(f.ell(0, 0, 8, 3.5), "S")
        c.paint(f.ell(0.5, -1.6, 6.5, 1.6), "D")                       # dark back
        c.paint(f.line(-4, 0.6, 4, 0.4, 1), "H")                       # sheen
        c.paint(f.tri((-0.5, 1.5), (-4, 4.5), (-2.5, 1.5)), "R")      # belly fin
        c.paint(f.dot(5.3, -0.6), "P")
    c.g[c.py > wy] = ""                                             # under the surface: hidden
    c.paint(c.ellipse(20, wy + 2.5, 13, 3.4) & (c.py > wy), "Wa")
    c.paint(c.ellipse(20, wy + 1.5, 9, 0.9), "L")
    if ring:
        rx, ry = 3 + 2 * ring, 1.6 + 0.4 * ring
        c.paint(c.ellipse(at, wy + 2, rx, ry) & ~c.ellipse(at, wy + 2, rx - 1.5, ry - 1), "Wt")
    if splash:
        for dx, dy in ((-3, -2 - splash), (-1, -4 - 2 * splash), (1, -5 - 2 * splash), (3, -2 - splash),
                       (-5, -1), (5, -1), (0, -2 - splash)):
            c.paint(c.rect(at + dx, wy + dy, at + dx + 1, wy + dy + 2 if dy < -3 else wy + dy + 1), "Wt")
    return finish(c, FISH)


# ---- bees ------------------------------------------------------------------------------------------------------

HIVE = {"S": "#db5", "D": "#a72", "E": "#321", "W": "#864", "Y": "#fe2", "Bk": "#222", "Wt": "#eef"}


def beehive(t):
    """A straw skep on a stand with three bees circling it; t in 0..1 is one lap."""
    c = A.Canvas(30, 30)
    c.paint(c.rect(8, 26, 22, 28), "W")
    c.paint(c.rect(9, 28, 11, 30) | c.rect(19, 28, 21, 30), "W")
    dome = c.ellipse(15, 17, 8.5, 10) & (c.py < 26)
    c.paint(dome, "S")
    for y in range(10, 26, 3):                                      # coils of straw
        c.paint(dome & c.rect(0, y, 30, y + 1), "D")
    c.paint(c.ellipse(15, 25, 2, 1.6) & (c.py < 26), "E")
    for k, (rx, ry, cy) in enumerate(((13, 5, 13), (12, 6, 16), (10, 7, 8))):
        a = 2 * np.pi * (t + k / 3) * (1 if k != 1 else -1)
        x, y = int(round(15 + rx * np.cos(a))), int(round(cy + ry * np.sin(a)))
        x, y = min(max(x, 1), 27), min(max(y, 1), 28)
        c.set(x, y, "Y")
        c.set(x + 1, y, "Bk")
        c.set(x, y - 1, "Wt")
    return finish(c, HIVE)


# ---- pieces ----------------------------------------------------------------------------------------------------

def pieces():
    return [
        A.Piece("Boar (moving, snuffling)",
                frames=[boar(0, 0), boar(1, 0), boar(2, 1), boar(2, 2), boar(1, 0)], kind="pixel", fps=4),
        A.Piece("Piglet (moving, hopping)",
                frames=[piglet(0, False), piglet(2, True), piglet(4, True), piglet(2, True)], kind="pixel", fps=6),
        A.Piece("Deer (moving, grazing)",
                frames=[deer(0, False), deer(0, True), deer(0.5, False), deer(1, False), deer(1, False),
                        deer(1, True), deer(0.5, False), deer(0, False)], kind="pixel", fps=4),
        A.Piece("Fawn (moving, ears and tail)",
                frames=[fawn(False, False), fawn(True, False), fawn(False, False), fawn(False, True),
                        fawn(False, False), fawn(False, False)], kind="pixel", fps=4),
        A.Piece("Neck (moving, tongue and blink)",
                frames=[neck(0, False), neck(2, False), neck(4, False), neck(2, False), neck(0, False),
                        neck(0, True)], kind="pixel", fps=5),
        A.Piece("Lox (moving, chewing)",
                frames=[lox(0, 0), lox(1, 1), lox(2, 1), lox(1, 0)], kind="pixel", fps=3),   # 3: fits the play budget (0.20.167)
        A.Piece("Wolf (moving, howling)",
                frames=[wolf(0, False, 0), wolf(0, False, 0), wolf(25, False, 0), wolf(55, True, 1),
                        wolf(55, True, 2), wolf(55, True, 2), wolf(55, True, 1), wolf(25, False, 0)],
                kind="pixel", fps=4),
        A.Piece("Hen (moving, pecking)",
                frames=[hen(0), hen(0.5), hen(1), hen(0.5), hen(0)], kind="pixel", fps=5),
        A.Piece("Chick (moving, peeping)",
                frames=[chick(False, False), chick(True, True), chick(False, True), chick(True, False)],
                kind="pixel", fps=6),
        A.Piece("Seagull (moving, flying)",
                frames=[gull((5, -6), 0), gull((5, -1), 1), gull((3, 8), 2), gull((5, 2), 1), gull((5, -5), 0),
                        gull((3, -10), 0)], kind="pixel", fps=6),
        A.Piece("Crow (moving, hopping and cawing)",
                frames=[crow(0, 0, False), crow(0, 1, False), crow(3, 0, False), crow(0, 1, False),
                        crow(0, 0, True), crow(0, 0, True)], kind="pixel", fps=5),
        A.Piece("Fish (moving, jumping)",
                frames=[fish(0.5), fish(0.68), fish(0.84), fish(0.98, splash=1), fish(None, splash=2),
                        fish(None, ring=1), fish(None, ring=2), fish(0.03, splash=1, at=12), fish(0.17), fish(0.33)],
                kind="pixel", fps=6),
        A.Piece("Beehive (moving, bees)", frames=[beehive(k / 6) for k in range(6)], kind="pixel", fps=6),
    ]
