#!/usr/bin/env python3
"""Sign row (gallery): the rig (45 signs + the six marble cubes they hang on) turned 90° and copied into a row
of blocks, a different piece on each, written into a copy of the world.

  row.py build <rig.json> <template world> <out parent> <new world name> [--force] [--dry-run]
  row.py preview <rig.json>          # renders only -> out/row/

Rig-local metres: u along the build (the rig's "right"), v along the sign normal (towards the viewer),
h up; origin = the rig's A1 cell. Each block turns +90° yaw (clockwise from above, BLOCK_YAW) about the
centre of its cubes, so its signs face -u and its own right is +v; block i is then moved ROW_V[i]
metres along +v. The block with offset 0 is the original (moved in place); the others are clones.
The pieces come from concepts/<module>.py (--pieces, default row2; PIECES, left to right as seen).
A piece returns {"art": Art} (laid out by the engine) or {"art": Art, "raw": {(cell, layer): text},
"placed": [preview glyphs], "notes": [...]} for texts written as they are (tag tests, other encodings).

Writes signart/rigs/<rig name>-row.json: every sign of every block by position, so a later run
can rewrite texts in a gallery world (the save has no stable ids).
"""
import argparse
import copy
import json
import math
import os
import shutil
import sys

sys.path.insert(0, os.environ.get("VSAVE_DIR", "/wt/tools"))
from vsave import Editor, Names, Selector, WorldDir  # noqa: E402

import engine as E  # noqa: E402
import importlib  # noqa: E402

from concepts.row1 import slots  # noqa: E402

PIECES = []                    # set by main(): concepts.<module>.PIECES (--pieces, default row2)
from rig import TEXT, WU_PER_M, loaded_world, locate, rename_world, rig_grid  # noqa: E402

CUBE = "blackmarble_2x2x2"
BLOCK_YAW = 90.0
ROW_V = [-10.0, 0.0, 10.0, 20.0, 30.0, 40.0]
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "row")
NAME_FILES = [f"/wt/tools/names/{f}" for f in ("prefab_catalog.json", "bundle_gameobjects_roots.txt",
                                               "extra_names.txt", "zdovars.tsv")]


# ----------------------------------------------------------------------------- geometry

class Frame:
    """Rig-local <-> world coordinates."""

    def __init__(self, rig):
        self.o, self.R, self.N = rig["origin"], rig["right"], rig["normal"]

    def local(self, p):
        dx, dz = p[0] - self.o[0], p[2] - self.o[2]
        return (dx * self.R[0] + dz * self.R[2], p[1] - self.o[1], dx * self.N[0] + dz * self.N[2])

    def world(self, u, h, v):
        return (self.o[0] + u * self.R[0] + v * self.N[0], self.o[1] + h, self.o[2] + u * self.R[2] + v * self.N[2])


def yaw_rotate(x, z, deg):
    """Unity yaw (left-handed, clockwise from above): forward (0,0,1) -> (sin, 0, cos)."""
    a = math.radians(deg)
    return x * math.cos(a) + z * math.sin(a), -x * math.sin(a) + z * math.cos(a)


def block_transform(pos, pivot, offset_world):
    rx, rz = yaw_rotate(pos[0] - pivot[0], pos[2] - pivot[2], BLOCK_YAW)
    return (pivot[0] + rx + offset_world[0], pos[1], pivot[2] + rz + offset_world[2])


def turned(rot):
    return (rot[0], (rot[1] + BLOCK_YAW) % 360.0, rot[2])


# ----------------------------------------------------------------------------- pieces

