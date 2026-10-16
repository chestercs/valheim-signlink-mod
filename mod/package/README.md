# SignLink

Put glyph art, coloured text and pictures into your world as sign text. **Every player sees them, with or without
the mod**, because they are ordinary signs saved in the world. Only the player who makes them needs SignLink.

![The Starry Night in a wooden frame on a Valheim house at night](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/framed-painting.jpg)

**204 ready-made art pieces come with the mod, and 150 of them are animated** ([see them all](https://github.com/chestercs/valheim-signlink-mod/blob/main/docs/art-library.md)):

![Animated pixel animals of Valheim](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/art/animals.gif)
![Animated pixel scenes from Valheim](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/art/scenes.gif)

| Floor mural | Ceiling fresco |
|---|---|
| ![The Great Wave as a floor mosaic](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/floor-mural.jpg) | ![The Creation of Adam on a ceiling](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/ceiling.jpg) |
| **Glyph art** | **Signposts and labels** |
| ![Glyph art emblems on a gable](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/glyph-art.jpg) | ![Signposts made with the text templates](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/text-templates.jpg) |

**Links:** [GitHub](https://github.com/chestercs/valheim-signlink-mod) (source, documentation) ·
[Wiki](https://github.com/chestercs/valheim-signlink-mod/wiki) ·
[Nexus Mods](https://www.nexusmods.com/valheim/mods/4453) ·
[Thunderstore](https://thunderstore.io/c/valheim/p/PetyusPolisz/SignLink/) ·
[Buy me a coffee](https://buymeacoffee.com/chestercs)

## What it does

- **Ready-made art.** Over 200 pieces in 15 groups: glyph art (pictures drawn with letters and symbols: Huginn and
  Muninn, a dragon ship, Eikthyr, Mjolnir, Jormungandr and more), pixel art (the cat and its friends, a boar family,
  necks, hens and chicks, forest animals, bears, wolves and moose, seals, fish and sea serpents, a fjord at dusk) and
  kitchen icons for chest labels. The animated ones go on moving; switch *Moving* off for a still.
- **Text with templates.** Write coloured text, or put ready pieces in a click: a coloured block to paint a wall or a
  floor, a label, a symbol from a palette (runes, arrows, frames, emoji), a two-colour pattern.
- **Pictures on signs.** PNG, JPG or GIF (its first frame) become sign text in game, sized in metres (phone photos
  the right way up). A sign shows up to 50 000 pixels; a bigger picture goes over copies of the sign stacked a
  fraction of a millimetre apart, so it stays one flat, seamless picture (up to a million pixels, in single player).
  On a server every player who arrives downloads it first, at 150 KB/s at best, and the objects around it wait
  behind it, so a multiplayer world has hard limits: at most 300 KB on one sign and 600 KB for all the SignLink signs
  within 100 m, every player's (a bar on the window shows how full the area is). The window shows the wait and the limits before you place
  anything; single player has none.
- **Transparency.** See-through parts of a PNG stay empty (and cost nothing), half-transparent parts stay half
  transparent.
- **Any pose.** Tilt a sign from standing to face up (a picture on the floor) or face down (read from below, for a
  ceiling), rotate it, resize and move what it holds along its face. The place you set stays put whatever the tilt.
- **Move or turn the sign itself** from a millimetre to a metre at a time (buttons, arrow keys or the Turn slider,
  3 Place), as far as the game still holds it up (up to 5 m: a sign standing on a floor goes metres towards or away, one
  on a wall barely off it) and any angle, without making it again; a step that would leave it too weakly held, so the
  game would break it, is refused. Steps show for you at once; *Apply sign coord changes* makes them everyone's, *Undo*
  takes them back.
- **Animations saved in the world.** They play for every SignLink player on a shared clock and survive restarts;
  players without the mod see the first frame.
- **Glowing or lit.** Pictures glow in their own colours day and night, or take the scene's light like normal sign
  text.
- **Multiplayer-safe.** Every sign of a picture stays under 400 KB, below the 512 KiB a sign can carry over the
  network, always.

## How to use

1. Install with r2modman or the Thunderstore Mod Manager (BepInExPack Valheim is installed with it).
2. Put your pictures (PNG, JPG or GIF) into `BepInEx/config/SignLink/images` (*Open folder* on the Picture source opens it;
   r2modman: *Settings › Browse profile folder*). Six public-domain paintings are already there to start with (Van Gogh,
   Hokusai, Michelangelo, Monet and two by Arbo).
3. In game, build a sign where the picture should go, then press **PageUp** (or type `/signlink` in the chat; the key
   can be changed or switched off on Settings, and the game's own sign editor has an *Open in SignLink* button).
4. Select the sign on **1 Sign**: *Pick in view* (the window hides until you click a sign; the one under the cursor
   wears a white frame), or choose it from *Signs nearby*. Opening the window does not take the sign you look at. **E** on a sign that already holds a picture, or *Open in SignLink* in the game's sign editor,
   opens SignLink on that sign: on 3 Place to edit a sign SignLink made, on 2 Content for a new one; the hotkey does
   the same while a sign is selected. A selected sign stays selected while the window is closed, until you go far from it.
   Until a sign is selected, *2 Content* and *3 Place* stay grey.
5. **2 Content**: write a *Text*, pick a built-in piece under *Art*, or pick a
   *Picture* file, look at the preview and the cost, and set its light (glowing, or lit by the scene like the game's
   own sign text). A picture has a **Resolution** (how many pixels wide it is: the only thing that sets what it
   costs; a new picture starts at *Max (multiplayer)*, the most that fits a server, and *Native* takes every pixel
   the file has) and a **Width** in metres, which makes the pixels
   bigger or smaller (with *Fine Width steps*, on by default, the exact size takes a few pixels more or less). On *Text* the templates
   write ready pieces in a click: a coloured *Block* in metres (paint a wall or a floor, translucent if you like),
   a *Label*, a *Symbol* from a palette (arrows, shapes, frames, runes, symbols, emoji), a *Pattern* in two colours.
   The live preview shows every pick (a text, a piece, a picture) on the selected sign, for you only, until step 6 puts it on.
6. **3 Place**: size it, move it, tilt or turn it if you like (the highlight shows where it goes), then press the button
   at the bottom (*Update content*, *Update sign* or *Update sign and content*: what changes). Nothing goes on the sign
   before that.
7. To change it later, select its sign: 2 Content and 3 Place show what it was made with. On 3 Place the *Content*
   group's changes have their own *Apply content changes*, *Undo* and *Reset to default*; *Undo changes* at the bottom
   drops everything not applied yet.
8. **1 Sign**: copy, paste, clear or delete it. *Made with* tells which SignLink version made it. Moving the sign
   itself a little (buttons or arrow keys) is on 3 Place in the *Sign* group (*Positioning fine-tune*); standing it up,
   laying it down, tilting and turning it there too (*Rotations*), an empty sign too.

| 1 Sign | 2 Content | 3 Place |
|---|---|---|
| ![Step 1: the Sign tab](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/window-1-sign.jpg) | ![Step 2: the Content tab](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/window-2-content.jpg) | ![Step 3: the Place tab](https://raw.githubusercontent.com/chestercs/valheim-signlink-mod/main/docs/media/window-3-place.jpg) |

Every slider takes a typed value too, and **Shift + mouse wheel** over a slider's row turns it a fine step a notch
(Tilt and Rotate 1°, Right 5 cm, Width about a pixel, Resolution to the next it makes); the plain wheel
scrolls the window.

The window shows what a picture costs before you place it: how many signs, how much it adds to the world save, and
how long every player who arrives waits for it on a server (the download at 150 KB/s at best, then the build).

## Multiplayer and servers

- Nobody else needs the mod to see your pictures. The server needs nothing either.
- **Hard limits in multiplayer.** On a server every player who arrives downloads all the signs around them, and the
  buildings there wait behind them. So in a world others share (on a server, or hosting with Start server)
  SignLink stores at most **300 KB on one sign** and **600 KB for all the SignLink signs within 100 m** (every
  player's together; vanilla signs do not count: the game keeps each small). The window's title shows how full the area is, and the *Area* bar on 2 Content shows it
  with what you picked in place of the sign's content. Over a limit the button stays grey and says why; making a sign lighter or deleting
  it always works. Single player has no limits.
- **A picture larger than one sign creates extra signs** (the stacked copies) without using materials, in single
  player and in multiplayer alike; the multiplayer limits above keep them small. Wards are always respected.
- Each copy of a picture is a sign that no player placed: the hammer takes it down, and it drops nothing for
  SignLink players (when a copy's data is owned by a player without the mod, their game drops a piece of wood). The
  *Delete* button on the Sign tab removes a whole stack.
- **Another player's picture can be edited** without its file: SignLink reads it back from the sign itself. Its size
  and place change freely, its Resolution only goes down (the sign holds no more pixels), and a moving GIF stays
  still on your computer (whoever has the file still sees it move). Wards are respected.
- **Copy and Paste** on the Sign tab carry what made a picture or a piece and how it sits, so the pasted sign edits
  like the original; a moving piece pastes moving.
- A pose survives restarts.
- A sign moved under *Positioning fine-tune* shows only for you until *Apply sign coord changes*, which makes its
  signs again where they stand:
  every player sees it there at once (everyone near loads the picture again). *Reset to default* puts it back where
  and as it was built, for everyone.

## Settings

Settings are on the window's *Settings* tab; each one is also a line in the config file
`BepInEx/config/PetyusPolisz.SignLink.cfg` (its sections are named below):

| Setting | Default | Where | What it does |
|---|---|---|---|
| 1 - Menu / Toggle | PageUp | Settings | opens and closes the window (*Change key* binds another, with Ctrl, Shift or Alt; *No key* switches it off: /signlink, E on a picture sign and the editor's *Open in SignLink* remain) |
| 1 - Menu / Scale, Width, Height | 1, 0 (automatic), 0.86 | Settings (text size, Default size); the window's edges and corners size it | text size, and the window's width and height as shares of the screen, apart (up to the whole screen; width 0 is as wide as 80 % of the screen's height, on any screen shape) |
| 1 - Menu / Opacity | 0.8 | Settings | how solid the window is (0.5: the world shows through it; the text stays opaque) |
| 1 - Menu / WindowX, WindowY | -1 | the file | where the window was dragged (-1: the default place) |
| 3 - Sign options / Highlight | on | Settings › Sign options | while the window is open, the selected sign and the sign under the cursor are outlined in the world, with where the content goes |
| 3 - Sign options / HighlightContent | on | Settings › Sign options | with Highlight on, where the content goes is framed too: green over what will show, red where a wall or a floor stands in front of it, grey stripes seen from behind (off: the sign's board alone) |
| 3 - Sign options / PreventEditor | on | Settings › Sign options | on signs holding SignLink content, E opens SignLink instead of the game's editor (whose Enter would cut the picture to 50 characters), and the hover text stays one short line |
| 3 - Sign options / RefreshSeconds | 2 s | Settings › Sign options | how often changed signs are re-read (2 s is the game's own pace; lower only for animations a tool writes over the network) |
| 4 - Playback / PlayAnimations, PlayGifs | on, on | Playing (Pause all / Play all) | glyph animations and moving GIFs play on this computer, each with its own button |
| 4 - Playback / Distance | 64 m | Settings › Playback | farther animations and GIFs pause |
| 5 - Pictures / PngTransparency | on | Settings | how every picture is made from now on: a PNG's see-through parts stay empty (every sign of a picture stays under 400 KB, always) |
| 5 - Pictures / MovingGifs | on | 2 Content (Moving, on a GIF) | a GIF goes on moving over its still wherever its file is in the images folder; the switch keeps your last choice |
| 5 - Pictures / FineWidth | on | Settings | A picture's Width and Height move finely (dragged, typed, Shift + mouse wheel); your Resolution stays, the exact size takes the pixels nearest it (a few more or less), never over the multiplayer limit; off: the next size it takes for the same data |
| 9 - Developer API / Enabled, AllowNetwork, Port, Password | off, off, 8417 | Settings | a local HTTP API for developers' tools; players do not need it |
| 9 - Developer API / LogSlowSigns | off | the file | logs sign hover texts and text refreshes that take over 30 ms (to find stalls) |

**Developer API.** Off by default. On, it listens on this computer only (127.0.0.1) unless *AllowNetwork* (then it
answers local networks only); HTTP is not encrypted, so use it on a home network only; every request needs the password
shown on the Settings tab.

**Manual install:** copy `SignLink.dll` into `BepInEx/plugins`. Keyboard and mouse only (no controller). A dedicated
server does not need it.

**Uninstall:** remove it in the mod manager, or delete `BepInEx/plugins/SignLink.dll`. The pictures stay as ordinary
signs. Optional: delete `BepInEx/config/SignLink/` (your images), `BepInEx/cache/SignLink/` (thumbnails) and
`BepInEx/config/PetyusPolisz.SignLink.cfg`.

## Known limits

- A picture moved off its sign is picked by the sign's board (where the sign stands), not by the picture.
- Big pictures take a moment to appear when a player walks up (about 13 µs per pixel, on every player's game).
- Without SignLink, pressing E on a picture sign opens the game's editor; pressing Enter there cuts the sign's text
  to 50 characters (Escape leaves it alone). Such a player's game also slows down while the crosshair rests on a
  picture sign's board up close (the game reads the whole picture for its hover hint). Hiding the sign in a wall or
  the ground, with only its picture out, avoids both.
- A GIF moves over its picture on every computer that has the GIF file in its `BepInEx/config/SignLink/images`
  folder (give the file to your friends); everyone else sees its first frame. Its frames are not saved in the world.
- The live preview builds a piece, or a sign's content moved, of up to 40 000 glyphs; a bigger one shows only the
  highlight's rectangle until it goes on (a picture or a text always shows).

## About this mod

SignLink is made by ChesTeRcs (team PetyusPolisz). It was written with Claude (Anthropic's AI) under their direction
and tested on their worlds with Valheim 1.0.20. The code is MIT licensed (LICENSE); the art library is CC BY 4.0
(LICENSE-ART): playing with the pieces needs no credit, sharing the pieces themselves does. The six paintings that
come with it are public domain: The Starry Night (Van Gogh), The Great Wave off Kanagawa (Hokusai), The Creation of Adam (Michelangelo),
Impression, Sunrise (Monet), and The Wild Hunt of Odin and Valkyrien (Arbo).
