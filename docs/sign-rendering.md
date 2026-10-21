# How a Valheim sign renders: the facts behind the engine

Everything here is read from the game (client 1.0.16, its `Unity.TextMeshPro.dll` and asset bundles)
or measured in game; anything else is marked **unverified**. The author logs in-game results in
`signart/ingame_tests.json` on their own computer (not in this repository); the simulator shows the log when it is
there.

Code references: `Sign.cs`, `TextInput.cs` = decompiled `assembly_valheim` (see the sibling
`valheim-world-tool` repo); `TextMeshProUGUI.cs`, `TMP_Text.cs`, `TMP_InputField.cs` = the game's
`Unity.TextMeshPro.dll` decompiled with ilspycmd 9.1 (not committed; regenerate the same way).

## 0. Two modes

| | **Typable** (`Art(mode="typable")`, default) | **Detailed** (`mode="detailed"`) |
|---|---|---|
| Who can build it | anyone, by copy-pasting commands into signs | a save edit (`rig.py`, `row.py`) or SignLink's window |
| Per sign | one glyph, <= 50 UTF-16 characters | any number of glyphs (`<line-height=0>` + `<br>`), no limit |
| Layout | `Art.build` / `assign_cells`: touching glyphs share a cell, tagged rows share a layer (plane) | `build_planes` / `assign_planes`: a whole piece in one stack |
| Glow | 5-character `<font>` alias + one loader sign | full `<font=valheim_fonts/valheim-norse>` path |
| Where it shows | simulator (copy buttons), `round*.py` | `row.py` renders, `concepts/row*.py` |

Gallery 3 (`concepts/row2.py`, world `MyWorld_gallery3`) shows both: blocks 1-3 typable
(round 9), blocks 4-6 detailed.

## 1. The sign prefab

`sign` lives in SoftRef bundle `c4210710`.

| Part | Value | Meaning |
|---|---|---|
| Canvas RectTransform | 1.0 x 0.5 (m) | the board |
| Text RectTransform | 17.29 x 8.05 units, local scale 0.05 | 1 m = 20 TMP units |
| TextMeshProUGUI | font `Valheim-Norse`, fontStyle **Bold**, color #1E1A12 | no color tag = dark brown text |
| alignment | Center (2) / Middle (512) | both axes centered |
| auto-size | on, 1..8 | irrelevant once `<size=N>` is absolute |
| wrapping | Normal, overflow Overflow | a 2nd glyph wraps (17-unit box) |
| rich text | on, `m_isOrthographic` = 1 | tag pixels = local units |
| `Sign.m_characterLimit` | 50 | `Sign.cs:16`, applied by `TextInput.cs:94` |

The 50-character limit is a property of the input field only (`TMP_InputField.cs:1874` truncates
`m_Text.Length`, the raw UTF-16 string: tags count, an emoji counts 2). The ZDO `text` string itself has
no limit, so a save editor or a mod can write longer sign texts (section 7c).

## 2. Position model (why 40/20 per sign)

The original calibration found: one physical sign to the right needs `pos -= 40`, one sign down needs
`voffset += 20`; with one empty slot between active signs, 80 and 40. The prefab explains it exactly:

- `<pos=P>` sets the pen x (`TMP_Text.cs:5769`). Center alignment then shifts the line by half its
  `maxAdvance` = `P + advance` (`TextMeshProUGUI.cs:2538`, `:2753`). The glyph moves P/2 units.
- `<voffset=V>` raises the baseline (`TMP_Text.cs:5792`). With a non-zero offset the line's extent
  keeps **both** the shifted and the unshifted ascender/descender (`TextMeshProUGUI.cs:1958`), so
  Middle alignment (`:2696`) re-centres by half of V. The glyph moves V/2 units.
- P/2 local units x 0.05 m = P x 2.5 cm. One sign (1 m) = 40 pos; half a metre (sign height) = 20 voffset.

The engine therefore works in **world units (wu) = 2.5 cm**, x right, y up, origin at the A1 board,
cell (col,row) at (80*col, -40*row). For one glyph per sign the glyph's bounding-box centre lands at

```
X = P + 2*sx*S*((xMin+xMax)/2 - adv/2) + 80*col
Y = V - S*(A+D) + S*(yMin+yMax)        - 40*row
```

with S = `<size>`, sx = `<scale>`, glyph metrics in em, and A/D the ascent/descent line of the glyph's
TMP font asset (table in section 4). Glyph **sizes are not halved**: a size-S glyph's em is 2*S wu.

Consequences the old model missed:

- Same voffset does not mean same centre: the size term makes a bigger glyph sit lower. Visible on the
  10-layer screenshot (rings thicker at the bottom); the engine corrects it.
- A size-70 `■` is ~112 wu wide, so the 3x3 proof grid (120 wu pitch) has 8 wu gaps, as photographed.
- Negative pos behaves symmetrically (the formula is linear); verified in game 2026-10-08.

`<rotate=R>` turns the glyph quad around its centre, counter-clockwise (`TextMeshProUGUI.cs:1935`).
`<scale=sx>` multiplies the glyph's x metrics *before* the rotation (`:1910`), so scale+rotate gives a
rotated ellipse/bar. Verified in game. The SDF corner rounding is stretched with it (slightly
chamfered corners at large sx).

