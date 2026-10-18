#!/usr/bin/env python3
"""Renders a QR template's sign text (written by the C# checks to mod/SignLink.Tests/qr_sample.txt) as the game draws
it, so a QR reader can be run on what the sign shows (from the text, not the code's modules).

Where each line lands comes from the TMP model (engine.layout_sign: the glyph's centre). How big a block is drawn
does not: the game draws the block glyph (■) S*(0.726*sx + 0.089) em wide and S*0.793 em tall (measured in game
2026-10-17; part of its width does not stretch with <scale>), while the engine stretches the whole outline, which
made a QR code no phone read look fine here. So the blocks are drawn as the game draws them (the same numbers as
Snippets.BlockA, BlockC, BlockH in the mod).

  qr_render.py IN.txt OUT.png        (./sign.sh image's container; then a zbar container reads OUT.png)
  qr_render.py DIR                   every DIR/*.txt to its .png (qr_sweep: sizes, versions and levels)
"""
import os
import sys

from PIL import Image, ImageDraw

import engine as E

BLOCK_A, BLOCK_C, BLOCK_H = 0.726, 0.089, 0.793


def main(src, png):
    text = open(src, encoding="utf-8").read()
    placed = [q for q in E.layout_sign(text, "A1") if q.color[3] > 0]     # not the invisible anchors
    rects = []
    for p in placed:
        lying = abs(abs(p.rotate) - 90) < 1e-9                              # a narrow block turned a quarter (Snippets.Block)
        assert p.glyph == "■" and (lying or abs(p.rotate) < 1e-9), f"a QR text holds blocks, upright or lying ({p.glyph}, {p.rotate})"
        w, h = 2 * p.size * (BLOCK_A * abs(p.scale) + BLOCK_C), 2 * p.size * BLOCK_H
        if lying:
            w, h = h, w
        rects.append((p.x - w / 2, p.y - h / 2, p.x + w / 2, p.y + h / 2, p.color))
    l, b = min(r[0] for r in rects), min(r[1] for r in rects)
    r_, t = max(r[2] for r in rects), max(r[3] for r in rects)
    pad, px = (r_ - l) * 0.15, 700
    k = px / (r_ - l + 2 * pad)
    img = Image.new("RGB", (px, round((t - b + 2 * pad) * k)), "#6b4f2e")    # on a wooden board's brown
    d = ImageDraw.Draw(img)
    for x0, y0, x1, y1, c in rects:                                         # in order: later lines over earlier ones
        d.rectangle([(x0 - l + pad) * k, (t + pad - y1) * k, (x1 - l + pad) * k, (t + pad - y0) * k], fill=tuple(c[:3]))
    img.save(png)
    print(f"{len(rects)} blocks rendered to {png}")


if __name__ == "__main__":
    if os.path.isdir(sys.argv[1]):                                          # a folder (qr_sweep): every .txt to its .png
        for f in sorted(os.listdir(sys.argv[1])):
            if f.endswith(".txt"):
                main(os.path.join(sys.argv[1], f), os.path.join(sys.argv[1], f[:-4] + ".png"))
    else:
        main(*sys.argv[1:3])
