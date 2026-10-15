#!/usr/bin/env python3
"""Runs a .pdx in the Playdate Simulator headlessly and captures a frame.

Usage: tools/simtest.py <game.pdx> [--frame N] [--timeout SECONDS] [--out capture.png]

Relies on SharpDate's development hook (see DevCapture in src/SharpDate/PD.cs): when the game's
data folder contains `sharpdate_capture`, the game writes frame N to `sharpdate_capture.pbm` and
mirrors Sys.Log output to `sharpdate_log.txt`. The Simulator is killed afterwards.
Exit code 0 when a frame was captured.
"""

import argparse
import os
import shutil
import struct
import subprocess
import sys
import time
import zlib

from playdate_sdk import bundle_id, sdk_path, simulator


def pbm_to_png(pbm_path, png_path):
    data = open(pbm_path, "rb").read()
    parts = data.split(b"\n", 2)
    width, height = map(int, parts[1].split())
    bits = parts[2]
    row_bytes = (width + 7) // 8
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            bit = (bits[y * row_bytes + x // 8] >> (7 - x % 8)) & 1
            raw.append(0 if bit else 255)

    def chunk(kind, payload):
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    open(png_path, "wb").write(png)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("pdx")
    parser.add_argument("--frame", type=int, default=30)
    parser.add_argument("--timeout", type=float, default=30)
    parser.add_argument("--out", default=None)
    args = parser.parse_args()

    pdx = os.path.abspath(args.pdx)
    sdk = sdk_path()
    bundle = bundle_id(pdx)
    if not bundle:
        raise SystemExit("pdxinfo has no bundleID")
    data_dir = os.path.join(sdk, "Disk", "Data", bundle)
    os.makedirs(data_dir, exist_ok=True)
    for name in ("sharpdate_capture.pbm", "sharpdate_log.txt"):
        if os.path.exists(os.path.join(data_dir, name)):
            os.remove(os.path.join(data_dir, name))
    with open(os.path.join(data_dir, "sharpdate_capture"), "w") as f:
        f.write(str(args.frame))

    proc = subprocess.Popen([simulator(), pdx], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    capture = os.path.join(data_dir, "sharpdate_capture.pbm")
    deadline = time.time() + args.timeout
    crashed = False
    while time.time() < deadline and not os.path.exists(capture):
        if proc.poll() is not None:
            crashed = True
            break
        time.sleep(0.25)
    time.sleep(0.5)
    if proc.poll() is None:
        proc.kill()
        proc.wait()
    os.remove(os.path.join(data_dir, "sharpdate_capture"))

    log = os.path.join(data_dir, "sharpdate_log.txt")
    if os.path.exists(log):
        print("---- game log ----")
        print(open(log, encoding="utf-8", errors="replace").read().rstrip())
        print("------------------")

    if not os.path.exists(capture):
        print("FAILED: %s" % ("simulator exited" if crashed else "no frame captured within %ss" % args.timeout))
        return 1
    out = args.out or os.path.splitext(pdx)[0] + "-capture.png"
    pbm_to_png(capture, out)
    print("captured:", out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
