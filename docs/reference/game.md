# Game, system and input

`Game`, `PD`, `Sys`, `MenuItem`, `Display` and the enums they use. Full member lists:
[PD](../api/PD.md), [Sys](../api/Sys.md), [Display](../api/Display.md), [enums](../api/Types.md).

## Game

The base class of every SharpDate game (the C `eventHandler` plus the update callback; in Lua,
`main.lua` with `playdate.update`). Derive from it, register it from a `[ModuleInitializer]`, and
override what you need:

| Member | When it runs | Lua / C equivalent |
|---|---|---|
| `Initialize()` | Once, at start-up (kEventInit). Load images, create sprites, add menu items. | top of main.lua / kEventInit |
| `Update()` | Every frame (30 per second by default). Return `true` if you drew something. | `playdate.update()` / update callback |
| `OnEvent(ev, arg)` | Pause, resume, lock, unlock, terminate, low power, Simulator key presses. | `playdate.gameWillPause()` ... / eventHandler |
| `InitializeLua()` | Once, when Lua is ready (only for games that have a main.lua). | kEventInitLua |
| `UsesUpdateCallback` | Return `false` if Lua runs the main loop. | not calling setUpdateCallback |

```csharp
using System.Runtime.CompilerServices;

public sealed class MyGame : Game
{
    // SharpDate calls this before the Playdate starts the game. PD.Run only stores the factory;
    // the game object is created when the system sends the Init event.
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new MyGame());

    private Font _font;
    private int _frames;

    protected override void Initialize()
    {
        // The Playdate API is ready here: load assets, set the frame rate, add menu items.
        _font = Font.Load("/System/Fonts/Asheville-Sans-14-Bold.pft");
        Display.RefreshRate = 30;
    }

    public override bool Update()
    {
        _frames++;
        Graphics.Clear(SolidColor.White);
        Graphics.SetFont(_font);
        Graphics.DrawText("Frame " + _frames, 10, 10);
        return true;                       // true = something was drawn, refresh the display
    }

    protected override void OnEvent(SystemEvent ev, uint arg)
    {
        switch (ev)
        {
            case SystemEvent.Pause:        // the system menu opened
                break;
            case SystemEvent.Terminate:    // about to quit: save the game now
                FileSystem.WriteAllText("frames.txt", _frames.ToString());
                break;
        }
    }
}
```

There is exactly one game per pdx. `PD.Game` returns the running instance.

## PD

The entry point and the raw C API. `PD.Run(factory)` registers the game (above).
`PD.RunEventHandler(handler)` replaces the `Game` class with a raw event handler, exactly like
writing the C `eventHandler` yourself. `PD.Api` is the `PlaydateAPI*` the game was started with, and
`PD.System`, `PD.Graphics`, `PD.Sprite`, `PD.Sound`, `PD.File`, `PD.Display`, `PD.Lua`, `PD.Json`,
`PD.Scoreboards`, `PD.Network` are its parts (see [native interop](native.md)).

```csharp
using System.Runtime.CompilerServices;

// A game without the Game class: a raw event handler, as in C.
public static unsafe class RawGame
{
    [ModuleInitializer]
    internal static void Register() => PD.RunEventHandler(OnEvent);

    private static int OnEvent(SystemEvent ev, uint arg)
    {
        if (ev == SystemEvent.Init)
            PD.System->setUpdateCallback(&Update, null);   // the C update callback
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Update(void* userdata)
    {
        Graphics.Clear(SolidColor.Black);
        return 1;
    }
}
```

The constants `PD.LCD_COLUMNS` (400), `PD.LCD_ROWS` (240), `PD.LCD_ROWSIZE` (52),
`PD.AUDIO_FRAMES_PER_CYCLE` (512) and `PD.NOTE_C4` (60) mirror the C macros.

## Sys: buttons

`Buttons` is a flags enum: `Left`, `Right`, `Up`, `Down`, `B`, `A`. Polling is the usual way:

