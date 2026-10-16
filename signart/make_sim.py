"""Inject glyph data, built concepts and the in-game test log into sim_template.html -> out/simulator.html."""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def main(entries=None):
    with open(os.path.join(HERE, "out", "glyphs.json"), encoding="utf-8") as f:
        glyphs = json.load(f)
    if entries is None:
        with open(os.path.join(HERE, "out", "concepts.json"), encoding="utf-8") as f:
            entries = json.load(f)
    log = os.path.join(HERE, "ingame_tests.json")                  # the author's in-game log, kept out of the repository
    tests = []
    if os.path.exists(log):
        with open(log, encoding="utf-8") as f:
            tests = json.load(f)
    data = {"glyphs": glyphs, "concepts": entries, "tests": tests}
    with open(os.path.join(HERE, "sim_template.html"), encoding="utf-8") as f:
        page = f.read()
    blob = json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("</", r"<\/")
    page = page.replace("/*__DATA__*/", "const DATA = " + blob + ";")
    out = os.path.join(HERE, "out", "simulator.html")
    with open(out, "w", encoding="utf-8") as f:
        f.write(page)
    print(f"{out} ({os.path.getsize(out) // 1024} KiB, {len(entries)} concepts)")


if __name__ == "__main__":
    main()
