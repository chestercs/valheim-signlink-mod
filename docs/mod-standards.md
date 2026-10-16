# SignLink: production plan and mod conventions

Research 2026-10-08 (templates, popular mods, Thunderstore docs). Game references are to the decompiled
client in `valheim-world-tool/decompiled`. Anything not confirmed is marked unverified.

## Layout

The plan of this section became the code; `mod/README.md` lists the layout as built (0.4.0). The package is
ready too:
- `mod/package/` holds manifest.json, icon.png (256x256, from make_icon.py), the README for the site and
  the LICENSE (MIT) and LICENSE-ART (CC BY 4.0);
- `./sign.sh package` checks that Plugin.Version, the csproj, the manifest, the top CHANGELOG entry and docs/openapi.yaml agree, then
  writes `mod/out/SignLink-<version>.zip`;
- publishing (website or `tcli publish`, AI Generated category) is the author's step, under the team
  PetyusPolisz (the package is `PetyusPolisz-SignLink`); the author is ChesTeRcs.

## Conventions adopted

- **Plugin metadata.** Use `[BepInPlugin(Guid, Name, Version)]` and `[BepInProcess("valheim.exe")]`, which also matches
  `valheim.x86_64` and skips the dedicated server. The GUID `PetyusPolisz.SignLink` (since 0.20.43; before
  it was `bakterhaz.signlink`, whose config file takes the new name once at start) does not change again: it names
  the config file and the Harmony id.
- **Single version.** `Plugin.Version`, the manifest's `version_number` and the top CHANGELOG entry are one
  version; the build checks it.
- **Config.** Pass a `ConfigDescription` with ranges and ConfigurationManager attributes. The hotkey is a
  `KeyboardShortcut` (PageUp). Do not use `KeyboardShortcut.IsDown()`: it is false while any other key is
  held, for example W while walking. Check the main key and its modifiers instead.
- **Logging.** Info for lifecycle, Debug per request, nothing every frame.
- **Harmony.** One class per patch, applied one at a time (`harmony.CreateClassProcessor(type).Patch()` in a
  try/catch, `Plugin.Patch`), and `UnpatchSelf` in `OnDestroy`. A patch that a game update breaks is skipped
  with a warning; the rest of the mod keeps working.
- **Tests outside the game.** Code without Unity (the picture encoder) is compiled into a .NET 8 test program
  as well and compared with the Python tools (`./sign.sh modtest`).
- **Multiplayer.** Client-side and vanilla compatible: no RPCs, no version handshake, no ServerSync.
  Writes mirror `Sign.SetText` (`PrivateArea.CheckAccess`, `ClaimOwnership`; `Sign.cs:175-188`).

## GUI

Choice: an **IMGUI** window (`GUILayout.Window`). It needs no dependency and about 150 lines. Jotunn's GUIManager
has the Valheim look but makes every user install Jotunn. uGUI built in code with vanilla sprites is 3-5 times
more code. The look can change later and keep the input blocker.

**Input blocking**, the way Jotunn and ComfyMods do it: a postfix makes `TextInput.IsVisible` return true
while the window is open. That one patch stops:
- movement and look (`PlayerController.TakeInput`, `PlayerController.cs:229`);
- attack and hotbar (`Player.TakeInput`, `Player.cs:2669`);
- the cursor capture (`GameCamera.UpdateMouseCapture`, `GameCamera.cs:191-205`);
- chat, Esc and the map (`Chat.cs:191`, `Menu.cs:388`, `Minimap.cs:695`).

**Walking with the window open** (`Patches/WalkPatch.cs`, 0.20.166, the author: WASD should work too ... very
carefully): after `PlayerController.FixedUpdate` set no controls, a postfix sets the walk (Forward, Backward,
Left, Right, Run or its toggle, Jump, AutoRun, as the game reads them) when `PlayerController.TakeInput` would be
true without the window (`InputBlockPatch.Bypass` for that one call: chat, console, menu, the game's own text input
and map search still stop it) and no SignLink field has the keyboard (`GUIUtility.keyboardControl`). Attacks, block,
the hotbar, crouch, dodge and the mouse look stay off: a click on the window must not swing a weapon.

`TextInput.IsVisible` does not cover two things, and each has its own small patch:
- the mouse wheel: a postfix makes `ZInput.GetMouseScrollWheel` return 0 while the mouse is over the window or a
  row is dragged (`SignLinkWindow.PointerOver`); away from it the camera zooms (`GameCamera.cs:254`, 0.20.166);
- Tab and E: `InventoryGui.Update` opens the inventory without checking text input (`InventoryGui.cs:526`), so
  a prefix skips `InventoryGui.Show`.

