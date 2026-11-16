#!/usr/bin/env python3
"""The notices of everything the Linux DiscWright carries inside its one file.

    python3 tools/notices-for-the-linux-download.py \
        src/DiscWright.LinuxApp/obj/project.assets.json <out.txt>

Run straight after `dotnet publish -r linux-x64`, while the assets file still
describes that publish. It is rewritten by the next build of anything else,
and then says nothing about Linux; this stops and says so rather than write
a list with half the names missing.

The one file is DiscWright and, packed in with it, .NET itself, Avalonia,
Skia, HarfBuzz, a typeface and a few smaller things. Each is somebody else's
work under its own licence, and each licence asks that its notice travels
with every copy. This writes them out.

**Nothing here is typed from memory.** Every licence text is read out of the
package it belongs to, on the machine doing the build, so it is the text
that version shipped with. A package that states a licence and carries no
text gets the copyright line from its own description and the MIT text once,
and that text is checked, word for word, against the one .NET ships. A
package under a licence this has no text for stops the build: a notice
nobody has read is not one to hand on.
"""

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

TARGET = "net10.0/linux-x64"
RULE = "=" * 78

# The permission and the disclaimer of the MIT licence, which every MIT
# licensed thing shares; only the copyright line above it differs. Held to
# .NET's own copy below, so a slip here cannot reach a download.
MIT = """Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE."""


def stop(why: str) -> None:
    raise SystemExit("FAILED: " + why)


def words(text: str) -> str:
    return " ".join(text.split())


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace").replace("\r\n", "\n").strip("\n")


def described(folder: Path) -> dict:
    """What a package says of itself, in its own description file."""
    nuspec = next(folder.glob("*.nuspec"), None)

    if nuspec is None:
        stop(f"{folder} has no description of itself in it.")

    said = {}

    for each in ET.parse(nuspec).getroot().iter():
        name = each.tag.split("}")[-1]

        if name in ("id", "version", "authors", "copyright", "projectUrl"):
            said[name] = (each.text or "").strip()
        elif name == "license":
            said["license"] = (each.text or "").strip()
            said["licenseKind"] = each.get("type", "")
        elif name == "repository":
            said["repository"] = each.get("url", "")

    return said


def its_own_texts(folder: Path) -> list:
    """The licence and notice files a package carries at its top."""
    return sorted(
        each for each in folder.iterdir()
        if each.is_file() and re.match(r"(?i)(licen[cs]e|third-party-notices|notice|copying)", each.name)
        and each.suffix.lower() in ("", ".txt", ".md")
    )


