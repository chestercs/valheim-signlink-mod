# Release checklist (Thunderstore, Nexus Mods, GitHub)

What is settled, the steps of a release, and what went live when. Publishing is the author's step: Claude builds and
checks the packages and prepares the uploads; the push to GitHub and the stores' final buttons are the author's.

## The package

- `./sign.sh package` (`mod/package/package.py`) builds `mod/out/SignLink-<version>.zip` (Thunderstore and mod managers)
  and `mod/out/SignLink-<version>-nexus.zip` (Nexus Mods and manual installs: `BepInEx/plugins/SignLink/` with the DLL,
  README, CHANGELOG and both licences). It checks first: one version in `Plugin.cs`, `SignLink.csproj`, `manifest.json`
  and the top `CHANGELOG.md` entry, and that entry dated (not "not released yet"); the description under 250
  characters; a 256 x 256 PNG icon; the DLL of that version, without a web server (`TcpListener`, `HttpListener`);
  `mod/package/LICENSE` and `LICENSE-ART` the same as the repository's. The Thunderstore zip holds `manifest.json`,
  `icon.png`, `README.md` (the player-facing `mod/package/README.md`), `CHANGELOG.md`, `LICENSE` (MIT), `LICENSE-ART`
  (CC BY 4.0) and `plugins/SignLink.dll`.
- Dependency `denikson-BepInExPack_Valheim-5.4.2351` (the newest on Thunderstore, checked 2026-10-10).
- `./sign.sh mod` fails on any assembly reference the game lacks (RefCheck); no tuples in the plugin. RefCheck checks assembly
  names only: a member renamed by a game update shows in game, so run the in-game checks after every Valheim update.
- No listening sockets or network code (none at all since 1.1.0); nothing is downloaded or run at runtime; no
  obfuscation.
- Reviewed before release (0.17.0): signs and multiplayer, the picture pipeline, the window and animations, then a
  regression review of the fixes. Again for 0.20.148 (switching between Text, Art and Picture, in the code and in game)
  and 0.20.151 (security, bugs and multiplayer, wording and repository hygiene).

## Settled

- **Plugin GUID** `PetyusPolisz.SignLink` since 0.20.43 (decided 2026-10-11): the config file is
  `BepInEx/config/PetyusPolisz.SignLink.cfg`; a `bakterhaz.signlink.cfg` from earlier versions takes the new name once
  at start, its settings with it. It is the mod's identity for BepInEx and other mods and never changes.
