"""The art library's second round of cats: the pixel cat's family at home in a Viking hall (concepts/anim2.py,
concepts/lib_pets.py), peeking out of a basket and a barrel, napping on a shield, playing, hunting and nursing.

Drawn like the cat: numpy masks on a Canvas at the cat's pitch, outlined automatically in the cat's dark outline,
so a kitten is small next to it. Every piece moves; the first frame is a good still for players without the mod.
Empty pixels are not written: the sprites stand on their own over whatever is behind.
"""
import numpy as np

import artlib as A
from concepts import anim2
from concepts.lib_pets import _chain, _paste, _rell, _sprite, _wave, _zee


# ---- a front-facing cat like anim2's, with ears that flick and eyes that look about --------------------------

def head(c, hx, hy, s=1.0, eye="G", eyes="open", look=(1, 0), ears=(0, 0), tabby=False, mouth="w"):
    """The cat's head (anim2) around (hx, hy) at scale s. Keys: F fur, N nose and inner ears, W muzzle, P pupil,
    D tabby marks, K lines, T tongue. eyes: open, half, closed, happy; look (dx, dy): the pupils' side (-1 left,
    1 right) and -1 up; ears (left, right): 1 flicks that ear out and down; mouth: w, open (a yawn), yawn (wide),
    or a tongue licking: tl, tc, tr."""
    for side, f in ((-1, ears[0]), (1, ears[1])):
        tip = (hx + side * (6.5 + 1.8 * f) * s, hy - (8.9 - 3.0 * f) * s)
        c.paint(c.triangle((hx + side * 7.5 * s, hy - 2.5 * s), tip, (hx + side * 1.9 * s, hy - 5.3 * s)), "F")
    c.paint(c.ellipse(hx, hy, 7.6 * s, 6 * s), "F")
    for side, f in ((-1, ears[0]), (1, ears[1])):
        tip = (hx + side * (5.9 + 1.5 * f) * s, hy - (7.3 - 2.5 * f) * s)
        c.paint(c.triangle((hx + side * 6.3 * s, hy - 4.3 * s), tip, (hx + side * 3.5 * s, hy - 5.3 * s)), "N")
    x0, yc = int(np.floor(hx)), int(np.floor(hy))           # the head's two middle columns are x0 and x0 + 1
    if tabby:
        c.paint(c.rect(x0 - 1, yc - 5, x0 + 3, yc - 4), "D")
        c.set(x0 - 2, yc - 4, "D").set(x0 + 3, yc - 4, "D").set(x0, yc - 3, "D").set(x0 + 1, yc - 3, "D")
    c.paint(c.ellipse(hx, hy + 2.7 * s, 3.4 * s, 2.3 * s), "W")
    ey = yc - 2
    for x in (x0 - 4, x0 + 3):
        if eyes in ("open", "half"):
            c.paint(c.rect(x, ey, x + 2, ey + 3), eye)
            px = x if look[0] < 0 else x + 1
            py = ey if look[1] < 0 else ey + 1
            c.set(px, py, "P").set(px, py + 1, "P")
            if eyes == "half":
                c.set(x, ey, "K").set(x + 1, ey, "K")
            else:
                c.set(2 * x + 1 - px, ey if look[1] >= 0 else ey + 2, "W")      # catchlight
        elif eyes == "happy":                                   # closed, smiling: a small arch
            c.set(x, ey + 2, "K").set(x + 1, ey + 1, "K").set(x + 2, ey + 2, "K")
        else:
            c.set(x, ey + 2, "K").set(x + 1, ey + 2, "K")
    c.set(x0, yc + 1, "N").set(x0 + 1, yc + 1, "N")
    if mouth == "open":
        c.paint(c.rect(x0 - 1, yc + 2, x0 + 3, yc + 4), "K")
        c.paint(c.rect(x0, yc + 3, x0 + 2, yc + 4), "T")
        c.set(x0 - 1, yc + 2, "W").set(x0 + 2, yc + 2, "W")    # two small fangs
    elif mouth == "yawn":                                       # wide open, the head thrown back
        jaw = c.ellipse(x0 + 1, yc + 4.4, 2.9, 2.9)
        c.paint(jaw, "K")
        c.paint(jaw & c.ellipse(x0 + 1, yc + 6.4, 2.2, 1.6), "T")
        c.set(x0 - 1, yc + 2, "W").set(x0 + 2, yc + 2, "W").set(x0 - 1, yc + 3, "W").set(x0 + 2, yc + 3, "W")
    else:
        c.set(x0 - 1, yc + 2, "K").set(x0 + 2, yc + 2, "K").set(x0, yc + 3, "K").set(x0 + 1, yc + 3, "K")
        tongue = {"tl": ((x0 - 1, yc + 3), (x0 - 2, yc + 3)), "tc": ((x0, yc + 4), (x0 + 1, yc + 4)),
                  "tr": ((x0 + 2, yc + 3), (x0 + 3, yc + 3))}.get(mouth, ())
        for x, y in tongue:
            c.set(x, y, "T")
    return x0, yc


