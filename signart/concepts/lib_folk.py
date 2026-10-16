"""Folk: Valheim's characters and bosses as glyph art for SignLink's Art tab.

Bold geometric glyphs, runes and strong silhouettes in the style of round2's Eikthyr: each piece is one
figure on a dark disc or emblem, built back to front from a few big shapes, with a rune line under it.
"""
import math

import artlib as A
from engine import Art
from concepts.common import *  # noqa: F401,F403


def bar(a, x0, y0, x1, y1, color, width=40, tag="bar"):
    """A thick straight limb from (x0,y0) to (x1,y1): a stretched, turned square."""
    L = math.hypot(x1 - x0, y1 - y0)
    ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
    ang = (ang + 90) % 180 - 90
    return a.add("■", (x0 + x1) / 2, (y0 + y1) / 2, h=width, w=L, rot=ang, color=color, tag=tag)


def _disc(a, color="#123", ring=CYAN, r=400, y=0):
    """The dark backing disc with a thin ring, like Eikthyr's."""
    a.add("●", 0, y, h=r * 2, color=color, tag="disc")
    a.add("◯", 0, y, h=r * 2 + 30, color=ring, tag="disc")


def elder():
    """The Elder: a walking tree with a mossy crown, two glowing eyes in a bark face and root arms reaching
    down to the ground."""
    a = Art("folk_elder", "Elder", "A Fekete erdő ura: sétáló fa, izzó szemek, gyökérkarok.")
    _disc(a, "#121a10", MOSS)
    for x, y, h in [(-150, 250, 230), (150, 250, 230), (0, 310, 260), (-60, 180, 200), (70, 190, 200)]:
        a.add("●", x, y, h=h, color="#2a5a2a", tag="crown")
    for x, y in [(-170, 280), (0, 350), (160, 290), (-80, 210), (90, 220)]:
        a.add("ᛉ", x, y, h=90, color="#5a8a3a", tag="crown")
    a.add("■", 0, -40, h=360, w=170, color="#5a3e26", tag="trunk")
    a.add("▲", 0, -230, h=90, w=260, color="#5a3e26", tag="trunk")
    for x in (-50, 0, 50):
        a.add("|", x, -50, h=300, color="#43301e", tag="trunk")
    a.add("■", 0, 60, h=70, w=150, color="#2b1d12", tag="trunk")
    a.add("●", -40, 60, h=42, color="#cf4", tag="trunk")
    a.add("●", 40, 60, h=42, color="#cf4", tag="trunk")
    a.add("▼", 0, -10, h=50, w=60, color="#2b1d12", tag="trunk")
    for s in (-1, 1):
        bar(a, s * 80, 60, s * 230, -120, "#5a3e26", tag=f"arm{s}")
        bar(a, s * 230, -120, s * 270, -260, "#5a3e26", tag=f"arm{s}")
        for dx in (-40, 0, 40):
            stroke(a, s * 270, -250, s * 270 + s * dx, -320, "#43301e", tag=f"arm{s}")
    row(a, "ᛖᛚᛞᛖᚱ", -140, 140, -340, 50, "#cf4", tag="runes")
    return a


def bonemass():
    """Bonemass: a bloated green mass of lumps around a bone skull face, dripping slime."""
    a = Art("folk_bonemass", "Bonemass", "A mocsár ura: puffadt zöld massza koponyaarccal.")
    _disc(a, "#141a10", "#7a5")
    for x, y, h, c in [(0, -20, 520, "#4a6a2a"), (-190, 60, 260, "#5a7a30"), (190, 60, 260, "#5a7a30"),
                       (-120, 220, 220, "#5a7a30"), (120, 220, 220, "#5a7a30"), (0, 270, 200, "#4a6a2a"),
                       (-220, -150, 200, "#3e5a24"), (220, -150, 200, "#3e5a24")]:
        a.add("●", x, y, h=h, color=c, tag="mass")
    for x, y, h in [(-160, 150, 50), (170, 120, 40), (-90, -200, 40), (130, -190, 54), (-230, -40, 36)]:
        a.add("●", x, y, h=h, color="#8a4", tag="mass")
    a.add("💀", 0, 40, h=260, color=BONE, tag="mass")
    for x in (-150, -40, 70, 180):
        a.add("▼", x, -280, h=50, w=26, color="#8a4", tag="drip")
    row(a, "ᛒᛟᚾᛖᛗᚨᛋ", -170, 170, -350, 46, "#9c5", tag="runes")
    return a


