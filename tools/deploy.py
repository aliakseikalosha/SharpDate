#!/usr/bin/env python3
"""Installs a .pdx on a Playdate connected over USB and launches it.

Usage: tools/deploy.py <game.pdx> [--serial /dev/cu.usbmodemPDU1_...] [--no-run]
                       [--data FILE=CONTENT ...] [--fetch NAME ...] [--fetch-after SECONDS]

Steps: pdutil <serial> datadisk -> copy to /Games -> eject -> wait for the device -> pdutil run.

--data writes files into the game's /Data/<bundleID>/ folder while the disk is mounted, and
--fetch copies files from that folder back after the game has run for --fetch-after seconds
(used with SharpDate's DevCapture hook to test on hardware).
"""

import argparse
import glob
import os
import platform
import shutil
import subprocess
import sys
import time


def sdk_path():
    path = os.environ.get("PLAYDATE_SDK_PATH")
    if path:
        return path
    config = os.path.expanduser("~/.Playdate/config")
    if os.path.exists(config):
        for line in open(config):
            if line.startswith("SDKRoot"):
                return line.split(None, 1)[1].strip()
    return os.path.expanduser("~/Developer/PlaydateSDK")


def find_serial():
    patterns = ["/dev/cu.usbmodemPD*"] if platform.system() == "Darwin" else ["/dev/serial/by-id/*Playdate*", "/dev/ttyACM*"]
    for pattern in patterns:
        ports = sorted(glob.glob(pattern))
        if ports:
            return ports[0]
    return None


def mount_points():
    user = os.environ.get("USER", "")
    if platform.system() == "Darwin":
        return ["/Volumes/PLAYDATE"]
    return ["/media/%s/PLAYDATE" % user, "/run/media/%s/PLAYDATE" % user, "/media/PLAYDATE", "/mnt/PLAYDATE"]


def wait_for(predicate, timeout, what):
    deadline = time.time() + timeout
    while time.time() < deadline:
        result = predicate()
        if result:
            return result
        time.sleep(0.5)
    raise SystemExit("timed out waiting for " + what)


def bundle_id(pdx):
    for line in open(os.path.join(pdx, "pdxinfo"), encoding="utf-8"):
        if line.startswith("bundleID="):
            return line.split("=", 1)[1].strip()
    return None


def pdutil(serial, *args):
    tool = os.path.join(sdk_path(), "bin", "pdutil")
    print("pdutil", serial, " ".join(args))
    subprocess.run([tool, serial] + list(args), check=True)


def copy_tree(source, target):
    """Copies files without metadata, so macOS doesn't leave ._ AppleDouble files on the FAT volume."""
    for root, dirs, files in os.walk(source):
        rel = os.path.relpath(root, source)
        os.makedirs(os.path.join(target, rel), exist_ok=True)
        for name in files:
            if not name.startswith("._") and name != ".DS_Store":
                shutil.copyfile(os.path.join(root, name), os.path.join(target, rel, name))


def remove_apple_double(path):
    parent, name = os.path.split(path)
    sidecar = os.path.join(parent, "._" + name)
    if os.path.exists(sidecar):
        os.remove(sidecar)


def mount(serial=None):
    if serial is None or not os.path.exists(serial):
        serial = wait_for(find_serial, 60, "the Playdate serial port")
        time.sleep(1)
    pdutil(serial, "datadisk")
    return wait_for(lambda: next((m for m in mount_points() if os.path.isdir(os.path.join(m, "Games"))), None), 60, "the Playdate data disk to mount")


def eject(volume):
    if platform.system() == "Darwin":
        subprocess.run(["diskutil", "eject", volume], check=True)
    else:
        subprocess.run(["sync"])
        if subprocess.run(["udisksctl", "unmount", "-p", "block_device=" + volume]).returncode != 0:
            subprocess.run(["umount", volume], check=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("pdx")
    parser.add_argument("--serial", default=None)
    parser.add_argument("--no-run", action="store_true")
    parser.add_argument("--data", action="append", default=[], help="NAME=CONTENT written to the game's data folder")
    parser.add_argument("--fetch", action="append", default=[], help="file to copy back from the game's data folder")
    parser.add_argument("--fetch-after", type=float, default=10)
    parser.add_argument("--fetch-to", default=".")
    args = parser.parse_args()

    pdx = os.path.abspath(args.pdx.rstrip("/"))
    if not os.path.isfile(os.path.join(pdx, "pdxinfo")):
        raise SystemExit("%s is not a .pdx (no pdxinfo); build it first" % pdx)
    name = os.path.basename(pdx)
    bundle = bundle_id(pdx)

    serial = args.serial or find_serial()
    if not serial:
        raise SystemExit("No Playdate found. Connect it over USB and unlock it.")

    volume = mount(serial)
    target = os.path.join(volume, "Games", name)
    print("copying %s -> %s" % (name, target))
    if os.path.exists(target):
        shutil.rmtree(target)
    remove_apple_double(target)
    copy_tree(pdx, target)
    if platform.system() == "Darwin":
        # macOS writes ._ AppleDouble files for attributes it adds on FAT volumes; drop them.
        subprocess.run(["dot_clean", "-m", target])
        for root, dirs, files in os.walk(target):
            for f in files:
                if f.startswith("._"):
                    os.remove(os.path.join(root, f))
        remove_apple_double(target)

    if bundle:
        data_dir = os.path.join(volume, "Data", bundle)
        if args.data or args.fetch:
            os.makedirs(data_dir, exist_ok=True)
        for item in args.fetch:
            stale = os.path.join(data_dir, item)
            if os.path.exists(stale):
                os.remove(stale)
        for item in args.data:
            file_name, _, content = item.partition("=")
            with open(os.path.join(data_dir, file_name), "w") as f:
                f.write(content)

    eject(volume)

    # After ejecting, the Playdate leaves disk mode and its serial port comes back.
    time.sleep(2)
    serial = wait_for(lambda: args.serial if args.serial and os.path.exists(args.serial) else find_serial(), 60, "the Playdate to reconnect")
    time.sleep(1)

    if args.no_run:
        print("installed /Games/%s" % name)
        return 0

    pdutil(serial, "run", "/Games/" + name)
    print("running /Games/%s on %s" % (name, serial))

    if args.fetch:
        time.sleep(args.fetch_after)
        serial = wait_for(find_serial, 30, "the Playdate serial port")
        volume = mount(serial)
        data_dir = os.path.join(volume, "Data", bundle)
        for item in args.fetch:
            source = os.path.join(data_dir, item)
            if os.path.exists(source):
                shutil.copy(source, os.path.join(args.fetch_to, item))
                print("fetched", item)
            else:
                print("missing", item)
        for item in args.data:
            leftover = os.path.join(data_dir, item.partition("=")[0])
            if os.path.exists(leftover):
                os.remove(leftover)
        eject(volume)
    return 0


if __name__ == "__main__":
    sys.exit(main())
