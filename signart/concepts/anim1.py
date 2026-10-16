"""Animation 1 (2026-10-08): the gallery-8 fjord, moving. Frames for SignLink (mod/SignLink, live.py anim).

Everything that moves is periodic in t (0..1), so the loop is seamless: the aurora curtain drifts and its rays
shimmer, stars twinkle, smoke rises from the longhouse, ripples and the moon glitter run over the lake, and
the lake mirrors the moving sky. Every frame is one sign text in the column encoding (engine.column_cmds),
positioned relative to the sign itself: the picture is centred over the sign, its bottom `above` wu higher.
"""
import numpy as np

import pixels as P
from concepts.row3 import HORIZON, SKY, _grad, _hex, _ship

TAU = 2 * np.pi


def _above(u, v, t):
    col = _grad(v, SKY)
    c = 0.17 + 0.06 * np.sin(6 * u + 1 + TAU * t) + 0.03 * np.sin(17 * u - 2 * TAU * t)
    rays = 0.55 + 0.45 * np.sin(42 * u + 3 * np.sin(7 * u + TAU * t) - 2 * TAU * t) ** 2
    I = np.exp(-((v - c) / 0.07) ** 2) * rays * (v < 0.45)
    top = np.clip((c - v) / 0.12 + 0.5, 0, 1)[..., None]
    aur = np.array(_hex("#39ff9c")) * (1 - top) + np.array(_hex("#8a5cff")) * top
    col = col + I[..., None] * aur * 0.85
    gx, gy = np.floor(u * 140), np.floor(v * 140)
    h = np.sin(gx * 12.9898 + gy * 78.233) * 43758.5453
    h = h - np.floor(h)
    sx, sy = (gx + 0.2 + 0.6 * ((h * 7) % 1)) / 140, (gy + 0.2 + 0.6 * ((h * 13) % 1)) / 140
    d = np.hypot(u - sx, v - sy)
    star = (h < 0.09) & (v < 0.42) & (d < 0.0016 + 0.0012 * ((h * 31) % 1))
    blink = 0.55 + 0.45 * np.cos(TAU * (np.floor(h * 997) % 3 + 1) * t + TAU * ((h * 53) % 1))
    col = np.where(star[..., None], np.maximum(col, (0.95 * blink)[..., None]), col)
    dm = np.hypot(u - 0.84, v - 0.13)
    moon = (dm < 0.045) & (np.hypot(u - 0.858, v - 0.118) > 0.040)
    col = col + (np.exp(-((dm - 0.04) / 0.03) ** 2) * 0.18 * ~moon)[..., None] * np.array(_hex("#fff4d0"))
    col = np.where(moon[..., None], np.array(_hex("#fff4d0")), col)
    f = 0.47 - 0.07 * (0.6 * np.sin(5.1 * u + 0.3) + 0.3 * np.sin(11.7 * u + 1.2) + 0.1 * np.sin(23 * u))
    m = v > f
    snow = m & (v < f + 0.022 + 0.01 * np.sin(40 * u)) & (f < 0.445)
    col = np.where(m[..., None], np.array(_hex("#2a2850")) + (v - f)[..., None] * 0.4, col)
    col = np.where(snow[..., None], np.array(_hex("#dfe4f2")), col)
    g = 0.565 - 0.025 * np.sin(3.3 * u + 2) - 0.012 * np.sin(9 * u)
    col = np.where((v > g)[..., None], np.array(_hex("#1c3326")), col)
    rng = np.random.RandomState(4)
    for ui in np.linspace(0.0, 1.0, 46) + rng.uniform(-0.008, 0.008, 46):
        if 0.10 < ui < 0.25 or 0.47 < ui < 0.67:
            continue
        gi = 0.565 - 0.025 * np.sin(3.3 * ui + 2) - 0.012 * np.sin(9 * ui)
        hi = rng.uniform(0.03, 0.065)
        tt = (v - (gi - hi)) / hi
        tree = (tt >= 0) & (tt <= 1.05) & (np.abs(u - ui) < hi * 0.33 * np.clip(tt, 0, 1))
        col = np.where(tree[..., None], np.array(_hex("#0d1c14")), col)
    house = (u > 0.125) & (u < 0.215) & (v > 0.585) & (v < 0.62)
    roof = (v > 0.555) & (v <= 0.585) & (np.abs(u - 0.17) < 0.052 * (v - 0.555) / 0.03)
    col = np.where(house[..., None], np.array(_hex("#3a2416")), col)
    col = np.where(roof[..., None], np.array(_hex("#5a3a1e")), col)
    flicker = 0.8 + 0.2 * np.sin(TAU * 5 * t) * np.sin(TAU * 3 * t + 1)
    win = ((np.abs(u - 0.145) < 0.006) | (np.abs(u - 0.195) < 0.006)) & (v > 0.595) & (v < 0.606)
    col = np.where(win[..., None], np.array(_hex("#ffcf6a")) * flicker, col)
    door = (np.abs(u - 0.17) < 0.007) & (v > 0.6)
    col = np.where(door[..., None], np.array(_hex("#140b06")), col)
    for k in range(8):                                   # puffs rise one slot per loop, fading in and out
        s = k + t
        sxk, syk = 0.205 + 0.012 * s + 0.004 * np.sin(s + TAU * t), 0.55 - 0.028 * s
        puff = np.hypot(u - sxk, (v - syk) * 1.3) < 0.007 + 0.002 * s
        w = 0.6 * min(1.0, s) * min(1.0, 8 - s)
        col = np.where(puff[..., None], col * (1 - w) + np.array(_hex("#a4a8b4")) * w, col)
    return col