def build_piece(fn, grid):
    """-> dict(key, title, idea, bg, texts {(cell, layer): text}, placed [Placed], notes [str])."""
    spec = fn(grid)
    a = spec["art"]
    if "raw" in spec:          # test texts written as they are: the layout model does not know their tags
        texts = dict(spec["raw"])
        longest = max(E.utf16_len(t) for t in texts.values())
        notes = [f"raw: {len(texts)} signs, longest {longest} chars"] + list(spec.get("notes", []))
        return {"key": a.key, "title": a.title, "idea": a.idea, "bg": a.bg, "texts": texts,
                "placed": list(spec.get("placed", [])), "mode": a.mode, "notes": notes}
    if a.mode == "typable":    # exactly what a player would type: one glyph per sign, <= 50 characters
        rows, _ = a.build()
        bad, probs = E.check_rows(rows)
        over = [f"{r['cell']}-{r['layer']}: {r['chars']}" for r in rows if r["chars"] > E.CHAR_LIMIT]
        if over:
            sys.exit(f"{a.key}: typable piece over {E.CHAR_LIMIT} characters: {over}")
        notes = [f"typable: {len(rows)} signs, longest {max(r['chars'] for r in rows)} chars"
                 + (f", {len(bad)} cross-cell touches" if bad else "")]
    else:
        rows = E.build_planes(a)
        notes = [f"{sum(r['glyphs'] for r in rows)} glyphs in {len(rows)} signs, "
                 f"{max(r['layer'] for r in rows)} paint levels, worst {max(r['err'] for r in rows):.1f} wu"]
    texts = {(r["cell"], r["layer"]): r["text"] for r in rows}
    placed = [r["placed"] for r in rows] if a.mode == "typable" else [q for r in rows for q in r["placed"]]
    key, title, idea, bg = a.key, a.title, a.idea, a.bg
    if spec.get("loader"):
        free = [s for s in slots(grid) if s not in texts]
        texts[free[-1]] = spec["loader"]
        notes.append(f"loader sign at {free[-1][0]}-{free[-1][1]}")
    return {"key": key, "title": title, "idea": idea, "bg": bg, "texts": texts, "placed": placed, "mode": a.mode,
            "notes": notes}


def render_piece(p, grid, stem):
    os.makedirs(OUT, exist_ok=True)
    if not p["placed"]:        # raw tag tests without a preview
        return
    box = E.bounds(p["placed"], margin=40)
    E.render(p["placed"], os.path.join(OUT, f"{stem}.png"), bg=p["bg"], box=box, grid=grid, boards=True)
    E.render(p["placed"], os.path.join(OUT, f"{stem}_night.png"), box=box, grid=grid, light="night", boards=True)


def render_row(pieces, grids):
    """All blocks side by side as the viewer sees them (block i shifted by its row offset)."""
    allp = []
    for i, (p, g) in enumerate(zip(pieces, grids)):
        dx = (ROW_V[i] - ROW_V[0]) * WU_PER_M
        allp += [E.replace(q, x=q.x + dx, z=q.z + 1000 * i) for q in p["placed"]]
        allp += [E.Placed("■", g.pos(c)[0] + dx, g.pos(c)[1], 12, color=(138, 106, 67, 255), layer=-1)
                 for c in g.order if not g.cells[c][2]]
    box = E.bounds(allp, margin=60)
    for light, name in ((None, "row.png"), ("night", "row_night.png"), ("back", "row_back.png")):
        E.render(allp, os.path.join(OUT, name), px=1800, box=box, light=light, grid=grids[0])


# ----------------------------------------------------------------------------- world edit

def survey(ed, frame, names, moved_ids, blocks):
    """Objects that a new block may collide with, and the floor under its bottom cubes."""
    issues, support = [], []
    near = Selector()
    near.near = (frame.o[0], frame.o[2])
    near.radius = 80.0
    others = [(ci, z) for ci, z in ed.find(near) if id(z) not in moved_ids]
    for bi, pieces in enumerate(blocks):
        loc = [frame.local(z.pos) for z in pieces]
        us, hs, vs = [p[0] for p in loc], [p[1] for p in loc], [p[2] for p in loc]
        box = (min(us) - 1.2, max(us) + 1.2, min(vs) - 1.2, max(vs) + 1.2)
        bottom = min(hs) - 1.0
        for ci, z in others:
            u, h, v = frame.local(z.pos)
            name = names(z.prefab)
            if box[0] <= u <= box[1] and box[2] <= v <= box[3] and h > bottom - 0.3 and h < max(hs) + 2.5:
                if "floor" in name and h < bottom:
                    continue
                issues.append(f"block {bi + 1}: {name} at u={u:.2f} h={h:.2f} v={v:.2f}")
        cubes = [z for z in pieces if names(z.prefab) == CUBE]
        low = min(frame.local(z.pos)[1] for z in cubes)
        for c in cubes:
            cu, ch, cv = frame.local(c.pos)
            if ch > low + 0.1:
                continue
            under = []
            for ci, z in others:
                name = names(z.prefab)
                if "floor" not in name:
                    continue
                u, h, v = frame.local(z.pos)
                half = 2.0 if "4x4" in name else 1.0 if "2x2" in name else 0.5 if "1x1" in name else 1.0
                if ch - 1.0 - 0.6 <= h <= ch - 1.0 and abs(u - cu) <= half + 0.05 and abs(v - cv) <= half + 0.05:
                    under.append(name)
            support.append((bi + 1, round(cu, 2), round(cv, 2), under))
    return issues, support


