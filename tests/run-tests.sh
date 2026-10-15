#!/bin/sh
# Builds tests/RuntimeTests (Simulator + device) and runs it in the mock Playdate host.
set -e
cd "$(dirname "$0")"
SDK="${PLAYDATE_SDK_PATH:-$HOME/Developer/PlaydateSDK}"
CONFIG="${1:-Debug}"

dotnet build RuntimeTests/RuntimeTests.csproj -c "$CONFIG" -nologo -v:q

mkdir -p MockHost/bin
cc -O1 -o MockHost/bin/mockhost MockHost/mockhost.c -I "$SDK/C_API" -DTARGET_SIMULATOR=1 -DTARGET_EXTENSION=1 -ldl 2>/dev/null \
  || cc -O1 -o MockHost/bin/mockhost MockHost/mockhost.c -I "$SDK/C_API" -DTARGET_SIMULATOR=1 -DTARGET_EXTENSION=1

LIB=RuntimeTests/obj/$CONFIG/sharpdate/pdex.dylib
[ -f "$LIB" ] || LIB=RuntimeTests/obj/$CONFIG/sharpdate/pdex.so
MockHost/bin/mockhost "$LIB" 10
