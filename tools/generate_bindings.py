#!/usr/bin/env python3
"""Generates SharpDate's raw C# bindings from the Playdate SDK C headers.

The Playdate C API is a set of structs of function pointers (pd->graphics->drawText(...)).
Their field order is the ABI, so the C# mirrors are generated rather than written by hand.

Usage: python3 tools/generate_bindings.py [PLAYDATE_SDK_PATH]
Writes src/SharpDate/Native/PlaydateApi.g.cs and prints the API structs it found.
"""

import os
import re
import sys

SDK = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("PLAYDATE_SDK_PATH", os.path.expanduser("~/Developer/PlaydateSDK"))
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "SharpDate", "Native", "PlaydateApi.g.cs")

HEADERS = ["pd_api.h"] + ["pd_api/" + h for h in [
    "pd_api_sys.h", "pd_api_file.h", "pd_api_gfx.h", "pd_api_sprite.h", "pd_api_display.h",
    "pd_api_sound.h", "pd_api_lua.h", "pd_api_json.h", "pd_api_scoreboards.h", "pd_api_network.h"]]

# C type name -> C# type name (for non-pointer use).
PRIMITIVES = {
    "void": "void", "int": "int", "unsigned int": "uint", "unsigned": "uint", "uint8_t": "byte",
    "int8_t": "sbyte", "uint16_t": "ushort", "int16_t": "short", "uint32_t": "uint", "int32_t": "int",
    "size_t": "nuint", "float": "float", "double": "double", "char": "byte", "bool": "byte",
    "uintptr_t": "nuint", "LCDColor": "nuint", "MIDINote": "float", "lua_State": "void",
    "SDFile": "SDFile", "va_list": "void*",
}

ENUMS = {
    "PDButtons": "Buttons", "PDLanguage": "Language", "PDPeripherals": "Peripherals",
    "PDPowerStatus": "PowerStatus", "FileOptions": "FileOptions", "LCDBitmapDrawMode": "DrawMode",
    "LCDBitmapFlip": "BitmapFlip", "LCDSolidColor": "SolidColor", "LCDLineCapStyle": "LineCapStyle",
    "PDStringEncoding": "StringEncoding", "LCDPolygonFillRule": "PolygonFillRule",
    "PDTextWrappingMode": "TextWrappingMode", "PDTextAlignment": "TextAlignment",
    "SpriteCollisionResponseType": "CollisionResponseType", "SoundFormat": "SoundFormat",
    "LFOType": "LFOType", "SoundWaveform": "SoundWaveform", "TwoPoleFilterType": "TwoPoleFilterType",
    "json_value_type": "JsonValueType", "l_valtype": "LuaValueType", "PDNetErr": "NetErr",
    "WifiStatus": "WifiStatus", "enum accessReply": "AccessReply", "enum LuaType": "LuaType",
    "enum MicSource": "MicSource", "PDSystemEvent": "SystemEvent",
}

STRUCTS = {
    "LCDRect": "LCDRect", "PDRect": "PDRect", "CollisionPoint": "CollisionPoint",
    "CollisionVector": "CollisionVector", "SpriteCollisionInfo": "SpriteCollisionInfo",
    "SpriteQueryInfo": "SpriteQueryInfo", "struct PDDateTime": "PDDateTime", "struct PDInfo": "PDInfo",
    "FileStat": "FileStat", "json_value": "JsonValue", "json_reader": "JsonReader",
    "json_decoder": "JsonDecoder", "struct json_decoder": "JsonDecoder", "json_encoder": "JsonEncoder",
    "struct json_encoder": "JsonEncoder", "lua_reg": "LuaReg", "lua_val": "LuaVal", "PDScore": "PDScore",
    "PDScoresList": "PDScoresList", "PDBoardsList": "PDBoardsList",
}

