#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
"""Generates the API listing in docs/api/ from the XML doc comments in src/SharpDate.

Usage: python3 tools/generate_api_docs.py

One Markdown page per source file (Graphics.cs -> docs/api/Graphics.md) listing every public type and
member with its declaration and documentation, plus docs/api/README.md as the index. Run it after
changing the public API; the build already warns about undocumented members (CS1591).
"""

import html
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC = os.path.join(ROOT, "src", "SharpDate")
OUT = os.path.join(ROOT, "docs", "api")

# Source files in reading order, with a title for each page.
PAGES = [
    ("PD.cs", "Game and entry point"),
    ("Sys.cs", "System: input, time, menu, device"),
    ("Display.cs", "Display, tile maps, video"),
    ("Graphics.cs", "Graphics, bitmaps, fonts"),
    ("Sprite.cs", "Sprites and collisions"),
    ("Sound.cs", "Sound"),
    ("File.cs", "Files"),
    ("Json.cs", "JSON"),
    ("Lua.cs", "Lua interop"),
    ("Network.cs", "Network and scoreboards"),
    ("Utf8.cs", "Strings for native calls"),
    ("Types.cs", "Enums and data structures"),
    ("Native/PlaydateApi.g.cs", "Raw C API (Playdate.Native)"),
]

TYPE_RE = re.compile(r"\b(class|struct|enum|delegate)\s+(\w+)")
MODIFIERS = ("public ", "internal ", "unsafe ", "sealed ", "readonly ", "partial ", "abstract ", "virtual ", "override ")


def strip_literals(line):
    """Removes string and char literals and // comments so braces can be counted."""
    line = re.sub(r'\$?@?"(?:[^"\\]|\\.)*"', '""', line)
    line = re.sub(r"'(?:[^'\\]|\\.)'", "''", line)
    return line.split("//")[0]


def xml_to_md(text):
    text = re.sub(r'<see cref="(?:[A-Z]:)?(?:Playdate\.)?([^"]+)"\s*/>', lambda m: "`" + m.group(1).replace("{", "<").replace("}", ">") + "`", text)
    text = re.sub(r'<(?:paramref|typeparamref) name="([^"]+)"\s*/>', r"`\1`", text)
    text = re.sub(r"<c>(.*?)</c>", r"`\1`", text, flags=re.S)
    text = re.sub(r"<code>(.*?)</code>", lambda m: "\n```csharp\n" + m.group(1).strip() + "\n```\n", text, flags=re.S)
    text = re.sub(r"</?para>", "\n\n", text)
    return html.unescape(text)


def parse_doc(lines):
    """Turns /// lines into a dict of summary/remarks/returns/params."""
    xml = "\n".join(l.strip()[3:].strip() for l in lines)
    doc = {"params": []}
    for tag in ("summary", "remarks", "returns", "value"):
        m = re.search(r"<%s>(.*?)</%s>" % (tag, tag), xml, re.S)
        if m:
            doc[tag] = " ".join(xml_to_md(m.group(1)).split()) if "```" not in m.group(1) and "<code>" not in m.group(1) else xml_to_md(m.group(1)).strip()
    for m in re.finditer(r'<param name="([^"]+)">(.*?)</param>', xml, re.S):
        doc["params"].append((m.group(1), " ".join(xml_to_md(m.group(2)).split())))
    return doc


def accessors(decl, lines, j):
    """For a property declaration, returns ' { get; }', ' { set; }' or ' { get; set; }' ('' otherwise)."""
    head = decl.split("=>")[0]
    if "(" in head or decl.rstrip().endswith(";") and "{" not in decl and "=>" not in decl:
        return ""
    if "=>" in decl and "{" not in head:
        return " { get; }"
    body = decl[decl.index("{"):] if "{" in decl else ""
    k = j
    if not body:
        depth = 0
        while k + 1 < len(lines):
            k += 1
            t = strip_literals(lines[k])
            body += " " + t
            depth += t.count("{") - t.count("}")
            if depth <= 0 and "}" in t:
                break
    has_get = re.search(r"\bget\b", body) is not None
    has_set = re.search(r"\bset\b", body) is not None
    if has_get and has_set:
        return " { get; set; }"
    if has_set:
        return " { set; }"
    if has_get:
        return " { get; }"
    return ""


def clean_declaration(decl, is_type=True):
    decl = " ".join(decl.split())
    cuts = [i for i in (decl.find(" => "), decl.find("{")) if i > 0]
    if cuts:
        decl = decl[:min(cuts)]
    decl = decl.rstrip(";").strip()
    decl = re.sub(r"\s*=\s*[^,()]+$", "", decl) if decl.startswith("public const") is False and " = " in decl and "(" not in decl else decl
    for mod in ("public ", "unsafe ", "sealed ", "partial ") + (("readonly ",) if is_type else ()):
        decl = decl.replace(mod, "")
    return decl.strip()


