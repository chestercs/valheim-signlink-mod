#!/usr/bin/env python3
"""Builds the Thunderstore zip from mod/out/SignLink.dll (./sign.sh mod first) and mod/package.

  package.py            -> mod/out/SignLink-<version>.zip (Thunderstore) and SignLink-<version>-nexus.zip (Nexus, manual)

Checks first: Plugin.Version, the csproj's <Version>, manifest.json's version_number, the top CHANGELOG entry and docs/openapi.yaml's
info.version are one version; the
description fits 250 characters; the icon is 256 x 256 PNG; the DLL holds that version. It uploads nothing:
publishing is the author's step (tcli publish, or the website), with the AI Generated category.
"""
import json
import os
import re
import sys
import zipfile

from PIL import Image

MOD = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(MOD, "package")


def fail(msg):
    sys.exit("package: " + msg)


def main():
    src = open(os.path.join(MOD, "SignLink", "Plugin.cs"), encoding="utf-8").read()
    version = re.search(r'Version = "([0-9.]+)"', src).group(1)
    csproj = open(os.path.join(MOD, "SignLink", "SignLink.csproj"), encoding="utf-8").read()
    built = re.search(r"<Version>([0-9.]+)</Version>", csproj)
    built = built.group(1) if built else "(none)"
    manifest = json.load(open(os.path.join(PKG, "manifest.json"), encoding="utf-8"))
    changelog = open(os.path.join(MOD, "CHANGELOG.md"), encoding="utf-8").read()
    top = re.search(r"^## ([0-9.]+)", changelog, re.M).group(1)
    spec = open(os.path.join(MOD, "..", "docs", "openapi.yaml"), encoding="utf-8").read()
    api = re.search(r"^  version: ([0-9.]+)$", spec, re.M)
    api = api.group(1) if api else "(none)"
    if not (version == built == manifest["version_number"] == top == api):
        fail(f"versions differ: Plugin.cs {version}, SignLink.csproj {built}, manifest {manifest['version_number']}, CHANGELOG {top}, "
             f"docs/openapi.yaml {api}")
    if len(manifest["description"]) > 250:
        fail(f"description is {len(manifest['description'])} characters (250 at most)")
    if not re.fullmatch(r"[A-Za-z0-9_]+", manifest["name"]):
        fail("name: letters, digits and _ only")
    icon = Image.open(os.path.join(PKG, "icon.png"))
    if icon.size != (256, 256) or icon.format != "PNG":
        fail(f"icon is {icon.format} {icon.size}, needs PNG 256 x 256")
    dll = os.path.join(MOD, "out", "SignLink.dll")
    if not os.path.exists(dll):
        fail("mod/out/SignLink.dll is missing: run ./sign.sh mod")
    if version.encode("utf-16-le") not in open(dll, "rb").read():
        fail(f"mod/out/SignLink.dll is not version {version}: run ./sign.sh mod")

    for name in ("LICENSE", "LICENSE-ART"):                          # the package's copies match the repository's
        if open(os.path.join(PKG, name), "rb").read() != open(os.path.join(MOD, "..", name), "rb").read():
            fail(f"mod/package/{name} differs from the repository's {name}: copy it over")

    out = os.path.join(MOD, "out", f"SignLink-{version}.zip")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(os.path.join(PKG, "manifest.json"), "manifest.json")
        z.write(os.path.join(PKG, "icon.png"), "icon.png")
        z.write(os.path.join(PKG, "README.md"), "README.md")
        z.write(os.path.join(MOD, "CHANGELOG.md"), "CHANGELOG.md")
        z.write(os.path.join(PKG, "LICENSE"), "LICENSE")
        z.write(os.path.join(PKG, "LICENSE-ART"), "LICENSE-ART")
        z.write(dll, "plugins/SignLink.dll")
    with zipfile.ZipFile(out) as z:
        names = z.namelist()
    print(f"{out}: {', '.join(names)} ({os.path.getsize(out) // 1024} KB), version {version}")

    # Nexus Mods and manual installs: unpacked into the game's folder (Vortex takes it as it is)
    nexus = os.path.join(MOD, "out", f"SignLink-{version}-nexus.zip")
    base = "BepInEx/plugins/SignLink/"
    with zipfile.ZipFile(nexus, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(dll, base + "SignLink.dll")
        z.write(os.path.join(PKG, "README.md"), base + "README.md")
        z.write(os.path.join(MOD, "CHANGELOG.md"), base + "CHANGELOG.md")
        z.write(os.path.join(PKG, "LICENSE"), base + "LICENSE")
        z.write(os.path.join(PKG, "LICENSE-ART"), base + "LICENSE-ART")
    with zipfile.ZipFile(nexus) as z:
        names = z.namelist()
    print(f"{nexus}: {', '.join(names)} ({os.path.getsize(nexus) // 1024} KB)")


if __name__ == "__main__":
    main()
