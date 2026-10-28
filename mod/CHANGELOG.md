# Changelog

## 1.2.0 (2026-10-28)

- **Moving GIFs at full quality again.** Since 1.0.2 a moving GIF played at most at twice its sign's pixels, so a GIF
  put on with a low Resolution (to keep the world small for the server) moved coarse and pixelated. It now plays at its
  own size again (at most 1024 pixels a side), whatever the Resolution of its still. The Resolution sets only the still
  that goes into the world, which everyone sees; the moving GIF comes from the file on each computer and costs the
  server nothing.
- Settings › Playback › **GIF quality**: *Full* (the default) or *Saver*, 1.0.2's way: at most twice the sign's pixels,
  about a quarter of the memory for a big GIF on a small sign, but coarser. It is this computer's choice alone;
  switched, the GIFs nearby start again at the new size (while *Play all GIFs* is off they show their still until it
  is on again). With Full, a big GIF takes more of the *GIF memory*: fewer
  move at once, and a long one may show fewer of its frames; 512 or 1024 MB gives them room.

## 1.1.0 (2026-10-21)

- A new look for the window: the game's own Averia Serif Libre font, rounded and framed controls, tabs and choices in a
  framed track with a line under the chosen one, a diamond before each heading, filled check boxes with a tick. It
  comes in two colour themes: **Classic** (dark wood and gold, the warm colours of before; the default) and **Nordic**
  (slate and amber). Settings › Window › Theme switches between them at once.
- The window is a little more solid by default (Opacity 0.85; a config file that already says 0.8 keeps it).
- The groups' arrows turn: hover a heading (3 Place's Content and Sign, the quick views, Art's groups) and its arrow
  turns the way a click would open or close it, and back when the mouse leaves.
- **The window speaks Hungarian, Chinese (Simplified) and Russian** besides English. Settings › Window › Language:
  *Game default* follows the game's language (English where SignLink has no translation for it), then English, Magyar,
  简体中文, Русский. Every text of the window, its messages and the hover line on a picture sign are translated.
- 1 Sign: *Pick in view* is now **Select sign**, and *Signs nearby* is the same grey button beside it; *Clear selection*
  sits at the bar's right. With no sign selected the bar says *Select a sign to work on*.
- *Show all SignLink signs* is a check box in the last row, beside Close, on every tab (grey and off while both
  highlights are off: nothing to frame with).
- Select sign and a click in the world find the sign under the cursor with any render scale: under 100 % the click
  landed off, further the further from the screen's lower left corner.
- They select a sign by its picture or moving GIF too, not only by its board (a picture moved up off its board, a
  tilted sign whose board is in the ground), and the empty part of a glyph art no longer hides the signs behind it.
- A click off any sign no longer ends Select sign: it waits for a sign, Esc or the window's key (Page Up).
- Look around with the window open: hold the right mouse button off the window and move the mouse; let go, the cursor
  is back where it was (also during Select sign). Not while one of the game's own panels is open (a trader's store, the
  map), and the mouse wheel stays the game's while you look.