OPAQUE = [
    "LCDBitmap", "LCDBitmapTable", "LCDFont", "LCDFontData", "LCDFontPage", "LCDFontGlyph", "LCDTileMap",
    "LCDVideoPlayer", "LCDStreamPlayer", "HTTPConnection", "TCPConnection", "FilePlayer", "PDMenuItem",
    "LCDSprite", "SoundSource", "AudioSample", "SamplePlayer", "PDSynthSignalValue", "PDSynthSignal",
    "PDSynthLFO", "PDSynthEnvelope", "PDSynth", "ControlSignal", "PDSynthInstrument", "SequenceTrack",
    "SoundSequence", "TwoPoleFilter", "OnePoleFilter", "BitCrusher", "RingModulator", "DelayLine",
    "DelayLineTap", "Overdrive", "SoundEffect", "SoundChannel", "LuaUDObject",
]

# Function typedefs: name -> (C declaration of the function type, is_pointer_typedef)
FUNC_TYPEDEFS = {}

def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    text = re.sub(r"//[^\n]*", "", text)
    return text

def split_top(s, sep):
    parts, depth, cur = [], 0, ""
    for ch in s:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if ch == sep and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur)
    return [p.strip() for p in parts]

FUNC_PTR_RE = re.compile(r"^(?P<ret>.+?)\(\s*\*\s*(?P<name>\w*)\s*\)\s*\((?P<params>.*)\)$", re.S)

def map_base(ctype):
    ctype = " ".join(ctype.replace("const ", "").replace(" const", "").split())
    if ctype in ENUMS:
        return "global::Playdate." + ENUMS[ctype]
    if ctype in STRUCTS:
        return "global::Playdate." + STRUCTS[ctype]
    bare = ctype.replace("struct ", "").replace("enum ", "")
    if ctype in PRIMITIVES:
        return PRIMITIVES[ctype]
    if bare in OPAQUE:
        return bare
    if bare in FUNC_TYPEDEFS:
        return FUNC_TYPEDEFS[bare]
    raise KeyError("unmapped C type: " + repr(ctype))

def map_type(ctype):
    """Maps a C type (possibly with pointers) to C#."""
    ctype = ctype.strip()
    m = FUNC_PTR_RE.match(ctype)
    if m:
        return map_funcptr(m.group("ret"), m.group("params"))
    stars = ctype.count("*")
    base = ctype.replace("*", "").strip()
    bare = " ".join(base.replace("const ", "").split()).replace("struct ", "")
    if bare in FUNC_TYPEDEFS:
        # e.g. `PDCallbackFunction* update` where PDCallbackFunction is a function type
        info = FUNC_TYPEDEFS_RAW[bare]
        pointer_already = info[1]
        mapped = FUNC_TYPEDEFS[bare]
        extra = stars - (0 if pointer_already else 1)
        return mapped + "*" * max(extra, 0)
    return map_base(base) + "*" * stars

def map_funcptr(ret, params):
    args = []
    plist = split_top(params, ",")
    if not (len(plist) == 1 and plist[0].strip() in ("void", "")):
        for p in plist:
            args.append(map_type(param_type(p)))
    args.append(map_type(ret))
    return "delegate* unmanaged[Cdecl]<" + ", ".join(args) + ">"

def param_type(p):
    """Strips the parameter name from a C parameter declaration, keeping its type."""
    p = p.strip()
    m = FUNC_PTR_RE.match(p)
    if m:
        return m.group("ret") + "(*)(" + m.group("params") + ")"
    arr = re.match(r"^(.*?)(\w+)\s*\[\s*\d*\s*\]$", p)
    if arr:
        return arr.group(1).strip() + "*"
    tokens = re.match(r"^(.*?[\s\*])(\w+)$", p)
    if tokens and tokens.group(2) not in ("void", "int", "float", "double", "char") and not tokens.group(1).strip() in ("struct", "enum", "unsigned"):
        candidate = tokens.group(1).strip()
        if candidate and candidate not in ("const",):
            return candidate
    return p

FUNC_TYPEDEFS_RAW = {}

