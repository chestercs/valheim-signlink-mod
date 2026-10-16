"""Valheim sign-art engine: TMP layout model, command codec, renderer, overlap check, cell assignment.

Coordinates ("world units", wu) follow the convention of the original in-game calibration:
one wu = 2.5 cm, X to the right, Y up, origin at the board of cell A1. A physical sign is 40 x 20 wu.
The build is a Grid of cells (cols x rows, each a stack of `depth` signs behind each other); with
`gap` empty sign slots between active signs, cell (col,row) sits at (40*(gap_x+1)*col, -20*(gap_y+1)*row).
The default 3x3x5 grid with one empty slot gives the calibrated 80/40 pitch.

Why the numbers are what they are (game code, docs/sign-rendering.md): the sign's TextMeshProUGUI is
centered both ways, with a 0.05 local scale. A single glyph moved by <pos=P> is re-centered by half
of its line advance, and one moved by <voffset=V> is re-centered by half of the line extent, so the
glyph only moves P/2 and V/2 local units = P and V wu. Glyph sizes are not halved: a <size=S>
glyph's em is 2*S wu tall.

Model for one visible glyph per sign (TMP 3.x, TextMeshProUGUI.GenerateTextMesh):
  bbox center X = P + 2*sx*S*((xMin+xMax)/2 - adv/2) + pitch_x*col
  bbox center Y = V - S*(A+D) + S*(yMin+yMax)     - pitch_y*row
with the glyph metrics in em, A/D the TMP ascent/descent lines of the glyph's font asset and
sx the <scale> factor (negative = mirrored). <rotate=R> turns the glyph around its bbox center
(counter-clockwise).
"""
from __future__ import annotations

import json
from collections import Counter
import math
import os
import re
from dataclasses import dataclass, field, replace

SIGN_W, SIGN_H = 40, 20            # one physical sign in wu (1.0 x 0.5 m)
# Where the art's (0,0) lands, in wu from the A1 board: a little right of the 3x3 build so a +-300 wu piece
# keeps every pos positive and short (a minus sign costs a character; negative pos itself works, verified
# 2026-10-08). Small pieces are centred over the build with Art.fit_above. Y = 640 wu = 16 m up.
DEFAULT_ORIGIN = (300, 640)
CHAR_LIMIT = 50                     # Sign.m_characterLimit, counted on the raw UTF-16 string
DEFAULT_COLOR = (0x1E, 0x1A, 0x12, 255)  # sign Text m_fontColor (dark brown) when no color tag
# Unlit text: <font=...> switches to a font asset's own material, the unlit TMP "Distance Field" shader
# (verified in game 2026-10-08: glows at night). The long Resources path must be loaded once per game
# session by a LOADER sign; after that the asset is found by the hash of its name, so a 5-character alias
# with the same TMP hash as "Valheim-Norse" works (font_alias.py). Loader on a sign closer to the viewer
# than the art's signs (objects are created nearest first); its small glowing rune shows it ran.
FONT_PATH = "valheim_fonts/valheim-norse"
FONT_ALIAS = "亨杓麩壀辨"
LOADER_TEXT = f"<font={FONT_PATH}><#ff0><size=9>ᛟ"

NAMED_COLORS = {"red": "#FF0000", "green": "#00FF00", "blue": "#0000FF", "white": "#FFFFFF",
                "black": "#000000", "yellow": "#FFFF00", "orange": "#FF8000", "purple": "#A020F0"}


def row_name(i: int) -> str:
    """0 -> A, 25 -> Z, 26 -> AA (spreadsheet style)."""
    s = ""
    i += 1
    while i:
        i, r = divmod(i - 1, 26)
        s = chr(65 + r) + s
    return s


@dataclass(frozen=True)
class Grid:
    """The physical build: cols x rows cells, each a stack of `depth` signs (L1 = rearmost).

    gap_x/gap_y = empty sign slots between neighbouring active signs (1 = the tested sparse layout,
    0 = signs edge to edge; fractions for free placement, e.g. 0.5). backdrop = extra cells deep behind the build, given as (col, row) board
    positions: their signs are meant to draw behind everything (round backgrounds). That needs the
    in-game depth test (signart/ingame_tests.json) before it is trusted.
    """
    cols: int = 3
    rows: int = 3
    depth: int = 5
    gap_x: float = 1
    gap_y: float = 1
    backdrop: tuple = ()
    backdrop_depth: int = 5
    # measured build (a rig, see rig.py): ((name, x_wu, y_wu), ...) in entry order, relative to A1;
    # overrides cols/rows/gaps. Real builds are not perfectly regular (rows 1.10 m and 0.99 m apart).
    positions: tuple = ()
    name: str = ""
    # measured position of every single sign: ((cell, layer, x_wu, y_wu[, depth_wu]), ...). Cells are the
    # mean of their layers; one sign can sit ~1 wu off it (and a few cm deeper), which shows as a seam
    # where a seamless mosaic changes sign.
    sign_positions: tuple = ()

    @property
    def pitch_x(self):
        return SIGN_W * (self.gap_x + 1)

    @property
    def pitch_y(self):
        return SIGN_H * (self.gap_y + 1)

    @property
    def cells(self) -> dict:
        """name -> (col, row, is_backdrop); col/row are None for a measured grid."""
        if self.positions:
            return {n: (None, None, n.startswith("BG")) for n, _, _ in self.positions}
        out = {f"BG{k + 1}": (c, r, True) for k, (c, r) in enumerate(self.backdrop)}
        for c in range(self.cols):
            for r in range(self.rows):
                out[f"{row_name(r)}{c + 1}"] = (c, r, False)
        return out

    def pos(self, cell, layer=None):
        """(x, y) in wu of the cell's boards (A1 = origin); with a layer, that sign's own board if measured."""
        if layer is not None:
            for t in self.sign_positions:
                if t[0] == cell and t[1] == layer:
                    return t[2], t[3]
        if self.positions:
            for n, x, y in self.positions:
                if n == cell:
                    return x, y
            raise KeyError(cell)
        col, row, _ = self.cells[cell]
        return self.pitch_x * col, -self.pitch_y * row

    def depth_of(self, cell, layer):
        """Measured depth of one sign along the sign normal (wu), None if not measured."""
        for t in self.sign_positions:
            if t[0] == cell and t[1] == layer and len(t) > 4:
                return t[4]
        return None

    @property
    def order(self) -> list:
        """In-game entry order: backdrop first, then column by column, top to bottom (A1, B1, C1, A2...)."""
        if self.positions:
            return [n for n, _, _ in self.positions]
        return [f"BG{k + 1}" for k in range(len(self.backdrop))] + \
            [f"{row_name(r)}{c + 1}" for c in range(self.cols) for r in range(self.rows)]

    @property
    def center_x(self):
        xs = [self.pos(c)[0] for c in self.order if not self.cells[c][2]]
        return (min(xs) + max(xs)) / 2

    @property
    def top_y(self):
        """Top edge of the top row of boards."""
        return max(self.pos(c)[1] for c in self.order if not self.cells[c][2]) + SIGN_H / 2

    def capacity(self, cell):
        return self.backdrop_depth if cell.startswith("BG") else self.depth

    def label(self):
        if self.positions:
            return f"rig {self.name or ''} ({len(self.positions)}×{self.depth})".replace("  ", " ")
        bg = f" + {len(self.backdrop)} háttér" if self.backdrop else ""
        return f"{self.cols}×{self.rows}×{self.depth}{bg}"

    def to_dict(self):
        d = {"cols": self.cols, "rows": self.rows, "depth": self.depth, "gap_x": self.gap_x,
             "gap_y": self.gap_y, "backdrop": [list(b) for b in self.backdrop],
             "backdrop_depth": self.backdrop_depth, "label": self.label()}
        if self.positions:
            d["positions"] = [[n, round(x, 3), round(y, 3)] for n, x, y in self.positions]
            d["name"] = self.name
        if self.sign_positions:
            d["sign_positions"] = [[t[0], t[1]] + [round(v, 3) for v in t[2:]] for t in self.sign_positions]
        return d

    @staticmethod
    def from_dict(d):
        d = {k: v for k, v in d.items() if k != "label"}
        d["backdrop"] = tuple(tuple(b) for b in d.get("backdrop", ()))
        d["positions"] = tuple(tuple(p) for p in d.get("positions", ()))
        d["sign_positions"] = tuple(tuple(p) for p in d.get("sign_positions", ()))
        return Grid(**d)


