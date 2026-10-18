#!/usr/bin/env bash
# sign.sh - Valheim sign-art desk. Everything runs in Docker (image valheim-signart; node:22-alpine for the JS check).
#
#   ./sign.sh image              build the Docker image
#   ./sign.sh extract            glyph outlines + TMP metrics from the game fonts -> signart/out/glyphs.json
#   ./sign.sh build [round...]   concepts -> cells, commands, renders, signart/out/simulator.html
#   ./sign.sh test               engine unit tests + JS/Python layout parity over every built row
#   ./sign.sh sheet [group...]   glyph contact sheets (shapes, lines, runes, symbols, emoji) -> signart/out/
#   ./sign.sh gallery <out.png> [--cells] [key...]   contact sheet of concept renders
#   ./sign.sh py <script> [args] any script inside signart/
#   ./sign.sh rig scan|apply|texts ...   sign rig in a world save (signart/rig.py); /wl = worlds_local
#     ./sign.sh rig scan /work/worlds/<template> rigs/<name>.json
#     ./sign.sh rig apply rigs/<name>.json /work/worlds/<template> /wl <NewWorld> --concept <key> [--force]
#   ./sign.sh row build|preview ...   the rig turned and copied into a row of blocks, one piece each (signart/row.py)
#     ./sign.sh row build rigs/<name>.json /work/worlds/<template> /wl <NewWorld> [--force] [--dry-run]
#   ./sign.sh mod                build the SignLink BepInEx plugin -> mod/out/SignLink.dll (.NET SDK container); fails when
#                                it references an assembly the game does not have (mod/RefCheck); the release build, without the
#                                developer API (DEVAPI=true ./sign.sh mod builds it in, as dev and plugin do)
#   ./sign.sh dev                build, then into the profile's BepInEx/scripts: ScriptEngine reloads it in the running game
#                                within seconds (plugins/SignLink*.dll goes: one copy only; the game holds it while it runs)
#   ./sign.sh plugin             build, then back into BepInEx/plugins as SignLink.<version>.dll (scripts/ emptied)
#   ./sign.sh modtest [image...] the plugin's C# picture encoder against the Python pipeline (mod/SignLink.Tests), and its
#                                resized and moved texts laid out by the engine (signart/transform_check.py)
#   ./sign.sh cstest             the C# checks alone (after one modtest, which writes the vectors)
#   ./sign.sh pagetest          the import page (mod/SignLink/Web) in jsdom against a mock of the API
#   ./sign.sh qrtest             the QR template as the sign shows it: cstest writes its text, the TMP model renders it, zbar reads it
#   ./sign.sh art                regenerate the art built into SignLink (mod/SignLink/Art, from the concept rounds)
#   ./sign.sh palette            the text templates' glyph table (mod/SignLink/Pictures/GlyphData.cs, from out/glyphs.json)
#   ./sign.sh package            the Thunderstore zip -> mod/out/SignLink-<version>.zip (checks versions; uploads nothing)
#   ./sign.sh live <cmd> ...     talk to SignLink in the running game (signart/live.py; ping, signs, write, anim; a dev build)
#   ./sign.sh hashes save|check out/hashes.json   fingerprint every generated text (refactors must keep them)
#
# VALHEIM_DATA points at the game's valheim_Data folder (read-only mount); WORLD_TOOL at the
# valheim-world-tool checkout (vsave.py); VALHEIM_USER at the game's LocalLow folder (worlds_local, Player.log);
# BEPINEX_PACK at an unpacked BepInExPack_Valheim (compile references only).
set -euo pipefail
export MSYS_NO_PATHCONV=1   # Git Bash: keep container paths untouched
ROOT="$(cd "$(dirname "$0")" && pwd -W 2>/dev/null || pwd)"
VALHEIM_DATA="${VALHEIM_DATA:-E:/SteamLibrary/steamapps/common/Valheim/valheim_Data}"
WORLD_TOOL="${WORLD_TOOL:-$ROOT/../valheim-world-tool}"
WIN_HOME="$(cygpath -m "${USERPROFILE:-$HOME}" 2>/dev/null || echo "${USERPROFILE:-$HOME}")"   # this user's Windows profile
TMM="$WIN_HOME/AppData/Roaming/Thunderstore Mod Manager/DataFolder/Valheim"   # Thunderstore Mod Manager's Valheim data
VALHEIM_USER="${VALHEIM_USER:-$WIN_HOME/AppData/LocalLow/IronGate/Valheim}"
BEPINEX_PROFILE="${BEPINEX_PROFILE:-$TMM/profiles/Default/BepInEx}"
BEPINEX_PACK="${BEPINEX_PACK:-$TMM/cache/denikson-BepInExPack_Valheim/5.4.2333/BepInExPack_Valheim}"
IMG=valheim-signart
run() { docker run --rm -v "$ROOT:/work" -w /work/signart "$@"; }

