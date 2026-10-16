"""Row 2 (2026-10-08): the third gallery, both modes side by side.

Blocks 1-3 TYPABLE (round9: one glyph per sign, <= 50 characters, anyone can copy-paste them in game).
Blocks 4-6 DETAILED (save edit now, a mod later: many glyphs per sign, no character limit, one stack
per piece via engine.assign_planes). Procedural pieces are seeded, so every build is the same.
"""
import math
import random

from engine import LOADER_TEXT, Art, glyph_box, parse_hex
from concepts.common import ring_of
from concepts.round9 import bee_meadow, boar_crest, first_night


def typable(fn, grid, number):
    """A round 9 concept on this build: same design, cells and commands recomputed for the grid."""
    base = fn()
    a = base.retarget(grid, title=f"{number} · [gépelhető] {base.title}").fit_above(signs_above=2)
    return a


def boar(grid):
    return {"art": typable(boar_crest, grid, 1)}


def bees(grid):
    return {"art": typable(bee_meadow, grid, 2)}


def campfire(grid):
    return {"art": typable(first_night, grid, 3), "loader": LOADER_TEXT}


# ----------------------------------------------------------------------------- 4: aurora over the fjord

def _mix(c0, c1, t):
    a, b = parse_hex(c0), parse_hex(c1)
    return tuple(round(x + (y - x) * t) for x, y in zip(a[:3], b[:3]))