def haldor():
    """Haldor the trader: a hooded old merchant with a huge white beard, holding up a lantern."""
    a = Art("folk_haldor", "Haldor", "A kereskedő: csuklyás öreg, hatalmas szakáll, lámpás.")
    _disc(a, "#16121e", GOLD)
    a.add("▲", 0, 40, h=520, w=420, color="#4a3a5a", tag="hood")
    a.add("■", 0, -200, h=60, w=420, color="#4a3a5a", tag="hood")
    a.add("●", 0, 60, h=210, w=190, color="#2a2034", tag="hood")
    a.add("●", 0, 60, h=150, w=140, color="#eb9", tag="face")
    a.add("■", -32, 95, h=18, w=50, rot=-10, color=WHITE, tag="face")
    a.add("■", 32, 95, h=18, w=50, rot=10, color=WHITE, tag="face")
    a.add("●", -30, 70, h=16, color=INK, tag="face")
    a.add("●", 30, 70, h=16, color=INK, tag="face")
    a.add("●", 0, -40, h=230, w=230, color="#ccd", tag="face")
    a.add("▼", 0, -170, h=130, w=150, color="#ccd", tag="face")
    a.add("●", 0, -20, h=150, w=150, color="#eef", tag="face")
    a.add("●", -40, 18, h=40, w=100, rot=-15, color=WHITE, tag="face")
    a.add("●", 40, 18, h=40, w=100, rot=15, color=WHITE, tag="face")
    a.add("●", 0, 40, h=50, w=44, color="#d87", tag="face")
    stroke(a, 230, 260, 230, 140, STEEL, tag="lantern")
    a.add("●", 230, 70, h=200, color="#fd63", tag="lantern")
    a.add("■", 230, 70, h=110, w=70, color="#fd6", tag="lantern")
    a.add("■", 230, 70, h=110, w=70, color="#da4", tag="lantern")
    a.add("■", 230, 70, h=80, w=46, color="#ffb", tag="lantern")
    a.add("▲", 230, 140, h=30, w=80, color="#753", tag="lantern")
    row(a, "ᚺᚨᛚᛞᛟᚱ", -150, 150, -320, 46, GOLD, tag="runes")
    return a


def hugin():
    """Hugin the guide: a raven perched on a branch before a pale moon, its eye bright, his name in runes
    arched above."""
    a = Art("folk_hugin", "Hugin", "A holló-kalauz egy ágon, mögötte rúnakör.")
    _disc(a, "#101828", CYAN)
    a.add("●", 20, 20, h=470, color="#22405a", tag="moon")
    ring_of(a, "ᚺᚢᚷᛁᚾ", 320, start=112, end=68, h=58, color="#9ff", tag="ring")
    bar(a, -330, -200, 330, -160, "#753", tag="branch")
    k, ox, oy = 1.2, -20, -180

    def p(x, y):
        return ox + (x - 10) * k, oy + (y + 150) * k
    a.add("●", *p(30, 0), h=230 * k, w=300 * k, color="#1a1a24", tag="raven")
    a.add("◀", *p(-170, -90), h=110 * k, w=160 * k, rot=20, color="#1a1a24", tag="raven")
    a.add("●", *p(-20, 20), h=120 * k, w=280 * k, rot=-15, color="#2a3444", tag="raven")
    a.add("●", *p(30, 50), h=80 * k, w=150 * k, rot=-15, color="#3a4658", tag="raven")
    a.add("●", *p(150, 120), h=150 * k, color="#1a1a24", tag="raven")
    a.add("▶", *p(255, 105), h=50 * k, w=100 * k, color="#556", tag="raven")
    a.add("●", *p(170, 135), h=40 * k, color="#9ff", tag="raven")
    a.add("●", *p(175, 135), h=16 * k, color=INK, tag="raven")
    for x in (40, 100):
        stroke(a, *p(x, -100), *p(x, -150), GOLD, tag="raven")
    return a