def sit(c, cx, by, s=1.0, tail=0.0, stripes=True):
    """anim2's sitting body (bottom at by, centred on cx) with its curling tail (tail -1..1 swings it); the head
    goes at (cx, by - 14.5 s)."""
    c.paint(_chain(c, cx + 5.9 * s, by - 2.8 * s, 10 + 30 * tail, 13, 0.65 * s, 12, 1.25 * s), "F")
    c.paint(c.ellipse(cx, by - 6.5 * s, 6.3 * s, 6 * s), "F")
    if stripes:
        for dy in (9, 7):
            y = int(round(by - dy * s))
            c.paint(c.rect(int(round(cx - 5.5 * s)), y, int(round(cx - 3.5 * s)), y + 1), "D")
            c.paint(c.rect(int(round(cx + 4.5 * s)), y, int(round(cx + 6.5 * s)), y + 1), "D")
    c.paint(c.ellipse(cx, by - 4.8 * s, 3.2 * s, 3.6 * s), "W")
    pw = max(2, int(round(3 * s)))
    c.paint(c.rect(int(round(cx - 4.5 * s)), int(by) - 2, int(round(cx - 4.5 * s)) + pw, int(by)), "W")
    c.paint(c.rect(int(round(cx + 1.5 * s)), int(by) - 2, int(round(cx + 1.5 * s)) + pw, int(by)), "W")
    return cx, by - 14.5 * s


def _bordered(c, mask, key):
    """Paint mask in key with a dark rim of its own (the cat's outline), so it reads over fur behind it."""
    pad = np.pad(mask, 1)
    grown = mask | pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    c.paint(grown, "K")
    c.paint(mask, key)


# ---- 1. kitten in a basket ------------------------------------------------------------------------------------

BASKET = {"F": "#dba", "D": "#b86", "W": "#fff", "G": "#5be", "P": "#111", "N": "#f8a", "T": "#e57",
          "B": "#c94", "b": "#963", "R": "#742"}


