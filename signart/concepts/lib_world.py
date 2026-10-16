"""Valheim's world and gear as glyph art: bold geometric glyphs and runes in emblem style, like Mjolnir (round1)
and the dragon crest (round4). Stills, each one sign and copies stacked on it.

Coordinates are wu around the art origin, added back to front; one_sign_stack scales each piece to 2.4 m.
"""
import math

import artlib as A
from engine import Art
from concepts.common import *  # noqa: F401,F403

STONE, STONE_L, STONE_D = "#889", "#aab", "#556"
FIRE, FLAME, EMBER = "#f82", "#fd4", "#c41"
LEAF, LEAF_L, LEAF_D = "#3a5", "#5c6", "#264"
BARK, BARK_D = "#863", "#542"
VOID = "#000"


def _bar(a, x0, y0, x1, y1, width, color, tag):
    """A solid bar from (x0, y0) to (x1, y1): a square glyph stretched and turned."""
    L = math.hypot(x1 - x0, y1 - y0)
    rot = math.degrees(math.atan2(y1 - y0, x1 - x0)) - 90
    rot = (rot + 180) % 360 - 180
    if abs(rot) > 90:                                   # a bar has no up: keep the rotate tag short
        rot = (rot + 360) % 360 - 180
    return a.add("■", (x0 + x1) / 2, (y0 + y1) / 2, h=L, w=width, rot=round(rot), color=color, tag=tag)


def _flame(a, x, y, k=1.0, tag="flame"):
    """A torch or camp flame: red, orange and yellow drops, one island."""
    a.add("●", x, y + 18 * k, h=70 * k, w=46 * k, color=EMBER, tag=tag)
    a.add("▲", x, y + 54 * k, h=50 * k, w=34 * k, color=EMBER, tag=tag)
    a.add("●", x, y + 12 * k, h=50 * k, w=32 * k, color=FIRE, tag=tag)
    a.add("▲", x, y + 38 * k, h=36 * k, w=22 * k, color=FIRE, tag=tag)
    a.add("●", x, y + 6 * k, h=28 * k, w=18 * k, color=FLAME, tag=tag)


def _swirl(a, x, y, h, w, phase=0.0, n=7, tag="swirl"):
    """A spiral of nested discs, each one touching the last inside at a point a quarter turn on: dark and
    light blue in turn, white in the eye. phase turns the spiral (degrees)."""
    cols = ["#126", "#37c", "#159", "#5ae", "#26b", "#8df", WHITE]
    discs, r, cx, cy = [], 1.0, 0.0, 0.0
    for k in range(n):
        discs.append((r, cx, cy))
        nr = r * 0.74
        ang = math.radians(phase - 90 * k)
        cx += (r - nr) * math.cos(ang)
        cy += (r - nr) * math.sin(ang)
        r = nr
    lx, ly = cx, cy                                     # where the spiral ends: pull it back to the middle
    for k, (r, cx, cy) in enumerate(discs):
        cx, cy = cx - lx * (1 - r), cy - ly * (1 - r)
        a.add("●", x + cx * w / 2, y + cy * h / 2, h=h * r, w=w * r, color=cols[k % len(cols)], tag=tag)


