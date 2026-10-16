#!/usr/bin/env python3
"""Import a picture onto ONE sign as faithfully as a sign allows (docs/limits.md).

  image_import.py IMAGE [--pixels 49500] [--pitch P] [--above 40] [--dither] [--name NAME]

- Size: the largest W x H with the image's aspect and W*H <= --pixels (a sign shows at most 50 000 pixels:
  a glyph and a line feed each, 99 999 visible characters).
- Downscaling: Lanczos in linear light (gamma-correct), alpha premultiplied; pixels below half alpha
  are left out (they show what is behind the sign). SignLink keeps alpha instead (grid translucent=True).
- Colour: 24-bit, `<#rrggbb>` (written short as `<#rgb>` when that is exact); --dither instead quantizes
  to 12-bit with the Bayer pattern (fewer characters).
- Pitch: the largest that keeps the column encoding (pixels x pitch <= ~125 000 wu), unless given; a
  larger pitch makes the text fall back to one pos + voffset per pixel (about twice the characters).
- Network size: a sign's ZDO must stay under 512 KiB in multiplayer (Steam's largest reliable message;
  docs/limits.md). --max-kb (default 400) lowers the pixel count until the text fits; 0 = no cap (single
  player only).
- Placement: relative to the sign itself, centred over it, the bottom `above` wu (40 = 1 m) higher,
  so `live.py write ID --file out/import/NAME.txt --mode zdo` puts it on any sign.

Writes out/import/NAME.txt (the sign text) and out/import/NAME.png (what the sign will show, one
pixel per sign pixel, scaled up).
"""
import argparse
import math
import os

import numpy as np
from PIL import Image, ImageOps

import engine as E
import pixels as P


def _to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def fit(w, h, max_pixels):
    """Largest W x H with aspect w:h and W*H <= max_pixels."""
    W = int(math.sqrt(max_pixels * w / h))
    H = int(max_pixels // W)
    H = min(H, max(1, round(W * h / w)))
    return W, H


def downscale(img, W, H):
    """Gamma-correct Lanczos resize with premultiplied alpha -> (rgb 0..1 sRGB, alpha 0..1), H x W."""
    a = np.asarray(img.convert("RGBA")).astype(np.float32) / 255
    alpha = a[..., 3:4]
    lin = _to_linear(a[..., :3]) * alpha

    def rs(ch):
        return np.asarray(Image.fromarray(ch.astype(np.float32), "F").resize((W, H), Image.LANCZOS))
    out = np.stack([rs(lin[..., i]) for i in range(3)], -1)
    al = np.clip(rs(alpha[..., 0]), 0, 1)
    rgb = _to_srgb(np.where(al[..., None] > 1e-6, out / np.maximum(al[..., None], 1e-6), 0))
    return rgb, al


MIN_ALPHA, SOLID_ALPHA = 8, 248     # SignLink's Resampler: Lanczos rings a little at hard edges


def grid(rgb, al, dither=False, translucent=False):
    """-> rows of '#rrggbb' (or 12-bit dithered '#rgb'), '' where alpha < 0.5. translucent (SignLink,
    Resampler.Grid): alpha of 255 under MIN_ALPHA is '', from SOLID_ALPHA on opaque, between kept: '#rrggbbaa'."""
    if translucent:
        assert not dither, "translucent grids are 24-bit"
        q = np.clip(np.round(rgb * 255), 0, 255).astype(int)
        a8 = np.round(al * 255).astype(int)
        return [["" if a8[r, c] < MIN_ALPHA else "#%02x%02x%02x" % tuple(q[r, c]) + ("" if a8[r, c] >= SOLID_ALPHA else "%02x" % a8[r, c])
                 for c in range(rgb.shape[1])] for r in range(rgb.shape[0])]
    if dither:
        px = P.quantize(rgb)
    else:
        q = np.clip(np.round(rgb * 255), 0, 255).astype(int)
        px = [["#%02x%02x%02x" % tuple(q[r, c]) for c in range(rgb.shape[1])] for r in range(rgb.shape[0])]
    return [[px[r][c] if al[r, c] >= 0.5 else "" for c in range(rgb.shape[1])] for r in range(rgb.shape[0])]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("image")
    ap.add_argument("--pixels", type=int, default=49500)
    ap.add_argument("--pitch", type=float, help="wu per pixel (default: the largest that keeps the column encoding)")
    ap.add_argument("--above", type=float, default=40.0)
    ap.add_argument("--dither", action="store_true", help="12-bit dithered instead of 24-bit")
    ap.add_argument("--max-kb", type=float, default=400, help="UTF-8 size cap per sign (0 = none, single player only)")
    ap.add_argument("--name")
    a = ap.parse_args()
    name = a.name or os.path.splitext(os.path.basename(a.image))[0]
    img = Image.open(a.image)
    if img.format == "JPEG":
        img = ImageOps.exif_transpose(img)                  # as SignLink turns phone photos (EXIF in JPEGs)
    budget = min(a.pixels, 50000)
    while True:
        W, H = fit(img.width, img.height, budget)
        # pixel pitch in steps of 1/8 wu: the line height (pitch / 2) is then a multiple of 1/16, which TMP
        # reads and adds up exactly when engine.exact_line_height accepts it; other pitches cost voffsets
        pitch = round((a.pitch or math.floor(125000 / (W * H) / 0.5) * 0.5) * 8) / 8
        rgb, al = downscale(img, W, H)
        px = grid(rgb, al, a.dither)
        n = sum(1 for row in px for c in row if c)
        gx, gy = E.DEFAULT_GRID.pos("A1")
        glyphs = P.items(px, pitch, gx, gy + a.above, "A1")
        cmds, lh = E.column_cmds(glyphs, E.DEFAULT_GRID)
        text = E.encode_lines(cmds, lh) if cmds is not None else E.encode_lines(E.line_cmds(E.order_lines(glyphs)))
        kb = len(text.encode("utf-8")) / 1024
        if not a.max_kb or kb <= a.max_kb:
            break
        budget = int(budget * (a.max_kb / kb) * 0.97)
        print(f"  {W} x {H}: {kb:.0f} KB is over {a.max_kb:g} KB, trying {budget} pixels")
    os.makedirs("out/import", exist_ok=True)
    open(f"out/import/{name}.txt", "w", encoding="utf-8").write(text)
    prev = Image.new("RGB", (W, H), (30, 36, 48))
    for r in range(H):
        for c in range(W):
            if px[r][c]:
                h = px[r][c].lstrip("#")
                h = "".join(ch * 2 for ch in h) if len(h) == 3 else h
                prev.putpixel((c, r), tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)))
    prev.resize((W * 4, H * 4), Image.NEAREST).save(f"out/import/{name}.png")
    visible = 2 * n - 1
    print(f"{name}: {W} x {H} = {n} pixels ({visible} visible characters of 99 999), pitch {pitch:g} wu "
          f"= {W * pitch / 40:.2f} x {H * pitch / 40:.2f} m, {'12-bit dithered' if a.dither else '24-bit'}, "
          f"{'column' if cmds is not None else 'fallback'} encoding: {len(text)} characters, "
          f"{len(text.encode('utf-8')) / 1024:.0f} KB -> out/import/{name}.txt")


if __name__ == "__main__":
    main()
