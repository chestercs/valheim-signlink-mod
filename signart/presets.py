#!/usr/bin/env python3
"""The art that comes with SignLink (its Art tab), and stacked pictures for its Saved tab.

  presets.py OUT_DIR [--images IMAGE ...]
  ./sign.sh art        -> mod/SignLink/Art/glyph/*, mod/SignLink/Art/pixel/* (embedded in the DLL)

OUT_DIR gets glyph/ (Unicode pieces of the concept rounds, each with NAME.png) and pixel/ (pixel art and
animations); --images adds stacked pictures straight into OUT_DIR (copy those into BepInEx/config/SignLink/saved).

Three kinds, told apart by the file name (mod/SignLink/Presets/PresetLibrary.cs):
  NAME.txt         one sign text: Apply puts it on the sign
  NAME.stack.txt   texts separated by U+001D: the sign and stacked copies (a picture too big for one sign, or a
                   glyph piece whose paint levels lie one behind the other)
  NAME.anim.txt    a first line "#signlink anim fps=F", then frames separated by U+001E: Apply saves it on the sign
Every text is relative to the sign it goes on: the art is centred over it, its bottom 1 m above it. Names group in
the window by the part before " (": "Dog (moving, x)" and "Dog (moving, y)" sit together.

Glyph pieces (Unicode art from the concept rounds) also get NAME.png, a render of the texts as the engine lays them
out: the window shows it as their quick view (pixel pictures are read back from their own text instead). Every piece
gets NAME.fit, where its middle sits (art_centres.py), so SignLink can turn it around its own centre.
"""
import argparse
import os
from dataclasses import replace

import art_centres
import engine as E
import pixels as P
import artlib
from concepts import anim1, anim2, round1, round2, round3, round4, round5, round7, row1

# the art library's modules (concepts/lib_*.py, artlib.Piece) and the Art tab's group each goes under, in the tab's
# order after CLASSICS (the pieces made here below); groups.tsv tells SignLink which piece is in which group
CLASSICS = "Classics"
ART_MODULES = [("concepts.lib_pets", "The cat's family"), ("concepts.lib_cats2", "The cat's family"),
               ("concepts.lib_friends", "The cat's friends"), ("concepts.lib_animals", "Animals of Valheim"),
               ("concepts.lib_boars", "The boar family"), ("concepts.lib_necks", "The neck family"),
               ("concepts.lib_hens", "Hens, chicks and eggs"), ("concepts.lib_critters", "Forest and farm"),
               ("concepts.lib_wild", "Wild animals"), ("concepts.lib_water", "Sea and rivers"),
               ("concepts.lib_creatures", "Creatures"), ("concepts.lib_scenes", "Scenes"),
               ("concepts.lib_kitchen", "Kitchen icons"), ("concepts.lib_folk", "Heroes and bosses"),
               ("concepts.lib_world", "The world")]
# the Art tab's groups, best first (the author, 2026-10-17: rank them by which is best): the most polished and the
# most Valheim first, the families of one animal last; groups.tsv carries the order to SignLink. Then the author's
# own order (the same day): Scenes, Creatures, Animals of Valheim, Kitchen icons first; the two cat groups together,
# low, just above the necks
GROUP_ORDER = ["Scenes", "Creatures", "Animals of Valheim", "Kitchen icons", "Heroes and bosses", "The world",
               "Sea and rivers", "Forest and farm", "Wild animals", CLASSICS, "The boar family", "Hens, chicks and eggs",
               "The cat's family", "The cat's friends", "The neck family"]

FS, GS = "\u001e", "\u001d"
ABOVE = 40.0                    # bottom edge 1 m above the sign
ART_SIZE_M = 2.4                # glyph pieces: the larger side


def write(out, name, text):
    path = os.path.join(out, name)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print(f"{name}: {len(text.encode('utf-8')) // 1024} KB")


def anim(out, name, frames, fps):
    write(out, f"{name}.anim.txt", f"#signlink anim fps={fps:g}\n" + FS.join(frames))


