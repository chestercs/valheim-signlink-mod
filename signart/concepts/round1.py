"""Round 1 concepts. Coordinates are wu relative to the art origin; add glyphs back to front.

Design language: up to 9 islands of at most 5 touching glyphs (one island = one cell's depth stack),
plus loose singletons; islands never touch each other (thin gaps read like carving grooves).
"""
from engine import Art
from concepts.common import *  # noqa: F401,F403


def odin_eye():
    a = Art("odin_eye", "Odin szeme", "Egyszemű sigil: arany mandulaszem, Gungnir a hold előtt, két holló, sugarak, ODIN rúnák.")
    # eye (one island, 5 layers)
    a.add("◆", 0, 0, h=150, w=300, color=GOLD, tag="eye")
    a.add("●", 0, 0, h=100, w=200, color=BONE, tag="eye")
    a.add("●", 0, 0, h=92, color=CYAN, tag="eye")
    a.add("●", 0, 0, h=42, color=INK, tag="eye")
    a.add("●", 13, 14, h=13, color=WHITE, tag="eye")
    # moon + spear (island)
    a.add("●", 0, 330, h=150, color=BONE, tag="moon")
    stroke(a, 0, 105, 0, 300, GOLD, tag="spear")
    a.add("◆", 0, 345, h=120, w=44, color=INK, tag="spear")
    # rays
    for ang, r0, r1 in [(0, 175, 245), (180, 175, 245), (60, 140, 215), (120, 140, 215),
                        (210, 165, 225), (330, 165, 225), (240, 150, 205), (300, 150, 205)]:
        x0, y0 = polar(r0, ang)
        x1, y1 = polar(r1, ang)
        stroke(a, x0, y0, x1, y1, AMBER, tag="ray")
    # ravens (one island each)
    for s in (-1, 1):
        a.add("◣" if s < 0 else "◢", s * 290, 190, h=120, w=140, color=TEAL, tag="raven")
        a.add("●", s * 235, 140, h=58, w=125, color=NAVY, tag="raven")
        a.add("●", s * 180, 168, h=46, color=NAVY, tag="raven")
        a.add("▶" if s < 0 else "◀", s * 146, 164, h=24, w=36, color=GOLD, tag="raven")
        a.add("●", s * 178, 174, h=10, color=BONE, tag="raven")
    # beard / valknut V
    a.add("V", 0, -205, h=170, color=GOLD, tag="V")
    a.add("▼", 0, -170, h=40, color=CYAN, tag="V")
    row(a, "ᛟᛞᛁᚾ", -96, 96, -340, 64, CYAN, tag="runes")
    return a


def fenrir_moon():
    a = Art("fenrir_moon", "Fenrir és a hold", "Hatalmas sápadt hold, rajta a farkas vonalrajza; hegyek hósapkával, fenyősor, csillagok, FENRIR rúnák.")
    a.add("●", 0, 120, h=380, color="#eec", tag="moon")
    a.add("●", -105, 225, h=52, color="#ccb", tag="moon")
    a.add("●", 118, 30, h=36, color="#ccb", tag="moon")
    a.add("🐺", 0, 105, h=265, color=INK, tag="moon")
    a.add("▲", -110, -235, h=200, w=380, color="#346", tag="mtn")
    a.add("▲", 160, -255, h=160, w=300, color=NAVY, tag="mtn")
    a.add("▲", -110, -158, h=46, w=88, color=WHITE, tag="mtn")
    a.add("▲", 160, -194, h=38, w=72, color=WHITE, tag="mtn")
    for i, x in enumerate([-270, -170, -70, 30, 130, 230]):
        a.add("▲", x, -430 + (i % 2) * 10, h=80 + (i % 3) * 14, w=44, color=PINE, tag="pine")
    for x, y, h in [(-290, 300, 26), (-230, 120, 18), (250, 290, 22), (300, 150, 16), (-300, -40, 14), (290, -60, 18)]:
        a.add("★", x, y, h=h, color="#ffd", tag="star")
    row(a, "ᚠᛖᚾᚱᛁᚱ", -175, 175, -540, 60, GOLD, tag="runes")
    return a


