#!/usr/bin/env python3
"""Sign rigs: the physical "render engine" in a world save, and writing art into it.

A rig is the set of signs that project the art, labelled once in game ("A1-1" ... "C3-5": cell, then
layer, 1 = rearmost). `scan` finds them in a world save and stores their exact positions in a small
JSON file (signart/rigs/), so the labelling never has to be repeated. Cell and layer are derived from
geometry (columns along the build, rows by height, layers along the sign normal) and checked against
the labels. `apply` builds a concept on the rig's measured grid and writes every sign's text straight
into a copy of the world (the save tool bench's vsave Editor), under a new world name, so nothing
has to be typed in game and the 50-character input limit does not apply.

Needs the world tool repo (vsave.py) on the path: sign.sh mounts it at /wt.

  rig.py scan  <world dir> <rig.json> [--name NAME]
  rig.py apply <rig.json> <world dir> <out parent> <new world name> --concept KEY [--keep-unused] [--force]
  rig.py texts <rig.json> <world dir>             # show what the rig signs say now
"""
import argparse
import json
import math
import os
import re
import shutil
import sys

sys.path.insert(0, os.environ.get("VSAVE_DIR", "/wt/tools"))
from vsave import Editor, Names, Selector, WorldDir, stable_hash  # noqa: E402

import engine as E  # noqa: E402

SIGNS = {stable_hash("sign"), stable_hash("sign_notext")}
TEXT = stable_hash("text")
LABEL = re.compile(r"^\s*([A-Z]+)(\d+)\s*[-_/. ]\s*L?(\d+)\s*$", re.I)
WU_PER_M = 40.0            # 1 wu = 2.5 cm


def _sub(a, b):
    return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]


def scan(world_dir, name):
    wd = WorldDir(world_dir)
    found = []
    for ci, z in wd.iter_zdos():
        if z.prefab in SIGNS:
            m = LABEL.match(z.strings.get(TEXT, ""))
            if m:
                found.append({"label": f"{m.group(1).upper()}{m.group(2)}-{m.group(3)}", "row": m.group(1).upper(),
                              "col": int(m.group(2)), "layer": int(m.group(3)), "pos": list(z.pos), "rot": list(z.rot),
                              "chunk": ci.filename})
    if not found:
        sys.exit("no labelled signs (A1-1 style) found")
    yaws = {round(s["rot"][1], 1) for s in found}
    if len(yaws) != 1:
        print(f"[warn] signs have different yaws: {sorted(yaws)}")
    yaw = math.radians(found[0]["rot"][1])
    normal = [math.sin(yaw), 0.0, math.cos(yaw)]            # sign local +Z in world (Unity yaw)
    # the build's column direction: from the lowest to the highest labelled column, made horizontal
    cols = sorted({s["col"] for s in found})

    def mean(points):
        return [sum(p[i] for p in points) / len(points) for i in range(3)]
    c0 = mean([s["pos"] for s in found if s["col"] == cols[0]])
    c1 = mean([s["pos"] for s in found if s["col"] == cols[-1]])
    d = _sub(c1, c0)
    d[1] = 0.0
    n = math.hypot(d[0], d[2]) or 1.0
    right = [d[0] / n, 0.0, d[2] / n]
    if abs(right[0] * normal[0] + right[2] * normal[2]) > 0.05:
        print("[warn] columns are not perpendicular to the sign normal")
    # cells: mean position of their layers; origin = the top-left cell (first row, first column)
    by_cell = {}
    for s in found:
        by_cell.setdefault(f"{s['row']}{s['col']}", []).append(s)
    rows = sorted({s["row"] for s in found}, key=lambda r: (len(r), r))
    origin_cell = f"{rows[0]}{cols[0]}"
    o = mean([s["pos"] for s in by_cell[origin_cell]])
    cells = {}
    problems = []
    for cname, signs in by_cell.items():
        c = mean([s["pos"] for s in signs])
        v = _sub(c, o)
        x = (v[0] * right[0] + v[2] * right[2]) * WU_PER_M
        y = v[1] * WU_PER_M
        layers = sorted(signs, key=lambda s: s["pos"][0] * normal[0] + s["pos"][2] * normal[2])
        for k, s in enumerate(layers):
            s["depth_rank"] = k + 1
            if s["layer"] != k + 1:
                problems.append(f"{s['label']}: depth rank {k + 1} along the sign normal")
        if len({s["layer"] for s in signs}) != len(signs):
            problems.append(f"{cname}: duplicate layer labels")
        cells[cname] = {"x_wu": round(x, 2), "y_wu": round(y, 2),
                        "signs": {str(s["layer"]): {"pos": [round(p, 4) for p in s["pos"]],
                                                    "rot": [round(r, 3) for r in s["rot"]]} for s in signs}}
    # geometry check: columns from x, rows from y (clusters split at gaps > 20 wu = half a sign row)
    def clusters(values, descending=False):
        vs = sorted(values, reverse=descending)
        idx, k = {}, 0
        for i, v in enumerate(vs):
            if i and abs(v - vs[i - 1]) > 20:
                k += 1
            idx[v] = k
        return idx
    cx = clusters([c["x_wu"] for c in cells.values()])
    cy = clusters([c["y_wu"] for c in cells.values()], descending=True)
    for cname, c in cells.items():
        g = f"{E.row_name(cy[c['y_wu']])}{cx[c['x_wu']] + 1}"
        if g != cname:
            problems.append(f"{cname} sits where {g} would be")
    depth = max(len(c["signs"]) for c in cells.values())
    order = sorted(cells, key=lambda n: (int(re.sub(r"\D", "", n)), (len(re.sub(r"\d", "", n)), re.sub(r"\d", "", n))))
    meta = wd.meta
    rig = {
        # no seed/uid: with the seed, these coordinates would locate the base for anyone reading the repo
        "name": name, "world": meta.name, "save": wd.save_number,
        "signs": len(found), "depth": depth, "yaw_deg": found[0]["rot"][1],
        "origin_cell": origin_cell, "origin": [round(p, 4) for p in o],
        "right": [round(r, 5) for r in right], "normal": [round(r, 5) for r in normal],
        "order": order, "cells": {k: cells[k] for k in order}, "problems": problems,
        "note": "x_wu/y_wu: cell position relative to the origin cell (1 wu = 2.5 cm, x along the build, y up). "
                "Layer 1 = rearmost (first entered). Positions identify the sign ZDOs (saves have no stable ids).",
    }
    return rig


