# SharpDate reference

What every part of the API does, with a commented example for each class. Every example on these
pages is compiled by `tools/check_doc_snippets.py`, so it matches the current API.

| Page | Classes |
|---|---|
| [Game, system and input](game.md) | `Game`, `PD`, `Sys`, `MenuItem`, `Display`, `Buttons`, `SystemEvent`, `PDDateTime` |
| [Graphics](graphics.md) | `Graphics`, `LCDColor`, `SolidColor`, `Pattern`, `Bitmap`, `BitmapTable`, `Font`, `TileMap`, `VideoPlayer`, `LCDRect` |
| [Sprites](sprites.md) | `Sprite`, `Sprites`, `CollisionList`, `SpriteCollisionInfo`, `SpriteList`, `SpriteQueryList`, `PDRect` |
| [Sound](sound.md) | `Sound`, `FilePlayer`, `AudioSample`, `SamplePlayer`, `Synth`, `Instrument`, `Envelope`, `LFO`, `SoundSequence`, `SequenceTrack`, `ControlSignal`, `SoundChannel`, `SoundEffect`, `SoundSource`, `SynthSignal` |
| [Files and JSON](files-and-json.md) | `FileSystem`, `PDFile`, `Json`, `JsonNode`, `JsonHandler`, `JsonValue`, `JsonWriter` |
| [Lua interop](lua.md) | `Lua`, `LuaFunction`, `LuaType` |
| [Network and scoreboards](network.md) | `Network`, `HttpConnection`, `TcpConnection`, `Scoreboards` |
| [Native interop and the base library](native.md) | `PD.Api`, `Playdate.Native`, `Utf8`, function pointers, what's in SharpDate.CoreLib |
| [Mini3D](mini3d.md) | the C#-only 3D library in `examples/Mini3D` |
| [Coming from Lua](lua-to-csharp.md) | every Lua API name and its C# equivalent (or what to do instead) |

The [API listing](../api/README.md) lists every type and member with its signature; it is generated
from the same doc comments IntelliSense shows, including every parameter with its valid range. The
[source tree](../source-tree.md) describes every file and class in the repository.

## Conventions

These rules hold for the whole API, so the pages don't repeat them.

**Namespaces.** Everything is in `Playdate` (imported automatically in game projects). The raw C
structs are in `Playdate.Native`; refer to them as `Playdate.Native.X` because some names
(`FilePlayer`, `SoundSource` ...) exist in both.

**Handles and objects.** Playdate objects come in two kinds:

| Kind | Types | Lifetime |
|---|---|---|
| Handle structs | `Bitmap`, `BitmapTable`, `Font`, `TileMap`, `VideoPlayer`, `PDFile`, `FilePlayer`, `AudioSample`, `SamplePlayer`, `Synth`, `Instrument`, `Envelope`, `LFO`, `SoundSequence`, `SequenceTrack`, `ControlSignal`, `SoundChannel`, `SoundEffect` | A struct holding a native pointer. Copying it copies the pointer. Nothing is freed automatically: call `Dispose()` when you're done with what you created. |
| Managed classes | `Sprite` (and your subclasses), `MenuItem`, `Pattern`, `HttpConnection`, `TcpConnection`, `JsonWriter`, `JsonHandler` | Normal C# objects. Those registered with the system (sprites, menu items, connections) keep themselves alive until you `Dispose`/`Remove` them. |

**Who frees what.** You free what you create or load (`Create`, `Load`, `Copy`, `Rotated`,
`CopyFrameBufferBitmap` ...). You never free what you *get* from something else: a bitmap from a
`BitmapTable` or a sprite, a synth's envelope, the display buffer bitmap. Lists returned by sprite
queries and collisions (`CollisionList`, `SpriteList`, `SpriteQueryList`) are native arrays: dispose
them, ideally with `using`.

**Errors.** `Load` methods stop the game with a clear message if the file is missing (it's almost
always a packaging mistake); `TryLoad`/`LoadInto`/`Open` variants return false or a null handle
instead. Functions that can fail return `bool` (or a `NetErr`); `FileSystem.LastError` and
`Sound.GetError()` explain file and sound failures. C# exceptions can't be caught on the Playdate: an
unhandled `throw` stops the game with its message, like `Sys.Error`.

**Units.**

| Quantity | Unit |
|---|---|
| Coordinates | Pixels, origin top-left, x right, y down. The screen is 400 x 240. |
| Angles | Degrees. Crank: 0 = up, clockwise. Arcs and rotations: clockwise from 12 o'clock. |
| Time | Seconds as `float`, except `Sys.CurrentTimeMilliseconds` (ms) and sound scheduling (samples, 44,100 per second). |
| Volume, mix, levels | 0 to 1. |
| Pitch | Hz for `PlayNote`, MIDI note numbers (60 = middle C, fractions allowed) for `PlayMIDINote`. |
| Colors | `SolidColor` (Black, White, Clear, XOR) or a `Pattern`; both convert to `LCDColor`. |

**Strings.** Pass normal C# strings. Text drawing takes `string`, `ReadOnlySpan<char>` (no
allocation) or UTF-8 literals (`"text"u8`). Strings returned by the API are copies you own.

**Callbacks and threads.** Everything runs on one thread, the game loop, including sprite
callbacks, menu callbacks, sound finish callbacks, JSON and network events. The exceptions are audio
callbacks that take a native function pointer (custom sound sources and effects, synth generators,
custom signals, the microphone): they run on the audio thread and must be `[UnmanagedCallersOnly]`
static methods that don't allocate.

**Numbers.** Use `float` and `MathF`. The device has a single-precision FPU: code that computes with
`double` (including `Math.Sin` and double literals like `1.5`) crashes there. Device builds warn about
every method that contains double-precision instructions.
