# Sign art: the design desk

Pictures in the sky of vanilla Valheim, drawn with nothing but signs. Every sign's rich text projects
one glyph — a shape, a rune, a line-art emoji — metres away from its board; a few dozen signs stacked
in a small grid compose one large piece.

This repo is the design desk for that: a model of how the game lays out sign text, the game's own
glyph outlines, a renderer and checker, a browser simulator, and the art concepts made with them. It
also has SignLink, a BepInEx plugin whose in-game window puts pictures, art and animations on signs.

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

Detailed texts reach the game in two ways: written into a copy of a world save (below), or through SignLink
(next section).

## Live in the game (SignLink)

`mod/SignLink` is a client-side BepInEx plugin (`mod/README.md`): its window writes pictures, art pieces and texts on
signs in the running game. Pictures need no script: the plugin's own encoder (the window's Picture source on 2 Content)
writes the texts `vanilla_hq.py` would, over stacked sign copies that every player sees. Its art pieces, animations
included, come from the concept rounds and the art library (`./sign.sh art`, [art-library.md](art-library.md)). The
toolkit's own texts reach the game by a save edit (below); `vanilla_hq.py` and `image_import.py` write theirs to files
(`signart/out/vanilla_hq/`, `signart/out/import/`). Every client re-reads a changed sign every 2 s, the game's own pace.
Writing on a shared server needs the server owner's go-ahead each time.

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
full `<font>` paths, glowing mirrored glyphs and long commands work there (the detailed mode).

```sh
./sign.sh rig apply rigs/my-rig.json /work/worlds/rig-template-save1487 /wl MyWorld_art --concept small_dragon_crest --force
```

**A row of blocks.** `row.py build` takes the whole block (the rig's 45 signs plus the six marble cubes
they hang on), turns it 90° and copies it into a row, a different piece on each block (the `PIECES` of a
`concepts/row*.py` module: `--pieces row2` by default), checks that nothing is in the way and that every bottom cube
stands on a floor tile, and writes the world; `signart/rigs/<rig name>-row.json` maps every sign of every block.

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
| `docs/limits.md` | what one sign can show and how fast it can change: pixels, bytes, network, frame cost, GIF limits |
| `docs/prior-art.md` | what others did with sign pictures before, compared with this engine; lessons; the mod survey and the moving-picture options |
| `docs/art-library.md` | the art that comes with SignLink, and how to make more |
| `mod/SignLink/` | SignLink: BepInEx plugin with a window to read, write, animate signs and import pictures live (`mod/README.md`, `mod/CHANGELOG.md`) |
| `mod/SignLink.Tests/`, `signart/encoder_vectors.py` | the plugin's C# picture encoder against the Python pipeline (`./sign.sh modtest`) |
| `signart/presets.py` | the art built into SignLink's Art source, from the concept rounds and the art library (`./sign.sh art`) |
| `signart/artlib.py`, `signart/concepts/lib_*.py` | the art library: building blocks and one file per theme |
| `signart/image_import.py`, `vanilla_hq.py` | a picture on one sign; a picture over stacked signs for vanilla players (the band texts, as SignLink writes them); both write text files under `signart/out/` |
| `signart/engine.py` | TMP layout model, command codec, renderer, overlap check, cell assignment, `Art` authoring class |
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
