# API guide

Everything is in the `Playdate` namespace (imported automatically in game projects). The idiomatic
wrappers cover the whole Playdate C API (SDK 3.1); anything else is one step away through the raw
bindings ([below](#the-raw-c-api)).

Handles to Playdate objects (`Bitmap`, `Font`, `FilePlayer`, `Synth`, ...) are small structs around a
native pointer: copying them copies the handle, not the object. Free what you create with `Dispose()`.
Classes (`Sprite`, `MenuItem`, `HttpConnection`, ...) are managed objects that keep themselves alive
until disposed.

## Graphics

```csharp
Graphics.Clear(SolidColor.White);
Graphics.FillRect(10, 10, 100, 40, SolidColor.Black);
Graphics.DrawLine(0, 0, 400, 240, 2, SolidColor.Black);
Graphics.DrawEllipse(150, 80, 100, 80, 3, 0, 0, SolidColor.Black);   // 0, 0 = full ellipse
Graphics.FillPolygon(new[] { 10, 10, 60, 30, 20, 70 }, SolidColor.Black);

var gray = new Pattern(0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55);  // 8x8 dither
Graphics.FillRect(0, 200, 400, 40, gray);

Graphics.SetDrawMode(DrawMode.Inverted);
Graphics.SetDrawOffset(dx, dy);
Graphics.SetClipRect(x, y, w, h);
```

Text uses UTF-16 strings directly (no conversion) or UTF-8 literals:

```csharp
Font font = Font.Load("/System/Fonts/Asheville-Sans-14-Bold.pft");
Graphics.SetFont(font);
Graphics.DrawText("Score: " + score, 10, 10);
Graphics.DrawText("Hello"u8, 10, 30);
int width = font.GetTextWidth("Score");
Graphics.DrawTextInRect(longText, 10, 50, 380, 150, TextWrappingMode.Word, TextAlignment.Center);
```

Bitmaps, image tables and tilemaps:

```csharp
Bitmap player = Bitmap.Load("images/player");          // Source/images/player.png
Graphics.DrawBitmap(player, x, y, BitmapFlip.FlippedX);
player.DrawRotated(200, 120, angle);

Bitmap canvas = Bitmap.Create(64, 64, SolidColor.Clear);
Graphics.PushContext(canvas);    // draw into the bitmap
Graphics.FillEllipse(0, 0, 64, 64, 0, 0, SolidColor.Black);
Graphics.PopContext();

BitmapTable walk = BitmapTable.Load("images/walk");    // walk-table-32-32.png
Graphics.DrawBitmap(walk[frame % walk.Count], x, y);

TileMap map = TileMap.Create();
map.ImageTable = BitmapTable.Load("tiles");
map.SetSize(25, 15);
map.SetTile(3, 4, 7);
map.Draw(0, 0);
```

Direct frame-buffer access (1 bit per pixel, 1 = white, 52 bytes per row):

```csharp
Span<byte> frame = Graphics.GetFrame();
frame[y * Graphics.RowSize + x / 8] &= (byte)~(0x80 >> (x % 8));   // black pixel
Graphics.MarkUpdatedRows(y, y);
```

`Display` controls the screen: `Display.RefreshRate = 50`, `SetInverted`, `SetScale(2)`,
`SetMosaic`, `SetOffset`, `Display.FPS`.

## Sprites

Subclass `Sprite` and override what you need. The sprite system calls `Update` for every sprite
from `Sprites.UpdateAndDrawSprites()`.

```csharp
public sealed class Bullet : Sprite
{
    public Bullet(Bitmap image, float x, float y)
    {
        Image = image;
        CollideRect = new PDRect(0, 0, image.Width, image.Height);
        MoveTo(x, y);
        ZIndex = 100;
        Add();                                  // add to the display list
    }

    protected override bool HasCollisionResponse => true;
    protected override CollisionResponseType CollisionResponse(Sprite other) => CollisionResponseType.Overlap;

    protected override void Update()
    {
        GetPosition(out float x, out float y);
        using CollisionList hits = MoveWithCollisions(x, y - 8, out _, out _);
        for (int i = 0; i < hits.Count; i++)
            if (hits[i].OtherSprite is Enemy enemy)
                enemy.Explode();
        if (y < 0)
            Dispose();                          // remove and free (safe inside Update)
    }
}

// in Game.Update:
Sprites.UpdateAndDrawSprites();
```

Custom drawing: return `true` from `HasDrawFunction` and override `Draw(bounds, drawRect)`.
Queries: `Sprites.QuerySpritesAtPoint`, `QuerySpritesInRect`, `QuerySpriteInfoAlongLine`,
`sprite.OverlappingSprites()`. Lists returned by collision functions are native arrays — `Dispose`
them (a `using` does it).

## Input and system

```csharp
if (Sys.JustPressed(Buttons.A)) Jump();
if (Sys.IsPressed(Buttons.Left)) x -= 2;
Sys.GetButtonState(out Buttons held, out Buttons pushed, out Buttons released);

float angle = Sys.CrankAngle;      // 0..360, 0 = up
float delta = Sys.CrankChange;     // degrees since the last call
bool docked = Sys.IsCrankDocked;

Sys.SetPeripheralsEnabled(Peripherals.Accelerometer);
Sys.GetAccelerometer(out float ax, out float ay, out float az);

Sys.Log("hello");                  // console (Simulator console, or device serial)
Sys.DrawFPS(0, 0);
uint ms = Sys.CurrentTimeMilliseconds;
```

System menu items (up to three):

```csharp
Sys.AddMenuItem("Restart", _ => Restart());
MenuItem sound = Sys.AddCheckmarkMenuItem("Sound", true, item => _sound = item.Checked);
Sys.AddOptionsMenuItem("Level", new[] { "Easy", "Hard" }, item => _level = item.Value);
```

Also: battery, power status, time zone and date conversion, `Sys.GetLaunchArgs`,
`Sys.RestartGame(args)`, `Sys.SetButtonCallback`, `Sys.SetMenuImage`.

## Sound

```csharp
FilePlayer music = FilePlayer.Load("music/theme");      // .pda/.mp3 compiled by pdc
music.Play(0);                                          // 0 = loop forever

SamplePlayer jump = SamplePlayer.Create(AudioSample.Load("sfx/jump"));
jump.Play();

Synth beep = Synth.Create(SoundWaveform.Square);
beep.AttackTime = 0.01f;
beep.ReleaseTime = 0.2f;
beep.PlayMIDINote(60, 1, 0.25f);                        // C4, quarter of a second

SoundSequence song = SoundSequence.Create();
song.LoadMIDIFile("song.mid");
Instrument piano = Instrument.Create();
piano.AddVoice(Synth.Create(SoundWaveform.Triangle));
for (int i = 0; i < song.TrackCount; i++) song.GetTrack(i).Instrument = piano;
Sound.DefaultChannel.AddSource(piano);
song.Play();
```

Modulation and effects: `LFO`, `Envelope`, `ControlSignal`, `SynthSignal`, `SoundChannel`,
`SoundEffect.TwoPoleFilter/BitCrusher/RingModulator/Overdrive/DelayLine`. Finish/loop callbacks
take ordinary C# delegates. Callbacks that run on the audio thread (custom sources and effects,
signal steps, the microphone) take function pointers to `[UnmanagedCallersOnly]` static methods
and must not allocate.

## Files

```csharp
FileSystem.WriteAllText("save.json", json);       // the game's data folder
string text = FileSystem.ReadAllText("save.json"); // data folder first, then the pdx
byte[] level = FileSystem.ReadAllBytes("levels/1.bin");

using (PDFile file = FileSystem.Open("log.txt", FileOptions.Append))
    file.Write("line\n");

List<string> files = FileSystem.ListFiles("levels");
FileSystem.Mkdir("saves");
```

## JSON

```csharp
JsonNode config = Json.ParseFile("config.json");
int lives = config["player"]["lives"].IntValue;
foreach (JsonNode enemy in config["enemies"].Array) { ... }

using var writer = new JsonWriter(pretty: true);
writer.StartTable();
writer.AddTableMember("score"); writer.WriteInt(score);
writer.EndTable();
FileSystem.WriteAllText("save.json", writer.ToString());
```

For large documents subclass `JsonHandler` and use `Json.Decode`/`Json.DecodeFile` to receive the
decoder's callbacks (`DidDecodeTableValue`, `DidDecodeArrayValue`, ...) without building a tree.

## Lua interop

A game can keep its main loop in `Source/main.lua` and implement hot paths in C#:

```csharp
public sealed unsafe class MyGame : Game
{
    protected override bool UsesUpdateCallback => false;   // Lua drives the frame

    protected override void InitializeLua()
    {
        Lua.AddFunction(&Distance, "mygame.distance", out _);
        Lua.RegisterClass("mygame.thing", new[] { new LuaFunction("new", &NewThing), new LuaFunction("__gc", &FreeThing) }, out _);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Distance(void* L)
    {
        float dx = Lua.GetArgFloat(1), dy = Lua.GetArgFloat(2);
        Lua.PushFloat(MathF.Sqrt(dx * dx + dy * dy));
        return 1;                                            // number of results
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NewThing(void* L) { Lua.PushManagedObject(new Thing(), "mygame.thing"); return 1; }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int FreeThing(void* L) { Lua.ReleaseManagedObject(1, "mygame.thing"); return 0; }
}
```

`Lua.GetArgManagedObject<T>(pos, className)` returns the C# object behind a Lua object. See
`examples/Array`, `examples/Particles` and `examples/Lib3D`.

## Network and scoreboards

`HttpConnection` and `TcpConnection` expose the Playdate's networking with events
(`OnRequestComplete`, `OnConnectionClosed`, ...); call `RequestAccess` first. `Scoreboards.AddScore`,
`GetScores`, `GetPersonalBest` and `GetScoreboards` take callbacks. See `examples/Networking`.

## The raw C API

Every function of the C API is available unwrapped:

```csharp
unsafe
{
    PD.Graphics->drawLine(0, 0, 100, 100, 1, (nuint)SolidColor.Black);
    PD.Sprite->setAlwaysRedraw(1);
    Playdate.Native.playdate_sys* sys = PD.System;
}
```

`PD.Api` is the `PlaydateAPI*` the game was started with; `PD.Graphics`, `PD.Sprite`, `PD.Sound`, ...
are its members. Refer to native types as `Playdate.Native.X` rather than importing the namespace:
some opaque native types (`FilePlayer`, `AudioSample`, ...) share names with the wrappers. The structs mirror the C headers exactly (generated by `tools/generate_bindings.py`,
field order verified against the C compiler) and live in `Playdate.Native`. Variadic C functions
(`logToConsole`, `error`, `formatString`) can't be called portably from C#; use `Sys.Log`/`Sys.Error`.

## The base class library

Games compile against SharpDate's own CoreLib, which includes: primitives, `string` (formatting,
interpolation, `StringBuilder`, UTF-8), arrays (including multidimensional), `Span<T>`, `List<T>`,
`Dictionary<K,V>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`, delegates, events, lambdas, `yield`,
tuples, `Math`/`MathF`, `Random`, `Console.WriteLine` (→ the Playdate console), `GC`, `GCHandle`,
`NativeMemory`. It does not include LINQ, reflection, `async`/`await`, threads, `System.IO` streams
or networking classes — see [How it works](how-it-works.md) for the details and the reasons.
