# Limits of sign pictures and sign animation

What one sign can show and how fast it can change. Every claim is verified in game (galleries 7-10,
SignLink measurements on 2026-10-08) or derived from the code with the reference; "unverified" otherwise.
Units: 1 wu = 2.5 cm; local TMP units are 5 cm (2 wu).

## Pictures: pixels per sign

| Limit | Value | Source |
|---|---|---|
| Visible characters per sign | 99 999; later ones are hidden; line feeds count, tags do not | `TMP_Text.cs:405`, `TextMeshProUGUI.cs:2946`; gallery 8 giant lost exactly the predicted tail |
| **Pixels per sign** | **50 000** in every one-glyph-per-line encoding (glyph + line feed = 2 visible characters) | gallery 8 |
| Glyphs per mesh | 16 383, but TMP spills into another sub-mesh: no cap | gallery 8, 26 600 glyphs whole |
| Text length in a save | none (length-prefixed strings) | gallery 8: 1.23 M characters in one sign loaded |
| Numbers inside tags | <= 32 767 (pos, voffset, indent, size ...) | `TMP_Text.cs:5270` |
| **One sign over the network** | the ZDO must stay **under 512 KiB**: Steam's largest reliable message (`k_cbMaxSteamNetworkingSocketsMessageSizeSend`). A bigger ZDOData message is refused; Valheim logs "Failed to send data", keeps it queued and stops sending on that connection (`ZSteamSocket.cs:263-281`). Single player has no limit. Safe target: <= 400 KB of UTF-8 text per sign | Steam headers; code read, not tried in game |

Shapes within the 50 000 pixels: 250 x 200, 280 x 178 (16:10), 298 x 168 (16:9), 223 x 223. The encoding
decides the cost per pixel and the physical extent.

| Encoding | Characters per pixel (UTF-8 bytes) | Pixels | Physical size |
|---|---|---|---|
| Column (`<indent>` + `<line-height>`, default) | 5.9 (7.9) for flat pixel art, 8.1 (10.1) for dithered pictures | <= 50 000, and pixels x pitch (wu) <= ~128 000 | any; the bound comes from voffsets <= 32 767 (one column climbs back by H x pitch / 2) |
| Fallback (`<line-height=0>`, pos + voffset per pixel) | ~21 (~23) | <= 50 000 | positions up to the tag limit (hundreds of metres); the art still vanishes when the board is unloaded (64-96 m) |
| Rows (`<mspace>` = `<line-height>`) | 4-6 | ~99 999 minus one per row | one board wide (0.86 m wrap box) |

Examples of the column bound (a pixel of p wu is 2.5 p cm):
- 50 000 pixels with pixels up to 2.5 wu (6.25 cm): at most 15.6 x 12.5 m;
- 120 x 80 pixels at 10 wu (25 cm): 30 x 20 m;
- the fjord, 60 x 40 at 5 wu: 7.5 x 5 m.

It bounds the physical size for a pixel count, never the smallest size. Bigger pictures need fewer pixels,
the fallback encoding, or several signs.

**Verified with a photo-like picture (2026-10-08):** `image_import.py` put Van Gogh's Starry Night on one
sign:
- 249 x 197 = 49 053 pixels, 24-bit colour, 1.6 wu (4 cm) pixels, 10 x 7.9 m;
- 546 786 characters (630 KB) and 98 105 of the 99 999 visible characters;
- written live through SignLink in 1.5 s (ZDO write, parse, mesh).

630 KB is fine in single player but over the 512 KiB network limit. The importer caps a sign at 400 KB by
default: the Starry Night then becomes 196 x 155 = 30 380 pixels at 24-bit (391 KB). With 12-bit dither
(about 10 instead of 13 bytes per pixel), about 40 000 pixels fit.

**Pixels per sign, in short:** 50 000 in single player; about 30 000 (24-bit) or about 40 000 (12-bit
dithered) when the world is shared. Several signs side by side go beyond either.

**Several signs stacked: one picture for vanilla clients (verified 2026-10-08).** `vanilla_hq.py` splits a
picture into bands of whole rows, each under 400 KB, and puts every band on its own copy of one sign: SignLink
clones the sign at the same rotation, 0.5 mm further along its normal each. All bands are drawn in one plane,
with no parallax and no seam. In game:
- the Starry Night at 499 x 395 = 197 105 pixels, 12.5 x 9.9 m, on 7 signs;
- the GIF's first frame at its native 360 x 640 = 230 400 pixels, 6.75 x 12 m, on 7 signs.

