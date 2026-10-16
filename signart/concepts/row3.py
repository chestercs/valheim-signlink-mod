"""Row 3 (2026-10-08): the pixel ladder — one scene, bigger and bigger, each picture ONE sign (detailed
mode). Finds the real limits of a sign:

- text length: none in a save (ZDO strings are length-prefixed);
- 16 383 glyphs per material in one mesh (TMP_MeshInfo.cs:48); TMP spills over into another sub-mesh
  beyond that (TextMeshProUGUI.cs:1225) -> blocks 4 (just under) and 5 (over): both whole in game;
- 99 999 visible characters per sign (TMP_Text.m_maxVisibleCharacters, line breaks count too): with one
  glyph + one <br> per pixel that is 50 000 pixels -> block 6 (55 200 pixels, high above the whole row)
  lost exactly its bottom 17 rows and the right third of row 166 in game (verified 2026-10-08).

The scene is a continuous function sampled at any resolution (supersampled, Bayer-dithered to 12-bit
colour so every colour tag is 6 characters): fjord at dusk, aurora, crescent moon, stars, snowy ridge,
pines, a longhouse with glowing windows, a longship, the lake mirroring it all.
"""
import numpy as np

import pixels as P
from engine import Art
from pixels import BAYER  # noqa: F401  (kept importable from here)

HORIZON = 0.62


def _grad(v, stops):
    out = np.zeros(v.shape + (3,))
    for (v0, c0), (v1, c1) in zip(stops, stops[1:]):
        t = np.clip((v - v0) / (v1 - v0), 0, 1)
        m = (v >= v0) & (v <= v1)
        out[m] = (np.array(c0) + (np.array(c1) - np.array(c0)) * t[m][:, None])
    out[v < stops[0][0]] = stops[0][1]
    out[v > stops[-1][0]] = stops[-1][1]
    return out


def _hex(h):
    return [int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]


SKY = [(0.0, _hex("#070b24")), (0.25, _hex("#142a5c")), (0.45, _hex("#6a3a78")), (0.56, _hex("#e0765a")),
       (HORIZON, _hex("#f5a65a"))]