The window draws its own styles (`UI/Theme.cs`: Unity's default skin draws windows half transparent), as solid as
its opacity (0.8 by default; **Opacity** below), and scales with the screen height.

The window opens only when no other UI is open (menu, inventory, map, console, text input, chat). Esc closes it. With a
sign picked it opens on 3 Place when SignLink made what the sign holds (an edit), on 2 Content when the sign is empty or
the game's editor wrote it, else on 1 Sign to pick one; E on a SignLink sign and Open in SignLink pick the sign first
(`OpeningTab`, 0.20.110-0.20.118: before, the tab it was closed on, and 1 Sign for those two).

**The frame in the world** (`UI/Ghost.cs`): the content's rectangle and the sign's board in the pending pose are
quads under a sign's own world-space canvas (`Hidden/Internal-Colored`, no depth test: it shows through walls and
floors, 0.20.30; `RawImage` bars, the path the GIF screens use, when the shader is missing): a child placed at the
pending pose with the canvas's scale cancelled, so its children are sized in metres; text-local units (1 = 2 wu =
5 cm) go through the text widget's transform. Which canvas matters: world-space canvases draw nearest last, so under the first sign's canvas the frame was
drawn under the text of the copies stacked in front of it and showed over one band only (0.20.0-0.20.1); a canvas of
its own, parented to the sign with `sortingOrder + 100` (0.20.2-0.20.3), did not show in game at all (cause not found).
Since 0.20.4 it hangs under the canvas of the stack's frontmost copy (`Session.Stack`'s last sign), last among its
children. That is not enough for the fill: the copies' canvases draw in the order Unity sorts world-space canvases
(nearest the camera last, by its own measure, which on a lying picture is not the copy stacked frontmost), so the
fill showed over one band of a picture and not the rest (the author's screenshot, 0.20.12); 0.20.13-0.20.25 drew it
over a single sign only. Since 0.20.26 the fill, and since 0.20.30 every bar, is a plain `MeshRenderer` quad
(`Ghost.Make`, `Hidden/Internal-Colored` with ZTest Always, no ZWrite, alpha blending, render queue 3100; one mesh
per colour): a renderer, not a canvas element, drawn after every canvas (queue 3000) and through walls and floors,
so the frame shows whole on any stack and for a sign under a floor. `Sprites/Default` is the fallback (whole, but
hidden by walls), the RawImage bars the last (the fill over a single sign only); the log says which.

**The tilt's pivot** (`Fit.Edge`, `SignPose` PivotKey, 0.20.30): a pose tips the sign over its bottom edge (the
edge stays put: a sign on the floor lies on it face up; every pose before 0.20.30, and the API's way) or about its
middle (the centre stays put, as a hammer turns a piece). Since 0.20.78 the window has no choice of pivot (the author
called it legacy: Move the sign sets where it rests): a tilt changed in the window turns about the middle, and a sign
posed over its edge keeps that pose until its tilt changes. Since 0.20.83 that middle is the middle of the **text's
plane** (PivotKey 3, `FloorPlan.FaceTip`), not the board's (2): the text sits 7 cm in front of the board's centre, so
about the board's middle a picture went 7 cm over it face up and 7 cm under it face down; now it stays where it is
whatever the tilt, and the board turns around it. A stack posed about the board's middle keeps that pivot until its
pose changes (`SignPose.PivotFor`). The pivot is part of the pose only while tilted (`Fit.SamePivot`), is kept on the
first sign (1 edge, 2 middle; unset reads as edge), and `Place`/`Unplace` skip `FloorPlan.Tip` for the middle. The GIF
screens get away with the first sign's canvas because they hide the whole stack's text, which needs the sign list to
hold the copies made in the same frame (`SignFinder.Loaded` rebuilds when a sign awoke, not only once per frame).
Pick in view draws a second `Ghost` (its board in white since 0.20.88) around the sign under the cursor (`Ghost.ShowStack`, 0.20.16): the
content's rectangle is the union of the stack's texts' `TMP_Text.textBounds` in the first sign's plane (what is drawn,
no reading of the sign), in front of the frontmost band, plus the board; the same ray as the click (`AlongRay`), every
frame while picking. The selected stack gets no hover frame while it wears the selection's (`Ghost.Frames`, 0.20.92):
under the cursor or under a row of Signs nearby it showed both frames at once.

**Nothing moves under the mouse** (0.20.75, 0.20.84): while a control is held or a field has the keys, the window's
top and body keep at least their height (`SignLinkWindow.Steady`); the footer only grows while the same tab shows the
same chosen sign at the same width (`FooterLeast`: a hint under its button comes and goes with the verb); the body is
never shorter than what its scroll shows (`BodyLeast`: no pull-back when it shrinks while scrolled to its end); a hint
above a slider never changes its line count with the slider's value (put the value's words on a line of their own).

**No pick of before on a sign that holds something** (0.20.89): a sign whose content no source loads (no record, or
its file or piece not here) gets a slot of its own with nothing picked (`Session.Blank`), and a source shows no pick
of its own kind while a sign of another kind is edited (Picture `selected`, Art `Selected`, the Text editor empty):
only a pick made for that sign can replace its content. The player's own choice still comes to an empty sign.

**The strip follows the slots** (`SourceFollow`, 0.20.90): 2 Content's source strip notes the player's own source on
every Layout their own slot shows, and a sign's slot loads into its source (`Leaving` first); a slot that loads into no
source (`Session.Blank`) leaves the strip where it is, and the own slot coming back brings its own source back. Noting
it only at a load sent the strip back to an older source after a blank slot.

**A source acts only on what it holds** (0.20.91): a source with nothing picked leaves the slot as it is, its light
included (`Change.PictureLight`): a light nobody chose turned the verb into a live Apply, and 3 Place rewrote a sign
by itself. A row that only matters for a pick (the Picture source's Light) is greyed until there is one.

**Our own remake keeps the slot** (0.20.91): Apply sign coord changes, Reset to default and a refit make the stack's
signs again and select the new first sign with `sameContent`; the session then reads it as the same sign
(`ReadHeld(other && !sameContent)`), so a typed text, a new pick and the rows stay. A slot made for content no source
loads (`Session.Blank`) keeps the sign's recipe and the Missing line across re-reads, and a sign whose content loses
its record gets a new blank slot.

**Shift + wheel tunes a slider** (`FineStep`, 0.20.93): over a `Widgets.Number` row, Shift + the mouse wheel moves the
value one fine step a notch (a fifth of the slider's snap, no finer than the row shows; a typed value goes to the next
grid point first) and uses the event, so the body does not scroll; the plain wheel scrolls as before. Only a row in
view of the body's scroll takes it (`Widgets.BodyView`): a row scrolled out of view still has the mouse inside its rect.

**A new pick hides what the sign holds** (`HeldVeil`, `Change.HidesHeld`, 0.20.95): while 2 Content's pick is a picture
or a piece other than the held one, the selected stack shows nothing on this computer (its text canvases switched
off, 0.20.130, its texts emptied before; a moving GIF's screen hidden by `ScreenPlayer.Veil`), so only the new pick's
frame shows where it goes; since 0.20.129 it also hides whenever the live preview shows (`ContentPreview.Showing`,
below), and `HidesHeld` covers the picks the preview cannot show yet. No pick, the held item again, another sign or
the window shut show the sign as it is; the veil does not change while a put runs (`Busy`). The session reads the
saved texts, so the veil changes no choice.

**Reset to default can be undone while the window stays open** (`MovePanel.BeforeReset`, 0.20.96): the reset notes
the move and turn since the sign was built (written and not yet), its pose and pivot and 3 Place's rows; Changes' Undo
(after any steps not applied) does the reverse of the reset, one part a frame, since signs made in a frame stand beside
the old ones until it ends: the pose where it was built first (the moves were made in that pose, so they go along its
face again), then the moves and turns, made again there, then the steps not applied. Apply, another sign, the stack made
again by anything else or the window closing drop it.

**No row widens the window** (0.20.98): GUILayout gives an element without a width at least its content's width, and
a row wider than the window widens the whole window past its size (and, clamped by its own width, past the screen's
edge); a label that changes with a slider (a verb button's "and tilt it to 5°") then rewrapped every row and moved the
slider under the mouse. Full-row buttons take `Widgets.Wide` (as wide as the row, the label fitted), a line of changing
words takes a width and its longest wording that fits (`AreaMeter.Fitting`). The window stays on the screen by the width
it was drawn at and logs once when it was drawn wider than its size ("window: drawn N px wide").

**Shift + wheel on a snapping row** (`FineStep.Notch`, 0.20.98): a row whose value snaps (a picture's width to pixels TMP
adds up exactly: `PictureSource.Planned`; a piece's size to exact line heights: `TextTransform.Snap`) passes `snap` to
`Widgets.Number`, and a notch goes to the next size it really takes; a centimetre snapped back to the same width.

**Vertex colours are converted for the colour space** (`Ghost.OnScreen`, 0.20.100): in a linear colour space Unity takes
mesh vertex colours as they are, so a colour chosen for the screen (sRGB) showed lighter and paler: the frame's green and
red looked milky and washed out, and earlier the rose too bright. Colours are written as meant on screen
and converted (`Color.linear`) when `QualitySettings.activeColorSpace` is Linear; the log says the colour space once.

**The board's outline draws last and glows** (0.20.127; the author: I can barely see the sign's border now): the
content's veil lies on the text's plane, in front of the board, so at the same render queue it covered the outline
and turned the yellow green wherever a picture overlapped the board. The board's bars have their own material at
render queue 3150 (after the veil, no depth test), a neon lime, opaque, and their vertex colours `BoardGlow` (2.2)
times brighter in linear space: over 1 the game's bloom makes them glow where the camera renders in HDR (the log says
"camera HDR" once), else they show at full strength. The hover frame's board keeps its cool white, unlit.

**The content's bars lie on the text's plane** (`Ghost.OnText`, 0.20.101): green and red are decided by depth where the
bars are, so the bars sit a millimetre in front of the text (the text itself is 2 cm in front of the board's face:
FloorPlan.TextZ, BoardHalf), not a centimetre (`Ahead`, the board's outline only): a picture sunk just under a floor's
surface showed only through the floor's gaps while its frame said it showed whole. The plane is the stack's frontmost
band (0.20.102): the copies stand Step apart in front of the first sign, and on the first sign's plane the bars lay on a
copy's and flickered green and red with what draws there (the selection's frame; the hover's measured it so already).
The pose is the sign's own transform while the chosen pose is the sign's (`Change.SamePose`, 0.20.104): worked out
again (`SignPose.Preview`) it lay a fraction of a degree off the picture (the world save keeps rotations to half a
degree), crossed its plane, and showed red on one side and flickered where they met; only a pose still to come is
worked out. The log line "the highlight: shown on ..." says which ("as it stands", "at the pose to come").
Over a moving GIF's screen (`ScreenPlayer.ScreenOver`, 0.20.105) the bars lie 4 mm in front of the screen's measured
plane (`OverScreen`: a screen lit by the scene writes depth), and the depth-tested bars carry a polygon offset of -1
(`_ZBias`); the "shown on" line gives the planes (text, frontmost band, screen, frame) and a frame made again ten times
back and forth within a second logs which of its inputs keep changing.

**The SignLink list reaches as far as the area limit** (0.20.103): Signs nearby's second list, *SignLink signs within 100 m*,
finds SignLink's stacks within `SaveCost.AreaRadiusM` of the player, loaded or not yet loaded, the reach the
multiplayer area limit counts in (`SignArea.Others`); it showed the loaded ones at any distance and every unloaded one
in the world.

**From behind, the frame does not judge** (`Ghost.Side`, 0.20.106): with the camera behind the content's plane the
content's bars wear one neutral colour (the board's) and test no depth; green and red say what the front shows. From
behind the bars lay behind the picture, and the author read that as a flicker (I may have been looking at the back of the picture).

**A move or a turn is weighed as the game weighs support** (`SupportMath`, `SignMove.Holds`, 0.20.106): the game passes
a piece's support on less a loss over the distance between centres of mass (wood: 100 at most, 10 at least, 0.2 a metre
sideways, 0.125 downwards) and breaks a sign under its least. A step is refused when it leaves the sign too weakly held
(5 % to spare) where the move is applied (boxes made anew) or as the game sees a step not applied (its boxes stay where
the sign was made, its centre of mass moves): asking only whether anything was there let a sign be pushed until it
broke. A step towards better support stays allowed. Since 0.20.109 this is the move's limit, with `SignMove.MaxM` (5 m
from where it was built) against a step gone wrong; the fixed 50 cm along the face and 10 cm off it went, and steps go
up to a metre.