**How wide a stretched block is drawn** (measured in game 2026-10-17, the block `■` from NotoSansJP-Thin SDF, sizes 1.5
and 4, sx 0.15 to 6, lit and glowing alike): its ink is `S*(0.726*sx + 0.089)` em wide and `0.793*S` em tall, centred
where the formula above puts it (an em is 2*S wu). Part of the width does not stretch: TMP pads a bold glyph's quad
sideways by a fixed amount (`style_padding`, not multiplied by `<scale>`), and the sign's heavy dilation fills the
quad. The engine stretches the whole outline (0.8 em plus bold, times sx), so for this glyph it draws a wide block too
wide and a narrow one too narrow (sx 6.77: 3 % short in the game; sx 0.17: 60 % wide). At sx = 1 both agree within
3 % (the size-70 grid above). The mod's text templates use the measured numbers (`Snippets.BlockA/BlockC/BlockH`): a
QR code drawn with the outline model was not read by a phone, drawn with these it is (zbar reads a game screenshot).
Other glyphs under `<scale>` were not measured (unverified).

## 3. Tags worth knowing

| Tag | Cost | Status |
|---|---|---|
| `<#rgb>` / `<#rgba>` / `<#rrggbb>` | 6 / 7 / 9 | ok; alpha ok (translucent layers) |
| `<size=N>` `<voffset=N>` `<pos=N>` | 9-11 | ok; a leading `.` parses (`TMP_Text.cs:5376`), verified with `<scale=.3>` |
| `<rotate=N>` | 10-12 | ok |
| `<scale=N>` | 9-11 | ok (horizontal stretch); `<scale=-1>` mirrors the glyph (verified): animal pairs can face each other |
| `<line-height=0>` | 16 | ok: several glyphs per sign on one baseline, each line centred on its own |
| `<width=N>` | 9+ | useless: can only narrow the wrap width (`TextMeshProUGUI.cs:1999`) |
| `\n` (typed backslash + n) | 2 | ok: a line feed, like `<br>` (4); TMP parses escapes itself (`TMP_Text.cs:2357-2440`), even inside `<noparse>`; `\\` gives a literal backslash |
| `\uXXXX`, `\UXXXXXXXX` | 6 / 10 | ok: any character by its code (`\u16A0` = ᚠ, `\U0001F43A` = 🐺) |
| `\t` | 2 | a tab, but one tab stop is wider than the wrap box at size 7: the rest wraps |
| `<sprite=N>` | 11+ | ok: TMP's EmojiOne sprite sheet, exactly 16 faces (0-15); `<sprite=16>` shows as text; `tint=1` takes the colour; `<sprite="EmojiOne" index=N>` works too |
| `<sprite anim="a,b,fps">` | 25+ | ok: **animates in vanilla**, client-side (`TMP_SpriteAnimator`), cycling faces a..b |
| `<mspace=N>` | 10+ | ok: fixed advance N (no faux-bold spacing in the mono branch, `TextMeshProUGUI.cs:2448-2450`) |
| `<line-height=N>` (N > 0) | 15+ | ok: line pitch N; a 4x3 grid with `<mspace=4><line-height=4>` is seamless |
| `<indent=N>` | 10+ | ok: pen start N on this and every following line |
| `<nobr>` | 6 | cannot keep a long run on one line: TMP breaks a line's first word between characters (`TextMeshProUGUI.cs:2642-2649`) |
| `<alpha=#hh>`, `<mark>`, `<s>`, `<u>`, `<cspace>`, `<sup>`/`<sub>` | | exist in TMP, not used |

**Value range.** Numbers in tags are parsed by `TMP_Text.ConvertToFloat` (`TMP_Text.cs:5270`): a value
above **32767** makes the tag invalid (it then shows as plain text); below that there is no clamp —
`<size=N>` sets the size directly (`TMP_Text.cs:5850`), the 1..8 auto-size range only applies to text
without a size tag. So size, pos, voffset, rotate and scale all go up to 32767 (a size-32767 glyph's em
is 1.6 km). Practical limits are elsewhere: the 50-character budget, the softening of SDF corners at
large sizes, and the loading distance below.

**Loading distance.** The sign prefab's `ZNetView` is not `m_distant`, so a sign exists on a client
only inside the active area: Chebyshev distance <= 1.5 zones (96 m) from the centre of the player's
zone (`ZNetScene.cs:390`), i.e. roughly 64-96 m from the player. The art disappears when the player is
farther than that from the **boards**, however large or far away the projected picture is.

Without `<line-height=0>` every glyph after the first wraps to a new line (the box is 17 units wide),
and the line gap includes the voffset (`:2097`-`:2110`): the glyphs end up 15-20 m apart.

## 4. Which glyphs exist: the font chain

Sign text resolves characters through the TMP font assets in bundle `8d5dbad8` (TTFs in `resources.assets`):

| Asset | Source TTF | ascent / descent (pt 48) | faux bold | provides |
|---|---|---|---|---|
| Valheim-Norse -> bold typeface Valheim-Norsebold | Norse / Norsebold | 44.16 / -8.64 | real bold | Latin, Latin-1 (Hungarian accents), **81 runes** |
| Fallback-NotoSansThin | NotoSans-ExtraLight | 51.31 / -14.06 | 0.10 | Greek, punctuation |
| NotoSansJP-Thin SDF | NotoSansJP-Thin | 55.68 / -13.82 | 0.45 | geometric shapes, blocks, box drawing, arrows, math, misc symbols |
| NotoSansKR/SC-Thin | | 55.68 / -13.82 | 0.45 (assumed) | a few stragglers |
| NotoEmoji-Light SDF | NotoEmoji-Light | 44.53 / -11.72 | 0.30 | ~1,200 monochrome emoji (line art) — verified |

Fallback order after NotoSansThin: Armenian, Bengali, Devanagari, Georgian, JP, KR, Malayalam, SC, Thai,
Emoji, Arabic, Hebrew. `./sign.sh extract` resolves ~1,190 useful glyphs and stores their outlines;
`./sign.sh sheet` draws contact sheets of them.

