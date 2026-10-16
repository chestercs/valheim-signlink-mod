# SignLink before 1.0 (0.3 to 0.20.194)

The pre-release versions, newest first, as they were written while SignLink was made. The changelog from 1.0.0 on is [`mod/CHANGELOG.md`](../mod/CHANGELOG.md).

## 0.20.194 (2026-10-16)

- The Max note stays only with the Resolution it was said for (Undo changes left it under a Resolution it was wrong
  for).
- Internal: the code's comments and the docs are in English throughout; the README shows today's window and links the
  docs.

## 0.20.193 (2026-10-16)

- Max (multiplayer) says so when the picture already has the most that fits.
- Signs nearby measures each sign from your character, as the bar above does (it measured from the camera).
- With no picture picked, the Resolution row is grey.
- Copies added to a stack tilted half a degree sit as its first copies do.

## 0.20.192 (2026-10-16)

- An empty sign starts clean: no text typed or piece picked for another sign waits on it, and nothing lights Undo.
  A sign whose content was cleared (and applied) is an empty sign like any other.

## 0.20.191 (2026-10-16)

- Tilt and Rotate take half degrees, the steps a world save keeps a sign's rotation in: a typed 12.3 is 12.5, so
  the content sits where the preview showed it (a piece 4 m from the middle was 1.4 cm off). An angle a sign had
  already stays as it is until you move the row.
- After the very-large question from Apply content changes, the window stays open, as for that button's other puts.
- A picture's quick view caption follows a new Width that keeps the same pixels.
- Looking at glyph art says "Glyph art" (it said "A long text").
- Signs nearby gives each sign's world-save size as 1 Sign does (a moving piece's frames counted), and the selected
  row's place reads dark on its amber.

## 0.20.190 (2026-10-16)

- Internal: one rule for what counts as a change of the content (3 Place's Changes block and the bottom button's
  words), with tests. Nothing changes for the player.

## 0.20.189 (2026-10-16)

Fixes from a review of today's changes:

- Apply content changes no longer puts a waiting tilt or turn of the sign on too: with one waiting it is grey and
  says the button below puts both on.
- The content Undo is lit only for what waits there (not for a new pick) and takes a Resolution back too.
- A number half typed when the window closed no longer goes on when it opens again.
- The Resolution row no longer stalls a drag at a small Width (it searched every Resolution for a bigger size).
- Native is lit by the Resolution you set; Max stops where the size takes no more pixels.
- The Summary blames the multiplayer limit only when the limit held the pixels.
- A pasted or newly saved moving piece plays even where the sign's old animation was paused, and a new pick's
  preview moves; Paste says "moving piece" only when it put one on.

## 0.20.188 (2026-10-16)

- A saved animation stops for being slow only after two slow frames in a row: one hitch of the game (a 242 ms frame
  in the log) stopped a light piece until the world was loaded again.

## 0.20.187 (2026-10-16)

- The window starts from what the sign holds each time it opens: opened again on the same sign it kept the last
  choice, rows the sign no longer held, and an Undo or Update lit at once.
- 3 Place's content Undo drops what is not applied, back to what the sign holds now (it went back to what the sign
  held when selected, which after an Apply was another pose's place, and lit Update).
- The content's Changes line no longer reads a new Resolution as a new size, nor an older glyph art's layer update
  as a change waiting.

## 0.20.186 (2026-10-16)

- The very-large question starts with the way out: lower the Resolution on 2 Content.
- 3 Place's Undo changes drops everything not applied, the sign's steps and turns too: back to what the sign holds
  and how it stands now.

## 0.20.185 (2026-10-16)

- The Resolution row shows the pixels the picture takes at its size: a small Width takes fewer, and the row follows.
  The Resolution you set comes back with a size that takes it; Summary names it.

## 0.20.184 (2026-10-16)

- With Fine Width, a picture's Width and Height start at 0.25 m whatever the Resolution (the low end moved with it).

## 0.20.183 (2026-10-16)

- A picture of many small pixels takes every Width now: where its Resolution cannot make the size (640 across took
  2.2 m, then 3.6 m), it takes fewer pixels for it, never more, so its data only shrinks. Your Resolution stays as
  you set it; Summary shows the pixels the size takes.

## 0.20.182 (2026-10-16)

- A moving piece paused on the Playing tab stops at once, also while the window is open on its sign (the sign's
  preview played it on).
- Resolution and Width (and Height): while dragged, the thumb stays under the mouse and the number shows the size it
  takes; let go, the thumb goes to that size. It no longer sticks between far-apart sizes (712, 640, 581 px).

## 0.20.181 (2026-10-16)

- Settings: "Prevent the game's editor from breaking pictures" is now "Short line when you look at a picture sign",
  with words that say what it does, and a tip: hide the sign in a wall or the ground with only its picture out, so
  players without SignLink have no sign to look at.

## 0.20.180 (2026-10-16)

- 3 Place has two groups, Content (Positioning, Size) and Sign (Rotations, Positioning fine-tune), each part under a
  title of its own.
- The button at the bottom says what it changes: Update content, Update sign, or Update sign and content.

## 0.20.179 (2026-10-16)

- 3 Place: the content's size and place no longer go on by themselves. A Changes block under Content size, as the
  sign's: Apply content changes, Undo (back to what the sign held when you selected it) and Reset to default (the
  content in the sign's middle). Until Apply, only you see the change.

## 0.20.178 (2026-10-16)

- Copy takes a moving piece's saved frames along, and Paste puts it back moving (it pasted as a still, its first
  frame). A moving GIF copies as before: it plays from the GIF file.
- The sliders move with the mouse again (0.20.176-0.20.177 took no press or drag on them, only Shift + wheel).

## 0.20.177 (2026-10-16)

- The selected sign's name stands under Pick in view, Clear selection and Signs nearby, so a long name wraps on a row
  of its own.
- 3 Place's button names no picture or piece: "Put the picture on the sign", "Put the text art on the sign" (Update
  alike).

## 0.20.176 (2026-10-16)

- The Resolution, Width and Height sliders really follow a slow drag now, either way (0.20.175 read the drag too late
  and still flickered).
- On the Playing tab, Play and Play all show in the yellow of the buttons that lead on, so a paused GIF or animation
  plainly waits for a press.

## 0.20.175 (2026-10-16)

- A picture's Resolution, Width and Height sliders no longer flicker left and right in a slow drag: the thumb follows
  the mouse's way and goes to the next size it takes once the mouse is past halfway to it.

## 0.20.174 (2026-10-16)

- The Sign tab's quick view shows only what the sign holds: the picture, or the sign's words, with no lines about it.
- The green and red veil over the selected content is half as opaque as before.

## 0.20.173 (2026-10-16)

- Clear selection beside Pick in view drops the selected sign (steps not applied go, as when another sign is picked).
- While no sign is selected, the sign under the mouse wears the hover frame, as while picking; a click selects it.

## 0.20.172 (2026-10-16)

- With the window open, a click on a sign selects it; Pick in view is no longer needed for that (a click off any
  sign does nothing).
- While no sign is selected, or Pick in view waits for a click, every sign within 30 m wears a faint grey board
  outline, so you see what a click can select.

## 0.20.171 (2026-10-16)

- Art list: the arrows reach the groups' headings too. Up and down go through headings and pieces alike; on a
  heading, right opens the group and left shuts it; left on a piece shuts its group and goes to its heading. The
  heading the arrows are on is lit.
- The quick views fold like 3 Place's groups: Art and Picture (Quick view) and 1 Sign (What the sign holds), open at
  first and kept as you leave them.

## 0.20.170 (2026-10-16)

- Art list: the left arrow shuts the picked piece's group, the right arrow opens it; up and down go on from the
  group's heading when it is shut.

## 0.20.169 (2026-10-16)

- The up and down arrows step through the Art and Picture lists on 2 Content (held, they repeat); the list scrolls
  to the pick. Left and right, and every arrow on 3 Place, still move the sign.
