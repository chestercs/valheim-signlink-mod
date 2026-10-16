"""Round 5: round translucent backgrounds, alpha play, other grids, small logos centred over the build.

- Backgrounds behind a whole logo touch everything, so either the logo lives in one deep stack
  (Grid(1, 1, 10): ten signs behind each other, safe) or the background goes to a backdrop cell deep
  behind the build (experimental until the in-game depth test passes).
- `small_*`: the author's test request — a quarter-size logo centred over the build, its bottom
  three sign heights above the top row of boards.
"""
from engine import Art, Grid, DEFAULT_GRID
from concepts.common import *  # noqa: F401,F403
from concepts.round1 import valhub
from concepts.round2 import jormungandr
from concepts.round3 import huginn_muninn
from concepts.round4 import dragon_crest


def _small(fn, key, title, min_h=18):
    base = fn()
    a = base.retarget(DEFAULT_GRID, key=key, title=title,
                      idea=f"{base.title} negyed méretben, a signek fölött középen, 3 signnyi magasan; a legapróbb részek legalább 45 cm-esek.")
    return a.fit_above(signs_above=3, factor=0.25, min_h=min_h)


def small_dragon_crest():
    return _small(dragon_crest, "small_dragon_crest", "Sárkánycímer — kicsi, középen")


def small_valhub():
    return _small(valhub, "small_valhub", "VALHUB — kicsi, középen")


def small_huginn_muninn():
    return _small(huginn_muninn, "small_huginn_muninn", "Huginn és Muninn — kicsi, középen", min_h=0)


def valknut_glass():
    """One stack of ten signs: everything may overlap, colours mix through alpha."""
    a = Art("valknut_glass", "Üvegvalknut", "Egyetlen 10 mély stack (1×1×10): áttetsző sötét korong, arany gyűrű, három "
            "egymásba csúsztatott áttetsző háromszög színkeveréssel, középen arany pont, négy rúna.",
            grid=Grid(1, 1, 10))
    a.add("●", 0, 0, h=330, color="#0128", tag="disc")
    a.add("◯", 0, 0, h=340, color=GOLD, tag="ring")
    for k, col in enumerate(("#e44a", "#4d6a", "#48fa")):
        x, y = polar(58, 90 + 120 * k)         # same orientation, staggered: the valknut silhouette
        a.add("▲", x, y - 8, h=128, color=col, tag="tri")
    a.add("●", 0, 0, h=22, color=GOLD, tag="core")
    for ch, (x, y) in zip("ᚨᛊᚷᚱ", [(0, 128), (128, 0), (0, -128), (-128, 0)]):
        a.add(ch, x, y, h=34, color=BONE, tag="rune")
    return a.fit_above(signs_above=3, factor=0.5)


def moon_glow():
    a = Art("moon_glow", "Holdudvar", "Fenrir-hold áttetsző fényudvarral: két alpha-gyűrű a hold mögött, ugyanabban a cellában (3×3×5).")
    a.add("●", 0, 120, h=560, color="#fd63", tag="moon")
    a.add("●", 0, 120, h=460, color="#fe94", tag="moon")
    a.add("●", 0, 120, h=370, color="#eec", tag="moon")
    a.add("●", -100, 225, h=50, color="#ccb", tag="moon")
    a.add("🐺", 0, 105, h=260, color=INK, tag="moon")
    for i, x in enumerate([-270, -170, -70, 30, 130, 230]):
        a.add("▲", x, -250 + (i % 2) * 10, h=80 + (i % 3) * 14, w=44, color=PINE, tag="pine")
    for x, y, h in [(-330, 380, 22), (330, 360, 18), (-340, 0, 14), (340, -40, 16)]:
        a.add("★", x, y, h=h, color="#ffd", tag="star")
    row(a, "ᚠᛖᚾᚱᛁᚱ", -175, 175, -360, 60, GOLD, tag="runes")
    return a


def dragon_crest_backdrop():
    a = dragon_crest().retarget(Grid(3, 3, 5, backdrop=((1, 1),)), key="dragon_crest_bg",
                                title="Sárkánycímer áttetsző korongon",
                                idea="Kísérleti: az áttetsző sötét korong és az arany perem egy háttér-cellában (BG1) áll, "
                                     "mélyen a rács mögött, így az egész címer alá kerülhet. Előbb a mélységi tesztet kell igazolni.")
    bg = Art("tmp", "tmp", origin=(a.ox, a.oy))
    bg.add("●", 0, 20, h=640, color="#1236", backdrop=True, tag="bg")
    bg.add("◯", 0, 20, h=660, color="#da48", backdrop=True, tag="bg")
    for p in a.items:
        p.z += 2
    a.items = bg.items + a.items
    return a


def jormungandr_row():
    a = jormungandr().retarget(Grid(10, 1, 10), key="jormungandr_10x1x10", title="Jörmungandr 10×1×10 rácson",
                               idea="Ugyanaz a mű egy sorban álló 10 cellán, cellánként 10 mély stackkel (könnyebb elrejteni); "
                                    "a rács fölött középen, 75%-os méretben (50%-nál a pikkelyek összeérnének).")
    return a.fit_above(signs_above=3, factor=0.75)


CONCEPTS = [small_dragon_crest, small_valhub, small_huginn_muninn, valknut_glass, moon_glow,
            dragon_crest_backdrop, jormungandr_row]