Useful families:

- **Masses:** `■ ● ◆ ▲ ▼ ◀ ▶ ★ ◢ ◣ ◤ ◥` (right triangles give arbitrary diagonal edges), half circles
  `◐ ◑ ◒ ◓`, blocks `█ ▀ ▄ ▌ ▐ ▖▗▘▝▙▚▛▜▞▟`, thin bars `▏▕▁▔`.
- **Carved strokes (Norsebold):** `| / \ - = ~ ^ < > ( )` — rough, chunky, very Norse. `|` is a 9:1 bar,
  a free line primitive without `<scale>`.
- **Runes:** `ᚠ ᚢ ᚦ ᚨ ᚱ ᚲ ᚷ ᚹ ᚺ ᚾ ᛁ ᛃ ᛇ ᛈ ᛉ ᛊ ᛏ ᛒ ᛖ ᛗ ᛚ ᛜ ᛞ ᛟ` and more, plus `᛫ ᛬ ᛭`.
- **Rings:** `◯` (thin), `⭕` (emoji, very thick: inner radius ~55 %), `◎ ◉`.
- **Line-art emoji:** 🐺 🐉 🐲 🦌 🐍 🦅 🐗 🐻 🪓 🗡 ⚔ 🛡 🔥 🌲 🌳 ⛵ 🌊 🌙 🌕..🌘 (filled moon phases) 👁 💀 ⚡.

## 4b. Lighting and materials

The sign's text material is `Valheim-Norse - Sign Lit` with the shader
**`TextMeshPro/Distance Field (Surface)`**: it is lit like a board (sun, ambient, torches). With the sun
behind the picture every colour turns dark (verified 2026-10-08: gold dragons came out dark olive, the
red shield dark brown, bone runes bluish). At night it is dark too. Practical consequences:

- Strong silhouettes read in every light; colour detail only in front light.
- Translucent glyphs (`<#rgba>`) let the bright sky through and stay light in backlight (stained glass).
- The simulator's light presets (front / backlight / dusk / night) multiply glyph colours with rough
  factors fitted to that screenshot; `build.py` also renders `*_back.png`.

The font assets' own materials use the unlit **`TextMeshPro/Distance Field`** shader, and they are in
`Resources` (`globalgamemanagers` container: `fonts & materials/valheim_fonts/valheim-norse`, `.../valheim-rune`,
the Noto fallbacks, ...). `<font=NAME>` and `<material=NAME>` resolve such paths through
`Resources.Load(TMP_Settings.defaultFontAssetPath + NAME)` (`TMP_Text.cs:5867`, `:5936`). **Verified 2026-10-08
(at night): `<font=valheim_fonts/valheim-norse>`, `<material=...>` and the Rune font all render unlit**, full
colour in the dark. The paths are long (33-38 characters), but the lookup has a shortcut:

- After loading, TMP registers the font asset under `GetHashCode(asset name)` (`TMP_FontAsset.cs:638`,
  `MaterialReferenceManager.cs:35`), and a `<font=VALUE>` tag looks up the hash of its value first
  (`TMP_Text.cs:5880`). The hash is `h = h*33 ^ upper(c)` over the value (`TMP_Text.cs:5417`), so any value
  with the hash of "Valheim-Norse" finds it: the name itself (`<font=valheim-norse>`, 20 chars) or a
  collision alias. `font_alias.py` finds them: five CJK characters (`<font=亨杓麩壀辨>`, 12 chars; four cannot
  reach this hash) or seven ASCII (`<font=BTR1GBJ>`, 14).
- The registry is per game session and only fed by a tag that loaded the path. So a **loader sign**
  (`engine.LOADER_TEXT`, draws a small glowing rune at its board) must render before the art's signs.
  Objects are created nearest first, about 10 per frame (`ZNetScene.cs:197`): put the loader closer to
  where people arrive than the art's signs. A sign whose alias failed shows its tag as text and keeps it
  (TMP does not re-parse an unchanged string, `TMP_Text.cs:666`) until it is recreated: walk >100 m away
  and back. Aliases: unverified in game yet.
- Budget: 12 for the alias leaves colour + 2-digit size + voffset + 2-digit pos. Glowing glyphs get no
  rotate/scale/mirror, and pieces stay small, over the build. `Art(unlit=True)` / `add(glow=...)`;
  the simulator renders them at full colour in every light preset.

## 5. Draw order: why cells exist

- Within one sign the mesh order is the character order (later on top) — deterministic.
- Different signs are different canvases; Unity sorts them by camera distance. Signs stacked
  **behind each other** (one cell) keep a stable order from the front: L1 = rearmost, drawn first.
  At least 20 layers per cell are proven.
- **The distance is to each renderer's mesh bounds, not to the sign** (verified 2026-10-15, SignLink 0.20.145).
  A stack's signs are 0.3-0.5 mm apart, but every text renderer (the main mesh and each font sub-mesh) is sorted
  by its own bounds' centre, which sits wherever that layer's glyphs are. On a sign tilted back 10°, the Cauldron's
  rear layer (tripod legs, low and wide) measured 6.03 m from the camera and its front layer (the pot) 6.17 m, so
  the legs drew over the pot; on standing signs it held only by luck of the layout. Fix in the engine
  (`build_planes` with `art.anchors`, set by `presets.one_sign_stack`): each layer gets two tiny invisible glyphs
  per font sub-mesh at the same two corners outside the whole piece, so every renderer of every layer has the same
  bounds and only the stack's own spacing differs. Measured in game: all renderers' centres equal to 0.1 mm apart
  from the layer offset, and the hidden hook reappeared in front of the pot.
