"""Wild: the big land animals as 8-bit pixel sprites, the cat's family (concepts/anim2.py): a bull moose, rabbits,
a brown bear, a pack of Mountain wolves and a giraffe.

The cat's pitch (5.6 cm a pixel) wherever the animal fits the 64 px limit at it, so it stands next to the cat at its
true size; the giraffe is five metres tall and takes a coarser pitch (GIRAFFE_PITCH) to keep its true size. A
creature is drawn on its own Canvas, facing left, outlined in the cat's dark outline and pasted into its scene, so
the one in front keeps its rim. Every piece moves; the first frame is a good still for players without the mod.
"""
import numpy as np

import artlib as A
from concepts.lib_animals import rell

GIRAFFE_PITCH = 3.6             # 9 cm a pixel: 64 px are 5.8 m


class Part:
    """Local coordinates turned by deg (screen degrees, + turns a left-facing part up), scaled by k and moved to
    (x, y): a body part drawn once and placed at any angle and size."""

    def __init__(self, c, x=0.0, y=0.0, deg=0.0, k=1.0):
        self.c, self.x, self.y, self.deg, self.k = c, x, y, deg, k
        self.a = np.radians(deg)

    def p(self, u, v):
        ca, sa = np.cos(self.a), np.sin(self.a)
        return self.x + self.k * (u * ca - v * sa), self.y + self.k * (u * sa + v * ca)

    def at(self, u, v, deg=0.0):
        """A child part at (u, v), turned deg more."""
        return Part(self.c, *self.p(u, v), self.deg + deg, self.k)

    def ell(self, u, v, rx, ry, deg=0.0):
        return rell(self.c, *self.p(u, v), self.k * rx, self.k * ry, self.deg + deg)

    def disc(self, u, v, r):
        return self.ell(u, v, r, r)

    def line(self, u0, v0, u1, v1, w=1.0):
        return self.c.line(*self.p(u0, v0), *self.p(u1, v1), w * self.k)

    def tri(self, a, b, d):
        return self.c.triangle(self.p(*a), self.p(*b), self.p(*d))

    def dot(self, u, v):
        x, y = self.p(u, v)
        x, y = int(np.floor(x)), int(np.floor(y))
        return self.c.rect(x, y, x + 1, y + 1)


def _grid(w, h):
    return np.full((h, w), "", dtype=object)


def _paste(dst, src, x, y):
    """Non-empty pixels of src over dst, src's top left at (x, y); what falls outside is dropped."""
    src = np.asarray(src, dtype=object)
    h, w = src.shape
    H, W = dst.shape
    x0, y0, x1, y1 = max(x, 0), max(y, 0), min(x + w, W), min(y + h, H)
    if x0 < x1 and y0 < y1:
        part = src[y0 - y:y1 - y, x0 - x:x1 - x]
        m = part != ""
        dst[y0:y1, x0:x1][m] = part[m]
    return dst


def _sprite(c):
    """A creature's canvas outlined: one pixel larger on every side."""
    return A.outline(c.g)


def _done(g, pal):
    return A.colours(g, {**pal, "K": A.OUTLINE})


# ---- bull moose ------------------------------------------------------------------------------------------------

MOOSE = {"B": "#543", "S": "#432", "D": "#211", "L": "#a98", "H": "#654", "Hd": "#432", "A": "#edc", "Ad": "#ba8",
         "Mt": "#987", "Mz": "#432", "M": "#311", "P": "#111", "Wt": "#fff", "G": "#6a3", "Gd": "#483",
         "Br": "#bcd", "Bl": "#89a", "Wa": "#247", "Wb": "#358", "Wr": "#8bd", "Wd": "#acf", "Wg": "#5a4"}


def moose_head(c, x, y, a, mouth, ear):
    """The bull's head at its joint (x, y), a degrees up: a long face with the overhanging nose, broad palm
    antlers with tines (the far one paler), the bell hanging straight down from the throat; mouth opens the jaw
    (0..1), ear flicks the ear back. -> the mouth's corner (x, y), for breath and weed."""
    h = Part(c, x, y, a)
    for (u, v, d), key in (((-6.5, -6.5, 22), "Ad"), ((6.5, -6.5, -22), "A")):   # far palm, near palm
        palm = h.at(u, v, d)
        c.paint(palm.ell(0, 0, 7.2, 2.9), key)
        for t in np.linspace(-1, 1, 5):                             # tines along the top edge
            c.paint(palm.line(6 * t, -2, 7 * t + 0.6 * t, -4.6, 1.2), key)
        c.paint(h.line(0.5, -2.5, u * 0.6, v * 0.6, 2.4), key)     # the beam down to the skull
    if ear:
        c.paint(h.line(1.5, -2, 6, 0, 2.2), "H")
    else:
        c.paint(h.line(1.5, -2, 4.5, -5.5, 2.2), "H")
        c.paint(h.line(2.2, -2.8, 4, -5, 0.9), "D")
    jaw = 2.5 * mouth
    if mouth:
        c.paint(h.tri((-3, 2.5), (-11.5, 3.5 + jaw), (-11, 5.5)), "M")
        c.paint(h.line(-3, 3.5, -10.5, 5 + jaw, 2.2), "Mz")         # the dropped lower jaw
    c.paint(h.ell(-0.5, 0, 4, 3.6) | h.line(-2, 0.5, -9.5, 1.8, 5.4), "H")   # skull and the long face
    c.paint(h.ell(-10.5, 2.4, 3.3, 2.9), "H")                       # the bulbous nose
    c.paint(h.ell(-11.5, 2.7, 2.2, 2), "Mt")
    c.paint(h.dot(-13, 2.2), "P")                                    # nostril
    if not mouth:
        c.paint(h.ell(-8.5, 4.8, 2.4, 1.1), "Mz")                   # lower lip
    c.paint(h.dot(-2.6, -1.2), "P")
    ex, ey = h.p(-2.6, -1.2)
    c.set(int(ex) - 1, int(ey), "Wt")                                # catchlight
    tx, ty = h.p(0.5, 3.4)                                           # the bell under the throat, hanging down
    c.paint(c.line(tx, ty, tx - 0.5, ty + 6, 2.2) | c.ellipse(tx - 0.6, ty + 6.6, 1.5, 1.8), "Hd")
    return h.p(-12, 4.5 + jaw * 0.6)