def _above(u, v):
    """Everything above the water line, for any (u, v) arrays (v down, 0..1)."""
    col = _grad(v, SKY)
    # aurora curtain
    c = 0.17 + 0.06 * np.sin(6 * u + 1) + 0.03 * np.sin(17 * u)
    rays = 0.55 + 0.45 * np.sin(42 * u + 3 * np.sin(7 * u)) ** 2
    I = np.exp(-((v - c) / 0.07) ** 2) * rays * (v < 0.45)
    top = np.clip((c - v) / 0.12 + 0.5, 0, 1)[..., None]
    aur = np.array(_hex("#39ff9c")) * (1 - top) + np.array(_hex("#8a5cff")) * top
    col = col + I[..., None] * aur * 0.85
    # stars: one per cell at most, sharp points (dim when averaged at low resolution)
    gx, gy = np.floor(u * 140), np.floor(v * 140)
    h = np.sin(gx * 12.9898 + gy * 78.233) * 43758.5453
    h = h - np.floor(h)
    sx, sy = (gx + 0.2 + 0.6 * ((h * 7) % 1)) / 140, (gy + 0.2 + 0.6 * ((h * 13) % 1)) / 140
    d = np.hypot(u - sx, v - sy)
    star = (h < 0.09) & (v < 0.42) & (d < 0.0016 + 0.0012 * ((h * 31) % 1))
    col = np.where(star[..., None], np.maximum(col, 0.95), col)
    # crescent moon with a glow
    dm = np.hypot(u - 0.84, v - 0.13)
    moon = (dm < 0.045) & (np.hypot(u - 0.858, v - 0.118) > 0.040)
    col = col + (np.exp(-((dm - 0.04) / 0.03) ** 2) * 0.18 * ~moon)[..., None] * np.array(_hex("#fff4d0"))
    col = np.where(moon[..., None], np.array(_hex("#fff4d0")), col)
    # snowy far ridge
    f = 0.47 - 0.07 * (0.6 * np.sin(5.1 * u + 0.3) + 0.3 * np.sin(11.7 * u + 1.2) + 0.1 * np.sin(23 * u))
    m = v > f
    snow = m & (v < f + 0.022 + 0.01 * np.sin(40 * u)) & (f < 0.445)
    col = np.where(m[..., None], np.array(_hex("#2a2850")) + (v - f)[..., None] * 0.4, col)
    col = np.where(snow[..., None], np.array(_hex("#dfe4f2")), col)
    # near hills and a pine forest on them
    g = 0.565 - 0.025 * np.sin(3.3 * u + 2) - 0.012 * np.sin(9 * u)
    col = np.where((v > g)[..., None], np.array(_hex("#1c3326")), col)
    rng = np.random.RandomState(4)
    for ui in np.linspace(0.0, 1.0, 46) + rng.uniform(-0.008, 0.008, 46):
        if 0.10 < ui < 0.25 or 0.47 < ui < 0.67:
            continue                                  # room for the longhouse and the longship's reflection
        gi = 0.565 - 0.025 * np.sin(3.3 * ui + 2) - 0.012 * np.sin(9 * ui)
        hi = rng.uniform(0.03, 0.065)
        t = (v - (gi - hi)) / hi
        tree = (t >= 0) & (t <= 1.05) & (np.abs(u - ui) < hi * 0.33 * np.clip(t, 0, 1))
        col = np.where(tree[..., None], np.array(_hex("#0d1c14")), col)
    # longhouse on the left shore, glowing windows, smoke
    house = (u > 0.125) & (u < 0.215) & (v > 0.585) & (v < 0.62)
    roof = (v > 0.555) & (v <= 0.585) & (np.abs(u - 0.17) < 0.052 * (v - 0.555) / 0.03)
    col = np.where(house[..., None], np.array(_hex("#3a2416")), col)
    col = np.where(roof[..., None], np.array(_hex("#5a3a1e")), col)
    win = ((np.abs(u - 0.145) < 0.006) | (np.abs(u - 0.195) < 0.006)) & (v > 0.595) & (v < 0.606)
    col = np.where(win[..., None], np.array(_hex("#ffcf6a")), col)
    door = (np.abs(u - 0.17) < 0.007) & (v > 0.6)
    col = np.where(door[..., None], np.array(_hex("#140b06")), col)
    for k in range(7):
        sxk, syk = 0.205 + 0.012 * k + 0.004 * np.sin(k), 0.55 - 0.028 * k
        puff = np.hypot(u - sxk, (v - syk) * 1.3) < 0.007 + 0.002 * k
        col = np.where(puff[..., None], col * 0.4 + np.array(_hex("#a4a8b4")) * 0.6, col)
    return col


def _ship(u, v, col):
    """A longship on the water (drawn over the lake and its own reflection is skipped)."""
    hull = (np.abs(u - 0.575) < 0.07 - 0.9 * np.maximum(v - 0.615, 0) ** 1.0 * 4) & (v > 0.6) & (v < 0.632)
    sail = (np.abs(u - 0.575) < 0.036) & (v > 0.52) & (v < 0.585)
    stripe = (np.floor((u - 0.539) / 0.012) % 2 == 0)
    mast = (np.abs(u - 0.575) < 0.0025) & (v > 0.5) & (v < 0.6)
    prow = (np.hypot(u - 0.508, v - 0.592) < 0.012) & (np.hypot(u - 0.514, v - 0.586) > 0.007)
    stern = (np.hypot(u - 0.642, v - 0.592) < 0.012) & (np.hypot(u - 0.636, v - 0.586) > 0.007)
    shields = (np.abs(v - 0.604) < 0.004) & (np.abs(u - 0.575) < 0.06) & (np.mod(u - 0.515, 0.012) < 0.007)
    col = np.where(sail[..., None], np.where(stripe[..., None], np.array(_hex("#a3222b")), np.array(_hex("#e6d6ae"))), col)
    col = np.where((mast | hull | prow | stern)[..., None], np.array(_hex("#24160e")), col)
    col = np.where(shields[..., None], np.array(_hex("#d9a441")), col)
    return col


