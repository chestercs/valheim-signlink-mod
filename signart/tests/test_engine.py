"""Engine tests: command codec, layout model against in-game measurements, cell assignment."""
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import engine as E  # noqa: E402

pytestmark = pytest.mark.skipif(not os.path.exists(E.GLYPHS_PATH), reason="run extract_glyphs.py first")


def test_parse_handoff_command():
    c = E.parse("<#f33><pos=120><voffset=720><size=70>■")
    assert (c.glyph, c.pos, c.voffset, c.size) == ("■", 120, 720, 70)
    assert c.color == (0xFF, 0x33, 0x33, 0xFF)
    assert not c.warnings


def test_parse_flags_limit_and_multi_glyph():
    c = E.parse("<#123456><size=100><voffset=600><pos=200><rotate=45>■■")
    assert any("more than one glyph" in w for w in c.warnings)
    long = "<#123456><size=100><voffset=600><pos=200><rotate=-45><scale=1.5>■"
    assert any("sign limit" in w for w in E.parse(long).warnings)


def test_emoji_counts_two_chars():
    assert E.utf16_len("🐺") == 2


def test_encode_roundtrip_short_forms():
    c = E.Cmd("◆", size=120, pos=160, voffset=640, rotate=0, scale=2.2, color=E.parse_hex("#ddaa44"))
    s = E.encode(c)
    assert s == "<#da4><size=120><voffset=640><pos=160><scale=2.2>◆"
    back = E.parse(s)
    assert (back.size, back.pos, back.voffset, back.scale) == (120, 160, 640, 2.2)


def test_known_3x3_grid_is_a_grid():
    """The in-game 3x3 proof (handoff section 8): nine squares from nine cells on an exact grid."""
    cmds = {"A1": "<#f33><pos=120><voffset=720><size=70>■", "A2": "<#3f3><pos=160><voffset=720><size=70>■",
            "A3": "<#36f><pos=200><voffset=720><size=70>■", "B1": "<#ff3><pos=120><voffset=640><size=70>■",
            "B2": "<#fff><pos=160><voffset=640><size=70>■", "B3": "<#f3f><pos=200><voffset=640><size=70>■",
            "C1": "<#f93><pos=120><voffset=560><size=70>■", "C2": "<#3ff><pos=160><voffset=560><size=70>■",
            "C3": "<#93f><pos=200><voffset=560><size=70>■"}
    pts = {k: E.layout(E.parse(v), k) for k, v in cmds.items()}
    xs = sorted({round(p.x, 6) for p in pts.values()})
    ys = sorted({round(p.y, 6) for p in pts.values()})
    assert len(xs) == 3 and len(ys) == 3
    assert xs[1] - xs[0] == pytest.approx(120) and xs[2] - xs[1] == pytest.approx(120)
    assert ys[1] - ys[0] == pytest.approx(120) and ys[2] - ys[1] == pytest.approx(120)
    # and a size-70 square is ~100 wu wide: the 20 wu gaps of the screenshot
    w, _ = E.glyph_box("■")
    assert 100 < 2 * 70 * w < 120   # ~112 wu: 8 wu gaps, as on the screenshot


def test_pitch_calibration_direct_neighbours():
    """Handoff section 6: neighbour one sign right needs pos-40, one sign down voffset+20 (= half the sparse pitch)."""
    a = E.layout(E.parse("<size=120><voffset=600><pos=200>■"), "A1")
    # cell B2 of the sparse grid is two physical signs right and down
    d = E.layout(E.parse("<size=120><voffset=640><pos=120>■"), "B2")
    assert (a.x, a.y) == pytest.approx((d.x, d.y))


def test_bigger_glyph_sits_lower_at_same_voffset():
    """Model prediction visible on the 10-layer screenshot: same voffset, bigger circle -> lower center."""
    big = E.layout(E.parse("<voffset=600><size=500>●"), "A1")
    small = E.layout(E.parse("<voffset=600><size=120>●"), "A1")
    assert big.y < small.y


def test_layout_inverse():
    p = E.Placed("◢", 300.0, 650.0, 90, 1.3, 30, (255, 255, 255, 255), "C2")
    c = E.to_cmd(p)
    q = E.layout(c, "C2")
    assert (q.x, q.y) == pytest.approx((p.x, p.y), abs=1.0)


