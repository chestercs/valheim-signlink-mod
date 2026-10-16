# SignLink: live sign art for Valheim (BepInEx plugin)

A client-side plugin, vanilla compatible. It puts high-quality pictures into a world as sign text that every
player sees, with or without the mod. Programs and web pages can write, animate and create sign art in the
running game, and players who have the mod see texture "screens".
- `signart/live.py` is its command-line client.
- `docs/mod-standards.md` has the conventions and the publishing plan.
- `docs/limits.md` has the numbers.
- `CHANGELOG.md` lists the versions.

## Versions

`CHANGELOG.md` lists the versions for players; the pre-release versions (0.3 to 0.20.194) are in
`docs/changelog-0.x.md`.

## Pictures for vanilla players

A picture is sign text: one ■ glyph per pixel, column by column (`docs/sign-rendering.md`). One sign shows at
most 50 000 pixels and must stay under 512 KiB to be sent in multiplayer, so a big picture is split into bands
of whole rows. Every band goes on its own copy of the sign. The copies hang at the same rotation, 0.5 mm apart
along the sign's normal, which keeps the picture flat and seamless. Each pixel glyph is two pixels wide by
default: from a distance the SDF shader draws small glyphs half transparent, and 2 x 2 layers make the picture
opaque again.

The C# encoder (`Pictures/`) writes the same texts as the Python pipeline (`signart/vanilla_hq.py`), byte for
byte. `./sign.sh modtest` checks this on number formatting, line heights, fits, band texts (the whole Starry
Night and a GIF frame) and resampling.

## Layout

```
mod/SignLink/
  Plugin.cs              [BepInPlugin] + [BepInProcess("valheim.exe")], wiring; patches applied one class at a time
  PluginConfig.cs        every config entry (ConfigurationManager order)
  ConfigMoves.cs         older config files' values moved to today's sections and names (tested)
  Commands.cs            /signlink (chat) and signlink (console)
  Api/                   LocalHttpServer (security), HttpRules (its rules apart from sockets: bodies, deadlines, peers,
                         lock-out; tested), MainThread (Run, Post), Routes (endpoints), Json, Password (its rules)
  Signs/                 SignFinder (every loaded sign, stacks, loading), SignMarks (SignLink's marks, the source record),
                         SignText (read, write, sizes, wards, author), SignRecipe (what made the content), SignBuilder
                         (clone, delete, SetContent), SignPose (tilt, turn: rebuilds), SignFit (size and place on the
                         sign), SignMove (move in place), SupportMath (the game's support, for moves), SignArea (the
                         multiplayer area's bytes), SignClip (Copy and Paste), FloorPlan, FastRefresh, StackMath
  Pictures/              pure C#: PyMath, PixelText (column encoding), Resampler (Lanczos; Box for thumbnails), RawPixels
                         (texture bytes to RGBA, halved), ImageHeader (sizes, EXIF orientation and preview), GifDecoder,
                         PictureOptions (the plan: pixels and their size), Fit, FitTexts (the texts a fit writes),
                         TextTransform, TextReadback (pixel pictures read back), SignGlow, SaveCost (bytes, load time,
                         the limits), SignPicture (a picture read back from its sign), Snippets and GlyphData (the text
                         templates); PictureImporter (decode, encode on a worker, place), ImportJob
  Animation/             TextAnimator (sign-text frames), StoredAnims (frames saved on the sign, world clock), AnimBlob
                         (their bytes), ScreenPlayer (texture screens; hide their stack's text), FrameTimes (a GIF's
                         delays), ScreenRestore (a moving GIF back when its sign loads)
  Patches/               HoverTextPatch (ghost face), PictureSignPatch (hover line, E opens SignLink), CopyDropPatch,
                         InputBlockPatch (window open = TextInput visible), InventoryBlockPatch, ScrollBlockPatch,
                         SignRegistryPatch (every sign registers on Awake), SlowSignPatch (logs slow hover and refresh)
  Presets/               Preset, ArtLibrary (built into the DLL)
  Art/                   the built-in glyph and pixel art (./sign.sh art)
  UI/                    SignLinkWindow (shell: target bar, picking, footer, one status line), Session (the shared state:
                         the sign, its stack, what it holds, the choice slots, the verb), Jobs (Job<T>: keyed, settled
                         background work), Change (the footer's verb), Tab, Tabs/ (SignTab, ContentTab, PlaceTab,
                         PlayingTab, SettingsTab), Sources/ (Source, PictureSource, ArtSource, TextSource), SourceFollow
                         (which source shows), FitPanel, SizeRows, LightRow, MovePanel (Move the sign), TemplateRows and
                         TextHistory (the Text editor's templates, Undo), Confirm, Armed (two-click buttons), Thumb
                         (quick views), Thumbs and ThumbCache (list thumbnails, cached on disk), ListRows (a long list
                         draws the rows in view), Ghost and Hatch (the
                         frame in the world), ContentPreview (the live preview), HeldVeil (the sign's own hidden
                         meanwhile), ShowAll (every SignLink sign framed), AreaMeter (the area bar), Widgets (U, scopes,
                         number fields), FineStep (Shift + wheel), WindowSizing, HotkeyText, Theme (styles at the text
                         scale)
  Web/index.html         the browser import page (served by the developer API)
mod/SignLink.Tests/      the pure C# picture code against vectors from signart/encoder_vectors.py (.NET 8)
mod/RefCheck/            fails the build when the DLL references an assembly the game does not have
mod/package/             manifest, icon, the Thunderstore README, LICENSE, package.py
```