def warrior():
    """A Viking warrior from the front: a nose-guard helmet, red beard, a round painted shield
    and a raised sword."""
    a = Art("folk_warrior", "Viking warrior", "Viking harcos: orrvédős sisak, kerek pajzs, felemelt kard.")
    _disc(a, "#141a22", STEEL)
    a.add("■", -50, -250, h=150, w=70, color="#543", tag="legs")
    a.add("■", 50, -250, h=150, w=70, color="#543", tag="legs")
    a.add("■", 0, -60, h=260, w=230, color="#a33", tag="body")
    a.add("■", 0, -170, h=30, w=230, color="#753", tag="body")
    bar(a, 130, 20, 220, 140, "#a33", tag="arm")
    bar(a, 235, 150, 235, 390, "#ccd", tag="sword")
    a.add("■", 235, 160, h=16, w=90, color=GOLD, tag="sword")
    a.add("●", 220, 140, h=50, color="#eb9", tag="sword")
    a.add("●", 0, 160, h=150, color="#eb9", tag="head")
    a.add("▼", 0, 80, h=120, w=150, color="#c62", tag="head")
    a.add("●", 0, 210, h=130, w=160, color=STEEL, tag="head")
    a.add("■", 0, 175, h=24, w=164, color="#778", tag="head")
    a.add("■", 0, 150, h=60, w=16, color="#778", tag="head")
    a.add("●", -32, 150, h=16, color=INK, tag="head")
    a.add("●", 32, 150, h=16, color=INK, tag="head")
    a.add("●", -120, -70, h=300, color="#753", tag="shield")
    a.add("◯", -120, -70, h=310, color=STEEL, tag="shield")
    for ang in (0, 90, 180, 270):
        x, y = polar(70, ang + 45)
        a.add("◆", -120 + x, -70 + y, h=110, w=60, rot=ang + 45 - 90, color=RED if ang % 180 else BONE, tag="shield")
    a.add("●", -120, -70, h=60, color=STEEL, tag="shield")
    return a


def valkyrie():
    """A valkyrie with great feathered wings spread, a winged helmet and a spear."""
    a = Art("folk_valkyrie", "Valkyrie", "Valkűr kitárt tollas szárnyakkal, szárnyas sisakkal, lándzsával.")
    _disc(a, "#141a2a", "#fe9")
    for s in (-1, 1):
        for L, w, c, angs in [(330, 56, "#bbc", (-10, 8, 26, 44, 62)), (230, 60, "#dde", (0, 20, 40, 60)),
                              (130, 60, WHITE, (10, 35, 60))]:
            for ang in angs:
                x, y = polar(40 + L / 2, ang)
                a.add("●", s * (50 + x), 60 + y, h=w, w=L, rot=s * ang, color=c, tag="wing")
    for s in (-1, 1):
        a.add("■", s * 55, 70, h=170, w=34, color=GOLD, tag="body")
    a.add("▲", 0, -130, h=400, w=260, color="#99a", tag="body")
    a.add("▲", 0, -100, h=280, w=120, color="#ccd", tag="body")
    a.add("●", 0, 60, h=70, w=180, color="#ccd", tag="body")
    a.add("■", 0, 0, h=24, w=110, color=GOLD, tag="body")
    bar(a, 190, -320, 190, 320, "#a86", width=16, tag="spear")
    a.add("◆", 190, 350, h=90, w=40, color="#ccd", tag="spear")
    a.add("●", 0, 140, h=120, color="#eb9", tag="head")
    a.add("●", 0, 185, h=80, w=130, color="#ccd", tag="head")
    a.add("■", 0, 160, h=16, w=130, color=GOLD, tag="head")
    for s in (-1, 1):
        a.add("◣" if s > 0 else "◢", s * 85, 210, h=80, w=60, color=WHITE, tag="head")
    a.add("●", -24, 125, h=12, color=INK, tag="head")
    a.add("●", 24, 125, h=12, color=INK, tag="head")
    row(a, "ᚹᚨᛚᚴᛃᚱᛁᛖ", -160, 160, -320, 44, "#fe9", tag="runes")
    return a


