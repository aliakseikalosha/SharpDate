#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
"""Installs a .pdx on a Playdate connected over USB and launches it (macOS, Linux, Windows).

Usage: tools/deploy.py <game.pdx> [--serial PORT] [--no-run]
                       [--data FILE=CONTENT ...] [--fetch NAME ...] [--fetch-after SECONDS]

Steps: pdutil <serial> datadisk -> copy to /Games -> eject -> wait for the device -> pdutil run.
PORT is /dev/cu.usbmodemPD... on macOS, /dev/serial/by-id/...Playdate... or /dev/ttyACM0 on Linux,
and COM3 (etc.) on Windows; by default the first Playdate found is used.

--data writes files into the game's /Data/<bundleID>/ folder while the disk is mounted, and
--fetch copies files from that folder back after the game has run for --fetch-after seconds
(used with SharpDate's DevCapture hook to test on hardware).
"""

import argparse
import glob
import os
import shutil
import subprocess
import sys
import time

from playdate_sdk import IS_MAC, IS_WINDOWS, bundle_id, tool

PLAYDATE_USB_VENDOR = "1331"   # Panic


# ---- Serial port -------------------------------------------------------------------------------

def windows_playdate_ports():
    """COM ports of connected Playdates, from the registry (USB VID 1331 devices that are present)."""
    import winreg
    present = set()
    try:
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"HARDWARE\DEVICEMAP\SERIALCOMM") as key:
            i = 0
            while True:
                try:
                    present.add(winreg.EnumValue(key, i)[1])
                    i += 1
                except OSError:
                    break
    except OSError:
        return []
    ports = []
    try:
        usb = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Enum\USB")
    except OSError:
        return []
    with usb:
        i = 0
        while True:
            try:
                device = winreg.EnumKey(usb, i)
                i += 1
            except OSError:
                break
            if not device.upper().startswith("VID_" + PLAYDATE_USB_VENDOR):
                continue
            with winreg.OpenKey(usb, device) as device_key:
                j = 0
                while True:
                    try:
                        instance = winreg.EnumKey(device_key, j)
                        j += 1
                    except OSError:
                        break
                    try:
                        with winreg.OpenKey(device_key, instance + r"\Device Parameters") as params:
                            port = winreg.QueryValueEx(params, "PortName")[0]
                    except OSError:
                        continue
                    if port in present and port not in ports:
                        ports.append(port)
    return sorted(ports)


def find_serial():
    if IS_WINDOWS:
        ports = windows_playdate_ports()
        return ports[0] if ports else None
    patterns = ["/dev/cu.usbmodemPD*"] if IS_MAC else ["/dev/serial/by-id/*Playdate*", "/dev/ttyACM*"]
    for pattern in patterns:
        ports = sorted(glob.glob(pattern))
        if ports:
            return ports[0]
    return None


def serial_present(serial):
    if IS_WINDOWS:
        return serial in windows_playdate_ports()
    return os.path.exists(serial)


# ---- Data disk ---------------------------------------------------------------------------------

def windows_playdate_drive():
    import ctypes
    kernel32 = ctypes.windll.kernel32
    mask = kernel32.GetLogicalDrives()
    for i in range(26):
        if not mask & (1 << i):
            continue
        root = "%c:\\" % (65 + i)
        label = ctypes.create_unicode_buffer(261)
        if kernel32.GetVolumeInformationW(root, label, 261, None, None, None, None, 0) and label.value == "PLAYDATE":
            return root
    return None


def linux_mount_points():
    user = os.environ.get("USER", "")
    return ["/media/%s/PLAYDATE" % user, "/run/media/%s/PLAYDATE" % user, "/media/PLAYDATE", "/mnt/PLAYDATE"]


def find_volume():
    if IS_WINDOWS:
        return windows_playdate_drive()
    if IS_MAC:
        candidates = ["/Volumes/PLAYDATE"]
    else:
        candidates = linux_mount_points()
    for m in candidates:
        if os.path.isdir(os.path.join(m, "Games")):
            return m
    if not IS_MAC and os.path.exists("/dev/disk/by-label/PLAYDATE"):
        # Not mounted automatically (no desktop session): mount it through udisks.
        result = subprocess.run(["udisksctl", "mount", "-b", "/dev/disk/by-label/PLAYDATE"],
                                capture_output=True, text=True)
        if result.returncode == 0:
            for m in linux_mount_points():
                if os.path.isdir(os.path.join(m, "Games")):
                    return m
            # "Mounted /dev/sdb1 at /media/user/PLAYDATE"
            words = result.stdout.strip().rstrip(".").split(" at ")
            if len(words) == 2 and os.path.isdir(os.path.join(words[1], "Games")):
                return words[1]
    return None


def wait_for(predicate, timeout, what):
    deadline = time.time() + timeout
    while time.time() < deadline:
        result = predicate()
        if result:
            return result
        time.sleep(0.5)
    raise SystemExit("timed out waiting for " + what)


def pdutil(serial, *args):
    print("pdutil", serial, " ".join(args))
    subprocess.run([tool("pdutil"), serial] + list(args), check=True)


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
    if serial is None or not serial_present(serial):
        serial = wait_for(find_serial, 60, "the Playdate serial port")
        time.sleep(1)
    pdutil(serial, "datadisk")
    return wait_for(find_volume, 60, "the Playdate data disk to mount")


def eject(volume):
    """Unmounts the data disk; the Playdate then leaves disk mode."""
    if IS_MAC:
        subprocess.run(["diskutil", "eject", volume], check=True)
    elif IS_WINDOWS:
        drive = volume.rstrip("\\")
        script = "(New-Object -ComObject Shell.Application).NameSpace(17).ParseName('%s').InvokeVerb('Eject')" % drive
        subprocess.run(["powershell", "-NoProfile", "-Command", script], check=True)
        wait_for(lambda: windows_playdate_drive() is None, 30, "Windows to eject " + drive)
    else:
        subprocess.run(["sync"])
        device = subprocess.run(["findmnt", "-n", "-o", "SOURCE", "--target", volume],
                                capture_output=True, text=True).stdout.strip()
        if device and subprocess.run(["udisksctl", "unmount", "-b", device]).returncode == 0:
            subprocess.run(["udisksctl", "power-off", "-b", device])
        else:
            subprocess.run(["umount", volume], check=True)
            if device:
                subprocess.run(["eject", device])


# ---- Main --------------------------------------------------------------------------------------

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

    pdx = os.path.abspath(args.pdx.rstrip("/\\"))
    if not os.path.isfile(os.path.join(pdx, "pdxinfo")):
        raise SystemExit("%s is not a .pdx (no pdxinfo); build it first" % pdx)
    name = os.path.basename(pdx)
    bundle = bundle_id(pdx)

    serial = args.serial
    if not serial:
        # Give a device that is still reconnecting (e.g. right after a previous deploy) a moment.
        deadline = time.time() + 15
        serial = find_serial()
        while not serial and time.time() < deadline:
            time.sleep(0.5)
            serial = find_serial()
    if not serial:
        raise SystemExit("No Playdate found. Connect it over USB and unlock it.")

    volume = mount(serial)
    target = os.path.join(volume, "Games", name)
    print("copying %s -> %s" % (name, target))
    if os.path.exists(target):
        shutil.rmtree(target)
    remove_apple_double(target)
    copy_tree(pdx, target)
    if IS_MAC:
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
    serial = wait_for(lambda: args.serial if args.serial and serial_present(args.serial) else find_serial(), 60,
                      "the Playdate to reconnect")
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