- Signs side by side (different cells) swap order as the camera moves: overlapping pixels from two
  cells flicker (the "Christmas tree" incident, and blue winning from front/back only).

Design rule the engine enforces: **glyphs that touch must share a cell**, layered back to front.
A cell holds `depth` signs (5 in the first build). Glyphs that touch nothing go anywhere.

The build is a `Grid` (cols x rows x depth, plus `gap` empty sign slots between active signs, also
fractional; pitch = 40*(gap+1) by 20*(gap+1) wu). 3x3x5 is only the default: a 10x1x10 row of deep
stacks hides more easily, and a single 1x1x10 stack can hold a whole small logo with its background,
because inside one stack everything may overlap. `Art.fit_above(signs, factor)` scales a design and
centres it over the build, its bottom `signs` sign heights above the top boards; the simulator's grid
panel does the same and reassigns the cells in the browser. Scaling down also narrows the gaps between
islands: below about 60-75 % neighbouring shapes may start to touch and need a deeper grid.

**Backdrop cells (experimental).** A round translucent background behind a whole logo touches every
glyph. Instead of one huge cell it can go on a sign placed deep behind the build (`Grid.backdrop`,
cells `BG1..`): if Unity sorts the canvases by camera distance, a sign 3+ m behind the grid is always
farther than the front cells for a viewer in front, so it should always draw first. Untested: test it in game
before building on it.
So a piece is up to nine *islands* of at most five layers plus loose singletons, separated by thin
gaps (>= 4 wu plus faux-bold growth). The gaps read like carving grooves or lead came.

## 6. Command budget (50 characters)

A typical command: `<#da4><size=120><voffset=640><pos=160><rotate=45>◆` = 50. Practical rules,
all applied by the engine automatically:

- 3-digit colours (`<#da4>`); the sign's own dark brown needs no tag at all (ravens, outlines).
- The cell decides pos (`P = X - 80*col`), so the cell assignment prefers cells that keep every
  command <= 50 and pos >= 0.
- Over-limit commands get invisible nudges: scale to one decimal, `.18` instead of `0.18`, pos or
  voffset 100..103 -> 99, size by < 4 % with the centre kept.
- Symmetric glyphs use rotations within +-90; rune rings flip their lower half upright.
- Triangles are rotations of each other (`▲◀▼▶`, `◢◥◤◣`): the engine swaps to the twin that needs
  the smallest rotation (`▲` at 120° is `◀` at 30°).
- Keep art below ~Y 1000 so voffset stays 3 digits.

A command over 50 is a real bug: the input field cuts the tail, which is the glyph itself.

## 7. Review of the original handoff (2026-10-07 session)

Right and kept: the pos/voffset calibration (40/20, sparse 80/40), the global canvas + transform, same
centre for nested layers, the cross-cell overlap rule and the ownership idea, the 20-layer proof, the
A1,B1,C1,... entry order.

Corrected or added:

- The glyph centre is not (pos, voffset): a size- and glyph-dependent offset applies (section 2).
- The render-order explanation is camera-distance sorting of separate canvases (section 5).
- The 50-character input limit, the bold Norse font, and the whole fallback chain were unknown; the
  vocabulary was ~9 glyphs. It is now ~1,190, with runes, carved strokes and line-art emoji.
- The old simulator drew Arial glyphs and tested bounding boxes; the new one draws the game's own
  outlines and tests real pixels, from the command strings themselves (what you type is what you see).
- The old art put one glyph per cell and thought in 3x3 tiles. Cells are draw-order groups, not
  tiles: the art is designed freely and the engine assigns cells.

The handoff package itself (screenshots, concept art, the old simulator) was removed from the repo on
2026-10-08; it is in the git history up to commit 65fea15 (`docs/handoff/`).

## 7b. The test rig (measured 2026-10-08)

Labelled in game and scanned from save 1487 (a rig, kept out of the repository: it holds real coordinates): 9 cells x 5
layers, yaw 67.5°. Columns are exactly 2.00 m apart (80 wu); rows are not: A->B 1.10-1.14 m (43-45.5 wu),
B->C 0.99 m, and B/C sit 2-3 wu to the side. Layers step 9 cm along the sign normal, label 1 rearmost.
Concepts built on the rig grid use these measured offsets instead of the ideal 80/40.

## 7c. Several glyphs in one sign (save edits)

With `<line-height=0>` and a line break (`<br>` or a newline) before every glyph after the first, one
sign draws any number of glyphs, in character order (one font: one sub-mesh). From the decompiled
`TextMeshProUGUI.cs`:

- line-height 0 never moves the baseline (`m_lineOffset += m_lineHeight`, `:2571`); every line starts
  with the pen at 0, so `<pos>` works per line, and each line is centred on its own (its `maxAdvance`);
- Middle alignment centres the block on `(m_maxTextAscender + maxVisibleDescender) / 2` (`:2696`), where
  the ascender is the **first** line's (only taken while `m_lineNumber == 0`, `:1978`) and the
  descender the **last** line's (overwritten at every line end, `:2512`), each including the voffset.