def test_assign_cells_groups_touching_glyphs():
    a = E.Art("t", "t")
    a.add("●", 0, 0, h=100, color="#fff")
    a.add("●", 0, 0, h=40, color="#000")     # touches the first -> same cell, layer 2
    a.add("■", 300, 0, h=50, color="#f00")   # far away -> own group
    rows, pairs = a.build()
    by = {r["tag"] or i: r for i, r in enumerate(rows)}
    cells = [r["cell"] for r in rows]
    big, small = [r for r in rows if r["placed"].glyph == "●"]
    assert big["cell"] == small["cell"] and big["layer"] == 1 and small["layer"] == 2
    bad, probs = E.check_rows(rows)
    assert not bad and not probs


def test_group_too_big_is_reported():
    a = E.Art("t", "t", capacity=2)
    for k in range(3):
        a.add("●", 0, 0, h=100 - 20 * k)
    with pytest.raises(E.CellError):
        a.build()


def test_mirror_keeps_center_and_flips():
    """Negative <scale> mirrors around the glyph's own centre (verified in game with 'c')."""
    a = E.parse("<size=150><voffset=600><pos=200>c")
    b = E.parse("<size=150><voffset=600><pos=200><scale=-1>c")
    pa, pb = E.layout(a, "A1"), E.layout(b, "A1")
    assert pb.scale == -1
    # same advance-centred placement for a glyph centred in its advance; 'c' is close to that
    assert abs(pa.y - pb.y) < 1e-9
    p = E.Placed("🐉", 300.0, 650.0, 120, -1.0, 0, (255, 255, 255, 255), "B2")
    q = E.layout(E.to_cmd(p), "B2")
    assert (q.x, q.y) == pytest.approx((p.x, p.y), abs=1.0) and q.scale == -1


def test_leading_dot_and_negative_scale_encoding():
    assert E.encode(E.Cmd("●", size=120, voffset=600, scale=0.3, color=E.parse_hex("#6de"))) == "<#6de><size=120><voffset=600><scale=.3>●"
    assert E.encode(E.Cmd("c", size=150, voffset=600, scale=-1, color=E.parse_hex("#eed"))) == "<#eed><size=150><voffset=600><scale=-1>c"
    assert E.parse("<scale=-.5>c").scale == -0.5


def test_rotation_twin_picks_the_short_tag():
    assert E.rotation_twin("▲", 120) == ("◀", 30)
    assert E.rotation_twin("▲", 90) == ("◀", 0)
    assert E.rotation_twin("◢", 180) == ("◤", 0)
    assert E.rotation_twin("▲", 90, sx=1.5) == ("▲", 90)      # stretched: no 90-degree swap
    assert E.rotation_twin("▲", 180, sx=1.5) == ("▼", 0)
    p = E.Placed("▲", 300.0, 650.0, 80, 1.0, 120, (255, 255, 255, 255), "A1")
    q = E.layout(E.to_cmd(p), "A1")
    assert q.glyph == "◀" and (q.x, q.y) == pytest.approx((p.x, p.y), abs=1.0)


def test_grid_cells_and_fit_above():
    g = E.Grid(10, 1, 10)
    assert g.order[:3] == ["A1", "A2", "A3"] and g.pitch_x == 80 and g.center_x == 360
    g2 = E.Grid(3, 3, 5, gap_x=0.5, backdrop=((1, 1),))
    assert g2.pitch_x == 60 and g2.order[0] == "BG1" and g2.cells["BG1"] == (1, 1, True)
    a = E.Art("t", "t", grid=g)
    a.add("●", 0, 0, h=100)
    a.fit_above(signs_above=3, factor=0.5)
    l, b, r, t = E.placed_bbox(a.items)
    assert (l + r) / 2 == pytest.approx(360) and b == pytest.approx(10 + 60)


def test_one_line_sign_is_the_single_glyph_model():
    c = E.parse("<#fc6><size=30><voffset=500><pos=40><rotate=20>ᚠ")
    a = E.layout(c, "B2")
    b = E.layout_sign(E.encode(c), "B2")[0]
    assert (round(a.x, 6), round(a.y, 6)) == (round(b.x, 6), round(b.y, 6))