- A click on a sign selects it with Show all SignLink signs on too (Show all switches off, as with Select sign).
- Signs nearby: the up and down arrows step through the list (shown while it is open).
- 3 Place: each group's *Changes* box is inside its group (Content's and Sign's), closed with it.
- 2 Content › Picture: the *Max (multiplayer)* button is now **Multiplayer max**.
- A quick view keeps its size while a picture loads ("Loading ..." stands in it): the window no longer jumps.
- A quick view (Art, Picture, 1 Sign's *What the sign holds*) stays closed while it has nothing to show and opens once
  it does.
- A picture picked again before the one between had loaded stayed on "Loading ..." (for good when Multiplayer max could
  not move the Resolution); it shows now, and a picture that could not be read is read again when picked again.
- Text: the editor's scroll bar is inside its frame.
- Art: the pieces are listed by their own names, without a heading line over a name's variants; more air between a
  group's name and its line, and between a list's thumbnails and their rows.
- 2 Content: *Art* and *Picture* read alike: a title row with the list's button, the pick's name, the *Quick view* (no
  line under it), *Light*, *Moving*, and a *Summary* on both; a click on a piece closes the list, as on Picture. Both
  quick views are equally tall, and the arrow keys' hint sits level with the list's button.
- The Summary, top to bottom: *Native pixels* (the file's) and *Resolution pixels* with how many of the file's go into
  one (1:4.5) on Picture, *Pixels* on Art; *Signs*; an animation's *Frames*; a PNG's free *See-through* pixels;
  *Content size* (was *World save*); in multiplayer only, *Multiplayer* (the limits) and *Arriving* (the download of a
  player who arrives). The size in metres
  went (3 Place sets it and shows it), and so did the seconds a player's computer takes to build the picture: that
  number was this computer's own, and under the multiplayer limits it stays below half a second. 1 Sign's *What the
  sign holds* reads alike: *Content size*, and *Multiplayer* only in multiplayer. The "very large" question before a put
  says the download and the content size. The "-9 px for this size" note under the pixels went: the
  *Resolution pixels* row shows what goes on. The *Native* / *Most* button is *Max for this size* (as many of the file's
  pixels as the size takes).
- The Area bar in the footers of 2 Content and 3 Place shows only in multiplayer, where its 600 KB limit holds; in
  single player the title says what SignLink's signs within 100 m hold, without a limit.
- The footer is as tall as what it shows: no empty grey stretch under the button once a longer line has gone (it
  still keeps its height while a slider is dragged, the wheel turns or a key moves the sign, so the button does not
  jump).
- 3 Place: a little air between the buttons of the Content and Sign *Changes* boxes; with another item picked the
  Content box says that the button below puts it on (its place and size too). Picture's *Width (free)* is *Width*.
- Playing: air between each row's Pause button and its *Moving* switch, and *here only* sits level with its button.
- 3 Place: putting a tipped sign back upright says so ("now standing"), and the footer no longer trips over a line
  that came between two draws of the same frame (a rare error in the log).
- The developer API is gone from SignLink's code altogether (off by default in 1.0.0-1.0.2, left out of the released mod
  since 1.0.3, in developer builds until now); any of its entries still in a config file leave it.
- 2 Content › Picture: *Max for this size* is lit only when it would give more pixels; the ratio beside the
  Resolution pixels no longer rounds a few dropped pixels to 1:1; with no sign selected, a pick over 300 KB says so
  in the multiplayer row.
- A picture file over 512 MB is not read (a video in the folder made the list read it whole), a picture that could not
  be read during a loading screen is read again, and *Open folder* on a read-only data folder no longer throws.
- 1 Sign: *Delete* says when a ward kept the sign, and when the sign is gone meanwhile.
- SignLink keeps working when one of its start steps fails (the images folder, an odd data folder), and a short burst
  of errors no longer switches the window off for the whole session.
- Art: with the list closed, the arrow keys step through the pieces and groups as the list would show them (after a
  group was closed with ←, ↓ went on inside it).
- The quick views let go of their pictures when the window closes (an art animation held its frames until the next pick).
- A damaged GIF no longer stops with an internal error.
- 1 Sign: the quick view of a long text (a QR code) no longer ends in a piece of a tag ("rotate").
- Playing and Settings no longer show an empty bar at the bottom.
- Settings › Sign options: the *Re-read changed signs every* slider is gone (it only sped up animations a developer tool
  wrote into signs; 2 s, the game's own pace, stays); its entry leaves the config file.
- Settings › Sign options: *Highlight signs* (every board outline: the selected sign's, Show all's, the sign under the
  cursor, the faint ones while you choose) and *Highlight sign contents* (every frame of where content goes) are two
  switches of their own; turning the first off no longer turns off the second.
- Settings › Window: Language, Theme and Text size are drop-down lists, and the group reads Language, Theme, Text size,
  Opacity, Window size (*Default size* at its right), Hotkey. A click on the hotkey's key changes it (the *Change key*
  button went); *No key* is *Unbind*. Every row fits one row in a narrow window.
- Settings › About has a *Graphics tip*: turn the game's Bloom off (glowing pictures stay sharp).
- A button that asks twice ("Sure? Click again": Clear, Paste, Delete, Reset to defaults) turns red while it waits for
  the second click.

## 1.0.3 (2026-10-18)

- Your pictures have a new home: `SignLink/images` in the game's own data folder, next to your worlds (on Windows
  `AppData/LocalLow/IronGate/Valheim/SignLink/images`). They were in `BepInEx/config/SignLink/images`, and a mod manager
  packs the config folder into every profile it exports or shares as a code, pictures and all. The first start of 1.0.3
  moves them over by itself (folders too); *Open folder* opens the new place.
- No web server in SignLink any more: the developer HTTP API and its browser page are gone from the released mod, which
  opens no port. Their settings leave the config file, the API password with them. The window does everything they did.