So, in wu: `Y_i = 2*V_i + S_i*(yMin+yMax)_i - K`, `K = S_0*A_0 + S_L*D_L + max(V_0,0) + min(V_L,0)`
(`engine.layout_lines`; `line_cmds` inverts it, `encode_lines` repeats only changed tags since TMP
carries color/size/voffset/rotate/scale across lines). One line gives back the single-glyph formula.
When every line has the same size, font and voffset, each line lands exactly like a one-glyph sign:
that is the two-glyph case verified in game. Lines with different voffsets: a line moves by its full
voffset relative to the first line, not by half — **verified 2026-10-08** (block 5 of the gallery: 24
runes in three signs, eight heights each, a perfect ring). Mixing fonts inside one sign
changes the draw order between sub-meshes (**unverified**). Typed in game, `<line-height=0>` + `<br>`
cost 16 + 4 characters, so this is mainly for save edits. The simulator does not show multi-glyph signs
yet (Python only: `row.py` renders them).

**Mosaics** (`plane_kw={"independent": True}`): gaps between tiles let the sky show through as a bright
grid (gallery 3); tiles that overlap their neighbours by 0.3 wu hide the seams, and skipping the touch
test keeps them spread over the layer-1 signs (the overlap strip may change owner with the camera,
too thin to see). Unlit tiles keep their exact colours; lit ones look muddy in daylight. Each sign of a
measured rig sits up to ~1 wu off its cell mean, more than the overlap: detailed builds use every sign's
own position (`Grid.sign_positions`, `rig_grid`), or rows break into thin seams where the sign changes.
The test rig is not flat either: the layer-1 signs of B1, A2 and B2 sit 3-4 cm off the depth of
the other six, and that parallax also drew seams (gallery 5). Loose glyphs and the main stack now use
only the signs on the most common depth, and mosaic chunks end where a row ends.

**Limits of one sign.** There is no text-length limit in a save: ZDO strings are length-prefixed
(`ZPackage.Write(string)`, BinaryWriter). 32767 is the largest number *inside* a tag. TMP builds at most
16 383 glyphs into one mesh (`TMP_MeshInfo.cs:48`, 16-bit indices) but then spills into another
sub-mesh with a copy of the material (`TextMeshProUGUI.cs:1225`), so that is not a cap: **verified
2026-10-08**, a 26 600-glyph sign renders whole. The real cap is `TMP_Text.m_maxVisibleCharacters = 99999`
(`TMP_Text.cs:405`; tested per character index at `TextMeshProUGUI.cs:2946`, together with
`m_maxVisibleLines` and `m_maxVisibleWords`, same default): characters from index 99 999 on are hidden, and
line breaks count. **Verified 2026-10-08** with gallery 8 (`concepts/row3.py`): the ladder of 2 400 ... 26 600
pixels is complete, and the 55 200-pixel giant (one glyph plus one `<br>` per pixel, rows top-down)
loses exactly the predicted tail: its bottom 17 rows and the right third of row 166 (the step in the
bottom edge sits at 67.5 % of the width, predicted 66.7 %). So one sign shows at most 50 000 pixels in
this encoding; a one-line-per-row encoding (`docs/prior-art.md` 5.3) would allow about twice that. A whole piece
fits one sign; it takes several only because overlapping glyphs of different font sub-meshes need
separate signs (layers).
Since gallery 7 all loose glyphs share the main layer-1 sign: the 2400-pixel landscape is ONE sign
(42 078 characters), so no seam can appear: **verified 2026-10-08**, the picture is flawless in game.
One sign held 17 108 characters (641-glyph aurora) without trouble in game. Over the network a sign must stay under
512 KiB, and big signs delay arriving players ([limits.md](limits.md), "One sign over the network" and "Network").

**Typable mode keeps rows on one plane too** (`assign_cells`): an island of touching glyphs fills
one cell from L1; loose glyphs with the same tag (a rune row) take one layer across the cells, the
front-most layer that keeps every command within 50 characters; a glyph that would go over 50 leaves
the plane, moves to a free sign or swaps with another loose glyph. The simulator mirrors this.

**Depth is parallax.** The layers of a cell are 9 cm apart; seen from below or from the side, a deeper
sign's picture slides (deeper = lower when looking up). Overlapping stacked shapes hide it, but a tiling
across layers falls apart: the gallery's pixel ship, one row per sign over five layers, split into
bands, and a backdrop disc on a sign 2.2 m behind the marble stayed behind the logo but slid off it.

**One stack for a whole piece** (`engine.assign_planes`, `build_planes`): every glyph that touches
another goes into one cell, its layer = its paint level. Inside one sign the glyphs of one font
sub-mesh (same `<font>` material, same font asset of the fallback chain) are drawn in character order,
so touching glyphs of one sub-mesh share a level (their lines keep the z order: a whole tree of wooden
bars is one sign); a glyph needs a deeper level only above a touching glyph of another sub-mesh, since
the order between sub-meshes inside a sign is not relied on. Lines that touch nothing may be reordered
(`order_lines`) when the voffset model needs it; `</font>` switches back to the lit sign font mid-sign. **Verified 2026-10-08** (gallery v2: the ship,
the one-sign rune ring, the disc staying on its ring, lit mountains next to glowing curtains). Glyphs that touch nothing go to layer 1 of any
cell: the same depth plane. A piece then needs only as many signs as paint levels (the gallery pieces:
1-4 signs; the 1120-pixel ship 9 signs of ~125 glyphs, one plane).

## 7d. The six-block row (`row.py`, 2026-10-08)

The author asked for the rig plus its six `blackmarble_2x2x2` turned 90° to the right and copied twice
to the left and three times to the right. Findings that shaped it:

- **Sun.** `EnvMan.cs:840`: the sun rises in the east, stands in the south at noon (about 60° high)
  and sets in the west. The original build faced west-southwest, so the evening sun shone into the
  viewer's eyes (the backlit screenshot). Turned +90° (clockwise from above) the signs face
  south-southeast: lit from the front around noon.