def collect_function_typedefs(text):
    # typedef int PDCallbackFunction(void* userdata);       (function type)
    # typedef void (*sndCallbackProc)(...)                  (pointer typedef)
    for m in re.finditer(r"typedef\s+([^;{}]+?)\(\s*\*\s*(\w+)\s*\)\s*\(([^;]*?)\)\s*;", text, re.S):
        FUNC_TYPEDEFS_RAW[m.group(2)] = ((m.group(1), m.group(3)), True)
    # typedef void (json_writeFunc)(...)                     (parenthesized function type)
    for m in re.finditer(r"typedef\s+([\w\s\*]+?)\(\s*(\w+)\s*\)\s*\(([^;]*?)\)\s*;", text, re.S):
        FUNC_TYPEDEFS_RAW.setdefault(m.group(2), ((m.group(1), m.group(3)), False))
    for m in re.finditer(r"typedef\s+([\w\s\*]+?)\s+(\w+)\s*\(([^;()]*(?:\([^;()]*\)[^;()]*)*)\)\s*;", text, re.S):
        name = m.group(2)
        if name not in FUNC_TYPEDEFS_RAW:
            FUNC_TYPEDEFS_RAW[name] = ((m.group(1), m.group(3)), False)

def resolve_function_typedefs():
    pending = dict(FUNC_TYPEDEFS_RAW)
    for _ in range(10):
        for name, ((ret, params), _ptr) in list(pending.items()):
            try:
                FUNC_TYPEDEFS[name] = map_funcptr(ret, params)
                del pending[name]
            except KeyError:
                pass
    if pending:
        raise KeyError("unresolved function typedefs: " + ", ".join(pending))

def parse_structs(text):
    structs = []
    for m in re.finditer(r"struct\s+(playdate_\w+|PlaydateAPI)\s*\{(.*?)\n\};", text, re.S):
        structs.append((m.group(1), m.group(2)))
    return structs

def csharp_member(decl, idx):
    decl = " ".join(decl.split())
    if not decl:
        return None
    m = re.match(r"^(const\s+)?struct\s+(\w+)\s*\*\s*(\w+)$", decl)
    if m:
        return "public %s* %s;" % (m.group(2), m.group(3))
    m = re.match(r"^uintptr_t\s+(\w+)\s*\[(\d+)\]$", decl)
    if m:
        return " ".join("public nuint %s%d;" % (m.group(1), i) for i in range(int(m.group(2))))
    m = FUNC_PTR_RE.match(decl)
    if not m:
        raise ValueError("cannot parse member: " + decl)
    name, ret, params = m.group("name"), m.group("ret"), m.group("params")
    if "..." in params:
        # Variadic: calling conventions differ from non-variadic calls on some ABIs (e.g. Apple arm64).
        # Use the SharpDate C helpers (see Native/Variadic.cs) instead of calling these directly.
        return "/// <summary>Variadic C function; call through <see cref=\"Variadic\"/> instead.</summary>\n        public void* %s;" % name
    return "public %s %s;" % (map_funcptr(ret, params), name)

def main():
    text = ""
    for h in HEADERS:
        with open(os.path.join(SDK, "C_API", h)) as f:
            text += strip_comments(f.read()) + "\n"
    collect_function_typedefs(text)
    resolve_function_typedefs()
    structs = parse_structs(text)

    out = []
    out.append("// <auto-generated>")
    out.append("// Generated by tools/generate_bindings.py from the Playdate SDK C headers")
    sdk_version = open(os.path.join(SDK, "VERSION.txt")).read().strip() if os.path.exists(os.path.join(SDK, "VERSION.txt")) else "?"
    out.append("// (SDK %s). Do not edit by hand." % sdk_version)
    out.append("// </auto-generated>")
    out.append("")
    out.append("#pragma warning disable CS0649, IDE1006")
    out.append("")
    out.append("namespace Playdate.Native")
    out.append("{")
    out.append("    // Opaque handles owned by the Playdate runtime.")
    for o in OPAQUE + ["SDFile"]:
        out.append("    public struct %s { }" % o)
    out.append("")
    for name, body in structs:
        members = split_top(body, ";")
        out.append("    /// <summary>Mirror of the C <c>struct %s</c>.</summary>" % name)
        out.append("    public unsafe struct %s" % name)
        out.append("    {")
        for i, mdecl in enumerate(members):
            cs = csharp_member(mdecl, i)
            if cs:
                out.append("        " + cs)
        out.append("    }")
        out.append("")
        print(name, len([x for x in members if x.strip()]))
    out.append("}")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as f:
        f.write("\n".join(out) + "\n")
    print("wrote", OUT)

if __name__ == "__main__":
    main()