def moose_body(c, gy, tail=0):
    """The bull's body facing left on ground row gy: the high hump, long legs pale below, a stub tail. -> the
    neck's root."""
    for x0, sh in ((23, "S"), (28, "B"), (46, "S"), (51, "B")):       # long legs, the far ones darker
        c.paint(c.rect(x0, gy - 22, x0 + 3, gy - 9), sh)
        c.paint(c.rect(x0, gy - 9, x0 + 3, gy - 2), "L")
        c.paint(c.rect(x0 - 1, gy - 2, x0 + 3, gy + 1), "D")
    c.paint(c.ellipse(40, gy - 27, 16, 8), "B")                      # body
    c.paint(c.ellipse(41, gy - 21.5, 11, 2.2), "S")                  # belly shade
    c.paint(c.ellipse(30, gy - 33, 9, 7.5), "B")                     # the shoulder hump
    c.paint(c.line(30, gy - 39, 40, gy - 34, 2), "S")                # dark ridge on the hump
    c.paint(c.line(55.5, gy - 31, 57 + tail, gy - 26, 2), "D")       # stub tail
    return 28, gy - 34


def moose_neck(c, root, joint):
    c.paint(c.line(*root, *joint, 8.5), "B")
    c.paint(c.line(root[0] - 1, root[1] + 3, joint[0] + 1, joint[1] + 3, 4), "S")   # the darker throat


def bellow(k):
    """The bull standing proud in the frost, then bellowing: head thrown up, mouth wide, breath steaming."""
    pose = [(4, 0.0, 0, False), (4, 0.0, 0, True), (16, 0.4, 0, False), (30, 1.0, 1, False), (30, 1.0, 2, False),
            (26, 0.6, 3, False), (12, 0.0, 4, False)][k]
    a, mouth, puff, ear = pose
    c = A.Canvas(62, 60)
    c.xx, c.px = c.xx - 3, c.px - 3                                   # room ahead and above for the breath
    c.yy, c.py = c.yy - 4, c.py - 4
    gy = 53
    root = moose_body(c, gy)
    joint = (17, gy - 39 - a * 0.1)
    moose_neck(c, root, joint)
    mx, my = moose_head(c, *joint, a, mouth, ear)
    for x, y in ((3, gy + 5), (5, gy + 4), (6, gy + 5), (59, gy + 5), (61, gy + 4), (17, gy + 5)):
        c.set(x, y, "G")
    g = _sprite(c)
    if puff:                                                         # breath clouds steaming up off the muzzle
        b = A.Canvas(64, 62)
        ox, oy = mx + 4, my + 5                                      # the outline pads by one, the room by 3 and 4
        for i in range(puff):
            age = puff - i
            r = 1 + 0.55 * age
            cx, cy = ox - 0.5 - 0.8 * age, oy - 2 - 2.4 * age
            if age <= 3:
                b.paint(b.disc(cx, cy, r) & (g == ""), "Br" if age < 3 else "Bl")
        g[b.g != ""] = b.g[b.g != ""]
    return _done(g, MOOSE)


