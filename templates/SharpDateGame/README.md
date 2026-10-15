# MyGame

A Playdate game written in C# with [SharpDate](__SHARPDATE_ROOT__).

```bash
dotnet build                          # MyGame.pdx in bin/Debug (Simulator + device code)
dotnet build -t:Run                   # build and open in the Playdate Simulator
dotnet build -c Release -t:Deploy     # install and run on a USB-connected Playdate
```

The same commands are available as VS Code tasks (Terminal → Run Task).

- `MyGame.cs` — the game (`Game` subclass registered with `[ModuleInitializer]`)
- `Source/` — `pdxinfo`, images, sounds, fonts: everything copied into the pdx
- `Directory.Build.props` — where SharpDate lives (`SharpDateRoot` or the `SHARPDATE_ROOT` environment variable)

Use `float` (not `double`): the Playdate's FPU is single precision.