**Face up and face down keep the content in the world** (`Fit.Laid`, 0.20.107): the content's place is kept in the frame
as built (Right, Up, Out); the rows show it on the face (Up runs towards the picture's top: away from you face up,
towards you face down). A pose button that turns the face round (face up <-> face down) keeps Right, Up and Out as
built, so the picture stays where it is and the Up row changes sign; a quarter turn (standing <-> lying) and the Tilt
row keep it where it sits on the face (`Posed`), so a continuous tilt never makes it jump. Before, face down sent a
picture set 3 m towards you 3 m away (the author chose the Up row changing sign over a jump somewhere on the Tilt row).

**The body holds its height, never more** (`WindowSizing.BodyLeast`, 0.20.111): scrolled down, the body is never
shorter than what the scroll shows (0.20.84: content that got shorter at its end pulled the scroll back), and never
taller than it was: the scroll view's own view is a little shorter than its rect, so holding "scroll + view" made room
at every notch and the body scrolled on and on into empty room.

**A moving GIF is framed as it shows** (0.20.116): its screen is a pixel larger on every side than its still
(`PictureImporter.PlayOver`: (W + 2) x (H + 2) pitches), so the selection's frame adds that pixel (`PictureSource.Size`
via `Screened`; what the sign holds, in `Ghost.Update`), and Show all's measures the screen's own rect with the texts'
bounds (`ScreenPlayer.ScreenOver`): the screen hides the text, which a GIF put on anew has never laid out, so Show all
found nothing to measure and framed the board alone.

**A picture's size moves in exact pixel sizes** (0.20.122): TMP reads only some pixel sizes exactly (multiples of 1/8 wu
whose half it parses exactly, `PictureOptions.PitchFor`), so a picture's free Width moves in steps of the pixel count
times 1/8 wu (20-150 cm on most pictures). A spike (2026-10-12) measured finer grids: 1/16 gives 101 usable pixel sizes
up to 8 wu, 1/32 179, 1/64 no more (they misread at 6 decimals); even 1/32 with the Resolution free by 3 % left 7-66 cm
steps, so the grid stays. A fine size needs a few pixels more or less: Settings › Pictures › Fine Width steps
(`PluginConfig.PictureFineWidth`, on since 0.20.124 under the key FineWidth).

