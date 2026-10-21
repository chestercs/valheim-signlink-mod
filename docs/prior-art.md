# Prior art: Img2Bpl and "Create ART with Signs" (and what it means for a mod)

Reviewed 2026-10-08; what became of the plans is brought up to date for SignLink 1.1.0. Sources: the video, its auto captions, and both code bases cloned and read in full.
Game-code references are to the decompiled client 1.0.16 (`client/File.cs:line`, sibling repo
`valheim-world-tool/decompiled`) and the game's `Unity.TextMeshPro.dll` (`TMP_*.cs`).
Anything not checked in game is marked **unverified**.

## 1. What exists

| Item | What it is | Dates |
|---|---|---|
| Video "Create ART with Signs in 5 Easy Steps - Valheim Tutorial" (Dhakhar - Valheim Mechanicus, youtube.com/watch?v=AqgxjgclsFk, 10:29) | tutorial: image -> blueprint -> place it with Infinity Hammer | 2023-11-19 |
| [dsterentyev/img2bpl.pl](https://github.com/dsterentyev/img2bpl.pl) | the original: a 96-line Perl script (ImageMagick) plus a PHP web front end (`web/`), the "website" the video uses (now offline) | 2023-07-23, web files 2024-03-27 |
| [Theocatic/Valheim_Img2Bpl](https://github.com/Theocatic/Valheim_Img2Bpl) | C# .NET 8 WinForms port of the web version (Magick.NET), the standalone tool the video description now links (release v1.0) | 2024-08-29 .. 2025-08-31 |

Mods used in the video: Infinity Hammer (load and place the blueprint), Gizmo (free rotation),
PlanBuild as an alternative loader. The art itself is **vanilla**: every player sees it without mods,
only the placing needs a mod.

## 2. How Img2Bpl works

Output is a PlanBuild `.blueprint` text file, one piece per line:
`prefab;category;x;y;z;qx;qy;qz;qw;"data";sx;sy;sz` (y up). For a sign, `data` is the sign text.
Every sign gets the quaternion `0;1;0;0` (180 degree yaw) and depth 0, so all signs lie in one plane.
An optional `wood_pole` at the origin gives the build support.

**One pixel column = 2 signs** (the Perl original: 3). Each sign holds a vertical column of pixels,
one per text line:

```
<size=9>\n\n\n...(h/1.5 padding lines)...<#rrggbb>■\n<#rrggbb>■\n...
```

- A text line is about twice as tall as a pixel, so sign A takes rows 0, 2, 4, ... and sign B, raised by
  `1.1 * pixsz`, takes rows 1, 3, 5, ...: the two interleave into a solid column.
- `pixsz = 0.3 * size / 9` m; columns are `1.1 * pixsz` apart. At size 1, 100 pixels are about 3.7 m
  ("a little less than 4 m", video 03:52).
- The leading newlines push the vertically centred text block up so the picture starts above the sign
  (`vshift` off) or straddles it (`vshift` on). The divisor (1.45, later 1.5) and the 1.1 spacing are
  empirical; the code says so ("still somehow slightly wrong and I have no idea why").
- Line breaks are written as the two characters `\` `n`. TMP turns that pair into a line feed itself
  when `parseCtrlCharacters` is on (`TMP_Text.cs:2357-2440`; also `\r`, `\t`, `\v`, `\uXXXX`).
- Colours are 24-bit (`<#rrggbb>`, `<#rrggbbaa>` for alpha); transparent pixels get alpha 00, or a
  hollow `□` ("Better Transparency") to reduce colour bleeding.
- "Sign Backing" adds blank signs (text " ") behind the picture, 0.95 overlap, as a canvas.
- Cost per pixel: 12 characters (`<#rrggbb>■\n`); a 200 x 200 image is 400 signs of about 1 500
  characters each.

The web version also had a **"simple" mode** (`web/img2bpl_alt.pl`): the whole image as one sign text,
one text line per pixel row:

```
<size=F><cspace=-0.2em><line-height=85%><nobr>█<#rrggbb>██<#rrggbb>█...\n...
```

A colour tag is written only when the colour changes (run-length colour). The page warns that more than
about 120 pixels of width "may crash the game" (**unverified**; it predates the 1.0 client).

## 3. What the video shows about using it

- Placing is the hard part: with a 400 x 300 picture on the hammer the game runs at "seconds per frame"
  (05:52). The workaround: open the build menu (the ghost stops updating), `hud` off, nudge, then
  `hammer_place;hammer_repair`.
- Sign count grows with width only, so tall pictures are cheap; the author makes wide pictures tall and
  turns them 90 degrees with Gizmo.
- Several high-resolution pictures lower the frame rate permanently.
- The transparent-colour option did not work for the author; PNG alpha did.

## 4. Compared with this engine

| | Img2Bpl | signart |
|---|---|---|
| Who places it | anyone with Infinity Hammer or PlanBuild, anywhere, also on a live server | typable: anyone, by hand; detailed: a save edit into a world copy, or SignLink's window on any sign |
| Signs per picture | 2 x width (400 for 200 x 200) | 1 up to 50 000 pixels (gallery 8); beyond, stacked copies of one sign (197 105 pixels on 7 signs, [limits.md](limits.md)) |
| Characters per pixel | 12 | 5.9-8.1 in the column encoding (section 5, item 3; [limits.md](limits.md)); ~17.5 in the first one (`<pos>`, `<voffset>`, colour, glyph, `<br>` per pixel) |
| Geometry | empirical factors, one plane, no seam model | calibrated TMP model (ascender/descender, per-line centring), per-sign measured positions, seam-free mosaics |
| Colour | 24-bit, no dithering | 12-bit `<#rgb>` with Bayer dithering and supersampling |
| Transparency | alpha-00 glyphs (still drawn) | empty pixels are simply not written |
| Glow, fonts | none | unlit `<font>` path or alias, sub-mesh aware layering |
| Lag | many objects; placing is very slow | one object; about 13.5 µs per glyph when a sign loads, 0.7 s for 50 000 pixels ([limits.md](limits.md)) |

## 5. What to take from it

1. **Placement by a building mod is the distribution channel.** A blueprint is just sign positions and
   texts; the viewers stay vanilla. Our detailed pieces are mostly one sign, so a blueprint (or a mod
   command) carrying one long text would let anyone place them on any server, without save editing.
   SignLink's window became that channel for its own players (the viewers still stay vanilla); an exporter
   `Art -> .blueprint` for building-mod users is still cheap and not written. Whether Infinity Hammer and
   PlanBuild keep 40k-character texts intact is **unverified**.
2. **`\n` is a 2-character line break.** In the typable mode it saves 2 characters over `<br>` (4) on every
   line. **Verified** in gallery 9 (a save-written sign; typing it in the editor stores the same
   characters). `\uXXXX` and `\UXXXXXXXX` type any character by its code.
3. **Denser pixel formats.** Their "simple" mode writes one text line per pixel row with a colour tag only
   where the colour changes. The code shows its limit: every glyph after the first in a line is checked
   against the 17.29-unit wrap box, and `<nobr>` does not help, because TMP breaks the first word of a line
   between characters anyway (`TextMeshProUGUI.cs:2642-2649`). Margins cannot be negative and `<width>` only
   narrows, so a row-encoded picture is at most one board wide (0.86 m). Two encodings follow from it, both
   tested in gallery 9 (`concepts/row4.py`):
   - **rows** (board-sized pictures): `<mspace>` = `<line-height>` = pitch, ~4-6 characters per pixel,
     one visible character per pixel plus one per row;
   - **columns** (any size): one glyph per line, so nothing wraps; `<indent>` once per column (it persists
     over lines), `<line-height>` = pitch, one `<voffset>` per column to climb back up; 8.1 characters per
     pixel for the 60 x 40 fjord against 21 for the current encoding (19 344 vs 50 532).
   Line breaks can be raw line feeds in a save (1 character). **Verified** in gallery 9 (2026-10-08), see
   `sign-rendering.md` 7e.
4. **Count objects and warn early.** The tool shows "this will consist of N objects" before converting.
   SignLink's window does the same: it shows a piece's *Content size* and checks the multiplayer limits before
   anything goes on a sign.
5. **Their pain points confirm our choices:** one plane with the sign as backing, no depth handling, and
   lag from hundreds of signs.

## 6. Towards a mod: real-time writing without the 50-character limit

### What the game allows (read from code)

- **Sign text refresh:** a sign re-reads its ZDO text only every 2 s (`Sign.cs:39`,
  `InvokeRepeating("UpdateText", 2f, 2f)`), and only when `DataRevision` changed (`Sign.cs:97-102`).
  `SetText` updates the writer's own sign at once (`Sign.cs:175-189`). A new sign object reads its text
  in `Awake` (`Sign.cs:32-40`).
- **Network:** ZDOs go to each peer at 20 Hz (`ZDOMan.cs:909-915`) while the socket's send queue is under
  10 KB (`ZDOMan.cs:1076-1085`). The size is checked before each ZDO, so one huge ZDO still goes out
  whole (`ZDOMan.cs:1113`). Steam sockets send at most 153 600 B/s (`ZSteamSocket.cs:79-83`), and ZDO
  data is not compressed. A 42 KB sign text therefore takes about 0.3 s of a peer's whole link, and
  other ZDOs (players, creatures) wait behind it.
- **The 50-character limit** lives only in the input field (`TextInput.cs:94`, `TMP_InputField.cs:1874`).

### Existing mods

| Mod | What it gives | Fits for |
|---|---|---|
| SignStyler (Ydalis) | limit 400 by default, up to 2 000; editor with colour picker, glyph grid, sliders for size, voffset, line-height, cspace, rotate; vanilla viewers see the result | friends typing richer signs by hand |
| ComfySigns (ComfyMods) | limit 999, plain-text editing of tags | same |
| World Edit Commands (JereKuusela) | `object field=component,field,value` sets component fields on objects; `spawn_object` with fields; vanilla compatible | scripted edits from the console; sign text through it **unverified** |
| Infinity Hammer (JereKuusela) | loads PlanBuild `.blueprint` and `.vbuild`, saves blueprints (`hammer_save`, `hammer_blueprint`, `hammer_place` in the video) | placing exported pieces |

All of them work through the in-game console or editor (**unverified** for the full mod lists).

### What was built: SignLink

SignLink is a BepInEx client plugin driven from its own in-game window; it takes no outside requests.

- It writes the selected sign's ZDO text (`ClaimOwnership` + `s_text`, as `Sign.SetText` does): everyone sees it,
  vanilla viewers within 2 s.
- A picture too big for one sign goes on stacked copies of the selected sign, at its exact rotation, a hair apart
  (`mod/SignLink/Signs/SignBuilder.cs`): no save editing. It never places a sign anywhere else by itself.
- An animation is stored on the sign once, and every SignLink client plays it locally (path A below); vanilla
  viewers see its first frame.

Writing to signs on a shared server is a live change there: it needs the server owner's go-ahead each time.

### Moving picture: what limits it

| Path | Who sees it | Frame rate | Cost / risk |
|---|---|---|---|
| A. Local flipbook: the plugin sets `m_textWidget.text` | only the plugin user | as fast as TMP rebuilds the mesh | about 13.5 µs per glyph and frame on the main thread (measured) |
| B. Networked frames: write the ZDO each frame | modded viewers smoothly only with a viewer patch that re-reads on every revision change (SignLink has none); vanilla viewers at most 0.5 fps, random phase per client | bandwidth: a frame must stay small (a few KB), or split into tiles and write only the tiles that changed | stalls other ZDO traffic if frames are big |
| C. Respawn trick: destroy the sign and create a new one per frame | everyone, vanilla included, as the new object reads its text in `Awake` | limited by object creation and ZDO create/destroy traffic | **unverified**; possible flicker; z-fighting if old and new overlap |
| D. `<sprite anim="first,last,fps">` | everyone, no mod, no network | any | only the 16 faces of TMP's EmojiOne sprite sheet (the default asset); **verified** in gallery 9: `<sprite anim="0,15,8">` cycles them in vanilla (`TMP_Text.cs:6409-6417`, `TMP_SpriteAnimator.cs:33-60`) |

What came of it: D, `\n` and the row tags were verified in galleries 9 and 10. Path A was measured with the moving
fjord (about 13.5 µs per glyph, [limits.md](limits.md)) and became SignLink's saved animations, played by every
SignLink client from frames stored on the sign. Path B ran with the fjord in three bands (only the moving band
resent). C was never tried.

## 7. What the mod code shows (survey 2026-10-08)

Cloned and read: Infinity Hammer (IH), World Edit Commands (WEC) and Server Devcommands (all Unlicense),
PlanBuild (WTFPL), SignStyler (MIT), ComfySigns (GPL-3.0: read only, nothing copied). Paths are in those repos.

- **Placing pieces in code.** IH pastes in one frame per blueprint:
  - it creates the ZDO first (`ZDOMan.CreateNewZDO`) and writes its data;
  - it sets the revision and puts the ZDO into `ZNetView.m_initZDO` (WEC `service/DataHelper.cs:148-177`);
  - then a plain `Instantiate` adopts it (`client/ZNetView.cs:55-58`, the same trick `ZNetScene.CreateObject`
    uses). A sign made this way shows its text from `Awake` on, with no author written.

  PlanBuild instantiates first and writes component data afterwards (`PlacementComponent.cs:121-462`).
  Neither batches; they only cut effects.
- **Floating pieces.** Neither handles support. Both write `health = -1` through `HasFields` (PlanBuild
  `:401-410`, IH `behavior/CustomHealth.cs:283-316`). `WearNTear.ApplyDamage` returns early at health <= 0
  (`client/WearNTear.cs:1248-1254`), so the no-support damage never lands. `HasFields` is applied by vanilla
  `ZNetView.LoadFields`, so it holds for unmodded clients too. This is how a plugin could hang signs in the air (SignLink does not: its
  copies hang where the original hangs and share its support).
- **Sign text in blueprints.**
  - PlanBuild reads the displayed text (`TextReceiver.GetText`, possibly censored), deletes semicolons and
    turns commas into dots on load (`PieceEntry.cs:27-30, 107-120`).
  - IH writes the raw text into the info column (a line feed breaks the row). It also adds a base64
    `ZPackage` of the ZDO data as column 13, which wins on load and has no string-length limit
    (`HammerSave.cs:199`, WEC `PlainDataEntry.cs:161-268`).
  - **An exporter for our art must use IH's data column**: our texts contain line feeds and commas.
- **Editing existing objects (WEC).** It claims ownership and writes the field. A local "refresh" destroys
  the GameObject and calls `ZNetScene.CreateObject(zdo)` (`Actions.cs:18-26`). Other clients rely on
  vanilla ZDO sync and on each component's polling; there are no custom RPCs.
- **Sign mods.** ComfySigns sets `m_characterLimit = 999` in a `Sign.Awake` postfix. SignStyler raises the input
  limit after `TextInput.RequestText` (400 by default, up to 2 000) and closes tags left open in a `SetText`
  prefix. Neither changes the 2-second polling (nor does SignLink).
- **Scale.** A scaled `sign` is not visible to vanilla clients: `ZNetView.Awake` reads the scale only for
  prefabs with `m_syncInitialScale`, and a `HasFields` override comes too late (`client/ZNetView.cs:67-82`
  vs `:107`). `<size>` stays the vanilla-visible way to scale.
- **Reusable (Unlicense):**
  - WEC `DataHelper.Init` (create with prefilled data), `CloneWithKeys`/`Print` (read every ZDO field type);
  - `ZdoHelper.Hash`;
  - Server Devcommands `Selector.GetNearby`/`GetHovered`;
  - IH `HammerBlueprint.cs:116-277`, which parses both `.blueprint` and `.vbuild`.

  None of them has a main-thread queue for work from other threads; SignLink has its own (`mod/SignLink/MainThread.cs`).

SignLink uses the `m_initZDO` path when it makes a sign again in a new pose (`mod/SignLink/Signs/SignPose.cs`: text, author,
creator, health and saved animation are on the ZDO before the object exists). Still open: an `Art -> IH blueprint`
exporter with the ZDO data column, so a piece could also be placed with Infinity Hammer.

