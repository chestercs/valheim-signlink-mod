"""Round 3: the flying raven (Norse V as raised wings) replaces the triangle-wing 'duck'; Odin's hat fixed."""
from engine import Art
from concepts.common import *  # noqa: F401,F403
from concepts.round2 import odin_eye2


def flying_raven(a, x, y, s=1, k=1.0, wings=DARK, tag="raven"):
    """Raven in flight seen from the side-front, heading s (+1 right / -1 left): 4 glyphs."""
    a.add("V", x, y + 40 * k, h=150 * k, w=230 * k, color=wings, tag=tag)
    a.add("●", x, y - 30 * k, h=40 * k, w=120 * k, color=DARK, tag=tag)
    a.add("●", x + s * 66 * k, y - 22 * k, h=38 * k, color=DARK, tag=tag)
    a.add("▶" if s > 0 else "◀", x + s * 100 * k, y - 24 * k, h=14 * k, w=36 * k, color=GOLD, tag=tag)


def odin_eye3():
    a = Art("odin_eye3", "Odin szeme v3", "Repülő hollók türkiz szárnnyal a lándzsa két oldalán; a szem, a sugarak és a V maradt.")
    base = odin_eye2()
    keep = [p for p in base.items if not p.tag.startswith("raven")]
    for p in keep:
        a.add(p.glyph, p.x - base.ox, p.y - base.oy, size=p.size, sx=p.scale, rot=p.rotate, color=p.color, tag=p.tag)
    flying_raven(a, -250, 205, +1, 0.9, wings=TEAL, tag="ravenL")
    flying_raven(a, 250, 205, -1, 0.9, wings=TEAL, tag="ravenR")
    return a


def huginn_muninn():
    a = Art("huginn_muninn", "Huginn és Muninn", "Gondolat és Emlékezet: egy holló átrepül a hold előtt, a másik türkiz szárnnyal kíséri; alul fenyők, rúnanevek.")
    a.add("●", 40, 120, h=400, color="#eec", tag="moon")
    flying_raven(a, 10, 110, +1, 1.3, tag="moon")
    flying_raven(a, -250, 330, +1, 0.8, wings=TEAL, tag="raven2")
    for i, x in enumerate([-260, -190, -120, 200, 270]):
        a.add("▲", x, -170 - (i % 2) * 12, h=110 + (i % 3) * 20, w=50, color=PINE, tag="pine")
    for x, y, h in [(-300, 120, 16), (290, 330, 20), (-120, 420, 14), (300, 90, 14)]:
        a.add("★", x, y, h=h, color="#ffd", tag="star")
    row(a, "ᚺᚢᚷᛁᚾ", -230, -40, -300, 46, CYAN, tag="runes")
    row(a, "ᛗᚢᚾᛁᚾ", 60, 250, -300, 46, GOLD, tag="runes")
    return a


def odin_wanderer2():
    a = Art("odin_wanderer2", "Odin, a vándor v2", "Széles, lapos karimájú kalap kupolával, félszemű arc, ősz szakáll, Gungnir, mellette repülő holló.")
    hat = "#445"
    a.add("●", 0, 178, h=130, w=170, color=hat, tag="hat")
    a.add("●", 0, 132, h=44, w=400, color=hat, tag="hat")
    a.add("■", 0, 160, h=14, w=168, color=AMBER, tag="hat")
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
    a.add("|", -300, -5, h=550, sx=0.35, color=GOLD, tag="spear")
    a.add("◆", -300, 325, h=110, w=42, color=BONE, tag="spear")
    flying_raven(a, 270, 120, -1, 0.75, wings=TEAL, tag="raven")
    row(a, "ᚢᛁᚦᚱᛁᚱ", -150, 150, -340, 46, GOLD, tag="runes")
    return a


CONCEPTS = [odin_eye3, huginn_muninn, odin_wanderer2]
