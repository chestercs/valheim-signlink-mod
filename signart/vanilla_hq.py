#!/usr/bin/env python3
"""A still picture for vanilla clients in high quality, split over several signs that hang at the exact same
place (SignLink 0.3.1 clones a sign at its rotation and position, each copy 0.5 mm further along the normal),
so every band is drawn in one plane: no parallax, no seam.

  vanilla_hq.py IMAGE [--frame 0] [--sign B1-1] [--pixels 198000] [--pitch 1.0] [--cover 2]
                [--above-m 1.0] [--max-kb 400] [--dry-run]

It writes the band texts to out/vanilla_hq/ (one file a sign, the first for the sign itself, the rest for its
copies); a save edit puts them on. SignLink's own window makes the same texts itself. Bands are horizontal strips of whole rows, each as many rows as
fit --max-kb (Steam's 512 KiB message limit, docs/limits.md) and 49 500 pixels.

The picture is opaque: alpha is dropped, never upscaled (at most the image's own pixels), and every pixel
glyph is --cover pitches wide, so each point lies under cover x cover glyphs. From a distance the SDF
shader draws pixels of a few screen pixels half transparent (2026-10-08: the 1.12-pitch Starry Night looked
see-through); stacked layers make them opaque, and up close the last drawn wins, so it stays sharp.
"""
import argparse
import json
import os

import engine as E
import image_import as I
import pixels as P
from PIL import Image
from rig import rig_grid


def bands(px, pitch, center_x, bottom, cell, layer, grid, max_kb, max_px=49500, size=None, font=E.FONT_PATH):
    """-> list of (row0, row1, text): the largest bands of whole rows under max_kb and max_px each. A band the
    column encoding cannot solve (one that crosses the sign's height with the wrong voffset signs, or too tall) is
    halved until it can. font=None: no <font> tag, the sign's own lit material (dark at night). Transparent
    pixels ('') are left out; rows with none at all get no sign."""
    H, W = len(px), len(px[0])
    out, r0 = [], 0
    while r0 < H:
        rows = min(H - r0, max_px // W)
        text = None
        while any(px[r][c] for r in range(r0, r0 + rows) for c in range(W)):
            try:
                text = P.column_text(P.items(px, pitch, center_x, bottom, cell, layer, rows=range(r0, r0 + rows), size=size,
                                             font=font),
                                     grid, nd=P.position_decimals(pitch), default_step=round(pitch / 2, 6))
            except ValueError:
                if rows == 1:
                    raise
                rows = max(1, rows // 2)
                continue
            if len(text.encode("utf-8")) <= max_kb * 1024 or rows == 1:
                break
            rows = max(1, int(rows * max_kb * 1024 / len(text.encode("utf-8")) * 0.97))
            text = None
        if text is not None:
            out.append((r0, r0 + rows, text))
        r0 += rows
    if not out:
        raise ValueError("the picture is fully transparent: nothing to put on a sign")
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("image")
    ap.add_argument("--frame", type=int, default=0, help="frame of an animated image (GIF)")
    ap.add_argument("--sign", default="B1-1")
    ap.add_argument("--pixels", type=int, default=198000)
    ap.add_argument("--pitch", type=float, default=1.0, help="wu per pixel: 0.25, 0.5, 0.75, 1, 1.5 or 2 (exact line heights)")
    ap.add_argument("--cover", type=float, default=2.0, help="pixel glyph width in pitches (whole numbers)")
    ap.add_argument("--above-m", type=float, default=1.0, help="bottom edge above the top of the build, metres")
    ap.add_argument("--max-kb", type=float, default=400)
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()

    g = rig_grid(json.load(open("rigs/bakterhaz-render-engine.json", encoding="utf-8")))
    cell, layer = a.sign.split("-")
    img = Image.open(a.image)
    img.seek(a.frame)
    img = img.convert("RGB")
    W, H = I.fit(img.width, img.height, min(a.pixels, img.width * img.height))
    px = I.grid(*I.downscale(img, W, H))
    size = P.glyph_size(a.pitch, cover=a.cover)
    parts = bands(px, a.pitch, g.center_x, g.top_y + a.above_m * 40, cell, int(layer), g, a.max_kb, size=size)
    kb = [len(t.encode("utf-8")) / 1024 for _, _, t in parts]
    print(f"{a.image}: {W} x {H} = {W * H} px, pitch {a.pitch} wu = {W * a.pitch / 40:.2f} x {H * a.pitch / 40:.2f} m, "
          f"cover {a.cover}, {len(parts)} signs, rows per sign {[r1 - r0 for r0, r1, _ in parts]}, {min(kb):.0f}-{max(kb):.0f} KB each")
    if a.dry_run:
        return

    out = os.path.join("out", "vanilla_hq")
    os.makedirs(out, exist_ok=True)
    stem = os.path.splitext(os.path.basename(a.image))[0]
    for k, (_, _, t) in enumerate(parts):
        with open(os.path.join(out, f"{stem}-{k + 1}.txt"), "w", encoding="utf-8") as f:
            f.write(t)
    print(f"{len(parts)} texts -> {out}/{stem}-1..{len(parts)}.txt (the first for {a.sign}, the rest for its copies)")


if __name__ == "__main__":
    main()
