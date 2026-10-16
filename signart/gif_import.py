#!/usr/bin/env python3
"""Play an animated GIF on a gallery block through SignLink, at its real speed (docs/limits.md).

  gif_import.py GIF [--block 2] [--step auto] [--load 0.6] [--height-m 7.5] [--mode local] [--dry-run]

- Timing: the GIF's own frame durations (browsers' rule: <= 10 ms means 100 ms). Every `step`-th frame is
  used and lasts step x its duration, so a loop takes exactly as long as the original.
- Resolution: as many pixels as fit the main-thread budget `--load` (0.6 = 60 % of every second, "all out")
  at ~13.5 us per changed pixel plus ~1 ms per rewritten sign (measured). The picture is split into
  6 tiles on the six signs of the block that share one depth (L1 of A1 C1 C2 A3 B3 C3), and SignLink
  rewrites only the tiles that changed, so still areas cost nothing and buy resolution.
- Colour: 24-bit, gamma-correct Lanczos downscaling (image_import.py), transparent pixels left out.
- Placement: centred over the block, its bottom 1 m above the build; pitch in 0.5 wu steps (exact line
  heights). `--step 0` picks the largest step that keeps at least 12 fps.
"""
import argparse
import json
import time

from PIL import Image, ImageSequence

import image_import as I
import live
import pixels as P
from rig import rig_grid

TILE_SIGNS = ["A1-1", "C1-1", "C2-1", "A3-1", "B3-1", "C3-1"]      # same depth (-7.12 wu) in every block
US_PER_PX, MS_PER_SIGN = 13.5e-3, 1.0


def gif_frames(path):
    """-> list of (RGBA image, duration ms) with disposal applied by Pillow."""
    im = Image.open(path)
    out = []
    for f in ImageSequence.Iterator(im):
        d = f.info.get("duration", 100) or 100
        out.append((f.convert("RGBA").copy(), 100 if d <= 10 else d))
    return out


def tiles(W, H, cols, rows):
    xs = [round(W * k / cols) for k in range(cols + 1)]
    ys = [round(H * k / rows) for k in range(rows + 1)]
    return [(ys[r], ys[r + 1], xs[c], xs[c + 1]) for r in range(rows) for c in range(cols)]


def grids(frames, W, H):
    return [I.grid(*I.downscale(img, W, H)) for img, _ in frames]


def cost(px_frames, W, H, layout):
    """Average ms per shown frame: changed tiles' pixels x 13.5 us + 1 ms per changed tile."""
    total = 0.0
    for k in range(len(px_frames)):
        prev, cur = px_frames[k - 1], px_frames[k]
        for (r0, r1, c0, c1) in layout:
            if any(prev[r][c0:c1] != cur[r][c0:c1] for r in range(r0, r1)):
                total += (r1 - r0) * (c1 - c0) * US_PER_PX + MS_PER_SIGN
    return total / len(px_frames)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("gif")
    ap.add_argument("--block", type=int, default=2)
    ap.add_argument("--step", type=int, default=2, help="use every step-th frame (0 = auto, >= 12 fps)")
    ap.add_argument("--load", type=float, default=0.6, help="share of the main thread to spend (0.6 = all out)")
    ap.add_argument("--height-m", type=float, default=7.5)
    ap.add_argument("--mode", choices=("local", "zdo"), default="local")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()

    t0 = time.time()
    frames = gif_frames(a.gif)
    sw, sh = frames[0][0].size
    dur = sum(d for _, d in frames)
    step = a.step or max(1, int(1000 / (12 * (dur / len(frames)))))
    used = frames[::step]
    period = sum(d for _, d in frames) / len(used)            # ms per shown frame: real speed kept
    fps = 1000 / period
    layout_cols, layout_rows = (2, 3) if sh > sw else (3, 2)
    # grow the pixel count until the measured change pattern fills the budget
    budget_ms = a.load * period
    n = (budget_ms - 6 * MS_PER_SIGN) / US_PER_PX
    for _ in range(4):
        n = max(400, min(n, sw * sh, 6 * 49000))
        W, H = I.fit(sw, sh, n)
        lay = tiles(W, H, layout_cols, layout_rows)
        px = grids(used, W, H)
        c = cost(px, W, H, lay)
        if abs(c - budget_ms) / budget_ms < 0.08:
            break
        n *= (budget_ms - 6 * MS_PER_SIGN) / max(1e-3, c - 6 * MS_PER_SIGN) if c > 6 * MS_PER_SIGN else 2
    pitch = max(0.5, round(a.height_m * 40 / H * 2) / 2)
    print(f"{a.gif}: {sw} x {sh}, {len(frames)} frames, {dur} ms loop -> every {step}. frame: {len(used)} frames at "
          f"{fps:.1f} fps ({period:.0f} ms); {W} x {H} = {W * H} px in {layout_cols} x {layout_rows} tiles, pitch {pitch} wu "
          f"({W * pitch / 40:.1f} x {H * pitch / 40:.1f} m); est. {c:.1f} ms per frame = {c / period:.0%} of the main thread")

    g = rig_grid(json.load(open("rigs/bakterhaz-render-engine.json", encoding="utf-8")))
    texts = {k: [] for k in TILE_SIGNS}
    for grid_px in px:
        for key, (r0, r1, c0, c1) in zip(TILE_SIGNS, lay):
            cell, layer = key.split("-")
            sub = [[grid_px[r][c] if r0 <= r < r1 and c0 <= c < c1 else "" for c in range(W)] for r in range(H)]
            glyphs = P.items(sub, pitch, g.center_x, g.top_y + 40, cell, int(layer))
            texts[key].append(P.column_text(glyphs, g) if glyphs else "")
    chars = sum(len(t) for v in texts.values() for t in v)
    print(f"  {len(used)} x {len(TILE_SIGNS)} texts, {chars / 1e6:.1f} M characters, built in {time.time() - t0:.0f} s")
    if a.dry_run:
        return
    block = json.load(open("rigs/bakterhaz-row.json", encoding="utf-8"))["blocks"][a.block - 1]["signs"]
    ids = live.ids_by_position({k: block[k] for k in TILE_SIGNS})
    body = live.SEP.join(live.GS.join(texts[k][f] for k in TILE_SIGNS) for f in range(len(used)))
    out = live.call("POST", "/anim", body, timeout=300, ids=",".join(ids[k] for k in TILE_SIGNS),
                    fps=round(fps, 3), mode=a.mode)
    print(json.dumps(out))


if __name__ == "__main__":
    main()
