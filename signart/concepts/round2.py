"""Round 2: fixes from the round-1 review plus new pieces (portrait, sunset, serpent, stag)."""
import math

from engine import Art
from concepts.common import *  # noqa: F401,F403


def _raven(a, x, y, s, scale=1.0, tag="raven"):
    """Raven in profile facing s (+1 right, -1 left): tail, raised wing, body, head, beak (5 glyphs)."""
    k = scale
    a.add("◀" if s > 0 else "▶", x - s * 70 * k, y - 8 * k, h=34 * k, w=58 * k, color=DARK, tag=tag)
    a.add("◣" if s > 0 else "◢", x - s * 28 * k, y + 58 * k, h=128 * k, w=150 * k, color=TEAL, tag=tag)
    a.add("●", x, y, h=60 * k, w=132 * k, color=DARK, tag=tag)
    a.add("●", x + s * 58 * k, y + 22 * k, h=50 * k, color=DARK, tag=tag)
    a.add("▶" if s > 0 else "◀", x + s * 96 * k, y + 18 * k, h=22 * k, w=40 * k, color=GOLD, tag=tag)


def odin_eye2():
    a = Art("odin_eye2", "Odin szeme v2", "A v1 javítva: sötét, felszálló hollók faroktollal és csőrrel; a többi változatlan.")
    a.add("◆", 0, 0, h=150, w=300, color=GOLD, tag="eye")
    a.add("●", 0, 0, h=100, w=200, color=BONE, tag="eye")
    a.add("●", 0, 0, h=92, color=CYAN, tag="eye")
    a.add("●", 0, 0, h=42, color=INK, tag="eye")
    a.add("●", 13, 14, h=13, color=WHITE, tag="eye")
    a.add("●", 0, 330, h=150, color=BONE, tag="moon")
    stroke(a, 0, 105, 0, 300, GOLD, tag="spear")
    a.add("◆", 0, 345, h=120, w=44, color=INK, tag="spear")
    for ang, r0, r1 in [(0, 175, 245), (180, 175, 245), (60, 140, 205), (120, 140, 205),
                        (210, 165, 225), (330, 165, 225), (240, 150, 205), (300, 150, 205)]:
        x0, y0 = polar(r0, ang)
        x1, y1 = polar(r1, ang)
        stroke(a, x0, y0, x1, y1, AMBER, tag="ray")
    _raven(a, -250, 170, +1, tag="ravenL")
    _raven(a, 250, 170, -1, tag="ravenR")
    a.add("V", 0, -205, h=170, color=GOLD, tag="V")
    a.add("▼", 0, -170, h=40, color=CYAN, tag="V")
    row(a, "ᛟᛞᛁᚾ", -96, 96, -340, 64, CYAN, tag="runes")
    return a


def vegvisir2():
    a = Art("vegvisir2", "Rúnairánytű v2", origin=(300, 590), idea="Vastagabb arany ágak, nagy csont-színű végződések, ágközépen türkiz dísz; 9 sziget pontosan 9 cellában.")
    a.add("●", 0, 0, h=112, color=INK, tag="hub")
    a.add("⭕", 0, 0, h=142, color=GOLD, tag="hub")
    a.add("ᛝ", 0, 0, h=62, color=CYAN, tag="hub")
    tips = ["ᛉ", "ᚼ", "◆", "ᛦ", "ᛏ", "●", "ᛣ", "ᛟ"]
    mids = ["●", "◆"] * 4
    for k in range(8):
        ang = 90 - 45 * k
        x0, y0 = polar(108, ang)
        x1, y1 = polar(262, ang)
        stroke(a, x0, y0, x1, y1, GOLD, tag=f"arm{k}")
        xm, ym = polar(185, ang)
        a.add(mids[k], xm, ym, h=30, color=CYAN, tag=f"arm{k}")
        xt, yt = polar(300, ang)
        rt = ((ang - 90) + 180) % 360 - 180
        rt = rt if abs(rt) <= 99 else (rt + 360) % 360 - 180   # 3-digit rotate does not fit: turn upright
        a.add(tips[k], xt, yt, h=84 if tips[k] not in "◆●" else 46, rot=rt,
              color=BONE, tag=f"arm{k}")
    ring_of(a, "ᚠᚢᚦᚨᚱᚲᚷᚹᚺᚾᛁᛃ", 380, start=105, h=44, color=CYAN, tag="outer")
    return a


