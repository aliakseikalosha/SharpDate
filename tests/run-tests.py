#!/usr/bin/env python3
"""Builds tests/RuntimeTests (Simulator + device) and runs it in the mock Playdate host.

Usage: tests/run-tests.py [Debug|Release] [--frames N] [--cc COMPILER] [--target-os OS] [--runner CMD]
                          [extra dotnet build arguments, e.g. -p:SharpDateBuildDevice=false]

Works on macOS, Linux and Windows. The mock host is compiled with --cc, $CC, or the first of
clang / gcc / cc found (on Windows: clang or gcc with a C library, i.e. Visual Studio or MinGW).
--runner runs the mock host through another program (e.g. --runner wine for a Windows build).
"""

import argparse
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "tools"))
from playdate_sdk import IS_MAC, IS_WINDOWS, sdk_path  # noqa: E402


def find_cc(explicit):
    if explicit:
        return explicit
    if os.environ.get("CC"):
        return os.environ["CC"]
    for cc in ("clang", "gcc", "cc"):
        if shutil.which(cc):
            return cc
    raise SystemExit("No C compiler found for the mock host; pass --cc.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("configuration", nargs="?", default="Debug")
    parser.add_argument("--frames", type=int, default=10)
    parser.add_argument("--cc", default=None, help="C compiler for the mock host")
    parser.add_argument("--target-os", choices=["osx", "linux", "windows"], default=None,
                        help="Simulator library to test (default: the host's)")
    parser.add_argument("--runner", default=None, help="program that runs the mock host, e.g. wine")
    args, dotnet_args = parser.parse_known_args()

    target = args.target_os or ("windows" if IS_WINDOWS else "osx" if IS_MAC else "linux")
    project = os.path.join(HERE, "RuntimeTests", "RuntimeTests.csproj")
    build = ["dotnet", "build", project, "-c", args.configuration, "-nologo", "-v:q"]
    if args.target_os:
        build.append("-p:SharpDateSimulatorTargets=" + target)
    subprocess.run(build + dotnet_args, check=True)

    host_dir = os.path.join(HERE, "MockHost", "bin")
    os.makedirs(host_dir, exist_ok=True)
    host = os.path.join(host_dir, "mockhost" + (".exe" if IS_WINDOWS or target == "windows" else ""))
    command = [find_cc(args.cc), "-O1", "-o", host, os.path.join(HERE, "MockHost", "mockhost.c"),
               "-I", os.path.join(sdk_path(), "C_API"), "-DTARGET_SIMULATOR=1", "-DTARGET_EXTENSION=1"]
    if not IS_WINDOWS and target != "windows" and not IS_MAC:
        command.append("-ldl")
    subprocess.run(command, check=True)

    library = os.path.join(HERE, "RuntimeTests", "obj", args.configuration, "sharpdate",
                           {"osx": "pdex.dylib", "linux": "pdex.so", "windows": "pdex.dll"}[target])
    run = ([args.runner] if args.runner else []) + [host, library, str(args.frames)]
    return subprocess.run(run).returncode


if __name__ == "__main__":
    sys.exit(main())
