# Getting started

## 1. Install the tools

| Tool | Why |
|---|---|
| [.NET SDK 10](https://dotnet.microsoft.com/download) | Compiles C#; the build downloads the matching NativeAOT compiler (ILCompiler) automatically |
| [Playdate SDK](https://play.date/dev/) | `pdc`, the Simulator, `pdutil`, and the C headers |
| clang (Xcode command line tools on macOS, `clang` package on Linux) | Links the Simulator library |
| [Arm GNU toolchain](https://developer.arm.com/downloads/-/arm-gnu-toolchain-downloads) (`arm-none-eabi-gcc`) | Device builds — the same toolchain Playdate C games use. Optional: without it you get Simulator-only builds |
| Python 3 | Device builds (`tools/fixup_vfp.py`) and the deploy/test tools |

SharpDate finds the Playdate SDK through `PLAYDATE_SDK_PATH`, `~/.Playdate/config` (written by the
SDK installer) or `~/Developer/PlaydateSDK`. Supported hosts: macOS (Intel and Apple Silicon) and
Linux. Windows is not supported yet.

## 2. Try the examples

```bash
git clone <this repository> SharpDate
cd SharpDate/examples/Demos
dotnet build -t:Run
```

This builds `SharpDateDemos.pdx` and opens it in the Simulator: a menu with every C# example.
Each example can also be built on its own (`examples/<Name>`).

## 3. Create your own game

Install the project template once:

```bash
dotnet new install path/to/SharpDate/templates/SharpDateGame
```

Then create a game next to (or anywhere outside) the SharpDate folder:

```bash
dotnet new sharpdate -n SpaceDuck --sharpdate path/to/SharpDate --bundleId com.example.spaceduck --creator "Your Name"
cd SpaceDuck
dotnet build -t:Run
```

`--sharpdate` is stored in `Directory.Build.props`; you can change it there or set the
`SHARPDATE_ROOT` environment variable instead.

The generated project:

```
SpaceDuck/
  SpaceDuck.csproj          game name and bundle ID
  SpaceDuck.cs              the game
  Source/pdxinfo            Playdate metadata; put images, sounds, fonts here too
  Directory.Build.props     imports build/SharpDate.Game.props
  Directory.Build.targets   imports build/SharpDate.Game.targets
  .vscode/tasks.json        Build / Run in Simulator / Install and run on Playdate
```

## 4. Build, run, deploy

| Command | Result |
|---|---|
| `dotnet build` | `bin/Debug/<Game>.pdx` with Simulator and device code |
| `dotnet build -c Release` | Optimized; on macOS the Simulator library is universal (x86_64 + arm64) |
| `dotnet build -t:Run` | Build, then open the game in the Playdate Simulator |
| `dotnet build -c Release -t:Deploy` | Build, install on a USB-connected Playdate and launch it (see [Device](device.md)) |
| `dotnet clean` | Remove build output |

In VS Code, the same actions are tasks (**Terminal → Run Task**). The repository's own
`.vscode/tasks.json` asks which example to use.

## 5. The game class

```csharp
using System.Runtime.CompilerServices;

namespace SpaceDuck;

public sealed class SpaceDuckGame : Game
{
    // Tell SharpDate which game to run. The factory runs when the Playdate sends kEventInit.
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new SpaceDuckGame());

    private Bitmap _duck;

    protected override void Initialize()
    {
        _duck = Bitmap.Load("images/duck");   // Source/images/duck.png
    }

    public override bool Update()             // every frame; return true to refresh the display
    {
        Graphics.Clear(SolidColor.White);
        Graphics.DrawBitmap(_duck, 180, 100);
        return true;
    }
}
```

Other overridable members: `InitializeLua()` (register functions for `main.lua`), `OnEvent(event, arg)`
(pause, resume, lock, terminate...), and `UsesUpdateCallback` (return `false` when Lua runs the main loop).

## Project properties

Set these in the game's `.csproj`:

| Property | Default | Meaning |
|---|---|---|
| `PlaydateGameName` | assembly name | Name of the `.pdx` and of a generated `pdxinfo` |
| `PlaydateBundleId` | `dev.sharpdate.<name>` | Used when `Source/pdxinfo` doesn't exist |
| `PlaydateAuthor`, `PlaydateDescription`, `PlaydateVersion`, `PlaydateBuildNumber` | | Generated `pdxinfo` fields |
| `PlaydateSourceDir` | `Source` | Assets copied into the pdx |
| `PlaydateExtraSourceDir` / `PlaydateSdkExampleAssets` | | More asset folders (the latter relative to `<SDK>/C_API/Examples`, `;`-separated) |
| `PlaydateSDKPath` | auto | Playdate SDK location |
| `SharpDateBuildDevice` | true if `arm-none-eabi-gcc` is found | Build `pdex.bin` |
| `SharpDateBuildSimulator` | true | Build `pdex.dylib`/`.so` |
| `SharpDateUniversalSimulator` | true for Release on macOS | x86_64 + arm64 Simulator library |
| `SharpDateArmGcc` | auto | Path to `arm-none-eabi-gcc` |
| `PlaydateSerialPort` | first Playdate found | Device used by `-t:Deploy` |

Next: [API guide](api-guide.md) · [Porting from C](porting-from-c.md) · [Device](device.md)
