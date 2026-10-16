#!/usr/bin/env python3
"""An animated contact sheet of the art library: every piece of a lib module moving at its own fps on one GIF.

  art_showcase.py OUT_DIR concepts.lib_pets [concepts.lib_x ...] [--tile 220] [--seconds 4]

Each piece is rendered from the texts that ship (artlib.texts, as art_preview.py does), so the sheet shows what a
player gets. OUT_DIR/<module>.gif: a grid of tiles with names, ticking at 12 frames a second; stills stand still.
"""
import argparse
import importlib
import os

from PIL import Image, ImageDraw, ImageFont

import art_preview as V
import artlib as A
import engine as E

TICK = 1 / 12


def frames_of(piece, tile):
    """-> ([PIL images], fps): the piece's frames rendered from its texts, fitted into a tile."""
    _, _, first, frames = A.texts(piece)
    signs = [[f] for f in frames] if frames else [first]
    allp = [q for s in signs for q in V.placed_of(s)]
    box = E.bounds(allp, margin=8)
    out = []
    for k, s in enumerate(signs):
        p = f"/tmp/showcase_{k}.png"
        E.render(V.placed_of(s), p, px=tile - 12, bg=V.BG, box=box)
        out.append(Image.open(p).convert("RGB"))
        os.remove(p)
    return out, (piece.fps if frames else 0)


def sheet(module, out_dir, tile, seconds):
    pieces = importlib.import_module(module).pieces()
    rendered = [(p.name, *frames_of(p, tile)) for p in pieces]
    cols = 4
    rows = (len(rendered) + cols - 1) // cols
    label_h = 40
    W, H = cols * tile, rows * (tile + label_h)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 13)
    except OSError:
        font = ImageFont.load_default()
    base = Image.new("RGB", (W, H), (15, 26, 38))
    d = ImageDraw.Draw(base)
    for i, (name, _, _) in enumerate(rendered):
        x, y = (i % cols) * tile, (i // cols) * (tile + label_h)
        lines, line = [], ""
        for word in name.split(" "):
            if line and d.textlength(line + " " + word, font=font) > tile - 10:
                lines.append(line)
                line = word
            else:
                line = (line + " " + word).strip()
        lines.append(line)
        d.multiline_text((x + 6, y + tile + 2), "\n".join(lines[:2]), fill=(230, 230, 220), font=font, spacing=2)
    out = []
    for t in range(int(seconds / TICK)):
        im = base.copy()
        for i, (_, imgs, fps) in enumerate(rendered):
            k = int(t * TICK * fps) % len(imgs) if fps else 0
            f = imgs[k]
            x, y = (i % cols) * tile, (i // cols) * (tile + label_h)
            im.paste(f, (x + (tile - f.width) // 2, y + (tile - f.height) // 2))
        out.append(im)
    path = os.path.join(out_dir, module.split(".")[-1].replace("lib_", "") + ".gif")
    # one palette for every frame (from a strip of them all), so unchanged pixels stay the same index and the GIF
    # encoder stores only what moves
    strip = Image.new("RGB", (W, H * min(len(out), 8)))
    for i, f in enumerate(out[:: max(1, len(out) // 8)][:8]):
        strip.paste(f, (0, i * H))
    ref = strip.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    pal = [f.quantize(palette=ref, dither=Image.Dither.NONE) for f in out]
    pal[0].save(path, save_all=True, append_images=pal[1:], duration=int(TICK * 1000), loop=0, optimize=True)
    print(f"{path}: {len(pieces)} pieces, {len(out)} frames, {os.path.getsize(path) // 1024} KB")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("modules", nargs="+")
    ap.add_argument("--tile", type=int, default=220)
    ap.add_argument("--seconds", type=float, default=4.0)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    for m in a.modules:
        sheet(m, a.out, a.tile, a.seconds)


if __name__ == "__main__":
    main()
