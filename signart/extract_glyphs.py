#!/usr/bin/env python3
"""Extract the glyph vocabulary a Valheim sign can render, with real outlines and TMP metrics.

Reads the TTF payloads of the game's Font objects (resources.assets) and resolves every
candidate character through the sign's TextMeshPro font chain, as configured in the game:

  sign Text (TextMeshProUGUI, fontStyle Bold) -> Valheim-Norse
     bold typeface (font weight table, 700) -> Valheim-Norsebold   (real bold, no faux bold)
     fallback -> Fallback-NotoSansThin (NotoSans-ExtraLight)
        fallbacks -> Armenian, Bengali, Devanagari, Georgian (ExtraLight), NotoSansJP-Thin,
                     NotoSansKR-Thin, Malayalam, NotoSansSC-Thin, Thai, NotoEmoji-Light, Arabic, Hebrew

Glyphs from the Noto chain get TMP faux bold (the font assets have no bold typeface).
The TMP face metrics (ascent/descent at pointSize 48) come from the font assets in bundle
8d5dbad8 (see docs/sign-art.md); they decide the vertical centering of a sign's text.

Usage: extract_glyphs.py <valheim_Data> <out-dir>
Writes <out-dir>/fonts/*.ttf (cache) and <out-dir>/glyphs.json.
"""
import json
import os
import sys
import unicodedata

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

# TMP font assets of the sign chain: source TTF, face metrics (pointSize 48) and faux-bold weight.
# boldStyle drives the SDF dilation of faux bold; None = rendered by the real bold typeface.
CHAIN = [
    # key,        ttf,                    ascent, descent, boldStyle
    ("norsebold", "Norsebold",            44.16, -8.64,  None),
    ("norse",     "Norse",                44.16, -8.64,  0.75),   # only if Norsebold lacks the char
    ("notosans",  "NotoSans-ExtraLight",  51.312, -14.064, 0.10),
    ("jp",        "NotoSansJP-Thin",      55.68, -13.824, 0.45),
    ("kr",        "NotoSansKR-Thin",      55.68, -13.824, 0.45),  # boldStyle not read: assumed = JP
    ("sc",        "NotoSansSC-Thin",      55.68, -13.824, 0.45),  # boldStyle not read: assumed = JP
    ("emoji",     "NotoEmoji-Light",      44.53125, -11.71875, 0.30),
]
POINT_SIZE = 48.0
# Visual stroke growth of TMP faux bold, in em per side, per unit of boldStyle. Estimated from
# the SDF shader (weight/4 * gradient scale 11 * ScaleRatioA / 48pt); unverified in game.
FAUX_BOLD_EM_PER_STYLE = 0.045

RANGES = [
    (0x21, 0x7E),      # ASCII (Norse letters, digits, punctuation)
    (0xA1, 0xFF),      # Latin-1 (Hungarian accents: Á É Í Ó Ö Ú Ü)
    (0x150, 0x151), (0x170, 0x171),  # Ő ő Ű ű
    (0x16A0, 0x16F0),  # runic
    (0x2190, 0x21FF),  # arrows
    (0x2200, 0x22FF),  # math
    (0x2300, 0x23FF),  # technical
    (0x2500, 0x257F),  # box drawing
    (0x2580, 0x259F),  # block elements
    (0x25A0, 0x25FF),  # geometric shapes
    (0x2600, 0x26FF),  # misc symbols
    (0x2700, 0x27BF),  # dingbats
    (0x2B00, 0x2BFF),  # misc symbols and arrows
    (0x3000, 0x303F),  # CJK symbols (、。〇〈〉「」…)
]
# Emoji are many and heavy; keep the ones that can carry a picture. Matched against unicode names.
EMOJI_RANGES = [(0x1F300, 0x1F5FF), (0x1F600, 0x1F6FF), (0x1F900, 0x1F9FF), (0x1FA70, 0x1FAFF)]
EMOJI_KEYWORDS = [
    "WOLF", "BIRD", "EAGLE", "OWL", "DOVE", "FEATHER", "RAVEN", "DRAGON", "SNAKE", "FISH", "WHALE",
    "SHARK", "OCTOPUS", "DEER", "BOAR", "PIG", "BEAR", "HORSE", "GOAT", "RAM", "OX", "BULL", "BAT",
    "SPIDER", "SCORPION", "BEE", "BUTTERFLY", "TREE", "EVERGREEN", "LEAF", "MUSHROOM", "HERB",
    "FIRE", "FLAME", "MOON", "SUN", "STAR", "COMET", "CLOUD", "LIGHTNING", "SNOWFLAKE", "WAVE",
    "MOUNTAIN", "VOLCANO", "SWORD", "DAGGER", "AXE", "SHIELD", "BOW AND ARROW", "HAMMER", "PICK",
    "SKULL", "BONE", "EYE", "CROWN", "SHIP", "SAILBOAT", "CANOE", "ANCHOR", "KEY", "CASTLE",
    "BEER", "HORN", "DRUM", "TROLL", "GHOST", "SPARKLES", "GEM", "COIN", "SCROLL", "MAP", "COMPASS",
    "HAND", "FIST", "FOOTPRINTS", "WIND", "TORNADO", "ROCK", "WOOD", "HUT", "FLAG", "BLACK HEART",
    "HEART", "TOOTH", "CANDLE", "MAGE", "ELF", "ZOMBIE", "SPIRAL", "SEEDLING", "WATER", "CROSS",
]