DEFAULT_GRID = Grid()
# kept for scripts written against the 3x3 build
CELLS = {k: (c, r) for k, (c, r, _) in DEFAULT_GRID.cells.items()}
ORDER = DEFAULT_GRID.order
PITCH_X, PITCH_Y = DEFAULT_GRID.pitch_x, DEFAULT_GRID.pitch_y

_HERE = os.path.dirname(os.path.abspath(__file__))
GLYPHS_PATH = os.path.join(_HERE, "out", "glyphs.json")


# ----------------------------------------------------------------------------- glyph data

class GlyphDB:
    def __init__(self, path: str = GLYPHS_PATH):
        with open(path, encoding="utf-8") as f:
            doc = json.load(f)
        self.fonts = doc["fonts"]
        self.glyphs = doc["glyphs"]

    def __contains__(self, ch):
        return ch in self.glyphs

    def get(self, ch):
        g = self.glyphs.get(ch)
        if g is None:
            raise KeyError(f"glyph {ch!r} (U+{ord(ch[0]):04X}) is not renderable on a sign")
        return g

    def metrics(self, ch):
        g = self.get(ch)
        f = self.fonts[g["f"]]
        return g, f["A"], f["D"], f["bold"]


_DB: GlyphDB | None = None


def db() -> GlyphDB:
    global _DB
    if _DB is None:
        _DB = GlyphDB()
    return _DB


# ----------------------------------------------------------------------------- command codec

@dataclass
class Cmd:
    """One sign's text, reduced to the tags the engine models."""
    glyph: str
    size: float = 1.0
    pos: float = 0.0
    voffset: float = 0.0
    rotate: float = 0.0
    scale: float = 1.0
    color: tuple = DEFAULT_COLOR    # r,g,b,a 0..255
    font: str | None = None         # <font=...> value: unlit material (see FONT_ALIAS)
    raw: str = ""
    warnings: list = field(default_factory=list)


def parse_hex(h: str):
    h = h.lstrip("#")
    if len(h) in (3, 4):
        h = "".join(c * 2 for c in h)
    if len(h) == 6:
        h += "FF"
    if len(h) != 8 or not re.fullmatch(r"[0-9a-fA-F]{8}", h):
        raise ValueError(f"bad color #{h}")
    return tuple(int(h[i:i + 2], 16) for i in range(0, 8, 2))


def utf16_len(s: str) -> int:
    return len(s.encode("utf-16-le")) // 2


_TAG = re.compile(r"<([^<>]*)>")


def parse(text: str) -> Cmd:
    """Parse a sign text (the subset of TMP rich text the engine models)."""
    c = Cmd(glyph="", raw=text)
    alpha = None
    visible = []
    i = 0
    while i < len(text):
        m = _TAG.match(text, i)
        if m:
            t = m.group(1)
            i = m.end()
            if t.startswith("#"):
                c.color = parse_hex(t)
            elif t.startswith("color="):
                v = t[6:].strip('"')
                c.color = parse_hex(v if v.startswith("#") else NAMED_COLORS[v.lower()])
            elif t.startswith("font="):
                c.font = t[5:].strip('"')
            elif t.startswith("alpha=#"):
                alpha = int(t[7:9], 16)
            elif "=" in t and t.split("=")[0] in ("size", "pos", "voffset", "rotate", "scale"):
                k, v = t.split("=", 1)
                if v.endswith("%") or v.endswith("em"):
                    c.warnings.append(f"<{t}>: only plain numbers are modeled")
                setattr(c, k, float(v.rstrip("%em")))
            else:
                c.warnings.append(f"<{t}> is not modeled (ignored)")
            continue
        visible.append(text[i])
        i += 1
    if alpha is not None:
        c.color = c.color[:3] + (alpha,)
    vis = "".join(visible)
    # one user-perceived glyph may be a surrogate pair (emoji) or carry a variation selector
    vis = vis.replace("️", "").replace("︎", "")
    if len(vis) == 0:
        c.warnings.append("no visible glyph")
    elif len(vis) > 1:
        c.warnings.append("more than one glyph per sign: line wrapping is not modeled")
    c.glyph = vis[:1]
    if utf16_len(text) > CHAR_LIMIT:
        c.warnings.append(f"{utf16_len(text)} chars > sign limit {CHAR_LIMIT}")
    return c


def _num(v: float, nd=0) -> str:
    if nd == 0:
        return str(int(round(v)))
    s = f"{v:.{nd}f}".rstrip("0").rstrip(".")
    if s in ("-0", ""):
        return "0"
    # TMP's tag parser accepts a leading '.', one character less: 0.18 -> .18 (verified in game)
    return s.replace("0.", ".", 1) if s.startswith(("0.", "-0.")) else s


def short_hex(rgba) -> str:
    r, g, b, a = rgba
    parts = [r, g, b] + ([a] if a < 255 else [])
    if all(p % 17 == 0 for p in parts):
        return "#" + "".join(f"{p // 17:x}" for p in parts)
    return "#" + "".join(f"{p:02x}" for p in parts)


def encode(c: Cmd) -> str:
    out = [f"<font={c.font}>"] if c.font else []
    if tuple(c.color) != DEFAULT_COLOR:
        out.append(f"<{short_hex(c.color)}>")
    out.append(f"<size={_num(c.size)}>")
    if round(c.voffset):
        out.append(f"<voffset={_num(c.voffset)}>")
    if round(c.pos):
        out.append(f"<pos={_num(c.pos)}>")
    if round(c.rotate) % 360:
        out.append(f"<rotate={_num(c.rotate)}>")
    if abs(c.scale - 1) > 0.005:
        out.append(f"<scale={_num(c.scale, 2)}>")
    out.append(c.glyph)
    return "".join(out)


# ----------------------------------------------------------------------------- layout model

@dataclass
class Placed:
    """A glyph as it lands on the canvas (world units)."""
    glyph: str
    x: float           # bbox center
    y: float
    size: float        # TMP size S; em = 2*S wu
    scale: float = 1.0
    rotate: float = 0.0
    color: tuple = DEFAULT_COLOR
    cell: str | None = None
    layer: int | None = None
    z: int = 0
    tag: str = ""      # free label from the art spec (debug)
    backdrop: bool = False   # drawn by a backdrop cell (deep behind the build): may sit under anything
    font: str | None = None  # unlit: rendered with a font asset's own (unlit) material


def center_offset(ch: str, S: float, sx: float = 1.0):
    """(dx, dy) of the glyph's bbox center relative to (pos, voffset), in wu, for a sign at cell (0,0)."""
    g, A, D, _ = db().metrics(ch)
    x0, y0, x1, y1 = g["bb"]
    dx = 2 * sx * S * ((x0 + x1) / 2 - g["adv"] / 2)
    dy = -S * (A + D) + S * (y0 + y1)
    return dx, dy


def layout(c: Cmd, cell: str, grid: Grid = DEFAULT_GRID, layer=None) -> Placed:
    bg = grid.cells[cell][2]
    gx, gy = grid.pos(cell, layer)
    dx, dy = center_offset(c.glyph, c.size, c.scale)
    return Placed(c.glyph, c.pos + dx + gx, c.voffset + dy + gy,
                  c.size, c.scale, c.rotate, c.color, cell, backdrop=bg, font=c.font)


