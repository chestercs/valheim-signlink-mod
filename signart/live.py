#!/usr/bin/env python3
"""Talk to the SignLink plugin in the running game (mod/SignLink): list, read, write and animate signs.

  live.py ping
  live.py signs [--radius 50]
  live.py read ID                     (ID: a ZDO id from `signs`, or BLOCK:CELL-LAYER such as 4:A2-1)
  live.py write ID (--text TEXT | --file PATH) [--mode local|zdo]
  live.py anim ID [--frames concepts.anim1:fjord[:above=90,n=24]] [--fps 8] [--mode local|zdo] [--once] [--save DIR]
               [--store]
  live.py unstore ID
  live.py image ID PICTURE [--pixels 200000] [--pitch 1] [--cover 2] [--above-m 1] [--max-kb 400] [--keep] [--dry]
  live.py stack ID
  live.py stats
  live.py stop [ID]

mode local: only this game client shows the text (no network); mode zdo: written like the vanilla editor,
every player sees it (vanilla clients re-read signs every 2 s). --store (SignLink 0.4+) saves the frames on the
sign instead: every SignLink player plays them on the world clock, after restarts too; vanilla players see the
first frame. Ids come from `signs` and change every session.
In Docker the game is reached at host.docker.internal; SIGNLINK_URL overrides the address.
"""
import argparse
import importlib
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

URL = os.environ.get("SIGNLINK_URL") or (
    "http://host.docker.internal:8417" if os.path.exists("/.dockerenv") else "http://127.0.0.1:8417")
SEP = "\u001e"      # between frames
GS = "\u001d"       # between the signs of one frame (anim-block)
CFG = os.environ.get("SIGNLINK_CFG", "/cfg/PetyusPolisz.SignLink.cfg")


def password():
    """SIGNLINK_PASSWORD, else the master password from the plugin's config (sign.sh mounts it at /cfg)."""
    if os.environ.get("SIGNLINK_PASSWORD"):
        return os.environ["SIGNLINK_PASSWORD"]
    try:
        for line in open(CFG, encoding="utf-8-sig"):
            k, _, v = line.partition("=")
            if k.strip() == "Password":
                return v.strip()
    except OSError:
        pass
    return ""


def call(method, path, body=None, timeout=60, **query):
    q = urllib.parse.urlencode({k: v for k, v in query.items() if v is not None})
    req = urllib.request.Request(f"{URL}{path}{'?' + q if q else ''}", method=method,
                                 data=None if body is None else body if isinstance(body, bytes) else body.encode("utf-8"),
                                 headers={"Content-Type": "text/plain; charset=utf-8", "X-SignLink-Password": password()})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        return json.loads(e.read().decode("utf-8"))
    except urllib.error.URLError as e:
        sys.exit(f"SignLink not reachable at {URL} ({e.reason}): is the game running with the plugin?")


def frames_from(spec, **extra):
    """'module:function[:key=value,...]' -> what the function returns (numbers passed as int/float)."""
    mod, _, rest = spec.partition(":")
    fn, _, args = rest.partition(":")
    kw = {}
    for pair in filter(None, args.split(",")):
        k, _, v = pair.partition("=")
        try:
            kw[k] = int(v) if v.lstrip("-").isdigit() else float(v)
        except ValueError:
            kw[k] = v
    return getattr(importlib.import_module(mod), fn or "frames")(**extra, **kw)


def resolve(sign, row_map="rigs/bakterhaz-row.json"):
    """'1:115635' (a ZDO id) stays; 'BLOCK:CELL-LAYER' (e.g. '4:A2-1') is looked up by position in the row map."""
    import re
    m = re.fullmatch(r"(\d+):([A-Z]\d+-\d+)", sign)
    if not m:
        return sign
    block = json.load(open(row_map, encoding="utf-8"))["blocks"][int(m.group(1)) - 1]["signs"]
    return ids_by_position({m.group(2): block[m.group(2)]})[m.group(2)]


def ids_by_position(positions, tol=0.05):
    """{key: world position} -> {key: sign id} for the loaded signs (saves keep no ids, positions do)."""
    out = call("GET", "/signs", radius=500)
    if not out.get("ok"):
        sys.exit(json.dumps(out))
    ids = {}
    for key, pos in positions.items():
        best = min(out["signs"], key=lambda s: sum((u - v) ** 2 for u, v in zip(s["pos"], pos)), default=None)
        if best is None or sum((u - v) ** 2 for u, v in zip(best["pos"], pos)) > tol ** 2:
            sys.exit(f"sign {key} at {pos} is not loaded (stand closer to the block)")
        ids[key] = best["id"]
    return ids


