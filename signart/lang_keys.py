#!/usr/bin/env python3
"""The window's words for translation (SignLink 1.1.0): every English text the code hands to Lang.T or Lang.F, and the
check of each language table against them.

  lang_keys.py keys            the keys, one a line (escaped as a table writes them), with where each is used
  lang_keys.py check [LANG..]  each table in mod/SignLink/Lang/ (or the ones named): keys missing, keys no longer in the
                               code, placeholders ({0}, {1:0.#}) that differ from the English; exit 1 on a problem
  lang_keys.py stub LANG       a table with every key and an empty translation (an existing table's lines kept)

A table is "english<TAB>translation" lines, # comments, \\n and \\t written so (mod/Kit/Lang.cs, Lang.Parse); the key
@language holds the language's own name for the setting's list. Run in Docker: ./sign.sh lang ...
"""
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "mod")
CODE = [os.path.join(ROOT, "SignLink"), os.path.join(ROOT, "Kit")]
TABLES = os.path.join(ROOT, "SignLink", "Lang")
NAME_KEY = "@language"
NL = chr(10)
CALL = re.compile(r"Lang\.(T|F)\(")
CTX = re.compile(r'Lang\.C\(\s*"([^"]*)"\s*,\s*"([^"]*)"')
MARK = "lang:keys"                                      # a line whose every string is a key (arrays, constants)
HOLE = re.compile(r"\{(\d+)(?:[,:][^}]*)?\}")


def literal(src, i):
    """The C# regular string literal starting at src[i] == '"' -> (text, end index after the closing quote)."""
    out, i = [], i + 1
    esc = {"n": "\n", "t": "\t", "r": "\r", "\\": "\\", '"': '"', "'": "'", "0": "\0"}
    while i < len(src):
        c = src[i]
        if c == "\\":
            n = src[i + 1]
            if n == "u":
                out.append(chr(int(src[i + 2:i + 6], 16)))
                i += 6
                continue
            out.append(esc.get(n, n))
            i += 2
            continue
        if c == '"':
            return "".join(out), i + 1
        out.append(c)
        i += 1
    raise ValueError("an unclosed string")


def first_arg(src, i):
    """The literals of the call's first argument starting at src[i] (just past "("): adjacent literals joined by "+" form
    one key; a ternary's branches are keys of their own. -> (list of keys, end index)."""
    keys, cur, depth, joined = [], None, 0, False
    while i < len(src):
        c = src[i]
        if c == '"':
            text, i = literal(src, i)
            if cur is not None and joined:
                cur += text
            else:
                if cur is not None:
                    keys.append(cur)
                cur = text
            joined = False
            continue
        if c == "@" and src[i + 1] == '"':
            raise SystemExit("a verbatim string as a key: use a regular one")
        if c in "([{":
            depth += 1
        elif c in ")]}":
            if depth == 0:
                break
            depth -= 1
        elif c == "," and depth == 0:
            break
        elif c == "+":
            joined = True
        elif not c.isspace():
            joined = False
        i += 1
    if cur is not None:
        keys.append(cur)
    return keys, i


def keys():
    """{english: [file:line, ...]}: the literals of every Lang.T / Lang.F first argument (both branches of a ternary), and
    every string on a line marked lang:keys (an array or a constant handed to T later)."""
    found = {}
    for top in CODE:
        for d, _, files in os.walk(top):
            if os.sep + "obj" in d or os.sep + "bin" in d:
                continue
            for f in files:
                if not f.endswith(".cs"):
                    continue
                path = os.path.join(d, f)
                src = open(path, encoding="utf-8").read()
                rel = os.path.relpath(path, ROOT)
                for m in CTX.finditer(src):                         # Lang.C("context", "english"): the key "context|english"
                    line = src.count(NL, 0, m.start()) + 1
                    found.setdefault(m.group(1) + "|" + m.group(2), []).append(f"{rel}:{line}")
                for m in CALL.finditer(src):
                    line = src.count(NL, 0, m.start()) + 1
                    for text in first_arg(src, m.end())[0]:
                        found.setdefault(text, []).append(f"{rel}:{line}")
                for n, row in enumerate(src.split(NL), 1):
                    if MARK in row:
                        j = 0
                        tail = row[row.index(MARK) + len(MARK):].split()
                        ctx = tail[0] + "|" if tail and tail[0].isidentifier() else ""   # lang:keys palette: Lang.C keys
                        code = row[:row.index("//")] if "//" in row else row
                        while True:
                            q = code.find('"', j)
                            if q < 0:
                                break
                            text, j = literal(code, q)
                            found.setdefault(ctx + text, []).append(f"{rel}:{n}")
    return found