cmd="${1:-help}"; shift || true
case "$cmd" in
  image)   docker build -t "$IMG" "$ROOT/signart" ;;
  extract) run -v "$VALHEIM_DATA:/game:ro" "$IMG" python extract_glyphs.py /game out ;;
  build)   run "$IMG" python build.py "$@" ;;
  test)    run "$IMG" python -m pytest -q tests
           run "$IMG" python tests/parity_dump.py
           run node:22-alpine node tests/parity.mjs ;;
  sheet)   run "$IMG" python sheet.py "$@" ;;
  gallery) run "$IMG" python montage.py "$@" ;;
  py)      run "$IMG" python "$@" ;;
  rig)     run -v "$WORLD_TOOL:/wt:ro" -v "$VALHEIM_USER/worlds_local:/wl" -v "$VALHEIM_USER:/vlog:ro" "$IMG" python rig.py "$@" ;;
  row)     run -v "$WORLD_TOOL:/wt:ro" -v "$VALHEIM_USER/worlds_local:/wl" -v "$VALHEIM_USER:/vlog:ro" "$IMG" python row.py "$@" ;;
  mod)     docker run --rm -e DEVAPI="${DEVAPI:-false}" -v "$ROOT/mod:/src" -v "${VALHEIM_DATA%/valheim_Data}:/game:ro" -v "$BEPINEX_PACK:/bepinex:ro" \
             -w /src/SignLink mcr.microsoft.com/dotnet/sdk:8.0 sh -c \
             'dotnet build -c Release -p:DevApi=$DEVAPI -o /src/build -nologo && mkdir -p /src/out && cp /src/build/SignLink.dll /src/out/ &&
              cd /src/RefCheck && dotnet run -c Release -- /src/out/SignLink.dll /game/valheim_Data/Managed /bepinex/BepInEx/core' ;;
  modtest) run -v "$WORLD_TOOL:/wt:ro" "$IMG" python encoder_vectors.py /work/mod/SignLink.Tests/vectors "$@"
           docker run --rm -v "$ROOT/mod:/src" -w /src/SignLink.Tests mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release -- vectors/vectors.json
           run "$IMG" python transform_check.py /work/mod/SignLink.Tests/vectors/transformed.json ;;
  cstest)  docker run --rm -v "$ROOT/mod:/src" -w /src/SignLink.Tests mcr.microsoft.com/dotnet/sdk:8.0 dotnet run -c Release -- vectors/vectors.json ;;   # the C# checks alone (modtest once first: it writes the vectors)
  qrtest)  "$0" cstest | grep -E "^(qr|FAIL|ALL)"                           # writes mod/SignLink.Tests/qr_sample.txt
           run "$IMG" python qr_render.py /work/mod/SignLink.Tests/qr_sample.txt /work/mod/SignLink.Tests/qr_sample.png
           got=$(docker run --rm -v "$ROOT/mod/SignLink.Tests:/t:ro" python:3.12-slim sh -c "apt-get update -qq >/dev/null && apt-get install -y -qq libzbar0 >/dev/null 2>&1 && pip install -q pyzbar==0.1.9 pillow==11.0.0 >/dev/null 2>&1; python -I -c \"from PIL import Image; from pyzbar import pyzbar; print(*[g.data.decode() for g in pyzbar.decode(Image.open('/t/qr_sample.png'))])\"")
           [ "$got" = "https://github.com/chestercs/valheim-signlink-mod" ] && echo "qrtest: zbar reads the sign: $got" || { echo "qrtest FAILED: zbar read [$got]"; exit 1; }
           run "$IMG" python qr_render.py /work/mod/SignLink.Tests/qr_sweep >/dev/null       # sizes, versions, levels (cases.tsv)
           docker run --rm -v "$ROOT/mod/SignLink.Tests/qr_sweep:/t:ro" python:3.12-slim sh -c "apt-get update -qq >/dev/null && apt-get install -y -qq libzbar0 >/dev/null 2>&1 && pip install -q pyzbar==0.1.9 pillow==11.0.0 >/dev/null 2>&1; python -I -c \"