def pixel_viking2():
    a = Art("pixel_viking2", "Pixel viking v2", "36 csempe: ívelt szarvak félcsempékkel, kupolás sisak, fehér szem pupillával (2 réteg), orrvédő, vörös szakáll.")
    S, H, F, N, R, W = STEEL, BONE, "#eb9", "#d97", "#c62", WHITE
    grid = [
        "L.....r",
        "H.dSb.H",
        "HSSSSSH",
        ".SSSSS.",
        ".FESEF.",
        ".RFNFR.",
        ".RRRRR.",
        "..pRq..",
    ]
    glyph = {"L": ("◣", H), "r": ("◢", H), "d": ("◢", S), "b": ("◣", S), "p": ("◥", R), "q": ("◤", R)}
    color = {"H": H, "S": S, "F": F, "N": N, "R": R, "E": W}
    pitch, tile = 72, 60
    for r, line in enumerate(grid):
        for c, ch in enumerate(line):
            if ch == ".":
                continue
            g, col = glyph.get(ch, ("■", color.get(ch)))
            x, y = (c - 3) * pitch, (3.5 - r) * pitch
            a.add(g, x, y, h=tile, w=tile, color=col, tag="px")
            if ch == "E":
                a.add("●", x, y - 4, h=26, color=INK, tag="px")
    return a


def aurora2():
    a = Art("aurora2", "Északi fény v2", "Függöny-szerű, áttetsző fénysávok két szigetben (5+5 réteg), havas hegyek, sarló hold, csillagok, víz.")
    cols = ["#3f85", "#3fa5", "#2fc5", "#5fd5", "#a6f5"]
    for base, ys in ((-200, [150, 175, 160, 185, 150]), (75, [190, 165, 180, 160, 175])):
        for i in range(5):
            a.add("●", base + 46 * i, ys[i], h=178 - 14 * (i % 2), w=56, color=cols[(i + (base > 0)) % 5], tag="curtain")
    a.add("▲", -140, -120, h=190, w=340, color="#235", tag="mtn")
    a.add("▲", 130, -140, h=150, w=270, color="#346", tag="mtn")
    a.add("▲", -140, -46, h=42, w=76, color=WHITE, tag="mtn")
    a.add("▲", 130, -82, h=34, w=62, color=WHITE, tag="mtn")
    a.add("🌙", 300, 420, h=80, color="#ffd", tag="moon")
    for x, y, h in [(-290, 360, 16), (-120, 380, 22), (40, 395, 14), (170, 370, 18), (-300, 40, 14), (300, 60, 16)]:
        a.add("★", x, y, h=h, color="#ffd", tag="star")
    for i in range(6):
        a.add("~", -250 + 100 * i, -255, w=70, color=TEAL, tag="water")
    return a


def fjord_sunset():
    a = Art("fjord_sunset", "Naplemente a fjordon", "Háromrétegű nap, előtte a hajó sziluettje (1 sziget, 5 réteg); alatta retró tükröződő csíkok, oldalt sziklák, sirályok.")
    a.add("●", 0, 70, h=360, color="#f638", tag="sun")
    a.add("●", 0, 70, h=270, color="#f73", tag="sun")
    a.add("●", 0, 70, h=180, color="#fb5", tag="sun")
    a.add("⛵", 0, 10, h=190, color="#211", tag="sun")
    stripes = [(-130, 300, "#f73"), (-152, 250, "#e63"), (-172, 200, "#c54"), (-190, 150, "#a45"),
               (-206, 104, "#845"), (-220, 64, "#636"), (-232, 32, "#526")]
    for y, w, c in stripes:
        a.add("■", 0, y, h=12, w=w, color=c, tag="sea")
    a.add("◢", -300, -10, h=320, w=190, color="#435", tag="cliff")
    a.add("◣", 300, -10, h=320, w=190, color="#435", tag="cliff")
    for x, y, h in [(-170, 300, 40), (-105, 265, 28), (150, 320, 34), (215, 280, 24)]:
        a.add("^", x, y, h=h, w=h * 1.9, color="#211", tag="gull")
    return a


