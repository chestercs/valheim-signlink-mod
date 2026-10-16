# Sign art: the design desk

Pictures in the sky of vanilla Valheim, drawn with nothing but signs. Every sign's rich text projects
one glyph — a shape, a rune, a line-art emoji — metres away from its board; a few dozen signs stacked
in a small grid compose one large piece.

This repo is the design desk for that: a model of how the game lays out sign text, the game's own
glyph outlines, a renderer and checker, a browser simulator, and the art concepts made with them. It
also has SignLink, a BepInEx plugin, so programs can write and animate sign texts in the running game.

## Quick start

```sh
./sign.sh image      # Docker image (Python, fonttools, skia, UnityPy)
./sign.sh extract    # glyph outlines + metrics from your Valheim install (VALHEIM_DATA)
./sign.sh build      # all concepts -> commands, renders, signart/out/simulator.html
./sign.sh test       # engine tests + JS/Python parity
```

Open `signart/out/simulator.html` in a browser: pick a concept, look at it on a night/dusk/day sky,
switch to the cell view, click a shape to nudge it, and copy each sign's command in entry order.

## Two modes

- **Typable**: one glyph per sign, every command at most 50 characters. Anyone can build it by
  copy-pasting the commands from the simulator into signs, no tools, no save editing.
- **Detailed**: many glyphs per sign and no length limit, so a whole picture fits in one stack of five
  signs (thousands of glyphs). Written by SignLink in game, or into a world save with `rig.py` and `row.py`.

Concepts say which one they are (`Art(mode=...)`); the simulator marks every card.

Detailed texts reach the game in two ways: written into a copy of a world save (below), or live through
SignLink (next section), which can also animate them.

## Live in the game (SignLink)

`mod/SignLink` is a client-side BepInEx plugin with an HTTP API on `127.0.0.1:8417` (`mod/README.md`);
`signart/live.py` is its client. Install it once into a Thunderstore Mod Manager profile, start the game
modded, turn the developer API on (its Settings tab; off by default), then:

```sh
./sign.sh live signs --radius 30                                   # ids of the signs around you
./sign.sh live write <id> --file text.txt --mode zdo               # any length; zdo = saved, everyone sees it
./sign.sh live image <id> images/a.jpg                            # a picture for vanilla players (0.4+)
./sign.sh live anim <id> --frames concepts.anim2:cat --fps 4 --mode zdo     # the pixel cat
./sign.sh live anim-block 4 --frames concepts.anim1:fjord_bands    # one clock over three signs of block 4
./sign.sh live stop
./sign.sh live clear                                               # empty every sign of the gallery row
```

Frames come from `pixels.py` (scene function -> 12-bit colours -> column-encoded text) and are uploaded
once; the plugin flips them itself. Pictures need no script any more: the plugin's own encoder (the
window's Picture source on 2 Content, `POST /image`) writes the texts `vanilla_hq.py` would, over stacked sign copies that
every player sees. Vanilla clients re-read a sign every 2 s, SignLink clients every
0.1 s. Writing on the live server needs the server owner's go-ahead each time.

## How a piece is built in game

- **Cells** in a grid with empty sign slots between them (default 3x3 with one empty slot:
  A1 A2 A3 / B1.. / C1..). Any size works: 10x1x10, 1x1x10, ... (`Grid` in the engine, the grid
  panel in the simulator).
- Each cell is a stack of signs **behind each other** (depth 5 by default); L1 is the rearmost.
- Enter signs column by column (A1, B1, C1, A2, ...), inside a cell L1 first.
- Shapes that overlap must come from the same cell; the engine guarantees that.

## Writing art into a save (no typing)

The physical build is a **rig**: its signs were labelled once in game (`A1-1` ... `C3-5`, cell then
layer, 1 = rearmost). `rig.py scan` reads their exact positions from a world save into
`signart/rigs/<name>.json` (on the computer that scanned it: a rig holds real base coordinates, so `.gitignore`
keeps it out of the repository); `rig.py apply` builds a concept on that measured grid and writes
every sign's text into a copy of the world under a new name. Saves have no 50-character input limit:
full `<font>` paths, glowing mirrored glyphs and long commands work there (`save_only` concepts).