The pictures are ordinary sign text, saved in the world, so they need no mod to be seen.

**Save check (save 1497, read-only copy).**
- `vsave.py verify`: every chunk re-serialises byte for byte.
- The chunk index matches the files: 2 876 363 ZDOs in 260 chunks.
- Both stacks are saved: 7 signs each, 0.5 mm apart, same rotation, every sign ZDO 93-394 KiB, all under
  512 KiB.
- Against the 13:55 auto backup only the sign count changed (-50 deleted, +18 created), besides the usual
  creature churn.
- A vanilla client loaded the world without errors.

**Network (seen on a dedicated server, 2026-10-11).** A player who arrives receives every stacked sign, about
2.7 MB per picture here. The server sends each player the objects around them nearest first, each one whole
(`ZDOMan.ServerSortSendZDOS`, distance only), and stops filling a player's send queue above 10 KiB
(`ZDOMan.SendZDOs`); the queue drains at Steam's 150 KB/s per connection (`ZSteamSocket` SendRateMin/Max
153600). A multi-megabyte picture next to the player therefore holds back every object behind it. The author
put three very large pictures (the red level of 0.20.30) on their own dedicated server; after logging in next to
them their base did not show for about 2 minutes, until the pictures had arrived. It is the same for vanilla
players and on every login; within one session a sign that did not change is not sent again
(`ZDOPeer.ShouldSend`). Single player has no download. SignLink 0.20.31 counts the wait as download plus build
(`SaveCost.WaitSeconds`, red above 10 s); on a server the area limit is 600 KB for SignLink signs within 100 m (`SaveCost.AreaLimit`).

**Opacity.** Pixel glyphs only a few screen pixels wide are drawn half transparent: the SDF shader's edge
ramp covers the whole glyph, and the Starry Night looked see-through from a distance. With `--cover 2`
every pixel glyph is two pitches wide, so each point lies under 2 x 2 glyphs. The layers add up to an opaque
picture, and up close the last-drawn one wins, so it stays sharp. It costs no characters: the size is one
tag. The author on the GIF frame: "perfect, zero transparency and a high-quality picture".

**Half transparency (SignLink 0.15.0).** A glyph's colour tag carries alpha (`<#rrggbbaa>`, or `<#rgba>` when
exact), and TMP blends overlapping glyphs of one sign in line order. Under cover 2 every point lies under four
glyphs, so a pixel of alpha a is written with `1 - (1 - a)^(1/4)` (`PixelText.ForCover`) and the four show a again
(within 3/255 after rounding; edges, under fewer glyphs, come out a little fainter). Alpha under 8/255 is left out,
from 248/255 on it is solid: Lanczos rings a little at hard alpha edges (`Resampler.MinAlpha`, `SolidAlpha`).
Unverified in game.

**Colour depth.** Tags are free for the visible-character cap, so depth only costs characters and bytes:

| Tag | Colours | Characters per colour change |
|---|---|---|
| `<#rgb>` (default, with 4x4 Bayer dither) | 4 096 | 6 |
| `<#rrggbb>` | 16.7 M | 9 (the fjord grows by a third) |
| `<#rgba>`, `<#rrggbbaa>` | plus 16 / 256 alpha levels | 7 / 11 |

The unlit font path (`<font=valheim_fonts/valheim-norse>`) shows the colours as written. Without it,
the scene light shades them. SignLink writes it by default and leaves it out when *Glowing pixels* is off
(`vanilla_hq.bands(font=None)`, `PixelText.ColumnText(glow: false)`): the ■ comes from NotoSansJP either way, so the
picture should sit in the same place (unverified in game). A colour tag is written only when the colour changes, so
flat pixel art is cheaper than dithered pictures.

**Size in the world save.** Sign objects are saved in the `.chunk` files, which are not compressed: a sign
costs its text in UTF-8 (with a length prefix) plus about 100 bytes for the object (position, rotation, prefab,
author). A picture over 7 signs of about 390 KB adds about 2.7 MB. SignLink shows this as "in the world save"
(`SaveCost`).

