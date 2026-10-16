"""Art library: small animated pixel scenes from Valheim's world (2026-10-14).

Each scene is only its objects, their light and their smoke on a transparent background, so the texts stay
small. Grids hold '#rgb' colours directly; solid parts get the cat's dark outline, while sparks, smoke, rain
and snow are drawn after it, unoutlined, into the empty pixels around. Every motion is a whole cycle over the
loop (phases k / n), so the last frame leads back into the first.
"""
import numpy as np

import artlib as A

K = A.OUTLINE
TAU = 2 * np.pi


# ---- helpers -------------------------------------------------------------------------------------------------

def outlined(c):
    """Every empty pixel next to the drawing (4 neighbours) becomes the outline, in place (keep a 1 px margin)."""
    g = c.g
    filled = g != ""
    pad = np.pad(filled, 1)
    near = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    g[~filled & near] = K
    return c


def empty(c, m):
    """The mask limited to the pixels nothing has been drawn on yet."""
    return m & (c.g == "")


def grid(c):
    return [list(r) for r in c.g]


class Local:
    """A canvas seen in local coordinates: origin (cx, cy), turned by ang (radians, clockwise on screen).
    Masks drawn through it land on the canvas turned and moved."""

    def __init__(self, c, cx, cy, ang=0.0):
        self.c = c
        dx, dy = c.px - cx, c.py - cy
        ca, sa = np.cos(ang), np.sin(ang)
        self.X = ca * dx + sa * dy
        self.Y = -sa * dx + ca * dy

    def ellipse(self, x, y, rx, ry):
        return ((self.X - x) / rx) ** 2 + ((self.Y - y) / ry) ** 2 <= 1

    def disc(self, x, y, r):
        return self.ellipse(x, y, r, r)

    def rect(self, x0, y0, x1, y1):
        return (self.X >= x0) & (self.X < x1) & (self.Y >= y0) & (self.Y < y1)

    def line(self, x0, y0, x1, y1, width=1.0):
        dx, dy = x1 - x0, y1 - y0
        L2 = max(dx * dx + dy * dy, 1e-9)
        t = np.clip(((self.X - x0) * dx + (self.Y - y0) * dy) / L2, 0, 1)
        return (self.X - x0 - t * dx) ** 2 + (self.Y - y0 - t * dy) ** 2 <= (width / 2) ** 2


def flame(c, cx, base, h, w, ph, amp=1.0):
    """A teardrop flame standing on y = base: round bottom w wide, tip h up, swaying with phase ph."""
    v = (base - c.py) / h
    sway = amp * np.sin(TAU * ph + v * 2.6) * v
    x = c.px - cx - sway
    lo = np.clip(1 - ((0.3 - v) / 0.3) ** 2, 0, 1)
    hw = np.where(v < 0.3, w * np.sqrt(lo), w * np.clip(1 - (v - 0.3) / 0.7, 0, 1) ** 0.9)
    return (v >= 0) & (v <= 1) & (np.abs(x) <= hw)


def fire(c, cx, base, h, w, t, amp=1.0, cols=("#d31", "#f82", "#fd5", "#ffe")):
    """Four nested flames, each flickering on its own beat."""
    for i, (k, col) in enumerate(zip((1.0, 0.74, 0.48, 0.25), cols)):
        hh = h * k * (1 + 0.13 * np.sin(TAU * (2 * t + 0.31 * i)))
        c.paint(flame(c, cx + 0.3 * np.sin(TAU * (t + 0.2 * i)), base, hh, w * (0.35 + 0.65 * k), t + 0.17 * i,
                      amp), col)


# ---- 1. campfire ---------------------------------------------------------------------------------------------

