"""Necks: the Meadows' shore lizard as a family of 8-bit pixel sprites, the cat's family (concepts/anim2.py).

A Neck (lib_animals.neck_body) is drawn in profile, facing left: a long low body on four short splayed legs, a long
tapering tail, a long neck carrying a small flat head held up and forward, a mouth lined with little teeth, beady
yellow eyes and a crest of small spines down the neck and back; greenish brown with a pale belly. With stars it is
bigger (s) and tinted, light blue with one, purple with two. Every sprite uses the cat's pitch, so a Neck stands
next to the cat at its true size. Drawn with artlib.Canvas masks, each sprite outlined in the cat's dark outline;
rain, ripples, mud and sparks are loose pixels around them. Every piece moves; the first frame is a good still for
players without the mod. Scenes are drawn on a roomy grid and cropped to what the frames use.
"""
import numpy as np

import artlib as A
from concepts import anim2
from concepts import lib_animals as AN


def _grid(w, h):
    return np.full((h, w), "", dtype=object)


def _paste(dst, src, x, y):
    """Non-empty pixels of src over dst, src's top left at (x, y); parts outside dst are cut off."""
    src = np.asarray(src, dtype=object)
    h, w = src.shape
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + w, dst.shape[1]), min(y + h, dst.shape[0])
    if x1 > x0 and y1 > y0:
        s = src[y0 - y:y1 - y, x0 - x:x1 - x]
        view = dst[y0:y1, x0:x1]
        view[s != ""] = s[s != ""]
    return dst


def _dot(g, x, y, col):
    if 0 <= y < g.shape[0] and 0 <= x < g.shape[1]:
        g[y, x] = col


def _crop(frames):
    """Frames cut to the box all of them use, so they stay aligned."""
    used = np.zeros(frames[0].shape, bool)
    for f in frames:
        used |= f != ""
    ys, xs = np.nonzero(used)
    return [[list(r) for r in f[ys.min():ys.max() + 1, xs.min():xs.max() + 1]] for f in frames]


def _prop(c, pal):
    """A canvas outlined and coloured: 1 px bigger on every side, so paste it 1 px up and left."""
    return np.asarray(AN.finish(c, pal), dtype=object)


# ---- the Neck (lib_animals.neck_body) ---------------------------------------------------------------------------

NECK, NECK_BLUE, NECK_PURPLE = AN.NECK, AN.NECK_BLUE, AN.NECK_PURPLE
BOX_W, BOX_H = AN.NECK_BOX
body = AN.neck_body


def neck(pal=NECK, right=False, s=1.0, **kw):
    """body() outlined on its own grid with room round it: the box at (6, 5), so the tongue has 6 px and the head
    5 px to rise; the feet stand on row FEET (s=1; feet() for any s). right mirrors it."""
    w, h = int(np.ceil(BOX_W * s)) + 8, int(np.ceil(BOX_H * s)) + 6
    c = A.Canvas(w, h)
    body(c, 6, h - 1 - BOX_H * s, s=s, **kw)
    g = _prop(c, pal)
    return A.flip(g) if right else g


def feet(g):
    """The lowest row a grid draws in, its outline included."""
    return int(np.nonzero((np.asarray(g) != "").any(axis=1))[0].max())


FEET = feet(neck())
STAR = ["..Y..", ".YYY.", "YYYYY", ".YYY.", ".Y.Y."]


def _star():
    return np.asarray(A.colours(A.outline(A.ascii(STAR, {"Y": 1})), {"Y": "#fd3", "K": A.OUTLINE}), dtype=object)


# ---- 1. shuffling in the muck -------------------------------------------------------------------------------

SHORE = {"w": "#38a", "h": "#7bf", "m": "#753", "d": "#542", "s": "#a86"}