def vegvisir():
    a = Art("vegvisir", "Rúnairánytű", "Vegvísir-szerű stave: nyolc különböző ág, középen gyűrű, kívül 16 apró rúna. 9 sziget = 9 cella.")
    a.add("●", 0, 0, h=104, color=INK, tag="hub")
    a.add("⭕", 0, 0, h=128, color=GOLD, tag="hub")
    a.add("ᛝ", 0, 0, h=52, color=CYAN, tag="hub")
    tips = ["ᛉ", "ᚼ", "◆", "ᛦ", "ᛏ", "ᚾ", "ᛣ", "ᛟ"]
    for k in range(8):
        ang = 90 - 45 * k
        x0, y0 = polar(100, ang)
        x1, y1 = polar(240, ang)
        stroke(a, x0, y0, x1, y1, BONE, tag=f"arm{k}")
        xc, yc = polar(175, ang)
        a.add("-", xc, yc, w=44, rot=ang, color=CYAN, tag=f"arm{k}")
        xt, yt = polar(268, ang)
        rt = ((ang - 90) + 180) % 360 - 180
        rt = rt if abs(rt) <= 99 else (rt + 360) % 360 - 180   # 3-digit rotate does not fit: turn upright
        a.add(tips[k], xt, yt, h=62, rot=rt, color=GOLD, tag=f"arm{k}")
    ring_of(a, "ᚠᚢᚦᚨᚱᚲᚷᚹᚺᚾᛁᛃ", 350, start=105, h=36, color=CYAN, tag="outer")
    return a


def drakkar():
    a = Art("drakkar", "Sárkányhajó", "Vörös-fehér csíkos vitorla, fa hajótest sárkányfejjel, pajzssor, hullámok, hold.")
    a.add("●", -250, 270, h=110, color="#eec", tag="moon")
    a.add("■", 0, 135, h=190, w=230, color=RED, tag="sail")
    a.add("■", -58, 135, h=190, w=34, color=BONE, tag="sail")
    a.add("■", 58, 135, h=190, w=34, color=BONE, tag="sail")
    stroke(a, 0, 232, 0, 300, WOOD, tag="sail")
    a.add("■", 0, -62, h=56, w=400, color=WOOD, tag="hull")
    a.add("◢", 235, -40, h=100, w=70, color=WOOD, tag="hull")
    a.add("◣", -235, -40, h=100, w=70, color=WOOD, tag="hull")
    a.add("S", 262, 42, h=80, color=GOLD, tag="hull")
    a.add("6", -262, 40, h=74, color=GOLD, tag="hull")
    for i, x in enumerate([-140, -70, 0, 70, 140]):
        a.add("●", x, 0, h=44, color=[RED, BONE, NAVY, BONE, RED][i], tag="shield")
        a.add("●", x, 0, h=12, color=GOLD, tag="shield")
    for j, y in enumerate([-135, -185]):
        for i in range(7):
            a.add("~", -270 + 90 * i + 45 * j, y, w=70, color=CYAN if j == 0 else TEAL, tag="wave")
    return a


def valhub():
    a = Art("valhub", "VALHUB", "A mém, ami az egészet elindította: VAL fehéren, HEIM feketén narancs dobozon. 8 sign, gyors első teszt.")
    row(a, "VAL", -230, -110, 0, 86, WHITE, tag="val")
    a.add("■", 110, 0, h=130, w=300, color="#f90", tag="box")
    row(a, "HEIM", 20, 200, 0, 86, "#000", tag="box")
    return a


def word_sign():
    a = Art("word_sign", "YGGDRASIL felirat", "Egy szó faragott arany betűkkel, árnyékkal; két végén rúnás pajzs, fent-lent hullámsor.")
    word = "YGGDRASIL"
    xs = [-256 + 64 * i for i in range(len(word))]
    for x, ch in zip(xs, word):
        a.add(ch, x + 6, -6, h=92, color="#310", tag="letter")
        a.add(ch, x, 0, h=92, color=GOLD, tag="letter")
    for s in (-1, 1):
        a.add("⭕", s * 350, 0, h=110, color=CYAN, tag="end")
        a.add("ᛉ" if s < 0 else "ᛟ", s * 350, 0, h=56, color=BONE, tag="end")
    for y in (90, -90):
        for i in range(9):
            a.add("~", -256 + 64 * i, y, w=46, color=TEAL, tag="wave")
    return a


def mjolnir():
    a = Art("mjolnir", "Mjölnir", "Thor kalapácsa: acél fej Thurisaz rúnával, bőrbe tekert nyél, villám-rúnák, aranygyűrű.")
    a.add("◯", 0, 20, h=600, color=GOLD, tag="ring")
    a.add("■", 0, 150, h=120, w=260, color=STEEL, tag="head")
    a.add("■", 0, 150, h=92, w=232, color="#bbc", tag="head")
    a.add("ᚦ", 0, 150, h=74, color=AMBER, tag="head")
    a.add("■", 0, -40, h=210, w=46, color=WOOD, tag="handle")
    for y in (40, -10, -60):
        a.add("=", 0, y, w=62, color=GOLD, tag="handle")
    a.add("●", 0, -168, h=56, color=GOLD, tag="handle")
    for s in (-1, 1):
        a.add("ᛋ", s * 190, 70, h=150, color="#ff6", tag="bolt")
    for x, y in [(-120, -120), (130, -100), (-170, 220), (175, 230), (0, 260), (-80, -230), (90, -235)]:
        a.add("★", x, y, h=20, color="#ffd", tag="star")
    row(a, "ᚦᛟᚱ", -90, 90, -390, 60, GOLD, tag="runes")
    return a