def campfire(t):
    """Stones in a ring, two crossed logs, a four-layer flame; sparks drift up."""
    c = A.Canvas(32, 36)
    stones = [(16 + 11 * np.cos(a), 30 + 3.2 * np.sin(a)) for a in np.radians(np.arange(0, 360, 45) + 22)]
    back = [s for s in stones if s[1] < 30]
    front = [s for s in stones if s[1] >= 30]
    for x, y in back:
        c.paint(c.ellipse(x, y, 2.6, 1.9), "#778")
    c.paint(c.ellipse(16, 30.5, 8, 2.2), "#a31")                                          # embers
    c.paint(c.line(7.5, 31.5, 22, 26.5, 2.6), "#743")
    c.paint(c.line(24.5, 31.5, 10, 26.5, 2.6), "#864")
    c.paint(c.disc(7.6, 31.3, 1.4) | c.disc(24.4, 31.3, 1.4), "#ca7")                   # cut ends
    fire(c, 16, 30, 23, 6.2, t, amp=1.2)
    for x, y in front:
        c.paint(c.ellipse(x, y, 2.8, 2.0), "#889")
        c.paint(c.ellipse(x - 0.6, y - 0.8, 1.3, 0.7), "#aab")
    outlined(c)
    for i, (x0, p0) in enumerate(((13, 0.0), (19, 0.33), (16, 0.66), (11, 0.5), (21, 0.15))):
        p = (p0 + t) % 1
        x = int(round(x0 + 1.5 * np.sin(TAU * (p + 0.25 * i))))
        y = int(round(9 - p * 9))
        if c.g[y, x] == "":
            c.set(x, y, "#fd5" if p < 0.5 else "#f83")
    return grid(c)


# ---- 2. portal -----------------------------------------------------------------------------------------------

SWIRL = ["#014", "#126", "#239", "#35c", "#58e", "#8bf", "#cef"]


def portal(t):
    """A standing-stone arch with glowing runes; the opening swirls, two arms turning once a loop."""
    W, H = 36, 46
    c = A.Canvas(W, H)
    cx, top, bot = 18, 15, 44
    outer = (c.rect(3, top, 33, bot) | c.ellipse(cx, top, 15, 13)) & (c.py < bot)
    inner = (c.rect(9, top, 27, bot) | c.ellipse(cx, top, 9, 8)) & (c.py < bot)
    stone = outer & ~inner
    c.paint(stone, "#889")
    c.paint(stone & (c.px > cx) & ~c.rect(27, top, 33, bot) | stone & c.rect(30, 0, 33, bot), "#667")
    for y in (21, 28, 35):                                                       # joints between the blocks
        c.paint(stone & c.rect(3, y, 33, y + 1) & ~c.ellipse(cx, top, 15, 13), "#556")
    for a in (-55, 0, 55):
        r = np.radians(a)
        c.paint(stone & c.line(cx + 8.5 * np.sin(r), top - 7.5 * np.cos(r), cx + 15 * np.sin(r),
                               top - 13 * np.cos(r), 0.9), "#556")
    c.paint(c.rect(1, bot, 35, bot + 1), "#665")                                  # footing
    glow = "#9ef" if t < 0.5 else "#5bf"
    runes = [((5, 17), (5, 20), (6, 18)), ((6, 24), (6, 27), (5, 25)), ((5, 31), (6, 32), (5, 33), (6, 34)),
             ((30, 17), (31, 18), (30, 19), (30, 20)), ((31, 24), (31, 27), (30, 26)), ((30, 31), (30, 34), (31, 32))]
    for rune in runes:
        pts = list(rune)
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            c.paint(c.line(x0 + 0.5, y0 + 0.5, x1 + 0.5, y1 + 0.5, 1.0) & stone, glow)
    # the swirl: polar bands around the opening's centre
    ox, oy = cx, 27
    dx, dy = (c.px - ox) / 9, (c.py - oy) / 15
    r = np.sqrt(dx * dx + dy * dy)
    th = np.arctan2(dy, dx)
    s = 0.5 + 0.5 * np.sin(2 * th + 7 * r - TAU * t)
    v = np.clip(1.15 - r, 0, 1) * 0.7 + s * 0.45
    idx = np.clip((v * (len(SWIRL) - 1)).astype(int), 0, len(SWIRL) - 1)
    for i, col in enumerate(SWIRL):
        c.paint(inner & (idx == i), col)
    outlined(c)
    return grid(c)


# ---- 3. longship ---------------------------------------------------------------------------------------------