def rig_grid(rig):
    o, right, normal = rig["origin"], rig["right"], rig["normal"]
    signs = tuple((n, int(layer),
                   round(((s["pos"][0] - o[0]) * right[0] + (s["pos"][2] - o[2]) * right[2]) * WU_PER_M, 2),
                   round((s["pos"][1] - o[1]) * WU_PER_M, 2),
                   round(((s["pos"][0] - o[0]) * normal[0] + (s["pos"][2] - o[2]) * normal[2]) * WU_PER_M, 2))
                  for n in rig["order"] for layer, s in sorted(rig["cells"][n]["signs"].items()))
    return E.Grid(depth=rig["depth"], name=rig["name"], sign_positions=signs,
                  positions=tuple((n, rig["cells"][n]["x_wu"], rig["cells"][n]["y_wu"]) for n in rig["order"]))


def find_concept(key):
    import glob
    import importlib
    here = os.path.dirname(os.path.abspath(__file__))
    for path in sorted(glob.glob(os.path.join(here, "concepts", "*.py"))):
        mod = os.path.basename(path)[:-3]
        if mod in ("__init__", "common"):
            continue
        m = importlib.import_module(f"concepts.{mod}")
        for fn in getattr(m, "CONCEPTS", []):
            a = fn()
            if a.key == key:
                return a
    sys.exit(f"concept {key!r} not found")


def rig_signs(rig):
    for cname, c in rig["cells"].items():
        for layer, s in c["signs"].items():
            yield cname, int(layer), s


def locate(ed, rig):
    """Map (cell, layer) -> (chunk info, ZDO) by exact position (2 cm)."""
    out = {}
    o = rig["origin"]
    near = Selector()
    near.prefab = "sign"
    near.near = (o[0], o[2])
    near.radius = 30.0
    cands = ed.find(near)
    for cname, layer, s in rig_signs(rig):
        best = None
        for ci, z in cands:
            d = math.dist(z.pos, s["pos"])
            if d < 0.02 and (best is None or d < best[0]):
                best = (d, ci, z)
        if best is None:
            sys.exit(f"sign {cname}-{layer} not found at {s['pos']} (was the rig moved or rebuilt?)")
        out[(cname, layer)] = best[1:]
    return out


