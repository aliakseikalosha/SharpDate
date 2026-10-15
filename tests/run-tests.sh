#!/bin/sh
# Builds tests/RuntimeTests and runs it in the mock Playdate host. See run-tests.py (also for Windows).
exec python3 "$(dirname "$0")/run-tests.py" "$@"
