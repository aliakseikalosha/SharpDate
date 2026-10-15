"""Locates the Playdate SDK and its tools on macOS, Linux and Windows (shared by the SharpDate tools)."""

import os
import platform

SYSTEM = platform.system()          # "Darwin", "Linux" or "Windows"
IS_WINDOWS = SYSTEM == "Windows"
IS_MAC = SYSTEM == "Darwin"


def sdk_path():
    """PLAYDATE_SDK_PATH, else SDKRoot from ~/.Playdate/config, else the installer's default folder."""
    path = os.environ.get("PLAYDATE_SDK_PATH")
    if path:
        return path
    config = os.path.join(os.path.expanduser("~"), ".Playdate", "config")
    if os.path.exists(config):
        for line in open(config, encoding="utf-8", errors="replace"):
            if line.startswith("SDKRoot"):
                return line.split(None, 1)[1].strip()
    if IS_WINDOWS:
        return os.path.join(os.path.expanduser("~"), "Documents", "PlaydateSDK")
    return os.path.join(os.path.expanduser("~"), "Developer", "PlaydateSDK")


def tool(name):
    """Path of an SDK command-line tool (pdc, pdutil)."""
    return os.path.join(sdk_path(), "bin", name + (".exe" if IS_WINDOWS else ""))


def simulator():
    """Path of the Simulator executable."""
    if IS_MAC:
        return os.path.join(sdk_path(), "bin", "Playdate Simulator.app", "Contents", "MacOS", "Playdate Simulator")
    return tool("PlaydateSimulator")


def bundle_id(pdx):
    for line in open(os.path.join(pdx, "pdxinfo"), encoding="utf-8"):
        if line.startswith("bundleID="):
            return line.split("=", 1)[1].strip()
    return None
