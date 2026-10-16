# Release checklist (Thunderstore)

What is ready, what the author decides, and the steps. Publishing is the author's step: Claude builds and checks the
package and uploads nothing.

## Ready

- `./sign.sh package` builds `mod/out/SignLink-<version>.zip` and checks: one version in `Plugin.cs`, `SignLink.csproj`,
  `manifest.json`, the top `CHANGELOG.md` entry and `docs/openapi.yaml`; the description under 250 characters; a 256 x 256 PNG icon; the DLL of that
  version. The zip holds `manifest.json`, `icon.png`, `README.md` (the user-facing one, `mod/package/README.md`),
  `CHANGELOG.md`, `LICENSE` (MIT) and `plugins/SignLink.dll`.
- Dependency `denikson-BepInExPack_Valheim-5.4.2351` (the newest on Thunderstore, checked 2026-10-10).
- `./sign.sh mod` fails on any assembly reference the game lacks (RefCheck); no tuples in the plugin. RefCheck checks assembly
  names only: a member renamed by a game update shows in game, so run the in-game checklist after every Valheim
  update.
- The developer API is off by default; nothing is downloaded or run at runtime; no obfuscation.
- Reviewed before release (0.17.0): signs and multiplayer, the picture pipeline, the window, the API and animations,
  then a regression review of the fixes. Again for 0.20.148 (switching between Text, Art and Picture, in the code and
  in game) and 0.20.151 (security, bugs and multiplayer, wording and repository hygiene).

## The author decides

1. **Plugin GUID.** Decided 2026-10-11: `PetyusPolisz.SignLink` since 0.20.43 (the config file is
   `BepInEx/config/PetyusPolisz.SignLink.cfg`; a `bakterhaz.signlink.cfg` from earlier versions takes the new name
   once at start, settings and API password with it). It is the mod's identity for BepInEx and other mods; it does
   not change after the first public upload.
2. **Team name** on Thunderstore: decided 2026-10-11, `PetyusPolisz` (the package is `PetyusPolisz-SignLink`); the
   author is ChesTeRcs (the LICENSE and the DLL's details name them).
3. **Version.** The first public upload as 1.0.0 (a version number can never be uploaded twice).
4. **website_url.** Empty, or a public repository (the private git server is not reachable for others).
5. **Screenshots.** Thunderstore shows the README; pictures need public image URLs (optional, but they sell a
   picture mod).

## State at 1.0.0 (2026-10-16)

- `main` is the release branch: one clean commit for 1.0.0, on the author's Gogs server and on GitHub alike. The whole
  pre-release history (0.3 to 0.20.194) is kept on the author's own Gogs server only, on the branch
  `pre-release-history`; it never goes to GitHub. Commits are by ChesTeRcs (GitHub's noreply address).
- The 0.x changelog is [`changelog-0.x.md`](changelog-0.x.md); `mod/CHANGELOG.md` is for players from 1.0.0 on.
- Left out of the repository, kept on the author's computer (`.gitignore`): `signart/rigs/` (real base coordinates),
  `.private/` (the window's design notes), `CLAUDE.md` and `signart/ingame_tests.json` (the in-game log).
- The GitHub repository is private for now; the 1.0.0 release is a draft with `SignLink-1.0.0.zip` (import it in
  r2modman or the Thunderstore Mod Manager: *Settings › Import local mod*). Making the repository public and
  publishing the release are the author's steps; a wiki from `docs/` can follow once it is public (GitHub Free has
  wikis on public repositories only).

- Fixes after 1.0.0 go to the branch `dev` on the author's Gogs server (CHANGELOG "Unreleased"); `main` takes them
  with the next release.

### Multiplayer test (2026-10-16, the author's dedicated test server)

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
- 25 moving pieces in one place hitched twice a second; fixed on `dev` (updates spread over the second).
- The area limit counts only the zones this game has loaded: kept as it is. A player can place more than 600 KB
  within 100 m by standing far from the older signs, and the test showed that a server carries far more than that.

### Still open

- `docs/media/window-steps.gif` still shows an older window.
- The author's tests: a pure-mod install from the release zip on a fresh game start.
- Thunderstore and Nexus Mods listings, when the author asks.

## Before the upload

1. In game: the three steps (a text, an art piece and a picture, each picked on 2 Content and put on from 3 Place),
   editing it later (*Update*), the frame standing, tilted and lying, thumbnails on a folder of big photos, the
   arrow keys, *Pick in view*, a few of the art library's pieces still and with *Moving* on. Write the results into
   `signart/ingame_tests.json`.
2. Multiplayer: a dedicated server (the author's test server), a second client without SignLink sees the picture; a
   second SignLink client sees a saved animation.
3. `./sign.sh mod`, `./sign.sh cstest`, `./sign.sh modtest`, `./sign.sh test`, `./sign.sh hashes check out/hashes.json`,
   `./sign.sh pagetest`, `./sign.sh package`: all green.
4. **A pure mod** (the author's decision, 2026-10-13: no loader in the published mod): the zip holds
   `plugins/SignLink.dll` and the package files only (no ScriptEngine, no `scripts/`, nothing of the dev tool), and its
   CHANGELOG and README name no loader. Test the release once as players get it: `./sign.sh plugin` (the DLL in
   `BepInEx/plugins`) in a profile without ScriptEngine.
5. **The public repository** (the author decides how): `signart/rigs/` holds real base coordinates and goes before
   anything is public; the history also holds working notes, so a fresh repository (or a rewritten history) is
   safer than making this one public.
6. The version 1.0.0 in all five places, and a player-facing CHANGELOG entry.

## Upload (author)

- thunderstore.io, Valheim community, Upload: the zip; categories **Client-side**, **Building** and **AI Generated** (required for
  AI-written code; check the exact names on the upload page): the one list, which `mod/README.md` and
  `docs/mod-standards.md` point to.
- Or `tcli publish` with a `thunderstore.toml` and `TCLI_AUTH_TOKEN` (see `docs/mod-standards.md`).