def longship(t):
    """A longship with a striped sail and a row of shields, rocking and bobbing; the waves in front roll by."""
    W, H = 64, 46
    c = A.Canvas(W, H)
    ang = np.radians(4.5) * np.sin(TAU * t)
    L = Local(c, 32, 36 + 0.8 * np.cos(TAU * t), ang)
    hull = L.ellipse(0, -3, 25, 6.5) & (L.Y >= -3)
    posts = L.line(22, -2, 26, -11, 2.4) | L.line(-22, -2, -26, -11, 2.4)
    curl = L.disc(26.4, -12, 1.8) | L.disc(-26.4, -12, 1.8)
    c.paint(L.line(0, -3, 0, -34, 1.6), "#653")                                   # mast
    c.paint(L.line(-14, -31, 14, -31, 1.4), "#653")                               # yard
    sail = L.rect(-13, -31, 13, -9) & (L.Y < -9 - 1.5 * np.sin(np.clip((L.X + 13) / 26, 0, 1) * np.pi))
    stripes = (np.floor((L.X + 13) / 4.34) % 2 == 0)
    c.paint(sail, "#eed")
    c.paint(sail & stripes, "#c33")
    c.paint(hull | posts | curl, "#a63")
    c.paint(hull & (L.Y > 0.2) & (L.Y < 1.2), "#742")                             # planks
    c.paint(hull & (L.Y > 2.8) & (L.Y < 3.8), "#742")
    c.paint(curl & L.disc(26.4, -12, 0.7) | curl & L.disc(-26.4, -12, 0.7), "#742")
    for i, x in enumerate(range(-17, 18, 5)):
        c.paint(L.disc(x, -3, 2.0), "#c33" if i % 2 else "#eb4")
        c.paint(L.disc(x, -3, 0.7), "#aab")
    outlined(c)
    # waves in front of the hull: a band of water with crests rolling to the left
    x = c.px
    crest = 39 + 1.2 * np.sin(x * 0.42 + TAU * t) + 0.6 * np.sin(x * 0.9 - TAU * 2 * t)
    water = (c.py >= crest) & (c.py < 44)
    c.paint(water, "#259")
    c.paint(water & (c.py < crest + 1.2), "#4ad")
    c.paint(water & (c.py < crest + 1.2) & (np.sin(x * 0.42 + TAU * t) > 0.75), "#eff")
    c.paint(water & (c.py >= 42) & ((c.xx + 3 * (c.yy % 2)) % 7 == 0), "#4ad")
    return grid(c)


# ---- 4. windmill ---------------------------------------------------------------------------------------------