def scene(u, v):
    col = _above(u, v)
    w = v >= HORIZON
    if w.any():
        mv = HORIZON - (v - HORIZON) * 1.15
        mu = u + 0.004 * np.sin(v * 260) * (v - HORIZON) * 8
        refl = _above(mu, mv)
        water = refl * 0.55 + np.array(_hex("#0a1428")) * 0.45
        ripple = (np.sin(v * 520 + np.sin(u * 30) * 2) > 0.6)
        water = np.where(ripple[..., None], water * 0.82, water)
        glitter = (np.abs(u - 0.84 + 0.01 * np.sin(v * 90)) < 0.012 * (1 - (v - HORIZON) * 1.6)) & \
                  (np.sin(v * 420) > 0.2)
        water = np.where(glitter[..., None], np.array(_hex("#f2e6b8")), water)
        col = np.where(w[..., None], water, col)
    return _ship(u, v, col)


def picture(W, H, ss=3):
    """W x H pixel colours ('#rgb'), supersampled ss x ss and dithered to 12-bit colour."""
    return P.quantize(P.sample(scene, W, H, ss))


def mosaic(key, title, idea, grid, W, H, width, above=2.0, dx=0.0, ss=3):
    px = picture(W, H, ss)
    pitch = width / W
    S = P.glyph_size(pitch)                                           # neighbours overlap a hair: no seams
    a = Art(key, title, idea, origin=(0, 0), grid=grid, unlit="path", mode="detailed")
    a.plane_kw = {"independent": True}
    x0 = grid.center_x + dx - (W - 1) / 2 * pitch
    y0 = grid.top_y + above * 20 + pitch / 2
    for r in range(H):
        for c in range(W):
            a.add("■", x0 + c * pitch, y0 + (H - 1 - r) * pitch, size=S, color=px[r][c])
    return {"art": a}


LADDER = [(60, 40), (96, 64), (128, 85), (156, 104), (200, 133)]


def _rung(k):
    W, H = LADDER[k]
    n = W * H

    def piece(grid):
        note = {3: " Épp a 16 383-as sub-mesh határ alatt.", 4: " A 16 383-at túllépi: a TMP egy második sub-meshbe "
                "teszi a többit (kódból; ezt teszteljük)."}.get(k, "")
        return mosaic(f"g4_ladder{k + 1}", f"{k + 1} · [részletes] Pixel-létra {W}×{H} = " + f"{n:,}".replace(",", " ") + " pixel, 1 sign",
                      f"Ugyanaz a jelenet egyre nagyobb felbontásban, 7,5 m széles keretben.{note}", grid, W, H, 300)
    piece.__name__ = f"ladder{k + 1}"
    return piece


def giant(grid):
    """55 200 pixels from block 6's one sign, 40 m wide, high above the whole row: past the 99 999
    visible-character cap (glyph + <br> per pixel) its bottom ~18 rows should be missing."""
    W, H = 300, 184
    # block 6 sits 40 m along the row from block 2; the row's middle is 15 m from block 2
    return mosaic("g4_giant", "6 · [részletes] ÓRIÁS: 300×184 = 55 200 pixel, 1 sign, a sor fölött",
                  "40 m széles kép egyetlen signből, 12 m-rel a gépek fölött, a sor közepe felett. A TMP alapból 99 999 "
                  "karaktert mutat egy szövegben (a sortörés is számít): ha ez a valódi plafon, a kép alsó ~18 sora "
                  "hiányozni fog.", grid, W, H, 1600, above=24, dx=-1000, ss=2)


PIECES = [_rung(k) for k in range(5)] + [giant]