- **Team** `PetyusPolisz` on Thunderstore (the package is `PetyusPolisz-SignLink`); the author is ChesTeRcs (the LICENSE
  and the DLL's details name them).
- **Versions.** The first public upload was 1.0.0; a version number can never be uploaded twice.
- **website_url** is the GitHub repository (https://github.com/chestercs/valheim-signlink-mod).
- **Pictures.** The pages' pictures live in `docs/media/`. The package README links them as `raw.githubusercontent.com`
  URLs of GitHub's `main`, so they show on Thunderstore once GitHub's `main` holds them.
- **A pure mod** (the author, 2026-10-13: no loader in the published mod): the zip holds `plugins/SignLink.dll` and the
  package files only (no ScriptEngine, no `scripts/`, nothing of the dev tool), and its CHANGELOG and README name no
  loader.
- **Branches.** `main` is the release branch. Work goes to `dev` on the author's Gogs server; `main` takes it with the
  next release. GitHub's `main` gets one commit per release, the tree of Gogs `main`, on top of the last (no force
  push). The first change after a release raises the version on `dev` at once (the author, 2026-10-17: the window must
  not show the released number on a changed build): `Plugin.cs`, `SignLink.csproj`, `manifest.json` and the
  CHANGELOG's top entry ("x.y.z (not released yet)", dated at the release).

## A release

1. **The texts.** `mod/package/README.md` reads as a page for players (the author, 2026-10-20): what the mod does and
   how to start first, no settings table pasted in, the art showcase last; the Nexus description
   (`.private/stores/nexus-description.bbcode`) in the same spirit. A changed text goes to the author as a draft before
   any upload. Date the CHANGELOG's top entry ("x.y.z (date)"; package.py reads the number and refuses an undated
   entry); README.md names the version by hand (its Version line and the Nexus zip's name).
2. **In game.** The three steps (a text, an art piece and a picture, each picked on 2 Content and put on from 3 Place),
   editing it later (*Update content*), the frame standing, tilted and lying, thumbnails on a folder of big photos, the
   arrow keys, *Select sign*, a few of the art library's pieces still and with *Moving* on. Write the results into
   `signart/ingame_tests.json`. Once as players get it: `./sign.sh plugin` (the DLL in `BepInEx/plugins`) in a
   profile without ScriptEngine.
3. **Multiplayer.** A dedicated server (the author's test server): a second client without SignLink sees the picture;
   a second SignLink client sees a saved animation.
4. **The checks**, all green: `./sign.sh mod` ("Build succeeded", RefCheck clean),
   `./sign.sh modtest "/work/mod/SignLink/Stock/The Starry Night (Van Gogh).jpg"` (writes the vectors with an image
   case, then the C# checks), `./sign.sh cstest`, `./sign.sh test`, `./sign.sh hashes check out/hashes.json`,
   `./sign.sh lang check` (every table complete), `./sign.sh qrtest`, then `./sign.sh package`.
5. **Gogs.** `main` fast-forwards to `dev` (`git push origin dev:main`), then the tag `v<version>` on it, pushed by name
   (never `--tags`).
6. **GitHub** (before the stores: the Thunderstore page takes its pictures from GitHub's `main`):
   `.private/stores/github-<version>-store.sh`, run by the author. It checks that Gogs `main` holds `dev`'s tree, the
   CHANGELOG is dated and both zips exist, makes one commit of Gogs `main`'s tree on top of GitHub's `main`
   (`git commit-tree`), pushes it and its tag, and opens a draft release with both zips and the CHANGELOG's top
   section as its notes.
7. **Thunderstore** (https://thunderstore.io, Valheim community, Upload): the zip, team `PetyusPolisz`, categories
   **Mods**, **Client-side**, **Building**, **Utility** and **AI Generated** (required for AI-written code). Claude may
   fill in the form; the author presses Submit. The page is the uploaded version's package README, so a change to it
   needs a new version. Since the 1.0.2 flag an update may wait for a moderator (`docs/mod-standards.md`, Thunderstore
   review).
8. **Nexus Mods** (https://www.nexusmods.com/valheim/mods/4453, edit pages `/games/valheim/mods/4453/edit/general`,
   `.../files`, `.../media`): *Files* › the nexus zip as an update of the previous file (Main, the version; the old one
   archived), a changelog line, the mod's version; the description through the editor's source view when it changed;
   the gallery when the pictures changed.
9. **After the stores are live:** publish GitHub's draft release.

## State at 1.0.0 (2026-10-16)

- `main` is the release branch: one clean commit for 1.0.0, on the author's Gogs server and on GitHub alike. The whole
  pre-release history (0.3 to 0.20.194) is kept on the author's own Gogs server only, on the branch
  `pre-release-history`; it never goes to GitHub. Commits are by ChesTeRcs (GitHub's noreply address).
- The 0.x changelog is [`changelog-0.x.md`](changelog-0.x.md); `mod/CHANGELOG.md` is for players from 1.0.0 on.
- Left out of the public repository (`.gitignore`): `signart/rigs/` (real base coordinates), `.private/` (the window's
  design notes, the store scripts), `CLAUDE.md` and `signart/ingame_tests.json` (the in-game log).

## Live since 1.0.1 (2026-10-16)

- Thunderstore: https://thunderstore.io/c/valheim/p/PetyusPolisz/SignLink/ (team PetyusPolisz; categories Mods,
  Client-side, Building, Utility, AI Generated).
- Nexus Mods: https://www.nexusmods.com/valheim/mods/4453 (Construction; `SignLink-1.0.1-nexus.zip`, requires
  BepInExPack Valheim 5.4.2351; description in BBCode, gallery with two single player / multiplayer comparisons;
  permissions: re-upload, conversion, modification and asset use allowed with credit). Mod Rewards opt-in once Nexus
  lists the mod on its rewards page.
- GitHub: public, release v1.0.1 (both zips), wiki, social preview, homepage = the Thunderstore page,
  `.github/FUNDING.yml` (Buy Me a Coffee).
- Every page links the others: README (badges, Install), the release notes, the Nexus description, the package README.

## Live since 1.0.2 (2026-10-18)

- Thunderstore 1.0.2, Nexus 4453 (the file uploaded as an update of the 1.0.1 file, which is archived; the gallery: the
  QR picture and three window pictures in place of the old ones), the GitHub release v1.0.2 with both zips, the wiki
  regenerated from the docs.
- How it went: a multiplayer check on the author's dev server first (a picture at the multiplayer maximum, a moving
  GIF, Moving off and on, a tilt applied), then the CHANGELOG dated, `./sign.sh mod`, `cstest` and `package`, Gogs
  `main` fast-forwarded with the tag, GitHub by `.private/stores/github-1.0.2-store.sh` (a draft release, published once
  the stores were live). The Thunderstore upload form was filled by Claude, the Submit was the author's click.

## Live since 1.0.3 (2026-10-18)

- Thunderstore's moderators hid the Valheim listing after the 1.0.2 upload ("Invalid submission"). Asked in their
  Discord's rejected-uploads forum, a moderator named two things: pictures must not live in the config folder (a mod
  manager packs `BepInEx/config` into every exported profile and profile code, which must stay small) and the web
  server must go. 1.0.3 did both: the images folder is `SignLink/images` in the game's data folder (moved over once),
  and the released build has no web server (1.1.0 has none in any build); `package.py` refuses a DLL with one.
- GitHub release v1.0.3 (`.private/stores/github-1.0.3-store.sh`), Nexus 4453 (the file as an update of 1.0.2,
  archived; one description line), the wiki updated, Thunderstore 1.0.3 uploaded (the author's Submit); the listing
  showed again once the moderators approved it.
- Rules to keep: nothing big under `BepInEx/config` (only the .cfg); no listening sockets or network code in a release.

## Live since 1.1.0 (2026-10-21)

- One test in installed mode before the upload: the release DLL from the Nexus zip in `BepInEx/plugins/SignLink/`, no
  SignLink copy under `scripts/`; an art piece and a picture put on a new sign, the language switched while open.
- Gogs `main` fast-forwarded with tag v1.1.0; GitHub `main` b910b94 and release v1.1.0 (both zips) by
  `.private/stores/github-1.1.0-store.sh`; Thunderstore 1.1.0 (the author's Submit) was hidden again until the moderator re-approved it the same evening (asked in the rejected-uploads forum with the package link);
  Nexus 4453: the file as an update of 1.0.3 (archived, mod version 1.1.0, a changelog line), the description and the
  summary from `.private/stores/nexus-description.bbcode`; the wiki regenerated.
- Not done: the Nexus gallery still shows the 1.0.2 window pictures (window-4/5 and transparency.jpg not added).

## 1.1.0's pictures

New in `docs/media/` for the README, the package README and the Nexus gallery: `window-1-sign.jpg` to
`window-5-settings.jpg` (one per tab), `window-steps.gif` (the three steps) and `transparency.jpg` (a cut-out PNG on a
sign hidden in the ground).

## Multiplayer test (2026-10-16, the author's dedicated test server)

- Pictures, the limits, poses, Copy/Paste of a moving piece, Pause, leaving and coming back: as in single player.
  Playback stops beyond 64 m; the area counts a sign 97 m away and not one 107 m away.
- 18 pictures of 256 KB (4.5 MB) in one place: the server's CPU and memory stayed flat, a save took 0.8-1.2 s with or
  without them, and the world grew by 1.2 bytes a byte of sign text. A player joining among them gets the signs one
  by one at the server's pace (about 105-150 KB/s: a 222 KB sign every 1.55 s, timed per sign) and sees one hitch as
  each is built (0.26-0.29 s for 222 KB, 0.3-0.5 s for 256 KB): the same without SignLink (the game's own text
  rendering). The download itself does not stop the game.
- Resolution and Width dragged with the mouse on a 3859 px picture on the server: every step goes the mouse's way.
- The same client with SignLink unloaded (a vanilla player, joined again): a picture, a 4 m transparent PNG, a
  two-sign glyph art and a moving piece tilted 30° show as with SignLink (same ids, places, poses and texts; the moving
  piece stands on its first frame). The game's own hover over a picture sign's board costs that player about 150 ms
  a frame (README, known limits). SignLink back: its short hover line, the stack and the movement return.
- 25 moving pieces in one place hitched twice a second; fixed in 1.0.1 (updates spread over the second).
- The area limit counts only the zones this game has loaded: kept as it is. A player can place more than 600 KB
  within 100 m by standing far from the older signs, and the test showed that a server carries far more than that.

## Feature ideas (after the stability phase; the author decides when)

- **Mod-only pictures** (the author, 2026-10-16): the sign holds only a reference to the picture (its name or id),
  plus how the uploader placed it (size, place, pose, Resolution, light, GIF playback). A vanilla player sees nothing of
  it, and a SignLink player sees the picture or the moving GIF, drawn by the mod.
  - **Set aside (the author, 2026-10-28):** the friend's wish (high-quality GIFs without loading the server) is met by
    1.2.0: a moving GIF plays at its own size whatever its still's Resolution, so a low Resolution keeps the world small.
    No still-less GIF signs: "ne bonyolítsuk még tovább a modunkat". What the code already has stays.
  - The open question is how the picture reaches the other SignLink players. Today a GIF moves only where its file
    is. It needs the player-to-player transfer that the frozen GIF topic leaned to (`docs/mod-standards.md`, "Moving
    GIFs for players without the file"): RPC chunks from a player who has the file, cached on disk, perhaps checked
    against a hash stored with the reference. Without a transfer, a player who lacks the file sees nothing.
  - Why (the author, 2026-10-16): a friend loves high-quality GIFs and does not mind that others cannot see them, as
    long as he sees them in multiplayer.
  - The gains: no sign-text cost (the multiplayer limits and the arrival hitch would not apply to these signs), and a
    real texture at full quality, GIFs moving for everyone with the mod.
  - To settle first: what a vanilla player sees (an empty sign, or a short line naming SignLink); what happens when
    nobody online has the file; size caps and the server's share of the traffic; whether the reference may go stale
    (a renamed or changed file).
- **GIFs moving before the put** (the author, 2026-10-20; later, not in 1.1.0): a GIF picked on 2 Content › Picture
  plays in the Quick view and in the live preview on the sign, as it will once put (the preview's motion only). About
  250-300 lines: a frames job in PictureSource (`GifScreen` at the screen width, `Plugin.Screens.FramesFor` first),
  counted against the GIF memory (a hold or a pinned park in ScreenPlayer, so the put reuses the frames and moves at
  once), Thumb playing timed RGB24 frames (`FrameTimes`), and a screen of its own over ContentPreview's picture
  (overscan as `PlayOver`, lit as the put, its texture destroyed on Clear). Risks: decode time of long GIFs, memory
  while picking one GIF after another, upload cost per frame.
