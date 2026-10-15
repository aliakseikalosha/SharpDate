#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
"""Compiles the C# examples in the documentation against SharpDate.

Usage: python3 tools/check_doc_snippets.py [markdown files...]   (default: README.md and docs/**/*.md)

Every ```csharp block is compiled, each in its own namespace:
  - blocks that declare a type (class/struct/enum at the start of a line) are compiled as they are;
  - other blocks are statements and become the body of a method.
`using` lines move to the top of the file. Put <!-- no-compile --> on the line before a block to skip it.
A fragment that uses names defined elsewhere can declare them in a comment on the line before it,
<!-- context: int x = 0, y = 0; -->, which is compiled in front of the block (inside the method for
statements, inside the namespace for type declarations). Generated pages (docs/api) are skipped.
Exit code 0 when everything compiles.
"""

import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
TYPE_DECL = re.compile(r"^(?:\[[^\]]*\]\s*)*(?:(?:public|internal|sealed|static|abstract|unsafe|readonly|partial)\s+)*(?:class|struct|enum|record)\s+\w+", re.M)
FENCE = re.compile(r"(?P<before>[^\n]*)\n```csharp\n(?P<code>.*?)\n```", re.S)


def snippets(path):
    text = open(path, encoding="utf-8").read()
    for m in FENCE.finditer("\n" + text):
        if "no-compile" in m.group("before"):
            continue
        line = text[:m.start()].count("\n") + 2
        context = re.search(r"<!--\s*context:(.*?)-->", m.group("before"))
        yield line, m.group("code"), context.group(1).strip() if context else ""


def main():
    files = sys.argv[1:] or [os.path.join(ROOT, "README.md")] + sorted(
        p for p in glob.glob(os.path.join(ROOT, "docs", "**", "*.md"), recursive=True)
        if os.sep + "api" + os.sep not in p)
    work = tempfile.mkdtemp(prefix="sharpdate-snippets-")
    sources = []
    origin = {}
    for path in files:
        for line, code, context in snippets(path):
            n = len(sources)
            usings = [l for l in code.split("\n") if l.startswith("using ") and l.rstrip().endswith(";") and "(" not in l]
            body = "\n".join(l for l in code.split("\n") if l not in usings)
            if context:
                body = context + "\n" + body
            if re.search(r"^namespace\s+[\w.]+;", body, re.M):
                src = "%s\n%s\n" % ("\n".join(usings), re.sub(r"^namespace\s+([\w.]+);", r"namespace DocSnippet%d.\1;" % n, body, flags=re.M))
            elif TYPE_DECL.search(body):
                src = "%s\nnamespace DocSnippet%d\n{\n%s\n}\n" % ("\n".join(usings), n, body)
            else:
                src = "%s\nnamespace DocSnippet%d\n{\n    static unsafe class Snippet\n    {\n        static void Run()\n        {\n%s\n        }\n    }\n}\n" % (
                    "\n".join(usings), n, body)
            name = "Snippet%03d.cs" % n
            open(os.path.join(work, name), "w", encoding="utf-8").write(src)
            origin[name] = "%s:%d" % (os.path.relpath(path, ROOT), line)
            sources.append(name)

    open(os.path.join(work, "DocSnippets.csproj"), "w").write("""<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="%s" />
  <PropertyGroup>
    <IsPlaydateGame>false</IsPlaydateGame>
    <!-- unused variables/values and unreachable examples are fine in snippets -->
    <NoWarn>$(NoWarn);CS0168;CS0219;CS0162;CS8321;CS0649;CS0169;CS0414;CS1998</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="%s" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="System.Runtime.CompilerServices" />
    <Using Include="System.Runtime.InteropServices" />
  </ItemGroup>
</Project>
""" % (os.path.join(ROOT, "build", "SharpDate.Game.props"), os.path.join(ROOT, "examples", "Mini3D", "Mini3D.csproj")))

    result = subprocess.run(["dotnet", "build", work, "-nologo", "-v:q"], capture_output=True, text=True)
    errors = sorted(set(l for l in result.stdout.splitlines() if " error " in l))
    for e in errors:
        m = re.search(r"(Snippet\d+\.cs)\((\d+),\d+\)", e)
        where = origin.get(m.group(1), "?") if m else "?"
        print("%s: %s" % (where, e.split(": error ", 1)[-1].split(" [")[0]))
    print("%d snippets in %d files: %s" % (len(sources), len(files), "OK" if result.returncode == 0 else "%d errors" % len(errors)))
    if result.returncode == 0:
        shutil.rmtree(work)
    else:
        print("generated sources kept in", work)
    return result.returncode


if __name__ == "__main__":
    sys.exit(main())
