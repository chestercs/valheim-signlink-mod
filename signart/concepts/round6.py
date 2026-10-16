"""Round 6: designs that survive the sun. The sign text is lit like a board (TMP Surface shader):
with the sun behind the picture every colour turns dark. Translucent glyphs let the bright sky through
(stained glass), and strong silhouettes read either way. Check each with the simulator's backlight view.
"""
from engine import Art
from concepts.common import *  # noqa: F401,F403
from concepts.round3 import flying_raven


def dragon_crest_glass():
    a = Art("dragon_crest_glass", "Sárkánycímer üvegből", "Áttetsző arany sárkányok és üvegpajzs: ellenfényben az ég átüt rajtuk, nem barnulnak be.")
    a.add("🐉", -265, 20, h=290, color="#fc4c", mirror=True, tag="dragonL")
    a.add("🐉", 265, 20, h=290, color="#fc4c", tag="dragonR")
    a.add("●", 0, 10, h=190, color="#fd5c", tag="shield")
    a.add("●", 0, 10, h=166, color="#e338", tag="shield")
    a.add("ᛟ", 0, 10, h=96, color="#fffd", tag="shield")
    for x in (-90, 0, 90):
        a.add("★", x, 230, h=34 if x else 44, color="#fe6c", tag="star")
    row(a, "ᛞᚱᚨᚲᚨ", -150, 150, -220, 56, "#fffc", tag="runes")
    return a


def rose_window():
    """A round window: nothing touches across islands, every pane translucent."""
    a = Art("rose_window", "Rózsaablak", "Kerek üvegablak: arany küllőkoszorú, nyolc szirom (♦: négy színes üveg, négy tömör arany), középen rúnás üvegkorong. Ellenfényre tervezve.")
    a.add("◯", 0, 0, h=500, color=GOLD, tag="rim")
    cols = ["#e44a", "#da4", "#48fa", "#da4", "#4d6a", "#da4", "#fd5a", "#da4"]   # diagonal petals solid (budget)
    for k in range(8):
        ang = 90 - 45 * k
        x, y = polar(150, ang)
        a.add("♦", x, y, h=120, rot=ang - 90, color=cols[k], tag=f"petal{k}")
        xs, ys = polar(198, ang + 22.5)
        a.add("●", xs, ys, h=22, color="#fe8c", tag=f"bead{k}")
    a.add("●", 0, 0, h=110, color="#fd59", tag="core")
    a.add("⭕", 0, 0, h=124, color=GOLD, tag="core")
    a.add("ᛟ", 0, 0, h=54, color="#fffd", tag="core")
    return a


def huginn_glass():
    a = Art("huginn_glass", "Holló az üveghold előtt", "A Huginn-kép üvegesen: áttetsző hold, előtte tömör fekete holló. Ellenfényben is olvasható sziluett.")
    a.add("●", 40, 120, h=400, color="#ffe9", tag="moon")
    flying_raven(a, 10, 110, +1, 1.3, tag="moon")
    flying_raven(a, -250, 330, +1, 0.8, wings="#299c", tag="raven2")
    for i, x in enumerate([-260, -190, -120, 200, 270]):
        a.add("▲", x, -170 - (i % 2) * 12, h=110 + (i % 3) * 20, w=50, color="#264", tag="pine")
    row(a, "ᚺᚢᚷᛁᚾ", -230, -40, -300, 46, "#6dec", tag="runes")
    row(a, "ᛗᚢᚾᛁᚾ", 60, 250, -300, 46, "#da4c", tag="runes")
    return a


CONCEPTS = [dragon_crest_glass, rose_window, huginn_glass]