def to_cmd(p: Placed, grid: Grid = DEFAULT_GRID) -> Cmd:
    """Inverse layout: the command that puts glyph p (with p.cell set) where p says."""
    gx, gy = grid.pos(p.cell)
    S, sx = max(1, round(p.size)), round(p.scale, 2)
    glyph, rot = rotation_twin(p.glyph, round(p.rotate), sx)
    dx, dy = center_offset(glyph, S, sx)
    if _symmetric_180(glyph):            # shorter tag: 135 -> -45, -90 -> 90 for point-symmetric glyphs
        rot = (rot + 89) % 180 - 89
    else:
        rot = (rot + 180) % 360 - 180
    c = Cmd(glyph, size=S, pos=round(p.x - dx - gx),
            voffset=round(p.y - dy - gy), rotate=rot, scale=sx, color=p.color, font=p.font)
    return shorten(c) if utf16_len(encode(c)) > CHAR_LIMIT else c


def shorten(c: Cmd) -> Cmd:
    """Drop a digit from an over-limit command with an invisible nudge.

    scale to one decimal (<4 %), pos/voffset 100..103 -> 99 (3 wu = 7.5 cm), size by <4 % (center kept).
    """
    def L(x):
        return utf16_len(encode(x))
    best = c
    if abs(c.scale - 1) > 0.005 and round(c.scale, 1) != c.scale:
        t = replace(c, scale=round(c.scale, 1))
        if t.scale and abs(t.scale - c.scale) / abs(c.scale) < 0.04:
            best = t
    for field_, tol in (("pos", 3), ("voffset", 3), ("size", None)):
        if L(best) <= CHAR_LIMIT:
            break
        v = getattr(best, field_)
        for target in (99, -9, 999):
            t_ol = tol if tol is not None else max(1, abs(v) * 0.04)
            if abs(v) > abs(target) and abs(v) - abs(target) <= t_ol and (v > 0) == (target > 0):
                t = replace(best, **{field_: target})
                if field_ == "size":   # keep the glyph center where it was
                    dx0, dy0 = center_offset(c.glyph, best.size, best.scale)
                    dx1, dy1 = center_offset(c.glyph, target, best.scale)
                    t = replace(t, pos=round(best.pos + dx0 - dx1), voffset=round(best.voffset + dy0 - dy1))
                best = t
                break
    return best


# Glyphs that are rotations of each other: glyph -> its counter-clockwise angle from the family's first member.
ROT_FAMILIES = [{"▲": 0, "◀": 90, "▼": 180, "▶": 270}, {"◢": 0, "◥": 90, "◤": 180, "◣": 270}]


def rotation_twin(ch, rot, sx=1.0):
    """Same shape with the smallest rotation tag: ▲ at 120° is ◀ at 30°.

    90° steps swap the stretch axis, so they are only used unstretched; 180° commutes with <scale>.
    Mirrored glyphs are left alone."""
    fam = next((f for f in ROT_FAMILIES if ch in f), None)
    if fam is None or sx < 0:
        return ch, rot
    total = fam[ch] + rot
    best = (ch, rot)
    for m, base in fam.items():
        if abs(sx - 1) > 0.005 and (base - fam[ch]) % 180:
            continue
        r = (total - base + 180) % 360 - 180
        if abs(r) < abs((best[1] + 180) % 360 - 180):
            best = (m, r)
    return best


def _symmetric_180(ch):
    return ch in "■□●○◆◇◯⭕█▬━─│┃|-=+×✕╳◎◉⊕⊗※✳✴❖♦♢◊"


def placed_bbox(items):
    """World bbox (l, b, r, t) of placed glyphs from their rotated em boxes (no rendering needed)."""
    import math
    l = b = math.inf
    r = t = -math.inf
    for p in items:
        x0, y0, x1, y1 = db().get(p.glyph)["bb"]
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        a = math.radians(p.rotate)
        for u, v in ((x0, y0), (x1, y0), (x1, y1), (x0, y1)):
            X, Y = (u - cx) * 2 * p.size * p.scale, (v - cy) * 2 * p.size
            px, py = p.x + X * math.cos(a) - Y * math.sin(a), p.y + X * math.sin(a) + Y * math.cos(a)
            l, r, b, t = min(l, px), max(r, px), min(b, py), max(t, py)
    return l, b, r, t


def glyph_box(ch: str):
    """bbox width/height of a glyph in em."""
    x0, y0, x1, y1 = db().get(ch)["bb"]
    return x1 - x0, y1 - y0


# ----------------------------------------------------------------------------- rendering (skia)

_PATH_TOKEN = re.compile(r"[MLQCZHVmlqczhv]|-?\d*\.?\d+(?:e-?\d+)?")
_PATH_CACHE: dict = {}


def svg_path(ch: str):
    import skia
    if ch in _PATH_CACHE:
        return _PATH_CACHE[ch]
    toks = _PATH_TOKEN.findall(db().get(ch)["d"])
    p = skia.Path()
    i, cmd, cur, start = 0, None, (0.0, 0.0), (0.0, 0.0)

    def nums(n):
        nonlocal i
        v = [float(t) for t in toks[i:i + n]]
        i += n
        return v
    while i < len(toks):
        t = toks[i]
        if t.isalpha():
            cmd = t
            i += 1
            if cmd in "Zz":
                p.close()
                cur = start
                continue
        if cmd == "M":
            x, y = nums(2); p.moveTo(x, y); cur = start = (x, y); cmd = "L"
        elif cmd == "L":
            x, y = nums(2); p.lineTo(x, y); cur = (x, y)
        elif cmd == "H":
            (x,) = nums(1); p.lineTo(x, cur[1]); cur = (x, cur[1])
        elif cmd == "V":
            (y,) = nums(1); p.lineTo(cur[0], y); cur = (cur[0], y)
        elif cmd == "Q":
            a, b, x, y = nums(4); p.quadTo(a, b, x, y); cur = (x, y)
        elif cmd == "C":
            a, b, c2, d, x, y = nums(6); p.cubicTo(a, b, c2, d, x, y); cur = (x, y)
        else:
            raise ValueError(f"path command {cmd} for {ch!r}")
    _PATH_CACHE[ch] = p
    return p


def world_matrix(p: Placed):
    """skia matrix: glyph em space (y up) -> world units (y up)."""
    import skia
    g = db().get(p.glyph)
    x0, y0, x1, y1 = g["bb"]
    m = skia.Matrix()
    m.preTranslate(p.x, p.y)
    m.preRotate(p.rotate)
    m.preScale(2 * p.size * p.scale, 2 * p.size)
    m.preTranslate(-(x0 + x1) / 2, -(y0 + y1) / 2)
    return m


def world_path(p: Placed):
    import skia
    path = skia.Path(svg_path(p.glyph))
    path.transform(world_matrix(p))
    return path


def faux_bold_wu(p: Placed) -> float:
    _, _, _, bold = db().metrics(p.glyph)
    return bold * 2 * p.size  # stroke growth per side, wu


def bounds(placed, margin=40):
    import skia
    r = None
    for p in placed:
        b = world_path(p).computeTightBounds()
        r = b if r is None else skia.Rect.MakeLTRB(min(r.left(), b.left()), min(r.top(), b.top()),
                                                   max(r.right(), b.right()), max(r.bottom(), b.bottom()))
    if r is None:
        return (-100, -100, 100, 100)
    return (r.left() - margin, r.top() - margin, r.right() + margin, r.bottom() + margin)


# The sign text is lit like a board (TMP "Distance Field (Surface)" shader): rough per-channel factors.
LIGHTS = {"front": ("#86aecb", (1, 1, 1)), "back": ("#d4e6f2", (0.34, 0.42, 0.55)),
          "dusk": ("#3a3150", (0.45, 0.33, 0.36)), "night": ("#0b121b", (0.1, 0.12, 0.17))}

TINTS = ["#e6194b", "#3cb44b", "#ffe119", "#4363d8", "#f58231", "#911eb4", "#46f0f0", "#f032e6", "#bcf60c",
         "#fabebe", "#008080", "#e6beff", "#9a6324", "#fffac8", "#800000", "#aaffc3"]