def test_same_size_and_voffset_lines_land_like_separate_signs():
    """The in-game two-glyph case: <line-height=0><size=60><voffset=600>●<pos=150>●."""
    two = E.layout_sign("<line-height=0><size=60><voffset=600>●<br><pos=150>●", "A1")
    one = [E.layout(E.parse("<size=60><voffset=600>●"), "A1"), E.layout(E.parse("<size=60><voffset=600><pos=150>●"), "A1")]
    for p, q in zip(two, one):
        assert abs(p.x - q.x) < 1e-6 and abs(p.y - q.y) < 1e-6


def test_first_line_voffset_sets_the_centring():
    """Model (unverified in game): Y_i = 2*V_i + S*(yMin+yMax)_i - K, K from the first line's ascender."""
    a, b = E.layout_sign("<line-height=0><size=30><voffset=400>ᚠ<br><voffset=500>ᚠ", "A1")
    assert abs((b.y - a.y) - 200) < 1e-6          # 100 voffset apart -> 200 wu, not 100


def test_line_cmds_roundtrip_and_tag_state():
    items = [E.Placed("ᚠ", 30 * i, 300 + 9 * i, 20, rotate=10 * (i % 2), color=(255, 200, 80, 255), cell="A1",
                      font=E.FONT_PATH) for i in range(6)]
    text = E.encode_lines(E.line_cmds(items))
    assert text.startswith(f"{E.LINE_TAG}<font={E.FONT_PATH}>") and text.count(E.BREAK) == 5
    assert text.count("<#") == 1                  # colour carried over, not repeated
    back = E.layout_sign(text, "A1")
    for p, q in zip(items, back):
        assert abs(p.x - q.x) <= 1 and abs(p.y - q.y) <= 1 and p.rotate == q.rotate and q.font == E.FONT_PATH


def test_assign_planes_levels_and_loose_glyphs():
    a = E.Art("t", "t", origin=(0, 0))
    a.add("●", 100, 300, h=200, color="#123")         # disc
    a.add("◯", 100, 300, h=200, color="#fc6")         # ring on the disc, same font sub-mesh -> level 1
    a.add("ᚦ", 100, 300, h=40, color="#fc6")          # on the disc, apart from the ring -> level 2
    a.add("★", 100, 300, h=20, color="#fff")          # on the rune -> level 3
    a.add("ᚠ", 400, 300, h=40, color="#fc6")          # touches nothing -> layer 1, any cell
    placed = [E.replace(p) for p in a.items]
    main, top = E.assign_planes(placed, a.grid)
    assert [p.layer for p in placed] == [1, 1, 2, 3, 1] and top == 3     # disc + ring: one sub-mesh (jp)
    assert {p.cell for p in placed[:4]} == {main}


def test_build_planes_round_trip_with_font_switch():
    a = E.Art("t", "t", origin=(0, 0), unlit="path")
    a.add("●", 100, 300, h=120, color="#eec")
    a.add("▲", 300, 260, h=60, color="#234", glow=False)  # lit glyph in the same sign as unlit ones
    a.add("ᚠ", -100, 340, h=40, color="#6df")
    rows = E.build_planes(a)
    assert len(rows) == 1 and "</font>" in rows[0]["text"]
    back = {q.glyph: q for q in rows[0]["placed"]}
    assert back["▲"].font is None and back["ᚠ"].font == E.FONT_PATH
    assert rows[0]["err"] < 0.5


def test_touching_glyphs_of_one_submesh_share_a_level():
    a = E.Art("t", "t", origin=(0, 0), unlit="path", mode="detailed")
    a.add("■", 0, 300, h=60, w=12, color="#532", glow=False)          # wood bars, one sub-mesh: one level
    a.add("■", 0, 340, h=50, w=8, rot=30, color="#532", glow=False)
    a.add("●", -12, 366, h=14, color="#5f9")                         # leaf (unlit): above the wood
    placed = [E.replace(p) for p in a.items]
    E.assign_planes(placed, a.grid)
    assert [p.layer for p in placed] == [1, 1, 2]


