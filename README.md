# SharpDate

Write [Playdate](https://play.date) games in **C#**. SharpDate compiles C# ahead of time to native
code for both the **Playdate Simulator** and the **Playdate device** (ARM Cortex‑M7), using the .NET
NativeAOT compiler (ILC) with a small purpose-built runtime. No interpreter, no JIT on the device.

```csharp
using System.Runtime.CompilerServices;

public sealed class HelloWorldGame : Game
{
    private Font _font;
    private int _x = 157, _y = 112, _dx = 1, _dy = 2;

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new HelloWorldGame());

    protected override void Initialize() => _font = Font.Load("/System/Fonts/Asheville-Sans-14-Bold.pft");

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        Graphics.SetFont(_font);
        Graphics.DrawText("Hello World!", _x, _y);
        _x += _dx; _y += _dy;
        if (_x < 0 || _x > 400 - 86) _dx = -_dx;
        if (_y < 0 || _y > 240 - 16) _dy = -_dy;
        Sys.DrawFPS(0, 0);
        return true;
    }
}
```

`dotnet build` turns that into `HelloWorld.pdx`, containing `pdex.dylib` for the Simulator and
`pdex.bin` for the device.

## Status

| | |
|---|---|
| Playdate C API (SDK 3.1) | Complete raw bindings (generated, layout-verified) + idiomatic wrappers for graphics, sprites, system, display, files, sound, JSON, Lua, scoreboards, networking |
| Playdate device | **Runs on hardware**: verified on a Playdate with the runtime test suite (118/118), the C#-only 3D demo and the Demos launcher |
| Simulator (macOS x86_64 / arm64) | Working; all examples verified (`tools/simtest.py`) |
| Simulator (Linux) | Build path implemented, untested |
| Simulator (Windows) | Not supported yet |

## Quick start

```bash
cd examples/Demos
dotnet build -t:Run                     # all examples in one pdx, opened in the Simulator
dotnet build -c Release -t:Deploy       # install and run on a USB-connected Playdate
```

New game from the template:

```bash
dotnet new install templates/SharpDateGame
dotnet new sharpdate -n MyGame --sharpdate path/to/SharpDate --bundleId com.example.mygame
cd MyGame && dotnet build -t:Run
```

VS Code: open the folder and use **Terminal → Run Task** (Build / Run in Simulator / Install and
run on Playdate / Runtime tests).

Requirements: .NET SDK 10, the Playdate SDK, clang, and `arm-none-eabi-gcc` + Python 3 for device
builds. Details: [docs/getting-started.md](docs/getting-started.md).

## Documentation

- [Getting started](docs/getting-started.md) · [API guide](docs/api-guide.md) · [Porting from C](docs/porting-from-c.md)
- [Running on a Playdate](docs/device.md) · [How it works](docs/how-it-works.md) · [Tools and testing](docs/tools-and-testing.md)

## Examples

Ports of every C example in the Playdate SDK (`C_API/Examples`), kept close to the originals, plus a
launcher and a C#-only version of the 3D example. Assets come from your SDK installation at build time.

| Example | Shows |
|---|---|
| [Demos](examples/Demos) | **All the C# examples in one pdx, picked from a menu** |
| [HelloWorld](examples/HelloWorld) | fonts, text, the `Game` class |
| [Life](examples/Life) | Conway's Life directly on the 1-bit frame buffer |
| [Tilemap](examples/Tilemap) | `TileMap`, image tables |
| [BachMidi](examples/BachMidi) | MIDI sequence on a sampled synth instrument; scrolling piano roll |
| [Json](examples/Json) | streaming JSON decoder callbacks; `Json.Parse` document API |
| [SpriteGame](examples/SpriteGame) | shoot-em-up: `Sprite` subclasses, collisions, crank, explosions |
| [SpriteCollisions](examples/SpriteCollisions) | bounce responses, custom draw, line queries |
| [Lib3DCSharp](examples/Lib3DCSharp) | the SDK's 3D scenes in pure C# (no Lua) on the [Mini3D](examples/Mini3D) library |
| [Lib3D](examples/Lib3D) | the same engine exposed to Lua, running the SDK's original Lua scenes |
| [Exposure](examples/Exposure) | a C# function called from Lua (`image:popcount()`) |
| [Array](examples/Array) | a C# class exposed to Lua that behaves like a table |
| [Particles](examples/Particles) | a C# particle system driven from Lua |
| [Networking](examples/Networking) | HTTP and TCP (run the SDK's `Examples/networking/server/server.py`) |

## The API

Everything lives in the `Playdate` namespace:

- `Graphics`, `Bitmap`, `BitmapTable`, `Font`, `Pattern`, `LCDColor`, `TileMap`, `VideoPlayer`
- `Sprite` (subclass it; override `Update`, `Draw`, `CollisionResponse`) and `Sprites`
- `Sys` (buttons, crank, accelerometer, time, menu items, logging), `Display`
- `FileSystem`, `PDFile`
- `Sound`: `FilePlayer`, `SamplePlayer`, `AudioSample`, `Synth`, `Instrument`, `SoundSequence`,
  `SequenceTrack`, `LFO`, `Envelope`, `ControlSignal`, `SoundChannel`, `SoundEffect`
- `Json`, `JsonHandler`, `JsonNode`, `JsonWriter`
- `Lua` (register C# functions and classes for hybrid Lua games)
- `Scoreboards`, `Network`, `HttpConnection`, `TcpConnection`

The complete C API is always available unwrapped through `PD.Api` (e.g. `PD.Graphics->drawLine(...)`),
with the C structs mirrored in `Playdate.Native` (`src/SharpDate/Native/PlaydateApi.g.cs`, generated by
`tools/generate_bindings.py` from the SDK headers).

Game entry: a class derived from `Game`, registered from a `[ModuleInitializer]` with `PD.Run(...)`.
Override `Initialize`, `Update` (return true to refresh the display), `InitializeLua` and `OnEvent`.
Games whose main loop is Lua return `false` from `UsesUpdateCallback`.

## How it works

```
 game.cs ─csc─▶ game.dll ┐
 SharpDate.dll ──────────┼─ILC (NativeAOT)─▶ game.o (x64/arm64 Mach-O)  ─clang──────▶ pdex.dylib ┐
 System.Private.CoreLib ─┘                 └▶ game.o (ARM32 ELF)          ─arm-gcc───▶ pdex.elf ──┼─pdc─▶ Game.pdx
                                              + sdrt.c, sdrt_*.S (runtime)                         ┘
```

- **`src/SharpDate.CoreLib`** — a small `System.Private.CoreLib` written for this project: primitives,
  strings, arrays, spans, generic collections, delegates/events, exceptions, math, text, plus the
  compiler contracts (type casting, boxing, interface dispatch, static constructors, module startup).
  Games compile against it instead of the full .NET libraries, so the same code runs on the device.
- **`src/SharpDate.Runtime`** — the native half (C + a few lines of assembly per architecture):
  allocation, a non-moving mark-and-sweep **garbage collector** (precise through objects using the
  compiler's GC descriptors, conservative over the native stack), GC handles, write barriers, the
  interface-dispatch stub, 64-bit arithmetic helpers for ARM, and the Playdate `eventHandler`.
- **`src/SharpDate`** — the Playdate API (raw generated bindings + wrappers).
- **`build/`** — MSBuild props/targets that run ILC for each target, link with clang /
  arm-none-eabi-gcc (using `src/SharpDate.Runtime/link_map.ld`, derived from the SDK's) and run `pdc`.

## Things to know

- **Use `float`, not `double`.** The Playdate's FPU is single precision; double arithmetic traps on
  the device. Device builds warn about methods containing double-precision instructions.
- **Garbage collection**: objects never move. A collection runs when ~256 KB (or the live size) has
  been allocated since the last one, or on `GC.Collect()`. Objects referenced *only* from native
  memory (e.g. Playdate userdata) must be kept alive with a static field or a `GCHandle` — the
  wrappers (`Sprite`, `MenuItem`, `HttpConnection`, Lua objects...) do this for you.
- **Exceptions are fatal**: `throw` reports the message through `pd->system->error` and stops the
  game. `try/finally` and `using` work on the normal path; `catch` blocks never run.
- **Audio callbacks** (custom sources, effects, signals) run on the audio thread/interrupt: write them
  as `[UnmanagedCallersOnly]` static methods and don't allocate in them.
- **Not supported**: reflection, generic *virtual* methods, `async`/`await`, threads, `double` on device,
  LINQ and most of the BCL beyond what's in `SharpDate.CoreLib`.

## Testing

```bash
tests/run-tests.sh
```

Builds `tests/RuntimeTests` (Simulator + device) and runs it in `tests/MockHost`, a tiny program
that loads the Simulator library with a fake Playdate API — no Simulator needed.

```bash
python3 tools/simtest.py examples/SpriteGame/bin/Debug/SpriteGame.pdx --frame 150
```

Runs a game in the real Simulator headlessly and saves frame 150 as a PNG (plus the game's log),
using SharpDate's capture hook (`DevCapture` in `src/SharpDate/PD.cs`).

## Licensing

SharpDate's runtime follows data contracts of the .NET runtime (MIT); see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). The examples are ports of Panic's SDK examples and
use assets from your local SDK; the Playdate SDK is not included. Choose a license for this
repository before publishing it.