- *LogSlowSigns* is under *9 - Diagnostics* now (its value carries over).

## 1.0.2 (2026-10-18)

- *Text* has a *QR* template: a link or a text as a QR code that phones read, in Unicode squares on the sign, with its
  error correction, size and colours.
- A *Block* (or a *Pattern* of ■) stretched wide or narrow is drawn the size asked: a wide block came out a little
  short and a narrow one too wide (measured in the game).
- While a tilt or a rotation waits, the sign's board shows in that pose too: it stood as it was and cut through the
  picture shown tilted.
- The Sign group's *Apply sign coord changes* and *Undo steps* take a tilt and a rotation too, not only the moves.
- The sign's frame turns red (a hot scarlet, apart from the picture's crimson) while nothing would hold the sign where
  the steps or a tilt not yet applied put it: the same "No support" that 2 Content and 3 Place say in red.
- The highlight frames a block, a QR code or a pattern where it draws, and 3 Place shows its real size: the frame stood
  a few centimetres off a big block's sides, and a 1.4 m QR code read as 1.45 m wide.
- 2 Content starts with *Art*, then *Picture*, and the text editor comes last.
- The buttons at the bottom of 1 Sign and 2 Content say *Next*.
- The text editor's buttons sit in one row over the box: *Insert* first, in the way-on yellow whenever a template can go
  in, then *Reset*, *Clear*, *Undo* and *Redo*; the *Light* row is up over them, a line between. Before, Insert sat in
  the template's group, the other buttons and the light under the box. The box wraps its long lines: a line longer
  than the box ran out of it, its end never seen.
- The window says less: the long explanations under its rows are gone or a word or two; what stops a button still
  says why. Every hotkey shows where it works, in a small frame: Shift+wheel by the sliders, the arrow keys by the
  sign's fine-tune, ↑ ↓ (and ← → on Art) by the lists, Ctrl+Z / Ctrl+Y by Undo and Redo, the window's key and Esc by Close.
- The short line when you look at a picture sign, and E opening SignLink there, are always on: the switch is gone
  from Settings and from the config file. Off, the game showed a picture's whole text by the crosshair with a stutter,
  and its editor cut the picture; a config file that still said off kept it off with no way back in the window.
- A moving GIF holds its frames at most at twice its sign's pixels, not up to 1024 a side: a 720 px GIF on a 214 px sign
  takes 46 MB instead of 185 MB, so far more GIFs move at once. One GIF on many signs counts once against the 256 MB
  the moving GIFs share (its frames were shared but counted for every sign, so a third copy stayed still). The Moving row says before the put when the other GIFs playing leave no
  room, and Playing says so on the row of a GIF that waits for it.
- A GIFs bar shows how much of the 256 MB the moving GIFs hold here (Playing, and on 2 Content with the GIF picked,
  what it would add). Playing says when a GIF waits for that room. The Area bar shows in single player too, against a
  server's 600 KB, for information: nothing is refused there.
- Settings › Playback › *GIF memory*: 256 MB (the default), 512 or 1024 for the moving GIFs' frames. 1024 wants a PC
  with 16 GB of RAM. Lowered, the farthest GIFs stop until the rest fit; raised, the waiting ones start.
- A GIF beside you no longer waits for a far one's zone to unload: GIFs idle because they are far away (beyond the
  Playback distance) or paused give up their room to it. And a GIF waiting for room starts within seconds of room
  freeing (a GIF stops, its sign unloads, the memory is raised), not after a fixed minute; while it waits, its file is
  left alone (it was read whole every minute).
- A long GIF keeps every frame while its frames at the sign's size fit 200 MB (about four fifths of the GIF memory):
  the rule counted the full-size frames, so a GIF on a small sign kept every second frame for nothing. The Moving row
  on 2 Content follows the sign's size.
