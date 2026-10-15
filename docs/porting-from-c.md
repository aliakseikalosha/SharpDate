# Porting a C Playdate game to C#

Every C example in the Playdate SDK has a C# port in `examples/`, kept close to the original
structure, so they're a good side-by-side reference.

## Entry point

```c
int eventHandler(PlaydateAPI* pd, PDSystemEvent event, uint32_t arg)
{
    if (event == kEventInit) {
        setup(pd);
        pd->system->setUpdateCallback(update, pd);
    }
    return 0;
}
static int update(void* ud) { ...; return 1; }
```

```csharp
public sealed class MyGame : Game
{
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new MyGame());

    protected override void Initialize() { /* setup */ }      // kEventInit; update callback is installed for you
    public override bool Update() { /* ... */ return true; }
    protected override void OnEvent(SystemEvent ev, uint arg) { /* kEventPause, ... */ }
}
```

For a C extension of a Lua game (`kEventInitLua`), override `InitializeLua` and return `false` from
`UsesUpdateCallback`. If you prefer a raw handler, `PD.RunEventHandler((ev, arg) => ...)` replaces the
`Game` class entirely.

## API mapping

The wrappers keep the C names in PascalCase, grouped by subsystem:

| C | C# |
|---|---|
| `pd->graphics->clear(kColorWhite)` | `Graphics.Clear(SolidColor.White)` |
| `pd->graphics->drawText(s, strlen(s), kASCIIEncoding, x, y)` | `Graphics.DrawText("text", x, y)` |
| `pd->graphics->loadBitmap(path, &err)` | `Bitmap.Load(path)` / `Bitmap.TryLoad(path, out bmp, out err)` |
| `pd->graphics->getBitmapData(b, &w, &h, &rb, &mask, &data)` | `b.GetData(out w, out h, out rb, out mask, out data)`, `b.Width`, `b.Height` |
| `pd->graphics->getFrame()` | `Graphics.GetFrame()` (span) / `Graphics.GetFramePointer()` |
| `pd->graphics->tilemap->...` | `TileMap` |
| `pd->sprite->newSprite()` + `setUpdateFunction` | subclass `Sprite`, override `Update()` |
| `pd->sprite->setDrawFunction` | override `HasDrawFunction` and `Draw(bounds, drawRect)` |
| `pd->sprite->setCollisionResponseFunction` | override `HasCollisionResponse` and `CollisionResponse(other)` |
| `pd->sprite->moveWithCollisions(...)` + `realloc(info, 0)` | `using CollisionList c = sprite.MoveWithCollisions(...)` |
| `pd->sprite->getTag(info.other)` | `info.OtherSprite is Enemy e` (or `Tag`) |
| `pd->sprite->removeSprite(s); freeSprite(s)` | `sprite.Dispose()` |
| `pd->system->getButtonState(&cur, &pushed, &rel)` | `Sys.GetButtonState(out cur, out pushed, out rel)`, `Sys.JustPressed(b)` |
| `pd->system->getCrankChange()` | `Sys.CrankChange` |
| `pd->system->logToConsole(fmt, ...)` | `Sys.Log($"...")` |
| `pd->system->error(fmt, ...)` | `Sys.Error("...")` |
| `pd->system->addMenuItem(title, cb, ud)` | `Sys.AddMenuItem(title, item => ...)` |
| `pd->display->setRefreshRate(r)` | `Display.RefreshRate = r` |
| `pd->sound->fileplayer->...` | `FilePlayer` |
| `pd->sound->synth->...`, `instrument`, `sequence`, `track` | `Synth`, `Instrument`, `SoundSequence`, `SequenceTrack` |
| `pd->file->open(path, kFileRead)` | `FileSystem.Open(path, FileOptions.Read)` → `PDFile` |
| `pd->json->decode(&decoder, reader, &val)` | subclass `JsonHandler`, `Json.Decode(handler, file)`; or `Json.Parse` |
| `pd->lua->addFunction(f, name, &err)` | `Lua.AddFunction(&F, name, out err)` |
| `pd->lua->pushObject(ptr, type, 0)` | `Lua.PushManagedObject(obj, type)` |
| `pd->network->http->...` | `HttpConnection` |
| `rand()` / `srand()` | `new Random(seed)`, `random.Next(n)` |
| `PDRectMake(x, y, w, h)` | `new PDRect(x, y, w, h)` |
| `LCD_COLUMNS`, `LCD_ROWS`, `LCD_ROWSIZE` | `PD.LCD_COLUMNS`, ... or `Graphics.Width/Height/RowSize` |

When there is no wrapper, call the C function directly: `PD.Graphics->setColorToPattern(...)`.

## Callbacks

C callbacks with a `userdata` pointer become C# delegates or virtual methods; the wrappers manage
the `GCHandle` that keeps the target alive. Where the C API takes a callback that runs on the audio
thread (custom synth generators, effects, sources), pass a function pointer to a static
`[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]` method, exactly as in C.

## Memory

- `malloc`/`pd->system->realloc` for game data → ordinary C# objects and arrays; the GC frees them.
  Avoid creating lots of short-lived objects every frame on the device.
- Native buffers still work: `NativeMemory.Alloc/Free`, `stackalloc`, pointers and `fixed`.
- Anything you hand to native code as a pointer to a managed object must stay alive: keep it in a
  field or a `GCHandle` (the wrappers do this for you).
- **C enums are 1 byte on the device** (arm-none-eabi-gcc defaults to `-fshort-enums`, and the
  firmware is built that way) but 4 bytes in the Simulator. Passing enums by value is fine; an enum
  *inside a struct* or behind a pointer is not the same size on both. If you declare your own mirror
  of a C struct, don't give it a C# enum field for a C enum member — see `SpriteCollisionInfo` in
  `src/SharpDate/Types.cs` for a layout that works on both, and zero-initialize anything the firmware
  writes through an enum pointer.

## Numbers

C's `float` math maps to `float` and `MathF`; keep it that way. A `double` literal (`1.5` instead of
`1.5f`) or `Math.Sin` makes the device fault — the build warns about the methods affected.
C's float-to-int conversions truncate, as `(int)x` does in C#.