from PIL import Image; from pyzbar import pyzbar
bad = []; cases = [l.split('\t') for l in open('/t/cases.tsv', encoding='utf-8').read().splitlines()]
for f, want in cases:
    got = [g.data.decode() for g in pyzbar.decode(Image.open('/t/' + f[:-4] + '.png'))]
    if got != [want]: bad.append(f)
print('qrtest sweep: zbar reads', len(cases) - len(bad), 'of', len(cases), 'codes', *(['; not read:'] + bad if bad else []))
raise SystemExit(1 if bad else 0)\"" || { echo "qrtest FAILED: the sweep"; exit 1; } ;;
  pagetest) ( trap 'docker rm -f signlink-mock >/dev/null 2>&1; docker network rm signlink-test >/dev/null 2>&1' EXIT   # a failing test still cleans up
           T="$ROOT/mod/SignLink.Tests/page"; mkdir -p "$T/out"; docker network create signlink-test >/dev/null 2>&1 || true
           run -v "$T/out:/out" "$IMG" python -c "import numpy as n; from PIL import Image as I; y,x=n.mgrid[0:300,0:400]; I.fromarray(n.stack([x*255//399,y*255//299,(x+y)*255//698],-1).astype(n.uint8)).save('/out/test_gradient.png')"
           docker rm -f signlink-mock >/dev/null 2>&1 || true
           docker run -d --rm --name signlink-mock --network signlink-test -v "$T:/m:ro" -v "$ROOT/mod/SignLink/Web:/page:ro" "$IMG" python -u /m/mock.py >/dev/null
           docker run --rm --network signlink-test -v "$T:/m" -v "$ROOT/mod/SignLink/Web:/page:ro" -w /m node:22-alpine sh -c 'npm install --silent --no-audit --no-fund --no-save jsdom@24 >/dev/null 2>&1; node page_test.mjs' ); exit $? ;;
  art)     rm -rf "$ROOT/mod/SignLink/Art"; run -v "$WORLD_TOOL:/wt:ro" "$IMG" python presets.py /work/mod/SignLink/Art ;;
  palette) run "$IMG" python palette.py ;;                              # the text templates' glyph table -> mod/SignLink/Pictures/GlyphData.cs
  package) run "$IMG" python /work/mod/package/package.py ;;
  live)    run -v "$WORLD_TOOL:/wt:ro" -v "$BEPINEX_PROFILE/config:/cfg:ro" "$IMG" python live.py "$@" ;;      # reaches the game via host.docker.internal (Docker Desktop)
  hashes)  run -v "$WORLD_TOOL:/wt:ro" "$IMG" python hashes.py "$@" ;;   # save|check out/hashes.json
  dev)     DEVAPI=true "$0" mod                                               # ScriptEngine (BepInEx.Debug) watches BepInEx/scripts
           mkdir -p "$BEPINEX_PROFILE/scripts"                            # and loads the copy there again (0.20.135); it reads
           cp "$ROOT/mod/build/SignLink.pdb" "$BEPINEX_PROFILE/scripts/SignLink.pdb"   # the symbols too (SymbolsNotFoundException
           cp "$ROOT/mod/out/SignLink.dll" "$BEPINEX_PROFILE/scripts/SignLink.dll"     # without): them first, the DLL wakes it
           for f in "$BEPINEX_PROFILE"/plugins/SignLink*.dll; do
             [ -e "$f" ] || continue
             if rm -f "$f" 2>/dev/null; then echo "removed plugins/$(basename "$f"): one copy only, in scripts/"
             else echo "plugins/$(basename "$f") is in use: close the game once, then ./sign.sh dev again (two copies would clash)"; fi
           done
           echo "scripts/SignLink.dll: a running game loads it again within a few seconds (ScriptEngine)" ;;
  plugin)  v=$(sed -n 's/.*public const string Version = "\([^"]*\)".*/\1/p' "$ROOT/mod/SignLink/Plugin.cs")
           DEVAPI=true "$0" mod                                         # the author's own game: with the developer API
           cp "$ROOT/mod/out/SignLink.dll" "$BEPINEX_PROFILE/plugins/SignLink.$v.dll"
           rm -f "$BEPINEX_PROFILE/scripts/SignLink.dll" "$BEPINEX_PROFILE/scripts/SignLink.pdb"
           echo "plugins/SignLink.$v.dll; scripts/ emptied (an older plugins/SignLink.*.dll goes after the next start)" ;;
  *)       sed -n '2,33p' "$0" ;;
esac
