#!/usr/bin/env python3
"""Build every concept: cells/layers, commands, re-check, PNG renders, out/concepts.json, simulator HTML.

Usage: build.py [module ...]   (default: every concepts/round*.py), run from signart/.
"""
import glob
import importlib
import json
import os
import sys
import traceback

import engine as E

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")


def build_one(fn):
    art = fn()
    entry = {"key": art.key, "title": art.title, "idea": art.idea, "bg": art.bg,
             "origin": [art.ox, art.oy], "grid": art.grid.to_dict()}
    if any(p.font == E.FONT_ALIAS for p in art.items):
        entry["loader"] = E.LOADER_TEXT
    entry["mode"] = art.mode            # typable (copy-paste in game) or detailed (save edit / mod)
    try:
        rows, _ = art.build()
    except E.CellError as e:
        entry["error"] = str(e)
        # still render the design itself so the problem can be seen
        E.render(art.items, os.path.join(OUT, "renders", f"{art.key}.png"), bg=art.bg, label=f"{art.title}: CELL ERROR")
        return entry
    bad, probs = E.check_rows(rows)
    placed = [r["placed"] for r in rows]
    cells_used = sorted({r["cell"] for r in rows}, key=art.grid.order.index)
    entry.update({
        "rows": [{k: r[k] for k in ("cell", "layer", "text", "chars", "warnings", "tag", "z")} for r in rows],
        "signs": len(rows), "cells": cells_used,
        "maxChars": max(r["chars"] for r in rows),
        "crossCellTouches": [[placed[i].cell, placed[j].cell, round(w[0]), round(w[1])] for i, j, w in bad],
        "problems": probs,
    })
    box = E.bounds(placed, margin=50)
    E.render(placed, os.path.join(OUT, "renders", f"{art.key}.png"), bg=art.bg, box=box,
             conflicts=[w for _, _, w in bad], grid=art.grid)
    E.render(placed, os.path.join(OUT, "renders", f"{art.key}_cells.png"), bg="#0b0f14", box=box, cell_tint=True, grid=art.grid)
    E.render(placed, os.path.join(OUT, "renders", f"{art.key}_back.png"), box=box, grid=art.grid, light="back")
    E.render(placed, os.path.join(OUT, "renders", f"{art.key}_night.png"), box=box, grid=art.grid, light="night")
    return entry


def main():
    os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
    # a partial build (named modules, e.g. the lab) renders only; the gallery + simulator need every round
    partial = bool(sys.argv[1:])
    mods = sys.argv[1:] or sorted(os.path.basename(p)[:-3] for p in glob.glob(os.path.join("concepts", "round*.py")))
    entries = []
    for m in mods:
        mod = importlib.import_module(f"concepts.{m}")
        for fn in mod.CONCEPTS:
            try:
                e = build_one(fn)
                e["round"] = m
            except Exception:
                traceback.print_exc()
                continue
            entries.append(e)
            status = e.get("error") or (f"{e['grid']['label']}: {e['signs']} signs, cells {len(e['cells'])}, max {e['maxChars']} chars"
                                        + (f", TOUCHES {e['crossCellTouches']}" if e["crossCellTouches"] else "")
                                        + (f", PROBLEMS {e['problems']}" if e["problems"] else ""))
            print(f"{m}.{e['key']}: {status}")
    if partial:
        return
    with open(os.path.join(OUT, "concepts.json"), "w", encoding="utf-8") as f:
        json.dump(entries, f, ensure_ascii=False, indent=1)
    if os.path.exists("sim_template.html"):
        import make_sim
        make_sim.main(entries)


if __name__ == "__main__":
    main()
