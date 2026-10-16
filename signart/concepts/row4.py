"""Row 4 (gallery 9, 2026-10-08): test blocks for the open questions of docs/prior-art.md.

Blocks 1-3 hold one test per board, on the front layer (L5) so no plank hides it; boards read A1 A2 A3
(top row, left to right), B1 B2 B3, C1 C2 C3. Blocks 4-6 compare pixel encodings.

1 escapes      \\n \\u \\U \\t typed as backslash sequences (TMP parseCtrlCharacters, TMP_Text.cs:2357-2440)
2 sprites      <sprite=N>, <sprite anim=first,last,fps> (TMP_Text.cs:6409), named asset, tint
3 wrapping     <nobr> against a long run (prediction from TextMeshProUGUI.cs:2642-2649: the first word of a line
               still breaks between characters, so <nobr> does NOT keep the run on one line), <mspace>,
               <line-height>, the block glyphs (U+2580-2590, full em squares in the jp fallback font)
4 rows         one text line per pixel row, pitch = <mspace> = <line-height>, colour tag only on change.
               A row must fit the 17.29-unit wrap box (0.86 m), so these pictures are board-sized.
5 columns      one line per pixel, column by column: <indent> once per column (it persists across lines,
               TMP_Text.cs:6281, TextMeshProUGUI.cs:2577), <line-height> = pitch, one <voffset> per column
               to climb back to the top, colour only on change: no pos per pixel, any width
6 reference    the same picture in the current encoding (pos + voffset + <br> per pixel, gallery 8 block 1)

Vertical model with line-height L (local units, 1 local = 2 wu) and N lines, extending layout_lines:
  Y_i = 2*V_i - 2*i*L + S*(yMin+yMax) - K + (N-1)*L,  K = S*A + S*D + max(V_0, 0) + min(V_last, 0)
(the first line's ascender and the last line's unshifted descender centre the block, :2696).
"""
import engine as E
import pixels as P
from concepts.row3 import mosaic, scene

FRONT = 5                                            # layer nearest the viewer


def _art(key, title, idea, grid):
    return E.Art(key, title, idea, origin=(0, 0), grid=grid, unlit="path", mode="detailed")


def _raw(key, title, idea, grid, texts, placed=None, notes=()):
    longest = max(E.utf16_len(t) for t in texts.values())
    a = _art(key, title, idea, grid)
    a.mode = "typable" if longest <= E.CHAR_LIMIT else "detailed"
    return {"art": a, "raw": texts, "placed": placed or [], "notes": list(notes)}


def _boards(tests):
    """{'A1': text, ...} -> {(cell, FRONT): text}"""
    return {(cell, FRONT): t for cell, t in tests.items()}


# ----------------------------------------------------------------------------- blocks 1-3: tag tests

def escapes(grid):
    return _raw("g9_escapes", "1 · Escape-ek: \\n \\u \\U \\t",
                "Minden tábla egy teszt (A1 bal felső ... C3 jobb alsó), a backslash-sorozatokat a TMP maga alakítja át.",
                grid, _boards({
                    "A1": "<size=7>■\\n■",              # typed backslash + n: two squares stacked?
                    "A2": "<size=7>■<br>■",             # control (known: line feed)
                    "A3": "<size=7>■\n■",               # a real line feed in the save
                    "B1": "<size=7>\\u25A0",            # one ■ from an escape
                    "B2": "<size=7>\\u16A0\\u16A2\\u16A6",  # runes ᚠᚢᚦ from escapes
                    "B3": "<size=7>■\\t■",              # tab: a gap between the squares
                    "C1": "<size=7>■\\\\n■",            # escaped backslash: shows the text \n literally?
                    "C2": "<size=7><noparse>■\\n■</noparse>",  # does noparse stop the escape?
                    "C3": "<size=7>\\U0001F43A",        # wolf emoji from an 8-digit escape
                }))


