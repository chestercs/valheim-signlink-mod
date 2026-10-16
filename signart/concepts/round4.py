"""Round 4: heraldry with mirrored glyphs (<scale=-1>, verified in game) — pairs facing each other."""
from engine import Art
from concepts.common import *  # noqa: F401,F403
from concepts.round1 import drakkar


def dragon_crest():
    a = Art("dragon_crest", "Sárkánycímer", "Két egymás felé forduló arany sárkány (egyik tükrözve) egy vörös-arany rúnás pajzs körül; fent csillagok, lent rúnasor.")
    a.add("🐉", -265, 20, h=290, color=GOLD, mirror=True, tag="dragonL")
    a.add("🐉", 265, 20, h=290, color=GOLD, tag="dragonR")
    a.add("●", 0, 10, h=190, color=GOLD, tag="shield")
    a.add("●", 0, 10, h=166, color=BLOOD, tag="shield")
    a.add("ᛟ", 0, 10, h=96, color=BONE, tag="shield")
    for x in (-90, 0, 90):
        a.add("★", x, 230, h=34 if x else 44, color=GOLD, tag="star")
    row(a, "ᛞᚱᚨᚲᚨ", -150, 150, -220, 56, BONE, tag="runes")
    return a


def stag_pair():
    a = Art("stag_pair", "Szarvaspár", "Eikthyr-címer: két szarvas egymással szemben (egyik tükrözve), köztük az Algiz (jávorszarvas) rúna villámokkal, alatta moha.")
    a.add("🦌", -200, 30, h=300, color=BONE, mirror=True, tag="stagL")
    a.add("🦌", 200, 30, h=300, color=BONE, tag="stagR")
    a.add("ᛉ", 0, 60, h=170, color=CYAN, tag="rune")
    a.add("ᛋ", -60, 250, h=90, rot=15, color="#ff6", tag="bolt")
    a.add("ᛋ", 60, 250, h=90, rot=-15, color="#ff6", tag="bolt")
    for i in range(7):
        a.add("~", -270 + 90 * i, -175, w=64, color=MOSS, tag="moss")
    row(a, "ᛖᛁᚲᚦᛃᚱ", -150, 150, -260, 50, CYAN, tag="runes")
    return a


def drakkar2():
    a = Art("drakkar2", "Sárkányhajó v2", "A v1 orrán valódi sárkányfej (tükrözve, hogy előre nézzen); minden más a régi.")
    base = drakkar()
    for p in base.items:
        if p.glyph == "S" and p.tag == "hull":
            continue
        a.add(p.glyph, p.x - base.ox, p.y - base.oy, size=p.size, sx=p.scale, rot=p.rotate, color=p.color, tag=p.tag)
    a.add("🐲", 268, 50, h=96, color=GOLD, mirror=True, tag="hull")
    return a


CONCEPTS = [dragon_crest, stag_pair, drakkar2]