def yggdrasil():
    """The world tree: a wide crown of leaf discs, a carved trunk with Algiz, roots, a gold ring of nine runes."""
    a = Art("w_yggdrasil", "Yggdrasil", "")
    a.add("◯", 0, 20, h=760, color=GOLD, tag="ring")
    for x1, y1 in [(-260, -230), (260, -230), (-140, -300), (140, -300), (0, -320)]:
        _bar(a, 0, -120, x1, y1, 34, BARK_D, tag="tree")
    a.add("■", 0, -40, h=240, w=96, color=BARK, tag="tree")
    a.add("■", -14, -40, h=220, w=10, color=BARK_D, tag="tree")
    for x0, x1 in [(-30, -170), (30, 170)]:
        _bar(a, x0, 20, x1, 110, 30, BARK, tag="tree")
    for x, y, d, c in [(-235, 150, 220, LEAF_D), (235, 150, 220, LEAF_D), (-140, 270, 250, LEAF_D),
                       (140, 270, 250, LEAF_D), (0, 175, 330, LEAF), (-185, 190, 160, LEAF), (185, 190, 160, LEAF),
                       (0, 310, 230, LEAF), (-80, 250, 110, LEAF_L), (100, 205, 90, LEAF_L), (-35, 355, 70, LEAF_L),
                       (-220, 140, 60, LEAF_L), (215, 255, 60, LEAF_L)]:
        a.add("●", x, y - 30, h=d * 0.95, color=c, tag="tree")
    a.add("ᛉ", 0, -60, h=110, color=GOLD, tag="tree")
    ring_of(a, "ᚠᚢᚦᚨᚱᚲᚷᚹᚺ", 445, start=90, h=56, color=CYAN, tag="realms")
    return a


def portal():
    """The rune portal: a dark wooden arch with horned posts and gold runes, a blue swirl in the gate."""
    a = Art("w_portal", "Portal", "")
    _swirl(a, 0, 20, 380, 300, tag="gate")
    for s in (-1, 1):
        a.add("■", s * 190, -20, h=420, w=70, color=BARK_D, tag="frame")
        a.add("◢" if s < 0 else "◣", s * 225, 250, h=120, w=70, color=BARK_D, tag="frame")
        for k, r in enumerate("ᚱᛁᛟ" if s < 0 else "ᛞᚾᚹ"):
            a.add(r, s * 190, 110 - 90 * k, h=60, color=CYAN, tag="frame")
    a.add("■", 0, 225, h=70, w=460, color=BARK_D, tag="frame")
    row(a, "ᛈᛟᚱᛏᚨᛚ", -150, 150, 225, 48, GOLD, tag="frame")
    a.add("■", 0, -245, h=36, w=520, color=STONE, tag="frame")
    return a


def anvil():
    """The forge: a steel anvil with its horn, a hammer leaning on it, sparks."""
    a = Art("w_anvil", "Anvil and hammer", "")
    a.add("■", 0, -195, h=50, w=320, color="#556", tag="anvil")
    a.add("▲", 0, -150, h=60, w=260, color="#667", tag="anvil")
    a.add("■", 0, -110, h=90, w=110, color="#778", tag="anvil")
    a.add("◢", -80, -80, h=40, w=50, color="#778", tag="anvil")
    a.add("◣", 80, -80, h=40, w=50, color="#778", tag="anvil")
    a.add("■", 30, -35, h=80, w=320, color=STEEL, tag="anvil")
    a.add("◀", -200, -22, h=54, w=140, color=STEEL, tag="anvil")
    a.add("■", 30, -2, h=14, w=320, color="#ccd", tag="anvil")
    _bar(a, 130, 20, 280, 230, 30, WOOD, tag="hammer")
    a.add("■", 115, 40, h=80, w=150, rot=-35, color="#bbc", tag="hammer")
    a.add("■", 115, 40, h=56, w=126, rot=-35, color=STEEL, tag="hammer")
    for x, y, h, c in [(-40, 70, 34, FLAME), (-110, 120, 22, FIRE), (10, 150, 26, FLAME), (-160, 60, 18, FIRE),
                       (40, 210, 16, FIRE), (-80, 200, 14, FLAME)]:
        a.add("★", x, y, h=h, color=c, tag="spark")
    row(a, "ᛋᛗᛁᚦ", -100, 100, -260, 54, GOLD, tag="runes")
    return a