## The import page (browser)

With the developer API on, `http://127.0.0.1:8417/` serves a page from the DLL (`Web/index.html`). It needs the
master password like any other client.
1. Pick a sign: the one under your crosshair, or one from the list of signs within 30 m (the API page).
2. Drop a PNG, JPG or GIF.
3. Set the width in metres, the pixel size (the window itself uses Detail, pixels across, since 0.20.35), the pose (as the sign is, standing, face up, face down), rotate, the place
   (right and up along the sign's face), glowing or lit, seamless pixels; PNG transparency and moving GIFs
   are set once on Settings (Pictures, Playback).
4. Check the size, then place it.

With `AllowNetwork` on, a phone on the same home network can use the page at `http://<this PC>:8417/`.
`./sign.sh pagetest` drives the page in jsdom against a mock of the API.

## The window (PageUp)

| Part | Does |
|---|---|
| `/signlink` | the chat command (and `signlink` in the F5 console): opens the window without PageUp; `version`, `close`, `help` |
| target bar | the sign everything works on (its small quick view, name, distance, signs, pose): *Pick in view* hides the window until you click a sign's board (Esc cancels; a lying sign by clicking where it lies; the sign under the cursor wears the hover frame, its board cool white, before the click), or **Signs nearby**: *every sign within 30 m*, or *SignLink within 100 m* (SignLink's stacks first, each with what it holds, its size and pose, then where it is from your view; farther ones with distance and direction). Why an action is off (a ward you have no access to) shows under the bar |
| 1 Sign | what the sign holds: its quick view beside the facts (its name when SignLink put it on, made with, how many signs, its fit in metres, its size in the world save, multiplayer, a saved animation); **Actions**: Clear, Copy, Paste, Delete for the whole stack (the ones that cannot be undone ask twice); the footer leads to *2 Content*, and stops a tool's animation there when one plays |
| 2 Content | what goes on the sign, one source at a time (the source strip stays in view while the body scrolls), each list with thumbnails: **Picture** (PNG, JPG or GIF from `BepInEx/config/SignLink/images`; the exact pixel grid; **Resolution** in pixels wide, what it costs (*Max*: the most that fits, *Native*: every pixel the file has); the cost from the exact texts: signs, world-save size, seconds to load; **seamless pixels**; PNG transparency and moving GIFs are set once on Settings › Pictures and › Playback), **Art** (the pieces that come with SignLink: glyph art with renders, pixel art and animations), **Text** (one sign's text of any length, tags as the game reads them; **templates** write a coloured Block, a Label, a Symbol from a palette or a Pattern into it in a click, in metres, with a colour and an opacity); the live preview shows the pick on the selected sign, for you only; the **Size** rows (Width and Height in metres, or a percentage) are on 3 Place. **Light** on every source (*Glowing* or *Lit by the scene*; a piece also *As made*; on Text it rewrites the text's own tags). The footer leads on to 3 Place, where the button is: nothing goes on the sign from here |
| 3 Place | where the sign stands and how the chosen content (or what the sign holds) sits, in sections that open and close: the content's two, a rule, the sign's two. **Content positioning**: **Right, Up** along the sign's face as you read it (whatever the tilt; the sign never moves), *Reset to default* (the middle). **Content size**: the picked item's size rows, or **Width / Height** or **Size** for content no source knows. **Sign rotations**: *Standing*, *Face up*, *Face down*, **Tilt** (about its middle; an empty sign takes a pose too), **Turn** about the up axis (any angle, in place), **Rotate** on its own face around its middle. **Sign positioning fine-tune**: **Move the sign** (steps of 1 mm-1 m on an arrow pad laid out as the arrow keys, ▲ over ◄ ▼ ►, *Away* over *Towards you* beside it; in place, as far as the game holds the sign up there, up to 5 m from where it was built). **Changes** under both: *Apply sign coord changes* (writes the steps and turns and makes the signs again where they stand: every player sees it at once) / *Undo* for steps and turns, *Reset to default* (where and as it was built, at once, for everyone; *Undo* brings it back until the window closes); *Undo changes*; the frame in the world shows it (Settings › Sign options › *Highlight signs*), and the live preview shows the result on the sign, for you only. The footer's button says what it does: *Place* a picture or text, *Put* a piece, *Update* the one on the sign with other settings, *Apply* a fit to content no source knows; a very large picture asks first; small stacks take a fit change live when the rows stop, big ones on the button |
| Playing | animations saved on signs nearby (Remove), GIFs playing over pictures (Pause / Play: the row stays, the still shows meanwhile; a moving GIF comes back whenever its sign loads near a computer that has the file in its images folder), tool-started animations when there are any; playback switches |
| Settings | **window width, height, text size and opacity** apart (every edge and corner of the window resizes it too) (the backgrounds take the alpha, text stays opaque; the config is written when the slider is let go), **show where the content goes** (Sign options › Highlight signs and Highlight sign contents, on by default), **Short line when you look at a picture sign** (Sign options: the hover text stays one line, E opens SignLink; a tip to hide signs from players without SignLink), **pictures** (PNG transparency, Fine Width steps: how every picture is made from now on; every sign stays under 400 KB), **playback** (animations and moving GIFs, how far they play), the **developer API** (off by default; on/off, network access and the port take effect at once; the password: show, copy, new), *Reset to defaults* (two clicks), about |

