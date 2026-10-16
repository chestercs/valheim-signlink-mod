"""The art library's building blocks: pieces for SignLink's Art tab, authored in concepts/lib_*.py.

A lib module has one function, pieces() -> [Piece]. presets.py turns every piece into the files the mod embeds
(mod/SignLink/Art), and art_preview.py renders the same texts for review, so what is reviewed is what ships.

Two kinds:
  glyph  an engine.Art (Unicode glyph art, detailed mode): one sign and copies stacked on it, a still.
         frames=[Art, ...] makes it animated; every frame must then fit one sign (one paint level).
  pixel  colour grids (rows of '#rgb', '' = empty): one grid is a still, several with fps an animation.

Pixel sprites share one scale, PITCH (2.25 wu = 5.6 cm a pixel, the cat's), so the animals stand side by side
at their true sizes; draw them with Canvas (numpy masks, like concepts/anim2.py) or from ASCII rows (ascii()).
"""
from dataclasses import dataclass, field

import numpy as np

import pixels as P

PITCH = 2.25                    # wu a pixel for sprites: the cat's (30 x 26 px = 1.7 m)
ABOVE = 40.0                    # the art's bottom 1 m above the sign
OUTLINE = "#213"                # the cat's outline
MAX_ANIM_KB = 80                # an animation's file (all frames); the cat is 23 KB
MAX_STILL_KB = 20               # a still's text; the cat is 3 KB
MAX_SIDE = 64                   # pixels, the larger side of a sprite
# a saved animation SignLink plays: at most this many ms of every second to build its frames (StoredAnims.MaxMsPerSecond,
# 100, less a margin), at SaveCost.GlyphSeconds a glyph; over it the game shows its first frame only (the portal swirl
# and the windmill never moved, 0.20.167)
MAX_PLAY_MS = 95.0
GLYPH_MS = 0.0135


def glyphs(text):
    """Glyphs a sign text builds, as SignLink counts them (SaveCost.Glyphs): characters past the space outside tags,
    a sprite tag as one."""
    n, i = 0, 0
    while i < len(text):
        c = text[i]
        if c == "<":
            end = min([k for k in (text.find("<", i + 1), text.find(">", i + 1)) if k > 0], default=-1)
            if end > 0 and text[end] == ">":
                if end - i > 6 and text[i + 1:i + 7].lower() == "sprite":
                    n += 1
                i = end + 1
                continue
        if c > " ":                                     # a code point: C# counts a surrogate pair once too
            n += 1
        i += 1
    return n


def play_ms(frames, fps):
    """ms a second an animation's frames take to build as it plays (StoredAnims.Judge)."""
    return sum(glyphs(f) for f in frames) * GLYPH_MS * fps / max(1, len(frames))


@dataclass
class Piece:
    name: str                   # the window's label; "Dog (moving)" groups under "Dog"
    kind: str                   # "glyph" or "pixel"
    art: object = None          # glyph: an engine.Art (a still)
    frames: list = field(default_factory=list)   # pixel: colour grids; glyph: engine.Art frames
    fps: float = 0.0            # > 0 with several frames: an animation
    pitch: float = PITCH        # pixel: wu a pixel
    size_m: float = 2.4         # glyph: the larger side in metres

    @property
    def animated(self):
        return self.fps > 0 and len(self.frames) > 1


# ---- sprites -------------------------------------------------------------------------------------------------