def render(placed, out_png, px=900, bg="#0f1a26", box=None, boards=False, cell_tint=False,
           conflicts=(), label=None, grid: Grid = DEFAULT_GRID, light=None):
    """Draw glyphs back to front (backdrop first, then cell layers) into a PNG. box = world (l,b,r,t)."""
    import skia
    l, b, r, t = box or bounds(placed)
    w, h = r - l, t - b
    k = px / max(w, h)
    W, H = int(w * k) + 1, int(h * k) + 1
    surf = skia.Surface(W, H)
    cv = surf.getCanvas()
    lk = (1, 1, 1)
    if light:
        bg, lk = LIGHTS[light]
    cv.clear(skia.Color(*parse_hex(bg)[:3]))
    view = skia.Matrix()
    view.setScale(k, -k)
    view.postTranslate(-l * k, t * k)
    if boards:
        pb = skia.Paint(Color=skia.Color(120, 90, 50, 160), AntiAlias=True)
        for name in grid.order:
            bx, by = grid.pos(name)
            rect = skia.Rect.MakeXYWH(bx - 20, by - 10, 40, 20)
            pr = skia.Path(); pr.addRect(rect); pr.transform(view)
            cv.drawPath(pr, pb)
    order = grid.order
    for p in sorted(placed, key=lambda q: (not q.backdrop, q.layer or 0, q.z)):
        f3 = (1, 1, 1) if p.font else lk      # unlit glyphs keep their colour in any light
        col = tuple(round(c * f) for c, f in zip(p.color[:3], f3)) + (p.color[3],)
        if cell_tint and p.cell:
            tc = parse_hex(TINTS[order.index(p.cell) % len(TINTS)] if p.cell in order else "#888")
            col = (tc[0], tc[1], tc[2], 150)
        # fill + faux-bold growth are one SDF pass in game: draw both opaque, composite once with alpha
        translucent = col[3] < 255
        if translucent:
            cv.saveLayerAlpha(None, col[3])
        paint = skia.Paint(Color=skia.Color(col[0], col[1], col[2], 255), AntiAlias=True)
        path = world_path(p)
        path.transform(view)
        cv.drawPath(path, paint)
        bw = faux_bold_wu(p) * k
        if bw > 0.3:
            sp = skia.Paint(Color=skia.Color(col[0], col[1], col[2], 255), AntiAlias=True,
                            Style=skia.Paint.kStroke_Style, StrokeWidth=2 * bw,
                            StrokeJoin=skia.Paint.kRound_Join)
            cv.drawPath(path, sp)
        if translucent:
            cv.restore()
    if conflicts:
        pc = skia.Paint(Color=skia.Color(255, 40, 40, 220), AntiAlias=True, Style=skia.Paint.kStroke_Style,
                        StrokeWidth=3)
        for (x, y) in conflicts:
            cv.drawCircle((x - l) * k, (t - y) * k, 10, pc)
    if label:
        font = skia.Font(None, 18)
        cv.drawString(label, 10, 26, font, skia.Paint(Color=skia.Color(230, 230, 230)))
    surf.makeImageSnapshot().save(out_png, skia.kPNG)
    return out_png


# ----------------------------------------------------------------------------- overlap + cells

def masks(placed, box, res=2.0, grow=2.0):
    """Boolean coverage per glyph on a common grid (res wu/px), grown by `grow` wu (AA + faux bold)."""
    import skia
    l, b, r, t = box
    W, H = int((r - l) / res) + 1, int((t - b) / res) + 1
    view = skia.Matrix()
    view.setScale(1 / res, -1 / res)
    view.postTranslate(-l / res, t / res)
    out = []
    for p in placed:
        surf = skia.Surface(W, H)
        cv = surf.getCanvas()
        cv.clear(skia.Color(0, 0, 0, 0))
        path = world_path(p)
        path.transform(view)
        paint = skia.Paint(Color=skia.Color(255, 255, 255, 255), AntiAlias=False)
        cv.drawPath(path, paint)
        sw = 2 * (grow + faux_bold_wu(p)) / res
        cv.drawPath(path, skia.Paint(Color=skia.Color(255, 255, 255, 255), Style=skia.Paint.kStroke_Style,
                                     StrokeWidth=sw))
        a = surf.makeImageSnapshot().toarray()[:, :, 3] > 0
        out.append(a)
    return out, (l, b, r, t, res)


def overlap_pairs(placed, res=2.0, grow=2.0):
    """Pairs (i, j, (x, y)) of glyphs whose drawn pixels touch, with one witness point in wu."""
    import numpy as np
    box = bounds(placed, margin=20)
    ms, (l, b, r, t, res) = masks(placed, box, res, grow)
    pairs = []
    for i in range(len(placed)):
        for j in range(i + 1, len(placed)):
            both = ms[i] & ms[j]
            if both.any():
                ys, xs = np.nonzero(both)
                k = len(xs) // 2
                pairs.append((i, j, (l + xs[k] * res, t - ys[k] * res)))
    return pairs


class CellError(Exception):
    pass


