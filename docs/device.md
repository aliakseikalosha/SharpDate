# Running on a Playdate

Every build that finds `arm-none-eabi-gcc` contains device code (`pdex.bin`) next to the Simulator
library, so any `.pdx` SharpDate produces can be installed on a Playdate.

## Install and run from the command line

1. Connect the Playdate with USB and unlock it.
2. Run:

```bash
dotnet build -c Release -t:Deploy
```

The `Deploy` target (`tools/deploy.py`):

1. finds the Playdate's serial port (`/dev/cu.usbmodemPD*` on macOS, `/dev/ttyACM*` on Linux),
2. switches it to data-disk mode with `pdutil <port> datadisk` and waits for the `PLAYDATE` volume,
3. copies the pdx to `/Games/<Name>.pdx` (replacing an older copy),
4. ejects the volume and waits for the device to come back,
5. launches the game with `pdutil <port> run /Games/<Name>.pdx`.

With several devices connected, choose one: `dotnet build -c Release -t:Deploy -p:PlaydateSerialPort=/dev/cu.usbmodemPDU1_Y0012345`.
`tools/deploy.py` also works on any existing pdx: `python3 tools/deploy.py MyGame.pdx [--no-run]`.

## From VS Code

Run the task **Install and run on Playdate** (template projects) or **SharpDate: Install and run on
Playdate** (this repository, asks for the project).

## Sideloading

The pdx is a normal Playdate game: you can also upload it with the Simulator (*Device → Upload Game
to Device*) or sideload it through [play.date](https://play.date/account/sideload/).

## Testing on hardware

`tools/deploy.py` can leave files in the game's data folder and fetch files back after a while,
which together with SharpDate's capture hook gives automated hardware tests:

```bash
python3 tools/deploy.py tests/RuntimeTests/bin/Release/RuntimeTests.pdx \
    --data sharpdate_capture=8 --fetch sharpdate_log.txt --fetch sharpdate_capture.pbm --fetch-after 12
```

When `sharpdate_capture` exists in the data folder, the game mirrors `Sys.Log` to
`sharpdate_log.txt` and saves frame N as `sharpdate_capture.pbm` (see `DevCapture` in
`src/SharpDate/PD.cs`). The runtime test suite passes on hardware (118/118).

## Performance notes

- The device's Cortex‑M7 runs at 168 MHz; the Simulator is far faster, so profile on hardware.
  `Sys.DrawFPS` and `Display.FPS` help.
- Allocation is cheap, but every collection walks all objects: allocate up front, reuse objects and
  arrays in the frame loop, and prefer structs and spans for temporary data.
- Interface calls go through a small cache; virtual calls, generics over value types and spans
  compile to the same kind of code as C.
- Use `float`. Device builds warn about methods that contain double-precision instructions; the
  single-precision register moves the .NET JIT sometimes emits as `vmov.f64` are rewritten
  automatically by `tools/fixup_vfp.py`.

## When something goes wrong

- A managed exception shows its message through `pd->system->error` and stops the game; the same
  text appears on the device's console.
- A crash on the device shows the Playdate's crash screen; `pdex.map` and `pdex.elf` in
  `obj/<Config>/sharpdate/device/` map addresses back to methods
  (`arm-none-eabi-addr2line -f -e pdex.elf <address>`).
- Device-only problems are usually doubles or allocation from an audio callback.