class Canvas:
    """A W x H grid of colour keys ('' = empty) drawn with masks; colours() maps the keys through a palette."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.g = np.full((h, w), "", dtype=object)
        self.yy, self.xx = np.mgrid[0:h, 0:w]
        self.px, self.py = self.xx + 0.5, self.yy + 0.5

    def ellipse(self, cx, cy, rx, ry):
        return ((self.px - cx) / rx) ** 2 + ((self.py - cy) / ry) ** 2 <= 1

    def disc(self, cx, cy, r):
        return self.ellipse(cx, cy, r, r)

    def rect(self, x0, y0, x1, y1):
        """Cells x0..x1-1, y0..y1-1."""
        return (self.xx >= x0) & (self.xx < x1) & (self.yy >= y0) & (self.yy < y1)

    def triangle(self, a, b, c):
        def side(p, q):
            return (q[0] - p[0]) * (self.py - p[1]) - (q[1] - p[1]) * (self.px - p[0])
        s1, s2, s3 = side(a, b), side(b, c), side(c, a)
        return ((s1 >= 0) & (s2 >= 0) & (s3 >= 0)) | ((s1 <= 0) & (s2 <= 0) & (s3 <= 0))

    def line(self, x0, y0, x1, y1, width=1.0):
        """A thick segment between two points."""
        dx, dy = x1 - x0, y1 - y0
        L2 = max(dx * dx + dy * dy, 1e-9)
        t = np.clip(((self.px - x0) * dx + (self.py - y0) * dy) / L2, 0, 1)
        return (self.px - x0 - t * dx) ** 2 + (self.py - y0 - t * dy) ** 2 <= (width / 2) ** 2

    def paint(self, mask, key):
        self.g[mask] = key
        return self

    def set(self, x, y, key):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.g[y, x] = key
        return self


def ascii(rows, legend):
    """Rows of characters -> a key grid: '.' and ' ' are empty, every other character is its own key (map it with
    the palette in colours()). All rows the same length."""
    w = len(rows[0])
    if any(len(r) != w for r in rows):
        raise ValueError("ascii rows differ in length")
    g = np.full((len(rows), w), "", dtype=object)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch not in ". ":
                if ch not in legend:
                    raise ValueError(f"no colour for {ch!r}")
                g[y, x] = ch
    return g


def outline(g, key="K"):
    """Pad by 1 and turn every empty pixel next to the drawing (4 neighbours) into the outline key."""
    g = np.pad(np.asarray(g, dtype=object), 1, constant_values="")
    filled = g != ""
    pad = np.pad(filled, 1)
    near = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    g[~filled & near] = key
    return g


def flip(g):
    """Mirrored left to right."""
    return np.asarray(g, dtype=object)[:, ::-1]


def colours(g, palette):
    """Key grid -> rows of '#rgb' ('' stays empty). Palette values are 3-digit '#rgb' (6-character colour tags)."""
    return [[palette[k] if k else "" for k in row] for row in np.asarray(g, dtype=object)]


# ---- texts ---------------------------------------------------------------------------------------------------

def check(piece):
    """Problems that would stop a piece from shipping, as strings."""
    out = []
    if piece.kind == "pixel":
        if not piece.frames:
            return ["no frames"]
        sizes = {(len(f), len(f[0])) for f in piece.frames}
        if len(sizes) > 1:
            out.append(f"frames differ in size {sorted(sizes)}: the sprite would jump")
        h, w = next(iter(sizes))
        if max(h, w) > MAX_SIDE:
            out.append(f"{w} x {h} px: at most {MAX_SIDE} on the larger side")
        for f in piece.frames:
            for row in f:
                for c in row:
                    if c and (len(c) != 4 or c[0] != "#"):
                        out.append(f"colour {c!r} is not '#rgb'")
                        return out
        if len(piece.frames) > 1 and piece.fps <= 0:
            out.append("several frames but no fps")
    elif piece.kind == "glyph":
        if piece.art is None and not piece.frames:
            out.append("no art")
    else:
        out.append(f"kind {piece.kind!r}")
    return out


def texts(piece):
    """-> (file name suffix, the file's text, [sign texts of the first frame or still], [frame texts or None]).
    The same texts presets.py writes and art_preview.py renders."""
    import presets
    if piece.kind == "pixel":
        frames = P.sign_frames(piece.frames, piece.pitch, ABOVE)
        if piece.animated:
            return ".anim.txt", f"#signlink anim fps={piece.fps:g}\n" + presets.FS.join(frames), [frames[0]], frames
        return ".txt", frames[0], [frames[0]], None
    if piece.frames:                                    # an animated glyph piece: one sign a frame
        frames = []
        for art in piece.frames:
            t, _, _ = presets.one_sign_stack(art, size_m=piece.size_m)
            if len(t) != 1:
                raise ValueError(f"{piece.name}: a frame needs {len(t)} signs; an animated glyph piece fits one")
            frames.append(t[0])
        return ".anim.txt", f"#signlink anim fps={piece.fps:g}\n" + presets.FS.join(frames), [frames[0]], frames
    t, _, _ = presets.one_sign_stack(piece.art, size_m=piece.size_m)
    if len(t) == 1:
        return ".txt", t[0], t, None
    return ".stack.txt", presets.GS.join(t), t, None