def assign_cells(placed, grid: Grid = DEFAULT_GRID, res=2.0, grow=2.0):
    """Typable layout (one glyph per sign): give every glyph a cell+layer.

    Different cells have no stable draw order (camera-dependent sorting), one cell does (its signs
    are stacked in depth, L1 at the back). So each connected group of touching glyphs (an island) must
    live in one cell, layered in z order from L1. Pre-set p.cell values are honoured and pull their
    whole island along. Backdrop glyphs only group with each other and go to backdrop cells.

    Glyphs that touch nothing are free to go anywhere, but layers are 9 cm apart in depth and a deeper
    sign's picture slides when seen from below or the side: a row of runes spread over several layers
    stops lining up (gallery 2026-10-08). So loose glyphs with the same tag (or all untagged ones) take
    ONE layer across the cells, the front-most layer with room for the whole set; only a set that fits
    no single layer is split over several.
    """
    cells = grid.order
    front_cells = [c for c in cells if not grid.cells[c][2]]
    back_cells = [c for c in cells if grid.cells[c][2]]
    if any(p.backdrop for p in placed) and not back_cells:
        raise CellError("backdrop glyphs but the grid has no backdrop cells")
    n = len(placed)
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    pairs = [(i, j, w) for i, j, w in overlap_pairs(placed, res, grow) if placed[i].backdrop == placed[j].backdrop]
    for i, j, _ in pairs:
        parent[find(i)] = find(j)
    groups = {}
    for i in range(n):
        groups.setdefault(find(i), []).append(i)
    groups = list(groups.values())
    loose = [g[0] for g in groups if len(g) == 1 and not placed[g[0]].cell and not placed[g[0]].backdrop]
    islands = [g for g in groups if not (len(g) == 1 and g[0] in loose)]
    load = {c: 0 for c in cells}
    plan = {}
    fixed = []
    for g in islands:
        pins = {placed[i].cell for i in g if placed[i].cell}
        if len(pins) > 1:
            raise CellError(f"touching glyphs pinned to different cells {sorted(pins)}: "
                            f"{[placed[i].tag or placed[i].glyph for i in g]}")
        if pins:
            fixed.append((g, pins.pop()))
    free = [g for g in islands if not any(placed[i].cell for i in g)]
    for g, c in fixed:
        plan[id(g)] = c
        load[c] += len(g)

    def cost(g, c):
        # the cell decides pos/voffset, hence the command length: prefer cells that keep every
        # command within the sign's 50-character limit, then the shortest commands
        over = total = 0
        for i in g:
            n = utf16_len(encode(to_cmd(replace(placed[i], cell=c), grid)))
            over += max(0, n - CHAR_LIMIT)
            total += n
        return over, total

    for g in sorted(free, key=len, reverse=True):
        pool = back_cells if placed[g[0]].backdrop else front_cells
        fits = [c for c in pool if load[c] + len(g) <= grid.capacity(c)]
        if not fits:
            raise CellError(f"group of {len(g)} touching glyphs does not fit "
                            f"(grid {grid.label()}, loads {load}): "
                            f"{[placed[i].tag or placed[i].glyph for i in g]}")
        cs = {c: cost(g, c) for c in fits}
        best = min(fits, key=lambda c: (cs[c][0], load[c], cs[c][1], cells.index(c)))
        plan[id(g)] = best
        load[best] += len(g)
    used = {c: set() for c in cells}
    for c in cells:
        members = sorted((i for g in islands if plan[id(g)] == c for i in g), key=lambda i: placed[i].z)
        if len(members) > grid.capacity(c):
            raise CellError(f"cell {c} has {len(members)} glyphs > capacity {grid.capacity(c)}")
        for k, i in enumerate(members):
            placed[i].cell, placed[i].layer = c, k + 1
            used[c].add(k + 1)
    # loose glyphs: one layer (depth plane) per tag set
    memo = {}

    def cost1(i, c):
        if (i, c) not in memo:
            memo[(i, c)] = cost([i], c)
        return memo[(i, c)]

    def match(todo, cands):
        """Cheapest (glyph, cell) pairs first; returns the pairs and the characters over the limit."""
        pairs = sorted(((cost1(i, c), k, cells.index(c), i, c) for k, i in enumerate(todo) for c in cands),
                       key=lambda t: (t[0][0], t[0][1], t[1], t[2]))
        got_i, got_c, out, over = set(), set(), [], 0
        for co, _, _, i, c in pairs:
            if i not in got_i and c not in got_c:
                got_i.add(i)
                got_c.add(c)
                out.append((i, c))
                over += co[0]
        return out, over
    sets = {}
    for i in sorted(loose, key=lambda i: placed[i].z):
        sets.setdefault(placed[i].tag, []).append(i)
    for tag, members in sorted(sets.items(), key=lambda kv: (-len(kv[1]), placed[kv[1][0]].z)):
        todo = list(members)
        while todo:
            room = {L: [c for c in front_cells if L not in used[c]] for L in range(grid.depth, 0, -1)}
            whole = [L for L in room if len(room[L]) >= len(todo)]
            if whole:     # the front-most layer that keeps every command within 50, else the least over
                trials = [(L,) + match(todo, room[L]) for L in whole]
                zero = next((t for t in trials if t[2] == 0), None)
                L, out, _ = zero or min(trials, key=lambda t: (t[2], -t[0]))
                if zero is None:      # a glyph over 50 characters would be cut off: let it leave the plane
                    out = [(i, c) for i, c in out if cost1(i, c)[0] == 0] or out
            else:
                L = max(room, key=lambda L: (len(room[L]), L))
                if not room[L]:
                    raise CellError(f"no free sign left for {[placed[i].tag or placed[i].glyph for i in todo]}")
                out, _ = match(todo[:len(room[L])], room[L])
            for i, c in out:
                placed[i].cell, placed[i].layer = c, L
                used[c].add(L)
            done = {i for i, _ in out}
            todo = [i for i in todo if i not in done]
    # repair: a loose glyph still over 50 moves to any free sign, or swaps with another loose glyph
    for i in sorted(loose, key=lambda i: placed[i].z):
        if cost1(i, placed[i].cell)[0] == 0:
            continue
        spot = next(((c, L) for L in range(grid.depth, 0, -1) for c in front_cells
                     if L not in used[c] and cost1(i, c)[0] == 0), None)
        if spot:
            used[placed[i].cell].discard(placed[i].layer)
            placed[i].cell, placed[i].layer = spot
            used[spot[0]].add(spot[1])
            continue
        for j in sorted(loose, key=lambda j: placed[j].z):
            if j != i and cost1(i, placed[j].cell)[0] == 0 and cost1(j, placed[i].cell)[0] == 0:
                (placed[i].cell, placed[i].layer), (placed[j].cell, placed[j].layer) = \
                    (placed[j].cell, placed[j].layer), (placed[i].cell, placed[i].layer)
                break
    return pairs


# ----------------------------------------------------------------------------- art authoring

class Art:
    """A sign-art piece designed on one global canvas. Add glyphs back to front."""

    def __init__(self, key, title, idea="", origin=None, bg="#0f1a26", capacity=None, grid: Grid | None = None,
                 unlit=False, mode="typable"):
        """mode "typable": one glyph per sign, every command <= 50 characters, anyone can copy-paste it
        in game (Art.build). mode "detailed": save edit (later a mod): many glyphs per sign, no
        character limit, a whole piece in one stack (build_planes)."""
        origin = origin or DEFAULT_ORIGIN
        if mode not in ("typable", "detailed"):
            raise ValueError(f"mode {mode!r}")
        self.mode = mode
        self.key, self.title, self.idea = key, title, idea
        self.ox, self.oy = origin
        self.bg = bg
        self.grid = grid or (replace(DEFAULT_GRID, depth=capacity) if capacity else DEFAULT_GRID)
        self.items: list[Placed] = []
        self.unlit = unlit

    def add(self, glyph, x, y, size=None, h=None, w=None, rot=0.0, sx=1.0, color="#fff", cell=None, tag="",
            mirror=False, backdrop=False, glow=None):
        """Glyph whose bbox center is at (x, y) relative to the art origin.

        Give the TMP size directly, or the wanted bbox height h / width w in wu (before rotation).
        A width and a height together set the horizontal <scale>. mirror=True flips the glyph
        horizontally with a negative <scale> (verified in game 2026-10-08; costs a <scale=-1> tag).
        backdrop=True sends the glyph to a backdrop cell of the grid (deep behind the build).
        glow=True/False overrides the art's `unlit` default for this glyph (unlit <font> alias).
        """
        gw, gh = glyph_box(glyph)
        if size is None:
            if h is not None:
                size = h / (2 * gh)
                if w is not None:
                    sx = w / (2 * gw * size)
            elif w is not None:
                size = w / (2 * gw * sx)
            else:
                raise ValueError("size, h or w needed")
        if mirror:
            sx = -abs(sx)
        col = parse_hex(color) if isinstance(color, str) else tuple(color)
        p = Placed(glyph, self.ox + x, self.oy + y, size, sx, rot, col, cell, z=len(self.items), tag=tag,
                   backdrop=backdrop, font=self._font(glow))
        self.items.append(p)
        return p

    def _font(self, glow):
        """unlit=True: the 5-char alias (needs a loader sign, fits 50 chars); unlit="path": the full
        Resources path (no loader, longer than 50: save-edit only)."""
        on = self.unlit if glow is None else glow
        if not on:
            return None
        return FONT_PATH if self.unlit == "path" else FONT_ALIAS

    def build(self):
        """Assign cells, quantize to commands, re-layout from the command strings (what you type is what you see)."""
        placed = [replace(p) for p in self.items]
        pairs = assign_cells(placed, self.grid)
        rows = []
        for p in placed:
            cmd = to_cmd(p, self.grid)
            text = encode(cmd)
            back = parse(text)
            q = layout(back, p.cell, self.grid)
            q.layer, q.z, q.tag = p.layer, p.z, p.tag
            rows.append({"cell": p.cell, "layer": p.layer, "text": text, "chars": utf16_len(text),
                         "warnings": back.warnings, "placed": q, "tag": p.tag, "z": p.z})
        order = self.grid.order
        rows.sort(key=lambda r: (order.index(r["cell"]), r["layer"]))
        return rows, pairs

    def fit_above(self, signs_above=3.0, factor=1.0, min_h=0.0, dx=0.0):
        """Scale the design by `factor` about its centre, then centre it over the build with its bottom
        `signs_above` sign heights above the top row of boards. min_h keeps small glyphs at least that
        tall (wu); dx shifts it sideways (wu). Returns self."""
        l, b, r, t = placed_bbox(self.items)
        cx, cy = (l + r) / 2, (b + t) / 2
        nx = self.grid.center_x + dx
        ny = self.grid.top_y + signs_above * SIGN_H + (t - b) * factor / 2
        def size(p):
            # keep tiny details visible: no glyph smaller than min_h wu (its centre stays put)
            s = p.size * factor
            gw, gh = glyph_box(p.glyph)
            return max(s, min_h / (2 * gh)) if min_h else s
        self.items = [replace(p, x=nx + (p.x - cx) * factor, y=ny + (p.y - cy) * factor, size=size(p))
                      for p in self.items]
        self.ox, self.oy = nx, ny
        return self

    def retarget(self, grid: Grid, key=None, title=None, idea=None, origin=None):
        """The same design on another build: a copy with a new grid (cells are reassigned on build)."""
        a = Art(key or self.key, title or self.title, idea or self.idea, origin=origin or (self.ox, self.oy),
                bg=self.bg, grid=grid, unlit=self.unlit, mode=self.mode)
        dx, dy = (origin[0] - self.ox, origin[1] - self.oy) if origin else (0, 0)
        a.items = [replace(p, cell=None, layer=None, x=p.x + dx, y=p.y + dy) for p in self.items]
        return a