- A glyph art piece put on several signs before 0.20.145 offers Update: put on again, its layers draw in order (the
  Dragon crest's rune hid behind its red disc: the game drew the rear sign's disc over the front sign's rune).

## 0.20.168 (2026-10-16)

- Classics hold the classic glyph art and the dragon ship: the cat moved to The cat's family, the fjord at dusk to
  Scenes. Their old still and slow variants went (Moving off gives the still); the fjord's moving sky went too, at
  565 KB it fitted no server, so the fjord is a still.
- Every art piece is at most 80 KB now, so each fits a server's limits; the art tools refuse a bigger one.
- Art shows what a piece costs a server, as a picture does: how long a player who arrives waits, whether it fits the
  multiplayer limits, and what it adds to the world save.

## 0.20.167 (2026-10-16)

- With the window open, Crouch and E work too. E uses what the crosshair is on (a door, an item, a bed); on a sign it
  selects that sign in the window. A chest or a workbench still opens only once the window is closed.
- The portal swirl, the windmill, the troll, the longship and the lox move again: they needed a little more time a
  second than a saved animation may take, so the game showed their first frame only. They play a step slower now,
  and the art tools refuse a moving piece that would not play.
- Art: every moving piece you pick starts with Moving on (switching it off holds for that piece only), and the Moving
  switch sits right above the quick view.

## 0.20.166 (2026-10-16)

- With the window open you can walk: WASD, Run, Jump and AutoRun work as in the game, unless you are typing in one
  of SignLink's fields. Attacks, the hotbar and looking round with the mouse stay off while the window is open.
- The mouse wheel zooms the camera when the mouse is not over the window; over the window it scrolls the window.

## 0.20.165 (2026-10-16)

- A moving art piece moves on the sign while you try it (with Moving on), not only in the quick view; so does a
  sign's own moving piece while you move or resize it. It stays on its first frame while glyph animations are paused
  (Playing tab) or when it is too heavy to build at its pace.

## 0.20.164 (2026-10-16)

The art library, after the author's review:
- New groups: Wild animals (a bull moose, rabbits, brown bears, wolves, a giraffe) and Sea and rivers (seals, a seal
  pup, a salmon, a perch, a pike, a herring shoal, a fish on a hook, the sea serpent, a sea turtle): 21 pieces.
- The neck family is drawn as the game's neck, a long-necked shore lizard with a spiny crest (they looked like frogs);
  the neck in Animals of Valheim and with the cat too. Two pieces renamed to match: Neck stars (moving, snapping in
  turn), Sunbathing neck (moving, water lily opening).
- The cats and dogs are cleaner: the cat loaf, the stalking cat and bird, the calico asleep, the old dog, the black
  and Siamese cats, the white cat washing, the shield cat and more.
- The creatures match the game's own looks (their trophies and textures): greydwarf eyes and twigs, the shaman's
  antlers, the surtling's ember body, the draugr's hair, the blob's skull, and more.
- Moder and Yagluth left the glyph art: no boss spoilers for new players.

## 0.20.163 (2026-10-16)

- The Playing tab has Pause all GIFs and Pause all glyph animations, one button for each kind (they were two switches
  under Settings › Playback, which keeps Pause beyond).

## 0.20.162 (2026-10-16)

- A picture's Resolution has two buttons: Max (multiplayer), the most that fits a server, where a new picture starts,
  and Native (every pixel the file has). Light went.
- The button that puts something on a sign says Put for a text, a piece and a picture alike.
- 3 Place: the button beside Apply sign coord changes is Undo steps (Undo changes at the bottom is another).
- 1 Sign: a sign SignLink did not write is made with "not SignLink" (it said Vanilla, also for other mods' text).

## 0.20.161 (2026-10-16)

Wording, from a review of every text a player sees:
- Stale texts fixed: the selected sign's board is lime; glyph animations switched off show their first frame; the
  pause distance counts GIFs too; Fine Width's limit is a server's; the import page names 2 Content › Picture and
  Settings › Developer API.
- One name for each thing: "players without SignLink", "copies" (no "stack"), "SignLink signs within 100 m", "rotated"
  for Rotate, "the sign's middle", KB everywhere.
- Shorter hints on 3 Place and the Picture source; the art size hint keeps to one line, so the rows below no longer jump
  while Width moves; the Playing tab says when glyph animations are off; a GIF that cannot play says why once.

## 0.20.160 (2026-10-16)

- A GIF has a Moving switch on 2 Content, as an art piece has: on, it plays over its still for everyone who has the
  file; off, it goes on as a still. It starts as your last choice. It was a setting under Settings › Playback.
- Settings › Playback has two switches: Play glyph animations and Play moving GIFs, each freezing only its own kind.

## 0.20.159 (2026-10-16)

- Tilt in place (0.20.158) is gone: tilting about the content's middle swings the sign round the picture, which makes no
  sense for a sign (the author's test). Tilt turns the sign about its middle again, the sign staying where it is.
- Kept from it: a tilt typed or dragged past a quarter turn (face up to -90) turns the face round as the Face down button
  does, the content staying where it is (it jumped 10 m the other way); a flip of the face keeps no hidden part of the
  content's place that came back on a later tilt; a sign whose pose changed is read with the content's place on its face
  kept.

## 0.20.157 (2026-10-15)

From a review of the placement maths (the put, the preview, the frames, the API):
- The frame follows the content under Rotate wherever it is: a text, content SignLink did not write, an old picture
  and a piece that cannot move were framed as turning round their own middle, while they turn round the sign's (up
  to 1.4 m apart); a text's frame stayed wrong after Rotate until the text changed.
- An art piece put before 0.20.38 is framed where it goes once its place changes (it is put on again centred).
- A sign's own picture whose box could not be padded previews at its place when it is resized.
- Another player's move, size or pose of the selected sign is taken by the rows instead of being written back by the
  live apply.
- The API's /pose keeps the content's place on the sign's face, as the window does (a tilt sent it off the face).
- A frame measured while another player's write is still reaching the sign is measured again.

## 0.20.156 (2026-10-15)

- While the Tilt slider (or Right, Up or the size) is held, a big content in the live preview stays with its frame;
  it went the other way under a tilt and caught up when the mouse was let go.

## 0.20.155 (2026-10-15)

- The frames measure where a text's glyphs really draw: a text's frame was far larger than its words and panels (a
  label sign 1.10 x 0.73 m for 0.90 x 0.40 m of glyphs), in Show all, under the cursor and on the selected sign alike.
- Show all and the frame under the cursor use the same measure as the selected sign's frame for art pieces and
  pictures SignLink made, so picking a sign no longer moves its frame a little; content without SignLink's recipe (a
  text written elsewhere, a picture from an early version) is framed where its glyphs draw in both, and a selected one
  is framed at all (it showed only the board).

## 0.20.154 (2026-10-15)

- Show all and the frame under the cursor (Pick in view, a row of Signs nearby) frame a built-in art piece where
  it is and at its size, as the selected sign's frame does; they framed glyph art from what the game measured of its
  text, which lay off to one side (the Dragon crest 3.2 x 2.9 m instead of 4.1 x 2.6 m).

## 0.20.153 (2026-10-15)

- While the Rotate slider (or any row) is held, a big piece or a sign's own content away from the sign's middle
  turns where it is in the live preview; it swung round the sign's middle and showed beside where it would go, until
  the mouse was let go.

## 0.20.152 (2026-10-15)

From the pre-release review (bugs, multiplayer, performance):
- A moving GIF that comes back (after a move, a light change, a pose, walking away and back, or on a friend's
  computer) has the size of its still again; since 0.20.149 it could come back a few per cent bigger.
- A GIF that found no room among the playing screens is tried again a minute later instead of staying still for the
  session; leaving the world forgets which files were decoded just now.
- An art piece an update redraws gets a new thumbnail in the list (the old one stayed in the cache).
- Pictures made with 0.20.149 or later keep their Resolution in the sign; SignLink 0.20.148 and older cannot read it
  back, so their moving GIFs stay still there. Everyone should use the same version.
- Small: /signs/clone claims the stack before it writes its count; the lit screens' material goes when the mod
  unloads; the animations' nearest-first order allocates nothing a frame.

## 0.20.151 (2026-10-15)

From the pre-release reviews (security, multiplayer, wording):
- Saved animations share one time budget a frame and a second, nearest first, so many animated signs in one place can
  no longer slow the game down; an animation that stays slow stops sooner.
- A hidden sign (the game's own hiding) shows its hidden words in the window and the playing list, and cannot be
  copied.
- Delete (the Sign tab and the API) leaves signs inside a no-build location alone.
- The developer API: a computer on the network may have three requests open at a time and does not queue behind the
  others; this computer is not limited.
- A sign's cost counts sprite tags as glyphs.
- Settings shows this computer's network address for the API; a few hints are clearer (no signs near, Moving on the
  Art source, putting a moved piece on again).
- The import page says "seamless pixels"; both READMEs say how to uninstall, the art library says Put and Moving.

## 0.20.150 (2026-10-15)

- In single player the Resolution buttons read Light, Multiplayer (what fits a server) and Native; "Max" was lighter
  than Light there. Native says Most when the file has more pixels than a picture may take (a million).
- A typed number is kept at the precision its row shows (12.34 on a row of tenths is 12.3).
- What a sign holds says "rotated" for the Rotate row ("turned" is Turn's).

## 0.20.149 (2026-10-15)

Sliders and number rows:
- Single player has no multiplayer limit on a picture's Resolution or its Fine Width sizes.
- Resolution moves the way you drag (a small drag could jump the other way, to the size the pixels really take).
- A piece's or a text's size goes to the sizes it really takes while you drag or type, so it no longer jumps back after
  the put.
- A picture's Width and Height sliders cover only the sizes its pixels can take: no dead stretch that snaps back.
- A typed number goes on when you press Enter or leave the field, not digit by digit (Turn turned 1°, 13°, 135° while
  "135" was typed; a picture was planned at 8, 81, 812 px); a button that changes the row meanwhile wins over the
  half-typed number.
- A picture keeps the Resolution you chose over an update: it crept down by a few pixels at every size change.
- Between the sizes a picture's pixels can take, the Width slider and a typed width go to the next size that way
  instead of snapping back.
- A size row steps by about 5 % of the content; Enter on a field you did not change changes nothing; the slider drops a half-typed value; Settings are
  written once the rows are still, not at every Shift + wheel notch. The Resolution row's "px wide" fits one line.

## 0.20.148 (2026-10-15)

Switching between Text, Art and Picture, reviewed in the code and tested in game:
- A move, a resize or Undo changes on another source's tab keeps the sign's picture, piece or text editable; it lost
  what made it, so Update, Resolution and a moving GIF stopped working on it.
- After a put, the other tabs no longer offer what they had picked before over the new content.
- Undo changes on another source's tab brings the sign's own content back in its own tab.
- A piece keeps its light over a tab round trip; Undo changes covers the Moving switch; the text editor's Reset empties
  the typed text; a put at a size the pixels round leaves nothing to apply.
- A sign's own picture whose file is not here keeps its Width and Resolution over a tab round trip or a reopen.
- Nothing else goes on while a picture is still being made.
- A picture's Resolution can be raised again with Fine Width on: it stayed at the few pixels over the old one (typed 420
  came out 160). Number fields: a value below the minimum waits for Enter.

## 0.20.147 (2026-10-15)

- The text editor's Reset on a sign holding a piece or a picture brings that piece or picture back (in its own tab),
  instead of loading its first layer as a text: putting that on left one sign of the stack (the raven of Huginn and
  Muninn vanished). The text editor also forgets a text the sign no longer holds, so it is not offered over a piece.
- The Art list names the two Dragon ships glyph art and pixel art.

## 0.20.146 (2026-10-15)

- Moving or relighting a piece of art puts it on again from the library, so a piece put before 0.20.145 gets the
  layer fix as soon as you move it.
- 3 Place: no separator above Changes, and its buttons are a size smaller.

## 0.20.145 (2026-10-15)

- Glyph art made of several layers draws in the right order from every angle: on a tilted sign the Cauldron's
  tripod drew over its pot. Put such a piece on again to get the fixed version.

## 0.20.144 (2026-10-15)

- Animated art has a Moving switch, off by default: the piece goes on as a still picture, its first frame. Switch it on
  and SignLink players see it move. A sign keeps how it was made, so editing it shows the same choice.

## 0.20.143 (2026-10-15)

- The Art list also stays open when you select a sign that already holds a piece, so you can click through the art
  while editing it.

## 0.20.142 (2026-10-15)

- The Art list stays open after a pick, so the next piece is a click away (the Picture list still closes).

## 0.20.141 (2026-10-15)

- The Art tab lists its pieces in groups (Classics, the cat's family, the boar family, kitchen icons and so on), each
  closed until you click it; the picked piece's group opens with it.

## 0.20.140 (2026-10-15)

- Six public-domain paintings come with SignLink: The Starry Night, The Great Wave, The Creation of Adam, Impression,
  Sunrise, Valkyrie and The Wild Hunt of Odin. They are put into the images folder once; one you delete stays deleted.

## 0.20.139 (2026-10-15)

- 105 more pieces in the Art tab, almost all animated: more cats and the cat's friends, a boar family, a neck family
  drawn as the game shows it (lily pads, four eyes), hens, chicks and eggs, forest and farm animals, and 25 kitchen
  icons for labelling chests. The neck was redrawn the same way. See docs/art-library.md.

## 0.20.138 (2026-10-15)

- 69 new pieces in the Art tab: the cat's friends (cats, dogs, a wolf cub), Valheim's animals and creatures, small
  scenes (a campfire, a portal, a longship, a windmill ...), all animated, plus glyph art of the bosses, characters and
  the world. See docs/art-library.md.

## 0.20.137 (2026-10-14)

- A paused GIF played again no longer logs a warning that it showed no new frame.

## 0.20.136 (2026-10-13)

- The green and red veil over a picture's frame is more see-through (25 % instead of 45 %).

## 0.20.135 (2026-10-13)

- SignLink cleans up after itself when it is unloaded: its frames, previews, animations, screens and patches go, and
  the signs show their own texts again.

## 0.20.134 (2026-10-13)

- Fixed: with Fine Width near the multiplayer limit, a picture could keep counting itself again (one pixel more, one
  less, ...) at some sizes, so the button stayed grey whatever you changed. It settles now.
- A button that is grey because the picture is still being read or counted says "Calculating ...".

## 0.20.133 (2026-10-13)

- The picture's Resolution moves only where the density changes: with Fine Width, the slider skips the values that make
  the same pixels at this size, a typed number rounds to one that is made, and Shift + wheel goes to the next one.

## 0.20.132 (2026-10-13)

- An optimisation and security review. SignLink keeps less in memory: signs you walked away from no longer stay in its
  caches, a photo's quick view needs a fraction of the memory, a moving GIF is decoded once (an Update reuses its
  frames; frames parked for a GIF go after 10 minutes unused), and work nobody waits for any more stops.
- Less work every frame: signs refreshed faster than the game rebuild only when their text changed, long picture and
  art lists draw only the rows you see, and the frame, picking and Show all skip work when nothing changed.
- *SignLink within 100 m* reads only the area around you and no longer counts some stacks twice.
- Other players' data is bounded: saved animations are only unpacked near you and within limits, kept sizes and poses
  must be sane, a GIF named on another player's sign is decoded at most once a minute, and a sign whose text the game
  hides for you (a muted or blocked author) shows none of it in SignLink either.
- API: network mode answers local networks only; this computer is never locked out; bodies only where a route reads
  one, with a Content-Length; clone steps 0 or 0.3-2 mm; at most 20000 animation frames; the copied password leaves
  the clipboard after 30 s and Paste refuses it.

## 0.20.131 (2026-10-12)

- Internal tidy-up for the final behaviour; nothing changes in game. A few hints now say what the window really does
  (the live preview, Fine Width's few pixels).

## 0.20.130 (2026-10-12)

- A deep review's fixes. Tilting or laying a sign is refused where nothing would hold it up (the game broke it after
  half a minute), where another sign stands, and where the game allows no building.
- Moving GIFs notice when another player rewrites their sign; screens and stored animations no longer stay in memory
  after you leave a world.
- The live preview measures text frames correctly and no longer rebuilds big texts when it hides and shows.
- Pressing the button while a picture is still being counted waits for Max and asks first if it turns out very large;
  a picture changed on disk since it was counted is read again instead of placed unseen.
- API: animations written to the world are capped at 16 KB/s in multiplayer; `host.docker.internal` is accepted only
  when the hosts file names it; `POST /signs/clone` spaces copies like the window.

## 0.20.129 (2026-10-12)

- Live preview: while you change a sign's content, size, place or pose, the sign shows the result as it will be, for
  you only, before the button puts it on: a new picture, text or piece too, not just its frame.

## 0.20.128 (2026-10-12)

- With *Fine Width steps*, resizing a picture keeps your Resolution: the exact size takes the pixels nearest it (a few
  more or less) and never goes over the multiplayer limit, so a bigger picture can still be saved and a smaller one
  keeps its detail. The pixels move at most 10 %; where a size would need more (small pictures of many pixels), it
  moves in steps instead. The Summary shows the pixels the size took.

## 0.20.127 (2026-10-12)

- The selected sign's board outline is a glowing neon lime, thicker, and no longer hidden under the content's
  green veil.

## 0.20.126 (2026-10-12)

- With *Fine Width steps* on, dragging or typing a picture's Width or Height is fine as well: the size follows the
  mouse instead of jumping, and its Resolution goes a few pixels up or down for it.

## 0.20.125 (2026-10-12)

- Resizing a picture no longer makes the button flicker: while the new size is being counted, the cost shows an estimate
  ("about ... (counting ...)") and the button stays usable. Pressed meanwhile, it waits for the count and checks the
  limits on it.

## 0.20.124 (2026-10-12)

- *Fine Width steps* is on by default.

## 0.20.123 (2026-10-12)

- *Show all SignLink signs* starts off every time you join a world; it is not remembered any more.

## 0.20.122 (2026-10-12)

- Settings › Pictures › *Fine Width steps*: Shift + mouse wheel on a picture's Width and Height moves it a pixel at a
  time (about 0.5-4 cm); its data follows the size. Off (as before): the next size it takes for the same data.

## 0.20.121 (2026-10-12)

- Settings has *Reset to defaults* (click twice): every setting back to how SignLink comes; the API's password stays.

## 0.20.120 (2026-10-12)

- Text size goes down to 0.6x; 2x went (on a 4K screen it looked the same as 1.5x).

## 0.20.119 (2026-10-12)

- Settings › About has an *Open folder* button for the pictures folder.

## 0.20.118 (2026-10-12)

- A new or empty sign opens on *2 Content*; only a sign SignLink made, with content on it, opens on *3 Place*.

## 0.20.117 (2026-10-12)

- 1 Sign no longer has a *Pause the GIF* button; GIFs pause and play on the *Playing* tab.

## 0.20.116 (2026-10-12)

- The frame over a moving GIF is as big as the GIF shows (it was a pixel short on every side).
- *Show all* frames a GIF that was just put on (it showed the board alone).

## 0.20.115 (2026-10-12)

- The selected sign's board outline is a brighter, neon yellow.

## 0.20.114 (2026-10-12)

- The selected sign's board outline is a darker yellow and a little thicker.

## 0.20.113 (2026-10-12)

- The red over hidden parts is an even veil too, like the green.

## 0.20.112 (2026-10-12)

- The frame shows what will be seen as an even green veil (the picture shows through it) instead of stripes; hidden
  parts keep red stripes.

## 0.20.111 (2026-10-12)

- The window no longer scrolls on and on into empty room (3 Place).

## 0.20.110 (2026-10-12)

- **With a sign selected the window opens on 3 Place**, and so do E on a picture sign and *Open in SignLink*.

## 0.20.109 (2026-10-12)

- **Move the sign goes much farther**: as far as the game still holds the sign up, up to 5 m from where it was built (a
  sign standing on a floor goes metres towards or away from a wall). Steps of 50 cm and 1 m.

## 0.20.108 (2026-10-12)

- The title says "0 / 600 KB multiplayer area limit".

## 0.20.107 (2026-10-12)

- **Face up and Face down no longer make the picture jump**: switching between them keeps it where it is; the Up row
  changes sign (face down, Up runs towards you).

## 0.20.106 (2026-10-12)

- **Moving or turning a sign can no longer break it**: a step that would leave it too weakly held is refused, as the
  game counts support (a sign pushed far from what held it fell apart).
- Seen from behind, the frame shows in one plain colour: green and red tell what the front will show.

## 0.20.105 (2026-10-12)

- The frame over a moving GIF should no longer flicker while you edit it: it lies a few millimetres in front of the
  GIF. If it still does, the log now says where everything lies.

## 0.20.104 (2026-10-12)

- The frame over a tilted picture no longer turns half red and flickers: it was drawn a fraction of a degree off the
  picture.

## 0.20.103 (2026-10-12)

- *Signs nearby*'s SignLink list shows SignLink's signs **within 100 m** of you, the reach the multiplayer area limit
  counts (it listed signs anywhere in the world).

## 0.20.102 (2026-10-12)

- The frame no longer flickers green and red over a picture made of several signs: it lay on one of the picture's own
  layers.

## 0.20.101 (2026-10-12)

- **The frame's green and red now match what will show**: they were judged a centimetre in front of the content, so a
  picture sunk just into a floor was shown green although only the floor's gaps would show it.

## 0.20.100 (2026-10-12)

- **Deeper frame colours**: the green and the red no longer look milky and washed out (the game drew them lighter than
  set); a deep emerald where the content shows, a deep crimson where a wall or a floor hides it.

## 0.20.99 (2026-10-12)

- **A new picture starts at Max**: its Resolution climbs to the most that fits by itself. The lighter start is the
  *Light* button (it was *Default*).

## 0.20.98 (2026-10-12)

- **The window keeps its width**: a long button label or the Area line made it wider than set, past the screen's edge,
  and moving the Tilt slider made the window and the frame in the world jump.
- **Shift + mouse wheel on Content size** goes to the next size the picture or piece can really take (a picture's width
  moves in whole pixels, so a centimetre came back to the same width).

## 0.20.97 (2026-10-12)

- Settings: two lines that said nothing are gone; **About** is a tidy table: the steps, every way to open the window
  (the hotkey, `/signlink` in the chat, E on a picture sign, *Open in SignLink* on the game's editor), the fine-tuning
  keys and the pictures folder.

## 0.20.96 (2026-10-12)

- **Reset to default on 3 Place can be undone**: the *Undo* beside *Apply sign coord changes* puts the sign back where
  and as it stood before the reset, for everyone, until you close the window.

## 0.20.95 (2026-10-12)

- **Picking a new picture or piece for a sign that already holds something hides the old one** while you choose (only
  you see it), so only the frame of the new one shows where it goes. Choosing nothing, the same picture again or a text,
  another sign, or closing the window shows the sign as it is.

## 0.20.94 (2026-10-12)

- The content's frame in the world is **green** where it shows and **deep red** where a wall or a floor stands in front
  of it (teal and violet before).

## 0.20.93 (2026-10-12)

- **Shift + mouse wheel over a slider turns it a fine step a notch**: Tilt and Turn 1°, Right and Up 5 cm, Width and
  Height 1 cm, Resolution 1 px, percentages 1 %. The plain wheel scrolls the window as before.

## 0.20.92 (2026-10-12)

- The selected sign no longer gets a second frame when the mouse is over its row in *Signs nearby* or over the sign
  itself in *Pick in view*: its selection frame alone shows.

## 0.20.91 (2026-10-12)

More fixes from the review:

- **3 Place no longer rewrites a sign by itself.** With the Picture list showing and no picture picked, a sign holding a
  plain text or a picture SignLink cannot load was relit and saved half a second after 3 Place opened. Picture's
  *Light* row now waits for a picture.
- **Apply sign coord changes and Reset to default keep your choices on 2 Content**: a typed text, a new picture, its
  size and a pending tilt stayed only until the button before.
- Pasting a plain text over a picture no longer offers to put the picture back.
- The "made from ..., which is not on this computer" line no longer disappears when the sign is turned.
- After putting a text on, the button no longer keeps offering *Update*.
- A text or piece SignLink cannot resize is framed where it really goes.
- The Area line's first number counts what you picked only when the button would put it on.
- Smoother while turning a large text sign or dragging a size slider; the GIF hints point to Settings › Playback.

## 0.20.90 (2026-10-12)

Fixes from a review of the last versions:

- **2 Content keeps your source**: after selecting a sign SignLink cannot load (an older picture, a typed text) and
  then an empty sign, the source strip goes back to the one you were using (it jumped to *Text*).
- **Show all SignLink signs** frames a sign again when its content changes (another player put something new on it)
  and frames the content of a sign that has just loaded (only its board showed).
- The *Highlight sign contents* setting describes today's colours (teal, violet behind walls).

## 0.20.89 (2026-10-12)

- **A picture you used elsewhere no longer lands on a sign by mistake.** Selecting a sign that holds something
  SignLink cannot load (an older picture, a typed text, a file not on this computer), or a sign of another kind,
  shows nothing picked: only what you pick for that sign can replace it. Your own pick still comes along to an
  empty sign.
- *Reload* and *Open folder* show only while the picture list is open.
- The hidden parts of the highlight are a deep violet.

## 0.20.88 (2026-10-12)

- **Show all SignLink signs**: picking a sign (Pick in view, Signs nearby, E, Open in SignLink) switches it off.
- **Calmer highlight colours**: the content in teal, its hidden parts in a muted violet, the selected sign's board in a
  soft grey, the one under the cursor in white.

## 0.20.87 (2026-10-12)

- **1 Sign › Show all SignLink signs**: every SignLink sign around wears its frame (amber stripes, the board's
  outline), with the window open or shut, until you switch it off. Meanwhile no sign is selected or picked.

## 0.20.86 (2026-10-12)

- **The highlight shows what is hidden**: the parts of the content's frame and stripes behind a wall or under a floor
  are rose instead of amber, so you see while fine-tuning when a picture slips under the floor.

## 0.20.85 (2026-10-12)

- 2 Content: smaller buttons in the Text editor, in every *Light* group and for a picture's *Resolution*, as small as
  3 Place's *Standing / Face up / Face down*; a picture's *Light* comes before its *Summary*.
- 3 Place: *Sign positioning* is now *Sign positioning fine-tune*.

## 0.20.84 (2026-10-12)

- **No more jumping while you adjust a sign** (dragging Right on 3 Place, say): the footer keeps its height while you
  work on the same sign, and the page no longer scrolls back when a line under it goes away.

## 0.20.83 (2026-10-12)

- **Face up and face down keep the picture at the same height.** A tilt now turns the sign about the middle of its
  picture, not of its board: the picture sits 7 cm in front of the board, so it went 14 cm up and down between face up
  and face down. Signs posed before keep their place until their pose changes.

## 0.20.82 (2026-10-12)

- **The hotkey reads as usual**: "Shift + Page Up" instead of "PageUp + LeftShift", on Settings in small key frames,
  and the same in the window's footer and in `/signlink version`.

## 0.20.81 (2026-10-12)

- **Opening the window no longer picks the sign you look at** (hotkey, /signlink): use *Pick in view* or *Signs
  nearby*. The sign picked before stays picked; *Open in SignLink* in the game's sign editor, and **E** on a SignLink
  sign, still open the window on that sign.

## 0.20.80 (2026-10-12)

- Back as before 0.20.79: **a selected sign stays selected while the window is closed** (until you go far from it),
  and **opening the window while looking at a sign selects it**. *2 Content* and *3 Place* stay grey while no sign is
  selected.

## 0.20.79 (2026-10-12)

- **The window opens with no sign selected**, on *1 Sign*: it no longer takes the sign in the crosshair or keeps the
  one from last time, so *2 Content* and *3 Place* stay grey until you pick a sign (*Pick in view*, *Signs nearby*),
  or open the window on one (**E** on a SignLink sign, *Open in SignLink*).

## 0.20.78 (2026-10-12)

- **Tilt around is gone**: a sign tilts about its middle; *Move the sign* puts it where it should rest.
- *Standing / Face up / Face down* and *Move by* are small buttons; the move steps are an arrow pad laid out like the
  arrow keys (▲ over ◄ ▼ ►, *Away* and *Towards you* beside it).
- A line under the tabs as well as over them.

## 0.20.77 (2026-10-12)

- 3 Place: *Content positioning* and *Content size* show closed while nothing in them can be used (a text SignLink
  cannot resize, say); *Sign positioning* starts open again.
- *Apply* under Changes is now **Apply sign coord changes**.
- Moving the sign with the arrow keys shows the matching button pressed.

## 0.20.76 (2026-10-12)

- **The content highlight is striped**: amber diagonal stripes inside its amber frame, so the picture shows between
  them (the full amber layer washed it out).
- **Settings › Sign options › Highlight sign contents** (on by default): off frames the sign's board alone.
- Settings: *Moving GIFs* is under *Playback*; the Pictures note now says how to remake a picture already placed.

## 0.20.75 (2026-10-12)

- **The window no longer jumps while you drag a slider or type.** While a control is held, no part of the window
  gets shorter (which moved what is under the mouse or pulled the scroll back); once you let go it settles. The hint
  above the Tilt slider keeps its lines whatever the tilt.

## 0.20.74 (2026-10-12)

- **Narrow windows**: long switch texts wrap instead of running out of the window, the label column narrows, and
  Settings rows put their buttons on a line of their own when there is no room.
- Settings: *Sign options* comes before *Pictures*.
- Quick views show the picture in the middle of their box.
- The *Area* line reads "598 KB / 600 KB within 100 m · already 299 KB": the total with what you picked first.

## 0.20.73 (2026-10-12)

- **Choosing a sign happens on 1 Sign only** (*Pick in view*, *Signs nearby*). Until a sign is selected, *2 Content*
  and *3 Place* are grey and do not open; if the selected sign goes away, the window returns to *1 Sign*.

## 0.20.72 (2026-10-12)

- **Move the sign: Apply now shows the move to everyone at once.** It makes the signs again where they stand, so
  players without SignLink see it too; *Rebuild for everyone* is gone.
- *Back to where it was built* is now **Reset to default**: the sign goes back where and as it was built, at once,
  for everyone.
- 2 Content: the *Area* bar sits under the button, in place of the line about 3 Place.

## 0.20.71 (2026-10-12)

- **3 Place, new order**: *Content positioning*, *Content size* (was *Size*), a line, *Sign rotations*, then *Sign
  positioning*, which starts closed.
- *Original size, middle* is now **Reset to default** and does what it says: it puts the content back in the sign's
  middle (its size is set under *Content size*).
- Text editor: *Load from the sign* is now **Reset**.
- Playing: an empty list is a box that says what would show there, in readable text.
- A little room between the *Area* bar and the button under it.

## 0.20.70 (2026-10-12)

Fixes from a review of the last twenty versions:

- **Tilt slider**: dragging it to or from 0° no longer interrupts the click with an error.
- **Move the sign after a turn or tilt**: steps no longer all fail with "at most 10 cm off its face" once the sign
  was moved and then turned or laid down; a limit now stops only a step that goes further past it.
- **A put under way is not closed by something else**: moving or turning the sign, Changes, or picking another sign
  while a picture is still being made keeps the window open (before, that closed it, undid the move and stopped the
  picture).
- Fewer allocations while the window is open (smoother frame times).

## 0.20.69 (2026-10-12)

- **2 Content: Light is the last group**, under its own *Light* title like *Resolution*, for pictures, art and text.
- **Hide list is grey until something is picked** (Picture and Art): with nothing picked the list is all there is.

## 0.20.68 (2026-10-12)

- **A text's Width and Height are right when you edit it again.** A text put at a small size (a chessboard pattern
  at its smallest, say) read back with the slider at its smallest but the numbers at the largest. The text editor
  now always holds the text as made, the sign shows it at the chosen size while you edit, and the Size rows measure
  it the same way whatever its size on the sign. Changing only the size of an unchanged text still updates it in place.

## 0.20.67 (2026-10-12)

- **The 600 KB area limit counts only SignLink signs**, every player's together; vanilla signs no longer take room
  from pictures. The *Area* bar now says how many SignLink signs are around (a picture of many stacked signs counts
  once) instead of every board.

## 0.20.66 (2026-10-12)

- **3 Place › Sign rotations**: every way the sign turns in one section, apart from *Sign positioning* (the move
  steps): *Standing / Face up / Face down*, *Tilt* and *Tilt around*, then *Turn*, then *Rotate*, which moved here from
  *Content positioning* (it turns the sign itself). *Changes* sits under both and names a tilt or a rotation too.
- **Back to where it was built** also stands a tilted or rotated sign up as it was built (its signs are made again at
  once, the content where it sat on the face).

## 0.20.65 (2026-10-12)

- **1 Sign's actions one a row**: *Clear*, *Copy*, *Paste* and *Delete* under each other, each with what it does
  beside it, instead of one row of buttons and one line of words for all.

## 0.20.64 (2026-10-12)

- **Nothing of Saved is kept.** Signs put from it read as plain content (they can still be moved and sized), and the
  mod no longer makes or reads a `saved` folder: `BepInEx/config/SignLink/saved` can be deleted.

## 0.20.63 (2026-10-12)

- **Saved is gone** (the source on 2 Content and *Save to collection* on 1 Sign). A saved sign kept its texts as they
  were, so its resolution could not change and a large one could not go on in multiplayer. A picture's file (any
  resolution) and *Copy / Paste* (which carries what made the sign) do the same work. Signs put from Saved stay as they
  are and can still be moved and sized.

