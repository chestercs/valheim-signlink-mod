"""Scratch bench: variants of one motif side by side (not part of the gallery rounds)."""
from engine import Art
from concepts.common import *  # noqa: F401,F403


def profiles():
    a = Art("lab_profiles", "Profil-labor", "Profilos emoji-állatok, felül eredeti, alul tükrözve.")
    chars = "🐉🦌🐗🐍🦅🐎🐋🦉🐲🐺"
    for i, ch in enumerate(chars):
        a.add(ch, -450 + 100 * i, 60, h=80, color=BONE)
        a.add(ch, -450 + 100 * i, -60, h=80, color=GOLD, mirror=True)
    return a


CONCEPTS = [profiles]