```sh
./sign.sh rig apply rigs/my-rig.json /work/worlds/rig-template-save1487 /wl MyWorld_art --concept small_dragon_crest --force
```

**A row of blocks.** `row.py build` takes the whole block (the rig's 45 signs plus the six marble cubes
they hang on), turns it 90° and copies it into a row, a different piece on each block
(`concepts/row1.py`), checks that nothing is in the way and that every bottom cube stands on a floor
tile, and writes the world; `signart/rigs/<name>-row.json` maps every sign of every block.

```sh
./sign.sh row build rigs/my-rig.json /work/worlds/rig-template-save1487 /wl MyWorld_gallery --force
```

The template world (`worlds/`, git-ignored) is the labelled save; the output goes to the game's
`worlds_local` (`/wl`) and is refused while Valheim has that world open. Writing into the live server's
save is a separate, server-owner-approved step (stop, edit, upload).

## Layout

| Path | What |
|---|---|
| `docs/sign-rendering.md` | everything known about sign rendering: layout math, tags, fonts, draw order, limits, review of the first session |
| `docs/prior-art.md` | what others did with sign pictures before, compared with this engine; lessons; the mod, MCP and moving-picture plan |
| `mod/SignLink/`, `signart/live.py` | SignLink: BepInEx plugin with a localhost HTTP API and a window to read, write, animate signs and import pictures live; its CLI client (`mod/README.md`, `mod/CHANGELOG.md`) |
| `mod/SignLink.Tests/`, `signart/encoder_vectors.py` | the plugin's C# picture encoder against the Python pipeline (`./sign.sh modtest`) |
| `signart/presets.py` | art presets for SignLink's Art source (the cat, the fjord, stacked pictures) |
| `signart/image_import.py`, `vanilla_hq.py`, `gif_import.py` | a picture on one sign; a picture over stacked signs for vanilla players (`--screen` plays a GIF over it); a GIF as text frames |
| `signart/engine.py` | TMP layout model, command codec, renderer, overlap check, cell assignment, `Art` authoring API |
| `signart/pixels.py` | pixel pictures: scene sampling, 12-bit dithering, ■ size per pitch, column-encoded sign texts |
| `signart/concepts/round*.py` | the art, round by round; `common.py` has the palette and helpers |
| `signart/build.py` | build every concept; `make_sim.py` + `sim_template.html` produce the simulator |
| `signart/extract_glyphs.py` | font chain resolution and outline extraction from the game files |
| `signart/ingame_tests.json` | the author's in-game verification log (local, not in the repository; the simulator shows it when there) |
| `signart/rig.py`, `signart/rigs/` (local) | sign rigs: scan a labelled build from a save, write concepts into a world copy |
| `signart/row.py`, `signart/concepts/row1-5.py` | the rig turned and copied into a row of blocks, one piece per block (save edit): galleries 2-10 |
| `signart/concepts/anim1.py`, `anim2.py` | animations for SignLink: the moving fjord (whole or in three bands), the 8-bit cat |
| `signart/font_alias.py` | short `<font>` aliases with the same TMP hash as a font name |
| `signart/tests/` | unit tests and the JS/Python parity check |

`signart/out/` is not committed: it holds data extracted from the game's fonts and build output.

## Writing a concept

```python
def raven_moon():
    a = Art("raven_moon", "Raven before the moon", "idea in one line")
    a.add("●", 0, 0, h=300, color="#eec")              # back to front, wu around the art origin
    a.add("🐺", 0, -10, h=220, color="#123")            # touches the moon: same cell, layer 2
    a.add("ᛟ", 0, -260, h=60, color="#da4")             # touches nothing: any cell
    return a
```

Add the function to a `CONCEPTS` list in `signart/concepts/roundN.py` and run `./sign.sh build`.
The build reports commands over 50 characters, groups too big for a cell, and cross-cell touches.
