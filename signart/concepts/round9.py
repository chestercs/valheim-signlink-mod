"""Round 9 (2026-10-08): TYPABLE pieces for the third gallery — one glyph per sign, every command
<= 50 characters, so anyone can copy-paste them sign by sign (no save editing). Designed at their final
size (<= 7.5 m wide) and centred over the build. Rows of glyphs share a tag: the engine keeps a tagged
row on one depth plane (layer), so it still lines up when seen from below.
"""
from engine import Art
from concepts.common import GOLD, row


def boar_crest():
    a = Art("boar_crest", "Vaddisznó-címer", "Rozsdavörös pajzs csontfehér vaddisznóval, két oldalán álló fejszék, "
            "alatta GALTI (vadkan) rúnákkal. 11 sign, nappal szép.")
    for s, blade in ((-1, "▶"), (1, "◀")):            # axe: wooden haft, steel blade with its edge outward
        a.add("|", s * 116, 6, h=150, color="#853", tag=f"axe{s}")
        a.add(blade, s * 136, 52, h=50, color="#bbc", tag=f"axe{s}")
    a.add("●", 0, 10, h=150, color="#b42", tag="crest")
    a.add("◯", 0, 10, h=156, color="#cba", tag="crest")
    a.add("🐗", 0, 8, h=92, color="#fec", tag="crest")
    row(a, "ᚷᚨᛚᛏᛁ", -76, 76, -104, 32, GOLD, tag="runes")
    return a.fit_above(signs_above=2)


def bee_meadow():
    a = Art("bee_meadow", "Méhek a napraforgónál", "Sárga napraforgó barna szívvel, zöld szár levéllel, körülötte "
            "három méh pontozott röppályával. 16 sign, nappali darab.")
    a.add("●", 6, 57, h=100, color="#fc3", tag="flower")
    a.add("●", 6, 59, h=40, color="#742", tag="flower")
    a.add("🌻", 6, 44, h=146, color="#531", tag="flower")
    a.add("|", 0, -78, h=110, color="#484", tag="flower")
    a.add("🍃", 30, -80, h=46, color="#5a4", tag="flower")
    a.add("🐝", -118, 98, h=34, color="#ea0", tag="bee")
    a.add("🐝", 122, 64, h=34, color="#ea0", mirror=True, tag="bee")
    a.add("🐝", 84, 150, h=28, color="#ea0", mirror=True, tag="bee")
    for x, y in [(-96, 82), (-80, 62), (-70, 40), (-72, 16)]:
        a.add("·", x, y, h=7, color="#fd7", tag="trail")
    for x, y in [(102, 84), (92, 104), (88, 126), (86, 146)]:
        a.add("·", x, y, h=7, color="#fd7", tag="trail")
    return a.fit_above(signs_above=2)


def first_night():
    """Glows with the 5-character alias (needs the loader sign); lit glyphs stay dark silhouettes."""
    a = Art("first_night", "Az első este", "Világító tábortűz szikrákkal, sötét hasábokon; mellette fejsze a "
            "tuskóban; világító hold és csillagok. Gépelhető világítás: álnév + 1 betöltő sign.", unlit=True)
    a.add("|", -20, -46, h=96, rot=-72, color="#421", glow=False, tag="fire")       # logs: a rotated stroke, no <scale>
    a.add("|", 20, -46, h=96, rot=72, color="#532", glow=False, tag="fire")
    a.add("🔥", 0, 6, h=104, color="#f72", tag="fire")
    a.add("🔥", 0, -6, h=58, color="#fd3", tag="fire")
    for x, y, h, c in [(-26, 82, 8, "#fb4"), (14, 104, 6, "#fd7"), (36, 74, 7, "#fb4"),
                       (-8, 128, 5, "#fd7"), (24, 146, 4, "#f94")]:
        a.add("●", x, y, h=h, color=c, tag="spark")
    a.add("🪵", -112, -44, h=46, color="#642", glow=False, tag="axe")
    a.add("🪓", -106, -6, h=52, color="#9aa", glow=False, tag="axe")
    a.add("🌙", 104, 138, h=52, color="#fec", tag="moon")
    for x, y in [(-120, 150), (-60, 172), (52, 176), (130, 70)]:
        a.add("★", x, y, h=12, color="#ffd", tag="star")
    return a.fit_above(signs_above=2)


CONCEPTS = [boar_crest, bee_meadow, first_night]