def mead_horn():
    """A drinking horn full of mead: a curved, shaded bone horn with gold bands and tip, foam at the mouth."""
    a = Art("w_mead_horn", "Mead horn", "")
    n = 36
    pts = []                                            # tip first: a U from the tip (left) to the mouth (right)
    for i in range(n):
        t = i / (n - 1)
        ang = math.radians(-195 + 195 * t)
        pts.append((230 * math.cos(ang), 120 + 250 * math.sin(ang), 34 + 130 * t))
    for x, y, d in pts:                                 # the shaded underside, then the horn over it
        a.add("●", x + 6, y - 8, h=d, color="#ba8", tag="horn")
    for x, y, d in pts:
        a.add("●", x - 3, y + 4, h=d * 0.9, color="#eed", tag="horn")
    for i in (1, 13, 25, n - 3):                       # gold bands across the horn
        x, y, d = pts[i]
        ang = -195 + 195 * i / (n - 1)
        tx, ty = -230 * math.sin(math.radians(ang)), 250 * math.cos(math.radians(ang))
        rot = (math.degrees(math.atan2(ty, tx)) + 90) % 180 - 90
        a.add("■", x, y, h=d * 1.04, w=18, rot=round(rot), color=GOLD, tag="horn")
    a.add("●", pts[0][0], pts[0][1], h=40, color=GOLD, tag="horn")
    x, y, d = pts[-1]
    a.add("●", x, y + 55, h=50, w=d + 24, color=GOLD, tag="horn")
    a.add("●", x, y + 58, h=34, w=d, color=AMBER, tag="horn")
    for dx, dd in [(-55, 40), (-20, 54), (20, 48), (55, 36)]:
        a.add("●", x + dx, y + 80, h=dd, color=WHITE, tag="horn")
    row(a, "ᛗᛁᛞ", -90, 90, -230, 60, GOLD, tag="runes")
    return a


def crossed_axes():
    """Crossed axes over a round shield: a painted plank shield with a steel boss, two broad axes crossed in front."""
    a = Art("w_axes", "Crossed axes", "")
    a.add("●", 0, 0, h=400, color=STEEL, tag="shield")
    a.add("●", 0, 0, h=370, color=WOOD, tag="shield")
    a.add("●", 0, 0, h=300, color=BLOOD, tag="shield")
    a.add("●", 0, 0, h=250, color=WOOD, tag="shield")
    for x in (-80, 0, 80):
        a.add("■", x, 0, h=240 if x else 250, w=8, color=BARK_D, tag="shield")
    a.add("●", 0, 0, h=100, color=STEEL, tag="shield")
    a.add("●", 0, 0, h=70, color="#bbc", tag="shield")
    for s in (-1, 1):
        _bar(a, -s * 220, -240, s * 190, 210, 30, BARK, tag="shield")
        ox, oy = s * 160, 180                           # the head: a half-moon blade on the outer side
        ux, uy = s * 0.7071, -0.7071                   # outwards, across the haft
        a.add("●", ox + ux * 60, oy + uy * 60, h=200, w=110, rot=-s * 45, color="#bbc", tag="shield")
        a.add("●", ox + ux * 52, oy + uy * 52, h=170, w=84, rot=-s * 45, color=STEEL, tag="shield")
        a.add("■", ox + ux * 15, oy + uy * 15, h=60, w=60, rot=-s * 45, color=STEEL, tag="shield")
    row(a, "ᚲᛚᚨᚾ", -120, 120, -310, 60, GOLD, tag="runes")
    return a