- **Support.** `WearNTear.UpdateSupport`: a piece is grounded when its collider bounds (+0.3 m) touch
  terrain or any collider without `WearNTear`; otherwise it takes support from neighbours. Marble loses
  half its support per metre sideways (`horizontalLoss 0.5`), so a cube hanging off a neighbour at the
  same height falls; blocks must stand on floor tiles. `row.py` checks every bottom cube against the
  floor under it and lists any object inside a block's footprint.
- **Space.** The plaza in front of the old build is 16 m long towards the old left; a block is 6 m
  wide. With 10 m between block centres (pieces up to 7.5 m wide never overlap), the original stays in
  place as block 2 and the row runs 1 left, 4 right along the plaza (stone wall and stonecutter at
  the left end, the crystal tower behind the gap between blocks 4 and 5).
- **Backdrop sign (v1, dropped).** Block 3 had a 46th sign on the back face of the cubes, ~2.2 m deeper
  than the rearmost layer. It always drew behind the logo, but parallax moved it off the logo; v2 puts
  the disc on paint level 1 of the same stack instead.

## 7e. Denser pixel encodings (gallery 9, verified 2026-10-08)

Gallery 9 (`concepts/row4.py`, world `MyWorld_gallery9`) tested the escapes, sprites and
spacing tags of section 3 and two pixel encodings beside the current one (pos + voffset + colour + `<br>`
per pixel, 21 characters per pixel for the 60 x 40 fjord):

- **Rows:** one text line per pixel row, `<mspace>` = `<line-height>` = pitch, colour tag only when it
  changes: 4-6 characters per pixel, one visible character per pixel plus one per row. Every glyph after
  the first in a line is checked against the 17.29-unit wrap box and `<nobr>` cannot stop it, so a picture
  is at most one board wide (0.86 m). Verified: 40 x 27 and 80 x 53 on a board, whole and seamless.
- **Columns:** one glyph per line (nothing wraps), column by column; `<indent>` once per column,
  `<line-height>` = pitch, one `<voffset>` per column to climb back to the top, colour only on change:
  **8.1 characters per pixel** (19 344 instead of 50 532), any size. Verified: the same picture lands
  exactly where the reference does, same size and quality.

Vertical model with line pitch L (local units, 1 local = 2 wu) for N lines, extending section 7c:

```
Y_i = 2*V_i - 2*i*L + S_i*(yMin+yMax)_i - K + (N-1)*L,   K = S_0*A_0 + S_L*D_L + max(V_0,0) + min(V_L,0)
```

For columns, V = c*H*L + C cancels the drop of column c; C follows from solving the top row's position
(`column_encoded` in `row4.py`). Line breaks in a save can be raw line feeds (1 character).

**The engine writes it.** `build_planes` encodes every sign whose glyphs do not touch (all mosaics) in
columns (`column_cmds`): column-major order, the most common vertical step as `<line-height>`, the general
solver of `line_cmds` (regime V_0 < 0 <= V_L, the one gallery 9 verified), `<indent>` instead of `<pos>`,
voffsets kept while within rounding, raw line feeds. The gallery-8 ladder drops from ~21 to 6.9-8.1
characters per pixel; its first rung parses to exactly the commands verified in gallery 9. A stack whose
voffsets would pass 32 767 (the 55 200-pixel giant) and signs with touching glyphs keep `<line-height=0>`,
now with `<indent>` and line feeds too.

## 7f. Vanilla animation (gallery 10, verified 2026-10-08)

`<sprite anim="a,b,fps">` animates on every client with no mod and no network (`concepts/row5.py`, world
`MyWorld_gallery10`). The EmojiOne sheet's 16 faces are indexed row by row (0 blushing smile ...
12 a "?" box ... 15 frowning); neighbouring faces make blinks and mood swings. Verified: a three-reel slot
machine and nine blinking faces (both typable, one command per board), the 16 faces with their numbers,
a 50-face crowd in one sign, and `tint=1` together with `anim` (a tinted disco wall). Multi-sprite signs are
laid out with a ■ of the same size as a stand-in and swapped afterwards; numbers under the faces land
where planned. Faces 6-12 cm across (star sprites over the fjord) read as dots: the frame changes are not
visible at that size.

### The ghost face by the crosshair (a vanilla bug worth keeping)

Seen in gallery 10: after looking at a sign with `<sprite anim=...>`, a face stayed by the crosshair.

- The hover text is the sign text through `RemoveRichTextTags` (`client/Sign.cs:45`). Its regex
  (`utils/StringExtensionMethods.cs`) allows `/ a-z A-Z 0-9 = space " ' # ; : ( ) $ _ -` but no comma and
  no dot. So `<sprite anim="a,b,f">` (and `<size=2.5>` and other tags with commas or dots) survive into
  the HUD's TMP text and render there: an animated face next to the crosshair.
- When the hover text becomes empty, `TextMeshProUGUI.GenerateTextMesh` clears the mesh and returns
  *before* `m_spriteAnimator.StopAllAnimations()` (`TextMeshProUGUI.cs:1486-1502`). The animator coroutine
  (`TMP_SpriteAnimator.cs:42-107`) keeps writing the sprite's quad into the mesh, so the face stays.
- It goes away when any other hover text is shown: a full layout stops the animations.

Possible uses: a sign that leaves a "sticker" in the reader's HUD (`<sprite anim="3,3,1">` is a still
face), or hover texts styled with tags that contain dots (sizes). SignLink always strips `<sprite>` tags from hover
texts (the sign itself still shows them; `mod/SignLink/Patches/HoverTextPatch.cs`), so its players never get the stuck face.