def check_blocks(layout, snap_signs, snap_cubes, pivot, frame):
    """Every block is the original turned by BLOCK_YAW about the pivot and moved along +v (1 mm)."""
    for i, (own_signs, own_cubes) in enumerate(layout):
        off = ROW_V[i]
        pairs = [(own_signs[k], snap_signs[k]) for k in snap_signs] + list(zip(own_cubes, snap_cubes))
        for new, old in pairs:
            want = block_transform(old.pos, pivot, (off * frame.N[0], 0.0, off * frame.N[2]))
            if math.dist(new.pos, want) > 0.001 or abs(new.rot[1] - turned(old.rot)[1]) > 0.01:
                sys.exit(f"block {i + 1}: a piece is not where the transform puts it ({new.pos} vs {want})")
    print(f"geometry: {len(layout)} blocks = the original turned {BLOCK_YAW:+.0f}° and moved {ROW_V} m")


def build(rig, template, out_parent, new_name, force=False, dry_run=False):
    frame = Frame(rig)
    names = Names(NAME_FILES)
    wd = WorldDir(template)
    ed = Editor(wd, names)
    signs = locate(ed, rig)                            # (cell, layer) -> (ci, zdo)
    sel = Selector()
    sel.prefab = CUBE
    sel.near = (frame.o[0], frame.o[2])
    sel.radius = 12.0
    cubes = []
    for ci, z in ed.find(sel):
        u, h, v = frame.local(z.pos)
        if -1.5 < u < 6.5 and -2.6 < v < 0.2 and -4.0 < h < 0.6:
            cubes.append((ci, z))
    if len(cubes) != 6:
        sys.exit(f"expected the 6 marble cubes behind the rig, found {len(cubes)}")
    pivot = (sum(z.pos[0] for _, z in cubes) / 6, 0.0, sum(z.pos[2] for _, z in cubes) / 6)
    grid = rig_grid(rig)
    grids = [grid] * len(PIECES)
    pieces = [build_piece(fn, grid) for fn in PIECES]
    for i, (p, g) in enumerate(zip(pieces, grids)):
        render_piece(p, g, f"block{i + 1}_{p['key']}")
        longest = max(E.utf16_len(t) for t in p["texts"].values())
        print(f"block {i + 1}: {p['title']}: {len(p['texts'])} signs, longest text {longest} chars"
              + (f"; {'; '.join(p['notes'][:6])}" if p["notes"] else ""))
    render_row(pieces, grids)

    # every block: the original's sign/cube ZDOs (index of ROW_V == 0) or deep copies, moved and turned
    blocks, layout = [], []
    moved_ids = {id(z) for _, z in signs.values()} | {id(z) for _, z in cubes}
    orig = ROW_V.index(0.0)
    # clones come from untouched snapshots: the original itself is moved in place by its own block
    snap_signs = {key: copy.deepcopy(z) for key, (_, z) in signs.items()}
    snap_cubes = [copy.deepcopy(z) for _, z in cubes]
    for i, off in enumerate(ROW_V):
        offset_world = (off * frame.N[0], 0.0, off * frame.N[2])
        own_signs = {key: signs[key][1] if i == orig else copy.deepcopy(z) for key, z in snap_signs.items()}
        own_cubes = [cubes[k][1] if i == orig else copy.deepcopy(z) for k, z in enumerate(snap_cubes)]
        for zz in list(own_signs.values()) + own_cubes:
            zz.set_position(block_transform(zz.pos, pivot, offset_world))
            zz.set_rotation(turned(zz.rot))
        blocks.append(list(own_signs.values()) + own_cubes)
        layout.append((own_signs, own_cubes))

    check_blocks(layout, snap_signs, snap_cubes, pivot, frame)
    issues, support = survey(ed, frame, names, moved_ids, blocks)
    print("collision check:", "nothing in the way" if not issues else "")
    for s in issues:
        print("  " + s)
    unsupported = [s for s in support if not s[3]]
    print(f"support: {len(support) - len(unsupported)}/{len(support)} bottom cubes stand on a floor tile"
          + "".join(f"\n  block {b}: cube at u={u} v={v} has NO floor under it" for b, u, v, _ in unsupported))
    if dry_run:
        return pieces

    out_dir = os.path.join(out_parent, new_name)
    if os.path.exists(out_dir):
        if not force:
            sys.exit(f"{out_dir} exists (use --force to replace a world this tool made)")
        if loaded_world() == new_name:
            sys.exit(f"{new_name} is open in Valheim right now: quit to the menu first")
        shutil.rmtree(out_dir)

    for i, ((own_signs, own_cubes), p) in enumerate(zip(layout, pieces)):
        for (cell, layer), z in own_signs.items():
            z.strings[TEXT] = p["texts"].get((cell, layer), "")
        if i == orig:
            for ci, _ in list(signs.values()) + cubes:
                ed.mark(ci, f"block {i + 1}: original moved")
        else:
            for z in list(own_signs.values()) + own_cubes:
                ed.insert(z)
    ed.write(out_dir)
    rename_world(out_dir, new_name)
    gal = {"rig": rig["name"], "world": new_name, "from_save": wd.save_number, "block_yaw": BLOCK_YAW,
           "row_v": ROW_V, "note": "positions identify the sign ZDOs (saves have no stable ids)",
           "blocks": [{"block": i + 1, "piece": p["key"], "title": p["title"],
                       "signs": {f"{c}-{l}": [round(x, 4) for x in z.pos] for (c, l), z in own_signs.items()}}
                      for i, ((own_signs, _), p) in enumerate(zip(layout, pieces))]}
    path = os.path.join(HERE, "rigs", f"{rig['name']}-row.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(gal, f, ensure_ascii=False, indent=1)
    print(f"wrote {out_dir} as world {new_name!r}; sign map -> {os.path.relpath(path, HERE)}")
    return pieces


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("build")
    p.add_argument("rig")
    p.add_argument("world")
    p.add_argument("out_parent")
    p.add_argument("new_name")
    p.add_argument("--force", action="store_true")
    p.add_argument("--dry-run", action="store_true", help="renders + checks, no world written")
    p = sub.add_parser("preview")
    p.add_argument("rig")
    for p in sub.choices.values():
        p.add_argument("--pieces", default="row2", help="concepts module with PIECES (row1 = gallery 2)")
    args = ap.parse_args()
    PIECES[:] = importlib.import_module(f"concepts.{args.pieces}").PIECES
    rig = json.load(open(args.rig, encoding="utf-8"))
    if args.cmd == "build":
        build(rig, args.world, args.out_parent, args.new_name, args.force, args.dry_run)
    else:
        grids = [rig_grid(rig)] * len(PIECES)
        pieces = [build_piece(fn, g) for fn, g in zip(PIECES, grids)]
        for i, (p, g) in enumerate(zip(pieces, grids)):
            render_piece(p, g, f"block{i + 1}_{p['key']}")
            print(f"block {i + 1}: {p['title']}: {len(p['texts'])} signs" + (f"; {p['notes']}" if p["notes"] else ""))
        render_row(pieces, grids)


if __name__ == "__main__":
    main()
