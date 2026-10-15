# Tools and testing

## Runtime tests (no Simulator needed)

```bash
python3 tests/run-tests.py            # Debug (tests/run-tests.sh does the same)
python3 tests/run-tests.py Release
py tests\run-tests.py                # Windows
```

Builds `tests/RuntimeTests` (Simulator and device) and runs its Simulator library inside
`tests/MockHost`, a small C program that provides a fake `PlaydateAPI` (memory, logging, timers).
The mock host needs a C compiler with a C library (on Windows: Visual Studio's or MinGW's; pass
`--cc`). It can also test another system's library, e.g. the Windows DLL from Linux with MinGW and
Wine: `tests/run-tests.py --target-os windows --cc x86_64-w64-mingw32-gcc --runner wine`.
The tests cover arithmetic (including 64-bit division and shifts), strings and formatting, collections
and iterators, interfaces and default methods, variance, casting and boxing, delegates and events,
static constructors, spans and native memory, and garbage collection under load. A failing check
logs `FAIL ...`; crashes print a backtrace. The same tests pass on a Playdate (see [Device](device.md)).

## `tools/simtest.py` — headless Simulator runs

```bash
python3 tools/simtest.py examples/SpriteGame/bin/Debug/SpriteGame.pdx --frame 150 --out frame.png
```

Plants a trigger file in the game's data folder, starts the Simulator, waits until the game has
saved frame N, kills the Simulator, converts the frame to PNG and prints the game's log. It relies on
the `DevCapture` hook in `src/SharpDate/PD.cs`, which is inert unless the trigger file exists. For
Lua-driven games the hook calls `playdate.update` itself so it can count frames. Works with the
macOS, Linux and Windows Simulators; on a headless Linux machine run it under `xvfb-run` with
`SDL_AUDIODRIVER=dummy`.

## `tools/deploy.py` — install on a device

See [Device](device.md). Also used by the `Deploy` MSBuild target.

## `tools/generate_bindings.py` — the raw C API

```bash
python3 tools/generate_bindings.py [PLAYDATE_SDK_PATH]
```

Parses the SDK's `pd_api*.h` headers and regenerates `src/SharpDate/Native/PlaydateApi.g.cs`: one
C# struct of `delegate* unmanaged[Cdecl]` fields per C struct, in the same order. Run it when the
SDK adds functions, then add wrappers as needed. To check the result against the C compiler, compare
`sizeof(struct playdate_*) / sizeof(void*)` with the number of fields of each generated struct.
Check data structs with both compilers: enums are 1 byte with `arm-none-eabi-gcc` and 4 bytes with
clang, so a layout that matches the Simulator can still be wrong on the device. The runtime tests
(`InteropLayoutTests`) check the structs that contain enums on both.

## `tools/fixup_vfp.py`

Runs automatically after linking a device build. It replaces `vmov.f64 Dd, Dm` (undefined on the
single-precision FPU) with branches to veneers placed in the area the linker script reserves.

`tools/check_doubles.py` runs next: it lists the methods that still contain double-precision
instructions, which the build reports as a warning.

`tools/playdate_sdk.py` is the small module the Python tools share to find the SDK, `pdc`,
`pdutil` and the Simulator on each operating system.

## The build, step by step

`build/SharpDate.Game.targets` runs after `Build`:

1. `_SharpDateIlcSimulator` / `_SharpDateIlcDevice` — ILC with `--systemmodule:System.Private.CoreLib
   --nativelib --reflectiondata:none --root:<game>` and the target OS/architecture.
2. `_SharpDateLinkSimulator` — links `pdex.dylib` (clang, `lipo` for a universal one), `pdex.so`
   (clang or gcc; clang + ld.lld from other hosts) and/or `pdex.dll` (clang + lld-link, importing
   C functions from `ucrtbase.dll`).
3. `_SharpDateLinkDevice` — arm-none-eabi-gcc compiles the runtime and the SDK's `setup.c` and
   links `pdex.elf`; then `fixup_vfp.py` and the double-precision check.
4. `SharpDatePdx` — stages `Source/` (plus extra asset folders and a generated `pdxinfo` if needed)
   with the native code and runs `pdc`.

Intermediate files are in `obj/<Config>/sharpdate/`. Build with `-v:n` to see each command.