| Member | Meaning |
|---|---|
| `Sys.IsPressed(b)` | `b` is held down now |
| `Sys.JustPressed(b)` | `b` went down since the last frame |
| `Sys.JustReleased(b)` | `b` went up since the last frame |
| `Sys.GetButtonState(out current, out pushed, out released)` | all three masks at once (cheapest) |
| `Sys.ButtonsCurrent`, `ButtonsPushed`, `ButtonsReleased` | the masks as properties |
| `Sys.SetButtonCallback(handler)` | every press and release as an event (catches taps shorter than a frame) |

```csharp
public sealed class InputDemo : Game
{
    private float _x = 200, _y = 120;
    private int _jumps;

    public override bool Update()
    {
        // Held buttons move continuously...
        if (Sys.IsPressed(Buttons.Left)) _x -= 3;
        if (Sys.IsPressed(Buttons.Right)) _x += 3;

        // ...while JustPressed fires once per press.
        if (Sys.JustPressed(Buttons.A))
            _jumps++;

        // Reading all masks in one call is cheaper when checking many buttons.
        Sys.GetButtonState(out Buttons held, out Buttons pushed, out Buttons released);
        if ((held & (Buttons.Up | Buttons.Down)) != 0)
            _y += (held & Buttons.Up) != 0 ? -3 : 3;

        Graphics.Clear(SolidColor.White);
        Graphics.FillEllipse((int)_x - 8, (int)_y - 8, 16, 16, 0, 0, SolidColor.Black);
        Graphics.DrawText("Jumps: " + _jumps, 4, 4);
        return true;
    }
}
```

Event style, for rhythm games or anything that must not miss a tap:

```csharp
public sealed class TapCounter : Game
{
    private int _taps;

    protected override void Initialize()
    {
        // Called once per button event from the previous frame; 'when' is in
        // Sys.CurrentTimeMilliseconds units. Return 0.
        Sys.SetButtonCallback((button, down, when) =>
        {
            if (button == Buttons.A && down)
                _taps++;
            return 0;
        });
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        Graphics.DrawText("Taps: " + _taps, 10, 10);
        return true;
    }
}
```

## Sys: crank and accelerometer

| Member | Meaning |
|---|---|
| `Sys.CrankAngle` | 0-360 degrees, 0 = pointing up, clockwise seen from the right side |
| `Sys.CrankChange` | degrees turned since it was last read (negative = backwards); read it once per frame |
| `Sys.IsCrankDocked` | the crank is folded away |
| `Sys.SetCrankSoundsDisabled(bool)` | silence the dock/undock clicks |
| `Sys.SetPeripheralsEnabled(Peripherals.Accelerometer)` | turn the accelerometer on (off by default) |
| `Sys.GetAccelerometer(out x, out y, out z)` | last reading in g; flat on a table it's about (0, 0, 1) |

```csharp
public sealed class CrankDemo : Game
{
    private float _ticksAccumulator;
    private int _menuIndex;

    protected override void Initialize() => Sys.SetPeripheralsEnabled(Peripherals.Accelerometer);

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);

        if (Sys.IsCrankDocked)
        {
            Graphics.DrawText("Undock the crank", 10, 10);
            return true;
        }

        // Lua's playdate.getCrankTicks(6): one step every 60 degrees.
        _ticksAccumulator += Sys.CrankChange;
        while (_ticksAccumulator >= 60) { _menuIndex++; _ticksAccumulator -= 60; }
        while (_ticksAccumulator <= -60) { _menuIndex--; _ticksAccumulator += 60; }

        // Draw a needle that follows the crank: 0 degrees = up, so subtract 90 for the math angle.
        float radians = (Sys.CrankAngle - 90) * MathF.PI / 180;
        Graphics.DrawLine(200, 120, 200 + (int)(MathF.Cos(radians) * 80), 120 + (int)(MathF.Sin(radians) * 80), 3, SolidColor.Black);

        Sys.GetAccelerometer(out float ax, out float ay, out float az);
        Graphics.DrawText("Item " + _menuIndex + "  tilt " + ax.ToString() + ", " + ay.ToString(), 10, 10);
        return true;
    }
}
```

## Sys: time and dates