## 0.20.62 (2026-10-12)

- **Undo and Redo in the Text editor**: buttons beside *Clear the text*, and Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z. A burst
  of typing is one step; a template, the light, *Load from the sign* or *Clear* are steps of their own; another sign
  starts over.
- **The Area bar sits in the footer**, one row over the button of 2 Content and of 3 Place, instead of at the top.

## 0.20.61 (2026-10-12)

- **The window closes when the content goes on.** After the button on 3 Place (Place, Put, Update, Apply) worked, the
  window closes and the result shows in the game's message corner; if it failed, the window stays open and says why.
  Moving the sign and the live changes of a small sign leave it open.

## 0.20.60 (2026-10-12)

- **1 Sign's quick view keeps its place** when there is nothing to show (an empty sign, none selected, still
  reading): a filled box as big as a picture's, its words in the middle, instead of a line in its corner with the
  world showing through the window.

## 0.20.59 (2026-10-12)

- **3 Place holds the size.** Its three sections open and close: **Size** on top (a picture's width and height in
  metres, a piece's or a text's size, or what the sign holds), then *Content positioning*, then *Sign positioning*.
  2 Content keeps what a picture costs: its **Resolution** (Default, Max, Native, Seamless pixels).
- **Undo changes stays in view**, in the footer beside the button that puts it on, and takes back everything the
  menu changed: the size, a picture's resolution, another pick or a typed text, the place and the light.

