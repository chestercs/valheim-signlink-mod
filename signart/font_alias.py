#!/usr/bin/env python3
"""Short aliases for <font=...>: strings with the same TMP hash as a registered font asset's name.

TMP keys loaded font assets by TMP_TextUtilities.GetHashCode(asset name): h = h*33 ^ upper(c), int32,
starting at 0 (TMP_TextUtilities.cs:663). A <font=VALUE> tag hashes its unquoted value the same way
(TMP_Text.cs:5417/5460) and looks the hash up in MaterialReferenceManager first (TMP_Text.cs:5880).
Once ONE sign in the session has loaded the font by its Resources path
(<font=valheim_fonts/valheim-norse>), any value with the same hash finds it: the asset name
(<font=valheim-norse>, 20 chars) or a 5-character alias (12 chars).

Usage: font_alias.py [font name] [count]
"""
import sys

import numpy as np

M = 0xFFFFFFFF
INV33 = pow(33, -1, 1 << 32)


def upper(c):
    return c - 32 if 97 <= c <= 122 else c


def tmp_hash(s: str) -> int:
    h = 0
    for ch in s:
        h = (((h << 5) + h) ^ upper(ord(ch))) & M
    return h


def as_int32(h):
    return h - (1 << 32) if h & 0x80000000 else h


# Characters an unquoted tag value may hold: anything but < > " (TMP_Text.cs:5360-5460). CJK ideographs
# are one UTF-16 unit each, render fine through the Noto fallbacks if a tag ever fails, and paste cleanly.
CJK = np.arange(0x4E00, 0xA000, dtype=np.uint64)


def aliases_cjk(target: int, length=5, count=5, seed=0):
    """CJK strings s of `length` chars with tmp_hash(s) == target.

    Random head, then solve the last two characters: c_last = target ^ ((h*33 ^ c_prev)*33). Four CJK
    characters cannot reach hashes above ~2^31 (0x9FFF*33^3), five reach everything."""
    rng = np.random.default_rng(seed)
    out = []
    lo, hi = 0x4E00, 0x9FFF
    tries = 0
    while len(out) < count and tries < 200000:
        tries += 1
        head = [int(x) for x in rng.integers(lo, hi + 1, size=length - 2)]
        h = 0
        for c in head:
            h = ((h * 33) ^ c) & M
        X = (h * 33) & M
        nxt = (np.uint64(X) ^ CJK) & np.uint64(M)
        last = np.uint64(target) ^ ((nxt * np.uint64(33)) & np.uint64(M))
        ok = np.nonzero((last >= lo) & (last <= hi))[0]
        for k in ok[:1]:
            s = "".join(map(chr, head)) + chr(int(CJK[k])) + chr(int(last[k]))
            assert tmp_hash(s) == target
            out.append(s)
    return out


def aliases_ascii(target: int, length=7, count=3):
    """Typeable aliases (A-Z, 0-9, first char a letter): meet in the middle, 3 chars forward."""
    letters = [ord(c) for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"]
    chars = letters + [ord(c) for c in "0123456789"]
    back_n = length - 3
    # backward table: from target undo back_n chars, keyed by the state before them
    from itertools import product
    table = {}
    for tail in product(chars, repeat=back_n):
        h = target
        for c in reversed(tail):
            h = ((h ^ c) * INV33) & M
        table.setdefault(h, tail)
    out = []
    for head in product(letters, chars, chars):
        h = 0
        for c in head:
            h = ((h * 33) ^ c) & M
        tail = table.get(h)
        if tail:
            s = "".join(map(chr, head + tail))
            assert tmp_hash(s) == target
            out.append(s)
            if len(out) >= count:
                break
    return out


if __name__ == "__main__":
    name = sys.argv[1] if len(sys.argv) > 1 else "Valheim-Norse"
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 5
    t = tmp_hash(name)
    print(f"{name}: hash {as_int32(t)} (0x{t:08x})", flush=True)
    for s in aliases_cjk(t, 5, n):
        print(f"  <font={s}>  ({len(s) + 7} chars)  U+" + " U+".join(f"{ord(c):04X}" for c in s))
    for s in aliases_ascii(t, 7, 3):
        print(f"  <font={s}>  ({len(s) + 7} chars)")