## 7g. Lying and turned signs (SignLink 0.11.0-0.12.0)

A sign draws its text in its own plane, so a picture on the floor needs its signs laid face up. 0.11.0 laid a new
stack on a floor it searched for in front of the sign; lying worked in game (2026-10-09), but the search missed on
an upper floor, and the author wants SignLink never to choose where a sign goes. Since 0.12.0 the selected sign itself
(`SignPose`) lies down where it stands, or turns in its own plane. Facts behind it (prefabs read from SoftRef bundle
`c4210710` with UnityPy, 2026-10-09):

- **The sign prefab.** The board (`New/wood_pole (1)`) is a unit cube scaled 1 x 0.5 x 0.1 m on the sign's origin, so
  its faces are 5 cm from its centre; the `collider` child is a box of 1 x 0.55 x 0.089 m. The `Canvas` sits at local
  z = +0.07 turned 180° about y: the text is read from the sign's **+forward** side, 2 cm in front of the board's face.
- **Placing** (`client/Player.cs:4008-4037`): the ghost is pushed out along the hit normal until its collider only
  touches the surface, so a sign standing on a floor has its centre 0.275 m over it (half the collider), and a sign on
  a wall stands 4.45 cm off it at the height aimed at.
- **Lying where it stands.** Its foot (the lowest collider point as built, 0.275 m under the centre) becomes the
  floor: the text lies `Lift` = 8 mm over it, the board's top 1.2 cm under it. Rotation `LookRotation(up, -facing)`:
  the canvas' up (the picture's top) points away from someone who faced the sign, its x to their right, so the picture
  reads upright to them. Copies stack up 0.3 mm apart (`Step`; standing 0.5 mm); the k-th board top is
  `Lift - 0.07 + 0.05 + k x Step`, under the floor for 25 signs (a picture needs about 21 at the 1 M pixel cap), the
  colliders under it for 58: no bump, and the crosshair meets the floor first.
- **Floors.** A floor's collider is not always its visible top: `wood_floor` has its collider top at +0.0969 m and
  its planks' top at +0.0808 m (1.6 cm lower); `stone_floor_2x2` collider +0.5, mesh +0.5013; `blackmarble_floor`
  +0.5 both. On wood the boards sit 4-11 mm over the planks, under the picture.
- **Ceiling (0.14.0).** The mirror image of the floor: `LookRotation(down, facing)`, the head (highest collider
  point as built, 0.275 m over the centre: a sign placed under a ceiling touches it there) becomes the ceiling, the
  text `Lift` under it, the board above it. From below, the canvas' x points to the right of someone facing where the
  sign faced and its y towards them (the top of their view when they look up and ahead): readable, not mirrored. A
  face-up sign seen from below shows its text mirrored, behind its own board. The author wanted every pose at the
  sign's foot instead (2026-10-09), so 0.15.0 replaced it with tipping.