def test_typable_rows_stay_on_one_plane():
    a = E.Art("t", "t", origin=(0, 0))
    a.add("●", 0, 0, h=120, color="#b42", tag="disc")
    a.add("🐗", 0, 0, h=70, color="#fec", tag="disc")
    for k, ch in enumerate("ᚷᚨᛚᛏᛁ"):
        a.add(ch, -80 + 40 * k, -100, h=30, color="#da4", tag="runes")
    a.fit_above(signs_above=2)
    rows, _ = a.build()
    runes = [r for r in rows if r["tag"] == "runes"]
    assert len({r["layer"] for r in runes}) == 1 and len({r["cell"] for r in runes}) == 5
    assert all(r["chars"] <= E.CHAR_LIMIT for r in rows)


def test_measured_sign_position_wins_over_the_cell_mean():
    g = E.Grid(depth=2, positions=(("A1", 0.0, 0.0), ("B1", 2.0, -44.0)),
               sign_positions=(("A1", 1, 0.3, -0.6), ("A1", 2, -0.2, 0.4)))
    assert g.pos("A1") == (0.0, 0.0) and g.pos("A1", 1) == (0.3, -0.6) and g.pos("B1", 1) == (2.0, -44.0)
    c = E.parse("<size=20><voffset=300>●")
    assert abs(E.layout(c, "A1", g, 1).y - E.layout(c, "A1", g).y + 0.6) < 1e-9


def test_parse_sign_line_height_and_indent():
    cmds, lh = E.parse_sign("<line-height=2.5><size=3><indent=10>■\n■<br><pos=-4>■\n■")
    assert lh == 2.5 and [c.pos for c in cmds] == [10, 10, -4, 10]     # pos is one line only, indent persists
    a, b, c, d = E.layout_sign("<line-height=2.5><size=3><indent=10>■\n■<br><pos=-4>■\n■", "A1")
    assert abs((a.y - b.y) - 5) < 1e-9 and abs((b.y - c.y) - 5) < 1e-9     # one line = 2*L wu lower
    assert a.x == b.x == d.x and abs((a.x - c.x) - 14) < 1e-9


def test_column_encoding_for_loose_glyphs():
    a = E.Art("m", "m", origin=(0, 0), unlit="path", mode="detailed")
    a.plane_kw = {"independent": True}
    cols = ["#c33", "#3c3", "#36c"]
    for r in range(8):
        for k in range(12):
            a.add("■", -30 + 5 * k, 200 - 5 * r, size=3.15, color=cols[(r // 3 + k // 4) % 3])
    rows = E.build_planes(a)
    assert len(rows) == 1
    t = rows[0]["text"]
    assert t.startswith("<line-height=2.5>") and t.count("<indent=") == 12 and t.count("<voffset=") <= 13
    assert "<pos=" not in t and "<br>" not in t and t.count("\n") == 95
    assert rows[0]["err"] < 0.15 and rows[0]["chars"] / 96 < 9      # 12 columns: indent + voffset each


def test_engine_model_matches_the_gallery9_column_text():
    """concepts/row4.column_encoded wrote the text verified in game (gallery 9 block 5): the engine's
    general line-height model must put every pixel where that generator aimed it."""
    from concepts import row4
    px = row4.picture(6, 4, ss=1)
    pv = E.Art("pv", "", origin=(0, 0), unlit="path", mode="detailed")
    text = row4.column_encoded(px, 5.0, 50.0, 260.0, "B2", None, E.DEFAULT_GRID, pv)
    back = E.layout_sign(text, "B2")
    assert len(back) == len(pv.items) == 24
    assert max(abs(q.x - p.x) + abs(q.y - p.y) for q, p in zip(back, pv.items)) < 0.2


def test_line_height_is_exact_in_float32():
    """TMP adds the line height in float32 at every line break: 0.8 drifts, multiples of 1/4 do not."""
    import numpy as np
    assert E.exact_line_height(0.8) == 0.75 and E.exact_line_height(2.5) == 2.5 and E.exact_line_height(1.5625) == 1.5
    for lh in (0.25, 0.75, 1.25, 2.5, 3.5):
        assert E._tmp_float(E._num(lh, 3)) == lh
        off = np.float32(0)
        for _ in range(50000):
            off = np.float32(off + np.float32(lh))
        assert float(off) == 50000 * lh