## 0.20.58 (2026-10-12)

- **The Art and Saved lists drop down** like the Picture list: *Browse pieces* opens it, a pick closes it, and
  editing a sign's own piece it starts closed; the pick stays in one line above the quick view.

## 0.20.57 (2026-10-12)

- **Tilt around is a small choice now**: a small label and small *Middle* / *Bottom edge* chips instead of a row as big
  as the pose buttons; grey while the sign stands (it matters only tilted), with a one-line hint while it is tilted.

## 0.20.56 (2026-10-12)

- **Move the sign, tidied.** Under the steps it says that the arrow keys move the sign too. Below a line, *Changes*
  sums up the moves and turns ("none: it stands where it was built" when there are none), with its four buttons in
  one group: *Apply* (glowing while there is something to apply) and *Undo*, *Back to where it was built* and
  *Rebuild for everyone*. One hint under them says what each does.
- **2 Content's way on glows only when there is something new.** With nothing typed or picked (an empty text box
  over a picture sign) it is a plain *3 Place ▸ move or turn the sign*, and 3 Place says "Nothing new to put on: the
  sign keeps what it holds".

## 0.20.55 (2026-10-12)

- **Move the sign further, safely.** Steps are 1 mm to 10 cm, 5 cm first. A sign moves up to 50 cm along its face
  (a wall's or a floor's plane) and 10 cm off it, and never to where nothing would hold it up: the game holds a sign
  by what lies within 15 cm of its board and breaks one it finds no support for. The old limit, 25 cm each way, let a
  sign leave its wall by more than that.

## 0.20.54 (2026-10-12)

