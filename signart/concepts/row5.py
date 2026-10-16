"""Row 5 (gallery 10, 2026-10-08): vanilla animation with <sprite anim> (verified in gallery 9).

TMP's EmojiOne sheet in the game has 16 faces, indexed row by row on the 4x4 sheet:
   0 smiling, blushing     1 savouring, tongue   2 heart eyes            3 sunglasses
   4 grin, dot eyes        5 grin, squinting     6 tears of joy          7 grin, big eyes
   8 grin, squinting       9 grin, sweat drop   10 persevering (X eyes) 11 winking, tongue
  12 "?" box              13 rolling, laughing  14 slightly smiling     15 slightly frowning
<sprite anim="a,b,fps"> cycles a..b on every client with no mod and no network (TMP_SpriteAnimator); tint=1
multiplies the face by the current colour. Blocks 1-2 are typable (one command per board, <= 50 characters).

Sprite metrics are not in the glyph database: multi-sprite signs are laid out with ■ as a stand-in of the
same size (line_cmds, order_lines), then every ■ is swapped for its sprite tag, so positions are close
(the face is about 1.2 em tall) but not exact.
"""
import numpy as np

import engine as E
from concepts.row3 import mosaic
from concepts.row4 import FRONT, _boards, _raw

FACES = {0: "pirulós mosoly", 1: "nyelves", 2: "szívszemű", 3: "napszemüveges", 4: "vigyor", 5: "hunyorgó vigyor",
         6: "könnyes nevetés", 7: "nagy szemű vigyor", 8: "hunyorgó vigyor", 9: "izzadós vigyor", 10: "fintor",
         11: "kacsintós", 12: "kérdőjel", 13: "hempergő nevetés", 14: "enyhe mosoly", 15: "szomorú"}


def sprite_sign(entries, cell, layer, grid):
    """entries: (x, y, size, sprite tag or glyph, colour) in wu -> one multi-line sign text. A sprite tag
    (starting with '<sprite') is laid out as a ■ of the same size and swapped in afterwards."""
    items = []
    for x, y, S, what, col in entries:
        g = "■" if what.startswith("<sprite") else what
        items.append(E.Placed(g, x, y, S, color=E.parse_hex(col), cell=cell, layer=layer, font=E.FONT_PATH,
                              tag=what))
    order = E.order_lines(items, grid)
    text = E.encode_lines(E.line_cmds(order, grid))
    tags = iter([p.tag for p in order if p.glyph == "■"])
    # ■ never appears inside a tag, and the k-th ■ of the text is the k-th ■ of the line order
    return "".join(next(tags) if ch == "■" else ch for ch in text)


def _check_swap(text, entries):
    assert text.count("<sprite") == sum(1 for e in entries if e[3].startswith("<sprite")), "sprite swap lost a face"


# ----------------------------------------------------------------------------- typable

def slot_machine(grid):
    """Three reels, one per board of the top row, lifted 2.5 m; different speeds never line up."""
    reels = {"A1": 7, "A2": 11, "A3": 13}
    texts = {(c, FRONT): f'<size=30><voffset=100><sprite anim="0,15,{f}">' for c, f in reels.items()}
    return _raw("g10_slots", "1 · [gépelhető] Félkarú rabló: 3 tárcsa, 3 sebesség",
                "Táblánként egy parancs (47 karakter). A tárcsák 7, 11 és 13 képkocka/mp-cel pörögnek, mod nélkül.",
                grid, texts)


def moods(grid):
    pairs = {"A1": (4, 5, 2), "A2": (14, 15, 1), "A3": (2, 3, 1), "B1": (7, 8, 3), "B2": (0, 1, 1),
             "B3": (10, 11, 2), "C1": (5, 6, 2), "C2": (13, 15, 3), "C3": (0, 15, 4)}
    return _raw("g10_moods", "2 · [gépelhető] Pislogó, nevető, hangulatváltó arcok",
                "Két-három szomszédos arc váltakozik: A1 pislog, A2 mosoly/szomorú, A3 szerelmes/laza, B1 pislog, "
                "B2 elpirul/nyelvet nyújt, B3 fintor/kacsint, C1 nevet/könnyezik, C2 hempereg, C3 mind a 16.",
                grid, _boards({c: f'<size=10><sprite anim="{a},{b},{f}">' for c, (a, b, f) in pairs.items()}))


# ----------------------------------------------------------------------------- detailed