def basket_kitten(k):
    """A fawn tabby kitten peeks over the rim of a wicker basket, paws on the edge: an ear twitches, it looks
    about, blinks, and stretches up to peek higher."""
    ears, look, eyes, bob = [((0, 0), (1, 0), "open", 0), ((1, 0), (1, 0), "open", 0),
                             ((0, 0), (-1, 0), "open", 0), ((0, 1), (-1, 0), "open", 0),
                             ((0, 0), (1, 0), "closed", 0), ((0, 0), (1, -1), "open", -1)][k]
    c = A.Canvas(30, 25)
    c.paint(c.ellipse(15, 17, 12.8, 15.5) & ~c.ellipse(15, 17, 11.4, 14.1) & (c.py < 16), "R")     # the handle
    c.paint(c.ellipse(15, 16, 11.5, 2.0), "b")                                     # the inside, behind
    head(c, 15, 11.6 + bob, 0.8, eye="G", eyes=eyes, look=look, ears=ears, tabby=True)
    body = (np.abs(c.px - 15) <= 12.6 - (c.py - 16) * 0.45) & (c.py > 16.5)
    body &= ~((c.py > 24) & (np.abs(c.px - 15) > 7.5))
    weave = ((c.xx // 3 + c.yy // 2) % 2) == 0
    c.paint(body & weave, "B").paint(body & ~weave, "b")
    c.paint(c.ellipse(15, 16.6, 12.8, 1.4), "R")                                   # the rim
    c.paint(c.ellipse(11, 16.2, 1.9, 1.3) | c.ellipse(19, 16.2, 1.9, 1.3), "W")    # paws on the edge
    return A.outline(c.g, "K")


# ---- 2. cat and butterfly -------------------------------------------------------------------------------------

BFLY_CAT = {"F": "#b85", "D": "#864", "W": "#fff", "G": "#5d5", "P": "#111", "N": "#f8a", "T": "#e57",
            "Y": "#fd3", "y": "#e72", "b": "#213"}
BFLY_OPEN = ["YYY.YYY", "YyYbYyY", ".YYbYY.", ".yY.Yy.", "..Y.Y.."]
BFLY_SHUT = ["..Y.Y..", ".YY.YY.", "..YbY..", "...b...", "......."]
BFLY_PATH = [(32, 6, 1), (36, 10, 0), (37, 16, 1), (33, 19, 0), (28, 16, 1), (26, 9, 0), (29, 5, 1), (31, 3, 0)]


def butterfly_cat(k):
    """A brown tabby cat sits and watches a yellow butterfly flutter round it: its eyes follow, then it swipes a
    paw up as the butterfly dips close, and the butterfly flutters away up."""
    bx, by, wings = BFLY_PATH[k]
    c = A.Canvas(42, 34)
    cx, hy = sit(c, 12, 34, tail=_wave(8, k) * 0.6)
    look = (1, -1 if by < 12 else 0)
    head(c, cx + (1 if bx >= 30 else 0), hy, eye="G", look=look, eyes="open")
    paw = {4: (19.8, 20.2), 5: (21.4, 15.6)}.get(k)
    if paw:                                                                        # the swiping paw
        px, py = paw
        _bordered(c, c.line(15.4, 25, px, py, 2.4) | c.disc(px, py, 1.5), "F")
        c.paint(c.disc(px, py, 1.3), "W")
    g = A.outline(c.g, "K")
    fly = A.outline(A.ascii(BFLY_OPEN if wings else BFLY_SHUT, {"Y": 1, "y": 1, "b": 1}), "K")
    return _paste(g, fly, bx - 4, by - 3)


# ---- 3. two kittens playing -----------------------------------------------------------------------------------

KITS = {"F": "#aab", "D": "#778", "W": "#fff", "G": "#5be", "P": "#111", "N": "#f8a", "T": "#e57",
        "f": "#e73", "d": "#b42", "g": "#5d5", "n": "#fa9", "w": "#fed", "S": "#fe8", "C": "#dde", "c": "#aab"}


def kit(pose="sit", eyes="open", look=(1, 0), tail=0.0, ginger=False):
    """A small kitten (anim2's cat at 0.72) seen from the front, outlined: sit, crouch (low, ready to pounce) or
    leap (stretched, front paws up)."""
    c = A.Canvas(18, 18)
    if pose == "sit":
        cx, hy = sit(c, 9, 18, 0.72, tail=tail)
    elif pose == "crouch":
        c.paint(_chain(c, 13, 14, 75 + 20 * tail, 8, 0.7, -8, 0.95), "F")           # the tail up, twitching
        c.paint(c.ellipse(9, 14.6, 6.4, 3.4), "F")
        c.paint(c.rect(4, 16, 7, 18), "W").paint(c.rect(11, 16, 14, 18), "W")
        cx, hy = 9, 10.5
    else:                                                                           # leaping, paws reaching ahead
        c.paint(_chain(c, 5, 12.5, 160, 6, 0.8, -14, 0.95), "F")
        c.paint(c.line(7, 14, 3, 16.5, 2.2), "F")
        c.paint(c.disc(2.6, 16.6, 1.2), "W")
        c.paint(_rell(c, 9.5, 12.5, 5.4, 3.8, -15), "F")
        c.paint(_rell(c, 10, 13.8, 2.8, 1.8, -15), "W")
        c.paint(c.line(12, 13, 16, 15.5, 2.2) | c.line(13, 11, 16.5, 11.5, 2.2), "F")
        c.paint(c.disc(16.3, 15.6, 1.2) | c.disc(16.6, 11.5, 1.2), "W")
        cx, hy = 8, 7.4
    head(c, cx, hy, 0.72, eye="G", eyes=eyes, look=look, tabby=True)
    g = c.g
    if ginger:
        g = np.vectorize(lambda v: {"F": "f", "D": "d", "G": "g", "N": "n", "W": "w"}.get(v, v), otypes=[object])(g)
    return A.outline(g, "K")


def tumble(k):
    """The two kittens rolling in a cartoon dust cloud: paws, ears and tails poke out, a few stars fly."""
    c = A.Canvas(26, 22)
    cl = c.disc(13, 12, 7.5)
    for x, y, r in ((6.5, 13, 4.5), (19.5, 13, 4.5), (9, 7, 4), (17, 7.5, 4), (13, 17, 4.5)):
        cl |= c.disc(x + (0.6 if k else -0.6), y, r)
    # bits sticking out: (grey or ginger) ears, a paw, a tail
    if k == 0:
        c.paint(c.triangle((6, 4.5), (5, 0.2), (9.2, 3)), "F")
        c.paint(_chain(c, 21, 15, 10, 6, 0.8, 14, 1.0), "f")
        c.paint(c.disc(3.2, 18, 1.5), "W").paint(c.disc(22.5, 6, 1.5), "w")
    else:
        c.paint(c.triangle((17, 3.5), (19.5, 0.2), (20.5, 4.5)), "f")
        c.paint(_chain(c, 5.5, 15, 170, 6, 0.8, -14, 1.0), "F")
        c.paint(c.disc(22.5, 18.5, 1.5), "w").paint(c.disc(3.5, 7, 1.5), "W")
    c.paint(cl, "C")
    c.paint(cl & ~c.disc(12.5, 11.5, 7.2) & (c.py > 12), "c")                      # the cloud's shaded underside
    for x, y in ([(9, 9), (16, 13)], [(15, 8), (8, 14)])[k]:                       # a face in the scuffle
        c.set(x, y, "K").set(x + 2, y, "K").set(x + 1, y + 1, "N")
    g = A.outline(c.g, "K")
    for x, y in ([(1, 2), (26, 3), (0, 12)], [(25, 1), (2, 3), (27, 11)])[k]:
        g[y, x] = "S"
    return g


def kittens(k):
    """Two kittens play: the grey one crouches with its tail up and pounces on the ginger one, they tumble over
    in a dust cloud, then sit up again side by side."""
    g = np.full((30, 48), "", dtype=object)
    if k == 0:
        _paste(g, kit(look=(1, 0)), 2, 10)
        _paste(g, kit(look=(-1, 0), ginger=True, tail=0.5), 28, 10)
    elif k == 1:
        _paste(g, kit("crouch", look=(1, 0), tail=1), 3, 10)
        _paste(g, kit(look=(-1, 0), ginger=True, tail=-0.5), 28, 10)
    elif k == 2:
        _paste(g, kit(look=(-1, -1), ginger=True), 28, 10)
        _paste(g, kit("leap", look=(1, 0)), 13, 2)
    elif k in (3, 4):
        _paste(g, tumble(k - 3), 18, 6)
    elif k == 5:
        _paste(g, kit(eyes="happy"), 12, 10)
        _paste(g, kit(eyes="happy", ginger=True), 28, 10)
    elif k == 6:
        _paste(g, kit(eyes="happy", tail=-0.5), 6, 10)
        _paste(g, kit(look=(-1, 0), ginger=True, tail=0.5), 28, 10)
    else:
        _paste(g, kit(look=(1, 0), tail=0.5), 2, 10)
        _paste(g, kit(eyes="closed", ginger=True), 28, 10)
    return g


# ---- 4. cat loaf ----------------------------------------------------------------------------------------------

LOAF = {"F": "#445", "H": "#778", "W": "#fff", "E": "#cd4", "P": "#111", "N": "#d79", "T": "#e57"}


def loaf(k):
    """A tuxedo cat sits as a loaf, a rounded body with every paw tucked out of sight and its head on top at the
    front, its tail wrapped along the bottom: it closes its eyes very slowly and opens them again (a cat's slow
    blink), and the tip of its tail twitches."""
    eyes = ["open", "open", "half", "closed", "closed", "half", "open", "open"][k]
    tip = [0, 0, 1, 0, 0, 0, -1, 0][k]
    c = A.Canvas(34, 23)
    body = (np.abs(c.px - 19) / 13.5) ** 2.4 + (np.abs(c.py - 16) / 6.6) ** 2.4 <= 1
    body &= c.py < 22
    c.paint(body, "F")
    c.paint(body & np.vstack([np.ones((1, c.w), bool), ~body[:-1]]) & (c.px > 15), "H")   # a light rim on the back
    c.paint(body & c.ellipse(11.5, 17.8, 3.0, 3.4), "W")                              # the white bib
    hd = A.Canvas(34, 23)                                                           # the head, outlined over the body
    head(hd, 11.5, 10.6, 0.9, eye="E", eyes=eyes, look=(0, 0))
    _bordered(c, hd.g != "", "F")
    c.g[hd.g != ""] = hd.g[hd.g != ""]
    tail = _chain(c, 31.5, 20.0, 178, 9, 1.0, -0.5, 1.2, 1.1)                       # wrapped along the bottom
    tail |= _chain(c, 23.6, 20.0, 172 - 25 * tip, 4, 0.9, -9, 1.05, 0.95)           # the tip, flicking
    _bordered(c, tail, "F")
    return A.outline(c.g, "K")


# ---- 5. cat with a fish ---------------------------------------------------------------------------------------

FISH_CAT = {"F": "#89a", "D": "#678", "W": "#fff", "G": "#fc3", "P": "#111", "N": "#f8a", "T": "#e57",
            "A": "#596", "a": "#cdb", "f": "#374", "r": "#d54"}


def fish_cat(k):
    """A blue-grey cat sits by the fish it caught and licks its lips, tongue from corner to corner, eyes shut
    happily; the fish's tail flips once."""
    eyes, look, mouth, flip = [("open", (1, 0), "w", 0), ("happy", (1, 0), "tl", 0), ("happy", (1, 0), "tc", 0),
                               ("happy", (1, 0), "tr", 0), ("closed", (1, 0), "w", 1), ("open", (1, 0), "w", 0)][k]
    c = A.Canvas(38, 25)
    fx, fy = 28, 21.4                                                              # the fish lying on its side
    c.paint(c.triangle((fx + 5, fy), (fx + 9.5, fy - 3.2 - 1.5 * flip), (fx + 9.5, fy + 3.2 - 1.5 * flip)), "f")
    c.paint(c.ellipse(fx, fy, 6.3, 2.8), "A")
    c.paint(c.ellipse(fx, fy + 1.2, 5.6, 1.6), "a")
    c.paint(c.triangle((fx - 1, fy - 2.2), (fx + 2.5, fy - 4.6), (fx + 3.5, fy - 2.2)), "f")       # the back fin
    c.paint(c.line(fx - 2.5, fy - 2.4, fx - 2.5, fy + 2.4, 0.8), "r")              # a red gill
    c.set(int(fx - 4.5), int(fy - 1), "K")
    cx, hy = sit(c, 11, 25, tail=0.4 * _wave(6, k))
    head(c, cx, hy, eye="G", eyes=eyes, look=look, mouth=mouth)
    return A.outline(c.g, "K")


# ---- 6. cat in a barrel ---------------------------------------------------------------------------------------

BARREL = {"F": "#eca", "D": "#d95", "W": "#fff", "G": "#5d5", "P": "#111", "N": "#f8a", "T": "#e57",
          "B": "#a63", "b": "#853", "H": "#667", "h": "#99a", "I": "#321", "R": "#c84"}


def barrel_cat(k):
    """A cream tabby cat peeks out of a wooden barrel bound in iron: it looks left and right, ducks down until
    only its ears show, flicks them, and comes up again."""
    hy, eyes, look, ears = [(9.5, "open", (1, 0), (0, 0)), (9.5, "open", (-1, 0), (0, 0)),
                            (12.5, "open", (1, -1), (0, 0)), (16.5, None, (0, 0), (1, 0)),
                            (16.5, None, (0, 0), (0, 1)), (12.5, "open", (-1, -1), (0, 0))][k]
    c = A.Canvas(26, 32)
    rim = c.ellipse(13, 13, 11.2, 2.6)
    c.paint(rim, "R")
    c.paint(c.ellipse(13, 13, 9.6, 1.6), "I")                                      # the dark inside
    head(c, 13, hy, eye="G", eyes=eyes or "open", look=look, ears=ears, tabby=True)
    staves = (np.abs(c.px - 13) <= 10.4 + 1.3 * np.sin(np.pi * np.clip((c.py - 13) / 19, 0, 1))) & (c.py > 13)
    staves &= c.py < 31.5
    c.paint(staves, "B")
    c.paint(staves & ((c.xx - 1) % 4 == 0), "b")                                  # the staves' seams
    for y0 in (15, 22, 28):
        c.paint(staves & (c.yy >= y0) & (c.yy < y0 + 2), "H")
        c.paint(staves & (c.yy == y0), "h")
    c.paint(rim & (c.py > 13), "R")                                                # the rim's front half
    return A.outline(c.g, "K")


# ---- 7. cat asleep on a shield --------------------------------------------------------------------------------

SHIELD_CAT = {"F": "#bbc", "D": "#778", "W": "#fff", "N": "#f8a", "Z": "#bdf",
              "I": "#99a", "i": "#556", "w": "#b84", "o": "#963"}


def shield_cat(k):
    """A silver tabby cat sleeps stretched out on a round Viking shield lying on the ground: its body rises and
    falls with each breath, small z marks float up."""
    ry = [4.6, 5.0, 5.3, 5.0][k]
    c = A.Canvas(48, 32)
    sx, sy = 24, 24.2
    c.paint(c.ellipse(sx, sy + 1.3, 22, 6.6), "i")                                 # the shield's edge below
    c.paint(c.ellipse(sx, sy, 22, 6.6), "I")                                       # the iron rim
    inner = c.ellipse(sx, sy, 20.4, 5.6)
    c.paint(inner & ((c.xx // 4) % 2 == 0), "w").paint(inner & ((c.xx // 4) % 2 == 1), "o")   # the planks
    bx, by = 27, 24.6 - ry                                                         # the body, breathing
    body = c.ellipse(bx, by, 11, ry)
    c.paint(body, "F")
    for x in (24, 28, 32):
        c.paint(body & c.line(x, by - ry, x - 1, by - ry + 2.5, 1.1), "D")
    haunch = c.ellipse(37.5, 21, 4.6, 3.6)
    _bordered(c, haunch & (c.px > 34), "F")                                        # the haunch
    c.paint(haunch, "F")
    c.paint(c.ellipse(41.5, 24.2, 2.8, 1.2), "W")                                  # a hind paw
    _bordered(c, _chain(c, 44, 20.5, 252, 12, 1.0, -6.5, 1.3, 1.0), "F")            # the tail round the front
    paws = c.ellipse(19.5, 24.4, 3.4, 1.3)                                         # front paws under the chin
    _bordered(c, paws, "W")
    hd = A.Canvas(48, 32)                                                          # the head, outlined over the body
    head(hd, 13.5, 19.6, 0.85, eyes="closed", tabby=True)
    _bordered(c, hd.g != "", "F")
    c.g[hd.g != ""] = hd.g[hd.g != ""]
    for st in (k % 4, (k + 2) % 4):
        if st < 3:
            x, y, size = [(9, 7, 4), (5, 3, 4), (1, 0, 4)][st]
            _zee(c, x, y, size, "Z")
    return A.outline(c.g, "K")


# ---- 8. cat watching a bird -----------------------------------------------------------------------------------

BIRD_CAT = {"F": "#e94", "D": "#b62", "S": "#c73", "W": "#fff", "w": "#dde", "E": "#5d5", "P": "#111", "N": "#f8a",
            "Bb": "#48c", "Bd": "#236", "By": "#fd5", "Bw": "#eef", "Bk": "#e82", "St": "#889", "st": "#667"}


def bird_cat(k):
    """A ginger and white cat stalks a little bird on a stone: crouched low and compact, shoulder blades up, head
    low and forward, eyes fixed on the bird, its tail held low behind with the tip hooking up and swishing; it
    wiggles its hindquarters ready to pounce, the bird pecks, hops up startled and settles again."""
    swish = _wave(8, k)
    wig = [0, 0, 0, 0, 1, 0, 1, 0][k]
    peck, hop = k in (1, 2), k == 6
    c = A.Canvas(60, 26)
    G = 25.6                                                                        # the ground
    # the tail, low behind: it sags from the rump toward the ground, then the tip hooks up and swishes
    tx, ty, ta = 13.4, 17.4 - wig, 200.0
    tail = np.zeros((c.h, c.w), bool)
    for i in range(13):
        tail |= c.disc(tx, ty, 1.3 - 0.25 * i / 12)
        ta += 5 if i < 6 else -18 - 6 * swish
        tx, ty = tx + np.cos(np.radians(ta)), ty - np.sin(np.radians(ta))
    c.paint(tail, "F")
    c.paint(tail & c.disc(tx, ty, 2.4), "D")                                        # the dark tip
    # far legs: the hind foot flat under the haunch, the far foreleg a step ahead
    c.paint(c.ellipse(19.0, G - 1.2, 3.6, 1.3), "S")
    c.paint(c.line(31.5, 20.5, 36.5, G - 1.2, 2.4) | c.ellipse(37.4, G - 1.1, 2.0, 1.1), "S")
    # body: haunch raised, the back dipping to the shoulder blades, the belly low
    c.paint(c.ellipse(17.5, 19.2 - wig, 5.0, 5.0), "F")
    c.paint(_rell(c, 24.5, 20.4, 8.4, 4.2, 5), "F")
    c.paint(c.ellipse(30.2, 17.2, 3.0, 2.9), "F")                                   # the shoulder blades, up
    c.paint(_rell(c, 25, 23.6, 6.5, 1.4, 4) & (c.g == "F"), "W")                    # the white belly
    for x in (18, 22, 26):                                                          # tabby stripes over the back
        c.paint(c.line(x, 15.0 - (wig if x < 21 else 0), x - 1, 18.6, 1.0) & (c.g == "F"), "D")
    hind = c.ellipse(21.8, G - 1.2, 4.2, 1.3)                                       # near hind foot, flat
    _bordered(c, hind, "F")
    c.paint(hind & (c.px > 23.5), "W")
    fore = c.line(32.0, 19.4, 31.0, G - 1.3, 2.8) | c.line(31.0, G - 1.3, 36.0, G - 1.2, 2.4)   # elbow up and
    fore |= c.ellipse(37.0, G - 1.1, 2.0, 1.1)                                      # back, the forearm flat
    _bordered(c, fore, "F")
    c.paint(c.ellipse(37.0, G - 1.1, 2.0, 1.1), "W")
    # the neck reaching forward and down, the head low, side on, ears pricked at the bird
    c.paint(c.line(31.5, 18.0, 36.5, 20.0, 4.6), "F")
    hx, hy = 39.0, 20.0
    c.paint(c.triangle((hx - 3.8, hy - 1.6), (hx - 2.0, hy - 6.4), (hx - 0.2, hy - 3.6)), "F")
    head_m = c.ellipse(hx, hy, 4.2, 3.6) | c.ellipse(hx + 2.8, hy + 1.2, 2.3, 1.8)
    _bordered(c, head_m, "F")
    c.paint(c.triangle((hx - 0.8, hy - 3.2), (hx + 1.6, hy - 7.0), (hx + 3.0, hy - 2.4)), "F")
    c.paint(c.triangle((hx + 0.1, hy - 3.1), (hx + 1.5, hy - 5.6), (hx + 2.3, hy - 2.8)), "N")
    c.paint(c.ellipse(hx + 2.6, hy + 1.8, 2.4, 1.4) & head_m, "W")                 # white muzzle and chin
    ex, ey = int(round(hx + 0.8)), int(round(hy - 1.6))
    c.paint(c.rect(ex, ey, ex + 2, ey + 2), "E")                                    # wide, fixed on the bird
    c.set(ex + 1, ey, "P").set(ex + 1, ey + 1, "P")
    c.set(int(hx + 4.6), int(hy + 0.2), "N")
    # the bird on its stone, facing away
    c.paint(c.ellipse(51.5, G - 2.0, 5.6, 2.4), "St")
    c.paint(c.ellipse(52.8, G - 1.0, 4.0, 1.2), "st")
    bx, by = 51.0, 17.6 - (4 if hop else 0)
    c.paint(c.line(bx - 2.4, by + 0.4, bx - 5.6, by - 1.6, 1.8), "Bd")             # tail, cocked up
    body = c.ellipse(bx, by, 3.6, 2.8)
    c.paint(body, "Bb")
    c.paint(body & c.ellipse(bx + 1.2, by + 1.4, 2.8, 1.8), "By")                  # yellow breast
    hdx, hdy = (bx + 3.4, by + 0.8) if peck else (bx + 2.6, by - 2.6)
    c.paint(c.disc(hdx, hdy, 2.1), "Bb")
    c.paint(c.disc(hdx, hdy, 2.1) & (c.py > hdy - 0.2) & (c.px < hdx + 1), "Bw")   # white cheek
    bk = (int(hdx) + 2, int(hdy) + (1 if peck else 0))
    c.set(*bk, "Bk").set(bk[0] + 1, bk[1], "Bk")                                    # the beak
    c.set(int(hdx), int(hdy) - 1, "K")
    if hop:
        c.paint(c.triangle((bx - 2.5, by - 0.5), (bx + 1.5, by - 0.5), (bx - 3, by - 5)), "Bd")   # wings up
        c.set(int(bx) - 1, int(by + 2.8), "Bk").set(int(bx) + 1, int(by + 2.8), "Bk")   # legs dangling
    else:
        c.paint(c.ellipse(bx - 0.8, by - 0.4, 2.4, 1.3) & body, "Bd")             # the folded wing
        for x in (int(bx) - 1, int(bx) + 1):                                        # little legs to the stone
            c.set(x, int(by + 2.8), "Bk").set(x, int(by + 3.8), "Bk")
    return A.outline(c.g, "K")


# ---- 9. mother cat and kittens --------------------------------------------------------------------------------

MOTHER = {"F": "#99a", "D": "#667", "W": "#fff", "G": "#fc3", "P": "#111", "N": "#f8a", "T": "#e57",
          "f": "#e83", "d": "#b52", "c": "#fed", "e": "#fff"}


def mother_cat(k):
    """A grey tabby mother cat lies on her side with two kittens, a ginger and a cream one, snuggled at her belly:
    the kittens wriggle and knead, their tails flick, and the mother blinks contentedly."""
    eyes = ["open", "happy", "happy", "happy", "open", "open"][k]
    c = A.Canvas(56, 30)
    G = 29.6                                                                       # the ground
    c.paint(_chain(c, 47, 15, 50, 11, 1.0, -15, 1.4, 1.1), "F")                     # the tail, curled over behind
    hind = c.line(44, 22, 50.5, G - 1.4, 3.0) | c.ellipse(51.5, G - 1.3, 2.4, 1.3)  # hind leg stretched out
    c.paint(hind, "F")
    body = c.ellipse(30.5, 20.5, 15.5, 7.6) | c.ellipse(42.5, 20.5, 6.6, 6.8)
    c.paint(body, "F")
    for x in (24, 29, 34, 39):                                                     # tabby stripes over the back
        c.paint(body & c.line(x, 12.5, x + 1, 16, 1.1), "D")
    c.paint(body & c.ellipse(30, 27.6, 13, 3.2), "W")                               # the pale belly
    c.paint(c.ellipse(51.5, G - 1.3, 2.4, 1.3) & ~body, "W")
    hip = c.ellipse(42.5, 20.5, 6.6, 6.8) & (c.px > 39)
    _bordered(c, hip & ~c.ellipse(41.5, 20.5, 6.0, 6.0), "F")                      # the haunch's edge
    c.paint(hip, "F")
    fore = c.line(20, 23.5, 11.5, G - 1.4, 2.8) | c.ellipse(10.2, G - 1.3, 2.4, 1.3)    # forelegs out in front
    _bordered(c, fore, "F")
    c.paint(c.ellipse(10.2, G - 1.3, 2.4, 1.3), "W")
    hd = A.Canvas(56, 30)                                                          # the head, outlined over the body
    head(hd, 13, 15.5, 0.92, eye="G", eyes=eyes, look=(1, 0), tabby=True)
    _bordered(c, hd.g != "", "F")
    c.g[hd.g != ""] = hd.g[hd.g != ""]
    for i, (kx, key, dkey) in enumerate(((24.5, "f", "d"), (35.5, "c", "D"))):        # the kittens, snuggled in
        ph = (k + 3 * i) % 6
        x = kx + [0, 0.5, 1, 0.5, 0, -0.5][ph]
        y = 24.6 - [0, 0.5, 1, 0.5, 0, 0][ph]
        tail = _chain(c, x + 6.4, y + 3.0, 10 + 45 * _wave(6, ph), 4, 0.9, 25, 1.0, 0.8)
        kit = c.ellipse(x + 2.8, y + 2.6, 4.4, 2.2)                                # the body lying beside it
        kit |= c.ellipse(x, y, 3.4, 2.9)                                           # the round head
        kit |= c.triangle((x - 3.3, y - 0.6), (x - 2.6, y - 4.4), (x - 0.6, y - 2.6))
        kit |= c.triangle((x + 3.3, y - 0.6), (x + 2.6, y - 4.4), (x + 0.6, y - 2.6))
        _bordered(c, kit | tail, key)
        ex, ey = int(np.floor(x)), int(np.floor(y))
        c.set(ex - 2, ey - 2, dkey).set(ex + 2, ey - 2, dkey)                      # ear insides
        c.set(ex - 2, ey, "K").set(ex - 1, ey, "K").set(ex + 1, ey, "K").set(ex + 2, ey, "K")   # eyes shut
        c.set(ex, ey + 1, "N")
        paw = ph in (1, 2)                                                         # kneading: a paw up and down
        c.paint(c.ellipse(x - 2.0, y + 2.6 - paw, 1.2, 0.9), "W")
    return A.outline(c.g, "K")


# ---- 10. the ginger cat in a Viking helmet --------------------------------------------------------------------

VIKING = {**{k: v for k, v in anim2.PAL.items() if k != "K"}, "Hm": "#99a", "Hh": "#dde", "Hd": "#667",
          "Hb": "#c93", "Hr": "#eed", "hr": "#cb9"}


def viking_cat(k):
    """The ginger cat (anim2) in a little iron helmet with a bronze band, a nose guard and two horns: it nods
    proudly, shutting its eyes at the bottom of the nod, and swings its tail."""
    t = k / 6
    nod = [0, 0, 1, 1, 0, 0][k]
    cat = anim2.frame(t, blink=nod == 1)                                           # 26 x 30, outlined
    if nod:                                                                        # the head dips one pixel
        top = cat[0:14, 2:22].copy()
        cat[0, 2:22] = ""
        cat[1:15, 2:22] = top
    g = np.full((32, 36), "", dtype=object)
    _paste(g, cat, 3, 6)
    h = A.Canvas(36, 32)
    y = 6 + nod                                                                    # the cat's row 0 on the grid
    cx = 3 + 12.5
    for side in (-1, 1):                                                           # horns, curving up and out
        hm = _chain(h, cx + side * 6.8, y + 3.2, 90 - side * 45, 5, 0.9, side * 16, 1.2, 0.7)
        h.paint(hm, "Hr")
        h.paint(hm & (h.py > y + 1.6), "hr")
    dome = h.ellipse(cx, y + 7.5, 7.6, 7.2) & (h.py < y + 6)
    h.paint(dome, "Hm")
    h.paint(dome & h.ellipse(cx - 2.4, y + 2.6, 2.2, 1.7), "Hh")
    h.paint(dome & (h.px > cx + 4.5), "Hd")
    h.paint(h.rect(int(cx - 7.5), y + 5, int(cx + 8.5), y + 7), "Hb")             # the band
    h.paint(h.rect(int(cx), y + 7, int(cx) + 1, y + 10), "Hb")                    # the nose guard
    h.set(int(cx) - 5, y + 5, "Hr").set(int(cx), y + 5, "Hr").set(int(cx) + 5, y + 5, "Hr")      # rivets
    _paste(g, A.outline(h.g, "K")[1:-1, 1:-1], 0, 0)
    return g


# ---- extras: a yawning cat, a kitten in a mead mug ------------------------------------------------------------

YAWN = {"F": "#bab", "D": "#878", "W": "#fff", "G": "#fa3", "P": "#111", "N": "#f8a", "T": "#e57"}


def yawning_cat(k):
    """A lilac-grey cat sits and yawns: the mouth opens wide with its eyes squeezed shut, then it smacks its lips
    and looks sleepily on."""
    eyes, mouth, up = [("open", "w", 0), ("half", "w", 0), ("closed", "open", -1), ("closed", "yawn", -1),
                       ("closed", "open", -1), ("happy", "tc", 0)][k]
    c = A.Canvas(28, 26)
    cx, hy = sit(c, 11.5, 26, tail=0.6 * _wave(6, k))
    head(c, cx, hy + up, eye="G", eyes=eyes, look=(0, 0), tabby=True, mouth=mouth)
    return A.outline(c.g, "K")


MUG = {"F": "#999", "D": "#666", "W": "#fff", "G": "#5be", "P": "#111", "N": "#f8a", "T": "#e57",
       "M": "#a63", "m": "#742", "H": "#99a", "h": "#556", "O": "#fff", "o": "#ec8"}


def mug_kitten(k):
    """A grey kitten sits inside a wooden mead tankard, foam on its head: it blinks, looks round and twitches
    its ears."""
    ears, look, eyes = [((0, 0), (1, 0), "open"), ((0, 0), (-1, 0), "open"), ((0, 1), (-1, 0), "open"),
                        ((0, 0), (1, 0), "closed")][k]
    c = A.Canvas(26, 27)
    c.paint(c.ellipse(21.5, 17.5, 4.2, 5) & ~c.ellipse(21.5, 17.5, 2.3, 3.2), "m")   # the handle
    c.paint(c.ellipse(11, 12.5, 9, 1.8), "m")
    head(c, 11, 9.6, 0.78, eye="G", eyes=eyes, look=look, ears=ears)
    mug = c.rect(2, 13, 20, 27)
    c.paint(mug, "M")
    c.paint(mug & ((c.xx - 2) % 4 == 3), "m")                                     # staves
    for y0 in (15, 23):
        c.paint(mug & (c.yy >= y0) & (c.yy < y0 + 2), "H")
        c.paint(mug & (c.yy == y0 + 1), "h")
    c.paint(c.ellipse(11, 13, 9.6, 1.5), "o")                                     # the foam at the rim
    c.paint(c.disc(4, 14, 1.4) | c.disc(17.5, 14.4, 1.2), "O")
    c.paint(c.ellipse(9, 13.4, 2, 1.1) | c.ellipse(14, 13.6, 2, 1.1), "W")        # paws on the rim
    return A.outline(c.g, "K")


# ---- pieces ---------------------------------------------------------------------------------------------------

def _frames(fn, n, pal):
    return [_sprite(fn(k), pal) for k in range(n)]


def pieces():
    return [
        A.Piece("Basket kitten (moving, peeking)", "pixel", frames=_frames(basket_kitten, 6, BASKET), fps=3),
        A.Piece("Cat and butterfly (moving, swiping)", "pixel", frames=_frames(butterfly_cat, 8, BFLY_CAT), fps=5),
        A.Piece("Kittens at play (moving, pouncing)", "pixel", frames=_frames(kittens, 8, KITS), fps=4),
        A.Piece("Cat loaf (moving, slow blink)", "pixel", frames=_frames(loaf, 8, LOAF), fps=3),
        A.Piece("Cat with fish (moving, licking lips)", "pixel", frames=_frames(fish_cat, 6, FISH_CAT), fps=4),
        A.Piece("Barrel cat (moving, peeking)", "pixel", frames=_frames(barrel_cat, 6, BARREL), fps=3),
        A.Piece("Shield cat (moving, asleep)", "pixel", frames=_frames(shield_cat, 4, SHIELD_CAT), fps=2),
        A.Piece("Cat and bird (moving, stalking)", "pixel", frames=_frames(bird_cat, 8, BIRD_CAT), fps=4),
        A.Piece("Mother cat (moving, kittens snuggling)", "pixel", frames=_frames(mother_cat, 6, MOTHER), fps=3),
        A.Piece("Viking cat (moving, proud nod)", "pixel", frames=_frames(viking_cat, 6, VIKING), fps=4),
        A.Piece("Yawning cat (moving)", "pixel", frames=_frames(yawning_cat, 6, YAWN), fps=3),
        A.Piece("Mead mug kitten (moving, peeking)", "pixel", frames=_frames(mug_kitten, 4, MUG), fps=3),
    ]
