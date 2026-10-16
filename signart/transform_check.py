"""Checks SignLink's TextTransform (resize and move placed glyphs) against the engine's layout model.

The plugin's test (mod/SignLink.Tests) writes transformed.json: sign texts (built-in art, pixel bands) with their
resized and moved versions. Every glyph must land k times as far from the sign's centre, then dx right and dy up,
k times as large (wu; engine.layout_sign is the model the art was made with).

    python transform_check.py /work/mod/SignLink.Tests/vectors/transformed.json
"""
import json
import sys

import engine as E

TOL = 0.03          # wu (0.75 mm): tags carry 4 decimals, the voffset shift 2


def placed(text):
    gx, gy = E.DEFAULT_GRID.pos("A1")
    return [(p.x - gx, p.y - gy, p.size, p.glyph) for p in E.layout_sign(text, "A1")]


def main():
    cases = json.load(open(sys.argv[1], encoding="utf-8"))
    bad, worst = 0, 0.0
    for c in cases:
        a, b = placed(c["text"]), placed(c["out"])
        k, dx, dy = c["k"], c["dx"], c["dy"]
        if len(a) != len(b):
            bad += 1
            print(f"FAIL {c['name']}: {len(a)} glyphs, after {len(b)}")
            continue
        err = 0.0
        for (x, y, s, g), (x2, y2, s2, g2) in zip(a, b):
            err = max(err, abs(x2 - (k * x + dx)), abs(y2 - (k * y + dy)), abs(s2 - k * s))
            if g != g2:
                err = float("inf")
        worst = max(worst, err)
        if err > TOL:
            bad += 1
            print(f"FAIL {c['name']}: off by {err:.4f} wu")
    print(f"transform: {len(cases)} texts laid out, worst {worst:.4f} wu" + (f", {bad} FAILED" if bad else ": ALL PASSED"))
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
