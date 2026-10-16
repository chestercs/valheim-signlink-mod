#!/usr/bin/env python3
"""The Thunderstore icon (256 x 256): a wooden sign on a post, carrying a pixel picture of a fjord at dusk.
Drawn at 32 x 32 and scaled up 8 times without smoothing, like the pixel art it advertises.

  make_icon.py OUT.png
"""
import sys

from PIL import Image

W = H = 32
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
px = img.load()


def rect(x0, y0, x1, y1, c):
    for y in range(y0, y1):
        for x in range(x0, x1):
            px[x, y] = c


# the post and the board's frame
rect(14, 22, 18, 31, (92, 62, 38, 255))
rect(15, 22, 16, 31, (118, 82, 50, 255))
rect(2, 4, 30, 24, (74, 48, 28, 255))
rect(3, 5, 29, 23, (110, 74, 42, 255))

# the picture on the board: sky, sun, mountains, fjord
sky = [(28, 30, 70), (52, 40, 96), (110, 56, 110), (180, 80, 96), (236, 132, 80), (250, 190, 96)]
for y in range(6, 22):
    c = sky[min(len(sky) - 1, (y - 6) * len(sky) // 10)]
    rect(4, y, 28, y + 1, c + (255,))
rect(19, 9, 23, 13, (255, 226, 140, 255))
rect(20, 8, 22, 14, (255, 226, 140, 255))
for x in range(4, 28):
    peak = 16 - max(0, 6 - abs(x - 10)) - max(0, 4 - abs(x - 24) // 1)
    for y in range(peak, 19):
        shade = (40, 46, 66) if (x + y) % 5 else (52, 58, 82)
        px[x, y] = shade + (255,)
    if peak <= 12:
        px[x, peak] = (230, 236, 246, 255)
rect(4, 19, 28, 22, (38, 70, 120, 255))
for x in range(5, 28, 3):
    px[x, 20] = (250, 190, 96, 255)

# rivets
for x, y in ((3, 5), (28, 5), (3, 22), (28, 22)):
    px[x, y] = (200, 170, 110, 255)

img.resize((256, 256), Image.NEAREST).save(sys.argv[1])
print(sys.argv[1], "256 x 256")
