#!/usr/bin/env python3
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
"""Lists the functions of a device build that contain double-precision FPU instructions.

The Playdate's FPU is single precision, so these instructions fault on the device. Prints one
function name per line (the build turns them into a warning).

Usage: check_doubles.py <arm-none-eabi-objdump> pdex.elf
"""

import re
import subprocess
import sys

FUNCTION = re.compile(r"^[0-9a-f]+ <(.+)>:$")
DOUBLE = re.compile(r"\.f64|vcvt\.f32\.f64|vcvt\.f64")


def main():
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    disassembly = subprocess.run([sys.argv[1], "-d", "--no-show-raw-insn", sys.argv[2]],
                                 capture_output=True, text=True, check=True).stdout
    function = None
    seen = []
    for line in disassembly.splitlines():
        match = FUNCTION.match(line)
        if match:
            function = match.group(1)
        elif function is not None and function not in seen and DOUBLE.search(line):
            seen.append(function)
    for name in seen:
        print(name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