The window is as solid as its opacity (0.8 by default), up to the whole screen wide (width 100 %), draws its text at its
own size for the text scale (`Menu / Scale`), keeps where it was dragged, and blocks game input while open, mouse wheel
included; *Pick in view* hides it until a click.
**Shift + mouse wheel** over a slider's row turns it a fine step a notch (`FineStep`: Tilt and Turn 1°, Right and Up
5 cm, Width about a pixel, Resolution 1 px, percentages 1 %); the plain wheel scrolls the window.

## Build, test and install

```sh
./sign.sh mod                                  # .NET 8 SDK container -> mod/out/SignLink.dll
./sign.sh modtest images/a.jpg images/b.gif    # C# picture code against the Python pipeline
./sign.sh pagetest                             # the import page against a mock API (jsdom)
```

Install the DLL into the Thunderstore Mod Manager profile's `BepInEx/plugins` (`./sign.sh plugin`).

**Reload without restarting the game** (0.20.135; development only: the published package is the plugin alone, in
`BepInEx/plugins`, docs/release-checklist.md): with ScriptEngine (BepInEx.Debug r11.1, `BepInEx/plugins/ScriptEngine.dll`;
its config `com.bepis.bepinex.scriptengine.cfg`: `LoadOnStart = true`, `EnableFileSystemWatcher = true`) the plugin runs
from `BepInEx/scripts`, and `./sign.sh dev` builds it there with its `SignLink.pdb` (ScriptEngine reads the symbols and refuses a DLL without them, and a failed first load also stops its file watcher until the next start): the running game unloads the old copy and loads the new one
about three seconds later (F6 does it by hand). Only one copy may exist, so `dev` removes `plugins/SignLink*.dll` (the
game holds that file while it runs: close it once). Each reload keeps the old code in memory (Mono cannot unload an
assembly): restart now and then. The window closes on a reload; what SignLink changed in the game is undone first
(docs/mod-standards.md "Reloading in a running game").
- **The running game locks the DLL.** Copy the new build next to the old one as `SignLink.<version>.dll`:
  BepInEx loads only the newest version of a GUID ("Skipping ... because a newer version exists"). Delete
  the old file after the next start.
