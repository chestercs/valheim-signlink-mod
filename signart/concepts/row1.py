"""Row 1 (2026-10-08): one piece per block of the six-block row (row.py). Save-edit only: full <font>
paths and multi-glyph signs (<line-height=0>, engine.layout_lines), laid out by engine.assign_planes:
a whole piece in one stack of five signs. Blocks are numbered 1-6 from the viewer's left.

v2 after the first in-game look: the backdrop sign 2.2 m behind the marble stayed behind the logo but
slid away from it by parallax; the pixel ship split into bands because its rows sat on five layers
9 cm apart. Now nothing that must line up leaves its plane."""
from engine import LOADER_TEXT, Art, Grid, glyph_box, parse_hex
from concepts.common import ring_of
from concepts.round1 import fenrir_moon, mjolnir
from concepts.round2 import eikthyr
from concepts.round7 import glow_aurora


def copy_art(base, key, title, idea, grid, unlit=False):
    a = Art(key, title, idea, grid=grid, unlit=unlit, bg=base.bg, mode="detailed")
    for p in base.items:
        glow = (p.font is not None) if base.unlit else None
        a.add(p.glyph, p.x - base.ox, p.y - base.oy, size=p.size, sx=p.scale, rot=p.rotate, color=p.color,
              tag=p.tag, backdrop=p.backdrop, glow=glow)
    return a


def slots(grid: Grid):
    return [(c, layer) for c in grid.order if not grid.cells[c][2] for layer in range(1, grid.depth + 1)]


def fenrir(grid):
    a = copy_art(fenrir_moon(), "g1_fenrir", "1 · Fenrir és a hold (megvilágított)",
                 "A legjobb nappali darab kicsiben: a forgatás után délben hátulról süt a nap, így szemből "
                 "megvilágítva látszik.", grid)
    return {"art": a.fit_above(signs_above=2, factor=0.5)}


def eikthyr_glow(grid):
    a = copy_art(eikthyr(), "g2_eikthyr_glow", "2 · Világító Eikthyr",
                 "Eikthyr minden része világít (teljes <font> útvonal, nincs betöltő sign): éjjel kék fényben ragyog.",
                 grid, unlit="path")
    return {"art": a.fit_above(signs_above=2, factor=0.48)}


def mjolnir_backdrop(grid):
    """The round translucent background is paint level 1 of the same stack (9 cm behind the ring, not 2.2 m)."""
    base = mjolnir()
    a = Art("g3_mjolnir_backdrop", "3 · Mjölnir áttetsző háttérkoronggal",
            "A korong most ugyanabban a stackben van, a leghátsó rétegen (9 cm-rel a gyűrű mögött): "
            "nem csúszik el oldalról nézve sem.", grid=grid, bg=base.bg, mode="detailed")
    a.add("●", 0, 20, h=560, color="#0b1a2ec0", tag="disc")
    for p in base.items:
        a.add(p.glyph, p.x - base.ox, p.y - base.oy, size=p.size, sx=p.scale, rot=p.rotate, color=p.color, tag=p.tag)
    return {"art": a.fit_above(signs_above=2, factor=0.5)}


# 40 x 28 pixel picture: longship in front of the setting sun. One sign = one pixel row (same size and
# voffset on every line: exactly the two-glyph case verified in game), 40 glyphs per sign.
SKY = [(0, "#1a1238"), (5, "#3a1b5c"), (9, "#7d2a6c"), (13, "#c8434f"), (16, "#ee7a3e"), (17, "#f8a640")]
HULL, PLANK, KEEL, WOODD, ROPE = "#2b1a12", "#4a2c18", "#1e130d", "#3b2416", "#24160f"
SAIL_R, SAIL_W = "#a3222b", "#e6d6ae"


def _lerp(c0, c1, t):
    a, b = parse_hex(c0), parse_hex(c1)
    return "#" + "".join(f"{round(x + (y - x) * t):02x}" for x, y in zip(a[:3], b[:3]))


SHIP = [                       # 22 x 15 sprite; H hull, P planks, K keel, M wood, R/W sail, r rope, E eye, 1-3 shields
    "..........M...........",
    "..........M...........",
    "...MMMMMMMMMMMMMMM....",
    "...rRRRWWWRRRWWWRr....",
    ".HHr.RRWWWRRRWWWr.....",
    "HEH.rRRWWWRRRWWWRr..HH",
    "..H.RRRRWWWRRRWWWRR.rH",
    "..H.rRRRWWWRRRWWWRR.H.",
    "..H..rRRWWWRRRWWWRrH..",
    "...H..RRWWWRRRWWWR.H..",
    "...H......M.......H...",
    "...H1H2H3H1H2H3H1H2H..",
    "...PPPPPPPPPPPPPPPPP..",
    "....HHHHHHHHHHHHHHH...",
    "......KKKKKKKKKKK.....",
]
SHIP_COLORS = {"H": HULL, "P": PLANK, "K": KEEL, "M": WOODD, "R": SAIL_R, "W": SAIL_W, "r": ROPE,
               "E": "#ff7a1a", "1": "#d9a441", "2": "#3a6ea5", "3": "#b8322b"}