- **Playing in two kinds**: *GIFs* on top, then *Glyph animations* (the ones saved on signs, and a tool's), with a line
  between. The playback switch and *Pause beyond* moved to **Settings › Playback**.

## 0.20.53 (2026-10-12)

- **Typing a text over a picture shows what you will get**: the preview covers the whole stack, the text on its first
  sign and its stacked copies empty, as *Write* or *Update* leaves it (it changed only the first sign before, and the
  rest of the picture stayed). Writing always made the stack one sign; now the preview says so.
- **No Window width and height rows** on Settings: take the window by an edge or a corner. *Default size* stays,
  beside the size the window has.

## 0.20.52 (2026-10-12)

- **1 Sign says it shorter**: no *Download* row; the *Multiplayer* row gives the limit and the load time together,
  "fits the 300 KB limit · loads in about 0.8 s on a server" (the download and the build, as a picture's Summary
  counts them).
- **2 Content opens on Text**: the strip is *Text, Art, Picture, Saved*.
- **A line under the Area meter** on 2 Content keeps it apart from the source below.

## 0.20.51 (2026-10-12)

- **The title fits a narrow window**: *SignLink* on the left with the version smaller beside it, the multiplayer area
  on the right with room between. Where the window is too narrow for "Area … multiplayer limit", it says "Area 302.7
  KB / 600 KB", then "302.7 KB / 600 KB", then that smaller; red when the area is over its limit.

## 0.20.50 (2026-10-12)

- **The window can be as wide as the screen** (Window width up to 100 %; it stopped at half the screen).
- **Take the window by any edge or corner** to resize it: the opposite edge stays, it never goes past the screen nor
  below what its rows need, and an amber line shows the edge under the mouse. The bottom-right corner keeps its dots.
- **A bigger title** with a little room and a thin line under it.
- **Button labels always show whole**: a label wider than its button is drawn with less padding, then a smaller font
  (down to 70 %), and only then wrapped, on every button, button row and tab of the window.

## 0.20.49 (2026-10-12)

- **Edit a picture without its file.** Selecting a sign whose picture file is not on your computer (another
  player's, or one you removed) no longer leaves your own choice active: 2 Content opens its Picture, read back from
  the sign itself, and the quick view shows it. Width and Height are free, Resolution goes down only (*Native* is
  every pixel the sign holds), the light and *Seamless pixels* change as usual; nothing is made again while nothing
  changes. *Update* keeps the file's name, so whoever has the file still edits it from the file at full resolution,
  at the same size, and still sees a moving GIF move. If another player changes the sign meanwhile, Update asks
  once more.
- **Copy and Paste keep what made the sign.** Copy takes a picture's or a piece's recipe, name and place along with
  its texts; Paste puts them on the other sign (in that sign's own pose), so it edits like the original. A plain
  text still copies as itself, and copies made before 0.20.49 still paste.

## 0.20.48 (2026-10-11)

- **The picture list drops down** like *Signs nearby*: *Browse pictures* opens it, a pick closes it. Editing a sign's
  picture it starts closed, so the quick view shows what you edit and the Size rows come first; the pick stays in
  one line under the *Picture* title.
- **Native: N px** (was *Full*): every pixel the file has.
- **Summary** is the title of a picture's numbers.
- **The buttons that lead on glow**: *2 Content ▸*, *3 Place ▸* and the button that puts it on are bright amber with
  a slow glow while they can be pressed.

## 0.20.47 (2026-10-11)

- **Every screen finds its size.** The *Window width* and *Window height* rows run only as far as your screen can
  show at your text size (no stretch of the row that changes nothing), and the width starts automatic: as wide as
  80 % of the screen's height, the same on 16:9, 21:9 and 32:9 screens (a share of the width made ultrawide windows
  far too wide). *Default size* goes back to it.

## 0.20.46 (2026-10-11)

- **Window width and height apart.** Settings › Window has *Window width* and *Window height* in percent of the screen
  instead of the four sizes: 100 % height reaches from the top of the screen to the bottom (Very large stopped at
  92 %); the width still stops at half the screen, so the crosshair stays free.
- **Drag the window's bottom-right corner** to resize it; the rows follow.
- Your old Window size is kept as Width and Height (`1 - Menu / WindowSize` becomes `Width` and `Height`).

## 0.20.45 (2026-10-11)

- **Settings › Sign options** (was *Signs in the world*) holds the sign settings: *Highlight signs* (On / Off; it was
  *Frame in the world* under Window), *Prevent the game's editor from breaking pictures* (it was "Picture signs read
  as one thing") with what it prevents in its description, and the re-read pace.
- The config file follows: `3 - Sign options / Highlight, PreventEditor, RefreshSeconds`; the values of an older
  file (`1 - Menu / ShowGhost`, `3 - Signs in the world / PictureSigns`, `4 - Playback / RefreshSeconds`) move there
  once.

## 0.20.44 (2026-10-11)

- **No building switch.** SignLink makes and removes signs in multiplayer too (the stacked copies of a big picture, a
  sign made again in a new pose), as it does in single player: the multiplayer limits keep the content small and a
  ward still protects its signs. The *Building* section and `2 - Building / AllowOnServers` are gone.
- **Made with** on 1 Sign says *Vanilla* or *SignLink 0.20.44* (the version that made the sign).
- **"Disabled"** where something cannot happen (the frame in the world, playback, the API, a picture's Multiplayer row).

## 0.20.43 (2026-10-11)

- **The mod's id is `PetyusPolisz.SignLink`** (it was `bakterhaz.signlink`), after its Thunderstore team, PetyusPolisz;
  the author is ChesTeRcs (the LICENSE and the DLL's details say so). The config file is
  `BepInEx/config/PetyusPolisz.SignLink.cfg` now: an old `bakterhaz.signlink.cfg` takes the new name at the first
  start, with its settings and the API password.

## 0.20.42 (2026-10-11)

- **The Turn row reaches -180°.** Dragged to the left end it jumped to the right end (-180° and 180° are one turn,
  read back as 180°) and let go of the mouse, and a typed -180 came back as 180; it keeps the end you chose now.
- **Default, Max and Full sit above the Resolution row**, and *Signs* is the first row of a picture's table.
- **The window is 80 % opaque by default** (Settings › Opacity; a config that has a value keeps it).

## 0.20.41 (2026-10-11)

- **Max resolution.** Beside *Default* and *Full*, a picture's Size has *Max: 300 KB*: the most Resolution whose
  signs stay within 300 KB (in multiplayer, within what the area still has room for), found from the counted bytes
  in two or three steps. Moving the Resolution row yourself stops it.
- **Delete and Pause on Playing.** Every row has *Pause / Play* (here only) and *Delete*. Delete takes the movement
  off the sign for everyone and leaves the plain sign with its still picture, as players without SignLink see it: a
  saved animation loses its frames, a moving GIF's recipe says still, so no SignLink game plays it again.
- **Turn above Tilt.** On 3 Place, the sign's *Turn (clockwise)* row sits between the pose buttons and *Tilt*.
- **Saved last.** The source strip is *Art, Text, Picture, Saved*.
- **World save last** in a picture's table.

## 0.20.40 (2026-10-11)

A code review of the whole mod (four reviewers, every finding checked in the code before a fix). Fixed:

- **A placed picture read back as another grid.** The recipe on the sign held the width asked for and an unused
  pixel size, so after Place the window could show 2-4x the data, say "over the limit", and offer Update. It now
  holds the very width and pixel the picture got; a moving GIF's screen is restored at its true size too.
- **The sign's own text got its size and place twice.** Editing a text already on a sign (or pressing Update after
  a light change) applied the whole fit again; now only the change goes on, and the frame shows its true size.
- **A stack whose copy was hammered away was "still loading" forever** and could not be changed again; it is
  treated as loaded 10 s after its newest sign appeared.
- **Selecting the same sign again** (opening the window while looking at it) no longer drops its quick view and
  frame for a moment.
- **Moves not applied** stay yours until Apply: an action that is then refused (a limit, a ward) no longer applies
  them, Delete drops them unsent. The Turn row turns to the number typed, even when typed fast. The rows above
  Apply/Undo are called *The move*, apart from 3 Place's *Undo changes* of the content.
- **The content source follows the selection on every tab** (picking a sign on 1 Sign could overwrite your own
  pick); a picture's glow no longer carries over to a piece; the way on to 3 Place is not greyed for a pose or a
  refit; *Undo changes* and *Original size, middle* are on only when they do something, and the latter keeps the
  tilt pivot; live changes on 3 Place include the light.
- **Frames**: a piece put before 0.20.38 is framed where it is; a piece's frame shows the size it will snap to.
- **The question before a very large piece** greys its button when a limit or a ward stops it meanwhile, and its
  button says *Put it on anyway*.
- **The menu key does not close the window while you type** (a letter bound as the key).
- **Templates**: the strip closes when its lit button is clicked again; the palette and colour rows fit the small
  window; Insert no longer leaves the text box enabled without a sign.
- **Signs nearby**: a row scrolled out of the box gets no frame in the world; the far list groups stacks fast.
- **Building**: a copy that comes to lead its stack drops nothing when hammered (no free wood); plain text laid
  over its bottom edge keeps that pivot; a plain sign moved with SignLink follows for other SignLink players; a lost
  write race restores the turn too; the hover line is for SignLink pictures and stacks only (a short text reads as
  it is).
- **Pictures and the API**: Full resolution stays within the pixel cap; very wide pictures (a 120:1 banner) no
  longer break thumbnails or GIF screens; a GIF screen of a tall GIF no longer overhangs; a file name another
  player's sign holds that Windows cannot take is skipped quietly; the import page works again (no maxkb=0), sends
  the file name (the window can edit what it placed) and shows the true size; `cover` is 0 or 2; `/pose` keeps the
  recipe; `/anim` checks the limits once per stack.
- **Texts**: the picture table names the button on 3 Place; *Default (multiplayer size)*; the Hotkey row's long
  words go under it; Settings › Pictures names the limits; Clear says when copies stay; Playing says why Remove is
  off; 1 Sign's download row says the build comes on top.
- **Cleanup**: SaveCost in its own file, one glyph-time constant, one Size-rows helper, dead code and stale
  comments gone.

Left for later: big photos placed through the API are decoded on the main thread (a hitch); a stack tipped over its
bottom edge and then turned does not count the swing of its base in *Moved*; after a turn the 25 cm box is checked
in the turned frame.

## 0.20.39 (2026-10-11)

- **No Multiplayer-safe switch.** Every sign of a picture stays under 400 KB, always: below the 512 KiB a sign can
  carry to other players (Steam's largest message; a bigger one stalls the connection), so a world made in single
  player can go on a server later. In multiplayer the 300 KB limit made the switch meaningless; the entry leaves
  the config file. The API's `maxkb` is 16..400 (no uncapped mode).

## 0.20.38 (2026-10-11)

- **Glyph art sits in the middle at 0, 0, 0.** A built-in or saved piece put with Right 0, Up 0 and Rotate 0 now
  sits on the middle of the sign, as a picture does; Right and Up count from there. Before, they counted from where
  the piece was drawn, and most pieces were drawn above the sign. The frame in the world shows it so before you put
  it; *Original size, middle* on 3 Place (it was *Size and place as made*) brings a piece back there. A piece put
  before keeps where it is until it is put again.

## 0.20.37 (2026-10-11)

- **Resolution, not Detail.** The picture's first Size row is *Resolution*, in *px wide*, as a picture's size is
  named everywhere else; its buttons are *Default* and *Full: N px wide*. It works as in 0.20.35: it alone sets what
  the picture costs, Width only sizes the pixels.

## 0.20.36 (2026-10-11)

- **The picture's numbers as a table.** Under a picture, one fact a row: *Pixels*, *Size* (with how big a pixel
  is), *Signs*, *World save*, *See-through*, *Arriving* (the wait on a server) and *Multiplayer* (fits, or why Place
  is off), instead of two long sentences.
- **The frame switch has its own row.** Settings › *Frame in the world: On / Off*, with its explanation under it. It
  was a long toggle right under the Opacity slider, and a stray click switched the selected sign's frame off without
  a word; now the window says so under the sign's name when it is off, and the log too.
- **The Area bar has room** above and beside it on 2 Content.

## 0.20.35 (2026-10-11)

- **Detail and Width, apart.** A picture's size on 2 Content is now two things: **Detail**, its pixels across, which
  alone sets what it costs (half the detail is a quarter of the data), and **Width** (and Height) in metres, which
  only makes the pixels bigger or smaller and costs nothing. Before, the *Pixel size* row fixed how big a pixel was,
  so a wider picture had more pixels: a 2 m picture of 105 KB became 1.32 MB at 20 m. Width moves in small steps,
  so every pixel stays one the game adds up exactly (any multiple of 3.125 mm whose half TMP reads exactly, not
  only the seven sizes before). *Default* puts Detail back where it starts (in multiplayer about 22 000 pixels,
  under the 300 KB limit for most pictures; in single player 320 across as before); *All N pixels* takes every
  pixel the picture has. A sign's own picture reads back as exactly its grid. The API keeps `width=` and `pitch=`
  and learns `detail=`.

## 0.20.34 (2026-10-11)

- **Apply and Undo for moving the sign.** On 3 Place, *Move the sign*: steps and turns show for you at once and wait;
  *Apply* makes them everyone's, *Undo* puts the sign back where it was last applied. Closing the window or
  selecting another sign undoes steps not applied. *Back to where it was built* is a step too (Apply keeps it). The
  moves no longer write themselves half a second after the last click.
- **The area limit in the title.** In multiplayer the window's title reads *Area 250 KB / 600 KB multiplayer limit*:
  around the selected sign, else around you. 2 Content keeps its bar with what you picked; 1 Sign lost its copy.
- **The frame after reopening.** Reopening the window on a sign that just got a picture showed no frame for some
  players: the frame is now made anew when the window opens, and the picked content keeps its size on every tab. If
  a frame is still missing, the log says why (lines starting "the frame in the world:").

## 0.20.33 (2026-10-11)

- **Two multiplayer limits: 300 KB on a sign, 600 KB in an area.** In a world others share, one sign and its stacked
  copies hold at most 300 KB, and all the signs within 100 m of it at most 600 KB together (every sign counts, made
  with SignLink or not: the server sends them all to a player who arrives). Over either limit the button stays grey
  and says why; putting something no bigger than what the sign holds is always allowed, so a full area can be made
  lighter. Single player has no limits.
- **The Area bar.** 2 Content and 1 Sign show how full the area is: *250 KB / 600 KB multiplayer limit within 100 m;
  with this: 370 KB*, green, amber near the limit, red over it (multiplayer only).
- **Glyph art first.** The source strip on 2 Content is *Art, Text, Saved, Picture*: glyph art is SignLink's main
  content, pictures a feature the multiplayer limits keep small.

## 0.20.32 (2026-10-11)

- **A hard limit in multiplayer: 600 KB on one sign.** Three large pictures on a dedicated server kept a base from
  showing for minutes and froze a game that came back to them. In a world other players share (on a server, or
  hosting with Start server) SignLink now stores at most 600 KB on one sign and its stacked copies, about 4 s for
  every player who arrives. A picture, piece or text over it greys the button with the reason and how to make it
  lighter; there is no "anyway". Every write keeps to it, the API included, and a sign already over it is not
  re-posed, rebuilt or remade (1 Sign says so); deleting it or putting something smaller on it always works.
  Single player has no limit.

## 0.20.31 (2026-10-11)

- **The wait on a server is counted honestly.** The cost line used to show only the time a game spends building
  the signs (a 664 KB picture: 0.7 s, green). On a server every player who arrives downloads the picture first, at
  150 KB/s at best, and the objects around it wait behind it: three very large pictures kept a base from showing
  for about 2 minutes after logging in. The line now reads *Every player who arrives here on a server waits about
  5.1 s for it: 4.4 s to download, 0.7 s to build*; green up to 3 s, yellow up to 10 s, red above (Place asks
  first). Glyph art and saved pieces say the same when it matters, the question before a very large piece says
  that the buildings next to it may show only after it, and 1 Sign has a *Download* row for the selected sign.
  Place waits for the count (a moment) so the red level always knows the bytes.

## 0.20.30 (2026-10-11)

- **Tilt around the middle, or over the bottom edge.** A *Tilt around* row on 3 Place: *Middle* (the default now)
  turns the sign in place, as a hammer would, so a face-down sign built low no longer goes under the floor; *Bottom
  edge* keeps the edge put, as every tilt did before (a sign on the floor lies on it face up). The choice is kept
  with the pose, so a sign reads back as it was made; older poses keep their edge.
- **The frames show through walls and floors.** The frame in the world and the hover frame are drawn with no depth
  test, after everything else: a sign under a floor or behind a wall still shows where its content and its board
  are. If the game lacks the shader for that, the frame is drawn as before and the log says so.

## 0.20.29 (2026-10-11)

- **Plainer words.** Settings: the key row is *Hotkey*, with its own line; the frame switch says what each colour
  is in one breath; *Picture signs read as one thing*. 3 Place › Move the sign: the rows are *Undo* and *For
  everyone*, the button *Rebuild it here (players without SignLink see the move at once)*. 1 Sign tells you how to
  select a sign without asking you to look at it first.

## 0.20.28 (2026-10-11)

- The *Signs nearby* button is wide enough for its bold text; *Rotate* sits under Right and Up on 3 Place; the
  *Re-read changed signs every* row moved from Playing to Settings › Signs in the world (a setting, not playback).

## 0.20.27 (2026-10-11)

- **Right and Up along the face, nothing else.** Content positioning on 3 Place lost *Towards you*: a sign is a
  flat thing, its content has no depth to move in, and the row only ever slid the content along the slope like
  Up did. Right and Up now count along the sign's face as you read it whatever the tilt (face up, Up runs away
  from you), and tilting the sign keeps the content where it sits on the face. Fits made before keep their
  numbers. Moving the sign itself is Move the sign.
- **Signs nearby behaves.** The list closes when you pick a row (the selected one too) and when you switch tabs,
  and its button stays lit while the list shows.

## 0.20.26 (2026-10-11)

- **The content is always amber; the board says the rest.** The frame around the sign under the cursor keeps the
  content's rectangle amber like the selection's, translucent fill included, and only the sign's board changes
  colour: bone when selected, blue under the cursor.
- **The fill shows whole on any stack.** It is drawn as a plain translucent quad after every sign's canvas instead
  of a UI bar under one of them (which showed over one band of a picture only, so since 0.20.13 the fill had been
  limited to single signs). If a build lacks the shader, the old single-sign fill stays and the log says so.
- **Signs nearby shows the frame too.** Moving the mouse over a row of the list frames that sign in the world (the
  content amber, the board blue) before you click it.

## 0.20.25 (2026-10-11)

- *Open in SignLink* sits above the game's sign editor, not under it.

## 0.20.24 (2026-10-11)

- **Both frames speak one language.** The selection's amber frame and Pick in view's blue frame are built alike:
  the content's rectangle with its top mark in the frame's colour, the sign's board as a small bone outline in
  both. 0.20.21 had taken the board out of the blue frame, which only made it harder to tell which rectangle was
  which. The pick hint and the Settings switch now say what each colour is.

## 0.20.23 (2026-10-11)

- **Air in the target bar.** The small quick view and the sign's name touched; the view now takes a fixed box and a
  gap follows it, so the name starts at one place whatever the picture's shape.

## 0.20.22 (2026-10-11)

- **Open in SignLink from the game's sign editor.** When the game's own text box opens on a sign (E on a plain
  sign), a button under it hides the editor and opens SignLink on that sign, with templates, a preview and no
  50-character cut.
- **The key is yours.** Settings › Window: *Change key* takes the next key you press (with Ctrl, Shift or Alt held
  if you like; Esc keeps the old one), *No key* switches the shortcut off; /signlink in the chat, E on a picture
  sign and the editor's button still open the window. PageUp stays the default.

## 0.20.21 (2026-10-11)

- **Pick in view's blue frame is the content alone.** The board's outline (a small square in the middle of a big
  picture) is gone from the hover frame; the selected sign's amber frame keeps it, where it shows where the sign
  itself stands under moved or lying content.

## 0.20.20 (2026-10-11)

- **Light first.** On 2 Content every source starts with its Light row (Picture, Art, Saved, Text), above the list
  and the text box.
- **The frame in the world works for a text.** A text's size and middle come from the sign showing the preview
  (what it really draws), so the frame follows whatever is typed, templates or tags, and the Text source's Size rows
  show Width and Height in metres like a picture's. Before, a text had no known size and the frame showed only the
  board.

## 0.20.19 (2026-10-11)

- **Text templates, and the sign as the preview.** On 2 Content › Text a strip of four templates writes ready
  pieces into the text in a click: *Block* (a coloured rectangle in metres, to paint a wall or a floor), *Label* (a
  line of text with capitals of a chosen height), *Symbol* (a palette of arrows, shapes, frames, runes, symbols and
  emoji, sized in metres, turned or mirrored) and *Pattern* (a grid of blocks or shades, two colours like a
  chessboard). Each has twelve colour swatches, a hex field and an opacity (translucent layers mix). The pieces land
  centred on the sign; 3 Place moves and resizes the whole text. While the Text source shows, the selected sign shows
  the text as you type, for you only (a third of a second after the last change); the button puts it on for
  everyone. The pieces are placed exactly (the glyphs' measured metrics, an invisible anchor line at both ends that
  pins the vertical centring), as plain sign text that vanilla players see too. The text box takes 50 000
  characters now.

## 0.20.18 (2026-10-11)

- **Greyed where nothing can happen.** One rule through the window: you can look without a sign, you cannot set.
  On 2 Content the lists stay open to browse, but the light, the sizes, a picture's pixel options and the text box
  are greyed until a sign is selected (and while a ward keeps it from you), with a line saying so. On 3 Place every
  row, Move the sign included, is greyed while a ward or building being off makes a change impossible (the bar says
  which). On 1 Sign, Delete needs building to be allowed (removing a sign is building) and the save name field needs
  something to save. On Playing, Remove is greyed on a sign a ward keeps from you.

## 0.20.17 (2026-10-11)

- **The re-read pace is a slider, off by default.** The Playing tab's *Re-read changed signs every 0.1 s* switch
  (on by default) is now *Re-read changed signs every* 0.1-2 s, at 2 s by default: the game's own pace, so nothing
  extra runs. Lower it only for animations a tool writes into signs over the network (the API's `zdo` mode) or to see
  other players' sign edits sooner. Config: `4 - Playback / RefreshSeconds`; the old `FastRefresh` entry leaves the
  file.

## 0.20.16 (2026-10-11)

- **Pick in view shows what the click would select.** While picking, the sign under the cursor (its content and its
  board, a lying one too) gets a blue frame in the world, the same kind the selected sign's amber one is, so you see
  the target before you click; it follows the cursor from sign to sign. The content's rectangle is what the sign
  draws (the texts' bounds), so it needs no reading of the sign.

## 0.20.15 (2026-10-11)

- **Pause instead of Stop.** A GIF on the Playing tab (and the button on 1 Sign) now pauses: its row stays, the
  still picture shows, *Play* moves it again from the frame it stopped at. Before, Stop dropped it from the list and
  a new login was the only way to get it moving again. The pause holds while the sign unloads and comes back.
- **Fixed: the frames-per-second figure on the Playing tab** counted since the screen started, so it crept down
  while playback was off and stayed low after it was switched on again. It now counts over the last two seconds,
  and a paused screen or playback that is off says so instead of a number.

## 0.20.14 (2026-10-11)

- **A moving GIF is decoded once, not at every change.** Its frames stay parked in memory when its stack is
  rewritten, made again or unloaded (within the screens' 256 MB budget, the oldest sets dropped first), and the
  screen comes back from there at once: a pose, place or light change of a moving GIF now moves its texts in place
  like any picture's (a Refit) instead of decoding and encoding the GIF again (0.20.7-0.20.13), and walking away and
  back costs no decode. After a login each GIF is decoded once, in the background; the log says how long it took.
  Width, pixel size, transparency or seamless pixels still make the picture again: they change its pixels.

## 0.20.13 (2026-10-11)

- **Moving GIFs come back.** A GIF placed as moving plays over its still again whenever its sign is loaded near you:
  after logging out and in, after walking away and back, and on every other computer that has the GIF file in its
  `BepInEx/config/SignLink/images` folder (`ScreenRestore`: the nearest such sign without a screen is decoded from the
  file every two seconds, one at a time, and its screen placed exactly as the import did). Before, a GIF moved only
  until you left, and the Playing tab forgot it. *Stop* on the Playing tab now keeps that GIF still until you log in
  again. The frames are still not in the world: players without the file see the still. The Moving GIFs setting and
  the Playing tab say so.
- **The frame in the world no longer paints one band of a picture.** Its translucent fill shows only over a single
  sign; on a stack the copies' canvases draw in an order Unity picks, and the fill under one of them covered one band
  of the picture and not the rest (the author's screenshot). The outline and the top mark stay on every stack.

## 0.20.12 (2026-10-11)

- **Fixed: Turn the sign crossed into the pose rows.** Turning a sign (Move the sign, *Turn*) changed the content's
  *Rotate* on 3 Place, and a pose just chosen there (Standing on a lying sign) snapped back to the sign's old pose
  the moment the sign was turned. The turn moves the signs at once but writes their ZDOs half a second after the
  last step, and until then the stored "as built" pose lagged behind: a flat sign turned about the up axis read as
  content rotated in its plane, and 3 Place took that reading for a new pose. The base read now counts the turn and
  the move not yet written in (`SignPose.Base`, `SignMove.PendingTurn`), so the pose reads the same before and after
  the write; the frame in the world no longer counts a pending move twice. A remade stack's copies no longer carry a
  stale turn mark.

## 0.20.11 (2026-10-11)

- **A text pass over the window**: every hint is one or two short sentences, the terms are the same everywhere (the
  sign, its copies, a piece, a picture, the frame in the world), stale pointers are gone (sizes are on 2 Content, the
  frame switch on Settings), and the groups breathe: a header stands off what is above it, sections and hints off what
  follows, sections a little roomier. Renamed: *What the sign holds* (1 Sign), *Pause beyond* (Playing), *3 Place ▸
  set where it sits, then put it on* (2 Content), the seamless-pixels switch shorter.

## 0.20.10 (2026-10-11)

- **One place for a size**: an art piece's, a saved piece's and a text's Size rows (Width and Height in metres when the
  piece's size is known, else a percentage) are on their source on 2 Content, under the quick view and the light, as a
  picture's Width is. 3 Place keeps Rotate and the place for a picked item, and the size only for content no source
  knows. A piece that cannot be resized says why under its rows.
- **Turn the sign**: a *Turn (clockwise)* row in Move the sign turns the sign and its copies about the up axis through
  the first sign, as the hammer turns a piece when building, as they are (no remaking, any content kept); the total
  since it was built shows, *Back to where it was built* undoes it, *Apply the move for everyone* shows it to
  players without SignLink. Before, a sign's direction could only be changed by rebuilding it.

## 0.20.9 (2026-10-11)

- **Picture options set once**: *Multiplayer-safe*, *PNG transparency* and *Moving GIFs* moved from 2 Content to
  Settings (*Pictures*; config `5 - Pictures`): they are how pictures are made, not a choice per picture. A picture
  made with other settings says so on 2 Content and offers *Update*, which makes it as the settings say now.
- **Seamless pixels** is the new name of *Solid from afar*, in the Size block of 2 Content: each pixel is drawn twice
  its size, so no gaps show between pixels from a distance; it costs nothing and stays on by default.

## 0.20.8 (2026-10-11)

- **3 Place in two blocks**: *Sign positioning* (the sign's pose: Standing, Face up, Face down, Tilt; and **Move the
  sign**, which moved here from 1 Sign with its arrow keys) and *Content positioning* (Rotate, Width / Height or Size,
  Right, Up, Towards you, Undo, As made). 1 Sign keeps what the sign holds and the actions on it.
- **An empty sign takes a pose**: a sign whose content went (deleted, cleared) can be stood up or laid down again
  without putting anything on it; the button says *Apply and stand it up*. Before, the rows were greyed on an empty
  sign and only new content could change its pose.
- **Move the sign** rows renamed for clarity: *Move by*, *Moved*, *Reset: Back to where it was built*, *Finish: Apply
  the move for everyone (makes the sign again)* (was *Remake for everyone*).

## 0.20.7 (2026-10-11)

- **A moving GIF stopped moving after a pose or place change**: a change that only moves the picture (tilt, lying,
  right, up, rotate) moved its texts in place (*Refit*), and the screen that plays the GIF over them was stopped and
  never started again. A moving GIF is now made again for any change (*Update*), so its screen comes back; a GIF
  placed as a still and every other picture still move their texts in place.
- **The source strip of 2 Content** (Picture · Art · Saved · Text) stays in view while the body scrolls, and the body
  starts at the top when the source changes.
- **Screens in the log**: a line when a GIF screen starts or stops (size, frames, the signs it hides, the memory held)
  and a warning when one shows no new frame for 3 s, with the reason (too far for *Playback Distance*, playback off,
  uploads too slow, or nothing due): what the author saw after an edit (a still, a doubled picture) will be in the log.
- The 0.1x `[Viewer] HoverSprites` line leaves the config file too.

## 0.20.6 (2026-10-11)

- **The Opacity slider and field did nothing** (0.20.5): the slider snapped back and a typed value went back to 100 on
  Enter. The row's change was read in the Layout event only, where IMGUI controls report no change (a drag or a key
  reports it in its own event). The change is taken in any event now; the config still takes it once the slider is let
  go.

## 0.20.5 (2026-10-11)

- **Opacity** (Settings, Window; `1 - Menu / Opacity`, 50-100 %, 100 by default): the window's backgrounds take the
  alpha, so the world shows through it while you set things; text, frames and the check marks stay opaque. The slider
  shows at once and the config takes the value once the slider is let go.
- **Show where it goes** moved to Settings (Window), on by default as before: it is a preference, not a step.
- **Back to where it was built** on 1 Sign (Move the sign) has a row of its own: the move the sign keeps, remakes
  included, is undone in one click (the small *Back* button was easy to miss).

## 0.20.4 (2026-10-11)

- **The frame in the world is back**: 0.20.2 hung it under a canvas of its own, sorted over the signs', and it did
  not show in game. It hangs under the canvas of the stack's frontmost copy now (the path 0.20.1 showed it on), last
  among that canvas's children: world-space canvases draw nearest last, so it draws over every band of the stack
  instead of over one.
- **The light sits with the content**: the *Light* row (*Glowing*, *Lit by the scene*; a piece also *As made*) is on
  2 Content, under a picture's options, under a piece's quick view, and on the Text source, where it writes the text
  itself (the glowing font's tag in front of it, or every such tag out: your own tags count). 3 Place keeps the sign's
  pose, the size and the place.
- **2 Content leads on to 3 Place**: its footer no longer places anything; it says what 3 Place's button will do and
  takes you there, so the place, the pose and the frame in the world are seen before something goes on the sign (and
  nothing goes on twice).
- **The emoji switch is gone** (*Keep emoji faces in sign hover texts*, `3 - Signs in the world / HoverSprites`):
  sprite tags always leave a sign's hover text, since the game leaves an animated face stuck by the crosshair after
  such a sign; the old entry leaves the config file.

## 0.20.3 (2026-10-11)

- **A moving GIF lit by the scene glowed anyway**: the light choice re-made the still under it, but the screen that
  plays the GIF over the still drew with Unity's unlit UI material, bright day and night. A screen placed *Lit by the
  scene* is now shaded like the sign's text (sun, ambient, torches; dark at night); *Glowing* is as before. The Playing
  tab says which (`lit by the scene` / `glowing`), and the API's `POST /screen` takes `lit=1`.

## 0.20.2 (2026-10-11)

- **A moving GIF over a stack that grew**: the screen hid the text of the signs that were there before the placement
  only; the copies made for the new bands kept showing the still over it, so only the top bands moved (the sign list
  is made once per frame, and a copy made in the same frame was not in it yet). A sign that just awoke is in the
  list at once now.
- **The frame in the world** (3 Place) draws on a canvas of its own, over every sign of the stack: as a child of the
  first sign's canvas it was drawn under the text of the copies in front of it, so it showed over one band only.
- **Check boxes and sliders at the window's text size**: the skin's 14 px toggles were unreadable at 4K.

## 0.20.1 (2026-10-10)

- **Clicks in the window did nothing** (0.20.0 on Valheim 1.0.17): the first click set a button pressed and nothing
  fired, Esc still closed the window. 0.20.0 took the keyboard focus away from text fields on every mouse-down, and
  that call (GUI.FocusControl inside the window) made the game's GUI drop the mouse-up before it reached the window.
  Focus now leaves a field between clicks, never during one.

## 0.20.0 (2026-10-10)

- **The window in three steps**: *1 Sign* (what the sign holds, with its quick view; move the sign; copy, paste, clear,
  delete; save to your collection), *2 Content* (what goes on it: a picture file, built-in art, a saved piece, or a
  text, with thumbnails in every list), *3 Place* (one panel for the tilt, rotation, size, place and light of
  whatever was chosen or whatever the sign holds). The footer's button always says what it does: *Place* a new
  picture or text, *Put* a piece, *Update* the one on the sign with other settings, *Apply* a new fit to content no
  tab knows. Selecting a sign loads what it holds into 2 and 3: editing is the same as making. "Edit on the ... tab"
  and the separate fine-tuner are gone. *Pick in view* hides the window until you click a sign.
- **A frame in the world** shows where the content ends up while you set it (size, tilt, rotation, place), with the
  sign's board in its new pose; *Show where it goes* on 3 Place switches it off.
- **Thumbnails** in the Picture, Art and Saved lists (a phone photo's own preview when it has one; made one per
  frame and cached under BepInEx/cache/SignLink/thumbs, so a folder of big photos never freezes the game).
- **Text** as content: write one sign's text of any length with tags, or load and change the one it holds.
- **Pictures**: pixel size 2 is the default (size 1 on a 2 m photo was 4-7 s of loading for everyone near it); a
  photo is decoded once and encoded once (the cost line's texts are the ones placed); the window is at most half
  the screen wide, keeps the place it was dragged to, and its text is drawn at its own size (crisp at 1440p and 4K).
- **Settings**: the config file's sections are renamed (1 Menu, 2 Building, 3 Signs in the world, 4 Playback,
  9 Developer API). The values a 0.19 config holds move to their new places at the first start (the API password
  and switch included) and the old entries leave the file; a setting already changed in 0.20 keeps that choice.
- Inside: one shared state for the window, one background-job helper, one recipe reader; SignUtil split into
  SignFinder, SignMarks and SignText; saved pieces capped (32 MB, 64 signs, 5000 frames) and named safely. The
  signs' data keeps its keys and 0.19 signs are edited the same way; what a sign records about its maker changes in
  two words: a saved piece by its file name (0.19 wrote the full path, which every player received), a text as
  "text".

## 0.19.1 (2026-10-10)

- Editing fixes from a review of 0.19.0: a built-in piece is known by its file, not its name (two pieces are called
  Dragon ship); the record of what made a sign holds only while the sign still holds that text (an older SignLink, the
  game's editor or a tool may have changed it), and removing a saved animation drops it; an odd file name on another
  player's sign no longer breaks the Sign tab; your own settings on the Picture, Art and Saved tabs come back when you
  select a sign that holds nothing they made; the Saved tab reads the sign again when reopened; the fine-tuner forgets
  the previous sign's size.

## 0.19.0 (2026-10-10)

- **One way to edit**: the tab that put something on a sign edits it. Select a sign holding a picture SignLink put on,
  and the Picture tab opens with its file, width, pixel size, options, place, pose and light, and its button says
  *Update the picture on the sign*; for built-in art and saved pieces the Art and Saved tabs pick the piece and how
  it sits, and *Update* it. The Sign tab sends you there (*Edit on the ... tab*); its own fine-tuner stays only for
  what no tab knows (typed or pasted text, older work, a picture whose file is gone).
- **Sizes in metres everywhere**: Art and Saved size a piece by its width and height in metres, as the Picture tab
  does (built-in art knows its size; saved pixel pictures are measured), and so does the Sign tab's fine-tuner for a
  pixel picture. A percentage is left only for saved glyph art, whose size SignLink cannot measure.
- **What made it**: the first sign keeps a short record of what put its content there (a picture's file and
  settings, or the piece's name and light: a few dozen bytes beside SignLink's mark), which is what the tabs read.
- **Slow signs in the log**: the game's own work on a sign (its hover text, its text refresh) is timed; anything over
  30 ms is logged with the sign and its size, to find the stall some players see when aiming at a picture.

## 0.18.0 (2026-10-10)

- **Move the sign** (Sign tab): the sign and its copies move in steps of 1 mm to 5 cm, sideways, up and down, towards
  you and away, with buttons or the arrow keys (Shift with up and down: away, towards you), up to 25 cm each way from
  where it was built; *Back* returns it there. Nothing is made again: the signs move in place (your game at once,
  other SignLink players' a moment later, players without SignLink when they come back to the area). For a picture
  that dips into the floor, or sits a little off. *Remake for everyone* makes the signs again where they are, so
  every player sees the move at once (everyone near loads the picture again).
- **The sign never moves for a picture's place.** Right, Up and Towards you move only the picture, along the sign's
  face; the sign only turns (0.16.0 moved the whole sign off its face, into a wall when tilted). Standing, Towards
  you does nothing; lying, Up does nothing; tilted, both slide the picture along the slope (the rows say so). A
  stack that 0.16-0.17 moved off its face goes back to its place the next time SignLink changes it.
- **SignLink's mark**: the first sign of every stack SignLink writes carries the SignLink version (a few bytes in the
  sign's data, nothing shows). *Made with SignLink* and the picture hover lines go by the mark only: signs stacked by
  hand stay the player's own (before, two signs on one spot counted as SignLink's). The Sign tab says which version
  made what it holds.
- **No freeze on quick views**: Art and Saved read a picked piece on a worker (a big saved picture stalled the game for
  seconds), the Sign tab checks a big picture's tags on a worker, and its quick view waits until every sign of the
  stack has loaded (a half-loaded stack showed a picture with a gap).

## 0.17.0 (2026-10-10)

A review of the whole mod before its public release, and the fixes it found.

- **Poses survive a reload.** The world save keeps rotations to half a degree, so after a restart a tilted or
  turned stack read as a little off: the next change made all its signs again for nothing (or was refused on a
  server), and copies could end up half a degree apart from their first sign. Poses are now made on that grid and
  kept on the sign, and copies take their first sign's exact angles.
- **Safer rebuilds.** A new pose makes all the new signs before the old ones go, so a failure leaves the stack as
  it was; nothing is changed while the area around the sign is still loading; a stack another mod moved keeps its
  pose; a sign tilted by another mod is taken as it stands; another player's sign on the same spot is no longer
  counted as a copy; a rebuilt picture names its new author.
- **Copies drop nothing.** Taking down a stack's copy with the hammer gave a piece of wood the copy never cost.
- **Pictures.** Phone photos turn the right way up (EXIF orientation). Pictures over 50 megapixels are refused
  before decoding (the game stalled on them). Broken or hostile GIFs (huge frames, cut-short files) fail with a
  message instead of eating memory, and GIFs with skipped frames play at the right speed. The quick view reads
  pictures whose pixels lie two rows apart correctly. Saving a sign under a name whose side files were left over,
  or under a Windows device name (CON, NUL), works.
- **Window.** Paste no longer wipes a sign when the clipboard holds no text, and refuses a sign too big for other
  players. A number field shows a value changed elsewhere (a button, another sign) instead of what was typed, and
  ignores "NaN". Dragging the width no longer throws a layout error on the Picture tab. The window closes when the
  player dies or leaves, does not open over the trader, the build menu or popups, keeps a pose chosen on a tab when
  an action fails, and greys Clear, Paste and Delete on a sign a ward protects.
- **Saved animations from other players** cannot take memory or time: their size is checked before unpacking, and
  one that keeps stalling the game stops.
- **Developer API** (off by default): no cross-site access (web pages could lock it with wrong passwords), a wrong
  password counts only when one was sent, slow or chunked requests are handled, numbers must be finite, the
  password must be at least 12 plain characters. The import page follows the current picture options (width in
  metres, pose, place, light, transparency, GIF).

## 0.16.0 (2026-10-09)

- **The place stays put when the sign tilts**: *Right*, *Up* and a new *Towards you* are fixed directions, measured
  from where you faced the sign as it was built, whatever the tilt (0.15.0 measured the second one along the sign's
  face, so it meant up, away or towards depending on the tilt, and tilting moved the picture: 5 m towards you became 5
  m away face up). Tilt and Rotate only turn the picture. When the place is off the sign's face (*Towards you* on a
  standing sign, *Up* on a lying one), the sign moves with the picture: *Up* lifts a face-down picture to the ceiling.
  Labels no longer change with the tilt; Tilt and Rotate sit together at the top. Fits saved by 0.13-0.15 are read in
  the new directions (the picture stays where it is). API: `out=` (metres towards you) on `/image`; a tipped sign's
  picture is centred unless `place`, `up` or `above` say otherwise.

## 0.15.0 (2026-10-09)

- **Tilt**: a slider from -90° to 90° instead of the Standing / Floor / Ceiling choice. The sign tips over its bottom
  edge, which stays where it is: whichever way and however far it tips, it ends up at the foot of where it stood
  (0.14.0 put a face-down sign at the top). 90° is face up (a picture on the floor), -90° face down (read from below,
  not mirrored); the *Standing*, *Face up* and *Face down* buttons set the slider. Everywhere the pose is chosen
  (Picture, Art, Saved, and the Sign tab's fine-tuner); API `tilt=-90..90` (`pose=standing|floor|ceiling` still
  works). A sign laid down by an older SignLink moves to its new place the next time SignLink changes it.
- **Half-transparent PNGs**: a picture's alpha now shows on the sign as it is (0.14.0 knew only see-through or
  solid). Half-transparent pixels carry their alpha in their colour tag (`<#rrggbbaa>`, two characters more), and
  with *Solid from afar*, where four overlapping glyphs cover every point, each gets the alpha that adds up to the
  picture's own. Quick views show them as they look. (Byte for byte with the Python pipeline, as before.)
- **No line in the main menu**: SignLink no longer writes its version there; it shows a line only when its window
  failed to load. The version: `/signlink version` in the chat.

## 0.14.0 (2026-10-09)

- **See-through PNGs**: a picture's transparent pixels stay empty on the sign (on by default, Picture tab, Options),
  so a character on a transparent background stands alone. They cost nothing: the cost and load lines count only the
  pixels that are drawn. (The encoder skips rows with nothing to draw, and copes with a pixel alone; the Python
  pipeline does the same, byte for byte.)
- **Ceiling**: besides Standing and Floor, a sign can turn face down against the ceiling above it, readable from
  below and not mirrored (its head, the highest point as built, becomes the ceiling; the board goes into it). On the
  Picture, Art and Saved tabs and in the Sign tab's fine-tuner; API `pose=standing|floor|ceiling`.
- **The size makes sense on screen**: the Picture tab says what goes on the sign ("480 x 270 pixels of 12.5 mm, 6 x
  3.4 m, from your 3840 x 2160 picture, 8 of its pixels to one"), and says it plainly when a small picture gets bigger
  pixels than chosen (SignLink never enlarges a picture) or when the pixel limit shrinks it. *Pixel for pixel* sets
  the width so that every picture pixel is one sign pixel.

## 0.13.2 (2026-10-09)

- **/signlink in the chat** (or `signlink` in the F5 console) opens the window without PageUp; `/signlink version`
  tells which version runs, `/signlink close` closes it (Tab completes them in the console).
- **The main menu shows it**: "SignLink 0.13.2 · in a world: PageUp or /signlink in the chat" in the bottom right
  corner. No line there means the game started without BepInEx (not through the mod manager).
- If the window ever fails to load, the main menu, the command and E on a picture sign say so (with a pointer to
  BepInEx/LogOutput.log) instead of silently doing nothing.

## 0.13.1 (2026-10-09)

- **Fix: 0.13.0 did not open** (neither PageUp nor E on a sign): the fit rows were built with a C# tuple, which needs
  the System.ValueTuple assembly; the game does not have it, so the window failed to load. Plain arrays now, and
  `./sign.sh mod` checks every assembly the plugin references against the game's and BepInEx's (mod/RefCheck).

## 0.13.0 (2026-10-09)

- **Fine-tune on the Sign tab**: what the selected sign holds can be changed in place, like a picture before Place:
  standing or lying, size, right and up, rotate, light (as it is, glowing, lit by the scene). Small pieces change as you
  move the rows; a big picture waits for *Apply* (everyone nearby loads it again). *Undo changes* and *As made* are
  one click away.
- **The same rows everywhere**: Picture, Art and Saved show *On the sign* (standing or lying, size, right, up, rotate,
  light) the same way, starting from the selected sign's pose; the light choice moved there from the footers.
- **Pieces turn around their own middle**: the built-in art knows where it sits (`NAME.fit`, worked out by the engine
  from its texts), saved signs keep theirs, and a sign remembers how SignLink put its content on (size, place, middle),
  so the fine-tuner starts from there. Content SignLink did not put on turns around the sign's middle.
- Clearer texts: the Sign tab's summary (*Holds*, *Signs*, *Fit*), pixel size in millimetres, shorter hints everywhere.

## 0.12.0 (2026-10-09)

- **The sign itself lies down.** *Lying* no longer looks for a floor in front of the sign: the selected sign (with
  its whole stack) lies down where it stands, flat on the floor under it, its picture's top away from you as you face
  it. Where it goes is the player's choice; SignLink does not move it. *Standing* at 0° puts it back exactly as it was
  built. Sign tab, **Pose**; the Picture tab gives the pose with *Place*.
- **Rotate** (-180° to 180°, clockwise): on the Picture tab the picture turns around its own centre and the sign with
  it; on the Sign tab the sign turns with all it holds. Other players see a new pose at once: the stack's signs are
  made again in it with all their data (text, author, builder, health, saved animation).
- **Size and position for art and saved signs**: *Size* (25-400 %), *Right* and *Up* from where the piece sits as
  made. Glyph art and pixel pictures are rewritten exactly (the layout model; checked glyph by glyph against the
  Python engine); pixel pictures take the sizes that keep their rows exact.
- **SignLink's signs listed apart**: *Signs nearby* has *Made with SignLink*, with what SignLink put on each (the
  picture's file name, the piece's name), its size and pose, and the ones farther away (in single player and on the
  host: the whole world) with their distance and direction.
- **Delete** on the Sign tab removes the sign and the copies stacked on it (click twice; nothing drops).
- The Sign tab's quick view recognises art that was resized or moved (by the name SignLink keeps on the sign).
- Shorter, clearer texts on the Picture, Sign, Art and Saved tabs.
- API: `POST /pose?id=&lying=&turn=`; `POST /image` takes `lying=` and `turn=` (left out: the pose stays).

## 0.11.0 (2026-10-09)

- **Pictures on the floor.** The Picture tab's *Position* has *Stands on the sign* / *Lies on the floor*. Lying, the
  picture lies flat on the floor in front of the selected sign, its top towards the sign, so it reads upright as you
  face the sign; *Out from the sign* (to the picture's near edge) and *Right of the sign* place it. Vanilla players
  see it too, and you can walk and run over it.
- A floor picture gets signs of its own, laid face up under the picture's centre with their boards under the
  floor's surface; the sign only marks the spot and may be removed afterwards. The floor picture is selected once
  laid: *Place* lays it again (Out and Right still count from the sign it was laid from), the Sign tab's *Clear*
  removes it whole. Laying again from the same sign replaces it instead of adding a second one.
- Floor pictures cannot be hovered or reached with the hammer: *Signs nearby* lists them ("a floor picture on N
  signs"), and *Pick in view* selects one when you look at its middle.
- API: `POST /image?...&lying=1&out=0`.

## 0.10.0 (2026-10-08)

- **Very large pictures ask first.** When the cost line is red (everyone who comes near waits more than 5 s while
  their game builds the picture, or a sign is over what other players can receive), *Place* opens a short
  explanation instead: how long the game stands still for every visitor, what it adds to the world save and how long
  a server takes to send it, then *Place it anyway* or *Cancel*. Art and saved signs ask the same way.
- **Window size and text size apart** (Settings): the window takes its share of the screen (*Small* to *Very
  large*), the text its own size (0.8x to 2x). A large window with normal text is the default; rows, lists and quick
  views follow the window.
- **Pictures read as one thing in the world**: a picture's signs and the copies of a stack show a short hover text
  ("A picture on 7 stacked signs") instead of the whole picture's text, and **E opens SignLink** on the stack instead
  of the game's editor, which would cut the text to 50 characters and break the picture. Settings, *Signs in the
  world*, switches it off.
- **The Sign tab's stop button** shows only while a GIF or a tool's animation plays on that sign, and says which.

## 0.9.1 (2026-10-08)

- **Glowing or lit.** Pictures glow by default (their own colours, day and night); a checkbox above *Place* turns
  that off, and the picture is then lit by the scene like normal sign text (dark at night). Art and saved signs get
  a choice above their put button: *As made*, *Glowing* (every part glows) or *Lit by the scene*. The API's
  `/image` takes `glow=0`.
- **The size in the world save.** The Picture tab counts the exact sign texts while you choose (the signs it needs,
  what they add to the world save, the largest sign), and says it again after placing. The Sign tab, the quick
  views of Art and Saved, and the put messages show it too.
- **Quick view on the Sign tab**: what the selected sign shows, to tell signs apart. Pictures and saved animations
  are read back from their texts, glyph art shows the render of the piece it came from, anything else its words.
- **Signs nearby** closes once a sign is chosen (from the list or with *Pick in view*). A stack is one row ("a stack
  of 3 signs"), and a short text sign shows its words.

## 0.9.0 (2026-10-08)

- **Picture, Art and Saved: three tabs by where the content comes from.**
  - **Picture**: your own image files, as before.
  - **Art**: the pieces that come with SignLink, built into the mod so nothing needs copying. Glyph art (drawn with
    letters and symbols, each with a rendered quick view) and pixel art (the cat, the fjord, the dragon ship, some
    of them moving).
  - **Saved**: your own collection. Save what the selected sign holds under a name, put it on other signs later,
    and delete with a second click to confirm. Files go in `BepInEx/config/SignLink/saved`.
- **Removed: the Presets tab and its folder.** Built-in art replaces the generated presets; your own presets
  belong in *saved*.

## 0.8.0 (2026-10-08)

- **The glyph art is back, as presets.** The Unicode pieces of the concept rounds (Huginn and Muninn, the dragon
  ship, Eikthyr, Mjolnir, the dragon crest, the moon halo, Jormungandr, the glowing midnight light) and a pixel-art
  dragon ship go on any sign.
  - Each paint level is one sign of a stack, all in one plane. A piece is about 2.4 m and sits 1 m above the sign.
  - `signart/presets.py` converts any concept the same way, and renders a picture of it for the quick view.
- **Presets are listed A to Z and grouped.** Variants share a heading, so *Cat* holds *moving*, *moving slowly* and
  *still*. Glyph art shows its rendered picture as the quick view.
- **Sign tab.** A table shows what the sign holds, its total size, whether it fits multiplayer, its animation and any
  ward.
- **The result line.** It says *Done:* or *Could not:* and empties after 10 seconds.

## 0.7.1 (2026-10-08)

- **Signs nearby no longer stutters.** The game lists the loaded signs once per refresh, not once per sign. Text
  sizes are counted again only when a sign changes, and the list changes only between frames.
- **Picture tab.** Picking another file removes the old quick view at once and shows that the new one is loading.
- **Presets tab.** *Save the selected sign as a preset* moved to the top, with an explanation and a text field you
  can see. *Apply* stands alone at the bottom.
- **Playing tab.** The *Stop the GIFs and tool animations* button is gone; each row has its own button.
- **Messages.** Clearer messages, cleared when you switch tabs.
- **Developer API.** It is back in Settings, off by default.
  - The switch takes effect at once, as do network access and the port.
  - Show, copy or renew the password, which every player's SignLink generates for itself on first start.

## 0.7.0 (2026-10-08)

- **Stacks look after themselves.**
  - Whatever goes on a sign (a picture, a preset, Clear, Paste, an animation) uses exactly as many stacked signs
    as it needs.
  - The copies already there are reused, missing ones are added, and leftovers are removed. Where you may not
    remove signs, the leftovers are emptied.
  - Clear leaves one empty sign. No copy keeps half of an earlier picture.
- **Presets.**
  - Pick one for a quick view: pixel pictures are drawn from their own text, and animations play.
  - *Apply* puts it on the sign; an animation is saved on it.
  - *Save the sign as a preset* stores what a sign holds: a picture over several signs, a text, or its saved
    animation.
  - *Play here* is gone.
- **Picture tab.**
  - Pixel size is a plain number, 1 to 16, where 1 is the finest pixel a sign draws. The default is 1.
  - The position is two signed sliders, right and up, from the sign's centre. The default is 0, 0: on the sign.
  - Typed values may go to 300 m.
  - Nothing scrolls sideways any more, and the *Replace* switch is gone: placing always replaces.
- **The window.**
  - Buttons that need a sign are greyed out until one is selected.
  - The selected sign in *Signs nearby* is highlighted.
  - The Sign tab shows what the sign holds, with Clear, Copy and Paste for the whole stack. Saved animations
    are listed on the Playing tab.
  - The Settings tab no longer shows the API.
- **API.** It is off by default and only in the config file, as a developer option. Players do not need it.

## 0.6.0 (2026-10-08)

- **Size in metres.**
  - You set the picture's width; the height follows its shape, and you can type either one.
  - Detail is the pixel size, from 3 mm to 5 cm.
  - The window shows what that costs: pixels, signs, data, and seconds to load for each player.
  - A picture with too few pixels for the size gets coarser pixels instead of being enlarged.
- **Position from the centre.**
  - Both offsets measure from the sign's centre to the picture's centre, and either may be negative.
  - *Above the sign* (1 m), *Centred on it* and *Below it* follow the picture's size; typing a number makes it a
    typed-in position.
  - Pictures below the sign or across it now encode, through a new voffset regime that is not yet verified in
    game. A band that still cannot be solved is split.
- **Signs nearby.** Signs within 30 m are listed with where they are from your view, stacks as one entry, so a
  sign built into a wall can still be chosen.
- **3 mm pixels.** This is the finest size whose line height TMP adds up exactly.
- **Smaller presets, 1 m above the sign.**
  - The Starry Night uses 6 mm pixels (3.1 x 2.5 m).
  - The cat is 1.7 m and the fjord 2.6 m.
- **Removed: the in-world preview of presets.** Over a stack it mixed with the other signs' text. Try presets on a
  spare sign.

## 0.5.0 (2026-10-08)

- **GIFs.**
  - A GIF's first frame becomes the picture on the signs, the one every player sees.
  - The animation plays over it, in full quality, for players with SignLink. It is not saved yet.
  - The mod decodes GIFs itself, frame for frame like Pillow.
- **Previews.**
  - The Picture tab shows the exact pixel grid the signs will get.
  - One-sign presets can be previewed on the sign for you only, then kept or undone.
- **Placement.**
  - Move the picture left or right, not only up.
  - Type exact numbers next to the sliders.
  - A finer pixel size: 0.625 cm (0.25 wu).
- **Art presets.** Three kinds:
  - texts;
  - pictures over several signs;
  - animations, which you preview here or save on the sign.
  `signart/presets.py` writes the cat, the fjord and stacked pictures.
- **The window, rebuilt.** One class per tab, plus:
  - a bar for the sign you work on;
  - a footer that always shows the main button and the status;
  - a Playing tab that explains its three kinds of moving things.

## 0.4.0 (2026-10-08)

- **Pictures from the mod itself.** A PNG or JPG becomes sign text on the sign you look at, with copies stacked
  on it when one sign is not enough. Every player sees it, with or without the mod. Use the window's Picture
  tab or `POST /image`. The encoder writes the same texts as the Python tools, byte for byte.
- **Animations stored on the sign.**
  - The frames are saved in the sign's own data and played by every SignLink player on the shared world clock,
    so everyone sees the same frame.
  - They survive logout and restarts and cost no network per frame. Vanilla players see the first frame.
  - Stop one in the Sign tab, or use `POST /anim?store=1` and `/anim/unstore`.
  - Editing the sign by hand stops it.
- **An import page in the browser.** Open `http://127.0.0.1:8417/` while the game runs.
  - Give the password and pick the sign under your crosshair or one nearby.
  - Drop a picture, check its size, place it.
  - With `AllowNetwork` it works from a phone on your home network too.
- **A new window (PageUp)** with five tabs: Sign, Picture, Playing, Presets and Settings. It is opaque, scales
  with the screen, and stops the mouse wheel from zooming the camera while it is open.
- **Stacks.** `GET /stack`, the Sign tab's stack actions, and screens that hide the text of their whole
  stack.
- **Fair play.**
  - Copies carry their author like the vanilla editor writes it.
  - Writing, cloning and deleting respect wards.
  - Copies have no creator, so monsters ignore them.
- **Robustness.**
  - Patches are applied one class at a time: one broken by a game update is skipped, and the rest of the mod
    keeps working.
  - Each playback system runs on its own, so one failing animation stops only itself.
  - A request that timed out never runs later.
  - Rejected uploads still get their answer.
  - Request bodies are read only after the password check.

## 0.3.3

- The first SignLink window (PageUp): playback switches, the sign you look at, presets, the API password.

## 0.3.2

- **Master password.** Requests must send `X-SignLink-Password`, which is generated on first start.
- **Wrong passwords.** Five wrong ones in a minute lock the address out for a minute.
- **Web pages.** CORS is answered for web pages, which still need the password.
- **Host header.** It must name this computer, which stops DNS rebinding.
- **Network.** `AllowNetwork` is off by default.
- **Building.** Creating and deleting signs works only in your own world, unless allowed.

## 0.3.1

- `POST /signs/clone` and `POST /signs/delete`.

## 0.3.0

- **Texture screens.** A GIF plays at full quality on a sign for players with the mod.
- **Layout.** The production project layout, with the game assembly publicized at build time.

## 0.2.1

- Animations follow the clock: a slow game drops frames, and the speed stays right.

## 0.1 / 0.2

- Live sign writing and text animations over a local HTTP API, local or synced.