def runestone():
    """A painted runestone: a tall grey stone, a carved ring of red runes around Othala, columns of runes, grass."""
    a = Art("w_runestone", "Runestone", "")
    a.add("●", 0, 120, h=300, w=330, color=STONE, tag="stone")
    a.add("■", 0, -40, h=340, w=330, color=STONE, tag="stone")
    a.add("●", 0, 90, h=230, w=250, color=STONE_D, tag="stone")
    a.add("●", 0, 90, h=200, w=220, color=STONE, tag="stone")
    for k, r in enumerate("ᚢᛁᚴᛁᚾᚴᛦ"):
        ang = 160 - 20 * k
        x, y = polar(108, ang)
        a.add(r, x, 90 + y, h=34, color=RED, tag="stone")
    a.add("ᛟ", 0, 90, h=100, color=RED, tag="stone")
    for c in range(3):
        for k, r in enumerate(["ᚦᚢᚱ", "ᛋᛏᛅ", "ᛁᚾᛋ"][c]):
            a.add(r, -80 + 80 * c, -40 - 70 * k, h=52, color=RED, tag="stone")
    for i in range(6):
        a.add("ᛁ", -200 + 80 * i, -220, h=50, rot=(-15 if i % 2 else 15), color=MOSS, tag="grass")
    a.add("■", 0, -215, h=20, w=460, color=MOSS, tag="grass")
    return a


def longhouse():
    """A viking longhouse: a steep thatched roof with crossed gable beams ending in dragon heads, plank walls,
    a door and shields."""
    a = Art("w_longhouse", "Longhouse", "")
    a.add("■", 0, -130, h=150, w=520, color=WOOD, tag="house")
    for x in range(-240, 260, 60):
        a.add("■", x, -130, h=150, w=6, color=BARK_D, tag="house")
    a.add("■", 0, -150, h=110, w=80, color=VOID, tag="house")
    a.add("▲", 0, 60, h=260, w=640, color="#a83", tag="house")
    for y in (-40, 10, 60, 110):                       # thatch courses
        a.add("■", 0, y, h=10, w=640 * (190 - y) / 260 - 30, color="#c95", tag="house")
    for s in (-1, 1):
        _bar(a, s * 120, 70, -s * 40, 250, 22, BARK_D, tag="house")
    a.add("🐉", -75, 285, h=100, color=GOLD, mirror=True, tag="dragonL")
    a.add("🐉", 75, 285, h=100, color=GOLD, tag="dragonR")
    for x, c in [(-180, RED), (-110, BONE), (110, BONE), (180, RED)]:
        a.add("●", x, -110, h=50, color=c, tag="shield")
        a.add("●", x, -110, h=14, color=GOLD, tag="shield")
    a.add("■", 0, -215, h=20, w=600, color=MOSS, tag="ground")
    return a


def crypt():
    """A Black Forest burial chamber: a stone gate with a dark arch, a skull above, two burning torches, moss."""
    a = Art("w_crypt", "Burial chamber", "")
    a.add("▲", 0, 170, h=180, w=560, color=STONE_D, tag="gate")
    a.add("■", 0, -40, h=300, w=440, color=STONE, tag="gate")
    for y in (60, -40, -140):
        a.add("■", 0, y, h=6, w=440, color=STONE_D, tag="gate")
    a.add("●", 0, 20, h=240, w=240, color=STONE_L, tag="gate")
    a.add("●", 0, 20, h=200, w=200, color=VOID, tag="gate")
    a.add("■", 0, -90, h=200, w=200, color=VOID, tag="gate")
    a.add("💀", 0, 175, h=70, color=BONE, tag="gate")
    for s in (-1, 1):
        a.add("■", s * 175, 10, h=90, w=18, color=BARK_D, tag=f"torch{s}")
        _flame(a, s * 175, 60, 0.9, tag=f"torch{s}")
    for x in (-200, -90, 60, 170):
        a.add("●", x, -195, h=34, w=90, color=MOSS, tag="moss")
    return a


def cauldron():
    """A cauldron over a fire: a black pot with a bubbling green brew on a tripod, logs and flames below."""
    a = Art("w_cauldron", "Cauldron", "")
    for s in (-1, 1):
        _bar(a, s * 230, -260, 0, 260, 20, BARK_D, tag="pot")
    a.add("|", 0, 175, h=110, color=STEEL, tag="pot")
    a.add("●", 0, 20, h=240, w=320, color="#223", tag="pot")
    a.add("●", 0, 100, h=50, w=300, color="#445", tag="pot")
    a.add("●", 0, 104, h=34, w=264, color="#5b3", tag="pot")
    for x, d in [(-70, 30), (20, 40), (90, 24)]:
        a.add("●", x, 130, h=d, color="#8e5", tag="pot")
    _bar(a, -150, -210, 150, -170, 36, BARK, tag="fire")
    _bar(a, -150, -170, 150, -210, 36, BARK, tag="fire")
    for x, k in [(-80, 0.9), (0, 1.3), (80, 0.9)]:
        _flame(a, x, -160, k, tag="fire")
    return a