def check_rows(rows, res=2.0):
    """Re-check the quantized result: cross-cell touches (glyph pairs) and per-command problems."""
    placed = [r["placed"] for r in rows]
    bad = [(i, j, w) for i, j, w in overlap_pairs(placed, res, grow=1.0)
           if placed[i].cell != placed[j].cell and placed[i].backdrop == placed[j].backdrop]
    probs = [f"{r['cell']}/L{r['layer']}: {w}" for r in rows for w in r["warnings"]]
    return bad, probs


# ----------------------------------------------------------------------------- multi-glyph signs

LINE_TAG = "<line-height=0>"
BREAK = "\n"            # a raw line feed in the save: 1 character (<br> = 4); verified in gallery 9
_LH = re.compile(r"<line-height=")


def parse_lines(text: str) -> list:
    return parse_sign(text)[0]


def parse_sign(text: str):
    """Parse a sign text into (one Cmd per line, line pitch L in local units).

    <br> or a line feed ends a line. Tags carry over to the next line as in TMP (color, size, voffset,
    rotate, scale, font, indent); <pos> does not: a line's pen starts at its own <pos> if it has one, else
    at the current <indent> (it persists over lines: TMP_Text.cs:6281, TextMeshProUGUI.cs:2577). One
    <line-height=L> for the whole text (plain numbers only). A text without <line-height=...> is one line
    (parse())."""
    if not _LH.search(text):
        return [parse(text)], 0.0
    state = {"color": DEFAULT_COLOR, "size": 1.0, "voffset": 0.0, "rotate": 0.0, "scale": 1.0, "font": None,
             "indent": 0.0}
    lh = None
    out, cur, i = [], None, 0
    body = text.replace("\n", "<br>")      # line feeds and <br> both end a line

    def flush():
        if cur is not None:
            out.append(cur)
    while i < len(body):
        m = _TAG.match(body, i)
        if m:
            t = m.group(1)
            i = m.end()
            if t == "br":
                flush()
                cur = None
                continue
            if t.startswith("line-height="):
                v = float(t.split("=", 1)[1])          # units (%, em) are not modelled: ValueError
                if lh is not None and v != lh:
                    raise ValueError("parse_sign: one <line-height> per text is modelled")
                lh = v
                continue
            if t.startswith("#"):
                state["color"] = parse_hex(t)
            elif t.startswith("font="):
                state["font"] = t[5:].strip('"')
            elif t == "/font":
                state["font"] = None
            elif "=" in t and t.split("=")[0] in ("size", "pos", "voffset", "rotate", "scale", "indent"):
                k, v = t.split("=", 1)
                if k == "pos":
                    state["_pos"] = float(v)
                else:
                    state[k] = float(v)
            continue
        ch = body[i]
        i += 1
        if ch in "️︎":
            continue
        if cur is not None:
            cur.warnings.append("two glyphs on one line: line wrapping is not modeled")
            continue
        cur = Cmd(ch, size=state["size"], pos=state.pop("_pos", state["indent"]), voffset=state["voffset"], rotate=state["rotate"],
                  scale=state["scale"], color=state["color"], font=state["font"], raw=text)
    flush()
    return out, lh or 0.0


def layout_lines(cmds: list, cell: str, grid: Grid = DEFAULT_GRID, layer=None, lh: float = 0.0) -> list:
    """Where every line's glyph lands in a multi-line sign (TextMeshProUGUI.cs, decompiled).

    With <line-height=0> all lines share baseline 0 (m_lineOffset never grows, :2571); with L > 0 line i
    sits i*L local units lower (:2571, 1 local = 2 wu), which also lowers the last line's descender in the
    centring below: K gains -(N-1)*L (verified with gallery 9's column encoding). Each line is centred
    on its own (its maxAdvance, as for one glyph). Middle alignment centres the block on
    (m_maxTextAscender + maxVisibleDescender) / 2 (:2696), where the ascender is the FIRST line's (it is
    only taken while m_lineNumber == 0, :1978) and the descender the LAST line's (overwritten at every
    line end, :2512). With voffset V a line keeps both its shifted and unshifted extent (:1958), so
      Y_i = 2*V_i - 2*i*L + S_i*(yMin+yMax)_i - K,   K = S_0*A_0 + S_L*D_L + max(V_0, 0) + min(V_L, 0) - (N-1)*L
    One line gives back layout(). Same size, font and voffset on every line: each line is exactly a
    one-glyph sign (verified in game with two glyphs); different voffsets are unverified."""
    if len(cmds) == 1:
        return [layout(cmds[0], cell, grid, layer)]
    gx, gy = grid.pos(cell, layer)
    bg = grid.cells[cell][2]
    first, last = cmds[0], cmds[-1]
    _, A0, _, _ = db().metrics(first.glyph)
    _, _, DL, _ = db().metrics(last.glyph)
    K = first.size * A0 + last.size * DL + max(first.voffset, 0) + min(last.voffset, 0) - (len(cmds) - 1) * lh
    out = []
    for i, c in enumerate(cmds):
        g, _, _, _ = db().metrics(c.glyph)
        x0, y0, x1, y1 = g["bb"]
        dx = 2 * c.scale * c.size * ((x0 + x1) / 2 - g["adv"] / 2)
        out.append(Placed(c.glyph, c.pos + dx + gx, 2 * c.voffset - 2 * i * lh + c.size * (y0 + y1) - K + gy,
                          c.size, c.scale, c.rotate, c.color, cell, backdrop=bg, font=c.font))
    return out


def layout_sign(text: str, cell: str, grid: Grid = DEFAULT_GRID, layer=None) -> list:
    cmds, lh = parse_sign(text)
    return layout_lines(cmds, cell, grid, layer, lh)


def order_lines(items: list, grid: Grid = DEFAULT_GRID) -> list:
    """Line order for one sign: the glyph needing the most voffset first, the next one last (both must
    have voffset >= 0 for line_cmds); the order between glyphs that never overlap does not matter."""
    if len(items) < 3:
        return sorted(items, key=lambda p: -p.y)
    gy = grid.pos(items[0].cell, items[0].layer)[1]
    need = sorted(items, key=lambda p: -(p.y - gy - p.size * sum(db().get(p.glyph)["bb"][1::2])))
    return [need[0]] + need[2:] + [need[1]]