def one_sign_stack(art, size_m=ART_SIZE_M, above=ABOVE):
    """A piece designed over a grid of cells -> texts for ONE sign and copies stacked on it, rear first: the art is
    scaled so its larger side is size_m, centred over the sign, its bottom `above` wu up; every paint level of
    touching glyphs (engine.assign_planes) is one sign of the stack, all in one plane. -> (texts, placed)."""
    items = [replace(p, backdrop=False, cell=None, layer=None) for p in art.items]
    l, b, r, t = E.placed_bbox(items)
    f = size_m * 40 / max(r - l, t - b)
    cx = (l + r) / 2
    moved = [replace(p, x=(p.x - cx) * f, y=(p.y - b) * f + above, size=p.size * f,
                     font=E.FONT_PATH if p.font == E.FONT_ALIAS else p.font) for p in items]
    one = E.Art(art.key, art.title, art.idea, origin=(0, 0), grid=E.Grid(1, 1, depth=16), unlit="path", mode="detailed")
    one.anchors = True                                  # every layer the same bounds: the stack draws in its own order
    one.items = moved
    if hasattr(art, "plane_kw"):
        one.plane_kw = art.plane_kw
    rows = sorted(E.build_planes(one), key=lambda row: row["layer"])
    worst = max(row["err"] for row in rows)
    if worst > 1.0:
        print(f"  {art.key}: up to {worst:.1f} wu off after the round trip")
    return [row["text"] for row in rows], [q for row in rows for q in row["placed"] if q.color[3] > 0], one.grid   # not the anchors


def piece(out, name, art):
    texts, placed, grid = one_sign_stack(art)
    if len(texts) == 1:
        write(out, f"{name}.txt", texts[0])
    else:
        write(out, f"{name}.stack.txt", GS.join(texts))
    E.render(placed, os.path.join(out, f"{name}.png"), px=480, bg=art.bg, grid=grid)


