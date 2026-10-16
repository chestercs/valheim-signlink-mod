#!/usr/bin/env python3
"""A still picture for vanilla clients in high quality, split over several signs that hang at the exact same
place (SignLink 0.3.1 clones a sign at its rotation and position, each copy 0.5 mm further along the normal),
so every band is drawn in one plane: no parallax, no seam.

  vanilla_hq.py IMAGE [--frame 0] [--block 1] [--sign B1-1] [--pixels 198000] [--pitch 1.0] [--cover 2]
                [--above-m 1.0] [--max-kb 400] [--prepare] [--dry-run] [--screen [--max-mb 100]]

--prepare deletes every other sign of the block first (keeps --sign), and the copies an earlier run stacked
on it. Bands are horizontal strips of whole rows, each as many rows as fit --max-kb (Steam's 512 KiB message
limit, docs/limits.md) and 49 500 pixels.

The picture is opaque: alpha is dropped, never upscaled (at most the image's own pixels), and every pixel
glyph is --cover pitches wide, so each point lies under cover x cover glyphs. From a distance the SDF
shader draws pixels of a few screen pixels half transparent (2026-10-08: the 1.12-pitch Starry Night looked
see-through); stacked layers make them opaque, and up close the last drawn wins, so it stays sharp.

--screen does not write: it plays the whole image (a GIF at its own speed) as a SignLink texture screen
exactly over the still the same arguments wrote. Vanilla players keep seeing the still; SignLink players see
the moving picture. The screen hangs on the kept sign and is a little larger than the still (it covers the
half-pitch rim of the --cover glyphs). Every stacked sign's text is hidden while it plays: the others get an
invisible 1 x 1 screen that hides their text (SignLink 0.3; from 0.4 a screen hides its whole stack itself).
Nothing of it is saved.
"""
import argparse
import json
import math

import engine as E
import image_import as I
import live
import pixels as P
from PIL import Image, ImageOps
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


def play_screen(a, g, block, cell, layer):
    """The texture screen over the still written with the same arguments (see the module doc)."""
    img = Image.open(a.image)
    if img.format == "JPEG":
        img = ImageOps.exif_transpose(img)                  # as SignLink turns phone photos (EXIF in JPEGs)
    W, H = I.fit(img.width, img.height, min(a.pixels, img.width * img.height))
    sx, sy = g.pos(cell, layer)
    cx = g.center_x - sx                                    # the still's centre relative to the kept sign, wu
    cy = g.top_y + a.above_m * 40 + H * a.pitch / 2 - sy
    raw, w, h, fps, _, _, n = live.gif_frames(a.image, a.max_mb)
    pitch = a.pitch * W / w * (W + 2) / W                   # same physical width, plus one pitch of overscan
    print(f"{a.image}: {n} frames -> {len(raw) // (w * h * 3)} at {fps} fps, {w} x {h}, {len(raw) / 1048576:.1f} MB, "
          f"over the {W} x {H} still at ({cx:.2f}, {cy:.2f}) wu, pitch {pitch:.5f}")
    if a.dry_run:
        return
    keep = live.ids_by_position({a.sign: block[a.sign]})[a.sign]
    kp = block[a.sign]
    version = tuple(int(x) for x in live.call("GET", "/ping")["version"].split("."))
    stack = [] if version >= (0, 4, 0) else [s["id"] for s in live.call("GET", "/signs", radius=500)["signs"]
                                             if s["id"] != keep and math.dist(s["pos"], kp) < 0.02]
    print(f"{len(stack)} more stacked signs get their text hidden")
    for sid in stack:
        live.call("POST", "/screen", bytes(3), id=sid, w=1, h=1, fps=1, cx=cx, cy=cy, pitch=0.0001, hide=1)
    print("screen ->", live.call("POST", "/screen", raw, timeout=300, id=keep, w=w, h=h, fps=fps, cx=round(cx, 3),
                                 cy=round(cy, 3), pitch=round(pitch, 6), hide=1, filter="bilinear"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("image")
    ap.add_argument("--frame", type=int, default=0, help="frame of an animated image (GIF)")
    ap.add_argument("--block", type=int, default=1)
    ap.add_argument("--sign", default="B1-1")
    ap.add_argument("--pixels", type=int, default=198000)
    ap.add_argument("--pitch", type=float, default=1.0, help="wu per pixel: 0.25, 0.5, 0.75, 1, 1.5 or 2 (exact line heights)")
    ap.add_argument("--cover", type=float, default=2.0, help="pixel glyph width in pitches (whole numbers)")
    ap.add_argument("--above-m", type=float, default=1.0, help="bottom edge above the top of the build, metres")
    ap.add_argument("--max-kb", type=float, default=400)
    ap.add_argument("--prepare", action="store_true", help="delete every other sign of the block first")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--screen", action="store_true", help="play the image as a texture screen over the still")
    ap.add_argument("--max-mb", type=float, default=100, help="--screen: cap on the raw RGB frames")
    a = ap.parse_args()

    g = rig_grid(json.load(open("rigs/bakterhaz-render-engine.json", encoding="utf-8")))
    block = json.load(open("rigs/bakterhaz-row.json", encoding="utf-8"))["blocks"][a.block - 1]["signs"]
    cell, layer = a.sign.split("-")
    if a.screen:
        return play_screen(a, g, block, cell, int(layer))
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

    keep = live.ids_by_position({a.sign: block[a.sign]})[a.sign]
    if a.prepare:
        others = {k: v for k, v in block.items() if k != a.sign}
        loaded = live.call("GET", "/signs", radius=500)["signs"]
        ids = []
        for key, pos in others.items():
            hit = [s for s in loaded if sum((u - v) ** 2 for u, v in zip(s["pos"], pos)) < 0.05 ** 2]
            ids += [s["id"] for s in hit if s["id"] != keep]
        kp = block[a.sign]                # copies an earlier run stacked on the kept sign (0.5 mm apart)
        ids += [s["id"] for s in loaded if s["id"] != keep and sum((u - v) ** 2 for u, v in zip(s["pos"], kp)) < 0.02 ** 2]
        for k in range(0, len(ids), 20):
            print("delete", live.call("POST", "/signs/delete", ids=",".join(ids[k:k + 20])))
    print("first band ->", a.sign, live.call("POST", "/sign", parts[0][2], timeout=300, id=keep, mode="zdo"))
    if len(parts) > 1:
        out = live.call("POST", "/signs/clone", live.GS.join(t for _, _, t in parts[1:]), timeout=300, id=keep, step=0.0005)
        print("clones ->", out)


if __name__ == "__main__":
    main()