def what_the_typeface_says(folder: Path) -> list:
    """The copyright and the licence the Inter typeface carries in itself.

    The typeface is under the SIL Open Font License, which lets its notice
    travel in the font's own description fields, and it does. They are read
    out of the package here and repeated, so somebody reading the notices
    sees them without opening a font.
    """
    library = next(folder.glob("lib/net10.0/*.dll"), None) or next(folder.glob("lib/*/*.dll"), None)

    if library is None:
        stop(f"{folder} has no library in it to read the typeface out of.")

    data = library.read_bytes()
    found = set()

    for start in (0, 1):
        text = data[start:].decode("utf-16-be", errors="ignore")
        found.update(re.findall(r"Copyright [0-9][ -~]{0,120}?Inter Project Authors[ -~]{0,60}?\)", text))
        found.update(re.findall(r"This Font Software is licensed under the SIL Open Font License[ -~]{0,120}?OFL", text))

    if not any("Copyright" in each for each in found) or not any("Open Font License" in each for each in found):
        stop("the Inter typeface no longer says whose it is and under what licence where this looks. "
             "Find where it says so now: its notice has to travel with it.")

    return sorted(found)


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)

    assets = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    out = Path(sys.argv[2])

    if TARGET not in assets.get("targets", {}):
        stop(f"{sys.argv[1]} does not describe a publish for {TARGET}. Run this straight after the publish.")

    packages = Path(next(iter(assets["packageFolders"])))
    parts = []
    mit_only = []

    # ------------------------------------------------------------- .NET itself
    runtimes = [
        each for each in assets["project"]["frameworks"]["net10.0"].get("downloadDependencies", [])
        if each["name"].startswith("Microsoft.NETCore.App.Runtime.")
    ]

    if not runtimes:
        stop("the publish does not name the .NET runtime it packed in.")

    dotnet_licence = None

    for each in runtimes:
        version = each["version"].strip("[]").split(",")[0].strip()
        folder = packages / each["name"].lower() / version
        texts = its_own_texts(folder)

        if len(texts) < 2:
            stop(f"{folder} should carry .NET's licence and its notices, and holds {[t.name for t in texts]}.")

        parts.append(f"{RULE}\n.NET {version}  ({each['name']})\nhttps://github.com/dotnet/runtime\n{RULE}")

        for text in texts:
            body = read(text)
            parts.append(f"--- {text.name} ---\n\n{body}")

            if text.name.upper().startswith("LICENSE"):
                dotnet_licence = body

    if dotnet_licence is None or words(MIT) not in words(dotnet_licence):
        stop("the MIT text in this script is not, word for word, the one .NET ships. Correct it from theirs.")

    # ------------------------------------------------------- everything else
    for name, library in sorted(assets["targets"][TARGET].items(), key=lambda pair: pair[0].lower()):
        if library.get("type") != "package":
            continue

        # Only what puts a file into the program: a package that is all
        # build tooling, or all Windows, is not in the download.
        carried = [k for k in list(library.get("runtime", {})) + list(library.get("native", {})) if not k.endswith("_._")]

        if not carried:
            continue

        folder = packages / assets["libraries"][name]["path"]
        said = described(folder)
        texts = its_own_texts(folder)

        heading = [f"{said.get('id', name)} {said.get('version', '')}".strip()]

        if said.get("copyright"):
            heading.append(said["copyright"])
        elif said.get("authors"):
            heading.append("By " + said["authors"])

        where = said.get("repository") or said.get("projectUrl", "")

        if where:
            heading.append(where.split("?")[0])

        heading.append("Licence: " + (said.get("license") or "see below"))
        parts.append(f"{RULE}\n" + "\n".join(heading) + f"\n{RULE}")

        for text in texts:
            parts.append(f"--- {text.name} ---\n\n{read(text)}")

        if not texts:
            if said.get("licenseKind") != "expression" or said.get("license") != "MIT":
                stop(f"{name} is under \"{said.get('license')}\" and carries no licence text. "
                     "Read that licence and teach this script what it asks for.")

            mit_only.append(heading[0])
            parts.append("Under the MIT licence, whose text is given once, at the end.")

        if name.lower().startswith("avalonia.fonts.inter/"):
            parts.append(
                "This package holds the Inter typeface, which is not Avalonia's and is under its own licence. "
                "The typeface says of itself:\n\n    " + "\n    ".join(what_the_typeface_says(folder))
                + "\n\nThe licence is at https://openfontlicense.org")

    if not mit_only and len(parts) < 4:
        stop("no packages were found in the publish, which cannot be right.")

    parts.append(f"{RULE}\nThe MIT licence\n{RULE}\n\nFor: " + ", ".join(mit_only) + ".\n"
                 "Each under the copyright line given with its name above.\n\n" + MIT)

    top = (
        "DiscWright for Linux: what is inside it that is somebody else's\n"
        "\n"
        "DiscWright is one file. Packed into it, beside DiscWright itself, are the\n"
        "programs and libraries below. Each is its authors' work under its own\n"
        "licence, and each licence asks that this notice goes wherever a copy does.\n"
        "DiscWright's own licence is in LICENSE.txt.\n"
        "\n"
        "Every text here was read out of the package it belongs to when this\n"
        "download was put together."
    )

    out.write_text(top + "\n\n" + "\n\n".join(parts) + "\n", encoding="utf-8", newline="\n")
    print(f"Notices written for {len(runtimes)} runtime and "
          f"{sum(1 for p in parts if p.startswith(RULE)) - len(runtimes) - 1} packages, {out.stat().st_size} bytes.")


if __name__ == "__main__":
    main()
