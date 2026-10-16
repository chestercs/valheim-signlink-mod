"""Where every built-in SignLink piece sits as made: NAME.fit beside NAME.txt / .stack.txt / .anim.txt.

SignLink turns a piece around its own centre (mod/SignLink/Pictures/Fit.cs) and needs that centre, and sizes it in
metres from its size as made (the Art tab's Width and Height); it cannot lay out
glyph art itself, so the engine does it here, from the very texts the DLL embeds: the bbox of every glyph of every sign
of the stack (an animation: its first frame), in wu from the sign's centre.

    python art_centres.py /work/mod/SignLink/Art        (presets.py runs it after writing the art)
"""
import os
import sys

import engine as E

FS, GS = "\u001e", "\u001d"


def texts_of(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    if path.endswith(".anim.txt"):
        return [text.split("\n", 1)[1].split(FS)[0]]
    if path.endswith(".stack.txt"):
        return text.split(GS)
    return [text]


def box(texts):
    """(centre x, centre y, width, height) of the piece as made, wu."""
    gx, gy = E.DEFAULT_GRID.pos("A1")
    items = [p for t in texts if t for p in E.layout_sign(t, "A1") if p.color[3] > 0]   # not the invisible anchors
    l, b, r, t = E.placed_bbox(items)
    return (l + r) / 2 - gx, (b + t) / 2 - gy, r - l, t - b


def write_all(root):
    for folder, _, files in os.walk(root):
        for f in sorted(files):
            for ext in (".anim.txt", ".stack.txt", ".txt"):
                if f.endswith(ext):
                    name = f[: -len(ext)]
                    x, y, w, h = box(texts_of(os.path.join(folder, f)))
                    with open(os.path.join(folder, name + ".fit"), "w", encoding="utf-8", newline="\n") as out:
                        out.write(f"centre={x:.3f},{y:.3f}\nsize={w:.3f},{h:.3f}\n")      # SignLink sizes it in metres
                    print(f"{name}: centre {x:.1f}, {y:.1f}, size {w:.1f} x {h:.1f} wu")
                    break


if __name__ == "__main__":
    write_all(sys.argv[1])