def yggdrasil():
    a = Art("yggdrasil", "Yggdrasil", "A világfa: zöld lombkorona-sziget, faragott törzs Algiz rúnával, gyökerek, aranygyűrű, kilenc világ rúnája.")
    a.add("●", 0, 150, h=260, color=PINE, tag="crown")
    a.add("●", -112, 115, h=170, color="#375", tag="crown")
    a.add("●", 112, 115, h=170, color="#375", tag="crown")
    a.add("●", 0, 230, h=150, color=MOSS, tag="crown")
    a.add("●", -40, 190, h=60, color="#6a6", tag="crown")
    a.add("■", 0, -85, h=170, w=56, color=WOOD, tag="trunk")
    a.add("/", -45, -195, h=70, color=WOOD, tag="trunk")
    a.add("\\", 45, -195, h=70, color=WOOD, tag="trunk")
    a.add("|", 0, -205, h=60, color=WOOD, tag="trunk")
    a.add("ᛉ", 0, -70, h=64, color=GOLD, tag="trunk")
    a.add("◯", 0, 30, h=700, color=GOLD, tag="ring")
    ring_of(a, "ᚠᚢᚦᚨᚱᚲᚷᚹᚺ", 400, start=90, h=46, color=CYAN, tag="realms")
    return a


def pixel_viking():
    a = Art("pixel_viking", "Pixel viking", "32 csempés pixel-art: szarvas sisak, szemek, orrvédő, vörös szakáll; ◢◣◥◤ félcsempék a sarkokon.")
    S, B, H, F, E, N, R = STEEL, "#bbc", BONE, "#eb9", INK, "#d97", "#c62"
    grid = [
        "h.....h",
        "hdSSSbh",
        ".SSSSS.",
        ".FESEF.",
        ".RFNFR.",
        ".RRRRR.",
        "..pRq..",
    ]
    glyph = {"d": ("◢", S), "b": ("◣", S), "p": ("◥", R), "q": ("◤", R)}
    color = {"h": H, "S": S, "F": F, "E": E, "N": N, "R": R}
    pitch, tile = 72, 60
    for r, line in enumerate(grid):
        for c, ch in enumerate(line):
            if ch == ".":
                continue
            g, col = glyph.get(ch, ("■", color.get(ch)))
            a.add(g, (c - 3) * pitch, (3 - r) * pitch, h=tile, w=tile, color=col, tag="px")
    return a


def aurora():
    a = Art("aurora", "Északi fény", "Áttetsző (alpha) zöld-türkiz-lila fényszalagok egy szigetben, alatta havas hegyek, csillagok, sarló hold.")
    a.add("●", -60, 200, h=70, w=520, color="#3f86", tag="aurora")
    a.add("●", 40, 150, h=60, w=480, color="#2fc6", tag="aurora")
    a.add("●", -20, 105, h=50, w=420, color="#a6f6", tag="aurora")
    a.add("●", 80, 240, h=40, w=300, color="#9fd6", tag="aurora")
    a.add("▲", -140, -120, h=190, w=340, color="#235", tag="mtn")
    a.add("▲", 130, -140, h=150, w=270, color="#346", tag="mtn")
    a.add("▲", -140, -46, h=42, w=76, color=WHITE, tag="mtn")
    a.add("▲", 130, -82, h=34, w=62, color=WHITE, tag="mtn")
    a.add("🌙", 250, 330, h=80, color="#ffd", tag="moon")
    for x, y, h in [(-280, 330, 16), (-150, 330, 22), (0, 340, 14), (120, 320, 18), (-300, 40, 14), (290, 40, 16)]:
        a.add("★", x, y, h=h, color="#ffd", tag="star")
    for i in range(6):
        a.add("~", -250 + 100 * i, -255, w=70, color=TEAL, tag="water")
    return a


def shield_crest():
    a = Art("shield_crest", "Klán-címer", "Keresztbe tett két balta mögött kerek viking pajzs (perem, festés, umbo); fent csillagok, lent rúnasor.")
    a.add("🪓", 0, 20, h=320, color=DARK, tag="crest")
    a.add("🪓", 0, 20, h=320, rot=-90, color=DARK, tag="crest")
    a.add("●", 0, 20, h=210, color=GOLD, tag="crest")
    a.add("●", 0, 20, h=186, color=BLOOD, tag="crest")
    a.add("●", 0, 20, h=58, color=STEEL, tag="crest")
    for x in (-110, 0, 110):
        a.add("★", x, 250, h=34, color=GOLD, tag="star")
    row(a, "ᚲᛚᚨᚾ", -120, 120, -230, 60, BONE, tag="runes")
    return a


CONCEPTS = [odin_eye, fenrir_moon, vegvisir, drakkar, valhub, word_sign, mjolnir, yggdrasil, pixel_viking, aurora, shield_crest]