def parse_file(path):
    """Returns a list of types: {name, kind, decl, doc, members: [{decl, doc}]}."""
    lines = open(path, encoding="utf-8").read().split("\n")
    types = []
    stack = []      # (type dict, brace depth at which its body starts)
    depth = 0
    pending = []    # doc lines waiting for their declaration
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        if stripped.startswith("///"):
            pending.append(line)
            i += 1
            continue
        if pending and (stripped.startswith("[") and stripped.endswith("]")):
            i += 1
            depth += strip_literals(line).count("{") - strip_literals(line).count("}")
            continue
        if pending and stripped:
            # Gather a multi-line declaration until its parentheses balance.
            decl = re.sub(r"\s//.*$", "", stripped)
            j = i
            while decl.count("(") > decl.count(")") and j + 1 < len(lines):
                j += 1
                decl += " " + re.sub(r"\s//.*$", "", lines[j].strip())
            doc = parse_doc(pending)
            pending = []
            m = TYPE_RE.search(decl.split("(")[0] if " delegate " not in decl else decl)
            is_public = decl.startswith("public") or (stack and stack[-1][0]["kind"] == "enum")
            if m and m.group(1) != "delegate" and "(" not in decl.split(m.group(2))[0]:
                t = {"name": m.group(2), "kind": m.group(1), "decl": clean_declaration(decl), "doc": doc, "members": [],
                     "parent": stack[-1][0]["name"] if stack else None, "public": is_public}
                types.append(t)
                stack.append([t, depth + 1, False])
            elif stack and is_public:
                kind = stack[-1][0]["kind"]
                suffix = "" if kind == "enum" else accessors(decl, lines, j)
                stack[-1][0]["members"].append({"decl": clean_declaration(decl, False) + suffix, "doc": doc})
            for k in range(i, j + 1):
                depth += strip_literals(lines[k]).count("{") - strip_literals(lines[k]).count("}")
            i = j + 1
            close_scopes(stack, depth)
            continue
        pending = []
        depth += strip_literals(line).count("{") - strip_literals(line).count("}")
        close_scopes(stack, depth)
        i += 1
    return [t for t in types if t["public"]]


def close_scopes(stack, depth):
    """Marks entered type bodies and pops the ones that just closed."""
    for entry in stack:
        if depth >= entry[1]:
            entry[2] = True
    while stack and stack[-1][2] and depth < stack[-1][1]:
        stack.pop()


def render_doc(doc, heading_level=None):
    out = []
    if doc.get("summary"):
        out.append(doc["summary"])
    if doc.get("params"):
        out.append("")
        for name, text in doc["params"]:
            out.append("- `%s`: %s" % (name, text))
    if doc.get("returns"):
        out.append("")
        out.append("Returns: " + doc["returns"])
    if doc.get("remarks"):
        out.append("")
        out.append(doc["remarks"])
    return "\n".join(out)


def anchor(name):
    return name.lower()


def main():
    os.makedirs(OUT, exist_ok=True)
    index = ["# SharpDate API listing", "",
             "Generated from the XML doc comments by `tools/generate_api_docs.py` — do not edit by hand.",
             "Everything is in the `Playdate` namespace except the raw C structs (`Playdate.Native`).",
             "For explanations and commented examples see the [reference](../reference/README.md).", ""]
    for source, title in PAGES:
        types = parse_file(os.path.join(SRC, source))
        page = os.path.splitext(os.path.basename(source))[0].replace(".g", "") + ".md"
        if source.startswith("Native/"):
            page = "Native.md"
        md = ["# " + title, "",
              "Source: [`src/SharpDate/%s`](../../src/SharpDate/%s). Generated by `tools/generate_api_docs.py`." % (source, source), ""]
        if source in ("Types.cs", "Native/PlaydateApi.g.cs"):
            md += ["Restates interface facts from the Playdate SDK headers, which remain Panic's; SharpDate's own contributions to this page are 0BSD. See [LICENSING.md](../../LICENSING.md).", ""]
        index.append("## [%s](%s)" % (title, page))
        index.append("")
        for t in types:
            full = (t["parent"] + "." if t["parent"] else "") + t["name"]
            index.append("- [`%s`](%s#%s) — %s" % (full, page, anchor(full.replace(".", "")), first_sentence(t["doc"].get("summary", ""))))
            md.append("## %s" % full)
            md.append("")
            md.append("```csharp")
            md.append(t["decl"])
            md.append("```")
            md.append("")
            md.append(render_doc(t["doc"]))
            md.append("")
            if t["members"]:
                if t["kind"] == "enum":
                    md.append("| Value | Meaning |")
                    md.append("|---|---|")
                    for m in t["members"]:
                        value = m["decl"].rstrip(",")
                        md.append("| `%s` | %s |" % (value.replace("|", "\\|"), render_doc(m["doc"]).replace("\n", " ").replace("|", "\\|")))
                    md.append("")
                else:
                    for m in t["members"]:
                        md.append("#### `%s`" % m["decl"].replace("`", ""))
                        md.append("")
                        md.append(render_doc(m["doc"]))
                        md.append("")
        index.append("")
        with open(os.path.join(OUT, page), "w", encoding="utf-8") as f:
            f.write("\n".join(md).rstrip() + "\n")
    with open(os.path.join(OUT, "README.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(index).rstrip() + "\n")
    print("wrote", OUT)
    return 0


def first_sentence(text):
    m = re.match(r"(.+?[^.](?<!e\.g)(?<!i\.e)\.)(\s|$)", text)
    return m.group(1) if m else text


if __name__ == "__main__":
    sys.exit(main())
