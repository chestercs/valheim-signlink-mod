#!/usr/bin/env python3
"""Renders the art library's pieces from the texts that ship (artlib.texts), for review.

  art_preview.py concepts.lib_pets [concepts.lib_x ...] [--out out/artlib] [--only NAME]

Per module, OUT/<module>/: NAME.png (a still, or an animation's first frame), NAME.gif (an animation, at its
fps) with NAME.strip.png (its frames side by side, to look at the motion), and _sheet.png, every piece's first frame with its name and size on one page. Prints each piece's size and
problems (artlib.check, the size caps) and exits 1 if any piece has one.
"""
import argparse
import importlib
import os
import sys

from PIL import Image, ImageDraw, ImageFont

import artlib as A
import engine as E

BG = "#0f1a26"


def placed_of(sign_texts):
    out = []
    for i, t in enumerate(sign_texts):
        out += [q for q in E.layout_sign(t, "A1", layer=i) if q.color[3] > 0]   # not the invisible anchors
    return out


def render(sign_texts, path, box, px):
    E.render(placed_of(sign_texts), path, px=px, bg=BG, box=box)


def name_file(name):
    return "".join(c if c.isalnum() or c in " ()-_,." else "_" for c in name)


def preview(piece, out, px=360):
    """-> (problems, kb, first png path)."""
    problems = A.check(piece)
    if problems:
        return problems, 0, None
    suffix, text, first, frames = A.texts(piece)
    kb = len(text.encode("utf-8")) / 1024
    cap = A.MAX_ANIM_KB if frames else A.MAX_STILL_KB
    if piece.kind == "pixel" and kb > cap:
        problems.append(f"{kb:.0f} KB: at most {cap} KB")
    if frames and piece.fps > 0:
        ms = A.play_ms(frames, piece.fps)
        if ms > A.MAX_PLAY_MS:
            problems.append(f"{ms:.0f} ms a second to play: at most {A.MAX_PLAY_MS:.0f} (lower the fps or the pixels)")
    base = os.path.join(out, name_file(piece.name))
    signs = [[f] for f in frames] if frames else [first]
    if piece.kind == "glyph" and not frames:
        signs = [first]
    allp = [q for s in signs for q in placed_of(s)]
    box = E.bounds(allp, margin=8)
    png = base + ".png"
    render(signs[0], png, box, px)
    if frames:
        tmp = []
        for k, s in enumerate(signs):
            p = f"{base}.f{k}.png"
            render(s, p, box, px)
            tmp.append(Image.open(p).convert("RGB"))
            os.remove(p)
        tmp[0].save(base + ".gif", save_all=True, append_images=tmp[1:], duration=int(1000 / piece.fps), loop=0)
        k = min(1.0, 200 / tmp[0].width)
        cells = [t.resize((max(1, int(t.width * k)), max(1, int(t.height * k)))) for t in tmp]
        strip = Image.new("RGB", (sum(c.width + 4 for c in cells), cells[0].height), (60, 60, 70))
        x = 0
        for c in cells:
            strip.paste(c, (x, 0))
            x += c.width + 4
        strip.save(base + ".strip.png")
    return problems, kb, png


def sheet(entries, path, tile=300):
    cols = 4
    rows = (len(entries) + cols - 1) // cols
    W, H = cols * tile, rows * (tile + 48)
    im = Image.new("RGB", (W, H), (15, 26, 38))
    d = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 15)
    except OSError:
        font = ImageFont.load_default()
    for i, (label, png) in enumerate(entries):
        x, y = (i % cols) * tile, (i // cols) * (tile + 48)
        if png and os.path.exists(png):
            t = Image.open(png).convert("RGB")
            t.thumbnail((tile - 10, tile - 10))
            im.paste(t, (x + (tile - t.width) // 2, y + (tile - t.height) // 2))
        lines, line = [], ""
        for word in label.split(" "):                   # wrap to the tile: long names would run into the next
            if line and d.textlength(line + " " + word, font=font) > tile - 12:
                lines.append(line)
                line = word
            else:
                line = (line + " " + word).strip()
        lines.append(line)
        d.multiline_text((x + 6, y + tile + 2), "\n".join(lines[:2]), fill=(230, 230, 220), font=font, spacing=2)
    im.save(path)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("modules", nargs="+")
    ap.add_argument("--out", default="out/artlib")
    ap.add_argument("--only")
    a = ap.parse_args()
    bad = 0
    for mod in a.modules:
        m = importlib.import_module(mod)
        out = os.path.join(a.out, mod.split(".")[-1])
        os.makedirs(out, exist_ok=True)
        entries = []
        for piece in m.pieces():
            if a.only and a.only != piece.name:
                continue
            try:
                problems, kb, png = preview(piece, out)
            except Exception as e:                      # noqa: BLE001 - report and go on
                problems, kb, png = [f"{type(e).__name__}: {e}"], 0, None
            kind = f"{piece.kind}, {len(piece.frames)} frames at {piece.fps:g} fps" if piece.animated else piece.kind
            print(f"{'FAIL' if problems else 'ok  '} {piece.name}: {kind}, {kb:.1f} KB" +
                  "".join(f"\n       - {p}" for p in problems))
            bad += bool(problems)
            entries.append((f"{piece.name} ({kb:.0f} KB)", png))
        if not a.only:
            sheet(entries, os.path.join(out, "_sheet.png"))
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
