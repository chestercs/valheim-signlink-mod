#!/usr/bin/env python3
"""Test vectors for SignLink's C# picture encoder (mod/SignLink/Pictures): what the Python pipeline makes of
the same input, so `./sign.sh modtest` can demand the same sign texts byte for byte.

  encoder_vectors.py OUT_DIR [IMAGE ...]

Writes OUT_DIR/vectors.json: number formatting cases, line heights, fits, pixel grids with their band texts,
and resampling cases (source RGBA, Python's grid). Every IMAGE adds a full case: fit to 198 000 pixels, the
grid, the bands for a sign-relative picture (centre over the sign, bottom 1 m above it, cover 2, 400 KB).
"""
import base64
import hashlib
import json
import os
import random
import sys

import numpy as np
from PIL import Image, ImageOps, ImageSequence

import engine as E
import image_import as I
import pixels as P
from vanilla_hq import bands


def num_cases():
    rnd = random.Random(7)
    vals = [0.0, -0.0, 0.05, -0.05, 0.15, 0.25, 0.35, 2.675, -2.675, 1.005, 0.125, -0.125, 39.65, 32767.45,
            -1.25, 1e-7, 0.375, 0.1875, 0.0625, 123.456789, -0.04]
    vals += [rnd.uniform(-500, 500) for _ in range(300)] + [round(rnd.uniform(-50, 50), 2) + 0.05 for _ in range(100)]
    return [{"v": v, "nd": nd, "round": round(v, nd), "fixed": f"{v:.{nd}f}", "num": E._num(v, nd)}
            for v in vals for nd in (1, 2, 6)]


def packed(c):
    """'#rrggbb' or '#rrggbbaa' as SignLink's grid pixel (PixelText.Pack): 0xTTRRGGBB, TT = 255 - alpha, so an
    opaque colour is plain 0xRRGGBB; '' is -1."""
    if not c:
        return -1
    r, g, b, a = E.parse_hex(c)
    v = ((255 - a) << 24) | (r << 16) | (g << 8) | b
    return v - (1 << 32) if v >= 1 << 31 else v


def text_case(name, px, pitch, cx, bottom, cover, max_kb, max_px=49500, glow=True):
    H, W = len(px), len(px[0])
    size = P.glyph_size(pitch, cover=cover or None)
    flat = [packed(c) for row in px for c in row]
    try:
        out = [{"r0": r0, "r1": r1, "text": t}
               for r0, r1, t in bands(px, pitch, cx, bottom, "A1", None, E.DEFAULT_GRID, max_kb, max_px, size=size,
                                                font=E.FONT_PATH if glow else None)]
        err = None
    except ValueError as e:
        out, err = [], str(e)
    return {"name": name, "w": W, "h": H, "px": flat, "pitch": pitch, "cx": cx, "bottom": bottom, "cover": cover,
            "size": size, "max_kb": max_kb, "max_px": max_px, "glow": glow, "bands": out, "error": err}


def random_grid(rnd, W, H, empty=0.0, palette=None, translucent=0.0, alphas=None):
    def colour():
        if rnd.random() < empty:
            return ""
        c = rnd.choice(palette) if palette else rnd.randrange(1 << 24)
        if rnd.random() < translucent:
            return "#%06x%02x" % (c, rnd.choice(alphas) if alphas else rnd.randrange(1, 255))
        return "#%06x" % c
    return [[colour() for _ in range(W)] for _ in range(H)]


def resample_case(name, img, W, H, opaque):
    rgba = np.asarray(img.convert("RGBA"), dtype=np.uint8)
    src = img.convert("RGB") if opaque else img
    px = I.grid(*I.downscale(src, W, H), translucent=not opaque)
    return {"name": name, "w": img.width, "h": img.height, "W": W, "H": H, "opaque": opaque,
            "rgba": base64.b64encode(rgba.tobytes()).decode(), "px": [packed(c) for row in px for c in row]}


