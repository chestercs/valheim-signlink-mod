"""Round 8: save-edit only (rig.py apply). No 50-character limit: full <font> path (glows without a
loader sign), mirror + rotate + scale on glowing glyphs, larger sizes."""
from engine import Art
from concepts.round4 import dragon_crest


def glow_dragon_crest_full():
    base = dragon_crest()
    a = Art("glow_dragon_crest_full", "Világító sárkánycímer (mentés-szerkesztés)",
            "A Sárkánycímer fél méretben, minden része világít a teljes <font=…> útvonallal: nincs betöltő sign, "
            "és a tükrözött sárkányok is világíthatnak. Parancsonként 60-80 karakter: csak rig.py apply-jal.",
            unlit="path", mode="detailed")
    for p in base.items:
        a.add(p.glyph, p.x - base.ox, p.y - base.oy, size=p.size, sx=p.scale, rot=p.rotate, color=p.color, tag=p.tag)
    return a.fit_above(signs_above=3, factor=0.5)


CONCEPTS = [glow_dragon_crest_full]