def wade(k):
    """The bull feeding in a lake: head up with water weed hanging from its mouth and dripping, chewing; then it
    plunges its head under for more and lifts it out streaming."""
    a, mouth, weed, drip, ripple = [(0, 0.0, 1, 0, 0), (2, 0.3, 1, 1, 0), (0, 0.0, 1, 2, 0), (-28, 0.0, 0, 0, 0),
                                    (-62, 0.0, 0, 0, 2), (-30, 0.3, 2, 3, 1)][k]
    c = A.Canvas(62, 46)
    wl = 39                                                          # the water line
    root = moose_body(c, 56)
    joint = (17 + a * -0.04, 22 - a * 0.12)
    moose_neck(c, root, joint)
    mx, my = moose_head(c, *joint, a, mouth, False)
    if weed:                                                         # weed from the lips, streaming down
        for dx, ln in ((0, 5), (1.5, 7), (3, 4)):
            x0 = mx + dx + 2
            c.paint(c.line(x0, my, x0 - 0.6 * (weed - 1) + 0.5, my + ln, 1.1), "Wg")
    g = _sprite(c)
    g[wl:, :] = ""
    for y in range(wl, 48):                                          # the lake, darker in the deep
        g[y, :] = "Wb" if y < wl + 3 else "Wa"
    for x in range(0, 64, 9):                                        # little glints
        g[wl + 1 + (x // 9) % 3, (x + 3 * k) % 64] = "Wd"
    for x0, x1 in ((22, 33), (45, 56)):                              # rings round the legs
        g[wl, x0:x1] = "Wr"
    if ripple:                                                       # rings where the head went under
        hx = int(joint[0] - 8)
        g[wl, max(hx - 4 - ripple * 2, 0):hx + 5 + ripple * 2] = "Wr"
        g[wl + 2, max(hx - 1 - ripple * 3, 0):hx + 2 + ripple * 3] = "Wd"
    if drip:                                                         # drops falling from the weed
        for i, (dx, dy) in enumerate(((3, 9), (5, 11), (4, 13))):
            y = int(my + dy + (drip + i) % 3 * 2)
            x = int(mx + dx)
            if 0 <= y < wl and 0 <= x < 64 and g[y, x] == "":
                g[y, x] = "Wd"
    return _done(g, MOOSE)


# ---- rabbit ----------------------------------------------------------------------------------------------------

RABBIT = {"B": "#987", "D": "#654", "L": "#dcb", "W": "#fff", "E": "#e9a", "P": "#111", "N": "#e78",
          "O": "#f82", "Od": "#c51", "C": "#5a3", "Cd": "#383", "G": "#6a3", "Ea": "#653", "Eh": "#211", "Ed": "#543"}


def rabbit(c, x, y, head=0.0, ear=0, nose=0, chew=0, hold=False):
    """A wild rabbit sitting, facing left, its hind foot at (x, y): round and compact, short ears without the
    hare's black tips, a white scut; head dips by head px, ear lays the near ear back (1) or both (2), nose
    twitches, chew works the jaw; hold sits it up with its forepaws raised to the mouth."""
    r = Part(c, x, y)
    up = 2 if hold else 0
    c.paint(r.ell(5.5, -4.5, 5.2, 4.4), "B")                        # haunch
    c.paint(r.ell(1, -5.5 - up, 4.2, 4 + up * 0.5), "B")            # chest
    c.paint(r.ell(1.5, -2.6, 4, 1.4) | r.ell(-0.5, -4.5 - up, 2, 3), "L")   # pale belly and chest
    c.paint(r.disc(10.6, -6, 1.7), "W")                             # the scut
    c.paint(r.line(1, 0, 8, 0, 1.6), "D")                           # hind foot
    if not hold:
        c.paint(r.line(-1.6, -3, -2.4, 0, 1.3), "L")               # forepaw
    hx, hy = -2.4, -10 - up + head
    for i, ang in enumerate((80, 72)):                              # ears: short, upright or laid back
        if ear == 2 or (ear == 1 and i == 1):
            ang = 42
        a = np.radians(ang)
        bx, by = hx + 1 + 0.8 * i, hy - 1.5
        ex, ey = bx + 5.2 * np.cos(a), by - 5.2 * np.sin(a)
        c.paint(r.line(bx, by, ex, ey, 1.9), "B" if i else "D")
        if i:
            c.paint(r.line(bx + 0.3, by - 1, bx + 3.6 * np.cos(a) + 0.3, by - 3.6 * np.sin(a), 0.8), "E")
    c.paint(r.ell(hx, hy, 3.2, 2.9) | r.ell(hx - 1.9, hy + 1, 1.8, 1.7), "B")   # head, round cheek
    c.paint(r.ell(hx - 1.6, hy + 1.9, 1.4, 0.8 + 0.3 * chew), "L")
    c.paint(r.dot(hx - 3.6, hy + 0.4 - 0.6 * nose), "N")
    c.paint(r.dot(hx - 0.6, hy - 0.8), "P")
    return r.p(hx - 3, hy + 2.2)


def carrot(c, x, y, deg, bite=0):
    """A carrot with its tip at (x, y) and its leafy top deg degrees round from straight down."""
    a = np.radians(deg)
    dx, dy = np.sin(a), np.cos(a)
    x0, y0 = x + dx * bite, y + dy * bite
    c.paint(c.line(x0, y0, x + 7 * dx, y + 7 * dy, 1.6) | c.line(x + 3 * dx, y + 3 * dy, x + 7 * dx, y + 7 * dy, 2.4),
            "O")
    c.paint(c.line(x + 4 * dx + 0.8, y + 4 * dy, x + 4 * dx + 1.5, y + 4 * dy, 0.7), "Od")
    for t in (-35, 0, 35):                                          # the leafy top
        b = a + np.radians(t)
        c.paint(c.line(x + 8 * dx, y + 8 * dy, x + 8 * dx + 3 * np.sin(b), y + 8 * dy + 3 * np.cos(b), 1), "C")


def nibble(k):
    """A wild rabbit sitting up nibbling a carrot held in its forepaws, nose twitching, an ear turning."""
    head, ear, nose, chew = [(0, 0, 0, 0), (0, 0, 1, 1), (0, 0, 0, 0), (1, 0, 1, 1), (1, 1, 0, 0), (0, 1, 1, 1),
                             (0, 0, 1, 0)][k]
    c = A.Canvas(24, 22)
    mx, my = rabbit(c, 9, 21, head=head, ear=ear, nose=nose, chew=chew, hold=True)
    carrot(c, mx - 0.5, my, -20)
    c.paint(c.line(7, 15 - 0.4 * head, 6, 17 - 0.4 * head, 1.6), "L")   # forepaws round the carrot
    for x in (1, 3, 21):
        c.set(x, 21, "G")
    return _done(_sprite(c), RABBIT)


def burrow(k):
    """Two rabbits at their burrow: one pops out of the hole, looks round and ducks back; the other sits by the
    mound, its nose and ears twitching."""
    rise, ear2 = [(6, 0), (6, 1), (6, 0), (3, 0), (0, 0), (3, 0), (6, 2), (6, 0)][k]
    sit_ear, sit_head = [(0, 0), (0, 0), (1, 0), (1, 1), (0, 1), (0, 0), (0, 0), (1, 0)][k]
    W, H = 44, 22
    gy = 21
    mound = A.Canvas(W - 2, H)
    mound.paint(mound.ellipse(13, gy + 3, 13, 8), "Ea")              # the earth mound
    mound.paint(mound.ellipse(13, gy - 1.5, 5, 3), "Eh")             # the hole: its top edge at gy - 4
    mound.paint(mound.rect(4, gy - 2, 5, gy - 1) | mound.rect(20, gy - 1, 22, gy), "Ed")
    pop = A.Canvas(W - 2, H)
    if rise:                                                         # the one in the hole: only what is out of it
        rabbit(pop, 15.5, gy + 7 - rise, ear=ear2, nose=k % 2)
        pop.g[(pop.py > gy - 4) | (pop.px > 18.5)] = ""
    sit = A.Canvas(W - 2, H)
    rabbit(sit, 32, gy, head=sit_head, ear=sit_ear, nose=(k + 1) % 2)
    g = _grid(W, H + 2)
    _paste(g, _sprite(mound), 0, 0)
    _paste(g, _sprite(pop), 0, 0)
    _paste(g, _sprite(sit), 0, 0)
    for x in (1, 28, 41):
        g[gy + 1, x] = "G"
    return _done(g, RABBIT)


# ---- brown bear ------------------------------------------------------------------------------------------------

BEAR = {"B": "#764", "Bh": "#986", "Bs": "#653", "S": "#543", "Dk": "#321", "L": "#a86", "Ld": "#875", "N": "#211",
        "P": "#111", "C": "#dcb", "M": "#311", "T": "#c66", "G": "#6a3", "R": "#677", "Rl": "#899", "Rd": "#566",
        "Wa": "#247", "Wb": "#469", "Wr": "#8bd", "Wd": "#def", "F": "#e78", "Fb": "#578", "Fs": "#cde", "Fd": "#456",
        "Tb": "#776", "Td": "#554", "Tl": "#998"}


def _down(m, n):
    """The mask moved n rows down (up for a negative n), empty where it came in."""
    out = np.zeros_like(m)
    if n > 0:
        out[n:] = m[:-n]
    elif n < 0:
        out[:n] = m[-n:]
    else:
        out[:] = m
    return out


def _grow(m):
    """The mask one pixel larger on every side (4 neighbours)."""
    out = m | _down(m, 1) | _down(m, -1)
    out[:, 1:] |= m[:, :-1]
    out[:, :-1] |= m[:, 1:]
    return out


def _fur(c, m, lit=1, shade=2):
    """Volume for a fur mass m already painted: the top rows grizzled pale, the bottom rows in shade."""
    c.paint(m & ~_down(m, lit), "Bh")
    c.paint(m & ~_down(m, -shade), "Bs")


def bear_head(c, x, y, a, mouth=0.0, eyes=True):
    """The bear's heavy head at its neck joint (x, y), a degrees up: a broad domed skull with heavy jowls, two
    small round ears on top, the long light muzzle with a dark nose; mouth opens the jaw (0..1), eyes False shuts
    them. -> the head (a Part), its jaws at (-9, 2.6)."""
    h = Part(c, x, y, a, 1.15)
    skull = h.ell(-1, -0.6, 4.4, 3.7) | h.ell(-2.2, 1.5, 3.8, 2.5)  # domed skull, heavy jowls
    c.paint(_grow(skull) & ~skull & (c.g != ""), "Bs")              # a shadow where the head meets the neck
    c.paint(h.disc(2.4, -4.6, 1.6), "S")                               # the far ear
    c.paint(h.disc(0.3, -5, 1.8), "B")                              # the near ear, round
    c.paint(h.dot(0.2, -4.9), "Dk")
    if mouth:                                                        # the open mouth: a dark gap, the lower jaw down
        c.paint(h.tri((-4, 2.4), (-11, 2.4 + 2.4 * mouth), (-10, 4 + 2.4 * mouth)), "M")
        c.paint(h.line(-4, 3.4, -10, 3.4 + 2.4 * mouth, 1.8), "Ld")
        c.paint(h.dot(-9, 2.8 + 1.8 * mouth), "T")
    c.paint(skull, "B")
    c.paint(skull & ~_down(skull, 1), "Bh")
    c.paint(h.line(-4.6, 0.9, -10.4, 1.4, 3.3) | h.ell(-5, 1.3, 1.8, 1.9), "L")   # the long muzzle
    c.paint(h.line(-5.6, 2.8, -10.2, 2.8, 1), "Ld")                 # its underside
    c.paint(h.ell(-11, 0.7, 1.2, 1.1), "N")                         # nose
    if eyes:
        c.paint(h.dot(-4.4, -1.3), "P")
    else:
        c.paint(h.line(-5.2, -0.9, -3.6, -1.1, 0.8), "Dk")          # eyes shut in bliss
    return h


def _gait(u):
    """A foot's (dx, lift) at u in 0..1 of a stride: on the ground going back, then lifted swinging forward."""
    u %= 1
    if u < 0.5:
        return -4 + 16 * u, 0
    return 4 - 16 * (u - 0.5), round(2 * np.sin(np.pi * (u - 0.5) * 2))


def _bgait(u):
    """A bear's foot (dx, lift) at u in 0..1 of a stride: planted and pushed back, then lifted and swung."""
    u %= 1
    if u < 0.6:
        return -4 + 13 * u, 0
    t = (u - 0.6) / 0.4
    return 3.8 - 7.8 * t, round(3 * np.sin(np.pi * t))


def bear_side(c, x, gy, legs, head_a=0.0, bob=0.0, mouth=0.0):
    """A grown brown bear on all fours facing left, its chest near x, feet on row gy: the big shoulder hump, the
    back sloping down to a lower rump, thick legs on broad clawed paws, the heavy head carried low; legs = (dx,
    lift) of the near front, near hind, far front and far hind feet. -> the head (bear_head)."""
    b = Part(c, x, gy - 16 + bob)
    nf, nh, ff, fh = legs

    def leg(top, foot, near, hind):
        dx, lift = foot
        tx, ty = b.p(*top)
        fx, fy = tx + dx, gy - 1 - lift
        knee = ((tx + fx) / 2 + (1.2 if hind else -0.3), (ty + fy) / 2 + 1)
        m = c.line(tx, ty, *knee, 6.4 if hind else 5.6) | c.line(*knee, fx, fy - 1, 4.8)
        c.paint(m, "B" if near else "S")
        c.paint(m & (c.py > gy - 7 - lift), "S" if near else "Dk")  # darker towards the paw
        paw = c.ellipse(fx - 0.8, fy - 0.3, 3.2, 1.5)
        c.paint(paw, "Dk")
        if not hind:
            c.set(int(fx - 3.6), int(fy), "C")                       # the long front claws
            c.set(int(fx - 2.6), int(fy), "C")

    leg((4.5, 2), ff, False, False)                                  # far legs
    leg((23, 1), fh, False, True)
    trunk = b.ell(15, -0.8, 14, 6.2) | b.ell(26.5, -2, 6.4, 6.4) | b.ell(1.5, 0, 5, 6.2)
    hump = b.ell(6.5, -6, 7.5, 6)
    c.paint(trunk | hump, "B")
    _fur(c, trunk | hump)
    c.paint(hump & ~_down(hump, 2) & (c.px < x + 11), "Bh")          # the grizzled hump
    for u, v in ((10, -2), (14, 1), (19, -3), (24, 0), (29, -3)):    # shaggy fur
        c.paint(b.line(u, v, u + 1.5, v + 0.8, 1), "Bs")
    c.paint(b.ell(33, -4, 1.2, 1), "S")                              # stub tail
    leg((2, 2), nf, True, False)                                     # near legs
    leg((26, 1), nh, True, True)
    neck = b.line(3, -3, -4.5, 2, 7.5)
    c.paint(neck, "B")
    c.paint(neck & ~_down(neck, 1), "Bh")
    return bear_head(c, *b.p(-5, 2.5), head_a, mouth)


def bear_walk(k):
    """A brown bear lumbering along on its four big paws, the hump rolling, its heavy head swinging low."""
    u = k / 6
    legs = (_bgait(u + 0.25), _bgait(u), _bgait(u + 0.75), _bgait(u + 0.5))
    c = A.Canvas(52, 32)
    bear_side(c, 17, 31, legs, head_a=-5 + 3 * np.sin(2 * np.pi * u), bob=0.6 * np.cos(4 * np.pi * u))
    for x in (2, 4, 49):
        c.set(x, 31, "G")
    return _done(_sprite(c), BEAR)


def salmon(c, x, y, deg, flap=0):
    """A salmon centred at (x, y), its head deg degrees round from pointing left: a dark back, a silver flank
    with the pink stripe, a forked tail that flaps (-1..1)."""
    f = Part(c, x, y, deg)
    c.paint(f.tri((4.6, 0), (8.4, -2.6 + flap), (8.4, 2.6 + flap)), "Fd")   # the tail
    c.paint(f.dot(8, 0.2 + flap * 0.5), "")                         # its fork
    c.paint(f.ell(0, 0, 5.4, 2.1), "Fs")                            # silver body
    c.paint(f.ell(0.4, -1.4, 4.6, 0.8), "Fb")                       # the dark back
    c.paint(f.line(-3.4, 0, 3.8, 0.2, 1), "F")                      # the pink stripe
    c.paint(f.tri((0, -1.8), (2.6, -1.8), (2, -3.4)), "Fd")         # the dorsal fin
    c.paint(f.dot(-3.8, -0.6), "P")


def bear_fish(k):
    """A brown bear at the lip of a waterfall: it holds a salmon flapping in its jaws, then watches the pool, a
    salmon leaps up the falling water and the bear snaps it out of the air."""
    head_a, mouth, held, leap = [(16, 0.0, 1, 0), (16, 0.0, -1, 0), (-6, 0.0, 0, 0),
                                 (-9, 0.0, 0, 1), (2, 0.6, 0, 2), (12, 1.0, 0, 3)][k]
    W, H = 60, 44
    gy = 30                                                          # the lip's top
    lip = 22                                                         # the fall pours over x < lip
    g = _grid(W, H)
    rock = A.Canvas(W - 2, H - 2)
    rock.paint(rock.rect(lip, gy, W - 2, H - 4) | rock.ellipse(lip + 1, gy + 8, 3, 9), "R")
    rock.paint(rock.rect(lip, gy, W - 2, gy + 1), "Rl")
    rock.paint(rock.rect(lip + 6, gy + 4, lip + 12, gy + 5) | rock.rect(lip + 15, gy + 9, lip + 23, gy + 10)
               | rock.rect(lip + 4, gy + 11, lip + 8, gy + 12), "Rd")
    _paste(g, _sprite(rock), 0, 0)
    for y in range(gy, H - 4):                                       # the falling water, streaks running down
        for x in range(4, lip + 1):
            g[y, x] = "Wd" if x % 3 == 1 and (y - 2 * k) % 6 < 3 else "Wr"
    bear = A.Canvas(W - 2, H - 2)
    h = bear_side(bear, 25, gy + 1, ((-1, 0), (1, 0), (2, 0), (-2, 0)), head_a=head_a, mouth=mouth)
    _paste(g, _sprite(bear), 0, 0)
    g[gy - 1, 4:lip + 1] = "Wd"                                      # the stream curling over the lip
    for x in range(lip + 1, W, 5):
        g[gy, (x + 2 * k) % (W - lip - 1) + lip + 1] = "Wd"
    g[H - 4:H - 2, :] = "Wb"                                         # the pool
    g[H - 2:, :] = "Wa"
    for x in range(2, lip + 3):                                      # foam where it lands
        if (x + k) % 3:
            g[H - 4, x] = "Wd"
    for x in (34 + k % 3, 50 + k % 2):
        g[H - 3, x] = "Wd"
    if held or leap:
        f = A.Canvas(W - 2, H - 2)
        if held:                                                     # held across the jaws, head up, tail flapping
            jx, jy = h.p(-9, 2.6)
            salmon(f, jx + 2.5, jy + 0.5, 98 + 8 * held, held)
        else:                                                        # on its arc up the fall to the jaws
            fx, fy, ang = [(5, 35, 72), (6, 28, 80), (7, 22, 86)][leap - 1]
            salmon(f, fx, fy, 180 - ang, (-1) ** leap)
        _paste(g, _sprite(f), 0, 0)
        if leap == 1:
            g[H - 5, 1:9] = "Wd"                                    # the splash it left
    return _done(g, BEAR)


def bear_stand(c, x, gy, a, eyes):
    """A brown bear standing up in profile, facing left, its back to the right on x, feet on row gy: the hump
    over the shoulders, a deep belly, a heavy haunch, the darker near arm hanging with its paw curled, short thick
    legs on long flat feet."""
    b = Part(c, x, gy)
    c.paint(b.line(-5, -26, -11, -18, 4.4), "Bs")                    # the far arm
    c.paint(b.disc(-11.5, -17.5, 2), "Dk")
    c.paint(b.ell(-9.5, -1.3, 4.5, 1.5), "Dk")                       # the far foot
    body = b.ell(-7, -19, 7, 12, 8) | b.ell(-9, -14, 6, 7) | b.ell(-7.5, -7, 6.5, 6) | b.ell(-3.5, -27.5, 5, 4)
    c.paint(body, "B")
    _fur(c, body, 1, 2)
    c.paint(b.ell(-3, -29.5, 3.5, 2) & body, "Bh")                   # the grizzled hump
    thigh = b.ell(-8.5, -8, 5.2, 5)                                  # the haunch, its front edge in shade
    c.paint(_grow(thigh) & ~thigh & body & (c.px < b.p(-8.5, 0)[0]), "Bs")
    c.paint(b.line(-10, -5, -11.5, -1.5, 5), "S")                    # the near shin
    c.paint(b.ell(-13, -1.2, 5, 1.6), "Dk")                          # the long hind foot
    c.set(*(int(v) for v in b.p(-18.2, -1)), "C")
    arm = b.line(-8, -26, -13.5, -18.5, 4.8)                         # the near arm hanging, darker
    c.paint(arm, "S")
    c.paint(arm & ~_down(arm, 1), "B")
    c.paint(b.ell(-14.5, -17, 2.4, 2.1), "Dk")                       # its paw, curled
    c.paint(b.dot(-16.6, -16.2) | b.dot(-15.8, -15.2), "C")
    neck = b.line(-5.5, -29, -9, -33, 8)
    c.paint(neck, "B")
    c.paint(neck & ~_down(neck, 1), "Bh")
    bear_head(c, *b.p(-9, -34), a, 0, eyes)


def bear_scratch(k):
    """A brown bear standing with its back to a pine, rubbing up and down, eyes shut in bliss, bark flaking."""
    dy, a, eyes = [(0, 4, True), (-1, 10, False), (-2, 16, False), (-1, 10, False), (0, 6, False), (-1, 12, False),
                   (-2, 16, False), (-1, 8, True)][k]
    W, H = 40, 48
    gy = 46
    g = _grid(W, H)
    tree = A.Canvas(W - 2, H - 2)
    tree.paint(tree.rect(24, 0, 35, gy - 1) | tree.triangle((20, gy), (38, gy), (29, gy - 8)), "Tb")
    tree.paint(tree.rect(24, 0, 25, gy - 1) | tree.rect(33, 0, 35, gy - 1), "Td")
    for x0, y0 in ((28, 2), (31, 9), (29, 17), (31, 37)):            # bark furrows
        tree.paint(tree.rect(x0, y0, x0 + 1, y0 + 4), "Td")
    tree.paint(tree.line(34, 6, 37.5, 2.5, 1.4), "Td")              # a broken branch
    _paste(g, _sprite(tree), 0, 0)
    bear = A.Canvas(W - 2, H - 2)
    bear_stand(bear, 25, gy + dy, a, eyes)
    _paste(g, _sprite(bear), 0, 0)
    for i, (x, y) in enumerate(((26, 16), (37, 22), (38, 30), (25, 6))):   # bark flakes drifting down
        if not eyes or i < 2:
            yy = y + (k * 3 + i * 5) % 12
            if g[yy, x] == "":
                g[yy, x] = "Tl"
    for x in (3, 5, 9):
        g[gy, x] = "G"
    return _done(g, BEAR)


# ---- wolves ----------------------------------------------------------------------------------------------------

WOLF = {"B": "#bbc", "W": "#eef", "D": "#778", "P": "#111", "Y": "#fd4", "M": "#622",
        "b": "#99a", "w": "#ccd", "d": "#667",
        "Mn": "#ffd", "Ms": "#ddb", "St": "#ffe", "Sn": "#eef", "Sd": "#bcd", "R": "#567", "Rd": "#345"}
DARK = {"B": "b", "W": "w", "D": "d"}


def _shade(g, dark):
    if dark:
        for a, b in DARK.items():
            g[g == a] = b
    return g


def wolf_sit(c, x, y, a, mouth, k=1.0):
    """A Mountain wolf sitting, facing left, its front feet at (x, y): lib_animals' wolf at scale k; head a
    degrees up (55 howls), mouth open."""
    w = Part(c, x, y, 0, k)
    c.paint(w.line(7, -2.5, 14, -5, 4) | w.ell(14.5, -6.5, 2.2, 3), "D")   # bushy tail
    c.paint(w.ell(15, -8, 1.6, 1.8), "W")
    c.paint(w.ell(6, -6, 6.5, 5.5), "B")                             # haunch
    c.paint(w.ell(2.5, -10.5, 5, 8, -20), "B")                       # torso, leaning back
    c.paint(w.line(-1.5, -10, -2, -1, 2.6) | w.line(1.5, -9, 1.5, -1, 2.6), "B")
    c.paint(w.ell(-2, -0.7, 2, 1) | w.ell(2, -0.7, 2, 1), "W")       # paws
    c.paint(w.ell(-1, -12.5, 2.6, 5, -15), "W")                      # chest ruff
    c.paint(w.line(3, -15, 1, -20, 5.5), "B")                         # neck
    h = w.at(0, -21, a)
    c.paint(h.tri((-0.5, -3), (3.6, -2.5), (2.6, -8.5)), "B")         # ear
    c.paint(h.line(1.6, -3.5, 2.2, -6, 1), "D")
    c.paint(h.ell(-0.5, 0, 4.6, 4) | h.line(-3, 0.4, -10, 0.8, 3.4), "B")
    c.paint(h.ell(-1, 2.8, 3.2, 1.4), "W")                           # pale cheek
    if mouth:
        c.paint(h.line(-3, 3, -9, 4.4, 2), "W")                     # the lower jaw, dropped
        c.paint(h.tri((-3.5, 2), (-10, 2.4), (-9, 3.6)), "M")
    else:
        c.paint(h.line(-4, 2, -9.5, 2.2, 1), "W")
    c.paint(h.dot(-10.6, 0.2), "P")
    c.paint(h.dot(-3.4, -1.6), "Y")


def howl(k):
    """Three Mountain wolves on a snowy ridge under the full moon, howling in turn: the one in the middle stands
    higher, behind; they start together."""
    tilt = [(55, 55, 55), (55, 50, 58), (40, 58, 30), (20, 58, 52), (30, 45, 58), (52, 30, 58)][k]
    W, H = 64, 50
    g = _grid(W, H)
    sky = A.Canvas(W, H)
    sky.paint(sky.disc(10, 9, 6.5), "Mn")                           # the moon, top left (top right once flipped)
    sky.paint(sky.disc(8, 8, 1.5) | sky.disc(12.5, 11, 1.1) | sky.disc(11, 6, 0.8), "Ms")
    _paste(g, sky.g, 0, 0)
    for x, y in ((30, 3), (44, 8), (58, 2), (24, 12), (52, 15), (3, 22)):   # stars, one twinkling
        g[y, x] = "St" if (x + k) % 4 else ""
    ridge = A.Canvas(W, H)
    ridge.paint(ridge.ellipse(30, H + 6, 40, 13), "Sn")
    ridge.paint(ridge.ellipse(30, H + 9, 34, 10), "Sd")
    _paste(g, _sprite(ridge), -1, -1)
    rock = A.Canvas(W, H)
    rock.paint(rock.ellipse(31, 45, 9, 6), "R")
    rock.paint(rock.ellipse(30, 40.5, 7, 1.8), "Sn")
    rock.paint(rock.rect(26, 44, 29, 45) | rock.rect(33, 47, 36, 48), "Rd")
    _paste(g, _sprite(rock), -1, -1)
    for (x, y, dark), a in zip(((30, 40, True), (44, 46, False), (15, 46, False)), (tilt[1], tilt[2], tilt[0])):
        c = A.Canvas(W, H)
        wolf_sit(c, x, y, a, a >= 50, 0.88)
        _paste(g, _shade(_sprite(c), dark), -1, -1)
    return _done(A.flip(g), WOLF)


def wolf_trot(c, x, y, u, k=1.0):
    """A Mountain wolf trotting, facing left, its feet on row y around x, u the stride (0..1): the diagonal legs
    swing together, the body bobs, the tail streams."""
    bob = 0.4 * np.cos(4 * np.pi * u)
    w = Part(c, x, y - bob, 0, k)

    def leg(hip, knee, s, key):
        dx, lift = s
        fx, fy = hip[0] + dx, -lift
        c.paint(w.line(*hip, hip[0] + knee + dx * 0.5, -4.5 - lift * 0.5, 2.4), key)
        c.paint(w.line(hip[0] + knee + dx * 0.5, -4.5 - lift * 0.5, fx, fy - 0.5, 1.6), key)
        c.paint(w.ell(fx - 0.6, fy - 0.4, 1.3, 0.7), "W" if key == "B" else "D")

    g1, g2 = _gait(u), _gait(u + 0.5)
    leg((-5, -10), 0.5, g2, "D")                                     # far front
    leg((7, -11), 2.2, g1, "D")                                      # far hind
    c.paint(w.line(10, -13, 16, -10, 3.2) | w.ell(16.5, -9.3, 2.4, 1.8), "D")   # tail, streaming
    c.paint(w.dot(18, -9.5), "W")
    c.paint(w.ell(1, -13.2, 8.5, 4.2), "B")                          # body
    c.paint(w.ell(-5, -13, 4.6, 5.2), "B")                           # the deep chest
    c.paint(w.ell(7, -13.8, 4.2, 3.9), "B")                          # haunch
    c.paint(w.ell(1.5, -16.6, 6, 1.4), "D")                          # the darker saddle
    c.paint(w.ell(-1, -9.8, 6, 1.4) | w.ell(-7, -11.5, 2.6, 3.6), "W")   # pale belly and chest
    leg((-4.5, -10), -0.5, g1, "B")                                  # near front
    leg((7.5, -11), 2.2, g2, "B")                                    # near hind
    c.paint(w.line(-5.5, -15, -9.5, -18.5, 5.6), "B")                # neck
    h = w.at(-10.5, -19, -6)
    c.paint(h.tri((0, -2.2), (3.4, -1.6), (2, -7.2)), "B")           # ear
    c.paint(h.line(1.6, -2.4, 1.9, -5, 0.9), "D")
    c.paint(h.ell(0, 0, 3.9, 3.3) | h.line(-2, 0.5, -7, 1, 3), "B")  # head and the shorter muzzle
    c.paint(h.ell(-1.2, 2.1, 2.8, 1.1) | h.line(-3, 2, -6.6, 1.8, 0.9), "W")
    c.paint(h.dot(-7.8, 0.2), "P")
    c.paint(h.dot(-2.6, -1.2), "Y")


def trot(k):
    """Two Mountain wolves trotting through the snow, the second one a step behind and further back, flakes
    drifting."""
    W, H = 64, 30
    g = _grid(W, H)
    snow = A.Canvas(W, H)
    snow.paint(snow.rect(0, H - 4, W, H), "Sd")
    snow.paint(snow.rect(0, H - 4, W, H - 3), "Sn")
    _paste(g, snow.g, 0, 0)
    back = A.Canvas(W, H)
    wolf_trot(back, 43, H - 5, k / 4 + 0.25, 0.92)
    _paste(g, _shade(_sprite(back), True), -1, -1)
    front = A.Canvas(W, H)
    wolf_trot(front, 18, H - 2, k / 4, 0.92)
    _paste(g, _sprite(front), -1, -1)
    for i, (x, y) in enumerate(((6, 2), (30, 5), (55, 1), (20, 9), (47, 10), (60, 6))):   # snow drifting down-right
        yy, xx = (y + k * 2 + i) % 16, (x + k) % W
        if g[yy, xx] == "":
            g[yy, xx] = "Sn"
    return _done(g, WOLF)


# ---- giraffe ---------------------------------------------------------------------------------------------------

GIRAFFE = {"O": "#c73", "Od": "#a52", "Oh": "#d95", "W": "#edb", "Wl": "#dc9", "D": "#532", "P": "#111", "T": "#536",
           "N": "#432", "G": "#5a3", "Gd": "#383", "Gl": "#7c4", "Tb": "#753"}


def _patches(c, mask, cell, seed):
    """The giraffe's network: pale lines between patches (a jittered Voronoi), inside mask."""
    rng = np.random.default_rng(seed)
    pts = np.array([(x + rng.uniform(0.15, 0.85) * cell, y + rng.uniform(0.15, 0.85) * cell)
                    for y in np.arange(-cell, c.h + cell, cell) for x in np.arange(-cell, c.w + cell, cell)])
    d = np.sqrt((c.px[..., None] - pts[:, 0]) ** 2 + (c.py[..., None] - pts[:, 1]) ** 2)
    d.sort(axis=-1)
    return mask & (d[..., 1] - d[..., 0] < 0.95)


def giraffe(c, x, y, a, chew, tongue, tail, seed=3):
    """A giraffe facing left, its near front hoof at (x, y): long legs pale below, a sloping back patched in a
    pale network, the long neck with its short mane up to a head a degrees up; chew works the jaw, tongue reaches
    out (0..2), tail swings (-1..1)."""
    g = Part(c, x, y)
    body = A.Canvas(c.w, c.h)
    b = Part(body, x, y)
    for hx, hy, fx, key in ((4, -25, 4.5, "Od"), (16, -23.5, 17.5, "Od"), (0.5, -26, 0, "O"), (18.5, -22.5, 20, "O")):
        kx = (hx + fx) / 2
        body.paint(b.line(hx, hy, kx, -10, 2.2), key)                  # upper leg
        body.paint(b.line(kx, -10, fx, -1.5, 1.8), "Wl" if key == "Od" else "W")   # the pale lower leg
        body.paint(b.disc(kx, -10, 1.2), key)                         # knee
        body.paint(b.line(fx - 0.6, -0.7, fx + 0.8, -0.7, 1.6), "D")   # hoof
    torso = b.ell(9.5, -27, 11, 5.6, -12)                            # high at the withers
    neck = b.disc(1.5, -29, 3.6)
    for t in np.linspace(0, 1, 18):
        neck |= b.disc(1.5 - 10 * t, -29 - 21 * t, 3.6 - 2.1 * t)
    body.paint(torso | neck, "O")
    body.g[_patches(body, torso | neck, 4.4, seed)] = "W"
    body.paint(b.line(4, -30.5, -7, -52, 1.2), "N")                  # the short mane
    c.paint(g.line(19.5, -29.5, 21 + 2 * tail, -19, 1) | g.ell(21 + 2.2 * tail, -18, 0.9, 1.6), "N")   # tail
    c.g[body.g != ""] = body.g[body.g != ""]
    h = g.at(-9, -51, a)
    c.paint(h.line(0.6, -1.6, 0.4, -5, 1.2) | h.line(2, -1.4, 2.6, -4.6, 1.2), "Oh")   # ossicones
    c.paint(h.dot(0.4, -5.4) | h.dot(2.7, -5), "N")
    c.paint(h.line(1.8, -0.4, 5, -1.6, 1.4), "Oh")                   # ear
    c.paint(h.ell(0, 0, 3, 2.4) | h.line(-1, 0.6, -6.6, 1.4 + 0.4 * chew, 2.8), "Oh")   # skull and muzzle
    c.paint(h.line(-3.6, 1.9, -6.6, 2 + 0.4 * chew, 1.2), "W")
    c.paint(h.dot(-1.4, -0.8), "P")
    c.paint(h.dot(-6.8, 0.6), "N")
    if tongue:
        c.paint(h.line(-7, 1.4, -7 - 1.3 * tongue, 0.5 - 1.3 * tongue, 1), "T")
    return h


def acacia(c, rustle):
    """A flat-topped tree at the top left: a thin trunk and a layered crown; rustle shifts a few leaves."""
    c.paint(c.line(6, 62, 7, 12, 1.8) | c.line(6.5, 22, 2.5, 14, 1.2) | c.line(7, 14, 11, 9.5, 1.2), "Tb")
    crown = c.ellipse(7.5, 7.5, 7.5, 2.8) | c.ellipse(3, 12.5, 3.4, 1.8) | c.ellipse(11.5, 5, 4, 1.8)
    c.paint(crown, "G")
    c.paint(crown & (c.py > 8.6), "Gd")
    for x, y in ((3, 6), (8, 5), (11, 4), (2, 12), (6, 7)):
        c.set(x + rustle, y, "Gl")


def giraffe_eat(k):
    """A giraffe browsing the top of a tree: tongue curling round the leaves, chewing, its tail swinging."""
    a, chew, tongue, tail, rustle = [(22, 0, 2, 0, 0), (22, 0, 1, 0.6, 1), (18, 1, 0, 1, 0), (18, 0, 0, -0.5, 1)][k]
    W, H = 54, 64
    c = A.Canvas(W - 2, H - 2)
    acacia(c, rustle)
    gc = A.Canvas(W - 2, H - 2)
    giraffe(gc, 29, 61, a, chew, tongue, tail)
    g = _grid(W, H)
    _paste(g, _sprite(c), 0, 0)
    _paste(g, _sprite(gc), 0, 0)
    return _done(g, GIRAFFE)


# ---- pieces ----------------------------------------------------------------------------------------------------

def pieces():
    return [
        A.Piece("Bull moose (moving, bellowing)", "pixel", frames=[bellow(k) for k in range(7)], fps=3),
        A.Piece("Bull moose (moving, wading)", "pixel", frames=[wade(k) for k in range(6)], fps=3),
        A.Piece("Rabbit (moving, nibbling a carrot)", "pixel", frames=[nibble(k) for k in range(7)], fps=4),
        A.Piece("Rabbit (moving, at its burrow)", "pixel", frames=[burrow(k) for k in range(8)], fps=4),
        A.Piece("Bear (moving, walking)", "pixel", frames=[bear_walk(k) for k in range(6)], fps=6),
        A.Piece("Bear (moving, fishing)", "pixel", frames=[bear_fish(k) for k in range(6)], fps=4),
        A.Piece("Bear (moving, scratching its back)", "pixel", frames=[bear_scratch(k) for k in range(8)], fps=4),
        A.Piece("Wolves (moving, howling at the moon)", "pixel", frames=[howl(k) for k in range(6)], fps=3),
        A.Piece("Wolves (moving, trotting)", "pixel", frames=[trot(k) for k in range(4)], fps=6),
        A.Piece("Giraffe (moving, eating leaves)", "pixel", frames=[giraffe_eat(k) for k in range(4)], fps=3,
                pitch=GIRAFFE_PITCH),
    ]