- **Tipping (0.15.0).** One rotation for every pose: about the sign's x axis by the tilt (`rot = base x Euler(-tilt,
  0, 0) x roll`), around the line `foot` under its centre and `Lift` behind its text (`FloorPlan.Tip`), which stays
  where it is. At 90° (face up) this is the floor pose above (text `Lift` over the floor, rotation `LookRotation(up,
  -facing)`), the picture's middle 0.28 m behind where the text stood (0.11-0.14 laid it straight down, centred under
  the sign); at -90° (face down) the text is `Lift` under that line with the board over it, rotation
  `LookRotation(down, facing)`, its middle 0.27 m in front. Between, the sign leans with its edge on the spot. The
  tilt reads back from the rotation against the pose as built (`atan2` of the forward axis' y and z there), the turn
  from what is left. A stack whose first sign is not where its pose puts it (laid by an older SignLink) is made again
  the next time SignLink changes it (a new picture, a fine-tune).
- **A place in fixed directions (0.16.0).** The picture's middle is set as Right, Up and Out (towards someone who faced
  the sign), in the frame of the sign as built, from the tipped sign's text centre (so with nothing set it still falls
  to the sign's foot). A sign draws only in its plane, so `Fit.OnSign` splits that offset: Right stays the sign's
  right, `Up cos t - Out sin t` runs along its face, and `Up sin t + Out cos t` moves the whole stack along its normal
  (`SignPose` shift; the board goes with it). Tilting then turns only the sign: the point stays (tested for 200 random
  fits). Fits saved before (`signlink_fit` without `signlink_fit_out`) had their up along the face and are read back
  as `Up = u cos t`, `Out = -u sin t`, the same point.
- **The save's rotation grid (0.17.0).** `ZDO.Save` (`client/ZDO.cs:1056`) writes a rotation with
  `ZPackage.WriteSmallRotation` (`client/ZPackage.cs:176`): each Euler angle times 2, cast to `uint` (truncated to
  half a degree), and x and z dropped when both are under a degree; network sync sends full floats
  (`client/ZDO.cs:912`), so the loss shows only after a reload. A pose is therefore made on that grid
  (`SignPose.Savable`: rounded to 0.5°, plus 0.001° so the truncation keeps the step) and written straight into
  `zdo.m_rotation`; deriving the angles again from a quaternion loses the x angle's last digits at ±90° tilt.
  Copies take their first sign's angles (`SignBuilder.Clone`). The pose asked for (`signlink_pose`: tilt, turn,
  shift) is kept on the first sign and read back while the sign still shows it; `Check` compares such a pose
  exactly (0.05°, 2 mm) and an older one (0.12-0.16, rounded by the save since) within 0.75° and 1 cm. Positions are not affected: `SmallPosition` (`utils/Utils.cs:1155`) is used only when exact.
- **The sign never moves for a picture's place (0.18.0).** The author wants the sign only turned, never moved off
  where they built it (turn the sign at most, never move it from its place): the normal part of
  `Fit.OnSign` is dropped (`SignFit.Shift` is 0), so the picture's middle is the nearest point on the sign's face to
  the place asked for. Standing, Towards you does nothing; lying, Up does nothing.
- **Moving the sign itself (0.18.0, `SignMove`).** The player moves the stack in small steps: the transforms move at
  once, the ZDOs (`ZDO.SetPosition`, owned first, which raises the data revision: the stack's texts go to everyone
  near again) when the player has stopped for 0.5 s. Signs have no `ZSyncTransform` (`client/ZSyncTransform.cs:138`
  is what moves synced objects), so SignLink games move a sign whose ZDO position differs from its transform
  (`SignMove.Follow`, every 0.5 s) and vanilla games place it anew when the area loads. The total move is kept on the
  first sign (`signlink_nudge`; since 0.20.109 as far as the game holds the sign up there, weighed as
  `WearNTear.UpdateSupport` weighs it (`SupportMath`, `SignMove.Holds`, 0.20.106), up to 5 m from where it was built;
  0.20.55-0.20.108 at most 50 cm along the face and 10 cm off it, 25 cm each way before), and
  the stored base pose moves along (`SignPose.MoveBase`).
- **Turning** is a roll about the sign's own forward axis: clockwise as seen from the front (lying: from above). The
  picture encoder turns the offsets back (`PictureOptions.Origin`), so the picture turns around its own centre.
- **Other players.** `ZNetScene.CreateObject` (`client/ZNetScene.cs:88`) reads a ZDO's position and rotation only when
  the object loads, and signs have no `ZSyncTransform`: moving one would show only after a reload. So a new pose makes
  the stack's signs again: `ZDOExtraData.GetData` copies every field (text, author, `creator`, health, saved animation),
  a new ZDO gets them before the object exists (`ZNetView.m_initZDO`, the game's own path), and the old signs go.
  The pose as built stays on the first sign (`signlink_base_p`, `signlink_base_r`): standing at 0° restores it exactly.
- **Support.** `WearNTear.UpdateSupport` (`client/WearNTear.cs:820`) tests the collider bounds grown by 0.3 m
  (`SetupColliders`, `:482`): terrain in them grounds the piece, a supporting piece passes on support that falls with
  distance. A board lying in the floor overlaps it, so it holds (lying verified in game; turned standing signs
  unverified).
- **Regime.** Pictures use the verified voffset regime whatever their position: with one line per pixel and many
  pixels per band, the first line's voffset is negative and the last one's positive (`PixelText.ColumnText`); only a
  small picture far below its sign needs the both-negative branch.

## 7h. Resizing and moving placed glyphs (SignLink 0.12.0)

The layout model (section 2 for one line, 7c for several) is linear: a glyph's x is its line's pen (`pos`, else the
`indent` in force) plus terms of its size, its y is `2 V_i - 2 i L + S_i (...) - K` with
`K = S_0 A_0 + S_L D_L + max(V_0, 0) + min(V_L, 0) - (N-1) L`. Hence (`TextTransform`):

- every length tag (`size`, `pos`, `indent`, `voffset`, `line-height`) times k scales a piece k times around the
  sign's centre (the max and min terms scale too);
- `pos` and `indent` plus d move it d wu right (an `<indent>` or, on one line, a `<pos>` goes in front when the first
  glyph has none);
- voffsets plus δ move it `f(δ) = 2δ - [max(V_0+δ, 0) - max(V_0, 0)] - [min(V_L+δ, 0) - min(V_L, 0)]` up: f never
  falls, so the wanted shift is found by bisection;
- a pixel picture's line height must stay a multiple TMP reads and adds up exactly (`PixelText.ExactLineHeight`), so
  it takes only the sizes that keep it.

`./sign.sh modtest` resizes and moves every built-in piece and several pixel bands five ways and lays both versions
out with `engine.layout_sign` (`signart/transform_check.py`): 155 texts, every glyph within 0.01 wu.

**Turning around a piece's own middle (0.13.0).** A sign turns only as a whole, so a piece turned around its own
middle c0 is the sign turned by θ plus the texts moved: `d' = R(-θ)(k c0 + d) - k c0` in the sign's own frame (`Fit`).
SignLink keeps on a stack's first sign how it put the content on (`signlink_fit` = scale and move,
`signlink_centre` = c0), so resizing or moving placed content (3 Place) goes from one fit to another with one TextTransform (`Fit.Between`).
c0 comes from the encoder for pictures (0, 0: made centred), from `NAME.fit` for built-in art (the bbox middle of
every glyph as `engine.layout_sign` places them, `signart/art_centres.py`) and from a saved sign's `NAME.fit`.

## 8. State and open ends

Done and verified in game:
- the two modes;
- multi-glyph signs with any line pitch;
- the column encoding (6.9-8.1 characters per pixel);
- the cap of one sign (50 000 pixels in this encoding);
- escapes;
- vanilla sprite animation;
- SignLink (`mod/README.md`): pictures, art and texts written live from its window, stacked copies of the selected
  sign for pictures too big for one sign, the moving fjord, the networked three-band fjord and the pixel cat.

Open:
- **Blueprint export**: an `Art -> Infinity Hammer blueprint` exporter with the ZDO data column
  ([prior-art.md](prior-art.md) section 7), so a piece could be placed with a building mod.
- **Simulator**: learn the multi-line model and sprites.