def load_fonts(data_dir, cache):
    os.makedirs(cache, exist_ok=True)
    need = {ttf for _, ttf, *_ in CHAIN}
    if all(os.path.exists(os.path.join(cache, n + ".ttf")) for n in need):
        return
    import UnityPy
    env = UnityPy.load(os.path.join(data_dir, "resources.assets"))
    for o in env.objects:
        if o.type.name != "Font":
            continue
        d = o.read()
        if d.m_Name in need and d.m_FontData:
            with open(os.path.join(cache, d.m_Name + ".ttf"), "wb") as f:
                f.write(bytes(d.m_FontData))
    missing = [n for n in need if not os.path.exists(os.path.join(cache, n + ".ttf"))]
    if missing:
        sys.exit(f"fonts not found in resources.assets: {missing}")


def glyph_record(font, ch):
    upem = font["head"].unitsPerEm
    gs = font.getGlyphSet()
    gname = font.getBestCmap()[ord(ch)]
    g = gs[gname]
    adv = font["hmtx"][gname][0] / upem
    bp = BoundsPen(gs)
    g.draw(bp)
    if bp.bounds is None:
        return None  # blank glyph (space-like): nothing to draw
    sp = SVGPathPen(gs, ntos=lambda v: f"{v:.4f}".rstrip("0").rstrip("."))
    g.draw(TransformPen(sp, (1 / upem, 0, 0, 1 / upem, 0, 0)))
    x0, y0, x1, y1 = (v / upem for v in bp.bounds)
    return {"adv": round(adv, 4), "bb": [round(x0, 4), round(y0, 4), round(x1, 4), round(y1, 4)],
            "d": sp.getCommands()}


def main():
    data_dir, out = sys.argv[1], sys.argv[2]
    cache = os.path.join(out, "fonts")
    load_fonts(data_dir, cache)
    fonts = {key: TTFont(os.path.join(cache, ttf + ".ttf")) for key, ttf, *_ in CHAIN}
    cmaps = {k: f.getBestCmap() for k, f in fonts.items()}
    meta = {k: {"A": a / POINT_SIZE, "D": d / POINT_SIZE,
                "bold": 0.0 if b is None else round(b * FAUX_BOLD_EM_PER_STYLE, 4)}
            for k, _, a, d, b in CHAIN}

    cands = [cp for a, z in RANGES for cp in range(a, z + 1)]
    for a, z in EMOJI_RANGES:
        for cp in range(a, z + 1):
            name = unicodedata.name(chr(cp), "")
            if any(k in name for k in EMOJI_KEYWORDS):
                cands.append(cp)

    glyphs = {}
    for cp in cands:
        ch = chr(cp)
        for key, *_ in CHAIN:
            if key == "norse" and cp in cmaps["norsebold"]:
                continue
            if cp in cmaps[key]:
                rec = glyph_record(fonts[key], ch)
                if rec:
                    rec["f"] = key
                    rec["n"] = unicodedata.name(ch, f"U+{cp:04X}").title()
                    glyphs[ch] = rec
                break
    doc = {"fonts": meta, "pointSize": POINT_SIZE, "glyphs": glyphs}
    path = os.path.join(out, "glyphs.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, separators=(",", ":"))
    by_font = {}
    for g in glyphs.values():
        by_font[g["f"]] = by_font.get(g["f"], 0) + 1
    print(f"{len(glyphs)} glyphs -> {path} ({os.path.getsize(path) // 1024} KiB) by font: {by_font}")


if __name__ == "__main__":
    main()
