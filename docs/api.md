# SignLink developer API

Write, animate and fill Valheim signs from your own scripts, servers and bots. This page is the reference; the
machine-readable version is [`openapi.yaml`](openapi.yaml) (OpenAPI 3.1). Open it in
[Swagger Editor](https://editor.swagger.io) (*File › Import file*), Postman or Insomnia to browse it or generate a
client.

```
Base URL   http://127.0.0.1:8417          (the port is a setting)
Auth       X-SignLink-Password: <password>
Answers    JSON: {"ok": true, ...}  or  {"ok": false, "error": "..."} with a 4xx status
```

🟦 `GET`  🟩 `POST`

## Contents

- [Getting started](#getting-started)
- [Status](#status): [`GET /ping`](#get-ping) · [`GET /`](#get-)
- [Signs](#signs): [`GET /signs`](#get-signs) · [`GET /hovered`](#get-hovered) · [`GET /sign`](#get-sign) · [`POST /sign`](#post-sign)
- [Pictures](#pictures): [`POST /image`](#post-image)
- [Stacks](#stacks): [`GET /stack`](#get-stack) · [`POST /pose`](#post-pose) · [`POST /signs/clone`](#post-signsclone) · [`POST /signs/delete`](#post-signsdelete)
- [Text animation](#text-animation): [`POST /anim`](#post-anim) · [`GET /anim`](#get-anim) · [`POST /anim/stop`](#post-animstop) · [`POST /anim/unstore`](#post-animunstore)
- [Screens](#screens): [`POST /screen`](#post-screen) · [`GET /screens`](#get-screens) · [`POST /screen/stop`](#post-screenstop)
- [Errors](#errors) · [Limits](#limits) · [Security](#security)

## Getting started

1. In the game, open SignLink, *Settings › Developer API*: switch it on and copy the password.
2. Keep the password in an environment variable and ask the game who is there:

```sh
export PW='the password'
curl -s -H "X-SignLink-Password: $PW" http://127.0.0.1:8417/ping
# {"ok":true,"version":"1.0.2","inWorld":true,"player":[80.12,31.5,-95.03]}
```

3. Look at a sign, then write to it:

```sh
ID=$(curl -s -H "X-SignLink-Password: $PW" http://127.0.0.1:8417/hovered | sed -E 's/.*"id":"([^"]+)".*/\1/')
curl -s -H "X-SignLink-Password: $PW" --data-binary 'Hello from <color=#ffcc00>outside</color>' \
     "http://127.0.0.1:8417/sign?id=$ID&mode=zdo"
```

`mode=zdo` saves the text and sends it to everyone, like the in-game editor. `mode=local` (the default) changes only
what your game shows: fast, and gone when the area reloads.

> [!NOTE]
> Sign ids (`"1234567890:42"`) are the game's object ids and change every session. List `/signs` again after a
> restart.

> [!IMPORTANT]
> A web page cannot call the API from a browser: there is no CORS, by design. Call it from a script, a home server or
> a bot.

## Status

<a id="get-ping"></a>
### 🟦 `GET /ping`

The version; with the password also whether a world is loaded and where the player stands. Without the password
header it answers the version only, so a tool can check that the API is on.

```json
{"ok": true, "version": "1.0.2", "inWorld": true, "player": [80.12, 31.5, -95.03]}
```

<a id="get-"></a>
### 🟦 `GET /`

The picture import page, served by the mod itself: pick the sign under your crosshair or one nearby, drop a picture,
set its size and pose, place it. No password header needed (the page asks for it). With network access on, a phone on
your home network can use it too.

## Signs

<a id="get-signs"></a>
### 🟦 `GET /signs`

The loaded signs around the player, nearest first.

| Parameter | Type | Default | |
|---|---|---|---|
| `radius` | number | 50 | metres around the player |

```json
{"ok": true, "signs": [
  {"id": "1234567890:42", "dist": 2.31, "pos": [80.0, 32.1, -95.5], "yaw": 90, "len": 11, "preview": "Fish & Ale"}
]}
```

`len` is the text's length in characters, `preview` its first 60.

<a id="get-hovered"></a>
### 🟦 `GET /hovered`

The sign under the crosshair, in the same shape, or `"sign": null`. For a stack it is the rearmost sign, the original
the copies were made from. Nothing is hovered while a tool such as the hammer is in hand.

<a id="get-sign"></a>
### 🟦 `GET /sign`

| Parameter | Type | |
|---|---|---|
| `id` | string, **required** | the sign |

```json
{"ok": true, "id": "1234567890:42", "text": "Fish & Ale"}
```

<a id="post-sign"></a>
### 🟩 `POST /sign`

Write any text, of any length, to one sign. The body is the text (UTF-8); rich text tags work as on any sign. Only this
sign changes: a stack's copies keep their texts. A text animation running on it stops.

| Parameter | Type | Default | |
|---|---|---|---|
| `id` | string, **required** | | the sign |
| `mode` | `local` \| `zdo` | `local` | `zdo` saves and syncs |

```sh
curl -s -H "X-SignLink-Password: $PW" --data-binary @board.txt "http://127.0.0.1:8417/sign?id=$ID&mode=zdo"
```

```json
{"ok": true, "ms": 0.42, "chars": 32}
```

`ms` is the time the game took to rebuild the text.

## Pictures

<a id="post-image"></a>
### 🟩 `POST /image`

A PNG, JPG or GIF as a picture on a sign, exactly as the window places it: saved sign text on the sign and, when it
needs more, on copies stacked on it. Every player sees it, with or without the mod. A moving GIF also plays over it for
SignLink players. The body is the image file.

| Parameter | Type | Default | |
|---|---|---|---|
| `id` | string, **required** | | the sign |
| `width` | number, 0..200 | | width in metres |
| `detail` | integer, 1..4000 | | pixels across, with `width` (the window's model) |
| `pitch` | number | 1 | pixel size in sign units instead of `detail`: a multiple of 0.125 up to 40 the game reads exactly (0.125, 0.25, 0.5, 0.625, 0.75, 1, 1.25, 1.5, 2 ...) |
| `place` | `above` \| `centred` \| `below` | `above` | on a standing sign; a tilted sign's picture is centred unless asked |
| `x`, `up`, `out` | metres, ±300 | 0 | the picture's centre from the sign's centre: right, up, towards you as the sign was built; `up` overrides `place` |
| `tilt` | degrees, -90..90 | as it is | tips the sign over its bottom edge, + face up |
| `pose` | `standing` \| `floor` \| `ceiling` | | instead of `tilt` |
| `turn` | degrees | as it is | clockwise |
| `maxkb` | 16..400 | 400 | at most this many KB a sign |
| `glow` | 0 \| 1 | 1 | 1 glows day and night, 0 is lit by the scene |
| `alpha` | 0 \| 1 | 1 | keep PNG transparency |
| `cover` | 0 \| 2 | 2 | 2 draws the pixels seamless |
| `animate` | 0 \| 1 | 1 | play a moving GIF over it |
| `dry` | 0 \| 1 | 0 | only encode and report the sizes (anything but 0 is a dry run) |
| `name` | string | | the file's name under the images folder, its folders with `/` (`funny/cat.gif`), kept so the window can edit the picture later and a GIF moves where the file is; a name that is not one (a step out of the folder, a device name) is dropped, and the `note` says so |

Older parameters still work: `pixels` (a pixel budget, 100..2 000 000) and `above` (the bottom edge in metres).

```sh
curl -s -H "X-SignLink-Password: $PW" --data-binary @starry-night.jpg \
     "http://127.0.0.1:8417/image?id=$ID&width=4&detail=160&place=centred&name=starry-night.jpg"
```

```json
{"ok": true, "w": 160, "h": 127, "pitch": 1, "signs": 1, "kb": [262], "saveKb": 262, "encodeMs": 85.4,
 "ids": ["1234567890:42"]}
```

`kb` lists each sign's size, `ids` the signs that hold the picture (not with `dry=1`). A GIF's answer has a `note`: it
plays over the picture, or why it stays still: the GIFs playing on this computer leave no room for its frames now (it
plays when room frees, when its file is in the images folder under `name`), or it is too big for the GIF memory at
any time. Its frames are decoded only when they can play; a dry run decodes the still alone.

> [!TIP]
> Pixel sizes come in steps, so the picture can come out a little wider than asked. Try with `dry=1` first and read
> `w` and `pitch` in the answer.

> [!NOTE]
> On a lying or tipped sign only the part along its face counts: lying, `x` and `out` move the picture and `up` is
> dropped. In a shared world every write must also fit 300 KB a sign and 600 KB within 100 m.

## Stacks

A picture bigger than one sign can hold uses copies of the sign stacked a fraction of a millimetre in front of it.

<a id="get-stack"></a>
### 🟦 `GET /stack`

The sign and the copies stacked on it, rear first.

```json
{"ok": true, "signs": [{"id": "1234567890:42", "len": 98112, "bytes": 268300}]}
```

<a id="post-pose"></a>
### 🟩 `POST /pose`

Tilt and turn a sign together with its stack, as the window's Tilt and Rotate rows do: the sign tilts about its middle
and the content keeps its place on the sign's face. The signs are made again, so their ids change: the answer lists
the new ones.

| Parameter | Type | |
|---|---|---|
| `id` | string, **required** | any sign of the stack |
| `tilt` | degrees, -90..90 | + face up; left out, as it is |
| `pose` | `standing` \| `floor` \| `ceiling` | instead of `tilt` |
| `turn` | degrees | clockwise; left out, as it is |

```json
{"ok": true, "ids": ["1234567890:51", "1234567890:52"]}
```

<a id="post-signsclone"></a>
### 🟩 `POST /signs/clone`

New signs stacked in front of `id`, at its exact rotation. The body holds one text per new sign, separated by
U+001D. The copies must fit one stack, and the multiplayer limits hold for the whole stack.

| Parameter | Type | Default | |
|---|---|---|---|
| `id` | string, **required** | | the sign to stack on |
| `step` | metres | 0 | between copies: 0 is the window's spacing, else 0.0003..0.002 |

```json
{"ok": true, "ids": ["1234567890:43", "1234567890:44"]}
```

<a id="post-signsdelete"></a>
### 🟩 `POST /signs/delete`

Removes signs, without a drop. Ids that are not loaded are skipped.

| Parameter | Type | |
|---|---|---|
| `ids` | string, **required** | comma-separated sign ids |

```json
{"ok": true, "deleted": 2}
```

## Text animation

Frames of sign text, all signs changing together on one clock. Players without SignLink see at most one frame every
2 s (the game's own pace); SignLink players follow faster.

<a id="post-anim"></a>
### 🟩 `POST /anim`

The body holds the frames separated by U+001E; inside a frame, one text per sign separated by U+001D, in the order of
`ids`. With one sign a frame is its whole text.

| Parameter | Type | Default | |
|---|---|---|---|
| `ids` | string | | comma-separated sign ids (`id` for one sign) |
| `fps` | number, up to 60 | 10 | frames a second |
| `mode` | `local` \| `zdo` | `local` | `zdo` saves and syncs every frame (at most 16 KB/s in a shared world) |
| `loop` | 0 \| 1 | 1 | |
| `store` | 0 \| 1 | 0 | save the frames on the signs instead of playing them from here |

With `store=1` every SignLink player plays the animation on the world clock, after restarts too, and the sign's text
holds the first frame for everyone else. One stored animation a stack, text and frames under 400 KB a sign.

A clock on two signs, in Python:

```python
import os, time, urllib.request

PW, BASE = os.environ["PW"], "http://127.0.0.1:8417"
FRAME, SIGN = "\u001e", "\u001d"

def post(path, body):
    req = urllib.request.Request(BASE + path, data=body.encode(), headers={"X-SignLink-Password": PW})
    return urllib.request.urlopen(req).read()

while True:
    now = time.localtime()
    texts = [time.strftime("<size=200%>%H:%M</size>", now), time.strftime("%A", now)]
    post("/anim?ids=1234567890:42,1234567890:43&fps=1&loop=0&mode=zdo", SIGN.join(texts))
    time.sleep(60 - now.tm_sec)
```

Answer while playing, and with `store=1`:

```json
{"ok": true, "signs": 2, "frames": 1, "chars": 31}
{"ok": true, "stored": [{"ok": true, "frames": 120, "kb": 34}]}
```

<a id="get-anim"></a>
### 🟦 `GET /anim`

The animations playing from here and the number of stored ones playing.

```json
{"ok": true, "anims": [{"ids": "1234567890:42,1234567890:43", "mode": "zdo", "frames": 1, "fps": 1, "shown": 12,
  "realFps": 1, "skipped": 0, "writes": 12, "kbPerSec": 0.01, "avgMs": 0.3, "maxMs": 0.9}], "stored": 0}
```

<a id="post-animstop"></a>
### 🟩 `POST /anim/stop`

Stops the animations played from here; `id` stops only that sign's. Answers `{"ok": true, "stopped": 1}`.

<a id="post-animunstore"></a>
### 🟩 `POST /anim/unstore`

Removes the stored animation from the sign `id`. Answers `{"ok": true, "removed": true}`.

## Screens

Raw RGB video over a sign: a texture on the sign's canvas, one upload a frame, smooth at any rate. Only SignLink
players see it and it is not saved; everyone else sees the sign's own text. Over a stacked picture, players without the
mod see the still and SignLink players the moving picture in the same place.

<a id="post-screen"></a>
### 🟩 `POST /screen`

The body holds whole frames of `w` × `h` × 3 bytes (RGB), rows top-down, one after the other.

| Parameter | Type | Default | |
|---|---|---|---|
| `id` | string, **required** | | the sign |
| `w`, `h` | integer, 1..4096, **required** | | frame size in pixels |
| `fps` | number, up to 60 | 10 | |
| `cx`, `cy` | sign units (2.5 cm) | 0 | the picture's centre from the sign's centre, right and up |
| `pitch` | sign units | 2 | per pixel |
| `loop` | 0 \| 1 | 1 | |
| `hide` | 0 \| 1 | 1 | hide the whole stack's text while it shows |
| `filter` | `point` \| other | `point` | `point` keeps hard pixels, anything else smooths them |
| `lit` | 0 \| 1 | 0 | 1 is shaded by the scene, 0 glows |

```sh
ffmpeg -i clip.mp4 -vf scale=160:90 -f rawvideo -pix_fmt rgb24 clip.rgb
curl -s -H "X-SignLink-Password: $PW" --data-binary @clip.rgb "http://127.0.0.1:8417/screen?id=$ID&w=160&h=90&fps=24"
```

```json
{"ok": true, "frames": 240, "w": 160, "h": 90, "mb": 9.89, "lit": false}
```

<a id="get-screens"></a>
### 🟦 `GET /screens`

```json
{"ok": true, "screens": [{"id": "1234567890:42", "w": 160, "h": 90, "frames": 240, "fps": 24, "shown": 1200,
  "realFps": 24, "paused": false, "skipped": 0, "avgMs": 0.12, "maxMs": 0.5, "mb": 9.89, "lit": false}]}
```

<a id="post-screenstop"></a>
### 🟩 `POST /screen/stop`

Stops the screens; `id` stops only that sign's. A moving GIF's screen stopped here is not started again. Answers
`{"ok": true, "stopped": 1}`.

## Errors

Every error is `{"ok": false, "error": "..."}`; the message says what to change.

| Status | When |
|---|---|
| 400 | a wrong or missing parameter, a sign that is not loaded, a ward you cannot access, a size limit |
| 401 | the password header is missing or wrong |
| 403 | the Host header does not name this computer |
| 404 | an unknown route |
| 411 | a body without a Content-Length (chunked bodies are refused) |
| 413 | a body over the route's cap, or a body on a route that takes none |
| 429 | five wrong passwords in a minute from this address (another computer's; this one is never locked out): wait a minute |

## Limits

| | |
|---|---|
| Body size | `/sign`, `/anim`, `/signs/clone` 16 MB; `/image` 64 MB; `/screen` 256 MB |
| Timing | the first byte within 2 s, the head within 10 s (3 s from another computer), the body within 30 s (or 2 MB a second when larger) |
| One sign | 400 KB (the network carries at most 512 KiB a sign) |
| Shared world | 300 KB a sign, 600 KB for all SignLink signs within 100 m, synced animations 16 KB/s |
| Animations | 20 000 frames, 60 fps |

## Security

- The API is off by default. It listens on this computer only; with *network access* on it also answers local
  networks (home, Tailscale), never the internet. HTTP is not encrypted: use it only on a network you trust.
- Every request but `GET /`, `GET /ping` and `GET /favicon.ico` needs the password. You can set your own in the config:
  at least 12 characters, plain ASCII, no spaces.
- It touches signs and nothing else, and it obeys the window's rules: wards and the size limits. A leaked password
  still lets its holder rewrite or remove any loaded sign no ward protects: keep it private.

The full security model is in [`mod/README.md`](../mod/README.md#security).