def escape(s):
    """As a table line writes it: a backslash doubled, a newline or tab as \\n or \\t (Lang.Unescape reads it back)."""
    return s.replace("\\", "\\\\").replace("\n", "\\n").replace("\t", "\\t")


def unescape(s):
    out, i = [], 0
    while i < len(s):
        if s[i] == "\\" and i + 1 < len(s) and s[i + 1] in "nt\\":
            out.append({"n": "\n", "t": "\t", "\\": "\\"}[s[i + 1]])
            i += 2
            continue
        out.append(s[i])
        i += 1
    return "".join(out)


def read_table(path):
    got, order = {}, []
    for raw in open(path, encoding="utf-8").read().split("\n"):
        line = raw.rstrip("\r")
        if not line or line.startswith("#") or "\t" not in line:
            continue
        k, v = line.split("\t", 1)
        got[unescape(k)] = unescape(v)
        order.append(unescape(k))
    return got, order


def holes(s):
    return sorted(HOLE.findall(s))


def stray(s):
    """A brace that is no placeholder and not doubled: string.Format would throw (Lang.F then shows the English)."""
    rest = HOLE.sub("", s.replace("{{", "").replace("}}", ""))
    return "{" in rest or "}" in rest


def check(langs):
    code = keys()
    bad = False
    names = langs or sorted(f[:-4] for f in os.listdir(TABLES) if f.endswith(".txt"))
    for lang in names:
        path = os.path.join(TABLES, lang + ".txt")
        table, _ = read_table(path)
        missing = [k for k in code if not table.get(k)]
        extra = [k for k in table if k not in code and k != NAME_KEY]
        wrong = [k for k in code if table.get(k) and holes(table[k]) != holes(k)]
        # a translation's own braces must form a valid .NET format (Lang.F falls back to English otherwise)
        broken = [k for k in code if table.get(k) and stray(table[k])]
        print(f"{lang}: {len(code)} keys, {len(code) - len(missing)} translated, {len(missing)} missing, {len(extra)} no longer used, "
              f"{len(wrong)} with other placeholders, {len(broken)} with stray braces; name: {table.get(NAME_KEY, '(none)')}")
        for k in missing[:40]:
            print("  missing:", escape(k))
        for k in extra[:40]:
            print("  unused: ", escape(k))
        for k in wrong:
            print("  placeholders:", escape(k), "->", escape(table[k]))
        for k in broken:
            print("  braces:", escape(k), "->", escape(table[k]))
        if missing or wrong or broken or NAME_KEY not in table:
            bad = True
    return 1 if bad else 0


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "keys"
    if cmd == "keys":
        for k, where in sorted(keys().items(), key=lambda kv: kv[1][0]):
            print(escape(k) + "\t" + ", ".join(where))
    elif cmd == "check":
        sys.exit(check(sys.argv[2:]))
    elif cmd == "stub":
        lang = sys.argv[2]
        path = os.path.join(TABLES, lang + ".txt")
        old, _ = read_table(path) if os.path.exists(path) else ({}, [])
        code = keys()
        os.makedirs(TABLES, exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# SignLink: the window in {lang}. english<TAB>translation; English is the key (mod/Kit/Lang.cs).\n")
            f.write(f"{NAME_KEY}\t{old.get(NAME_KEY, '')}\n")
            for k, where in sorted(code.items(), key=lambda kv: kv[1][0]):
                f.write(f"# {where[0]}\n{escape(k)}\t{escape(old.get(k, ''))}\n")
        print(f"{path}: {len(code)} keys")
    else:
        print(__doc__)
        sys.exit(2)


if __name__ == "__main__":
    main()