**Float precision: the line height must be a multiple of 1/4.** TMP reads tag numbers in float32, the
n-th decimal as digit x 0.1f^n (`TMP_Text.ConvertToFloat`). It adds the line height to the line offset at
every line break, also in float32 (`TextMeshProUGUI.cs:2571`).

The column encoding has one line per pixel, so the offset runs into the tens of thousands. A pitch like
0.8 is neither read nor added exactly: over 49 000 lines it drifts down by 2.7 local units and then up to
+19, and the first Starry Night showed a kink in the middle (2026-10-08).

Multiples of 1/4 are read and added exactly (simulated over 50 000 lines). `engine.exact_line_height`
therefore snaps to them, with the voffsets absorbing any rest, and `image_import.py` uses pitches in steps
of 0.5 wu.

## Cost of drawing a frame

Measured with SignLink (local mode, 2026-10-08), the time to set a text and rebuild its TMP mesh:

| Pixels | Characters | Apply time (avg / max) | Per pixel |
|---|---|---|---|
| 256 | 2 534 | 4.7 / 7.5 ms | 18.5 µs |
| 1 024 | 9 179 | 14.7 / 18.1 ms | 14.3 µs |
| 4 096 noisy | 34 709 | 56.1 / 71.0 ms | 13.7 µs |
| 4 096 one colour | 10 144 | 53.9 / 61.3 ms | 13.2 µs |
| 16 384 | 134 636 | 221.6 / 256.9 ms | 13.5 µs |
| the cat (371) / the fjord (2 400) | 2.2 k / 19.4 k | 5.6 / 32 ms | 15 / 13 µs |

**About 13.5 µs per pixel plus ~1 ms per frame**, nearly independent of the colour tags: glyph count is
what costs. The game pays the same once whenever a sign's text appears: a 50 000-pixel sign takes about
0.7 s when it loads.

**Texture screens (SignLink 0.3, for players with the mod).**
- A RawImage on the sign's canvas shows RGB frames; a new frame is one texture upload.
- Measured with the 360 x 640 GIF at 262 x 466: 32 of 33.3 fps, 0.05 ms per frame (text frames of 35 x 62
  took 34 ms), and no FPS drop.
- The resolution and frame rate of a screen are practically free. Vanilla clients see the sign's text
  instead.

## Animation: frame rates

| Path | Who sees it | Ceiling | What limits it |
|---|---|---|---|
| Local (SignLink sets the mesh) | this client | the game's frame rate | main-thread budget: pixels x fps x 13.5 µs |
| Networked (`zdo`, `Sign.SetText`) | everyone | 20 frames/s sent (`ZDOMan.cs:909-915`) | bandwidth (Steam caps a connection at 150 KB/s, `ZSteamSocket.cs:79-83`, shared with all game traffic); the viewer's refresh |
| ... seen by vanilla clients | | 0.5 frames/s, random phase per client | `Sign.cs:39`: texts are re-read every 2 s |
| ... seen by SignLink clients | | 0.5 frames/s by default; up to 10 frames/s with a lower RefreshSeconds | FastRefresh every RefreshSeconds (2 s by default, 0.1 s at the least) |
| Vanilla `<sprite anim>` | everyone, no mod | any | only the 16 EmojiOne faces |

**Budget rule.** Keep animations to about 10 % of the main thread (100 ms per second): `pixels x fps <= ~7 500`.
Networked, keep one animation to about 30 KB/s: `pixels x fps <= ~3 000` (10 bytes per pixel, the
unchanged signs of a group are not resent).

| Picture | Local (10 % budget) | Networked (30 KB/s) |
|---|---|---|
| sprite <= 400 px (the cat) | 12-18 fps | 6-10 fps |
| 1 000 px | 7 fps | 3 fps |
| 2 400 px (the fjord) | 3 fps | 1.2 fps; 1.5 with bands (land not resent) |
| 5 000 px | 1.5 fps | 0.6 fps |

**Recommended:** small sprites at 6-10 fps, pictures up to 2-3 k pixels at 2-4 fps, and only the moving
part of a large picture on its own sign (bands). For vanilla viewers, use slow, few-frame loops (one frame
per 2 s). Storing the frames on the sign once and letting every modded client play them locally (SignLink
does this: saved animations) removes the network cost per frame. Then only the local budget applies.