def gif_frames(path, max_mb=48.0):
    """A GIF (at its real speed: the shortest frame duration is the clock, longer frames repeat) or a still image
    -> (raw RGB bytes, rows top-down; W; H; fps; source w; source h; source frame count), scaled down only to
    stay under max_mb."""
    from PIL import Image, ImageSequence
    im = Image.open(path)
    frames, durations = [], []
    for f in ImageSequence.Iterator(im):
        frames.append(f.convert("RGB").copy())
        d = f.info.get("duration", 100) or 100
        durations.append(100 if d <= 10 else d)
    tick = min(durations)
    seq = [fr for fr, d in zip(frames, durations) for _ in range(max(1, round(d / tick)))]
    w, h = frames[0].size
    scale = min(1.0, (max_mb * 1048576 / (len(seq) * w * h * 3)) ** 0.5)
    W, H = max(1, int(w * scale)), max(1, int(h * scale))
    raw = b"".join((fr if (W, H) == (w, h) else fr.resize((W, H), Image.LANCZOS)).tobytes() for fr in seq)
    return raw, W, H, round(1000 / tick, 3), w, h, len(frames)


def screen(sign_id, path, height_m=7.5, above=40.0, max_mb=48.0, filt="bilinear"):
    """A GIF or a still image as a texture screen centred over the sign (SignLink 0.3+). Frames go as raw RGB,
    rows top-down."""
    raw, W, H, fps, w, h, n = gif_frames(path, max_mb)
    pitch = height_m * 40 / H
    print(f"{path}: {w} x {h}, {n} frames -> {len(raw) // (W * H * 3)} at {fps:.1f} fps, {W} x {H} "
          f"({W * pitch / 40:.1f} x {height_m:.1f} m), {len(raw) / 1048576:.1f} MB raw")
    return call("POST", "/screen", raw, timeout=300, id=sign_id, w=W, h=H, fps=fps, cx=0,
                cy=round(above + H * pitch / 2, 3), pitch=round(pitch, 5), filter=filt)