def windmill(t):
    """A wooden windmill on a stone base; four lattice sails turn a quarter round a loop."""
    W, H = 50, 62
    c = A.Canvas(W, H)
    hx, hy = 25, 22
    tower = c.triangle((14, 60), (36, 60), (25, 4)) & (c.py >= 16)
    tower = tower | c.rect(13, 50, 37, 60)
    c.paint(tower, "#a74")
    c.paint(tower & (c.px > 27), "#863")
    c.paint(tower & (c.yy % 4 == 0) & (c.py < 50), "#753")                       # planks
    c.paint(c.rect(12, 50, 38, 61), "#889")                                        # stone base
    c.paint(c.rect(12, 50, 38, 61) & ((c.yy - 50) % 4 == 3), "#667")
    c.paint(c.rect(12, 50, 38, 61) & ((c.xx + 3 * ((c.yy - 50) // 4)) % 6 == 0), "#667")
    c.paint(c.rect(22, 52, 28, 61) | c.ellipse(25, 52.5, 3, 2.5) & (c.py > 50), "#542")   # door
    c.paint(c.rect(23, 33, 27, 37), "#fd6")                                         # lit window
    c.paint(c.rect(25, 33, 26, 37) | c.rect(23, 35, 27, 36), "#542")
    c.paint(c.triangle((11, 17), (39, 17), (25, 6)), "#744")                        # cap
    c.paint(c.triangle((11, 17), (39, 17), (25, 6)) & (c.px > 25), "#633")
    for b in range(4):
        L = Local(c, hx, hy, TAU * (t / 4 + b / 4 + 1 / 8))
        c.paint(L.line(0, 0, 0, -22, 1.4), "#542")                                 # spar
        sail = L.rect(0.6, -22, 5.6, -5)
        c.paint(sail, "#eed")
        c.paint(sail & ((np.abs(L.Y + 13.5) < 0.5) | (np.abs(L.Y + 18) < 0.5) | (np.abs(L.Y + 9) < 0.5)
                        | (np.abs(L.X - 5.1) < 0.5)), "#a85")
    c.paint(c.disc(hx, hy, 2.2), "#653")
    c.paint(c.disc(hx, hy, 0.9), "#aab")
    outlined(c)
    return grid(c)


# ---- 5. smelter ----------------------------------------------------------------------------------------------

def smelter(t):
    """A clay furnace with bellows; the mouth glows and flickers, smoke puffs rise and spread from the top."""
    W, H = 32, 56
    c = A.Canvas(W, H)
    body = c.triangle((6, 54), (26, 54), (16, 10)) & (c.py >= 22)
    body = body | c.ellipse(16, 51, 10.5, 4)
    c.paint(body, "#b75")
    c.paint(body & (c.px > 19), "#964")
    for y in (31, 39, 47):
        c.paint(body & (np.abs(c.py - y - 0.03 * (c.px - 16) ** 2) < 0.6), "#853")   # clay rings
    c.paint(c.rect(12, 17, 20, 23), "#a65")                                          # chimney
    c.paint(c.rect(17, 17, 20, 23), "#854")
    c.paint(c.rect(11, 16, 21, 18), "#753")
    c.paint(c.ellipse(16, 51, 4.2, 3.6) & (c.py < 53.5), "#311")                    # the mouth
    glow = 0.5 + 0.5 * np.sin(TAU * 2 * t)
    c.paint(c.ellipse(16, 51.5, 3.2, 2.6 + 0.4 * glow) & (c.py < 53.5), "#e52")
    c.paint(c.ellipse(16, 52.2, 1.9, 1.4 + 0.5 * glow) & (c.py < 53.5), "#fc4")
    squeeze = 0.5 + 0.5 * np.cos(TAU * t)                                            # bellows on the left
    c.paint(c.triangle((1.5, 42 - 3 * squeeze), (1.5, 50 + 1 * squeeze), (8, 47)), "#754")
    c.paint(c.line(1.5, 42 - 3 * squeeze, 1.5, 50 + squeeze, 1.2), "#a85")
    c.paint(c.line(6, 47, 9, 47, 1.2), "#556")
    outlined(c)
    for i in range(4):                                                               # smoke puffs
        p = (t + i / 4) % 1
        y = 14 - p * 13
        x = 16 + 1.5 * np.sin(TAU * (p + i * 0.3)) + p * 4
        r = 1.6 + p * 2.6
        col = "#bbb" if p < 0.35 else "#999" if p < 0.7 else "#777"
        m = empty(c, c.disc(x, y, r))
        c.paint(m, col)
        c.paint(m & c.disc(x - r * 0.35, y - r * 0.35, r * 0.5), "#ddd" if p < 0.5 else "#aaa")
    return grid(c)


# ---- 6. wall torch -------------------------------------------------------------------------------------------

def wall_torch(t):
    """An iron sconce on a wall plate holds a torch; the flame flickers and sways."""
    W, H = 18, 34
    c = A.Canvas(W, H)
    c.paint(c.rect(7, 23, 11, 32), "#653")                                         # wall plate
    c.paint(c.rect(7, 23, 11, 32) & (c.px > 9.5), "#542")
    c.paint(c.line(9, 22, 9, 30, 2.2), "#863")                                     # the stick
    c.paint(c.rect(7, 17, 11, 22), "#542")                                         # wrapped head
    c.paint(c.rect(7, 18, 11, 19) | c.rect(7, 20, 11, 21), "#321")
    c.paint(c.rect(6, 26, 12, 27), "#556")                                         # iron ring
    c.paint(c.rect(8.5, 26, 9.5, 28), "#556")
    fire(c, 9, 18, 16, 3.6, t, amp=0.9)
    outlined(c)
    for i, (x0, p0) in enumerate(((8, 0.0), (10, 0.5))):
        p = (p0 + t) % 1
        x, y = int(round(x0 + np.sin(TAU * (p + i * 0.5)))), int(round(3 - p * 2.5))
        if c.g[y, x] == "":
            c.set(x, y, "#fd5")
    return grid(c)


# ---- 7. northern lights --------------------------------------------------------------------------------------

def aurora(t):
    """Snowy peaks under a green curtain of northern lights that ripples sideways; a few stars twinkle."""
    W, H = 64, 42
    c = A.Canvas(W, H)
    x = c.px
    ridge = np.minimum(np.minimum(41 - 0.9 * np.abs(x - 20) * 1.15, 41 - 0.85 * np.abs(x - 46)) + 12,
                       41) - 0.0
    peak_a = 41 - 23 + 0.95 * np.abs(x - 20)
    peak_b = 41 - 19 + 0.85 * np.abs(x - 46)
    top = np.minimum(peak_a, peak_b)
    mount = (c.py >= top) & (c.py < 41)
    c.paint(mount, "#345")
    c.paint(mount & (((x > 20) & (x < 33)) | (x > 46)), "#234")                   # shaded flanks
    c.paint(mount & (c.py < top + 3) & (c.py < 26 + 0 * ridge), "#dde")            # snow caps
    c.paint(mount & (c.py < 22) & (c.py < top + 3) & (x > 20) & (x < 24), "#aab")
    outlined(c)
    # the curtain: columns hanging from a wavy line, bright at the bottom, dotted towards the top
    for col in range(1, W - 1):
        xs = col + 0.5
        base = 16 + 3 * np.sin(0.16 * xs + TAU * t) + 2 * np.sin(0.07 * xs - TAU * t)
        length = 7 + 3 * np.sin(0.27 * xs - TAU * 2 * t)
        for row in range(int(base - length), int(base) + 1):
            if not (0 < row < H - 1) or c.g[row, col] != "":
                continue
            d = base - row
            if d < 1.5:
                key = "#8fb"
            elif d < 4:
                key = "#3d8"
            elif (row + col) % 2 == 0:
                key = "#2a7" if d < length * 0.8 else "#a6e" if (row * 3 + col) % 4 == 0 else None
            else:
                continue
            if key:
                c.g[row, col] = key
    for i, (sx, sy) in enumerate(((5, 3), (14, 6), (29, 2), (40, 5), (52, 2), (60, 8), (35, 9), (9, 11))):
        if (i + int(t * 4)) % 3 and c.g[sy, sx] == "":
            c.set(sx, sy, "#ffd")
    return grid(c)


# ---- 8. snowy cabin ------------------------------------------------------------------------------------------

def cabin(t, n):
    """A log cabin with a snow-laden roof and a warm window; snow falls past it, drifting from side to side."""
    W, H = 48, 44
    c = A.Canvas(W, H)
    walls = c.rect(8, 22, 40, 41)
    c.paint(walls, "#963")
    c.paint(walls & (c.yy % 3 == 0), "#742")                                       # logs
    c.paint(c.disc(7.5, 23.5, 1.4) | c.disc(7.5, 26.5, 1.4) | c.disc(7.5, 29.5, 1.4) | c.disc(7.5, 32.5, 1.4)
            | c.disc(7.5, 35.5, 1.4) | c.disc(7.5, 38.5, 1.4), "#ca7")              # log ends
    c.paint(c.rect(26, 29, 33, 41), "#531")                                        # door
    c.paint(c.rect(29, 29, 30, 41), "#421")
    flick = "#fd6" if int(t * n) % 3 else "#fc4"
    c.paint(c.rect(13, 28, 20, 34), flick)                                         # window
    c.paint(c.rect(16, 28, 17, 34) | c.rect(13, 30, 20, 31), "#531")
    roof = c.triangle((3, 24), (45, 24), (24, 6))
    c.paint(roof, "#754")
    c.paint(roof & ((c.yy + c.xx) % 4 == 0) & (c.py > 12), "#643")
    snow = c.triangle((2, 21), (46, 21), (24, 3.5)) & ~c.triangle((6, 23), (42, 23), (24, 7.5))
    c.paint(snow | c.ellipse(24, 6, 3, 2), "#eef")
    c.paint(snow & (c.px > 24), "#ccd")
    for x0 in (8, 15, 33, 39):                                                     # icicles
        c.paint(c.line(x0 + 0.5, 23, x0 + 0.5, 25.5 + (x0 % 3) * 0.5, 0.9), "#bdf")
    c.paint(c.ellipse(24, 42.5, 22, 2.2), "#eef")                                  # drift on the ground
    outlined(c)
    P = 12                                                                         # snow pattern height
    flakes = [(3, 0), (11, 5), (19, 2), (27, 8), (35, 4), (43, 10), (7, 9), (23, 11), (31, 1), (39, 7), (15, 6),
              (46, 3)]
    for i, (fx, fy) in enumerate(flakes):
        for rep in range(0, H, P):
            y = rep + (fy + t * P) % P
            x = fx + 1.2 * np.sin(TAU * (t + i / 5))
            xi, yi = int(round(x)), int(y)
            if 0 <= yi < H - 3 and 0 <= xi < W and c.g[yi, xi] == "":
                c.set(xi, yi, "#fff")
    return grid(c)


# ---- 9. swamp storm ------------------------------------------------------------------------------------------

def swamp(t, flash):
    """A dead swamp tree on a muddy hummock in slanting rain; on the flash frame lightning strikes behind it."""
    W, H = 44, 50
    c = A.Canvas(W, H)
    if flash:
        c.paint(c.line(30, 1, 27, 10, 1.4) | c.line(27, 10, 33, 16, 1.4) | c.line(33, 16, 29, 27, 1.4)
                | c.line(33, 16, 39, 22, 1.0), "#ffe")
    bark, shade, mud, pool = ("#998", "#776", "#786", "#9bc") if flash else ("#554", "#332", "#453", "#365")
    c.paint(c.ellipse(22, 47, 19, 3.5), mud)
    c.paint(c.ellipse(30, 47.5, 7, 1.2), pool)
    trunk = c.triangle((17, 46), (27, 46), (21, 14)) | c.ellipse(22, 45, 6.5, 2)
    trunk = trunk | c.line(21, 30, 9, 20, 2.2) | c.line(9, 20, 6, 12, 1.6) | c.line(9, 20, 4, 21, 1.2)
    trunk = trunk | c.line(22, 24, 33, 14, 2.0) | c.line(33, 14, 34, 7, 1.4) | c.line(33, 14, 39, 13, 1.2)
    trunk = trunk | c.line(21, 15, 18, 6, 1.4) | c.line(28, 19, 26, 12, 1.0)
    c.paint(trunk, bark)
    c.paint(trunk & ((c.px > 22) & (c.py > 24) | (c.xx % 5 == 0) & (c.py > 32)), shade)
    c.paint(c.line(15, 46, 12, 47.5, 1.0) | c.line(27, 46, 31, 47.5, 1.0), shade)       # roots
    outlined(c)
    rain = "#cde" if flash else "#79a"
    P = 10
    drops = [(2, 0), (9, 6), (16, 3), (23, 8), (30, 1), (37, 5), (44, 2), (6, 4), (20, 9), (34, 7), (41, 0), (12, 1),
             (27, 4), (47, 6)]
    for dx0, dy0 in drops:
        for rep in range(-P, H, P):
            y = rep + (dy0 + t * P * 2) % P
            x = dx0 - (y - rep) * 0.4 - rep * 0.4
            for k in range(3):
                xi, yi = int(round(x - k * 0.4)), int(round(y - k))
                if 0 <= yi < H - 4 and 0 <= xi < W and c.g[yi, xi] == "":
                    c.set(xi, yi, rain)
    return grid(c)


# ---- 10. fishing float ---------------------------------------------------------------------------------------

def float_(t):
    """A red and white fishing float on its line bobs in the water; rings of ripples spread from it."""
    W, H = 40, 22
    c = A.Canvas(W, H)
    bob = 1.2 * np.sin(TAU * t)
    cx, wy = 20, 14.5
    for k in range(2):                                                           # ripples, behind the float
        p = (t + k / 2) % 1
        rx, ry = 4 + p * 14, 1.2 + p * 2.6
        ring = c.ellipse(cx, wy + 0.5, rx, ry) & ~c.ellipse(cx, wy + 0.5, rx - 1.6, ry - 1.0)
        c.paint(ring, "#8ce" if p < 0.5 else "#58a")
    above = c.py < wy + 0.6                                                       # the water hides the bottom
    body = c.ellipse(cx, wy - 1.5 + bob, 3.4, 4.4) & above
    c.paint(body, "#eee")
    c.paint(body & (c.py < wy - 1.5 + bob), "#d33")
    c.paint(body & (np.abs(c.py - (wy - 3.9 + bob)) < 0.6), "#eee")
    c.paint(c.line(cx, wy - 9.5 + bob, cx, wy - 5 + bob, 1.2), "#d33")              # antenna
    c.paint(c.ellipse(cx, wy + 0.6, 4.4, 0.8), "#bdf")                              # the water ring at the float
    outlined(c)
    c.paint(empty(c, c.line(cx, wy - 9.5 + bob, cx - 9, -6, 0.8)), "#ccc")          # the line, up to the rod
    return grid(c)


# ---- extras --------------------------------------------------------------------------------------------------

def cooking(t):
    """A cooking station over a small fire: a sausage turns on the spit, fat drips and sizzles."""
    W, H = 36, 34
    c = A.Canvas(W, H)
    for x in (5, 31):
        c.paint(c.line(x, 32, x, 11, 1.6), "#753")                                 # posts with forks
        c.paint(c.line(x, 11, x - 2, 8, 1.2) | c.line(x, 11, x + 2, 8, 1.2), "#753")
    c.paint(c.ellipse(18, 31.5, 8, 1.8), "#a31")
    c.paint(c.line(11, 32, 25, 29.5, 2.2), "#743")
    c.paint(c.line(25, 32, 11, 29.5, 2.2), "#864")
    fire(c, 18, 31, 12, 4.4, t, amp=0.8)
    c.paint(c.line(3, 10.5, 33, 10.5, 1.2), "#99a")                                # the spit
    turn = np.sin(TAU * t)
    s = c.ellipse(18, 12, 9, 2.6)
    c.paint(s, "#b53")
    c.paint(s & (np.abs(c.py - (12 + 1.6 * turn)) < 0.6), "#d86")                  # the shine turns round
    c.paint(s & ((c.xx + int(round(2 * turn))) % 5 == 0) & (c.py > 12), "#833")   # grill marks
    c.paint(c.disc(8.5, 12, 1.2) | c.disc(27.5, 12, 1.2), "#a43")                   # tied ends
    c.paint(c.line(3, 10.5, 9, 10.5, 1.2) | c.line(27, 10.5, 33, 10.5, 1.2), "#99a")
    outlined(c)
    for i, (x0, p0) in enumerate(((15, 0.0), (21, 0.5))):                          # fat drops
        p = (p0 + t) % 1
        y = int(round(15.5 + p * 6))
        if c.g[y, x0] == "":
            c.set(x0, y, "#fe8")
    for i, (x0, p0) in enumerate(((12, 0.2), (24, 0.7), (18, 0.45))):              # sizzle puffs
        p = (p0 + t) % 1
        y = int(round(8 - p * 6))
        x = int(round(x0 + np.sin(TAU * (p + i / 3))))
        if c.g[y, x] == "":
            c.set(x, y, "#ccd" if p < 0.5 else "#99a")
    return grid(c)


# ---- the pieces ----------------------------------------------------------------------------------------------

def _anim(fn, n, *a):
    return [fn(k / n, *a) for k in range(n)]


def pieces():
    storm = [swamp(k / 10, k == 0) for k in range(10)]
    return [
        A.Piece("Campfire (moving, flames and sparks)", "pixel", frames=_anim(campfire, 6), fps=6),
        A.Piece("Portal (moving, swirl)", "pixel", frames=_anim(portal, 8), fps=5, pitch=2.5),   # 5: fits the play budget (0.20.167)
        A.Piece("Longship (moving, rocking on waves)", "pixel", frames=_anim(longship, 6), fps=4, pitch=3),
        A.Piece("Windmill (moving, sails)", "pixel", frames=_anim(windmill, 6), fps=5, pitch=3),
        A.Piece("Smelter (moving, smoke)", "pixel", frames=_anim(smelter, 8), fps=5),
        A.Piece("Wall torch (moving)", "pixel", frames=_anim(wall_torch, 4), fps=6),
        A.Piece("Northern lights (moving)", "pixel", frames=_anim(aurora, 6), fps=4, pitch=3),
        A.Piece("Snowy cabin (moving, snowfall)", "pixel", frames=[cabin(k / 6, 6) for k in range(6)], fps=4, pitch=3),
        A.Piece("Swamp storm (moving, rain and lightning)", "pixel", frames=storm, fps=6, pitch=2.5),
        A.Piece("Fishing float (moving, bobbing)", "pixel", frames=_anim(float_, 8), fps=5),
        A.Piece("Cooking station (moving, sausage)", "pixel", frames=_anim(cooking, 6), fps=6),
    ]