def drakkar_pixels(W=40, H=28, horizon=18, ship_at=(16, 5), sun=(9.0, 17.0, 6.6)):
    px = [[None] * W for _ in range(H)]
    for r in range(horizon):
        col = SKY[-1][1]
        for (r0, c0), (r1, c1) in zip(SKY, SKY[1:]):
            if r0 <= r <= r1:
                col = _lerp(c0, c1, (r - r0) / (r1 - r0))
                break
        px[r] = [col] * W
    sx, sy, R = sun
    for r in range(horizon):
        for c in range(W):
            d = ((c - sx) ** 2 + (r - sy) ** 2) ** 0.5
            if d <= R - 2.0:
                px[r][c] = "#ffe88a"
            elif d <= R:
                px[r][c] = "#ffc94d"
            elif d <= R + 1.4:
                px[r][c] = _lerp(px[r][c], "#ffab4a", 0.4)
    for r, c in [(1, 3), (2, 9), (0, 14), (1, 31), (3, 37), (4, 1), (0, 24), (2, 19), (6, 36), (5, 12), (3, 27)]:
        px[r][c] = "#fff2c0"
    for r in range(horizon, H):
        k = r - horizon
        px[r] = [_lerp("#1d3557", "#0b1424", k / (H - 1 - horizon))] * W
        w = 6.2 - 0.62 * k
        for c in range(W):
            if k % 2 == 0 and abs(c - sx) <= w and (c * 3 + r) % 5:
                px[r][c] = "#f8b04a" if k % 4 == 0 else "#e07a3a"
            elif (c * 7 + r * 3) % 13 == 0:
                px[r][c] = "#35557e"
    ox, oy = ship_at
    for r, line in enumerate(SHIP):
        for c, ch in enumerate(line):
            if ch != ".":
                px[oy + r][ox + c] = SHIP_COLORS[ch]
    for r in (horizon + 2, horizon + 4):          # the hull's broken reflection
        for c in range(ox + 4, ox + 19):
            if (c + r) % 3:
                px[r][c] = "#0d1626"
    return px


def pixel_drakkar(grid):
    px = drakkar_pixels()
    H, W = len(px), len(px[0])
    pitch = 8.0
    gw, _ = glyph_box("■")
    S = round((pitch - 0.8) / (2 * gw + 4 * 0.0203), 2)          # ink + faux bold on both sides, 0.8 wu gap
    a = Art("g4_pixel_drakkar", "4 · Pixel-drakkar naplementében (sok glyph egy signben)",
            f"{W}x{H} pixel, világít. Minden pixel ugyanabban a síkban (az 1. rétegen), signenként ~125 glyph: "
            "nem csúsznak szét a sorok, bárhonnan nézed.", origin=(0, 0), grid=grid, unlit="path", mode="detailed")
    x0 = grid.center_x - (W - 1) / 2 * pitch
    y0 = grid.top_y + 2 * 20 + pitch / 2                          # bottom row centre, 1 m above the build
    for r in range(H):
        for c in range(W):
            a.add("■", x0 + c * pitch, y0 + (H - 1 - r) * pitch, size=S, color=px[r][c])
    a.plane_kw = {"grow": 0.2, "res": 0.25}        # pixels only touch at their 0.8 wu seams: one plane
    return {"art": a}


RUNES = "ᚠᚢᚦᚨᚱᚲᚷᚹᚺᚾᛁᛃᛇᛈᛉᛊᛏᛒᛖᛗᛚᛜᛞᛟ"


def rune_ring(grid):
    """v1 proved the multi-line model in game (24 runes in 3 signs at different heights: a perfect ring).
    v2: the whole piece in one sign."""
    a = Art("g5_rune_ring", "5 · Rúnakör (egyetlen signben)",
            "Az első körben 3 signben, eltérő magasságokkal tökéletes kör lett: a több-soros modell igazolva. "
            "Most a teljes darab (24 rúna, 2 gyűrű, nap) EGY signben van.", origin=(0, 0), grid=grid, unlit="path", mode="detailed")
    R = 126
    cx, cy = grid.center_x, grid.top_y + 2 * 20 + 172
    a.add("◯", cx, cy, h=336, color="#c9963c")
    a.add("◯", cx, cy, h=184, color="#6df")
    a.add("☀", cx, cy, h=128, color="#ffd76a")
    ring_of(a, RUNES, R, start=90, h=32, color="#fc6", cx=cx, cy=cy)
    return {"art": a}


def alias_aurora(grid):
    """Experiment: the 5-character unlit alias <font=亨杓麩壀辨> with its loader sign (typable within 50)."""
    a = copy_art(glow_aurora(), "g6_alias_aurora", "6 · Éjféli fény rövid font-álnévvel (kísérlet)",
                 "Ugyanaz a világítás, de az 5 karakteres álnévvel (<font=亨杓麩壀辨>), ami 50 karakterbe is "
                 "begépelhető. Egy betöltő sign (sárga ᛟ a táblán) tölti be a fontot. Kérdés: kilépés és "
                 "visszalépés után is világít-e minden rész.", grid, unlit=True)
    return {"art": a.fit_above(signs_above=2, factor=1.2), "loader": LOADER_TEXT}


PIECES = [fenrir, eikthyr_glow, mjolnir_backdrop, pixel_drakkar, rune_ring, alias_aurora]
