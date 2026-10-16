"""Helpers for concept authoring: palette (#rgb so color tags stay 6 chars) and placement helpers."""
import math

# 3-digit hex keeps every color tag at 6 characters of the 50-character budget.
BONE, WHITE, GOLD, AMBER, CYAN, TEAL, NAVY, INK = "#eed", "#fff", "#da4", "#c83", "#6de", "#299", "#236", "#123"
RED, BLOOD, STEEL, WOOD, PINE, MOSS = "#c33", "#a22", "#99a", "#753", "#264", "#486"
DARK = "#1e1a12"  # the sign's own text color: costs no color tag at all


def polar(r, deg):
    a = math.radians(deg)
    return r * math.cos(a), r * math.sin(a)


def ring_of(art, chars, r, start=90.0, end=None, h=40, color=BONE, outward=True, cx=0.0, cy=0.0, tag="ring"):
    """Glyphs spread clockwise on a circle (start/end in degrees, 0 = right, 90 = up)."""
    n = len(chars)
    if end is None:
        step = 360.0 / n
    else:
        step = (start - end) / max(n - 1, 1)
    out = []
    for i, ch in enumerate(chars):
        ang = start - i * step
        x, y = polar(r, ang)
        rot = ((ang - 90) + 180) % 360 - 180 if outward else 0
        if abs(rot) > 90:          # bottom half: turn the glyph upright (readable, shorter rotate tag)
            rot = (rot + 360) % 360 - 180
        out.append(art.add(ch, cx + x, cy + y, h=h, rot=rot, color=color, tag=tag))
    return out


def stroke(art, x0, y0, x1, y1, color=BONE, glyph="|", tag="stroke"):
    """A carved straight stroke from (x0,y0) to (x1,y1) using a tall thin glyph (Norse '|': 9:1)."""
    L = math.hypot(x1 - x0, y1 - y0)
    ang = math.degrees(math.atan2(y1 - y0, x1 - x0)) - 90
    return art.add(glyph, (x0 + x1) / 2, (y0 + y1) / 2, h=L, rot=ang, color=color, tag=tag)


def row(art, chars, x0, x1, y, h, color, tag="row"):
    n = len(chars)
    return [art.add(ch, x0 + (x1 - x0) * i / max(n - 1, 1), y, h=h, color=color, tag=tag) for i, ch in enumerate(chars)]