def library(glyph, pixel, module):
    """The pieces of one concepts/lib_*.py module (artlib.Piece), written from artlib.texts: the texts art_preview.py
    renders. A glyph piece also gets NAME.png, rendered from its texts. -> their 'folder/name's."""
    import importlib
    names = []
    for p in importlib.import_module(module).pieces():
        problems = artlib.check(p)
        if problems:
            raise SystemExit(f"{module}: {p.name}: " + "; ".join(problems))
        suffix, text, first, frames = artlib.texts(p)
        if frames and p.fps > 0 and artlib.play_ms(frames, p.fps) > artlib.MAX_PLAY_MS:   # it would never move in game
            raise SystemExit(f"{module}: {p.name}: {artlib.play_ms(frames, p.fps):.0f} ms a second to play, at most {artlib.MAX_PLAY_MS:.0f}")
        write(glyph if p.kind == "glyph" else pixel, p.name + suffix, text)
        names.append(("glyph/" if p.kind == "glyph" else "pixel/") + p.name)
        if p.kind == "glyph":
            placed = [q for i, t in enumerate(first) for q in E.layout_sign(t, "A1", layer=i) if q.color[3] > 0]
            E.render(placed, os.path.join(glyph, f"{p.name}.png"), px=480, bg=(p.art or p.frames[0]).bg)
    return names


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("--images", nargs="*", default=[], help="pictures to add as stacks (6 mm pixels)")
    a = ap.parse_args()
    glyph, pixel = os.path.join(a.out, "glyph"), os.path.join(a.out, "pixel")
    for d in (glyph, pixel):
        os.makedirs(d, exist_ok=True)

    # pixel pieces: the cat 30 x 26 pixels of 5.6 cm (1.7 m), the fjord 2.6 m wide, the drakkar 2 m wide
    # the cat and the fjord sit in their themes' groups, and their still and slow variants went: Moving off makes the
    # still (0.20.168, the author: "a classicból vegyük ki ami nem oda való. pl cicából a standing már legacy is")
    cat = anim2.cat(pitch=2.25, above=ABOVE)
    anim(pixel, "Cat (moving, tail and blink)", cat, 4)
    # the fjord as a still only: its moving sky was 565 KB, over every multiplayer limit (the author: "moving sky is
    # nagyon smelly ... nem is multi kompatibilis")
    fjord = anim1.fjord(width=105.0, above=ABOVE)
    write(pixel, "Fjord at dusk.txt", fjord[0])
    moved = {"The cat's family": "pixel/Cat (moving, tail and blink)", "Scenes": "pixel/Fjord at dusk"}
    ship = [[c or "" for c in row] for row in row1.drakkar_pixels()]
    pitch = 2.0
    write(pixel, "Dragon ship.txt",
          P.column_text(P.items(ship, pitch, 0.0, ABOVE, "A1", size=P.glyph_size(pitch, cover=2.0)), nd=P.position_decimals(pitch)))

    # glyph pieces from the concept rounds
    piece(glyph, "Dragon ship", round4.drakkar2())
    piece(glyph, "Dragon crest", round4.dragon_crest())
    piece(glyph, "Eikthyr", round2.eikthyr())
    piece(glyph, "Huginn and Muninn", round3.huginn_muninn())
    piece(glyph, "Jormungandr", round2.jormungandr())
    piece(glyph, "Midnight light", round7.glow_aurora())
    piece(glyph, "Mjolnir", round1.mjolnir())
    piece(glyph, "Moon halo", round5.moon_glow())
    groups = GROUP_ORDER
    assert sorted(set(g for _, g in ART_MODULES) | {CLASSICS}) == sorted(groups), "every group ranked once"
    lines, seen = [], set()
    for mod, group in ART_MODULES:
        if group not in seen:
            seen.add(group)
            if group in moved:
                lines.append(f"{moved[group]}\t{group}")         # first in its group
        lines += [f"{n}\t{group}" for n in library(glyph, pixel, mod)]
    with open(os.path.join(a.out, "groups.tsv"), "w", encoding="utf-8", newline="\n") as f:   # not .txt: no piece
        f.write("# the Art tab's groups, in order; a piece not listed is a classic\n")
        f.write("".join(f"#group\t{g}\n" for g in groups) + "".join(line + "\n" for line in lines))
    art_centres.write_all(a.out)                    # NAME.fit: where each piece sits, to turn it around its centre
    # every piece, glyph or pixel, classic or library, fits a multiplayer sign and area with room to spare (the author:
    # "tényleg a glypheknél is figyelünk a kb sizera remélem"); a bigger one does not ship
    big = []
    for d in (glyph, pixel):
        for name in sorted(os.listdir(d)):
            if name.endswith(".txt"):
                kb = os.path.getsize(os.path.join(d, name)) / 1024
                if kb > artlib.MAX_ANIM_KB:
                    big.append(f"{os.path.basename(d)}/{name}: {kb:.0f} KB")
    if big:
        raise SystemExit(f"over {artlib.MAX_ANIM_KB} KB, not for every server: " + "; ".join(big))

    if a.images:
        from PIL import Image
        import image_import as I
        from vanilla_hq import bands
        for path in a.images:
            img = Image.open(path)
            img.seek(0)
            img = img.convert("RGB")
            W, H = I.fit(img.width, img.height, min(198000, img.width * img.height))
            px = I.grid(*I.downscale(img, W, H))
            pitch = 0.25                                    # 6 mm pixels: 499 px are 3.1 m
            size = P.glyph_size(pitch, cover=2.0)
            texts = [t for _, _, t in bands(px, pitch, 0.0, ABOVE, "A1", None, E.DEFAULT_GRID, 400, size=size)]
            name = os.path.splitext(os.path.basename(path))[0].replace("_", " ").capitalize()
            write(a.out, f"{name} ({W * pitch / 40:.1f} x {H * pitch / 40:.1f} m, {len(texts)} signs).stack.txt", GS.join(texts))


if __name__ == "__main__":
    main()