**The Resolution is the player's** (0.20.128; the author: when I set the size the KB grows and I cannot save onto it;
when I edit and shrink the size, its density gets very bad; of the rules offered, the Resolution is the
player's). 0.20.122-0.20.127 wrote the fine size into the Resolution: Shift + wheel stepped a pixel at the same pixel size,
so growing a picture grew its data over the limit and shrinking it many notches took its pixels away for good. Now,
with Fine Width on (`PictureOptions.Fine`), `WidthM` is the size asked for (drag, typed, Shift + wheel: the next size
about a pixel away, `PictureSource.Finer`) and `PlanFor` takes the pixels nearest the Resolution (`Detail`) that make it
within half a pixel (`PictureOptions.FineDetail`: the exact pixel nearest size over Resolution, then the pixels that
size takes). Measured over 1-8 m (2026-10-12) that moves the pixels 2.5-8 % on average, but up to 50 % for 250-320
across on small pictures, near the smallest exact pixel; so a size may move them at most `PictureOptions.FineSpread`
(10 %, the data by about a fifth), and a size further off keeps the Resolution's pixels and steps as without Fine
Width (0-22 % of sizes 1-8 m, more with more pixels; Shift + wheel then goes to the next step). `Detail` itself is never written by a size: 2 Content's Resolution
row shows it, Max and Native set it, and the Summary says "Resolution 200: +4 across for exactly this size". A cap
(`PictureOptions.FineCap`, `PictureSource.FineCapFor`) keeps a size from going over what Max aims at (`MaxTarget`,
2 % short) by this picture's counts (`PictureOptions.MostWithin`: the data grows with the pixels; since 0.20.134 the
first count sets it and later ones may only lower it, `PictureOptions.NextCap`, kept per picture, Resolution and aim:
made fresh from every count, the cap and the plan fed each other, 109, 110, 109, ... for good at some sizes, and a
press waiting for its count never went on, the author: the bug is back: I cannot save onto it because the button is grey; it notices neither the small width change nor my density change and the button stays grey), never under the
Resolution itself (over the limit, `Source.Limit` says so): past the cap `FineDetail` takes the next bigger exact
pixel, so the size stays exact with a few fewer pixels ("fewer to stay within the limit"), or steps when that is more
than 10 % fewer. Max therefore stays Max
at any size. A placed picture's recipe keeps the plan's width and pixel, so an edit starts from the pixels on the
sign (the Resolution then). The slider moves about 10 cm per screen pixel (0.25-30 m); centimetres are Shift + wheel
or a typed value. Fine Width off: the Resolution is the pixels and the size snaps to the sizes they take exactly.
The row moves only where the density does (0.20.133; the author: the slider should skip the dead stretches where the density does not change, and round a number typed in): at a
size, the exact pixels lie some pixels apart (a pixel is a multiple of 3.125 mm), so every Resolution between two of
them made the same picture ("Resolution 100: -1 across"). The slider, a typed number and Shift + wheel now go to the
pixels a Resolution really makes there (`PictureOptions.Achievable`: PlanFor's pixels, taken again until they make
themselves; tested to be a fixed point that never gives fewer pixels for more), so the thumb jumps over the dead
stretch and a typed number shows as the pixels it makes. Max, Native and a size change leave the Resolution as it is (the
Summary's shift line says what a size made of it).

**The live preview shows the result** (0.20.129; the author: it would be good if it reacted to the content too and
updated on every change, so we see the end result instead of the half-done old picture; useful when creating a new
picture too; scope: pictures,
texts and pieces at once; the green veil stays over it). Before, only Right/Up of a small stack showed (live apply
writes the world), Turn and Move moved the signs themselves, and Tilt, Rotate, a picture's Width and a new pick showed
the old content under a frame. Now, whenever something new would go on (`Session.Verb` not None, the sign can be
changed), `UI/ContentPreview` shows the result on this computer only: copies of the selected sign's text canvas (only
the canvas: a copy of the sign would be a networked piece; SignLink's own children stripped) stand where the stack's
signs will stand (the sign's own transform while the pose is its own, else `SignPose.Preview`; the copies
`FloorPlan.Step` or `SignBuilder.DefaultStep` apart, as `SignPose.Set` makes them). They show what the put writes:
`Source.Preview` gives a picture's quick view (the very grid Place encodes, as a RawImage at the plan's size and the
chosen place, as a GIF's screen; lit with the screens' material only when no pixel is see-through), a text or a piece
as `FitTexts.ForPut` writes it (`SignFit.Put` uses the same), and anything else that moves or is relit what the sign
holds as `FitTexts.ForRefit` (`SignFit.Refit` uses the same). A pose, a place or a picture's size only moves the copies;
texts are set again only when they change, over 3000 glyphs once the rows stand still (0.3 s, no control held), over
40 000 glyphs not at all (the frame only, as before). The sign's own content is switched off meanwhile (`HeldVeil`: its
text canvases, 0.20.130; emptying the texts before cost a full rebuild of the saved text to show it again, and a TMP
text switched back on builds its whole mesh again, a canvas keeps it); the preview's copies are shown and hidden by
their canvases the same way. The Text source measures its frame off the preview's first copy (`ContentPreview.First`). Nothing goes into the
world: others see the sign as it is until the button; live apply of small stacks is unchanged and ends the preview by
itself (nothing new to put on).

**Deep review 0.20.130** (the author: let's do a bug hunt and a deep review of it from several angles; six reviews:
world writes, the picture pipeline, the window, visual layers, the API, the live preview). What it settled:
- *A new pose is held up and lands on no other sign*: `SignPose.Check` weighs a tilt or a rotation as a move is weighed
  (`SignMove.Holds(..., remade: true)`: the signs are made again, their boxes anew), and every move, turn and pose is
  refused where the game builds nothing (`Location.IsInsideNoBuildLocation`) or where another sign would join the
  stack (`SignFinder.Intruder`: the next write would take it for a copy). A sign laid face up on a floor stood ~16 cm
  off it and broke after the game's first half minute.
- *The game's support boxes follow a written or undone move* (`SignMove.Rebox`: `WearNTear.m_colliders` dropped, made
  again where the signs stand); changes to one stack write only that stack's pending steps (`SignMove.WriteFor`); the
  check two seconds after a write never overrides a move another player made since (`zdo.IsOwner`).
- *A stack whose rearmost arrived sign is a copy is still loading* for `ArriveSeconds` (`SignFinder.IsLoaded`: the
  server sends the nearest first, so a viewer in front gets the first sign last); no stack is deeper than one stack
  reads (`SignBuilder.RequireDepth`); a write the game half-finishes (its view check on a "host" author) goes on.
- *A GIF's screen ends when its sign is written elsewhere* (`ScreenPlayer.Step`: the root's data revision, then its
  recipe and text compared; ScreenRestore brings it back if it still moves); screens and stored animations are dropped
  when the world is left; a veiled screen uploads no frames; parked frames are kept when new ones could not fit anyway.
- *The picture pipeline*: a press made on an estimate waits for Max's last count, is said when it cannot go on, and
  asks the red-level question when the count turns out very large; another choice stops Max; a failed grid is said
  ("These settings do not work"), not counted for ever; a file changed on disk since it was counted is read again,
  not placed unseen; the player's own options come back as they were (not the plan's pixels); a full area caps a fine
  size at the Resolution.
- *The API*: an animation written to the world (mode=zdo) sends at most 16 KB/s in a shared world
  (`TextAnimator.SharedBytesPerSecond`); a stopped server serves no queued request, and a waiting connection holds no
  pool thread; `host.docker.internal` counts as this computer only when the hosts file names it; `POST /signs/clone`
  spaces copies as the window does and writes the stack's count; E on a long text never opens the game's editor, even
  after an error turned the patch off.
- *The window*: an edge drag sizes the window at the frame's last event; Settings flush typed values on close; a tab
  change stops a key capture and drops keyboard focus; a step queued before a remake finds the new stack; the
  session's read is guarded and retried; Close drains actions queued by queued ones; the thumbnail cache never drops
  rows on screen; Show all frames the nearest 64 SignLink stacks within 100 m.
