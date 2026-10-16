"""Round 7: night pieces with unlit (glowing) glyphs — <font=ALIAS> + a loader sign (engine.FONT_ALIAS).

The alias costs 12 of the 50 characters, so a glowing glyph gets colour + 2-digit size + voffset +
2-digit pos and nothing else: no rotate, no scale, no mirror. Pieces are therefore small and sit over
the build (pos <= 99 per column). Unlit and lit glyphs mix: lit ones stay dark at night (silhouettes).
"""
from engine import Art
from concepts.common import *  # noqa: F401,F403


def _night(key, title, idea):
    a = Art(key, title, idea, unlit=True, bg="#0f1a26")
    return a


def glow_name():
    a = _night("glow_word", "Világító YGGDRASIL", "Egy szó éjjel is világító arany betűkkel a signek fölött, kék rúna-pontokkal a két végén.")
    word = "YGGDRASIL"
    for i, ch in enumerate(word):
        a.add(ch, -116 + 29 * i, 0, h=40, color="#fd5", tag="letter")
    for s in (-1, 1):
        a.add("ᛟ", s * 152, 0, h=30, color="#6df", tag="end")
    a.fit_above(signs_above=3)
    return a


def glow_aurora():
    a = _night("glow_aurora", "Éjféli fény", "Világító, áttetsző fényfüggöny (vastag Norse vonások), alatta sötét hegyek (nem világítanak), fölötte világító csillagok.")
    cols = ["#3f8a", "#3fba", "#2fda", "#7fea", "#a6fa"]
    for i in range(10):
        a.add("|", -110 + 24 * i, 40 + (i % 3) * 8, h=90 + (i * 37 % 40), color=cols[i % 5], tag="curtain")
    a.add("▲", -60, -50, h=60, w=110, color="#234", glow=False, tag="mtn")
    a.add("▲", 60, -56, h=48, w=90, color="#345", glow=False, tag="mtn")
    for x, y in [(-120, 130), (-20, 140), (100, 128)]:
        a.add("★", x, y, h=12, color="#ffd", tag="star")
    a.fit_above(signs_above=3, dx=30)
    return a


def glow_wolf():
    a = _night("glow_wolf", "Holdfény-farkas", "Világító sápadt hold, rajta a farkas sötét vonalrajza (az is világít, sötétkék színnel), két oldalt világító rúnák.")
    a.add("●", 0, 0, h=150, color="#eec", tag="moon")
    a.add("🐺", 0, -4, h=104, color="#124", tag="moon")
    for s, ch in ((-1, "ᚠ"), (1, "ᚱ")):
        a.add(ch, s * 110, -20, h=40, color="#6df", tag="rune")
    a.fit_above(signs_above=3)
    return a


def glow_valknut():
    a = _night("glow_valknut", "Világító valknut", "Három áttetsző, világító háromszög színkeveréssel egyetlen 5 mély stackben, fölötte arany pont.")
    for k, col in enumerate(("#e44c", "#4d6c", "#48fc")):
        x, y = polar(20, 90 + 120 * k)
        a.add("▲", x, y - 3, h=50, color=col, tag="tri")
    a.add("●", 0, 4, h=8, color="#fd5", tag="core")
    a.fit_above(signs_above=3, dx=-35)
    return a


CONCEPTS = [glow_name, glow_aurora, glow_wolf, glow_valknut]
