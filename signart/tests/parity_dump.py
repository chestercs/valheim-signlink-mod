"""Dump the Python layout of every built row (out/parity.json) for the JS parity check."""
import json
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import engine as E  # noqa: E402

out = []
for e in json.load(open("out/concepts.json", encoding="utf-8")):
    grid = E.Grid.from_dict(e["grid"])
    for r in e.get("rows", []):
        c = E.parse(r["text"])
        p = E.layout(c, r["cell"], grid)
        back = E.encode(E.to_cmd(p, grid))      # inverse layout must give the same command back
        out.append({"grid": e["grid"], "cell": r["cell"], "text": r["text"], "x": p.x, "y": p.y,
                    "enc": E.encode(c), "back": back})
json.dump(out, open("out/parity.json", "w", encoding="utf-8"), ensure_ascii=False)
bad = [o for o in out if o["back"] != o["text"]]
print(len(out), "rows,", len(bad), "python round-trip mismatches", bad[:2])
sys.exit(1 if bad else 0)