def aurora_fjord(grid):
    rnd = random.Random(9)
    a = Art("g3_aurora_fjord", "4 · [részletes] Északi fény a fjord felett",
            "Kb. 700 hajszálvékony, áttetsző, világító vonás két hullámzó függönyben (zöld → türkiz → lila), "
            "mögötte 110 csillag, alatta sötét (nem világító) hegyek, a tóban a fény tükörképe, a parton egy ház "
            "világító ablakkal.", origin=(0, 0), grid=grid, unlit="path", mode="detailed")
    a.plane_kw = {"grow": 0.4, "res": 0.5}
    water = -62
    # stars first (behind everything)
    for _ in range(110):
        x, y = rnd.uniform(-148, 148), rnd.uniform(-10, 128)
        big = rnd.random() < 0.08
        a.add("★" if big else "●", x, y, h=rnd.uniform(4, 6) if big else rnd.uniform(1.6, 3.0),
              color="#fff8e0" if big else rnd.choice(["#ffffffb0", "#d8e6ffa0", "#fff2d090"]), tag="star")
    curtains = [  # (base y, height, phase, alpha, x range, column step)
        (44, 72, 1.7, 0.55, (-140, 140), 6.5),
        (30, 92, 0.0, 1.0, (-148, 148), 4.6)]
    strokes = []
    for base, height, ph, alpha, (x0, x1), step in curtains:
        x = x0
        while x <= x1:
            bottom = base + 11 * math.sin(x / 38 + ph) + 5 * math.sin(x / 13 + 2 * ph)
            h = height * (0.55 + 0.45 * math.sin(x / 27 + ph) ** 2)
            y = bottom
            while y < bottom + h - 6:
                t = (y - bottom) / h
                rgb = _mix("#4dffa0", "#3fe0d8", t / 0.55) if t < 0.55 else _mix("#3fe0d8", "#a070ff", (t - 0.55) / 0.45)
                al = round(255 * alpha * (0.9 - 0.75 * t))
                xx = x + 1.2 * math.sin(y / 9 + x)
                a.add("|", xx, y + 8, h=16, w=2.4, color=rgb + (min(255, al + 30),), tag="aurora")
                strokes.append((xx, y + 8, rgb, al))
                y += 19
            x += step
    # mountains (lit: dark silhouettes at night), snow caps
    for x, w, h, col in [(-96, 150, 58, "#141e2c"), (-20, 120, 70, "#0f1724"), (70, 170, 54, "#16202f"),
                         (128, 90, 40, "#111a28")]:
        a.add("▲", x, water + h / 2, h=h, w=w, color=col, glow=False, tag="mtn")
    for x, w, h, top in [(-20, 26, 14, 70), (-96, 26, 12, 58)]:
        a.add("▲", x, water + top - h / 2, h=h, w=w, color="#c9d2de", glow=False, tag="snow")
    # longhouse with a glowing window on the shore
    a.add("■", -128, water + 6, h=12, w=24, color="#1a130e", glow=False, tag="house")
    a.add("▲", -128, water + 16, h=10, w=30, color="#21170f", glow=False, tag="house")
    a.add("■", -122, water + 6, h=4, color="#ffcf6a", tag="window")
    # the lake: the aurora's lower half mirrored and squeezed, fainter
    for xx, y, rgb, al in strokes:
        if y < 70 and rnd.random() < 0.55:
            ry = water - 6 - (y - 10) * 0.45
            if ry > -116:
                a.add("|", xx, ry, h=8, w=2.0, color=rgb + (max(30, al // 3),), tag="reflection")
    a.add("■", 0, water - 2, h=2.2, w=300, color="#2a3d52", glow=False, tag="shore")
    return {"art": a.fit_above(signs_above=2)}


# ----------------------------------------------------------------------------- 5: pixel landscape

SKY = [(0, "#1b2550"), (8, "#3b3f7a"), (15, "#9a5a8a"), (20, "#e0806a"), (23, "#f6b06a")]


def _hex(rgb):
    return "#" + "".join(f"{v:02x}" for v in rgb)


def _lerp(c0, c1, t):
    return _hex(_mix(c0, c1, max(0.0, min(1.0, t))))


def meadow_pixels(W=60, H=40, horizon=24):
    px = [[None] * W for _ in range(H)]
    sky = []
    for r in range(horizon):
        col = SKY[-1][1]
        for (r0, c0), (r1, c1) in zip(SKY, SKY[1:]):
            if r0 <= r <= r1:
                col = _lerp(c0, c1, (r - r0) / (r1 - r0))
                break
        sky.append(col)
        px[r] = [col] * W
    sx, sy, R = 44, 15.5, 4.4
    for r in range(horizon):
        for c in range(W):
            d = math.hypot(c - sx, r - sy)
            if d <= R - 1.6:
                px[r][c] = "#fff0b0"
            elif d <= R:
                px[r][c] = "#ffd27a"
            elif d <= R + 1.6:
                px[r][c] = _lerp(px[r][c], "#ffc27a", 0.4)
    for c0, r, n, col in [(5, 10, 13, "#b8708c"), (29, 11, 9, "#d38f9a"), (40, 12, 12, "#c27a90"),
                          (12, 13, 7, "#e3a0a0"), (49, 9, 8, "#9a6090")]:
        for c in range(c0, min(W, c0 + n)):
            px[r][c] = col
    for r, c in [(6, 26), (5, 27), (6, 28), (4, 33), (3, 34), (4, 35)]:
        px[r][c] = "#2a2340"
    far = [17 + 3.5 * math.sin(c / 6.5) + 2 * math.sin(c / 2.7 + 1) for c in range(W)]
    near = [21 + 1.5 * math.sin(c / 9 + 2) + math.sin(c / 4) for c in range(W)]
    for c in range(W):
        for r in range(max(0, round(far[c])), horizon):
            if px[r][c] in ("#fff0b0", "#ffd27a") and r < 18:
                continue                                  # the sun sets behind the ridge, its top still shows
            px[r][c] = "#e6e8f4" if r <= round(far[c]) + (2 if far[c] < 15.6 else 1) and far[c] < 17.2 else "#3e3c6e"
        for r in range(max(0, round(near[c])), horizon):
            px[r][c] = "#21402e"
    for c in [3, 7, 12, 17, 23, 28, 49, 53, 57]:
        top = round(near[c]) - (4 + c % 3)
        for k, r in enumerate(range(top, round(near[c]) + 1)):
            for dc in range(-(k // 2), k // 2 + 1):
                if 0 <= c + dc < W and 0 <= r < horizon:
                    px[r][c + dc] = "#11241a"
    for r in range(horizon, H):
        m = 2 * horizon - 1 - r
        for c in range(W):
            src = px[m][c] if m >= 0 else sky[0]
            col = _lerp(src, "#0d1428", 0.38 + 0.02 * (r - horizon))
            if r % 2:
                col = _lerp(col, "#0a1020", 0.25)
            px[r][c] = col
        w = 2.6 - (r - horizon) * 0.12
        if r % 2 == 0:
            for c in range(W):
                if abs(c - sx) <= w and (c + r) % 3:
                    px[r][c] = "#ffd27a" if (c + r) % 2 else "#f6b06a"
    for r in (horizon, horizon + 1):                     # the shore under the house
        for c in range(0, 22 - (r - horizon) * 3):
            px[r][c] = "#24352c"
    house = ["....RRRR....", "..RRRRRRRR..", ".WWWWWWWWWW.", ".WGWWDDWWGW.", ".WWWWDDWWWW."]
    for k, line in enumerate(house):
        for j, ch in enumerate(line):
            if ch != ".":
                px[horizon - 5 + k][5 + j] = {"R": "#5a3a1e", "W": "#3a2416", "G": "#ffcf6a", "D": "#1a0e08"}[ch]
    for r, c, col in [(18, 13, "#8a8f9c"), (17, 14, "#9aa0ac"), (16, 14, "#a0a4b0"), (15, 15, "#a8acb8"),
                      (14, 16, "#b0b4c0"), (13, 17, "#b8bcc8"), (12, 18, "#c0c4d0")]:
        px[r][c] = col
    return px


def pixel_meadow(grid):
    px = meadow_pixels()
    H, W = len(px), len(px[0])
    pitch = 5.0
    gw, _ = glyph_box("■")
    S = round((pitch + 0.3) / (2 * gw + 4 * 0.0203), 2)          # tiles overlap by 0.3 wu: no seams
    a = Art("g3_pixel_meadow", "5 · [részletes] Pixel-táj: este a fjordnál",
            f"{W}x{H} = {W * H} pixel, világít (pontos színek nappal is): naplemente, havas hegyek, fenyves, a "
            "tóban tükörkép és napcsillogás, a parton füstölgő hosszúház. Hézag nélküli, picit átfedő pixelek, "
            "mind egy síkban, signenként ~130 glyph.", origin=(0, 0), grid=grid, unlit="path", mode="detailed")
    a.plane_kw = {"independent": True}
    x0 = grid.center_x - (W - 1) / 2 * pitch
    y0 = grid.top_y + 2 * 20 + pitch / 2
    for r in range(H):
        for c in range(W):
            a.add("■", x0 + c * pitch, y0 + (H - 1 - r) * pitch, size=S, color=px[r][c])
    return {"art": a}


# ----------------------------------------------------------------------------- 6: Yggdrasil

def _bar(a, x0, y0, x1, y1, width, color, tag, glow=False):
    L = math.hypot(x1 - x0, y1 - y0)
    ang = math.degrees(math.atan2(y1 - y0, x1 - x0)) - 90
    return a.add("■", (x0 + x1) / 2, (y0 + y1) / 2, h=L, w=width, rot=ang, color=color, tag=tag, glow=glow)


def yggdrasil_detailed(grid):
    rnd = random.Random(6)
    a = Art("g3_yggdrasil", "6 · [részletes] Yggdrasil, a világfa",
            "Ágról ágra kirajzolt fa (törzs, 3 főág, 6 mellékág, 12 gally, gyökerek; nem világít), rajta ~170 "
            "világító, áttetsző levél, szentjánosbogarak, körülötte arany gyűrű a kilenc világ rúnáival.",
            origin=(0, 0), grid=grid, unlit="path", mode="detailed")
    wood = ["#7a5532", "#83603a", "#8c6a42", "#96744a"]          # lit: brown by day, a silhouette at night
    _bar(a, 0, -96, 0, -26, 22, wood[0], "trunk")
    for x, ang in ((-8, -140), (0, -95), (8, -40)):                # roots
        r = math.radians(ang)
        _bar(a, x, -90, x + 20 * math.cos(r), -90 + 20 * math.sin(r), 5, wood[0], "root")
    tips = []

    def grow(x, y, ang, length, width, depth):
        r = math.radians(ang)
        x2, y2 = x + length * math.cos(r), y + length * math.sin(r)
        _bar(a, x, y, x2, y2, width, wood[min(depth, 3)], f"branch{depth}")
        if depth == 3:
            tips.append((x2, y2))
            return
        for d in (-26, 26):
            grow(x2, y2, ang + d + rnd.uniform(-6, 6), length * 0.68, width * 0.6, depth + 1)
    for ang, length in ((140, 52), (90, 46), (40, 52)):
        grow(0, -32, ang, length, 12, 1)
    # leaves: never touching each other (one paint level above the wood they rest on)
    leaves = []
    greens = ["#5dff9a", "#3fe0b0", "#7dffcf", "#46d18a", "#9affd0", "#38c8a0"]
    tries = 0
    while len(leaves) < 170 and tries < 20000:
        tries += 1
        if rnd.random() < 0.6 and tips:
            tx, ty = rnd.choice(tips)
            x, y = tx + rnd.gauss(0, 16), ty + rnd.gauss(0, 13)
        else:
            t, rr = rnd.uniform(0, 2 * math.pi), math.sqrt(rnd.random())
            x, y = 100 * rr * math.cos(t), 48 + 74 * rr * math.sin(t)
        h = rnd.uniform(8, 15)
        if math.hypot(x, y - 10) > 122 - h / 2:
            continue
        if any(math.hypot(x - lx, y - ly) < (h + lh) / 2 + 2.5 for lx, ly, lh in leaves):
            continue
        leaves.append((x, y, h))
    for x, y, h in leaves:
        c = parse_hex(rnd.choice(greens))
        a.add("●", x, y, h=h, color=c[:3] + (rnd.choice([0xb0, 0xc8, 0xe0]),), tag="leaf")
    flies = 0
    while flies < 18:
        x, y = rnd.uniform(-115, 115), rnd.uniform(-90, 120)
        if math.hypot(x, y - 10) > 118 or any(math.hypot(x - lx, y - ly) < lh / 2 + 5 for lx, ly, lh in leaves):
            continue
        if abs(x) < 16 and y < -20:
            continue
        a.add("●", x, y, h=rnd.uniform(2.5, 4), color=rnd.choice(["#ffe680", "#fff2a0", "#ffd24a"]), tag="firefly")
        flies += 1
    a.add("◯", 0, 10, h=300, color="#c9963c", tag="ring")
    ring_of(a, "ᚨᚹᚨᛗᛃᛗᚾᛊᚺ", 143, start=90, h=20, color="#2a1a08", cx=0, cy=10, tag="worlds")
    return {"art": a.fit_above(signs_above=2)}


PIECES = [boar, bees, campfire, aurora_fjord, pixel_meadow, yggdrasil_detailed]