def clan_banner():
    """A clan banner: red cloth with a swallowtail hanging from a pole, a gold boar, a band of runes."""
    a = Art("w_banner", "Clan banner", "")
    _bar(a, -220, 300, 220, 300, 26, BARK, tag="pole")
    for s in (-1, 1):
        a.add("●", s * 225, 300, h=40, color=GOLD, tag="pole")
    a.add("■", 0, 60, h=440, w=340, color=BLOOD, tag="cloth")
    a.add("▼", -85, -205, h=110, w=170, color=BLOOD, tag="cloth")
    a.add("▼", 85, -205, h=110, w=170, color=BLOOD, tag="cloth")
    a.add("■", 0, 260, h=26, w=340, color=GOLD, tag="cloth")
    a.add("🐗", 0, 90, h=220, color=GOLD, tag="cloth")
    row(a, "ᚴᛚᚨᚾ", -100, 100, -90, 50, BONE, tag="cloth")
    return a


def rune_compass():
    """A Vegvisir-style rune compass: eight gold staves with different rune tips around a ringed hub, an outer
    ring of runes."""
    a = Art("w_compass", "Rune compass", "")
    a.add("◯", 0, 0, h=760, color=GOLD, tag="ring")
    a.add("●", 0, 0, h=130, color=INK, tag="hub")
    a.add("⭕", 0, 0, h=160, color=GOLD, tag="hub")
    a.add("ᛝ", 0, 0, h=70, color=CYAN, tag="hub")
    tips = ["ᛉ", "ᚼ", "◆", "ᛦ", "ᛏ", "●", "ᛣ", "ᛟ"]
    for k in range(8):
        ang = 90 - 45 * k
        x0, y0 = polar(115, ang)
        x1, y1 = polar(270, ang)
        _bar(a, x0, y0, x1, y1, 18, GOLD, tag=f"arm{k}")
        for r in (165, 220):
            xm, ym = polar(r, ang)
            a.add("■", xm, ym, h=10, w=60, rot=ang - 90 if abs(ang - 90) <= 90 else ang + 90, color=GOLD,
                  tag=f"arm{k}")
        xt, yt = polar(305, ang)
        rt = ((ang - 90) + 180) % 360 - 180
        rt = rt if abs(rt) <= 90 else (rt + 360) % 360 - 180
        a.add(tips[k], xt, yt, h=80 if tips[k] not in "◆●" else 48, rot=rt, color=BONE, tag=f"arm{k}")
    ring_of(a, "ᚠᚢᚦᚨᚱᚲᚷᚹᚺᚾᛁᛃ", 440, start=105, h=48, color=CYAN, tag="outer")
    return a