def sprites(grid):
    return _raw("g9_sprites", "2 · Sprite-ok: <sprite>, <sprite anim>",
                "A játékban benne van a TMP EmojiOne sprite-készlete. B1 és B2 elvileg magától animál, mod nélkül.",
                grid, _boards({
                    "A1": "<size=10><sprite=0>",
                    "A2": "<size=10><sprite=7>",
                    "A3": "<size=10><sprite=15>",
                    "B1": '<size=10><sprite anim="0,15,8">',
                    "B2": '<size=10><sprite anim="0,15,2">',
                    "B3": '<size=10><sprite="EmojiOne" index=3>',
                    "C1": "<size=10><#f44><sprite=1 tint=1>",
                    "C2": "<size=10><sprite=16>",          # a 17th sprite?
                    "C3": "<size=10>😀",                    # control: emoji font fallback (works)
                }))


def _grid4x3(S, M, glyph="■", lf="\n"):
    cols = ("#c33", "#3c3", "#36c", "#cc3")
    rows = []
    for r in range(3):
        rows.append("".join(f"<{cols[(r + k) % 4]}>{glyph}" for k in range(4)))
    return f"<size={S}><mspace={M}><line-height={M}>" + lf.join(rows)


def wrapping(grid):
    S = round(P.glyph_size(8), 2)                        # ■ ink = 8 wu pitch + a hair
    return _raw("g9_wrap", "3 · Tördelés és rács: <nobr>, <mspace>, <line-height>, █",
                "A1/A2/C3: kifut-e a sor a tábla 0,86 m-es tördelési dobozából (jóslat: a <nobr> nem segít). "
                "B1-B3: 4x3-as színes rács mspace + line-height lépéssel, hézag nélkül kéne.",
                grid, _boards({
                    "A1": "<size=5><mspace=.5em><nobr>■■■■■■■■",   # 20 units > 17.29: breaks after 6?
                    "A2": "<size=5><mspace=.5em>■■■■■■■■",          # control without nobr
                    "A3": "<size=2><mspace=1em>■■■■■■■■",           # 16 units: fits, one evenly spaced row
                    "B1": _grid4x3(S, 4),                            # real line feeds
                    "B2": _grid4x3(S, 4, lf="\\n"),                  # typed \n escapes
                    "B3": _grid4x3(4, 4, glyph="█"),                 # full block: em = pitch, no gaps
                    "C1": "<size=2><cspace=-0.2em><line-height=85%><nobr><#c33>██<#3c3>██<#36c>██\n"
                          "<#cc3>██<#c3c>██<#3cc>██",                # Img2Bpl simple-mode style
                    "C2": "<size=3>█▀▄▌▐",
                    "C3": "<size=5><mspace=.5em><nobr>■■■■ ■■■■",  # nobr across a space
                }))


# ----------------------------------------------------------------------------- blocks 4-6: encodings

def picture(W, H, ss=3, dither=True):
    """row3.picture with an option to skip the dither (flat areas: fewer colour changes)."""
    return P.quantize(P.sample(scene, W, H, ss), dither)


def _size_for(p):
    return round(P.glyph_size(p), 2)


def _num(x):
    return f"{x:.2f}".rstrip("0").rstrip(".")


def row_encoded(px, p, cell, layer, grid, preview):
    """One text line per pixel row; pitch p wu = mspace = line-height (p/2 local). Centred on the sign."""
    H, W = len(px), len(px[0])
    M = p / 2
    assert W * M + 0.5 <= 17.29, "row wider than the wrap box"
    S = _size_for(p)
    g, A, D, _ = E.db().metrics("■")
    yc = S * (g["bb"][1] + g["bb"][3])
    gx, gy = grid.pos(cell, layer)
    lines, cur = [], None
    for r in range(H):
        s = ""
        for k in range(W):
            if px[r][k] != cur:
                cur = px[r][k]
                s += f"<{cur}>"
            s += "■"
            preview.add("■", gx + (k + 0.5 - W / 2) * p, gy + yc - S * (A + D) + (H - 1 - 2 * r) * M,
                        size=S, color=cur)
        lines.append(s)
    return f"<font={E.FONT_PATH}><size={_num(S)}><mspace={_num(M)}><line-height={_num(M)}>" + "\n".join(lines)