def bench(sign_id, out_path, sizes=((16, True), (32, True), (64, True), (64, False), (128, True))):
    """Apply time per frame for square pictures (noisy 12-bit colours or one colour), 160 wu wide, in local
    mode: docs/limits.md. Each size runs ~4 frames at a rate that keeps it near a quarter of a second per second."""
    import random
    import engine as E
    import pixels as P
    rnd = random.Random(1)
    gx, gy = E.DEFAULT_GRID.pos("A1")
    rows = []
    for side, noisy in sizes:
        n = side * side
        frames = [P.column_text(P.items([[("#" + "".join(rnd.choice(P.HEX) for _ in range(3))) if noisy
                                          else ("#4af" if k % 2 else "#fa4") for _ in range(side)] for _ in range(side)],
                                        160.0 / side, gx, gy + 60, "A1")) for k in range(4)]
        fps = max(1.0, min(20.0, round(0.25 / (n * 13e-6))))
        call("POST", "/anim", SEP.join(frames), id=sign_id, fps=fps, mode="local")
        time.sleep(4.5 / fps + 0.8)
        st = [x for x in call("GET", "/anim").get("anims", []) if sign_id in (x.get("id"), x.get("ids"))][0]
        call("POST", "/anim/stop", id=sign_id)
        r = {"pixels": n, "noisy": noisy, "chars": sum(map(len, frames)) // len(frames), "fps": fps,
             "shown": st["shown"], "avgMs": st["avgMs"], "maxMs": st["maxMs"], "usPerPixel": round(st["avgMs"] * 1000 / n, 2)}
        rows.append(r)
        print(r, flush=True)
        time.sleep(1.0)
    json.dump(rows, open(out_path, "w"), indent=1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("ping")
    p = sub.add_parser("signs")
    p.add_argument("--radius", type=float, default=50)
    p = sub.add_parser("read")
    p.add_argument("id")
    p = sub.add_parser("write")
    p.add_argument("id")
    g = p.add_mutually_exclusive_group(required=True)
    g.add_argument("--text")
    g.add_argument("--file")
    p.add_argument("--mode", choices=("local", "zdo"), default="local")
    p = sub.add_parser("anim")
    p.add_argument("id")
    p.add_argument("--frames", default="concepts.anim1:fjord", help="module:function returning a list of texts")
    p.add_argument("--fps", type=float, default=8)
    p.add_argument("--mode", choices=("local", "zdo"), default="local")
    p.add_argument("--once", action="store_true", help="play once instead of looping")
    p.add_argument("--save", help="also write the frames to this folder (frame_NN.txt)")
    p.add_argument("--store", action="store_true", help="save the animation on the sign (SignLink 0.4+)")
    p = sub.add_parser("unstore", help="remove the animation stored on a sign (SignLink 0.4+)")
    p.add_argument("id")
    p = sub.add_parser("anim-block", help="one animation over several signs of a gallery block (one clock)")
    p.add_argument("block", type=int, help="block number in the row map (1-6)")
    p.add_argument("--frames", default="concepts.anim1:fjord_bands", help="module:function(grid, ...) -> {cell-layer: [texts]}")
    p.add_argument("--fps", type=float, default=4)
    p.add_argument("--mode", choices=("local", "zdo"), default="zdo")
    p.add_argument("--map", default="rigs/bakterhaz-row.json")
    p.add_argument("--rig", default="rigs/bakterhaz-render-engine.json")
    p.add_argument("--store", action="store_true", help="save the animation on the signs (SignLink 0.4+)")
    p = sub.add_parser("screen", help="play a GIF (or show an image) as a texture screen on a sign (SignLink 0.3+)")
    p.add_argument("id")
    p.add_argument("image", help="GIF, PNG or JPG")
    p.add_argument("--height-m", type=float, default=7.5, help="picture height in metres")
    p.add_argument("--above", type=float, default=40, help="bottom edge above the sign, wu (40 = 1 m)")
    p.add_argument("--max-mb", type=float, default=48, help="cap on the raw RGB frames; larger GIFs are scaled down")
    p.add_argument("--filter", choices=("point", "bilinear"), default="bilinear")
    p = sub.add_parser("image", help="a PNG or JPG as saved sign text, stacked copies when needed (SignLink 0.4+)")
    p.add_argument("id")
    p.add_argument("image", help="PNG or JPG (the plugin decodes and encodes it)")
    p.add_argument("--pixels", type=int, default=200000)
    p.add_argument("--pitch", type=float, default=1.0, help="wu per pixel: 0.5, 0.75, 1, 1.5 or 2")
    p.add_argument("--cover", type=float, default=2.0, help="pixel glyph width in pixels (2: opaque from afar)")
    p.add_argument("--above-m", type=float, default=1.0, help="bottom edge above the sign, metres")
    p.add_argument("--max-kb", type=float, default=400, help="per sign; 0 = no cap (single player only)")
    p.add_argument("--keep", action="store_true", help="keep the copies already stacked on the sign")
    p.add_argument("--dry", action="store_true", help="encode only: sizes, nothing written")
    p = sub.add_parser("stack", help="the sign and the copies stacked on it (SignLink 0.4+)")
    p.add_argument("id")
    p = sub.add_parser("screen-stop", help="remove the texture screen of a sign, or all")
    p.add_argument("id", nargs="?")
    sub.add_parser("screens")
    p = sub.add_parser("bench", help="time frames of 256..16384 pixels on one sign (local mode; the game stutters)")
    p.add_argument("id")
    p.add_argument("--out", default="out/bench_local.json")
    p = sub.add_parser("clear", help="empty every sign of a gallery row (positions from the row map), nothing else")
    p.add_argument("--map", default="rigs/bakterhaz-row.json")
    p.add_argument("--mode", choices=("local", "zdo"), default="zdo")
    p.add_argument("--dry-run", action="store_true")
    sub.add_parser("stats")
    p = sub.add_parser("stop")
    p.add_argument("id", nargs="?")
    a = ap.parse_args()
    if getattr(a, "id", None):
        a.id = resolve(a.id)          # also BLOCK:CELL-LAYER, e.g. 4:A2-1

    if a.cmd == "ping":
        out = call("GET", "/ping")
    elif a.cmd == "signs":
        out = call("GET", "/signs", radius=a.radius)
        if out.get("ok"):
            for s in out["signs"]:
                print(f"{s['id']:>24}  {s['dist']:6.1f} m  yaw {s['yaw']:6.1f}  {s['len']:7d} chars  {s['preview']!r}")
            return
    elif a.cmd == "read":
        out = call("GET", "/sign", id=a.id)
        if out.get("ok"):
            print(out["text"])
            return
    elif a.cmd == "write":
        text = a.text if a.text is not None else open(a.file, encoding="utf-8").read()
        out = call("POST", "/sign", text, id=a.id, mode=a.mode)
    elif a.cmd == "anim":
        t0 = time.time()
        frames = frames_from(a.frames)
        sizes = [len(f) for f in frames]
        print(f"{len(frames)} frames in {time.time() - t0:.1f} s, {min(sizes)}-{max(sizes)} chars each "
              f"({sum(sizes) / len(sizes) * a.fps / 1024:.0f} KB/s at {a.fps:g} fps if sent over the network)")
        if a.save:
            os.makedirs(a.save, exist_ok=True)
            for i, f in enumerate(frames):
                open(os.path.join(a.save, f"frame_{i:02d}.txt"), "w", encoding="utf-8").write(f)
        out = call("POST", "/anim", SEP.join(frames), id=resolve(a.id), fps=a.fps, mode=a.mode, loop=0 if a.once else 1,
                   store=1 if a.store else None)
    elif a.cmd == "unstore":
        out = call("POST", "/anim/unstore", id=resolve(a.id))
    elif a.cmd == "anim-block":
        from rig import rig_grid
        gal = json.load(open(a.map, encoding="utf-8"))
        block = gal["blocks"][a.block - 1]["signs"]
        t0 = time.time()
        per_sign = frames_from(a.frames, grid=rig_grid(json.load(open(a.rig, encoding="utf-8"))))
        keys = sorted(per_sign)
        n = len(per_sign[keys[0]])
        ids = ids_by_position({k: block[k] for k in keys})
        sent = [sum(len(per_sign[k][f]) for k in keys if f == 0 or per_sign[k][f] != per_sign[k][f - 1]) for f in range(n)]
        print(f"block {a.block}: {len(keys)} signs x {n} frames in {time.time() - t0:.1f} s; after the first frame "
              f"{sum(sent[1:]) / max(1, n - 1) / 1024:.1f} KB per frame changes "
              f"({sum(sent[1:]) / max(1, n - 1) * a.fps / 1024:.0f} KB/s at {a.fps:g} fps in zdo mode)")
        body = SEP.join(GS.join(per_sign[k][f] for k in keys) for f in range(n))
        out = call("POST", "/anim", body, ids=",".join(ids[k] for k in keys), fps=a.fps, mode=a.mode,
                   store=1 if a.store else None)
    elif a.cmd == "screen":
        out = screen(a.id, a.image, a.height_m, a.above, a.max_mb, a.filter)
    elif a.cmd == "image":
        with open(a.image, "rb") as f:
            out = call("POST", "/image", f.read(), timeout=300, id=resolve(a.id), pixels=a.pixels, pitch=a.pitch,
                       cover=a.cover, above=a.above_m, maxkb=a.max_kb, replace=0 if a.keep else 1, dry=1 if a.dry else 0)
    elif a.cmd == "stack":
        out = call("GET", "/stack", id=resolve(a.id))
    elif a.cmd == "screen-stop":
        out = call("POST", "/screen/stop", id=a.id)
    elif a.cmd == "screens":
        out = call("GET", "/screens")
    elif a.cmd == "bench":
        bench(a.id, a.out)
        return
    elif a.cmd == "clear":
        gal = json.load(open(a.map, encoding="utf-8"))
        mine = [pos for b in gal["blocks"] for pos in b["signs"].values()]
        out = call("GET", "/signs", radius=500)
        if not out.get("ok"):
            sys.exit(json.dumps(out))
        hits = [s for s in out["signs"]
                if s["len"] and min(sum((u - v) ** 2 for u, v in zip(s["pos"], m)) for m in mine) < 0.05 ** 2]
        print(f"{gal['world']}: {len(mine)} mapped signs; {len(hits)} loaded ones with text"
              + (" (dry run)" if a.dry_run else ""))
        for s in hits:
            if not a.dry_run:
                r = call("POST", "/sign", "", id=s["id"], mode=a.mode)
                if not r.get("ok"):
                    sys.exit(json.dumps(r))
            print(f"  cleared {s['id']} ({s['len']} chars)")
        return
    elif a.cmd == "stats":
        out = call("GET", "/anim")
    else:
        out = call("POST", "/anim/stop", id=a.id)
    print(json.dumps(out, ensure_ascii=False, indent=1))
    if not out.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
