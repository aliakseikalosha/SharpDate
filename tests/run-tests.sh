#!/bin/sh
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# Builds tests/RuntimeTests and runs it in the mock Playdate host. See run-tests.py (also for Windows).
exec python3 "$(dirname "$0")/run-tests.py" "$@"