def fjord_sunrise():
    """Sunrise over the fjord: a gold sun half risen between dark cliffs with long rays, its path of light on the
    water, a longship silhouette with a red-white sail off to one side, gulls."""
    a = Art("w_fjord_sunrise", "Fjord sunrise", "")
    hy = -160                                           # the horizon
    for k in range(7):
        ang = 157.5 - 22.5 * k
        x, y = polar(255, ang)
        a.add("▲", x, hy + y, h=140, w=40, rot=round(ang - 90), color=AMBER, tag=f"ray{k}")
    a.add("●", 0, hy, h=300, color=FLAME, tag="sun")
    a.add("●", 0, hy, h=250, color="#fe8", tag="sun")
    a.add("▲", -300, -30, h=380, w=280, color="#234", tag="cliffL")
    a.add("▲", -250, -90, h=200, w=160, color="#345", tag="cliffL")
    a.add("▲", 300, -30, h=380, w=280, color="#234", tag="cliffR")
    a.add("▲", 250, -100, h=180, w=150, color="#345", tag="cliffR")
    a.add("■", 0, hy - 95, h=190, w=860, color="#358", tag="sea")
    for y, w in [(-178, 230), (-208, 170), (-238, 120), (-268, 80), (-298, 44)]:
        a.add("■", 0, y, h=12, w=w, color=FLAME, tag="sea")
    for j, y in enumerate([-200, -255, -310]):
        for i in range(2):
            a.add("~", 190 + 100 * i + 40 * j, y, w=60, color="#7cf", tag="sea")
    x0, y0 = -230, -275                                 # the longship, on the water left of the sun path
    a.add("■", x0, y0, h=26, w=170, color=INK, tag="ship")
    a.add("◢", x0 + 100, y0 + 17, h=60, w=40, color=INK, tag="ship")
    a.add("◣", x0 - 100, y0 + 17, h=60, w=40, color=INK, tag="ship")
    a.add("■", x0, y0 + 50, h=80, w=10, color=INK, tag="ship")
    a.add("■", x0, y0 + 80, h=90, w=110, color=BLOOD, tag="ship")
    for dx in (-30, 30):
        a.add("■", x0 + dx, y0 + 80, h=90, w=18, color=BONE, tag="ship")
    for x in range(-60, 60, 30):
        a.add("●", x0 + x + 15, y0 + 4, h=18, color=GOLD, tag="ship")
    for x, y in [(-120, 190), (-60, 230), (120, 210)]:
        a.add("v", x, y, h=24, w=46, color="#dde", tag="gull")
    return a


def portal_frames(n=6):
    """The portal's swirl turning: the wooden arch stands still, the spiral of nested discs turns once over
    the loop. Shapes only (one font), so each frame is one sign."""
    out = []
    for f in range(n):
        a = Art(f"w_portal_{f}", "Portal", "")
        _swirl(a, 0, 20, 380, 300, phase=-360 * f / n, tag="gate")
        for s in (-1, 1):
            a.add("■", s * 190, -20, h=420, w=70, color=BARK_D, tag="frame")
            a.add("◢" if s < 0 else "◣", s * 225, 250, h=120, w=70, color=BARK_D, tag="frame")
            for k in range(3):
                a.add("◆", s * 190, 110 - 90 * k, h=40, w=30, color=CYAN, tag="frame")
        a.add("■", 0, 225, h=70, w=460, color=BARK_D, tag="frame")
        for k in range(5):
            a.add("●", -140 + 70 * k, 225, h=26, color=GOLD, tag="frame")
        a.add("■", 0, -245, h=36, w=520, color=STONE, tag="frame")
        out.append(a)
    return out


def pieces():
    return [A.Piece("Yggdrasil", "glyph", art=yggdrasil()),
            A.Piece("Portal", "glyph", art=portal()),
            A.Piece("Anvil and hammer", "glyph", art=anvil()),
            A.Piece("Mead horn", "glyph", art=mead_horn()),
            A.Piece("Crossed axes", "glyph", art=crossed_axes()),
            A.Piece("Runestone", "glyph", art=runestone()),
            A.Piece("Longhouse", "glyph", art=longhouse()),
            A.Piece("Burial chamber", "glyph", art=crypt()),
            A.Piece("Cauldron", "glyph", art=cauldron()),
            A.Piece("Clan banner", "glyph", art=clan_banner()),
            A.Piece("Rune compass", "glyph", art=rune_compass()),
            A.Piece("Fjord sunrise", "glyph", art=fjord_sunrise()),
            A.Piece("Portal (moving, glyph swirl)", "glyph", frames=portal_frames(), fps=6)]