def line_cmds(items: list, grid: Grid = DEFAULT_GRID, size_nd=2, nd=1, strict=False, lh: float = 0.0,
              exact_single=False) -> list:
    """Inverse of layout_lines: commands that put the glyphs `items` (same cell, in line order) where
    they say. Sizes keep `size_nd` decimals, pos/voffset `nd` (no 50-character budget in a save edit). A lone
    glyph gets a whole-number voffset (typable art), or `nd` decimals with exact_single (a pixel band)."""
    cell = items[0].cell
    gx, gy = grid.pos(cell, items[0].layer)
    cmds = []
    for p in items:
        S, sx = round(p.size, size_nd), round(p.scale, 2)
        glyph, rot = rotation_twin(p.glyph, round(p.rotate), sx)
        rot = (rot + 89) % 180 - 89 if _symmetric_180(glyph) else (rot + 180) % 360 - 180
        dx, _ = center_offset(glyph, S, sx)
        cmds.append(Cmd(glyph, size=S, pos=round(p.x - dx - gx, nd), rotate=rot, scale=sx, color=p.color, font=p.font))
    if len(cmds) == 1:
        _, dy = center_offset(cmds[0].glyph, cmds[0].size, cmds[0].scale)
        cmds[0].voffset = round(items[0].y - dy - gy, nd if exact_single else None)
        return cmds
    first, last = cmds[0], cmds[-1]
    _, A0, _, _ = db().metrics(first.glyph)
    _, _, DL, _ = db().metrics(last.glyph)
    base = first.size * A0 + last.size * DL
    e = [c.size * sum(db().get(c.glyph)["bb"][1::2]) for c in cmds]
    T = [p.y - gy for p in items]
    N = len(cmds)
    # V_i = W_i + K/2 (layout_lines solved for V); K = base + max(V_0, 0) + min(V_L, 0)
    W = [(T[i] - e[i] + 2 * i * lh - (N - 1) * lh) / 2 for i in range(N)]
    # regime V_0 >= 0 (and V_L >= 0): K = base + V_0, so V_0 = 2*W_0 + base
    K = base + 2 * W[0] + base
    vs = [w + K / 2 for w in W]
    if (vs[0] < 0 or vs[-1] < 0) and strict:
        raise ValueError("line_cmds: this line order needs a negative voffset on the first or last line")
    if vs[0] < 0 or vs[-1] < 0:
        # regime V_0 < 0, V_L >= 0: K = base, every line on its own (verified: gallery 9 columns)
        vs = [w + base / 2 for w in W]
        if vs[0] >= 0 or vs[-1] < 0:
            # regime V_0 < 0, V_L < 0 (glyphs below the sign): K = base + V_L, so V_L = 2*W_L + base (unverified)
            K = base + 2 * W[-1] + base
            vs = [w + K / 2 for w in W]
            if vs[0] >= 0 or vs[-1] >= 0:
                raise ValueError("line_cmds: put a line with voffset >= 0 last (mixed-sign voffsets are not solvable)")
    step, prev = 0.5 * 10 ** -nd, None
    for c, v in zip(cmds, vs):
        # keep the previous value while it is within rounding: one voffset per column, no float jitter
        c.voffset = prev if prev is not None and abs(v - prev) < step else round(v, nd)
        prev = c.voffset
    return cmds


def _tmp_float(s: str) -> float:
    """A tag number as TMP reads it (TMP_Text.ConvertToFloat): float32, the n-th decimal as digit * 0.1f^n."""
    import numpy as np
    f = np.float32
    num, mul, frac = f(0), f(0), False
    for ch in s.lstrip("+-"):
        if ch == ".":
            frac, mul = True, f(0.1)
            continue
        d = f(ord(ch) - 48)
        if frac:
            num, mul = f(num + f(d * mul)), f(mul * f(0.1))
        else:
            num = f(num * f(10) + d)
    return -float(num) if s.startswith("-") else float(num)


def exact_line_height(step: float) -> float:
    """The line pitch TMP adds at every line break in float32 (m_lineOffset, TextMeshProUGUI.cs:2571) must
    be read exactly and add up exactly over tens of thousands of lines. Otherwise the columns drift: 0.8 is
    off by up to 19 local units after 49 000 lines, which kinked the Starry Night (2026-10-08). Multiples
    of 1/4 are exact. A step that is itself an exactly read multiple of 1/8 or 1/16 is kept as it is
    (fine pictures: 0.1875 for 0.375 wu pixels); any other step snaps to the nearest multiple of 1/4, and
    the voffsets absorb the difference."""
    for q in (4, 8, 16):
        lh = round(step * q) / q
        if lh > 0 and abs(lh - step) < 1e-6 and _tmp_float(_num(lh, 6)) == lh:
            return lh
    for q in (4, 2, 1):
        lh = max(1.0 / q, round(step * q) / q)
        if _tmp_float(_num(lh, 6)) == lh:
            return lh
    return float(max(1, round(step)))


def column_cmds(items: list, grid: Grid = DEFAULT_GRID, nd=1, default_step=None):
    """Glyphs whose order does not matter (nothing touches): column-major lines with one line pitch, so
    <indent> and <voffset> change once per column and a pixel costs its glyph, a line feed and its colour
    when it changes (gallery 9: 8.1 instead of 21 characters per pixel). -> (cmds, L) or (None, 0).
    default_step: the line pitch when no column has two glyphs above each other (a transparent picture's
    sparse band; pixels.column_text gives half the pixel pitch); None: no encoding then."""
    order = sorted(items, key=lambda p: (round(p.x, nd), -p.y))
    need = [p.y - p.size * sum(db().get(p.glyph)["bb"][1::2]) for p in order]
    steps = Counter(round((need[i] - need[i + 1]) / 2, 6) for i in range(len(order) - 1)
                    if round(order[i].x, nd) == round(order[i + 1].x, nd) and need[i] > need[i + 1] + 1e-6)
    if not steps and default_step is None:
        return None, 0.0
    lh = exact_line_height(steps.most_common(1)[0][0] if steps else default_step)
    try:
        cmds = line_cmds(order, grid, nd=nd, lh=lh, exact_single=True)
    except ValueError:
        return None, 0.0
    if max(abs(c.voffset) for c in cmds) > 32000 or max(abs(c.pos) for c in cmds) > 32000:
        return None, 0.0                  # tag values above 32767 are invalid (TMP_Text.cs:5270)
    return cmds, lh


def encode_lines(cmds: list, lh: float = 0.0, nd: int = 1, single: bool = True) -> str:
    """One sign text for several glyphs: <line-height=L> + one line per glyph, separated by line feeds.
    Only changed tags are repeated: TMP keeps color/size/voffset/rotate/scale/indent across lines, and
    the pen of every line starts at the current <indent> (used instead of a <pos> per line). nd: decimals of
    voffset and indent (2 for pixels finer than the 0.1 wu grid; they change once per column, so it is cheap).
    single=False: one glyph is written the same way (a pixel picture's band of one pixel), not as a typable command
    (encode: whole numbers, no line height)."""
    if len(cmds) == 1 and single:
        return encode(cmds[0])
    out = [f"<line-height={_num(lh, 6)}>" if lh else LINE_TAG]
    st = {"color": DEFAULT_COLOR, "size": None, "voffset": 0, "rotate": 0, "scale": 1.0, "font": None,
          "indent": 0.0}
    for i, c in enumerate(cmds):
        if i:
            out.append(BREAK)
        if c.font != st["font"]:          # unlit and lit glyphs may share a sign (they never overlap there)
            out.append("</font>" if c.font is None else ("</font>" if st["font"] else "") + f"<font={c.font}>")
            st["font"] = c.font
        if tuple(c.color) != st["color"]:
            out.append(f"<{short_hex(c.color)}>")
            st["color"] = tuple(c.color)
        if c.size != st["size"]:
            out.append(f"<size={_num(c.size, 2)}>")
            st["size"] = c.size
        if round(c.voffset, nd) != st["voffset"]:
            out.append(f"<voffset={_num(c.voffset, nd)}>")
            st["voffset"] = round(c.voffset, nd)
        if round(c.pos, nd) != st["indent"]:
            out.append(f"<indent={_num(c.pos, nd)}>")
            st["indent"] = round(c.pos, nd)
        if round(c.rotate) != st["rotate"]:
            out.append(f"<rotate={_num(c.rotate)}>")
            st["rotate"] = round(c.rotate)
        if abs(c.scale - st["scale"]) > 0.005:
            out.append(f"<scale={_num(c.scale, 2)}>")
            st["scale"] = c.scale
        out.append(c.glyph)
    return "".join(out)