def column_encoded(px, p, x_left, y_top, cell, layer, grid, preview):
    """One line per pixel, column-major: <indent> + <voffset> once per column, colour only on change.
    (x_left, y_top) = centre of the top-left pixel, wu."""
    H, W = len(px), len(px[0])
    L = p / 2
    S = _size_for(p)
    g, A, D, _ = E.db().metrics("■")
    yc = S * (g["bb"][1] + g["bb"][3])
    dx, _ = E.center_offset("■", S, 1.0)
    gx, gy = grid.pos(cell, layer)
    N, span = W * H, (W - 1) * H * L
    T = y_top - gy - yc + S * (A + D) - (N - 1) * L        # f(C) = 2C - max(C,0) - min(span+C,0) = T
    C = T if T >= 0 else (T / 2 if T >= -2 * span else T + span)
    C = round(C, 1)
    lines, cur = [], None
    for c in range(W):
        x = x_left + c * p
        head = f"<indent={_num(round(x - dx - gx, 1))}><voffset={_num(round(c * H * L + C, 1))}>"
        for r in range(H):
            s = head if r == 0 else ""
            if px[r][c] != cur:
                cur = px[r][c]
                s += f"<{cur}>"
            lines.append(s + "■")
            preview.add("■", x, y_top - r * p, size=S, color=cur)
    return f"<font={E.FONT_PATH}><size={_num(S)}><line-height={_num(L)}>" + "\n".join(lines)


def _stats(text, W, H):
    vis = W * H + text.count("\n")
    return f"{E.utf16_len(text)} chars ({E.utf16_len(text) / (W * H):.1f}/pixel), {vis} visible"


def rows_block(grid):
    texts, notes = {}, []
    pv = _art("g9_rows_pv", "", "", grid)
    for cell, (W, H, p, dither) in {"A2": (40, 27, 0.8, True), "B2": (80, 53, 0.4, True),
                                     "C2": (80, 53, 0.4, False)}.items():
        px = picture(W, H, dither=dither)
        t = row_encoded(px, p, cell, FRONT, grid, pv)
        texts[(cell, FRONT)] = t
        notes.append(f"{cell} {W}x{H}{'' if dither else ' flat'}: {_stats(t, W, H)}")
    return _raw("g9_rows", "4 · Sor-kódolás: egy pixelsor = egy szövegsor (tábla-méret)",
                "A2: 40x27, B2: 80x53, C2: 80x53 dither nélkül. <mspace> és <line-height> adja a rácsot, szín csak "
                "ha változik. Egy sor nem lehet szélesebb a tábla 0,86 m-es dobozánál.", grid, texts, pv.items, notes)


REF = dict(W=60, H=40, width=300)


def columns_block(grid):
    """The gallery-8 block-1 picture (60 x 40, 7.5 m) in the column encoding, on the same sign the reference uses."""
    W, H, width = REF["W"], REF["H"], REF["width"]
    p = width / W
    ref = E.build_planes(mosaic("tmp", "", "", grid, W, H, width)["art"])
    cell, layer = ref[0]["cell"], ref[0]["layer"]
    x_left = grid.center_x - (W - 1) / 2 * p
    y_top = grid.top_y + 2.0 * 20 + p / 2 + (H - 1) * p          # as row3.mosaic
    pv = _art("g9_cols_pv", "", "", grid)
    px = picture(W, H, ss=3)
    t = column_encoded(px, p, x_left, y_top, cell, layer, grid, pv)
    return _raw("g9_columns", "5 · Oszlop-kódolás: <indent> + <line-height>, pos nélkül",
                "Ugyanaz a kép, mint a 6. blokkon, ugyanazon a signen, de pixelenként csak ■ + sortörés (+ szín, ha "
                "változik). Ha jó a modell, pont a 6. blokkéval egyező helyen és méretben jelenik meg.",
                grid, {(cell, layer): t}, pv.items, [f"{cell}-{layer}: {_stats(t, W, H)}",
                                                    f"reference: {ref[0]['chars']} chars"])


def reference(grid):
    out = mosaic("g9_ref", "6 · Referencia: a jelenlegi kódolás (pos + voffset + <br> pixelenként)",
                 "A gallery 8 1. képe, összehasonlításnak az 5. blokkal.", grid, REF["W"], REF["H"], REF["width"])
    return out


PIECES = [escapes, sprites, wrapping, rows_block, columns_block, reference]
