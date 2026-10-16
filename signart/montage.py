"""Contact sheet of renders: montage.py <out.png> key1 key2 ... (default: all in out/concepts.json)."""
import json, sys
import skia
keys = sys.argv[2:] or [e["key"] for e in json.load(open("out/concepts.json", encoding="utf-8"))]
suffix = ""
if keys and keys[0].startswith("--"):
    suffix, keys = "_" + keys[0][2:], keys[1:] or [e["key"] for e in json.load(open("out/concepts.json", encoding="utf-8"))]
T, cols = 460, 3
rows = (len(keys) + cols - 1) // cols
surf = skia.Surface(cols * T, rows * (T + 24))
cv = surf.getCanvas(); cv.clear(skia.Color(8, 10, 14))
font = skia.Font(skia.Typeface("DejaVu Sans"), 16)
for i, k in enumerate(keys):
    img = skia.Image.open(f"out/renders/{k}{suffix}.png")
    s = min(T / img.width(), T / img.height())
    x, y = (i % cols) * T, (i // cols) * (T + 24)
    cv.drawImageRect(img, skia.Rect.MakeXYWH(x + (T - img.width() * s) / 2, y + 24 + (T - img.height() * s) / 2, img.width() * s, img.height() * s))
    cv.drawString(k, x + 6, y + 18, font, skia.Paint(Color=skia.Color(220, 220, 220), AntiAlias=True))
surf.makeImageSnapshot().save(sys.argv[1], skia.kPNG)
print(sys.argv[1])