def touch_pairs(placed, res=1.0, grow=1.0):
    """overlap_pairs for many glyphs: bounding boxes first, pixels only where two boxes meet."""
    boxes = []
    for p in placed:
        r = world_path(p).computeTightBounds()
        g = grow + faux_bold_wu(p) + res
        boxes.append((r.left() - g, r.top() - g, r.right() + g, r.bottom() + g))     # l, b, r, t (y up)
    order = sorted(range(len(placed)), key=lambda i: boxes[i][0])
    pairs = []
    for k, i in enumerate(order):
        bi = boxes[i]
        for j in order[k + 1:]:
            bj = boxes[j]
            if bj[0] > bi[2]:
                break
            if bj[1] > bi[3] or bj[3] < bi[1]:
                continue
            box = (max(bi[0], bj[0]), max(bi[1], bj[1]), min(bi[2], bj[2]), min(bi[3], bj[3]))
            (mi, mj), (l, b, r, t, rs) = masks([placed[i], placed[j]], box, res, grow)
            both = mi & mj
            if both.any():
                ys, xs = both.nonzero()
                a, c = (i, j) if i < j else (j, i)
                pairs.append((a, c, (l + xs[len(xs) // 2] * rs, t - ys[len(ys) // 2] * rs)))
    return pairs


def submesh(p: Placed):
    """What TMP draws as one mesh inside a sign: the <font> material and the font asset of the glyph."""
    return p.font or "", db().get(p.glyph)["f"]


def assign_planes(placed, grid: Grid = DEFAULT_GRID, main=None, res=1.0, grow=1.0, loose_per_sign=100000,
                  independent=False):
    """Save-edit layout with several glyphs per sign (layout_lines). Sets p.cell / p.layer in place.

    Every glyph that touches another goes into ONE cell (`main`, default the middle one); its layer is
    its paint level. Inside one sign, glyphs of one font sub-mesh (same <font> material and same font
    asset of the fallback chain) are drawn in character order, so touching glyphs of one sub-mesh may
    share a level (their lines keep the z order); a glyph only needs a deeper level than an earlier,
    touching glyph of ANOTHER sub-mesh (sub-mesh order inside a sign is not relied on). Loose glyphs
    (touching nothing) go to layer 1, the same depth plane, spread over the cells. So a whole piece needs one stack of
    `depth` signs, and nothing that should line up sits on another plane: layers are 9 cm apart, and
    seen from below a deeper layer slides down (the gallery's pixel ship broke into bands this way).

    independent=True (mosaics): skip the touch test, every glyph is loose. For tiles that overlap their
    neighbours by a hair so no seam shows: the overlap strip may change owner with the camera, unseen."""
    front = [c for c in grid.order if not grid.cells[c][2]]
    flat = front
    if all(grid.depth_of(c, 1) is not None for c in front):
        # a measured build is not perfectly flat: use the signs whose depth is the most common one
        bins = {}
        for c in front:
            bins.setdefault(round(grid.depth_of(c, 1) / 0.5), []).append(c)
        flat = max(bins.values(), key=len)
    if main is None:
        cx = grid.center_x
        main = min(flat, key=lambda c: (abs(grid.pos(c)[0] - cx), abs(grid.pos(c)[1] + 40)))
    touch = {i: set() for i in range(len(placed))}
    for i, j, _ in ([] if independent else touch_pairs(placed, res, grow)):
        touch[i].add(j)
        touch[j].add(i)
    level = {}
    for i in sorted(range(len(placed)), key=lambda i: placed[i].z):
        level[i] = max((level[j] + (submesh(placed[j]) != submesh(placed[i])) for j in touch[i] if j in level),
                       default=1)
    top = max(level.values(), default=0)
    if top > grid.depth:
        raise CellError(f"{top} paint levels > depth {grid.depth}")
    loose = [i for i in sorted(range(len(placed)), key=lambda i: placed[i].z) if not touch[i]]
    for i in range(len(placed)):
        if touch[i]:
            placed[i].cell, placed[i].layer = main, level[i]
    others = [main] + [c for c in flat if c != main]        # loose glyphs: layer 1, the common depth
    n_signs = max(1, min(len(others), -(-len(loose) // loose_per_sign)))
    cuts = [round(k * len(loose) / n_signs) for k in range(n_signs + 1)]
    for k in range(1, n_signs):            # move each cut to the nearest row end (a change of y)
        ends = [j for j in range(cuts[k - 1] + 1, cuts[k + 1]) if abs(placed[loose[j]].y - placed[loose[j - 1]].y) > 1e-6]
        if ends:
            cuts[k] = min(ends, key=lambda j: abs(j - cuts[k]))
    for k in range(n_signs):
        for i in loose[cuts[k]:cuts[k + 1]]:
            placed[i].cell, placed[i].layer = others[k], 1
    return main, top


def build_planes(art, **kw):
    """Art -> sign rows (cell, layer, text, placed) with assign_planes; re-laid out from the texts."""
    placed = [replace(p) for p in art.items]
    assign_planes(placed, art.grid, **{**getattr(art, "plane_kw", {}), **kw})
    groups = {}
    for p in placed:
        groups.setdefault((p.cell, p.layer), []).append(p)
    rows = []
    free_all = getattr(art, "plane_kw", {}).get("independent", False)
    anchored = getattr(art, "anchors", False) and len(groups) > 1
    if anchored:
        # Unity draws the signs of a stack in the order of their renderers' bounds, not of the signs: a layer whose
        # glyphs sit nearer the camera drew over the layers in front of it (the cauldron's tripod over its pot on a
        # tilted sign, 0.20.145). Two invisible, tiny glyphs per sub-mesh at the same two corners outside the whole
        # piece give every renderer of every layer the same bounds, so the stack's own spacing decides again.
        l, b, r, t = placed_bbox(placed)
        m = max(4.0, 0.2 * max(2 * p.size for p in placed))
        top, bottom = (r + m, t + m), (l - m, b - m)
    for (cell, layer), items in groups.items():
        items = sorted(items, key=lambda p: p.z)            # touching glyphs of one sub-mesh: z order
        n_anchor = 0
        if anchored:
            reps = {}
            for p in items:
                reps.setdefault(submesh(p), p)
            def anchor(rep, at):
                return Placed(rep.glyph, at[0], at[1], 0.01, 1.0, 0.0, (0, 0, 0, 0), cell, layer, z=-1, font=rep.font)
            items = [anchor(q, top) for q in reps.values()] + items + [anchor(q, bottom) for q in reps.values()]
            n_anchor = len(reps)
        cmds, lh = None, 0.0
        if len(items) > 2 and not n_anchor and (free_all or not touch_pairs(items, 1.0, 1.0)):
            cmds, lh = column_cmds(items, art.grid)         # nothing touches: column-major, one line pitch
            if cmds is not None:
                items = sorted(items, key=lambda p: (round(p.x, 1), -p.y))
        if cmds is None:
            try:
                cmds = line_cmds(items, art.grid, strict=True)
            except ValueError:
                if len(items) > 1 and touch_pairs(items, 1.0, 1.0):
                    raise
                items = order_lines(items, art.grid)        # nothing touches: any order will do
                cmds = line_cmds(items, art.grid)
        text = encode_lines(cmds, lh)
        back = layout_sign(text, cell, art.grid, layer)
        for k, q in enumerate(back):
            q.layer, q.z = layer, k
        err = max(math.hypot(q.x - p.x, q.y - p.y) for q, p in zip(back, items))
        rows.append({"cell": cell, "layer": layer, "text": text, "chars": utf16_len(text), "glyphs": len(items),
                     "placed": back, "err": err, "warnings": [f"{err:.1f} wu off"] if err > 1.0 else []})
    order = art.grid.order
    rows.sort(key=lambda r: (order.index(r["cell"]), r["layer"]))
    return rows