def troll():
    """A troll's face as an emblem: blue skin, a big nose, small eyes under a heavy brow, white tusks."""
    a = Art("folk_troll", "Troll face", "Troll-arc címer: kék bőr, nagy orr, agyarak.")
    a.add("⭕", 0, 0, h=820, color="#753", tag="ring")
    ring_of(a, "ᛏᚱᛟᛚᛚᛏᚱᛟᛚᛚ", 360, h=40, color="#da4", tag="ring")
    a.add("●", 0, 0, h=640, color="#1a2030", tag="disc")
    for s in (-1, 1):
        a.add("◆", s * 230, 60, h=150, w=80, rot=s * -20, color="#4a6a9a", tag="face")
    a.add("●", 0, 20, h=480, w=420, color="#5a7ab0", tag="face")
    for x, r in [(-60, 20), (0, 0), (60, -20)]:
        a.add("▲", x, 255, h=90, w=50, rot=r, color="#334", tag="face")
    for s in (-1, 1):
        a.add("●", s * 65, 80, h=60, color="#fd4", tag="face")
        a.add("●", s * 65, 80, h=26, color=INK, tag="face")
    for s in (-1, 1):
        a.add("■", s * 70, 118, h=40, w=150, rot=-s * 14, color="#3a5080", tag="face")
    a.add("●", 0, -30, h=180, w=150, color="#7a9ad0", tag="face")
    a.add("●", -36, -70, h=34, color="#2a3a60", tag="face")
    a.add("●", 36, -70, h=34, color="#2a3a60", tag="face")
    a.add("■", 0, -160, h=50, w=220, color="#213", tag="face")
    for s in (-1, 1):
        a.add("▲", s * 90, -140, h=110, w=46, color=BONE, tag="face")
    return a


def shaman():
    """A greydwarf shaman: a hunched mossy creature with a pale mask, a crooked staff and a green puff of
    healing spores."""
    a = Art("folk_shaman", "Greydwarf shaman", "Szürketörpe sámán: mohos test, bot, zöld spóra-felhő.")
    _disc(a, "#10180f", "#6a4")
    for x, y, h in [(180, 210, 150), (240, 270, 110), (130, 290, 100), (250, 170, 80)]:
        a.add("●", x, y, h=h, color="#7f45", tag="puff")
    for x, y in [(190, 240), (140, 300), (240, 280)]:
        a.add("✳", x, y, h=40, color="#bf7", tag="puff")
    bar(a, 200, -320, 200, 160, "#654", tag="staff")
    a.add("ᛉ", 200, 180, h=80, color="#654", tag="staff")
    a.add("●", -20, -110, h=330, w=360, color="#5a5a44", tag="body")
    for x, y, h in [(-140, -40, 90), (-60, 20, 110), (40, 10, 100), (-150, -150, 80)]:
        a.add("●", x, y, h=h, color="#4a7a3a", tag="body")
    a.add("■", -80, -300, h=90, w=50, color="#4a4a38", tag="body")
    a.add("■", 40, -300, h=90, w=50, color="#4a4a38", tag="body")
    bar(a, 80, -60, 200, -10, "#5a5a44", tag="body")
    a.add("●", -10, 110, h=180, w=170, color="#5a5a44", tag="head")
    a.add("ᛉ", -40, 230, h=90, rot=-15, color="#4a7a3a", tag="head")
    a.add("ᛉ", 30, 230, h=80, rot=15, color="#4a7a3a", tag="head")
    a.add("●", -10, 100, h=120, w=120, color="#ccb", tag="head")
    a.add("●", -40, 115, h=26, color="#8f4", tag="head")
    a.add("●", 20, 115, h=26, color="#8f4", tag="head")
    a.add("■", -10, 65, h=10, w=50, color="#332", tag="head")
    return a


def pieces():
    return [A.Piece("Elder", "glyph", art=elder()),
            A.Piece("Bonemass", "glyph", art=bonemass()),
            A.Piece("Haldor", "glyph", art=haldor()),
            A.Piece("Hugin", "glyph", art=hugin()),
            A.Piece("Viking warrior", "glyph", art=warrior()),
            A.Piece("Valkyrie", "glyph", art=valkyrie()),
            A.Piece("Troll face", "glyph", art=troll()),
            A.Piece("Greydwarf shaman", "glyph", art=shaman())]