- **Restarting the game** is needed to load a new version. Logging out and back in is not enough.

## Security

- **Password.** Every request except `GET /` (the page), `GET /ping` and `GET /favicon.ico` sends
  `X-SignLink-Password`. The password is generated the first time the API is switched on. The window can show, copy
  or renew it, or you can set your own in the config: at least 12 characters, plain ASCII without spaces, or the API
  does not start. Copy leaves it on the clipboard for 30 s (taken off again if it is still there); 1 Sign › Paste
  refuses a clipboard that holds it, so it never lands on a sign every player reads.
- **Wrong passwords.** Five wrong ones in a minute lock that address out for a minute. A request without the header
  is refused but not counted, and so is a wrong one from this computer (every local tool arrives as 127.0.0.1, and a
  program here can read the config anyway): nothing local can lock the owner out. The import page forgets a remembered
  password the API refuses.
- **Requests.** The first byte within 2 s, the head within 10 s, the body within 30 s (or 2 MB a second when larger).
  Only the routes that read a body take one: `POST /sign`, `/anim`, `/signs/clone` 16 MB, `/image` 64 MB, `/screen`
  256 MB, each with a Content-Length (411 without, chunked ones too); a body anywhere else gets 413. Numbers must be
  finite; `/anim` takes at most 20000 frames.
- **Where requests come from.**
  - Only this computer by default; `AllowNetwork` listens on every interface but answers only this computer and
    local networks (private, link-local, CGNAT/Tailscale and unique local addresses); anyone else is closed
    unanswered. HTTP is not encrypted, so use it only on a trusted home network and never open the port to the internet.
  - No CORS: other web sites cannot send the password header, and the import page, served by the API itself,
    needs none. The page may not be framed.
  - In local mode the Host header must be localhost, 127.0.0.1, [::1] or host.docker.internal, which
    stops DNS rebinding.
- **What the API can touch.** Only signs: write, animate, screens, pictures, clone, delete. Within that, a leaked
  password lets its holder rewrite or remove any loaded sign no ward protects (other players' too; written under your
  platform id, removed without a drop), read the signs' texts and see where you stand: keep it private.
  - Creating and deleting signs (copies of a picture included) works in single player and in multiplayer (0.20.44;
    before, a server needed `Building/AllowOnServers`); the multiplayer limits hold for every write.
  - A ward you cannot access protects its signs, as it does against the in-game editor.
  - Copies carry the author like the vanilla editor writes it, so platform content filters apply to them.
    They have no creator, like world-placed pieces: monsters do not target them, and the hammer still
    removes them (they drop nothing where the copy's owner runs SignLink: `signlink_copy`, CopyDropPatch; see Known
    limits).
  - Moving a sign (3 Place, on *Apply*) writes its ZDO position (owned first); signs have no ZSyncTransform, so other SignLink
    games follow the ZDO (SignMove.Follow) and vanilla games place it anew when the area loads again.
  - Nothing on the client protects a server: a server that wants to forbid copies would need a server-side mod.
- **Generic world editing is not here.** SignLink touches signs only. Developer tools (any object, ZDO fields, driving
  the window in tests) live in a separate private tool and never ship with SignLink.