def main():
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    rnd = random.Random(1)
    v = {"num": num_cases()}
    v["line_height"] = [{"step": s, "lh": E.exact_line_height(s)}
                        for s in (0.25, 0.375, 0.4, 0.8, 0.1875, 1.3, 0.0625, 0.03, 0.5, 1.0, 0.75, 0.3125, 2.2)]
    v["fit"] = [{"w": w, "h": h, "max": m, "fit": list(I.fit(w, h, m))}
                for w, h, m in ((360, 640, 230400), (1280, 1014, 198000), (1920, 1080, 49500), (100, 3, 5000),
                                (7, 900, 20000), (4000, 3000, 250000))]

    tiny = [["#ff0000", "#00ff00", "#0000ff"], ["#123456", "", "#1e1a12"]]
    v["text"] = [
        text_case("tiny", tiny, 1.0, 0.0, 40.0, 2.0, 400),
        text_case("random 40x30 pitch 0.5", random_grid(rnd, 40, 30), 0.5, 0.0, 40.0, 2.0, 400),
        text_case("random 120x90 cover 0, 20 KB bands", random_grid(rnd, 120, 90, 0.05), 1.0, 12.5, 40.0, 0, 20),
        text_case("palette 64x64 pitch 0.75", random_grid(rnd, 64, 64, 0.0, [0xffffff, 0x000000, 0x1e1a12]),
                  0.75, -30.0, 10.0, 2.0, 400),
        text_case("below the sign", random_grid(rnd, 10, 8), 1.0, 0.0, -200.0, 2.0, 400),
        text_case("across the sign (centred)", random_grid(rnd, 120, 90), 0.5, 0.0, -22.5, 2.0, 400),
        text_case("far below, 3 mm pixels", random_grid(rnd, 200, 150), 0.125, 0.0, -400.0, 2.0, 400),
        text_case("max px 1000", random_grid(rnd, 50, 50), 1.5, 0.0, 40.0, 2.0, 100000, 1000),
        text_case("fine pixels 0.25 wu, offset", random_grid(rnd, 90, 70), 0.25, 37.5, 12.0, 2.0, 400),
        text_case("0.5 wu, left of the sign", random_grid(rnd, 81, 33), 0.5, -60.0, 0.0, 2.0, 400),
        text_case("lit (no font tag), 20 KB bands", random_grid(rnd, 120, 90, 0.05), 1.0, 12.5, 40.0, 2.0, 20, glow=False),
        text_case("lit, below the sign", random_grid(rnd, 30, 20), 0.5, 0.0, -100.0, 2.0, 400, glow=False),
        text_case("alpha: holes", random_grid(rnd, 60, 40, 0.3), 0.5, 0.0, 40.0, 2.0, 400),
        text_case("alpha: empty rows between bands", [[("" if 4 <= r < 9 else "#%06x" % (r * 2000 + c)) for c in range(20)]
                                                       for r in range(12)], 1.0, 0.0, 40.0, 2.0, 400, 60),
        text_case("alpha: one pixel", [["#ff8800" if (r, c) == (2, 3) else "" for c in range(5)] for r in range(5)],
                  1.0, 0.0, 40.0, 2.0, 400),
        text_case("alpha: a diagonal, no pixel above another", [["#00ff00" if r == c else "" for c in range(8)] for r in range(8)],
                  0.5, 0.0, 40.0, 2.0, 400),
        text_case("alpha: fully transparent", [["" for _ in range(6)] for _ in range(4)], 1.0, 0.0, 40.0, 2.0, 400),
        text_case("alpha: half transparent", random_grid(rnd, 50, 40, 0.1, None, 0.6), 0.5, 0.0, 40.0, 2.0, 400),
        text_case("alpha: #rgba tags, cover 0", random_grid(rnd, 30, 30, 0.1, [0xffffff, 0x112233, 0x88aacc], 0.5,
                                                            [0x11, 0x88, 0xee]), 1.0, 0.0, 40.0, 0, 400),
        text_case("alpha: every other row (line height = pitch)",
                  [[("" if r % 2 else "#%06x" % (r * 9000 + c * 77)) for c in range(24)] for r in range(16)], 0.5, 0.0, 40.0, 2.0, 400),
        text_case("alpha: half transparent, lit, 20 KB bands", random_grid(rnd, 80, 60, 0.05, None, 0.3), 1.0, 0.0, -10.0,
                  2.0, 20, glow=False),
    ]
    big = Image.fromarray(np.uint8(np.random.default_rng(3).integers(0, 256, (48, 64, 4))), "RGBA")
    soft = np.zeros((40, 60, 4), np.uint8)                    # a disc with a soft edge on a transparent ground
    yy, xx = np.mgrid[0:40, 0:60]
    soft[..., 0], soft[..., 1], soft[..., 2] = 200, 80, 30
    soft[..., 3] = np.uint8(np.clip((18 - np.hypot(xx - 30, yy - 20)) * 40, 0, 255))
    v["resample"] = [resample_case("noise 64x48 -> 20x15 alpha", big, 20, 15, False),
                     resample_case("soft disc 60x40 -> 30x20 alpha", Image.fromarray(soft, "RGBA"), 30, 20, False),
                     resample_case("noise 64x48 -> 64x15 opaque", big, 64, 15, True),
                     resample_case("noise 64x48 -> 21x48 opaque", big, 21, 48, True)]
    # EXIF orientations (ImageHeader.Orient): every one against Pillow's exif_transpose methods
    src = Image.fromarray(np.uint8(np.random.default_rng(11).integers(0, 256, (2, 3, 4))), "RGBA")
    methods = {2: Image.Transpose.FLIP_LEFT_RIGHT, 3: Image.Transpose.ROTATE_180, 4: Image.Transpose.FLIP_TOP_BOTTOM,
               5: Image.Transpose.TRANSPOSE, 6: Image.Transpose.ROTATE_270, 7: Image.Transpose.TRANSVERSE,
               8: Image.Transpose.ROTATE_90}
    v["orient"] = [{"o": o, "w": src.width, "h": src.height, "rgba": base64.b64encode(src.tobytes()).decode(),
                    "W": t.width, "H": t.height, "want": base64.b64encode(t.tobytes()).decode()}
                   for o, t in ((o, src.transpose(m)) for o, m in methods.items())]
    for path in sys.argv[2:]:
        img = Image.open(path)
        img.seek(0)
        img = (ImageOps.exif_transpose(img) if img.format == "JPEG" else img).convert("RGB")
        W, H = I.fit(img.width, img.height, min(198000, img.width * img.height))
        rc = resample_case(os.path.basename(path), img, W, H, True)
        v["resample"].append(rc)
        grid = [["#%06x" % rc["px"][r * W + c] for c in range(W)] for r in range(H)]
        v["text"].append(text_case(os.path.basename(path) + " bands", grid, 1.0, 0.0, 40.0, 2.0, 400))
    # header sizes (ImageHeader.cs): a PNG, a baseline and a progressive JPEG with a big APP1 block, every IMAGE
    sizes = []
    grad = Image.fromarray(np.uint8(np.random.default_rng(5).integers(0, 256, (37, 53, 3))), "RGB")
    grad.save(os.path.join(out_dir, "h_test.png"))
    grad.save(os.path.join(out_dir, "h_base.jpg"), quality=90)
    grad.save(os.path.join(out_dir, "h_prog.jpg"), quality=90, progressive=True, exif=b"Exif" + bytes(60002))
    sizes += [{"file": n, "w": 53, "h": 37} for n in ("h_test.png", "h_base.jpg", "h_prog.jpg")]
    for o, le in ((6, True), (8, False)):                       # a phone's portrait photo: stored sideways, turned by EXIF
        exif = Image.Exif()
        exif[0x0112] = o
        data = exif.tobytes()
        if not le:                                              # the same tag in big-endian TIFF
            data = b"Exif\x00\x00MM\x00\x2a\x00\x00\x00\x08\x00\x01\x01\x12\x00\x03\x00\x00\x00\x01\x00" + bytes([o]) + b"\x00\x00\x00\x00\x00\x00"
        grad.save(os.path.join(out_dir, f"h_rot{o}.jpg"), quality=90, exif=data)
        sizes.append({"file": f"h_rot{o}.jpg", "w": 37, "h": 53})
    for path in sys.argv[2:]:
        img = Image.open(path)
        if img.format in ("PNG", "JPEG", "GIF"):
            name = "h_" + os.path.basename(path)
            with open(path, "rb") as src, open(os.path.join(out_dir, name), "wb") as dst:
                dst.write(src.read())
            shown = ImageOps.exif_transpose(img) if img.format == "JPEG" else img
            sizes.append({"file": name, "w": shown.width, "h": shown.height})
    with open(os.path.join(out_dir, "sizes.json"), "w", encoding="utf-8") as f:
        json.dump(sizes, f)

    # GIFs (GifDecoder.cs): Pillow's composited RGB frames as SHA-1, and their durations
    rng = np.random.default_rng(9)
    frames = [Image.fromarray(np.uint8(rng.integers(0, 256, (45, 61, 3))), "RGB").quantize(64) for _ in range(4)]
    for k in range(1, 4):                                   # later frames change only a corner: Pillow crops them
        frames[k] = frames[0].copy()
        frames[k].paste(frames[(k + 1) % 4].crop((0, 0, 20 + 8 * k, 15 + 5 * k)), (3 * k, 2 * k))
    frames[0].save(os.path.join(out_dir, "g_partial.gif"), save_all=True, append_images=frames[1:], duration=[40, 70, 100, 30],
                   loop=0, disposal=1)
    frames[0].save(os.path.join(out_dir, "g_restore.gif"), save_all=True, append_images=frames[1:], duration=50, loop=0, disposal=2)
    big = [Image.fromarray(np.uint8(rng.integers(0, 256, (130, 170, 3))), "RGB") for _ in range(3)]
    big[0].save(os.path.join(out_dir, "g_interlaced.gif"), save_all=True, append_images=big[1:], duration=80, interlace=True)
    # frames with their own palettes (disposal 3 and transparency are checked on synthetic files in Program.cs: Pillow's
    # reader restores to the previous frame and colours transparent pixels, which is not what the GIF spec says)
    local = [Image.fromarray(np.uint8(rng.integers(0, 256, (40, 50, 3))), "RGB").quantize(32) for _ in range(3)]
    local[0].save(os.path.join(out_dir, "g_localpal.gif"), save_all=True, append_images=local[1:], duration=70, loop=0, disposal=1,
                  optimize=False)
    gifs = ["g_partial.gif", "g_restore.gif", "g_interlaced.gif", "g_localpal.gif"]
    for path in sys.argv[2:]:
        if Image.open(path).format == "GIF":
            name = "g_" + os.path.basename(path)
            with open(path, "rb") as src, open(os.path.join(out_dir, name), "wb") as dst:
                dst.write(src.read())
            gifs.append(name)
    gif_cases = []
    for name in gifs:
        im = Image.open(os.path.join(out_dir, name))
        shas, durs = [], []
        for f in ImageSequence.Iterator(im):
            shas.append(hashlib.sha1(f.convert("RGB").tobytes()).hexdigest())
            durs.append(f.info.get("duration", 0))
        gif_cases.append({"file": name, "w": im.width, "h": im.height, "sha1": shas, "durations": durs})
    with open(os.path.join(out_dir, "gifs.json"), "w", encoding="utf-8") as f:
        json.dump(gif_cases, f)
    with open(os.path.join(out_dir, "vectors.json"), "w", encoding="utf-8") as f:
        json.dump(v, f, ensure_ascii=False)
    print(f"{out_dir}/vectors.json: {len(v['num'])} numbers, {len(v['text'])} text cases, {len(v['resample'])} resamples")


if __name__ == "__main__":
    main()
