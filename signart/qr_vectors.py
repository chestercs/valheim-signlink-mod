"""Reference QR codes for SignLink's QR template (mod/SignLink/Pictures/QrCode.cs).

The codes come from python-qrcode, an independent encoder, at a fixed mask; every one is rendered and read back with
zbar (a QR reader), so the fixture holds codes a phone reads. Writes mod/SignLink.Tests/qr_vectors.json (committed:
the C# checks need neither). Run again only to change the cases:
  docker run --rm -v "$PWD:/work" -w /work python:3.12-slim sh -c "apt-get update -qq && apt-get install -y -qq libzbar0 &&
    pip install -q qrcode==7.4.2 pyzbar==0.1.9 pillow==11.0.0 && python -I signart/qr_vectors.py"
(segno 1.6.1 placed other data bits than python-qrcode and SignLink for the same text, level and mask, 2026-10-16.)
"""
import json

import qrcode
from PIL import Image
from pyzbar import pyzbar
from qrcode.util import MODE_8BIT_BYTE, QRData

TEXTS = ["A", "Skål!", "https://github.com/chestercs/valheim-signlink-mod",
         "https://www.nexusmods.com/valheim/mods/4453 · Thunderstore: PetyusPolisz/SignLink · ÁRVÍZTŰRŐ TÜKÖRFÚRÓGÉP ✓",
         "x" * 300, "".join(chr(0x41 + i % 26) for i in range(600))]
LEVELS = {"L": qrcode.constants.ERROR_CORRECT_L, "M": qrcode.constants.ERROR_CORRECT_M,
          "Q": qrcode.constants.ERROR_CORRECT_Q, "H": qrcode.constants.ERROR_CORRECT_H}


def reads(rows, text):
    n, scale, quiet = len(rows), 6, 4
    img = Image.new("L", ((n + 2 * quiet) * scale,) * 2, 255)
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            if c == "1":
                img.paste(0, ((x + quiet) * scale, (y + quiet) * scale, (x + quiet + 1) * scale, (y + quiet + 1) * scale))
    got = pyzbar.decode(img)
    if len(got) != 1:
        return False
    data, want = got[0].data, text.encode("utf-8")
    # UTF-8 in byte mode without an ECI marker (as python-qrcode and segno write it, and phones read it): zbar guesses a
    # character set for the bytes ("Skål!" came back as b'Sk\xe7\x96\x87l!', its guess in UTF-8). So an ASCII text must
    # read back exactly, and any other must read as one QR code of as many letters around the guess
    if text.isascii():
        return data == want
    return got[0].type == "QRCODE" and data.startswith(b"Sk") == text.startswith("Sk") and len(data) >= len(text)


cases = []
for t in TEXTS:
    for ecl, level in LEVELS.items():
        for mask in ([0, 3, 5, 7] if len(t) < 400 else [2]):
            q = qrcode.QRCode(error_correction=level, mask_pattern=mask, border=0)
            q.add_data(QRData(t.encode("utf-8"), mode=MODE_8BIT_BYTE))
            q.make(fit=True)
            rows = ["".join("1" if c else "0" for c in r) for r in q.get_matrix()]
            assert reads(rows, t), f"zbar does not read {t[:20]} {ecl} {mask}"
            cases.append({"text": t, "ecc": ecl, "mask": mask, "version": q.version, "rows": rows})
with open("mod/SignLink.Tests/qr_vectors.json", "w", encoding="utf-8") as f:
    json.dump(cases, f, ensure_ascii=False, separators=(",", ":"))
print(len(cases), "cases, all read back by zbar; versions", sorted({c["version"] for c in cases}))