## API

The public reference with examples is `docs/api.md`, and `docs/openapi.yaml` its OpenAPI 3.1 spec: keep both in step with `Api/Routes.cs`.

| Request | Does |
|---|---|
| `GET /ping` | version; with the password also world loaded? and player position |
| `GET /signs?radius=50`, `GET /hovered`, `GET /sign?id=ID` | list, the sign under the crosshair, read |
| `GET /stack?id=ID` | the sign and the copies stacked on it: ids, characters, bytes |
| `POST /sign?id=ID&mode=local\|zdo` | body = text, any length (`zdo` = saved and synced like the editor); this one sign only: a stack's copies keep theirs (whole stacks: `/image`, `/signs/clone`) |
| `POST /image?id=ID&width=6&pitch=0.5&place=above&x=0&up=&out=&tilt=&turn=&cover=2&maxkb=400&glow=1&alpha=1&animate=1&dry=0` | body = a PNG, JPG or GIF: the picture as saved sign text on the sign and stacked copies (`glow=0`: lit by the scene; `alpha=0`: PNG transparency dropped). width in metres (or the older `pixels=` budget), pitch = pixel size in wu (a multiple of 0.125 up to 40 whose half TMP reads exactly: 0.125, 0.25, 0.5, 0.625, 0.75, 1, 1.25, 1.5, 2 ...), or `detail=` = pixels across with `width=` (the window's model: the pixel follows from both), x, up and out = its centre from the sign's centre in metres (right, up, towards you as the sign was built, whatever the tilt; a tipped sign takes only the part along its face: lying, `x` and `out` move the picture and `up` is dropped; `up` overrides `place`; the older `above=` sets the bottom edge; a tipped sign's picture is centred unless asked); `tilt=` (-90..90°, + face up; or `pose=standing\|floor\|ceiling`) and `turn=` (degrees, clockwise) give the sign a pose (left out: as it is; a new pose makes its signs again, `ids` lists them); `maxkb` 16..400 KB a sign (400 by default; in a shared world every write must also fit the multiplayer limits, 300 KB a sign and 600 KB within 100 m), a moving GIF also plays over it (`animate=1`), `dry=1` only encodes |
| `POST /anim?ids=A,B&fps=&mode=&loop=` | frames separated by U+001E, inside a frame one text per sign separated by U+001D |
| `POST /anim?...&store=1`, `POST /anim/unstore?id=` | save the frames on the signs instead of playing them from here: every SignLink client plays them on the world clock, after restarts too; the text holds the first frame for vanilla players. Text and frames stay under 400 KB per sign |
| `POST /anim/stop[?id=]`, `GET /anim` | stop; stats (shown, real fps, skipped, writes, KB/s, ms; number of stored animations playing) |
| `POST /screen?id=&w=&h=&fps=&cx=&cy=&pitch=&loop=&hide=&filter=&lit=` | body = frames of W x H RGB bytes, rows top-down. `(cx, cy)` is the picture's centre relative to the sign in wu, `pitch` is wu per pixel. A RawImage on the sign's canvas shows it; `hide` hides the text of the whole stack; `lit=1` shades it by the scene like the sign's text (default: glowing) |
| `POST /screen/stop[?id=]`, `GET /screens` | stop; stats |
| `POST /signs/clone?id=&step=0.0005` | body = texts separated by U+001D: new signs at the exact rotation of ID, each `step` m past the stack's front along its normal (0: the window's spacing; else 0.0003..0.002; the copies must fit one stack) |
| `POST /signs/delete?ids=A,B` | removes signs (ownership claim, ZDO destroyed) |
| `POST /pose?id=ID&tilt=90&turn=0` | the stack tipped over its bottom edge (`tilt` -90..90°, + face up; or `pose=standing\|floor\|ceiling`), turned clockwise (left out: as it is), its content turned around its own middle when SignLink knows it (as the fine-tuner does); its signs are made again, `ids` lists the new ones |

**Text frames and screens.**
- Text frames cost the TMP rebuild (about 13.5 µs per glyph) and are seen by everyone. Vanilla clients see
  at most one frame every 2 s.
- Screens cost one texture upload (well under a millisecond) but only SignLink players see them, and they
  are not saved yet. Over a stacked picture (`vanilla_hq.py --screen`), vanilla players see the still and
  SignLink players see the moving picture in the same place.

## live.py (in Docker; reaches the game at host.docker.internal; password from the mounted config)

```sh
./sign.sh live ping | signs | read ID | write ID --file f.txt --mode zdo | stop | stats | clear
./sign.sh live image ID images/a.jpg [--pixels 200000 --pitch 1 --above-m 1 --dry]   # SignLink 0.4+
./sign.sh live stack ID
./sign.sh live anim ID --frames concepts.anim2:cat --fps 4 --mode zdo
./sign.sh live anim ID --frames concepts.anim2:cat --fps 4 --store     # saved on the sign (0.4+); unstore ID removes it
./sign.sh live anim-block 4 --frames concepts.anim1:fjord_bands
./sign.sh live screen 2:A2-1 images/clip.gif          # texture screen; ids may be BLOCK:CELL-LAYER
./sign.sh live bench ID                                # apply time per frame size
```

Related scripts in `signart/`:
- `image_import.py`: one picture to one sign text, at most 50 000 pixels and 400 KB;
- `vanilla_hq.py`: one picture over several stacked signs (clone), `--screen` plays a GIF over it;
- `gif_import.py`: a GIF as text frames on six tiles;
- `encoder_vectors.py`: test vectors for the C# encoder.

## Known limits

- *Pick in view* selects a sign by its board: a picture moved off its sign is picked where the sign stands.
- Each copy of a picture can be removed with the hammer; it drops nothing where the copy's owner runs SignLink (the drop
  is decided by the game of whoever owns the copy's data, so a copy owned by a vanilla player's game still drops a
  piece of wood).
