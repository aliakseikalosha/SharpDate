#!/usr/bin/env python3
"""Rewrites double-precision register moves in a linked Playdate pdex.elf.

The .NET ARM32 JIT sometimes copies a pair of single-precision registers with one
`vmov.f64 Dd, Dm`. The Playdate's FPU (fpv5-sp-d16) is single precision only, so that
encoding is UNDEFINED there. Each such instruction is replaced with a `b.w` to a veneer:

    vmov.f32 s(2d),   s(2m)
    vmov.f32 s(2d+1), s(2m+1)
    b.w      <instruction after the original>

Veneers go into the zero-filled area between the linker symbols __sharpdate_veneers_start and
__sharpdate_veneers_end (see src/SharpDate.Runtime/link_map.ld).

Usage: fixup_vfp.py pdex.elf
"""

import struct
import sys

VENEER_SIZE = 12


def read_elf(data):
    if data[:4] != b"\x7fELF" or data[4] != 1 or data[5] != 1:
        raise SystemExit("not a 32-bit little-endian ELF file")
    e_shoff, = struct.unpack_from("<I", data, 0x20)
    e_shentsize, e_shnum, e_shstrndx = struct.unpack_from("<HHH", data, 0x2E)
    sections = []
    for i in range(e_shnum):
        name, type_, flags, addr, offset, size, link, info, align, entsize = struct.unpack_from("<10I", data, e_shoff + i * e_shentsize)
        sections.append(dict(name=name, type=type_, flags=flags, addr=addr, offset=offset, size=size, link=link, entsize=entsize))
    shstr = sections[e_shstrndx]
    for s in sections:
        end = data.index(b"\0", shstr["offset"] + s["name"])
        s["name"] = data[shstr["offset"] + s["name"]:end].decode()
    return sections


def symbols(data, sections):
    result = []
    for s in sections:
        if s["type"] != 2:  # SHT_SYMTAB
            continue
        strtab = sections[s["link"]]
        for off in range(s["offset"], s["offset"] + s["size"], 16):
            name, value, size, info, other, shndx = struct.unpack_from("<IIIBBH", data, off)
            end = data.index(b"\0", strtab["offset"] + name)
            result.append((data[strtab["offset"] + name:end].decode(), value, size, info & 0xF))
    return result


def is_32bit(hw):
    return (hw >> 11) in (0b11101, 0b11110, 0b11111)


def vmov_f32(sd, sm):
    hw1 = 0xEEB0 | ((sd & 1) << 6)
    hw2 = ((sd >> 1) << 12) | 0x0A40 | ((sm & 1) << 5) | (sm >> 1)
    return struct.pack("<HH", hw1, hw2)


def branch_w(source, target):
    offset = target - (source + 4)
    if not -(1 << 24) <= offset < (1 << 24):
        raise SystemExit("veneer out of branch range")
    s = (offset >> 24) & 1
    i1 = (offset >> 23) & 1
    i2 = (offset >> 22) & 1
    imm10 = (offset >> 12) & 0x3FF
    imm11 = (offset >> 1) & 0x7FF
    j1 = (1 - i1) ^ s
    j2 = (1 - i2) ^ s
    return struct.pack("<HH", 0xF000 | (s << 10) | imm10, 0x9000 | (j1 << 13) | (j2 << 11) | imm11)


def main():
    path = sys.argv[1]
    data = bytearray(open(path, "rb").read())
    sections = read_elf(data)
    syms = symbols(data, sections)
    by_name = {name: value for name, value, _, _ in syms}
    start = by_name.get("__sharpdate_veneers_start")
    end = by_name.get("__sharpdate_veneers_end")
    if start is None or end is None:
        raise SystemExit("veneer area symbols not found (link with SharpDate's link_map.ld)")

    def file_offset(addr):
        for s in sections:
            if s["type"] == 1 and s["addr"] <= addr < s["addr"] + s["size"]:
                return s["offset"] + addr - s["addr"]
        raise SystemExit("address %#x not in a PROGBITS section" % addr)

    next_veneer = start
    patched = 0
    seen = set()
    # Walk every function (Thumb function symbols have the low bit set).
    for name, value, size, kind in syms:
        if kind != 2 or size == 0:  # STT_FUNC
            continue
        func = value & ~1
        if func in seen:
            continue
        seen.add(func)
        pc = func
        prev_hw = None
        while pc < func + size:
            off = file_offset(pc)
            hw1, = struct.unpack_from("<H", data, off)
            if is_32bit(hw1):
                hw2, = struct.unpack_from("<H", data, off + 2)
                # VMOV.F64 Dd, Dm (register): 1110 1110 1D11 0000 | Vd 1011 01M0 Vm
                in_it_block = prev_hw is not None and (prev_hw & 0xFF00) == 0xBF00 and (prev_hw & 0x000F) != 0
                if (hw1 & 0xFFBF) == 0xEEB0 and (hw2 & 0x0FD0) == 0x0B40 and not in_it_block:
                    d = ((hw1 >> 6) & 1) << 4 | (hw2 >> 12)
                    m = ((hw2 >> 5) & 1) << 4 | (hw2 & 0xF)
                    if next_veneer + VENEER_SIZE > end:
                        raise SystemExit("too many double-precision moves for the veneer area")
                    veneer = vmov_f32(2 * d, 2 * m) + vmov_f32(2 * d + 1, 2 * m + 1) + branch_w(next_veneer + 8, pc + 4)
                    data[file_offset(next_veneer):file_offset(next_veneer) + VENEER_SIZE] = veneer
                    data[off:off + 4] = branch_w(pc, next_veneer)
                    next_veneer += VENEER_SIZE
                    patched += 1
                prev_hw = hw1
                pc += 4
            else:
                prev_hw = hw1
                pc += 2

    open(path, "wb").write(data)
    if patched:
        print("fixup_vfp: rewrote %d double-precision register move(s) in %s" % (patched, path))


if __name__ == "__main__":
    main()
