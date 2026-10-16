"""Render the glyph vocabulary as contact sheets (out/sheet_<group>.png), each glyph fitted to a tile."""
import sys
import skia
from engine import db, svg_path

GROUPS = {
    "shapes": [chr(c) for c in range(0x25A0, 0x2600)] + [chr(c) for c in range(0x2580, 0x25A0)],
    "lines": [chr(c) for c in range(0x2500, 0x2580)] + list("←↑→↓↖↗↘↙⇦⇧⇨⇩⬅⬆⬇"),
    "runes": [chr(c) for c in range(0x16A0, 0x16F1)] + list(r"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789(){}[]<>/\|^~*+-=_"),
    "symbols": [chr(c) for c in list(range(0x2600, 0x2700)) + list(range(0x2700, 0x27C0)) + list(range(0x2200, 0x2300)) + list(range(0x2B00, 0x2C00)) + list(range(0x3000, 0x3040))],
    "emoji": [chr(c) for c in list(range(0x1F300, 0x1F700)) + list(range(0x1F900, 0x1FB00))],
}
def sheet(name, chars, cols=16, tile=90):
    chars = [c for c in chars if c in db().glyphs]
    rows = (len(chars) + cols - 1) // cols
    surf = skia.Surface(cols * tile, rows * tile)
    cv = surf.getCanvas(); cv.clear(skia.Color(16, 26, 38))
    fill = skia.Paint(Color=skia.Color(235, 225, 200), AntiAlias=True)
    box = skia.Paint(Color=skia.Color(60, 80, 100), Style=skia.Paint.kStroke_Style, StrokeWidth=1)
    font = skia.Font(None, 11); lab = skia.Paint(Color=skia.Color(140, 170, 200))
    for i, ch in enumerate(chars):
        cx, cy = (i % cols) * tile, (i // cols) * tile
        cv.drawRect(skia.Rect.MakeXYWH(cx, cy, tile, tile), box)
        g = db().glyphs[ch]; x0, y0, x1, y1 = g["bb"]
        s = (tile - 26) / max(x1 - x0, y1 - y0, 1e-3)
        m = skia.Matrix(); m.setScale(s, -s); m.postTranslate(cx + tile / 2 - s * (x0 + x1) / 2, cy + (tile - 12) / 2 + s * (y0 + y1) / 2)
        p = skia.Path(svg_path(ch)); p.transform(m); cv.drawPath(p, fill)
        cv.drawString(f"{ord(ch):X} {g['f'][:2]}", cx + 3, cy + tile - 3, font, lab)
    out = f"out/sheet_{name}.png"; surf.makeImageSnapshot().save(out, skia.kPNG); print(out, len(chars))
for k in (sys.argv[1:] or GROUPS):
    sheet(k, GROUPS[k])