- The frame uploads of all the GIFs together take at most 5 ms of a game frame, the nearest first (the 50 ms a second
  rule was each GIF's own: many big GIFs together could stall a weak CPU).
- *Moving* switched off then on comes back at once (its frames were dropped and read again after a minute). A GIF whose
  file is missing or still being copied into the images folder is looked for again every minute, not passed over until
  a restart.
- Putting a GIF on a sign reads its file off the game's thread (a 35 MB GIF on a hard disk was a short stall), and the
  same GIF on another sign is never refused for room: it takes none.
- The API's `/image` shares a GIF's frames with the signs that already play it and keeps them parked when the sign
  unloads, as the window does (its screens were decoded and held apart).
- A GIF put on a sign while the other GIFs leave no room for its frames goes on still at once, with the reason, and
  plays when room frees: its frames were decoded first (seconds of work and a heap of memory) and dropped at the start.
  The API's `/image` says in a `note` whether the GIF plays, and its dry run decodes the still alone.
- A moving GIF comes with the mod: Muybridge's galloping horse (1887, public domain; 300 px, 15 frames). It arrives in
  the images folder with the paintings, so it moves for every SignLink player: every computer with the mod has the file.
- Folders in the images folder: copy a whole folder of pictures in, and the Picture list shows it. A click or → opens a
  folder, ← goes back up, ↑ ↓ walk the rows, and *Open folder* opens the folder shown. A sign remembers its picture's
  folder (`funny/cat.gif`), so a GIF moves on a friend's computer when the file sits in the same folder of their images
  folder; a name from a sign still names nothing outside that folder.
- The *Art* groups come in a new order: scenes, creatures, the animals of Valheim and the kitchen icons lead; the cat's
  family and its friends sit together near the end, above the necks.
- *Playing*: the *Delete* button is now the *Moving* switch. Off stops a GIF or a glyph animation moving for everyone,
  and the row stays, so Moving can go on again. A glyph animation keeps its frames (players still on 1.0.1 see it move).
- A GIF says on its *Moving* row, before it goes on, whether every frame moves, fewer of them (a long GIF), or
  none (too big to move: it goes on still). A GIF too long or too large to read says what the limits are.
  The limits are in the README.
- An art piece's or a text's *Width* and *Height* follow the mouse when dragged: they jumped back and forth before.
- 3 Place: the steps and the *Turn* slider no longer stop where the sign would have no support. The sign goes there for
  you; only the apply is refused, with the reason in red, until it is held again. A tilt or rotation it would have no
  support in greys the button below with the reason in red.
- Fixes from a review: a sign near a trader or a boss (where the game allows no building) takes no content that adds or
  removes its stacked copies; leaving a world forgets which animations and GIFs were paused or passed over there, so
  another world's signs with the same ids play; the developer API stops at once even while requests wait; a piece
  switched off on Playing pastes still (Copy and Paste carry *Moving* off) and stays off through a refit.

## 1.0.1 (2026-10-16)

- Many moving pieces in one place no longer make the game stutter twice a second: their updates are spread over the
  whole second.
- Licences made clear: the code stays MIT (LICENSE); the art library is CC BY 4.0 (LICENSE-ART). Playing with the
  pieces, and screenshots or videos of them, need no credit; sharing the pieces themselves does.

## 1.0.0 (2026-10-16)

The first public release.

- **Pictures on signs.** PNG, JPG and GIF from a folder (six public-domain paintings come with the mod), sized in
  metres; the Resolution sets the cost, *Max (multiplayer)* the most a server takes. Transparency stays transparent.
  A bigger picture spreads over stacked copies of the sign and still looks like one.
- **204 ready-made art pieces, 150 of them animated:** glyph art drawn with the game's runes and symbols, pixel art
  (cats, animals, Valheim's creatures and scenes) and kitchen icons. *Moving* puts an animated piece on moving.
- **Text with templates:** coloured blocks, labels, symbols and patterns in a click.
- **Glowing or lit by the scene.**
- **Any pose:** standing, face up for a floor, face down for a ceiling, tilted, turned, rotated, and moved in fine steps.
- **Three steps in one window:** 1 Sign, 2 Content, 3 Place. A live preview and a highlight in the world (green where
  it shows, red where something hides it) show the result for you only; nothing goes on the sign before the button,
  and *Undo changes* drops what is not applied.
- **Edit later**, another player's picture too; **Copy and Paste** carry a piece to another sign, a moving one moving.
- **Playing tab:** pause or play GIFs and glyph animations on your computer.
- **Everyone sees it:** players without the mod see the pictures (an animation's first frame); the server needs
  nothing; remove the mod and the signs stay.
- **Multiplayer limits:** at most 300 KB on a sign and 600 KB for SignLink signs within 100 m; wards are respected.
  Single player has none.
- An optional developer API (off by default).

The versions before 1.0 are in [`docs/changelog-0.x.md`](https://github.com/chestercs/valheim-signlink-mod/blob/main/docs/changelog-0.x.md).