| Member | Meaning |
|---|---|
| `Sys.CurrentTimeMilliseconds` | ms since an arbitrary start; for timing (pauses while the device sleeps) |
| `Sys.ElapsedTime` / `ResetElapsedTime()` | high-resolution stopwatch in seconds (µs accuracy) |
| `Sys.SecondsSinceEpoch`, `GetSecondsSinceEpoch(out ms)` | wall clock: seconds since 2000-01-01 UTC |
| `Sys.ConvertEpochToDateTime` / `ConvertDateTimeToEpoch` | seconds ↔ `PDDateTime` (year, month, day, weekday, hour, minute, second) |
| `Sys.TimezoneOffset` | local offset from UTC in seconds |
| `Sys.ShouldDisplay24HourTime` | the user's clock preference |
| `Sys.GetServerTime(callback)` | trusted time from Panic's server (the device clock can be changed by the user) |

SharpDate has no timer objects (Lua's `playdate.timer`); count frames or compare
`CurrentTimeMilliseconds`, as below.

```csharp
public sealed class ClockDemo : Game
{
    private uint _nextBlink;
    private bool _colon = true;

    public override bool Update()
    {
        // A 500 ms timer without a timer object.
        uint now = Sys.CurrentTimeMilliseconds;
        if (now >= _nextBlink)
        {
            _colon = !_colon;
            _nextBlink = now + 500;
        }

        // Local wall-clock time.
        uint local = (uint)(Sys.SecondsSinceEpoch + Sys.TimezoneOffset);
        PDDateTime t = Sys.ConvertEpochToDateTime(local);
        int hour = Sys.ShouldDisplay24HourTime ? t.Hour : (t.Hour + 11) % 12 + 1;

        Graphics.Clear(SolidColor.White);
        string minutes = ((int)t.Minute).ToString("D2");        // two digits; format strings work on int/uint/float
        Graphics.DrawText(hour + (_colon ? ":" : " ") + minutes, 170, 110);
        return true;
    }
}
```

Measuring how long code takes:

```csharp
Sys.ResetElapsedTime();
float sum = 0;
for (int i = 0; i < 10000; i++)
    sum += MathF.Sqrt(i);
Sys.Log("10000 square roots took " + (Sys.ElapsedTime * 1000).ToString() + " ms");
```

## Sys: logging and errors

`Sys.Log(text)` prints to the Simulator console or, on a device, to the USB serial port (Lua
`print`). `Sys.Error(text)` prints in red and stops the game. `Sys.DrawFPS(x, y)` draws the frame
rate. `Console.WriteLine` also works and goes to the same place as `Sys.Log`.

## MenuItem: the system menu

A game can add up to three items to the menu that opens with the Menu button. The callbacks run
after the menu closes (for checkmark and options items, only if the value changed).

| Method | Item |
|---|---|
| `Sys.AddMenuItem(title, callback)` | an action |
| `Sys.AddCheckmarkMenuItem(title, value, callback)` | on/off, read `item.Checked` |
| `Sys.AddOptionsMenuItem(title, options, callback)` | cycles through options, read `item.Value` (index) |
| `item.Title`, `item.Value`, `item.Checked` | read or change the item at any time |
| `item.Remove()`, `Sys.RemoveAllMenuItems()` | remove items |
| `Sys.SetMenuImage(bitmap, xOffset)` | a 400x240 picture beside the menu (call on `SystemEvent.Pause`) |

```csharp
public sealed class MenuDemo : Game
{
    private static readonly string[] Levels = { "Easy", "Normal", "Hard" };
    private MenuItem _sound, _difficulty;
    private int _restarts;
    private Bitmap _pauseImage;

    protected override void Initialize()
    {
        Sys.AddMenuItem("Restart", item => _restarts++);
        _sound = Sys.AddCheckmarkMenuItem("Sound", true, item => Sys.Log("sound " + item.Checked));
        _difficulty = Sys.AddOptionsMenuItem("Level", Levels, item => Sys.Log("level " + Levels[item.Value]));
        _difficulty.Value = 1;                       // start on "Normal"

        // Prepare a picture for the pause screen; only the left 200 pixels stay visible.
        _pauseImage = Bitmap.Create(400, 240, SolidColor.White);
        Graphics.PushContext(_pauseImage);
        Graphics.DrawText("Paused", 60, 110);
        Graphics.PopContext();
    }

    protected override void OnEvent(SystemEvent ev, uint arg)
    {
        if (ev == SystemEvent.Pause)
            Sys.SetMenuImage(_pauseImage);
    }

    public override bool Update()
    {
        Graphics.Clear(SolidColor.White);
        // Value is the index of the selected option; Title is the item's own label ("Level").
        Graphics.DrawText("Sound: " + (_sound.Checked ? "on" : "off") + "  Level: " + Levels[_difficulty.Value], 10, 10);
        Graphics.DrawText("Restarts: " + _restarts, 10, 30);
        return true;
    }
}
```

## Sys: device, launch and localization

| Member | Meaning |
|---|---|
| `Sys.SystemInfo` | `OSVersion` (e.g. 30102 = 3.1.2), `Language`, `PdxVersion` |
| `Sys.Language` | `English` or `Japanese` |
| `Sys.GetLocalizedText(key)` | looks the key up in `en.strings` / `jp.strings` in the pdx |
| `Sys.BatteryPercentage`, `BatteryVoltage`, `PowerStatus` | battery and charging state |
| `Sys.Volume` | system volume 0-1 |
| `Sys.Flipped`, `Sys.ReduceFlashing` | accessibility settings to respect |
| `Sys.SetAutoLockDisabled(bool)` | keep the device from locking after 3 minutes without input |
| `Sys.GetLaunchArgs(out path)` / `Sys.RestartGame(args)` | restart with fresh state and pass a string along |
| `Sys.ExitToLauncher()` | quit to the launcher |
| `Sys.SetSerialMessageCallback(handler)` | receive `msg` commands sent over USB (`!msg` in the Simulator console) |

```csharp
public sealed class LevelSelect : Game
{
    private string _level;

    protected override void Initialize()
    {
        // Restarting is the simplest way to reset all game state. The argument survives the restart.
        _level = Sys.GetLaunchArgs(out string pdxPath);
        if (string.IsNullOrEmpty(_level))
            _level = "1";

        // Localized title: Source/en.strings contains   "title" = "Space Duck"
        string title = Sys.GetLocalizedText("title") ?? "Space Duck";
        Sys.Log(title + " level " + _level + " from " + pdxPath);

        if (Sys.BatteryPercentage < 10 && (Sys.PowerStatus & PowerStatus.Charging) == 0)
            Sys.Log("Battery low");
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            Sys.RestartGame(_level == "1" ? "2" : "1");
        if (Sys.JustPressed(Buttons.B))
            Sys.ExitToLauncher();

        Graphics.Clear(SolidColor.White);
        Graphics.DrawText("Level " + _level + "   A: next level   B: quit", 10, 110);
        return true;
    }
}
```

## Display

| Member | Meaning |
|---|---|
| `Display.RefreshRate` / `SetRefreshRate` | target frames per second, default 30, max 50; 0 = as fast as possible |
| `Display.FPS` | the frame rate actually achieved |
| `Display.SetScale(n)` | 1, 2, 4 or 8: draw at a lower resolution, each pixel shown as n x n |
| `Display.Width`, `Display.Height` | size of the drawing area at the current scale |
| `Display.SetInverted(bool)` | white on black |
| `Display.SetMosaic(x, y)` | mosaic effect, 0-3 |
| `Display.SetFlipped(x, y)` | mirror the whole display |
| `Display.SetOffset(x, y)` | move the whole image (screen shake) |

```csharp
public sealed class DisplayDemo : Game
{
    private int _shake;

    protected override void Initialize()
    {
        Display.RefreshRate = 50;      // smoother animation, more battery
        Display.SetScale(2);           // draw in a 200x120 area, shown 2x
    }

    public override bool Update()
    {
        if (Sys.JustPressed(Buttons.A))
            _shake = 10;

        // Screen shake: offset the whole display for a few frames.
        int dx = _shake > 0 ? (_shake % 2 == 0 ? 3 : -3) : 0;
        Display.SetOffset(dx, 0);
        if (_shake > 0) _shake--;

        Graphics.Clear(SolidColor.White);
        Graphics.DrawRect(10, 10, Display.Width - 20, Display.Height - 20, SolidColor.Black);
        Graphics.DrawText(((int)Display.FPS).ToString(), 20, 20);
        return true;
    }
}
```