def scene(u, v, t):
    col = _above(u, v, t)
    w = v >= HORIZON
    if w.any():
        mv = HORIZON - (v - HORIZON) * 1.15
        mu = u + 0.004 * np.sin(v * 260 - TAU * t) * (v - HORIZON) * 8
        refl = _above(mu, mv, t)
        water = refl * 0.55 + np.array(_hex("#0a1428")) * 0.45
        ripple = np.sin(v * 520 + np.sin(u * 30) * 2 - 3 * TAU * t) > 0.6
        water = np.where(ripple[..., None], water * 0.82, water)
        glitter = (np.abs(u - 0.84 + 0.01 * np.sin(v * 90 + TAU * t)) < 0.012 * (1 - (v - HORIZON) * 1.6)) & \
                  (np.sin(v * 420 - 2 * TAU * t) > 0.2)
        water = np.where(glitter[..., None], np.array(_hex("#f2e6b8")), water)
        col = np.where(w[..., None], water, col)
    return _ship(u, v, col)


def picture(W, H, t, ss=2):
    return P.quantize(P.sample(lambda u, v: scene(u, v, t), W, H, ss))


def fjord(n=24, W=60, H=40, width=300.0, above=40.0):
    """n frames of the moving fjord (default: 60 x 40 pixels, 7.5 m wide, 1 m above the sign)."""
    return P.sign_frames([picture(W, H, k / n) for k in range(n)], width / W, above)


# three bands on three signs of a gallery block (same layer and depth): sky, land, water. The land band is
# still, so after the first frame only the sky and the water are rewritten (SignLink skips unchanged texts).
BANDS = {("A1", 1): (0, 15), ("C1", 1): (15, 25), ("C3", 1): (25, 40)}


def fjord_bands(grid, n=24, W=60, H=40, width=300.0, above=2.0, still_land=True):
    """{"A1-1": [text per frame], ...}: the moving fjord over a gallery block, split into BANDS (rows)."""
    p = width / W
    pics = [picture(W, H, k / n) for k in range(n)]
    out = {}
    for (cell, layer), (r0, r1) in BANDS.items():
        out[f"{cell}-{layer}"] = [
            P.column_text(P.items(pics[0] if still_land and r0 == 15 else pics[k], p, grid.center_x,
                                  grid.top_y + above * 20, cell, layer, rows=range(r0, r1)), grid)
            for k in range(n)]
    return out