def muck(k, n=4):
    """A Neck shuffles in place in the mud at the water's edge, legs stepping, tail swaying, mud flicked up behind
    it; ripples run over the water in front."""
    g = _grid(64, 30)
    c = A.Canvas(58, 6)
    c.paint(c.rect(0, 1, 14, 6), "w")
    c.paint(c.triangle((8, 6), (15, 0), (15, 6)) | c.rect(15, 0, 58, 6), "m")
    for x, y in ((20, 2), (25, 4), (31, 1), (36, 3), (41, 2), (28, 5), (15, 4), (46, 4), (52, 2), (56, 4)):
        c.set(x, y, "d")
    for x in range(2, 12, 4):                                    # ripples drift toward the shore
        c.set((x + k) % 12 + 1, 2 + (x // 4) % 2 * 2, "h")
        c.set((x + k) % 12 + 2, 2 + (x // 4) % 2 * 2, "h")
    _paste(g, _prop(c, SHORE), 0, 23)
    _paste(g, neck(step=(-1, 0, 1, 0)[k], tail=(1, 0, -1, 0)[k], head=(0, 1, 0, 1)[k]), 4, 25 - FEET - (k % 2))
    for j in range(2):                                           # mud specks thrown up behind
        t = (k / n + j / 2) % 1
        x, y = 50 + int(8 * t), 23 - int(12 * t * (1 - t) * 2.2)
        _dot(g, x, y, SHORE["s"])
        _dot(g, x + 1, y, SHORE["s"])
        _dot(g, x, y + 1, SHORE["s"])
    return g


# ---- 2. in the rain -------------------------------------------------------------------------------------------

def rain(k, n=6):
    """A Neck in the rain stretches its neck up at the drops and blinks; the streaks fall, the puddle under it
    splashes."""
    g = _grid(56, 34)
    rng = np.random.default_rng(7)
    drops = [(int(rng.integers(0, 56)), int(rng.integers(0, 32))) for _ in range(18)]
    for x, y0 in drops:                                          # one fall per loop: 5 px a frame
        y = (y0 + 5 * k) % 32
        for i in range(3):
            _dot(g, x - (y + i) // 10, y + i, "#9cf" if i else "#cef")
    c = A.Canvas(52, 4)
    c.paint(c.ellipse(26, 2, 25, 1.8), "w")
    c.paint(c.ellipse(26, 1.3, 20, 0.8), "h")
    _paste(g, _prop(c, SHORE), 1, 30)
    eyes = ("up", "up", "closed", "up", "open", "up")[k]
    _paste(g, neck(eyes=eyes, head=3 if eyes != "open" else 1, reach=-1, fin=1 + k % 2), 3, 31 - FEET)
    for x in ((8, 43), (14, 37), (20, 48), (5, 30), (34, 11), (17, 46))[k]:        # splashes
        _dot(g, x, 30, "#cef")
        _dot(g, x - 1, 29, "#9cf")
        _dot(g, x + 1, 29, "#9cf")
    return g


# ---- 3. no star, one star, two stars ----------------------------------------------------------------------------

def trio(k, n=6):
    """Three Necks one behind the other, each a little bigger, their stars over their heads: no star green in front,
    one star light blue, two stars purple at the back; each in turn rears its head and snaps."""
    g = _grid(80, 66)
    hop = [0, 0, 0]
    hop[k // 2] = 2 - k % 2
    for i, pal, s, x, ground in ((2, NECK_PURPLE, 1.1, 14, 31), (1, NECK_BLUE, 1.0, 7, 46), (0, NECK, 0.9, 0, 60)):
        nk = neck(pal, s=s, head=hop[i] * 1.5, jaw=1 if hop[i] == 2 else 0, fin=1 + (hop[i] > 0))
        y = ground - feet(nk) - (hop[i] == 2)
        for gx in range(x + 6 + int(9 * s), x + 6 + int(38 * s)):     # the meadow under its feet
            _dot(g, gx, ground, "#5a3" if gx % 3 else "#8c4")
        _paste(g, nk, x, y)
        top = int(np.nonzero((nk != "").any(axis=1))[0].min())
        for j in range(i):                                       # the stars over the head
            _paste(g, _star(), x + 5 + int(5.5 * s) + 6 * j - 3 * (i - 1), y + top - 7)
    return g


# ---- 4. the bite ----------------------------------------------------------------------------------------------

def bite(k, n=6):
    """A Neck rears with its jaw gaping full of little teeth, shoots its head forward on its long neck, snaps shut
    with a flash, and draws back."""
    reach, jaw, head, flash, fin = ((-1, 3, 2, 0, 2), (4, 0, 0, 2, 2), (4, 0, 0, 1, 1), (2, 0, 0, 0, 1),
                                    (0, 0, 1, 0, 1), (-1, 1, 2, 0, 2))[k]
    g = _grid(56, 28)
    _paste(g, neck(jaw=jaw, head=head, reach=reach, fin=fin, eyes="closed" if flash == 2 else "open",
                   step=1 if reach > 0 else 0), 5, 26 - FEET)
    if flash:                                                    # the snap: sparks round the teeth
        cx, cy = 12 - reach, 26 - FEET + 10 - head
        col = "#fff" if flash == 2 else "#fd3"
        for dx, dy in ((-3, 0), (-2, -2), (-2, 2), (0, -3), (0, 3)) if flash == 2 else ((-4, 0), (-3, -3), (-3, 3)):
            _dot(g, cx + dx, cy + dy, col)
            if flash == 2:
                _dot(g, cx + 2 * dx // 3 - (dx < 0), cy + 2 * dy // 3, "#fd3")
    return g


# ---- 5. sunbathing on a stone ---------------------------------------------------------------------------------

STONE = {"s": "#889", "d": "#667", "h": "#aab", "m": "#6a4", "y": "#fd3", "o": "#fa3", "w": "#38a", "W": "#7bf",
         "G": "#4c5", "g": "#294", "p": "#f9c", "P": "#fde", "c": "#fd3"}


def sunbath(k, n=6):
    """A Neck lies flat on a warm stone by the water with its eyes shut, its crest rising and falling; the lily
    pads in the water open a flower; the sun glows in the corner."""
    g = _grid(60, 36)
    c = A.Canvas(56, 8)                                          # the water in front, its lily pads
    c.paint(c.rect(0, 2, 56, 8), "w")
    for x in range(3, 56, 7):
        c.set(x + k % 2, 4 + x % 3, "W")
    for x, y, r in ((8, 4.5, 3.2), (20, 5.5, 2.6), (46, 4.8, 3)):
        pad = c.ellipse(x, y, r, r * 0.45)
        c.paint(pad, "G")
        c.paint(pad & ~c.ellipse(x, y - 0.5, r, r * 0.45), "g")
        c.paint(pad & c.triangle((x, y), (x + 1.5, y - 2), (x + 2.6, y - 2)), "w")
    bloom = (0, 1, 2, 3, 3, 2)[k]                                # the water lily opens
    fx, fy = 10, 3
    c.set(fx, fy, "p").set(fx, fy - 1, "p")
    if bloom >= 1:
        c.set(fx - 1, fy, "p").set(fx + 1, fy, "p")
    if bloom >= 2:
        c.set(fx - 1, fy - 1, "P").set(fx + 1, fy - 1, "P").set(fx, fy - 2, "P")
    if bloom >= 3:
        c.set(fx - 2, fy, "p").set(fx + 2, fy, "p").set(fx, fy - 1, "c")
    s = A.Canvas(44, 9)
    rock = (s.ellipse(22, 6, 21.5, 5.6) | s.ellipse(31, 4, 9, 4)) & s.rect(0, 0, 44, 9)
    s.paint(rock, "s")
    s.paint(rock & ~s.ellipse(21, 3.5, 22, 5.2), "d")
    s.paint(s.ellipse(14, 2.2, 8, 0.9) | s.ellipse(33, 1.4, 3, 0.7), "h")
    for x, y in ((6, 5), (7, 6), (24, 6), (25, 7), (37, 5)):     # cracks
        s.set(x, y, "d")
    s.paint(rock & (s.ellipse(4, 3.5, 2.5, 1.4) | s.ellipse(40, 6.5, 2.2, 1.2)), "m")    # moss
    _paste(g, _prop(s, STONE), 8, 22)
    _paste(g, neck(eyes="closed", legs=False, head=-1, fin=(0, 1, 2, 2, 1, 0)[k], tail=(1, 1, 0, 0, 0, 1)[k]), 6,
           24 - FEET)
    _paste(g, _prop(c, STONE), 1, 28)
    sun = A.Canvas(9, 9)
    sun.paint(sun.disc(4.5, 4.5, 3), "y")
    sun.paint(sun.disc(5.2, 5.2, 1.6), "o")
    _paste(g, _prop(sun, STONE), 49, 0)
    rays = ((5, 0), (5, 10), (0, 5), (10, 5)) if k % 2 == 0 else ((1, 1), (9, 1), (1, 9), (9, 9))
    for x, y in rays:
        _dot(g, 49 + x, y, "#fd3")
    return g


# ---- 6. a baby Neck hatching ----------------------------------------------------------------------------------

EGG = {**NECK, "e": "#eed", "f": "#bb9", "p": "#8b5", "n": "#863", "t": "#6a3"}
EGG_RX, EGG_RY = 6, 7.2


def _baby(c, x, y, bottom, eyes="open", tongue=False):
    """A baby Neck in profile facing left, its head's centre at (x, y) (whole numbers): a short thick neck down into
    the egg to row bottom, a pale throat, a row of little spines down the back of the head and neck."""
    neckm = c.line(x + 1.6, y + 0.5, x + 2.2, bottom, 3.6)
    headm = c.ellipse(x + 0.6, y, 2.8, 1.9) | c.ellipse(x - 1.8, y + 0.5, 1.8, 1.2)
    c.paint(neckm | headm, "B")
    c.paint(neckm & ~headm & ~np.roll(neckm | headm, 1, axis=1), "L")
    for yy in range(y - 1, bottom - 4, 2):                       # the crest, down to the shell
        xs = np.nonzero(c.g[yy] != "")[0]
        if len(xs):
            c.set(int(xs.max()) + 1, yy, "F")
    c.paint(c.rect(x - 3, y + 1, x + 1, y + 2), "M")
    c.set(x - 2, y + 1, "W")
    c.set(x, y - 1, "S" if eyes == "closed" else "E")
    if tongue:
        c.set(x - 4, y + 1, "T").set(x - 5, y + 1, "T").set(x - 6, y, "T").set(x - 6, y + 2, "T")


def _cap(c, x, y, deg):
    """The egg's cracked-off top, its rim's middle at (x, y), turned by deg (screen degrees: > 0 tips it back)."""
    a = np.radians(deg)
    dx, dy = c.px - x, c.py - y
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a) - 1                     # the egg's own frame, its centre 1 px below the rim
    col = np.floor(u + 13) % 4                                   # the crack lined up with the lower shell's (cx 13)
    zig = -1 + (col == 1) * 1.0 - (col == 3) * 1.0
    return ((u / EGG_RX) ** 2 + (v / EGG_RY) ** 2 <= 1) & (v < zig)


def hatch(k, n=8):
    """A baby Neck pokes its head out of its cracked egg wearing the shell's top tipped back like a cap, blinks,
    flicks its tongue, ducks back in under the cap, the egg wobbles, and it bursts out again, the cap flying up."""
    head, cap, eyes, tongue = ((0, ("head", 4, 35), "open", False), (0, ("head", 4, 35), "closed", False),
                               (0, ("head", 4, 40), "open", True), (-4, ("egg", 0, 3, 0), "open", False),
                               (None, ("egg", 0, 0, 0), "open", False), (None, ("egg", -1, 0, -8), "open", False),
                               (None, ("egg", 1, 0, 8), "open", False), (1, ("egg", 4, 14, -20), "open", False))[k]
    c = A.Canvas(28, 34)
    cx, cy = 13, 25
    for x0, x1 in ((5, 3), (8, 7), (18, 20), (21, 23)):          # the grass nest
        c.paint(c.line(x0, 33, x1, 29, 1.2), "t")
    c.paint(c.ellipse(13, 32.5, 9, 1.6), "n")
    egg = c.ellipse(cx, cy, EGG_RX, EGG_RY)
    zig = cy - 1 + (c.xx % 4 == 1) * 1.0 + (c.xx % 4 == 3) * -1.0
    lower = egg & (c.yy >= zig)
    hx, hy = cx - 1, cy - 6 - (head or 0)
    if head is not None:
        _baby(c, hx, hy, cy + 2, eyes=eyes, tongue=tongue)
    c.paint(lower, "e")
    for x, y in ((10, 27), (15, 29), (17, 25), (11, 30)):
        c.set(x, y, "p")
    if cap[0] == "head":                                         # tipped back on the head
        top = _cap(c, hx + cap[1], hy - 1.5, cap[2])
    else:                                                        # on the egg, lifted, wobbling or flying
        top = _cap(c, cx + cap[1], cy - 1 - cap[2], cap[3])
    c.paint(top, "e")
    c.paint(top & ~np.roll(top, -1, axis=0), "f")                 # its broken rim
    c.paint(top & np.roll(top, -1, axis=0) & np.roll(top, 1, axis=0) & np.roll(top, 1, axis=1)
            & ((c.xx * 3 + c.yy * 2) % 9 == 0), "p")
    if head is None and k == 4:
        c.paint(lower & (c.yy == cy) & (c.xx >= 10) & (c.xx <= 12), "f")
    return _prop(c, EGG)


# ---- 7. catching a fly ----------------------------------------------------------------------------------------

def fly_catch(k, n=8):
    """A Neck follows a fly looping in front of its nose with its head up, shoots out its forked tongue, catches it,
    gulps it down with its eyes shut, and the next fly arrives."""
    fx, fy, eyes, tongue, head = ((8, 4, "up", 0, 3), (13, 1, "up", 0, 4), (6, 6, "up", 0, 3), (5, 11, "open", 0, 1),
                                  (None, None, "open", 9, 1), (None, None, "open", 4, 1), (None, None, "closed", 0, 2),
                                  (None, None, "closed", 0, 2))[k]
    g = _grid(60, 30)
    nk = neck(eyes=eyes, tongue=tongue, head=head, fin=2 if eyes == "closed" else 1)
    _paste(g, nk, 8, 29 - FEET)
    my = 29 - FEET + 10 - head                                   # the mouth's row
    if fx is not None:
        _paste(g, _fly(k), fx, fy)
    elif tongue:
        _paste(g, _fly(k), 13 - tongue - 4, my - 1)
    if k == 7:                                                   # the next fly, far away
        _paste(g, _fly(k), 0, 0)
    return g


def _fly(k):
    """A green bottle fly, its wings up or down by k."""
    c = A.Canvas(4, 3)
    c.paint(c.rect(0, 1, 3, 3), "b")
    c.set(0, 1, "r")
    c.set(1 + k % 2, 0, "w").set(2 + k % 2, 0, "w")
    return _prop(c, {"b": "#4a7", "r": "#e33", "w": "#eff"})


# ---- 8. swimming ----------------------------------------------------------------------------------------------

def swim(k, n=6):
    """A Neck swims like a little sea serpent: its long neck and head ride high above the surface, its crest cuts
    the water, its body and paddling legs show through it, its tail sweeps, and small waves run off behind."""
    g = _grid(60, 30)
    bob = (0, 0, 1, 1, 1, 0)[k]
    _paste(g, neck(step=(-1, -1, 0, 1, 1, 0)[k], tail=(2, 3, 2, 1, 0, 1)[k], head=1, fin=1 + bob), 6,
           26 - FEET + bob)
    wy = 17                                                      # the surface; under it the water's band
    for y in range(wy, wy + 6):
        half = 29 - 1.2 * (y - wy) ** 1.5
        for x in range(60):
            if abs(x - 30.5) <= half:
                if y >= wy + 3:
                    g[y, x] = "#37b" if g[y, x] == "" else "#4a9"
                else:
                    g[y, x] = "#48c" if g[y, x] == "" else "#5ab"
    g[wy + 6:, :] = ""                                          # deeper down the water hides it
    for x in range(60):                                         # glints running along the surface
        if g[wy, x] == "#48c" and (x + k * 2) % 7 < 2:
            g[wy, x] = "#9df"
    for i in range(2):                                          # the wake: waves spreading back from the tail
        r = (2 * k + 6 * i) % 12
        for x in range(46 + r // 2, 49 + r // 2):
            _dot(g, x, wy - 1, "#cef" if i == 0 else "#9df")
        _dot(g, 6 - r // 3, wy - 1, "#cef")
    return g


# ---- 9. asleep ------------------------------------------------------------------------------------------------

def sleep(k, n=4):
    """A Neck asleep, lying flat on the ground with its head laid down in front of it, eyes shut; its body swells
    and settles with its breath, its crest lies down, and z's drift up."""
    g = _grid(56, 34)
    nk = neck(eyes="closed", legs=False, head=-6, reach=-2, fin=0, curl=3, tail=-1, breath=(0, 0.6, 1, 0.6)[k])
    _paste(g, nk, 0, 33 - feet(nk))
    for i in range(2):                                           # z's drift up and grow
        t = (k + 2 * i) % 4
        x, y = 10 + 3 * t, 20 - 3 * t
        size = 4 if t < 2 else 5
        for j in range(size):
            _dot(g, x + j, y, "#cdf")
            _dot(g, x + j, y + size - 1, "#cdf")
        for j in range(1, size - 1):
            _dot(g, x + size - 1 - j, y + j, "#cdf")
    return g


# ---- 10. the cat pokes a Neck ---------------------------------------------------------------------------------

def _cat(t, blink=False, look=True):
    """anim2's ginger cat (30 x 26) with its pupils turned right."""
    g = anim2.frame(t, blink=blink)
    if look and not blink:
        for x in (8, 15):
            g[8:11, x:x + 2] = "G"
            g[8:11, x + 1] = "P"
            g[8, x] = "Wt"
    return np.asarray(anim2.colours(g), dtype=object)


def poke(k, n=8):
    """The ginger cat pokes a Neck on the nose with one paw: the Neck pulls its head back on its long neck with its
    eyes shut and its crest up, then stretches out again and flicks its tongue back at the cat while the cat's tail
    swishes."""
    reach, eyes, fin, tongue, head, blink = ((1.0, "closed", 2, 0, 3, False), (0.5, "open", 2, 0, 2, False),
                                             (0.0, "open", 1, 0, 1, False), (0.0, "open", 1, 4, 0, False),
                                             (0.0, "open", 1, 0, 0, True), (0.0, "open", 1, 0, 1, False),
                                             (0.3, "up", 1, 0, 1, False), (0.7, "open", 1, 0, 0, False))[k]
    g = _grid(70, 30)
    _paste(g, _cat(k / n, blink=blink), 0, 3)
    _paste(g, neck(eyes=eyes, fin=fin, tongue=tongue, head=head, reach=-3 if k == 0 else (-1 if k == 1 else 0),
                   curl=9, tail=5), 17, 29 - FEET)
    if reach:
        c = A.Canvas(14, 7)
        x1, y1 = 2 + 9 * reach, 4.5 - 1.5 * reach
        c.paint(c.line(1, 5, x1, y1, 2.6), "O")
        c.paint(c.disc(x1 + 0.4, y1, 1.5), "Wt")
        _paste(g, _prop(c, anim2.PAL), 16, 15)
    if k == 0:                                                   # the poke
        for x, y in ((28, 15), (30, 13), (32, 15)):
            _dot(g, x, y, "#fd3")
    return g


# ---- extras ---------------------------------------------------------------------------------------------------

def flower(k, n=4):
    """A Neck holds a yellow flower in its mouth by the stem, head up; the flower sways and the Neck blinks."""
    sway = (0, 1, 0, -1)[k]
    g = _grid(56, 30)
    _paste(g, neck(head=2, eyes="closed" if k == 3 else "open", tail=(1, 2, 1, 0)[k]), 8, 29 - FEET)
    c = A.Canvas(10, 12)
    mx, my = 7, 9                                                # where the stem goes into the mouth
    c.paint(c.line(mx, my, mx - 3, my - 3, 1) | c.line(mx - 3, my - 3, mx - 3 + sway, my - 6, 1), "g")
    c.set(mx - 2, my - 2, "G").set(mx - 4, my - 4, "G")
    fx, fy = mx - 3 + sway, my - 7
    for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)):
        c.set(fx + dx, fy + dy, "Y" if dx * dy == 0 else "y")
    c.set(fx, fy, "o")
    _paste(g, _prop(c, {"g": "#294", "G": "#4c5", "Y": "#fe4", "y": "#fc2", "o": "#e82"}), 8,
           29 - FEET + 10 - 2 - my)
    return g


FIRE = {"l": "#642", "L": "#975", "s": "#865", "m": "#a63", "M": "#c85", "c": "#542", "f": "#e52", "F": "#fa3",
        "y": "#fe7", "r": "#999"}


def spit(k, n=4):
    """A Neck tail roasting on a spit over a campfire: the tail turns, its grill marks rolling round, and the
    flames flicker."""
    c = A.Canvas(30, 26)
    for x in (4, 25):                                           # the forked sticks
        c.paint(c.line(x, 25, x, 11, 1.4), "s")
        c.set(x - 1, 10, "s").set(x + 1, 10, "s")
    c.paint(c.line(2, 11, 28, 11, 1.2), "s")
    flame = [(15, 13.5), (11, 16), (19, 15.5)]
    for i, (fx, fy) in enumerate(flame):
        h = 5 + ((k + i) % 3)
        c.paint(c.triangle((fx - 2.5, 22), (fx + 2.5, 22), (fx + (k + i) % 2 - 0.5, 22 - h)), "f")
        c.paint(c.triangle((fx - 1.2, 22), (fx + 1.2, 22), (fx, 22 - h * 0.55)), "F")
    c.paint(c.ellipse(15, 21.5, 3, 1.2), "y")
    c.paint(c.line(7, 23, 23, 25, 2) | c.line(7, 25, 23, 23, 2), "l")
    c.paint(c.disc(7, 23.6, 0.8) | c.disc(23, 23.6, 0.8), "L")
    for x in (5, 9, 21, 25):
        c.set(x, 25, "r")
    tail = c.line(10, 11, 15, 11, 5) | c.line(15, 11, 20, 11.3, 3.4) | c.line(20, 11.3, 23, 11.6, 2)
    c.paint(tail, "m")                                          # the tail, the thick end left
    for i in range(3):                                          # grill marks roll round as it turns
        x = 11 + 4 * i + k
        c.paint(tail & (c.line(x, 8, x - 2, 14, 1) | c.line(x + 1, 8, x - 1, 14, 0.5)), "c")
    c.paint(tail & ~np.roll(tail, 1, axis=0) & c.rect(0, 0, 30, 11), "M")
    c.paint(c.line(2, 11, 9, 11, 1.2) | c.line(23, 11, 28, 11, 1.2), "s")
    g = _prop(c, FIRE)
    for i in range(2):                                          # sparks rise
        y = 7 - (k * 2 + i * 4) % 8
        _dot(g, 13 + 4 * i + (k + i) % 2, y, "#fd3")
    return g


# ---- pieces ---------------------------------------------------------------------------------------------------

def _frames(fn, n):
    return _crop([np.asarray(fn(k), dtype=object) for k in range(n)])


def pieces():
    return [
        A.Piece("Neck in the muck (moving, shuffling)", "pixel", frames=_frames(muck, 4), fps=5),
        A.Piece("Neck in the rain (moving, blinking)", "pixel", frames=_frames(rain, 6), fps=5),
        A.Piece("Neck stars (moving, snapping in turn)", "pixel", frames=_frames(trio, 6), fps=4),
        A.Piece("Neck bite (moving, snapping)", "pixel", frames=_frames(bite, 6), fps=6),
        A.Piece("Sunbathing neck (moving, water lily opening)", "pixel", frames=_frames(sunbath, 6), fps=3),
        A.Piece("Neck egg (moving, hatching)", "pixel", frames=_frames(hatch, 8), fps=4),
        A.Piece("Neck and fly (moving, tongue)", "pixel", frames=_frames(fly_catch, 8), fps=5),
        A.Piece("Swimming neck (moving, paddling)", "pixel", frames=_frames(swim, 6), fps=4),
        A.Piece("Sleeping neck (moving, breathing)", "pixel", frames=_frames(sleep, 4), fps=2),
        A.Piece("Cat and neck (moving, poking)", "pixel", frames=_frames(poke, 8), fps=4),
        A.Piece("Neck with flower (moving, swaying)", "pixel", frames=_frames(flower, 4), fps=3),
        A.Piece("Neck tail on a spit (moving, roasting)", "pixel", frames=_frames(spit, 4), fps=6),
    ]
