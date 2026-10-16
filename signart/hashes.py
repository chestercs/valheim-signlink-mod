#!/usr/bin/env python3
"""Fingerprint every generated sign text, to prove a refactor changed nothing.

  hashes.py save out/hashes.json     # before
  hashes.py check out/hashes.json    # after: lists every text that differs

Covers every piece of concepts/row1-5.py (as row.py builds it) and the frames of concepts/anim1-2.py.
"""
import hashlib
import importlib
import json
import sys

import row
from concepts import anim1, anim2
from rig import rig_grid


def fingerprints():
    g = rig_grid(json.load(open("rigs/bakterhaz-render-engine.json", encoding="utf-8")))
    h = lambda t: hashlib.sha1(t.encode("utf-8")).hexdigest()  # noqa: E731
    out = {}
    for mod in ("row1", "row2", "row3", "row4", "row5"):
        for fn in importlib.import_module(f"concepts.{mod}").PIECES:
            for (c, l), t in sorted(row.build_piece(fn, g)["texts"].items()):
                out[f"{mod}.{fn.__name__}.{c}-{l}"] = h(t)
    out.update({f"anim1.fjord.{i}": h(t) for i, t in enumerate(anim1.fjord())})
    for k, v in anim1.fjord_bands(g).items():
        out.update({f"anim1.bands.{k}.{i}": h(t) for i, t in enumerate(v)})
    out.update({f"anim2.cat.{i}": h(t) for i, t in enumerate(anim2.cat())})
    return out


if __name__ == "__main__":
    cmd, path = sys.argv[1], sys.argv[2]
    now = fingerprints()
    if cmd == "save":
        json.dump(now, open(path, "w"), indent=0, sort_keys=True)
        print(len(now), "texts fingerprinted ->", path)
    else:
        old = json.load(open(path))
        diff = sorted(k for k in old.keys() | now.keys() if old.get(k) != now.get(k))
        print(f"{len(now)} texts, {len(diff)} differ" + "".join(f"\n  {k}" for k in diff))
        sys.exit(1 if diff else 0)