def rename_world(out_dir, new_name):
    wd = WorldDir(out_dir)
    meta = wd.meta
    meta.name = new_name
    with open(os.path.join(out_dir, f"_main.{wd.save_number}.fwl2"), "wb") as f:
        f.write(meta.serialize())


def loaded_world():
    """Name of the world Valheim currently has open, from Player.log (None if unknown)."""
    log = os.environ.get("PLAYER_LOG", "/vlog/Player.log")
    if not os.path.exists(log):
        return None
    last = None
    with open(log, encoding="utf-8", errors="replace") as f:
        for line in f:
            if "ZNet.LoadWorld:" in line:
                last = line.split("ZNet.LoadWorld:")[1].split("(")[0].strip()
            elif "ZNet Shutdown" in line:
                last = None
    return last


def apply(rig, world_dir, out_parent, new_name, key, keep_unused=False, force=False):
    art = find_concept(key)
    grid = rig_grid(rig)
    a = art.retarget(grid)
    rows, _ = a.build()
    bad, probs = E.check_rows(rows)
    long_rows = [r for r in rows if r["chars"] > E.CHAR_LIMIT]
    if bad:
        print(f"[warn] {len(bad)} cross-cell touches on the rig grid")
    if probs:
        print("[info] " + "; ".join(probs[:5]))
    if long_rows:
        print(f"[info] {len(long_rows)} texts are longer than the 50-character input (fine in a save edit)")
    out_dir = os.path.join(out_parent, new_name)
    if os.path.exists(out_dir):
        if not force:
            sys.exit(f"{out_dir} exists (use --force to replace a world this tool made)")
        if loaded_world() == new_name:
            sys.exit(f"{new_name} is open in Valheim right now: quit to the menu first")
        shutil.rmtree(out_dir)
    wd = WorldDir(world_dir)
    ed = Editor(wd, Names([]))
    where = locate(ed, rig)
    want = {(r["cell"], r["layer"]): r["text"] for r in rows}
    used = 0
    for (cname, layer), (ci, z) in where.items():
        text = want.get((cname, layer))
        if text is None and keep_unused:
            continue
        z.strings[TEXT] = text if text is not None else ""
        ed.mark(ci, f"{cname}-{layer}")
        used += text is not None
    missing = [k for k in want if k not in where]
    if missing:
        sys.exit(f"concept needs signs the rig does not have: {missing}")
    ed.write(out_dir)
    rename_world(out_dir, new_name)
    print(f"{key}: wrote {used} sign texts ({len(where) - used} blanked) -> {out_dir} as world {new_name!r}")
    return rows


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("scan")
    p.add_argument("world")
    p.add_argument("rig")
    p.add_argument("--name", default="render-engine")
    p = sub.add_parser("apply")
    p.add_argument("rig")
    p.add_argument("world")
    p.add_argument("out_parent")
    p.add_argument("new_name")
    p.add_argument("--concept", required=True)
    p.add_argument("--keep-unused", action="store_true", help="leave signs the concept does not use as they are")
    p.add_argument("--force", action="store_true")
    p = sub.add_parser("texts")
    p.add_argument("rig")
    p.add_argument("world")
    args = ap.parse_args()
    if args.cmd == "scan":
        rig = scan(args.world, args.name)
        os.makedirs(os.path.dirname(os.path.abspath(args.rig)), exist_ok=True)
        with open(args.rig, "w", encoding="utf-8") as f:
            json.dump(rig, f, ensure_ascii=False, indent=1)
        print(f"{rig['signs']} signs, {len(rig['cells'])} cells x depth {rig['depth']} -> {args.rig}")
        for c in rig["order"]:
            print(f"  {c}: x={rig['cells'][c]['x_wu']:8.2f} wu  y={rig['cells'][c]['y_wu']:8.2f} wu")
        print("problems:", rig["problems"] or "none")
    elif args.cmd == "apply":
        rig = json.load(open(args.rig, encoding="utf-8"))
        apply(rig, args.world, args.out_parent, args.new_name, args.concept, args.keep_unused, args.force)
    elif args.cmd == "texts":
        rig = json.load(open(args.rig, encoding="utf-8"))
        ed = Editor(WorldDir(args.world), Names([]))
        for (cname, layer), (ci, z) in sorted(locate(ed, rig).items(), key=lambda kv: (rig["order"].index(kv[0][0]), kv[0][1])):
            print(f"{cname}-{layer}: {z.strings.get(TEXT, '')!r}")


if __name__ == "__main__":
    main()