- The tidy-up after it (0.20.131) removed what the final behaviour no longer uses and shared what was written twice:
  `Session.First` (the stack's first sign), `Session.PendingPose` (the frame's and the preview's pose), `Fit.PoseOnly`
  and `Fit.WithPose`, `Change.LightOf(SignRecipe)`; one `ArtSource` (no `PresetSource`); `TextReadback` (was
  `TextPreview`: it reads pixel pictures back, it previews nothing).
- Left as they are (rulings): `POST /sign` writes one sign (a stack's copies keep theirs: documented); a Turn's raw
  rotation is not snapped to the save grid (`SignPose.Savable` loses digits at ±90°; a later same-pose refit remakes
  the stack once); a piece's or held animation's preview snaps by its first frame.

**Optimisation and security review 0.20.132** (the author: let's do an optimisation and a security review too; six reviews: the window's
per-frame cost, the runtime in the world, the picture pipeline and memory, the local API, data from the world and
files, Unity objects and threads; fixed in three parallel branches and merged, then one review of the whole). What it
settled:
- *Nothing keeps a destroyed sign*: caches keyed by `Sign` drop the destroyed ones when `SignFinder.Gone` rises
  (`SignFinder.Prune`). A destroyed sign stays a C# object while anything holds it, and with it its TMP buffers
  (megabytes for a picture's band): the caches piled up 70-80 MB with every return to a big base.
- *Work only when something changed*: FastRefresh builds a mesh again only when the shown text changed, 20 ms a step
  at most; the frame (`Ghost.Inputs`, compared before its key is made), the hover frame (the mouse and the camera
  still), the text's red level, the area count (every 2 s) and a stack found whole are not worked out again for
  nothing; Show all stacks only from SignLink's marked signs; the far list reads the zones within 100 m
  (`FindSectorObjects`: `GetAllZDOsWithPrefabIterative` reads the sector it pauses at twice, "2 signs"); a long list
  draws the rows in view (`ListRows`); `Widgets.W`/`H` make an option once per size; `useGUILayout` stays on (an Open
  from inside OnGUI would miss its Layout pass; 0.20.0 turned it back on for that).
- *The picture pipeline holds less*: `Resampler.Grid` makes each channel's rows from the RGBA bytes as it reads them
  (no full-size float planes: some 190 MB less for a 12 MP photo at Native); a GIF's frames are shrunk as they are
  decoded (one set, not two), its Update reuses the frames playing or parked (`ScreenPlayer.FramesFor`), parked frames
  go after `ParkSeconds` (10 min); work nobody waits for stops (`Job.WantStoppable`: a staleness test the work asks,
  a photo's main-thread decode included); `TextTransform` scans tags by hand, once (the regex passes allocated per tag).
  Every output stays byte-identical: the old code is the tests' oracle (`SignLink.Tests/Oracles.cs`).
- *Data from the world is bounded*: saved animations are judged within PlayDistance + 16 m and read as they unpack
  (`AnimBlob.MaxChars`, 16M characters: every blob an older version played), and one left out for room is not unpacked again
  until it comes nearer; a fit, pose or base kept on a sign must be sane (`Fit.Sane`, `StackMath.SanePose`,
  `SaneBase`: finite, a base within 6 m of its sign) or counts as none; a screen's place and pixel are checked;
  `CopyDropPatch` acts on signs only; a GIF named by another player's recipe is decoded at most once a minute and
  never a Windows device name; an EXIF preview over 1 MP is not decoded; a sign whose text the game hides
  (`m_isViewable`: a muted or blocked author) shows none of it in the window either.
- *The local API* (`HttpRules`, tested): exact routes take a body (413 elsewhere, 411 without a Content-Length), heads
  and bodies have hard deadlines, a clone's step is 0 or 0.3-2 mm and its copies must fit one stack before anything
  runs, `/anim` takes 20000 frames at most, the network mode answers local networks only, this computer is never
  locked out, the page forgets a remembered password the API refuses, a copied password leaves the clipboard after
  30 s and Paste refuses it.
- Left as they are (rulings): the far list's names show for muted authors (no sign loaded to ask the game); saved
  animation frames skip the game's word filter (Known limits); EXIF orientation's extra copy, `TextReadback`'s
  per-line allocations, a second encode of big bands in single player and main-thread list thumbnails (small or rare).

**Where a setting lives**: a setting of the content (a picture's glow, a piece's light) sits with the content on
2 Content (`UI/LightRow.cs`), one of the sign's pose or the content's place on 3 Place (`FitPanel`); a text's light is
its own tags, so the Text source rewrites the text instead of keeping a mode. Where the sign itself stands (Move the sign) is *Sign
positioning* on 3 Place, how it turns (its pose, Turn, Rotate: 0.20.66) *Sign rotations*, how the content sits on it
*Content positioning* (0.20.8: the author stood an emptied sign up again and found the pose rows greyed without
content; an empty sign's pose change is an *Apply*). A preference (Highlight signs, the window's size, text size and opacity) is on Settings, not on
a step. 2 Content never puts anything on the sign:
its footer leads to 3 Place, where the one verb button is (`ContentTab.DrawVerbButton`), so the pose, the place and
the frame in the world are seen first and nothing is placed twice.

**A grey button says why** (0.20.134; the author: whenever the button is grey because the mod is still calculating, the button should read calculating): grey while its source still reads or counts
(`Source.Calculating`: the folder, the picture, its count, a piece; a press waiting for its count), the verb button and
2 Content's way on say "Calculating ..." (the line under it what is counted); 20 s of it puts the reason in the log.

**SignLink and the dev tool** (the author, 2026-10-13: here, though, I would draw the line between what the SignLink mod is and what our developer tool is; SignLink having its own API is already overpowered, I would not go further ... when we publish, I do not want this loader approach in my mod, I want it as a pure mod): SignLink is a pure mod. What it ships
is the plugin and its own sign API, and that API is the limit: it gets no screenshots, no driving of its window, no
access to other objects. Everything for developing it (ScriptEngine's reload, screenshots, a window driven by a test,
UnityExplorer, the developer bridge and its MCP server) belongs to a separate dev-tool mod and project, never to
SignLink's package. The clean unload below stays in SignLink: every mod should undo what it changed when it unloads,
and it names no loader.

**Reloading in a running game** (0.20.135; the author: now let's build the OnDestroy solution mentioned; it is great, a pity we did not do it earlier): ScriptEngine (BepInEx.Debug r11.1)
loads plugins from `BepInEx/scripts` under a new assembly name each time and, on a reload (its file watcher, or F6),
destroys the old copy's GameObject first: `Plugin.OnDestroy` and `SignLinkWindow.OnDisable`/`OnDestroy` run, and the new
copy's `Awake` a frame later. So everything SignLink changes in the game is undone there: the window is shut as a
player shuts it (moves written; not while the game quits, `Application.quitting`), the API's and stored animations give
their signs their saved texts back, screens stop, the HTTP server closes its port, the patches go, and every Unity
object it made is destroyed (selection and hover frames, Show all's frames, the preview's copies, quick views and list
thumbnails, the frames' shared materials and meshes `Ghost.ReleaseShared`, the window's textures `Theme.Release`). The
new copy's registry starts empty while the signs awoke long ago: `SignFinder.Seed` adds every loaded sign as long
arrived. Statics are the old assembly's and stay with it (Mono never unloads an assembly), so a new rule: whatever a
feature creates in the game (objects, patches, subscriptions to the game's events, threads, ports) gets its undo in
these OnDestroy paths. Only one copy may load (the same GUID is refused): `./sign.sh dev` puts it in `scripts/` and
takes it out of `plugins/`, `./sign.sh plugin` the other way.

**What is enabled when** (0.20.18, the author's rule: greyed wherever nothing can happen). One idea: without a sign
you can look, not set; with a sign a ward keeps from you (`Session.CanEdit`) nothing is set either (`Session.Reason`
names it, under the bar). Making and removing signs is allowed everywhere since 0.20.44 (no building switch).

| Where | Control | Enabled when |
|---|---|---|
| target bar | Pick in view, Signs nearby, the list's rows | always |
| 1 Sign | Clear | a sign, editable, holds something |
| 1 Sign | Copy | a sign that holds something |
| 1 Sign | Paste | a sign, editable (over content: asks twice; over a multiplayer limit it fails and says why) |
| 1 Sign | Delete | a sign, editable |
| 1 Sign | 2 Content ▸ | a sign |
| 1 Sign | Stop the tool's animation (a GIF's Pause / Play is on Playing since 0.20.117) | a tool's animation playing on the stack |
| 2 Content | the source strip, every list, Reload, Open folder | always |
| the tab strip | 2 Content, 3 Place | a selected sign (`SignLinkWindow.NeedsSign`, 0.20.73); the picker is on 1 Sign only |
| 2 Content | Light (every source's last group, under its own title, 0.20.69), a picture's pixel options, the text box, Reset (the sign's text) | `Source.Ready`: a sign, editable (`Source.NotReady` says so under the rows) |
| 2 Content | Hide list (Picture, Art) | something picked: with nothing picked the list is all there is (0.20.69) |
| 2 Content | 3 Place ▸ | a sign, nothing picked wrong (`Source.Problem`), within the multiplayer limits (`Source.Limit`), no `Reason` |
| 3 Place | the pose rows, Move the sign, the content rows | a sign and no `Reason` (the content rows also need content, picked or held) |
| 3 Place | Undo changes | the chosen fit or light differs from the sign's (0.20.40: a new pick or new picture settings are not its to undo) |
| 3 Place | Content positioning, Content size (sections) | content that can move or be resized: otherwise shown closed, the title grey (0.20.77) |
| 3 Place | Reset to default (Content positioning) | content that can move, off the sign's middle (Right, Up; the size and the pose stay) |
| 3 Place | Right / Up | with the content rows: along the sign's face as you read it (no third direction: a sign's content has no depth, 0.20.27) |
| 3 Place | Apply sign coord changes, Undo (Move the sign) | steps or turns not applied yet (`SignMove.HasPending`); Apply writes them and makes the signs again where they stand (`SignPose.Remake`, 0.20.72: every player sees it at once); closing the window or selecting another sign undoes them (`SignMove.Revert`) |
| 3 Place | Reset to default (Changes) | moved, turned, tilted or rotated since it was built, or steps not applied (at once, for everyone: the signs made again where and as built; steps never applied are dropped) |
| 3 Place | Rotate, Tilt, the pose buttons (Sign rotations) | a sign (an empty one too: its pose changes with an *Apply*) |
| 3 Place | the verb button | per `Change.Decide`: greyed as "Select a sign first", "As it is", the problem or the multiplayer limit; "Working ..." while busy |
| Playing | Delete (a saved animation's frames; a GIF's movement: its recipe says still) | the sign is editable (a ward) |
| Playing | Pause / Play (both kinds, here only) | always |
| Settings | Playback: Pause beyond (from Playing, 0.20.54) | always |
| Playing | Pause all GIFs, Pause all glyph animations (0.20.163) | always |
| Settings | Sign options › Highlight signs (On / Off) | always; when off, the window says so under the selected sign (0.20.36, renamed 0.20.45) |
| 1 Sign | Show all SignLink signs (On / Off) | always; while on, no sign is selected; Pick in view, Signs nearby, E and Open in SignLink switch it off and pick (0.20.88) |
| Settings | Sign options › Highlight sign contents (On / Off) | Highlight signs on (the content's frame: an even green veil over what shows (0.20.112), the same in deep red where something stands in front of it (0.20.113), 25 % since 0.20.136 (45 % before), grey stripes from behind; 0.20.86, colours 0.20.88-0.20.89, 0.20.94; off: the board alone; 0.20.76) |
| Settings | the API's network switch, password buttons | the API is on (the password buttons: a password exists) |

**The multiplayer limits** (0.20.32, 0.20.33; the author after three large pictures froze a client on a dedicated
server: in multiplayer the limit should be 600 KB in an area and 300 KB on one sign). In a shared world
(`SignText.Shared`: on a server, hosting with Start server, or with peers) SignLink stores at most
`SaveCost.SignLimit` (300 KB, as `SaveCost.Bytes` counts: texts, 100 bytes a sign, a saved animation) on one sign
stack, and SignLink's signs within `SaveCost.AreaRadiusM` (100 m) of it hold at most `SaveCost.AreaLimit` (600 KB)
together, every player's (`SignArea.Others`, by SignLink's marks, `SignMarks.IsSignLinks`; vanilla signs do not count
since 0.20.67, the game's editor keeps each small; only signs loaded here count: about 128 m around the player at the classic view distance, about 64-90 m at the lower ones (`client/SimulationDistance.cs:41-49`, `client/ZNetScene.cs:360-367`), so a sign far from the player, or a low view distance, can miss signs at the area's edge (0.20.152 review; how to handle it is open).
`SaveCost.Allowed` decides both; content no bigger than what the sign holds now is always allowed, so a full area
can still be made lighter. The window greys the button with the reason (`Source.Limit`) and shows the area as a bar
on 2 Content (`AreaMeter`: "250 KB / 600 KB multiplayer limit within 100 m; with this: 370 KB") and in the window's
title (0.20.34, `SignLinkWindow.AreaTitle`: around the selected sign, else around the player); every
write refuses it as well (`SignText.RequireRoom` in `SignBuilder.SetContent`, `SignPose.Set` when it makes the signs
again, `SignPose.Remake`, `StoredAnims.Store`, the API's text write, clone and synced animations). Removing is
always allowed; single player has no limit and no meter.

**A picture's cost is estimated while it is counted again** (0.20.125; the author: the calculation runs 3-4 times or keeps
flicking the button below, which flashes yellow ... let's make a smooth solution). A Width, Height or Resolution step changes the
picture's options key, so its texts are counted again (`PictureSource.Count`, half a second after the rows stop). Until
then the last count of the same file (and stamp) scaled to the pixels now (`SaveCost.Scaled`, `PictureSource.Estimated`)
stands in: the rows read "about ... (counting ...)", the area meter and the limits use it, and the button does not go
grey and light up again after every step. A press on the estimate waits for the count (the button says "Working ...")
and then goes on only if the counted bytes pass `Problem` and `Source.Limit`; Max, Place's texts and every write use
the counted numbers only (`Have`), and `SignText.RequireRoom` still guards every write.

**Pieces are put centred** (0.20.38; the author: the glyph art pieces are not centred by default at 0-0-0). A
`Fit`'s Right and Up count from the content's centre as made (`c0`, NAME.fit `centre=`). Pictures are made centred
(`c0` = 0), but built-in and saved pieces were drawn where they were made, mostly above the sign, so 0, 0 left them
there. `ArtSource.Act` now puts a piece with `SignFit.Put(..., centred: true)`: `Fit.OffsetCentred` moves its
middle onto the sign's middle before Right and Up, and the sign stores 0, 0 as its centre as made, so Refit, the
frame in the world and the rows read Right and Up from the middle afterwards. Text keeps its place as written; a
piece put before 0.20.38 keeps its old centre until it is put again.

**The Detail model** (0.20.35; the author: only the density should affect the amount of data, and the
sizing should not). A picture in the window is `PictureOptions.Detail` pixels across (the data) and `WidthM` metres wide (only
the pixel's size): `PlanFor` takes the pixels first (at most the picture's own, within `MaxPixels`), then
`PitchFor(width, pixels)`, the nearest multiple of `PitchStep` (0.125 wu) up to `MaxPitch` whose half is a line height
TMP reads exactly (`ExactLineHeight`; about a quarter of the multiples drift under TMP's digit-by-digit reading, 1.375
among them). The width rows show and snap to `plan.WidthM`. `DetailAuto` picks `AutoDetail` (multiplayer: about
`SharedPixels` = 22 000; single player: 320 across, the old 2 m at size 2) until the player moves the row; a sign's
recipe stores the plan's own width and pixel (`SignRecipe.ForPicture(o, plan, name)`: the choice in
`PictureSource.Tick`, the mark when `PictureImporter` places it) and loads with Detail 0, so the window takes the
grid that width and pixel make. Large pixels make big tag numbers: `PixelText.Bands` halves a band the column encoding
cannot solve, so a very wide picture only takes more stacked signs.

**Text templates** (`Pictures/Snippets.cs`, `Pictures/GlyphData.cs`, `UI/TemplateRows.cs`, 0.20.19): a template writes lines of one sign text,
each glyph with all its tags restated, between two invisible anchor lines (size 1, voffset 0, alpha 0). TMP's Middle
alignment centres the block on the first line's ascender and the last line's descender only (`docs/sign-rendering.md`
7c), so the anchors make that a constant and every content line lands where asked by the engine's formulas; the
glyph metrics come from the game's font assets (`signart/palette.py` → `GlyphData.cs`, `./sign.sh palette`). The
output stays within what `TextTransform` scales, so 3 Place moves and resizes it. **The text in the live preview**
(`ContentPreview`, 0.20.129; a local one-frame animation on the selected sign itself before, `TextAnimator.Preview`):
the Text source works out the text as `FitTexts.ForPut` writes it a third of a second after the last change
(`TextSource.WorkOutPreview`), and the preview shows it for this computer only; it ends when the text empties, the
sign changes, the source closes or the text is written.

**Opacity** (`Theme.Ensure(scale, opacity)`): the generated background textures take the alpha (window, sections,
buttons, fields' insides, the toggle's inside); text, frames and the toggle's mark stay opaque, so the window reads at
50 %. A config value written on every slider step would save the file all through a drag: the Settings tab previews
the dragged value (`SettingsTab.PreviewOpacity`, read by `SignLinkWindow.Layout`) and writes the config once the
slider is let go (`hotControl` and `keyboardControl` both 0) or the window closes.

**Move the sign writes late, the base reads early** (`Signs/SignMove.cs`, `SignPose.Base`): a nudge or a turn moves
the signs at once and writes their ZDOs half a second after the last step (one update for a run of clicks). The pose
as built (`BaseP`, `BaseR` on the first sign) moves and turns with the stack at that write (`MoveBase`); until then
`SignPose.Base` applies the pending move and turn (`SignMove.Pending`, `PendingTurn`) to the stored base itself, so
whatever reads the pose (`Read`, `Preview`, `Frame`, the Session's "its pose changed, start from it") sees the same
pose before and after the write. Without that, a flat sign turned about the up axis read as content rotated in its
plane (`Explains` cannot tell a yaw from an in-plane turn on a flat sign), and 3 Place dropped the tilt the player had
just chosen (0.20.10-0.20.11).

**Moving GIFs come back** (`Animation/ScreenRestore.cs`): a screen lives only in memory, so a GIF stopped moving
once its sign unloaded or the player logged out, and the Playing tab forgot it (0.5-0.20.12). Every two seconds the
nearest loaded first sign whose recipe (`SignMarks.Source`, `SignRecipe.Animate`, a `.gif`) names a file in the
images folder and that has no screen is decoded again on a worker (one at a time, `GifDecoder` as the import) and its
screen started as the import did (`PictureImporter.PlayOver`: the plan from the recipe and the GIF's size, centre and
bottom from the fit on the sign, `SignFit.Read`). So it moves on every computer that has the file, and the frames
stay out of the world (a GIF's frames are megabytes, `docs/limits.md`). A sign passed over (no file,
a bad file, or stopped on the Playing tab or through the API) is remembered by its ZDO and recipe until the recipe
changes or the game restarts; one whose frames find no room in the screens' budget is tried again after a minute (0.20.152). The plan comes from the recipe's width and pixel, not its Resolution, which Fine Width may miss by a few pixels (0.20.152).

**A GIF is decoded once** (`ScreenPlayer.Park`, 0.20.14): a screen whose sign goes (the stack rewritten by
`SignBuilder.SetContent`, made again by `SignPose.Set`, or unloaded) parks its frames under the file's stamp
(`PictureImporter.Stamp`: name, length, write time), within the same 256 MB budget as the playing screens; the oldest
parked set goes when a screen needs the room. `ScreenRestore` takes parked frames before it reads the file, and the
window asks for a scan at once after any change (`Restore.Soon` on `Session.Changed`), so a moving GIF refits like a
still (its texts moved in place, `Change.Decide` → *Refit*; 0.20.7-0.20.13 forced an *Update* because a refit lost the
screen) and plays on within a frame or two. A screen the player stops drops its frames. What really needs a new
encode is a change of the still's pixels: width, pixel size, transparency, seamless pixels. From the author's log a
still's encode is cheap (the grid 90 ms, the texts 0.2-0.4 s for 360x640 over 7 signs); the costly parts are the GIF
decode (seconds, on a worker; 90 MB of frames for 137 frames at 360x640) and a rebuild of the stack when its pose
changes (about 4 s: every sign is made again, because the game places a sign only when it loads). Screens log their
start and stop and, after 3 s without a new frame, why (`ScreenPlayer.Idle`). The player pauses a screen rather than
stopping it (`ScreenPlayer.Pause`, 0.20.15): the frames and the row stay, Play goes on from the same frame, and
`ScreenRestore` keeps the pause for a screen restored after an unload (by ZDO and recipe). A real stop with no
restart is for tools only (`/screen/stop`, `ScreenRestore.Stopped`). The shown/s figure is a two-second window
(`RateWindow`); a figure since the start crept down while playback was off and never recovered.

**Moving GIFs for players without the file** (frozen 2026-10-11): the frames are not in the world, and putting a
version there is capped by the ZDO limit per sign (512 KiB including the band's text, often 100-394 KiB: little room,
only a coarse version would fit without extra carrier signs) and by the save and network cost (150 KB/s per peer);
the author chose not to load the world save with video and leans to a player-to-player transfer (RPC chunks from a
player who has the file, cached on disk) when the topic is picked up again.

**The GIF screens' light** (`Animation/ScreenPlayer.cs`): a `RawImage` without a material draws with Unity's default UI
material, which is unlit, so a GIF placed "lit by the scene" still glowed at night (its still under the screen was lit,
the screen was not; 0.20.2). A lit screen gets a material of `Legacy Shaders/Diffuse`: the Lambert surface shader in
the set every Unity build includes (`valheim_Data/Resources/unity_builtin_extra`; `Standard` is not in it), lit by the
same lights as the text's `TextMeshPro/Distance Field (Surface)` material, and a canvas mesh's normals face the canvas's
front like the text's. When the lookup fails the screen glows and the log says so once.

**Text size**: the styles are built at their pixel size for the window's scale (`Theme.Ensure`) and every literal
size goes through `Widgets.U`; nothing is drawn through `GUI.matrix` (magnified text is soft at 1440p and 4K).
Layout groups are `using` scopes (`Widgets.Horizontal`, `Vertical`, `Scroll`), so an exception inside one does not
mislay the window. State changes from clicks run at the first Layout event of a later frame (`SignLinkWindow.Later`).

**Keyboard focus**: never call `GUI.FocusControl` inside the window function during a mouse event. Unity's window
system then drops the mouse-up before it reaches the window (verified on Valheim 1.0.17, Unity 2022: the mouse-down
reached the window function and set the button hot, the mouse-up reached only the outer OnGUI), so every button stays
pressed and nothing fires, while keyboard input still works. A field's focus is dropped through `Widgets.DropFocus`,
which `SignLinkWindow.OnGUI` applies at a Layout event with `GUIUtility.hotControl == 0` (no mouse button held).

## Stored animations (survive logout, cost no network per frame)

- **Where.** The frames go into a byte array on the sign's ZDO (`ZDO.Set(int, byte[])`, `ZDO.cs:454`). Vanilla
  stores the gzipped map data the same way (`MapTable.cs:122`). Custom keys persist on dedicated servers
  (`ZDOExtraData.cs:94-103`).
- **Format.** A versioned header (fps, frame count, start time, hash of the static text), then
  `Utils.Compress` of the frames.
  - The vanilla `text` keeps one frame as the static picture for clients without the mod.
  - If the text no longer matches the stored hash, the animation is ignored: someone edited the sign by hand.
- **Size.** Write once, never per frame: every `Set` resends the whole ZDO. Keep a blob far below the 512 KiB
  network limit (`docs/limits.md`); the cap is to be set from measurements.
- **Playback.** Every SignLink client plays what it finds on a sign within range. The frame comes from the
  synchronised world time (`ZNet.GetTimeSeconds()`), so all players see the same frame. Playback is
  culled by distance, a global frame budget limits the cost, and a menu toggle turns all of it off.
- **Text refresh.** Vanilla rewrites the text widget every 2 s (`Sign.cs:39`, `:141-163`). Patch it so an
  animated sign is left alone; animate only when the sign is viewable (`m_isViewable`). `Signs/FastRefresh.cs`
  re-reads changed signs sooner for animations a tool writes over the network (`/anim` in `zdo` mode); since
  0.20.17 its pace is `3 - Sign options / RefreshSeconds` (0.1-2 s), 2 s by default, which is the game's own pace and
  runs nothing: the author called the switch legacy, and the direction is one local-playback module for art
  animations and GIFs alike, with nothing animated in the world.

## Packaging (Thunderstore)

- **`manifest.json`:**
  - `name` is `SignLink` (letters, digits and `_`);
  - `version_number` is semver with no suffix, and an uploaded version cannot change;
  - `description` is at most 250 characters;
  - `dependencies` is `["denikson-BepInExPack_Valheim-5.4.2351"]`;
  - `website_url` is the repo URL or `""`.
- **Package files:**
  - `icon.png` exactly 256 x 256;
  - `README.md` (UTF-8, shown on the site);
  - `CHANGELOG.md`;
  - the zip holds these files and `plugins/SignLink.dll`, with no game or BepInEx DLLs.
- **Categories:** the one list is in `docs/release-checklist.md` (Upload).
- **Team and categories:**
  - the team (namespace) is set on Thunderstore, not in the manifest;
  - categories: client-side, tools/building, and **AI Generated**, which the global rules require for
    AI-written code;
  - the rules also forbid obfuscated code and code downloaded at runtime, and require tested builds and an
    accurate README.
- **Build and publish.** `tcli build` from `thunderstore.toml` with copy entries for the DLL and the CHANGELOG;
  `tcli publish` with `TCLI_AUTH_TOKEN`.
- **CI (later).** A GitHub Actions build against the dedicated server's Managed folder (SteamCMD app 896660,
  anonymous) and the BepInEx pack.
- **License.** MIT is the norm (Jotunn, ServerSync, Azumatt).

## Sources

- **Thunderstore:**
  - wiki.thunderstore.io: creating-a-package, packaging-your-mods, moderation/global-rules;
  - thunderstore-cli (`Models/ThunderstoreProject.cs`, `Commands/BuildCommand.cs`).
- **Templates and libraries:**
  - AzumattDev/ServerSyncModTemplate;
  - Valheim-Modding/JotunnModStub;
  - AugusDogus/ValheimModTemplate;
  - Jotunn (`Managers/GUIManager.cs`, GUI and network-compatibility tutorials);
  - BepInEx v5-lts (`Chainloader.cs`, `KeyboardShortcut.cs`);
  - BepInEx.ConfigurationManager;
  - BepInEx.AssemblyPublicizer.
- **Mods:**
  - redseiko/ComfyMods (ColorfulPieces, ComfySigns);
  - JereKuusela (Infinity Hammer, Expand World Data, World Edit Commands).
- **Steam:** ValveSoftware/GameNetworkingSockets `steamnetworkingtypes.h` (512 KiB message limit).