def index_chart(grid):
    """The 16 faces with their numbers: which index is which (for typing <sprite=N>)."""
    ent = []
    x0, ytop = grid.center_x - 1.5 * 60, grid.top_y + 60 + 3 * 72
    for n in range(16):
        r, k = divmod(n, 4)
        x, y = x0 + k * 60, ytop - r * 72
        ent.append((x, y, 8, f"<sprite={n}>", "#fff"))
        digits = str(n)
        for j, d in enumerate(digits):
            ent.append((x + (j - (len(digits) - 1) / 2) * 4.4, y - 30, 3, d, "#ffd84a"))
    t = sprite_sign(ent, "A2", 1, grid)
    _check_swap(t, ent)
    return _raw("g10_index", "3 · Az EmojiOne 16 arca a számukkal",
                "Melyik <sprite=N> melyik arc: 0-15. A 12-es egy kérdőjeles doboz.", grid, {("A2", 1): t},
                notes=[f"{E.utf16_len(t)} chars"])


def crowd(grid):
    """A stadium: 10 x 5 grinning faces, each its own range and speed."""
    rng = np.random.RandomState(7)
    ranges = [(4, 5), (4, 9), (5, 6), (7, 8), (8, 9), (0, 1), (13, 14)]
    ent = []
    for r in range(5):
        for k in range(10):
            a, b = ranges[rng.randint(len(ranges))]
            f = int(rng.randint(2, 11))
            x = grid.center_x + (k - 4.5) * 30 + (r % 2) * 8
            y = grid.top_y + 50 + (4 - r) * 30
            ent.append((x, y, 6, f'<sprite anim="{a},{b},{f}">', "#fff"))
    t = sprite_sign(ent, "B2", 1, grid)
    _check_swap(t, ent)
    return _raw("g10_crowd", "4 · Szurkoló tömeg: 50 arc, mind más ütemben",
                "Egyetlen sign, 50 animált sprite, 2-10 képkocka/mp. Mod nélkül él.", grid, {("B2", 1): t},
                notes=[f"{E.utf16_len(t)} chars"])


def twinkle(grid):
    """The fjord (column-encoded mosaic) with animated star sprites on the front layer of the same cell."""
    base = mosaic("g10_twinkle", "5 · Pislákoló csillagok a fjord felett",
                  "A pixelkép (oszlop-kódolás) elé 36 apró, halványsárga, animált sprite: a csillagok pislognak.",
                  grid, 60, 40, 300)["art"]
    rows = E.build_planes(base)
    (r0,) = rows
    texts = {(r0["cell"], r0["layer"]): r0["text"]}
    rng = np.random.RandomState(11)
    pitch = 300 / 60
    x0 = grid.center_x - 29.5 * pitch
    ytop = grid.top_y + 40 + pitch / 2 + 39 * pitch
    ent = []
    ranges = [(13, 14), (14, 15), (9, 10), (5, 6)]
    while len(ent) < 36:
        u, v = rng.uniform(0.03, 0.97), rng.uniform(0.03, 0.42)
        if np.hypot(u - 0.84, v - 0.13) < 0.08:            # not on the moon
            continue
        a, b = ranges[rng.randint(len(ranges))]
        S = round(float(rng.uniform(0.8, 1.6)), 2)
        ent.append((x0 + u * 59 * pitch, ytop - v * 39 * pitch, S,
                    f'<sprite anim="{a},{b},{int(rng.randint(1, 5))}" tint=1>', "#ffec"))
    t = sprite_sign(ent, r0["cell"], FRONT, grid)
    _check_swap(t, ent)
    texts[(r0["cell"], FRONT)] = t
    return _raw(base.key, base.title, base.idea, grid, texts, r0["placed"],
                notes=[f"picture {r0['chars']} chars, stars {E.utf16_len(t)} chars"])


def disco(grid):
    """3 x 3 big faces, each tinted and cycling all 16 at its own speed: a flickering colour wall."""
    cols = ["#f33c", "#f93c", "#ff3c", "#3f6c", "#3ffc", "#39fc", "#93fc", "#f3fc", "#fffc"]
    ent = []
    for n in range(9):
        r, k = divmod(n, 3)
        ent.append((grid.center_x + (k - 1) * 80, grid.top_y + 60 + (2 - r) * 80, 14,
                    f'<sprite anim="0,15,{6 + n}" tint=1>', cols[n]))
    t = sprite_sign(ent, "C2", 1, grid)
    _check_swap(t, ent)
    return _raw("g10_disco", "6 · Diszkófal: 9 színezett, pörgő arc",
                "tint=1: a sprite a szövegszínnel szorzódik. 6-14 képkocka/mp.", grid, {("C2", 1): t},
                notes=[f"{E.utf16_len(t)} chars"])


PIECES = [slot_machine, moods, index_chart, crowd, twinkle, disco]