- Two SignLink players changing one stack at the same moment can leave two stacks on the spot (remove one with Delete).
- A big picture under the open sky may be resent to everyone near on weather wear (unverified): put big pictures
  under a roof.
- Two screens on one stack share the hidden text: stopping one shows the text under the other. Copies added
  after a screen started are not hidden.
- Stored animations are text frames only. A GIF over a picture is not saved: its frames (14 MB for the test GIF)
  are far bigger than a sign can carry. Since 0.20.13 it is decoded again from the images folder whenever its sign
  loads near a computer that has the file (`ScreenRestore`), so it moves for everyone who has the file.
- Saved animations' frames are shown as written: where the platform asks the game to filter words, the filter
  does not reach them. A stack's name in *SignLink within 100 m* (not loaded) shows even when its author is muted:
  there is no sign loaded to ask the game.
- Any player's game can mark a sign as SignLink's: such a sign follows its data at once (`SignMove.Follow`) and counts
  against the area bar, which is SignLink's own, client-side check.
- Only SignLink players get the picture protection (short hover text, E opens SignLink): in a vanilla client E opens
  the game's editor, and Enter there cuts the sign's text to 50 characters (Escape leaves it alone).
- A small picture placed far below its sign (further down than about half its pixels times their size) uses a voffset
  regime (first and last lines negative) derived from the verified model but not yet seen in game. Pictures above,
  across and just below their sign, lying or not, use the regime verified in gallery 9 (first line negative,
  last not).
- A tilted sign tips over its bottom edge (the board's lowest point as built), which stays put: a sign standing on a
  floor and tipped face up puts its picture 8 mm over that floor, its board under it, the picture's middle 0.28 m behind
  where its text stood; face down, 8 mm under that line, 0.27 m in front (the picture slides along the sign's face;
  the sign itself never moves for a place); a sign on a wall tipped face up lies half in the wall. On uneven
  ground the ground covers parts of a lying picture. A lying sign's board is out of the hammer's reach: Delete (Sign
  tab) removes it. On a wood floor the boards sit 4-11 mm over the planks, hidden under the picture unless it is
  smaller than a board (1 x 0.5 m).