def jormungandr():
    a = Art("jormungandr", "Jörmungandr", "Gyöngysor-kígyó a saját farkába harap a Mjölnir körül: minden pikkely külön sign, bármelyik cellába mehet.")
    # hammer (two islands)
    a.add("■", 0, 60, h=96, w=210, color=STEEL, tag="head")
    a.add("■", 0, 60, h=72, w=186, color="#bbc", tag="head")
    a.add("ᚦ", 0, 60, h=58, color=AMBER, tag="head")
    a.add("■", 0, -88, h=170, w=38, color=WOOD, tag="handle")
    for y in (-40, -85, -130):
        a.add("=", 0, y, w=50, color=GOLD, tag="handle")
    # serpent beads from head (angle 60) clockwise all the way round
    r = 215
    ang = 50.0
    a.add("◆", *polar(r, 66), h=60, w=74, color="#4a6", tag="snakehead")
    hx, hy = polar(r, 66)
    a.add("●", hx + 10, hy + 10, h=12, color=GOLD, tag="snakehead")
    d = 42.0
    n = 0
    while n < 29:
        x, y = polar(r, ang)
        a.add("●", x, y, h=d, color="#386" if n % 2 else "#4a6", tag="scale")
        nd = max(14.0, d - 0.9)
        step = math.degrees((d / 2 + nd / 2 + 7) / r)
        ang -= step
        d = nd
        n += 1
        if ang < 66 - 360 + 14:
            break
    row(a, "ᛗᛁᛞᚷᚨᚱᛞ", -150, 150, -360, 48, CYAN, tag="runes")
    return a


def odin_wanderer():
    a = Art("odin_wanderer", "Odin, a vándor", "Portré: széles karimájú kalap, félszemű arc szemkötővel, nagy ősz szakáll, Gungnir, holló a vállon.")
    hat = "#445"
    a.add("▲", 0, 190, h=120, w=200, color=hat, tag="hat")
    a.add("■", 0, 120, h=26, w=380, color=hat, tag="hat")
    a.add("■", 0, 143, h=16, w=150, color=AMBER, tag="hat")
    a.add("●", 0, 42, h=118, w=150, color="#eb9", tag="face")
    a.add("●", 32, 52, h=24, color=CYAN, tag="face")
    a.add("◆", -32, 54, h=34, w=40, color=INK, tag="face")
    stroke(a, -78, 90, 10, 28, INK, tag="face")
    a.add("▼", 0, 22, h=30, w=26, color="#d97", tag="face")
    a.add("▼", 0, -148, h=236, w=250, color="#dde", tag="beard")
    a.add("▼", 0, -172, h=150, w=150, color="#aab", tag="beard")
    a.add("~", 0, -52, w=104, color=WHITE, tag="beard")
    a.add("◢", -230, -175, h=200, w=120, color=NAVY, tag="cloak")
    a.add("◣", 230, -175, h=200, w=120, color=NAVY, tag="cloak")
    a.add("│", -300, 0, h=560, color=GOLD, tag="spear")
    a.add("◆", -300, 330, h=110, w=42, color=BONE, tag="spear")
    _raven(a, 225, 25, -1, scale=0.8, tag="raven")
    row(a, "ᚢᛁᚦᚱᛁᚱ", -150, 150, -340, 46, GOLD, tag="runes")
    return a


def eikthyr():
    a = Art("eikthyr", "Eikthyr", "Az első főellenség: a szarvas vonalrajza sötét korongon, villámló agancs-rúnák, mohazöld talaj.")
    a.add("●", 0, 40, h=360, color="#123", tag="disc")
    a.add("◯", 0, 40, h=392, color=CYAN, tag="disc")
    a.add("🦌", 0, 40, h=300, color=BONE, tag="disc")
    for s in (-1, 1):
        a.add("ᛋ", s * 255, 210, h=120, rot=s * 20, color="#ff6", tag="bolt")
        a.add("ᛋ", s * 290, 40, h=80, rot=s * 10, color="#ff6", tag="bolt")
    for i in range(7):
        a.add("~", -270 + 90 * i, -205, w=64, color=MOSS, tag="moss")
    row(a, "ᛖᛁᚲᚦᛃᚱ", -150, 150, -290, 52, CYAN, tag="runes")
    return a


CONCEPTS = [odin_eye2, vegvisir2, pixel_viking2, aurora2, fjord_sunset, jormungandr, odin_wanderer, eikthyr]
