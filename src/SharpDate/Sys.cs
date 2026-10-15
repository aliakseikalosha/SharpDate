// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// System services: logging, buttons, crank, accelerometer, time, the system menu, battery and device
    /// settings (C: <c>pd->system</c>; in Lua these are spread over <c>playdate.*</c>).
    /// </summary>
    public static unsafe class Sys
    {
        private static playdate_sys* Api => PD.System;

        // ---- Logging ----

        /// <summary>
        /// Prints a line to the console: the Simulator's console, or the serial port of a device (Lua:
        /// print).
        /// </summary>
        /// <param name="message">The text; may contain line breaks. null prints an empty line.</param>
        public static void Log(string message)
        {
            Variadic.LogToConsole(Utf8.Arg(message));
            if (DevCapture.Enabled)
                DevCapture.Log(message);
        }

        /// <summary>
        /// Prints a UTF-8 literal, e.g. <c>Sys.Log("hello"u8)</c>, without creating a string.
        /// </summary>
        /// <param name="utf8Message">UTF-8 text without a terminating zero, e.g. <c>"hello"u8</c>.</param>
        public static void Log(ReadOnlySpan<byte> utf8Message)
        {
            byte* p = stackalloc byte[utf8Message.Length + 1];
            utf8Message.CopyTo(new Span<byte>(p, utf8Message.Length));
            p[utf8Message.Length] = 0;
            Variadic.LogToConsole(p);
        }

        /// <summary>
        /// Reports an error: prints it in red and stops the game (the device shows the message). Use for
        /// unrecoverable problems; unhandled C# exceptions end up here too.
        /// </summary>
        /// <param name="message">The error text shown to the player and printed to the console.</param>
        public static void Error(string message)
        {
            if (DevCapture.Enabled)
                DevCapture.Log("ERROR: " + message);
            Variadic.Error(Utf8.Arg(message));
        }

        /// <summary>Draws the current frames per second at (x, y) (Lua: playdate.drawFPS).</summary>
        /// <param name="x">Left edge in screen pixels, 0-399.</param>
        /// <param name="y">Top edge in screen pixels, 0-239.</param>
        public static void DrawFPS(int x, int y) => Api->drawFPS(x, y);

        // ---- Time ----

        /// <summary>
        /// Milliseconds since an arbitrary point; consistent while the game runs, but it doesn't advance
        /// while the device sleeps. Use for timing, not for dates.
        /// </summary>
        public static uint CurrentTimeMilliseconds => Api->getCurrentTimeMilliseconds();

        /// <summary>Seconds (and milliseconds) since 2000-01-01 00:00 UTC, from the device clock.</summary>
        /// <param name="milliseconds">Receives the milliseconds within the current second, 0-999.</param>
        public static uint GetSecondsSinceEpoch(out uint milliseconds)
        {
            uint ms;
            uint s = Api->getSecondsSinceEpoch(&ms);
            milliseconds = ms;
            return s;
        }

        /// <summary>
        /// Seconds since 2000-01-01 00:00 UTC, from the device clock (which the user can change; see
        /// <see cref="GetServerTime"/>).
        /// </summary>
        public static uint SecondsSinceEpoch => Api->getSecondsSinceEpoch(null);

        /// <summary>
        /// Seconds since <see cref="ResetElapsedTime"/>, with microsecond resolution. Good for profiling.
        /// </summary>
        public static float ElapsedTime => Api->getElapsedTime();
        /// <summary>Restarts the high-resolution timer read by <see cref="ElapsedTime"/>.</summary>
        public static void ResetElapsedTime() => Api->resetElapsedTime();

        /// <summary>The time zone's offset from UTC in seconds.</summary>
        public static int TimezoneOffset => Api->getTimezoneOffset();
        /// <summary>True if the user prefers 24-hour time.</summary>
        public static bool ShouldDisplay24HourTime => Api->shouldDisplay24HourTime() != 0;

        /// <summary>
        /// Converts seconds since 2000-01-01 to a calendar date and time (UTC; add
        /// <see cref="TimezoneOffset"/> for local time).
        /// </summary>
        /// <param name="epoch">Seconds since 2000-01-01 00:00 UTC.</param>
        public static PDDateTime ConvertEpochToDateTime(uint epoch)
        {
            PDDateTime dt;
            Api->convertEpochToDateTime(epoch, &dt);
            return dt;
        }

        /// <summary>Converts a calendar date and time to seconds since 2000-01-01.</summary>
        /// <param name="dateTime">
        /// The date and time; month 1-12, day 1-31, hour 0-23, minute and second 0-59 (the weekday is
        /// ignored).
        /// </param>
        public static uint ConvertDateTimeToEpoch(PDDateTime dateTime) => Api->convertDateTimeToEpoch(&dateTime);

        /// <summary>
        /// Blocks the game for <paramref name="milliseconds"/>. Rarely useful; the frame loop keeps time for
        /// you.
        /// </summary>
        /// <param name="milliseconds">How long to block, in milliseconds.</param>
        public static void Delay(uint milliseconds) => Api->delay(milliseconds);

        // ---- Buttons ----

        /// <summary>Current button state plus buttons pushed and released since the last update.</summary>
        /// <param name="current">Buttons held down right now.</param>
        /// <param name="pushed">
        /// Buttons that went down since the previous frame (not missed even if already released).
        /// </param>
        /// <param name="released">Buttons that went up since the previous frame.</param>
        public static void GetButtonState(out Buttons current, out Buttons pushed, out Buttons released)
        {
            // PDButtons is a 1-byte enum on the device: the firmware writes only the low byte.
            Buttons c = 0, p = 0, r = 0;
            Api->getButtonState(&c, &p, &r);
            current = c;
            pushed = p;
            released = r;
        }

        /// <summary>Buttons currently held down.</summary>
        public static Buttons ButtonsCurrent
        {
            get
            {
                GetButtonState(out Buttons current, out _, out _);
                return current;
            }
        }

        /// <summary>Buttons pushed since the previous update.</summary>
        public static Buttons ButtonsPushed
        {
            get
            {
                GetButtonState(out _, out Buttons pushed, out _);
                return pushed;
            }
        }

        /// <summary>Buttons released since the previous update.</summary>
        public static Buttons ButtonsReleased
        {
            get
            {
                GetButtonState(out _, out _, out Buttons released);
                return released;
            }
        }

        /// <summary>
        /// True while <paramref name="button"/> is held (any of them, if several are combined).
        /// </summary>
        /// <param name="button">One button, or several combined with | (true if any of them is held).</param>
        public static bool IsPressed(Buttons button) => (ButtonsCurrent & button) != 0;
        /// <summary>
        /// True if <paramref name="button"/> went down since the last frame. Use this for actions like
        /// jumping.
        /// </summary>
        /// <param name="button">
        /// One button, or several combined with | (true if any of them was pushed).
        /// </param>
        public static bool JustPressed(Buttons button) => (ButtonsPushed & button) != 0;
        /// <summary>True if <paramref name="button"/> went up since the last frame.</summary>
        /// <param name="button">
        /// One button, or several combined with | (true if any of them was released).
        /// </param>
        public static bool JustReleased(Buttons button) => (ButtonsReleased & button) != 0;

        /// <summary>
        /// A button event handler for <see cref="SetButtonCallback"/>: the button, whether it went down, and
        /// the time of the event in milliseconds (<see cref="CurrentTimeMilliseconds"/> scale). Return 0.
        /// </summary>
        /// <param name="button">The single button the event is about.</param>
        /// <param name="down">True when the button went down, false when it went up.</param>
        /// <param name="when">
        /// Time of the event in milliseconds, on the <see cref="CurrentTimeMilliseconds"/> clock.
        /// </param>
        /// <returns>0 (non-zero is reserved for reporting an error).</returns>
        public delegate int ButtonHandler(Buttons button, bool down, uint when);

        private static ButtonHandler s_buttonHandler;

        /// <summary>
        /// Receives every button press and release as an event instead of polling with
        /// <see cref="GetButtonState"/>, so quick taps between frames aren't missed. The handler is called
        /// once per event that happened during the previous frame. Pass null to remove the handler.
        /// </summary>
        /// <param name="queueSize">How many events to buffer per frame (5 is enough at 30 fps; raise it for lower frame rates).</param>
        /// <param name="handler">
        /// Called once per event; null removes the callback and returns to polling only.
        /// </param>
        public static void SetButtonCallback(ButtonHandler handler, int queueSize = 5)
        {
            s_buttonHandler = handler;
            if (handler == null)
                Api->setButtonCallback(null, null, 0);
            else
                Api->setButtonCallback(&ButtonTrampoline, null, queueSize);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int ButtonTrampoline(Buttons button, int down, uint when, void* userdata)
            => s_buttonHandler?.Invoke(button, down != 0, when) ?? 0;

        // ---- Crank & accelerometer ----

        /// <summary>
        /// The crank's position in degrees, 0-360: 0 points up, and the value grows as the crank turns
        /// clockwise (seen from the right side of the device).
        /// </summary>
        public static float CrankAngle => Api->getCrankAngle();

        /// <summary>
        /// Degrees the crank turned since the last time this was read (negative = counter-clockwise). Read it
        /// once per frame.
        /// </summary>
        public static float CrankChange => Api->getCrankChange();

        /// <summary>True when the crank is folded into the device.</summary>
        public static bool IsCrankDocked => Api->isCrankDocked() != 0;

        /// <summary>Turns off the system sounds played when the crank is docked or undocked.</summary>
        /// <returns>The previous setting.</returns>
        /// <param name="disabled">True to silence the dock/undock sounds, false to restore them.</param>
        public static bool SetCrankSoundsDisabled(bool disabled) => Api->setCrankSoundsDisabled(disabled ? 1 : 0) != 0;

        /// <summary>Switches sensors on or off. The accelerometer is off by default to save power.</summary>
        /// <param name="mask">
        /// The peripherals to switch on; everything not in the mask is switched off.
        /// <see cref="Peripherals.None"/> turns everything off.
        /// </param>
        public static void SetPeripheralsEnabled(Peripherals mask) => Api->setPeripheralsEnabled(mask);

        /// <summary>
        /// The last accelerometer reading in g on each axis (enable it first with
        /// <see cref="SetPeripheralsEnabled"/>). Lying flat, face up, it reads about (0, 0, 1).
        /// </summary>
        /// <param name="x">
        /// Acceleration along the screen's horizontal axis in g (about -1 to 1 when held still; more when
        /// shaken).
        /// </param>
        /// <param name="y">Acceleration along the screen's vertical axis in g.</param>
        /// <param name="z">Acceleration perpendicular to the screen in g (about 1 lying face up).</param>
        public static void GetAccelerometer(out float x, out float y, out float z)
        {
            float ax, ay, az;
            Api->getAccelerometer(&ax, &ay, &az);
            x = ax;
            y = ay;
            z = az;
        }

        // ---- Device ----

        /// <summary>The device's language.</summary>
        public static Language Language => Api->getLanguage();
        /// <summary>True if the user turned on the "upside down" (left-handed) setting.</summary>
        public static bool Flipped => Api->getFlipped() != 0;
        /// <summary>True if the user asked for reduced flashing; avoid strobing effects then.</summary>
        public static bool ReduceFlashing => Api->getReduceFlashing() != 0;
        /// <summary>
        /// Stops the device from locking after 3 minutes without input (for games where the player may only
        /// watch). Re-enable it when that part of the game ends.
        /// </summary>
        /// <param name="disabled">
        /// True to stop the 3-minute auto lock, false to enable it again (which restarts the timer).
        /// </param>
        public static void SetAutoLockDisabled(bool disabled) => Api->setAutoLockDisabled(disabled ? 1 : 0);
        /// <summary>Battery charge, 0 (empty) to 100 (full).</summary>
        public static float BatteryPercentage => Api->getBatteryPercentage();
        /// <summary>Battery voltage.</summary>
        public static float BatteryVoltage => Api->getBatteryVoltage();
        /// <summary>Whether the device is charging and where its power comes from.</summary>
        public static PowerStatus PowerStatus => Api->getPowerStatus();
        /// <summary>
        /// The system volume, 0-1. If it's 0, the game can skip waiting for sounds to finish.
        /// </summary>
        public static float Volume => Api->getVolume();
        /// <summary>OS version, language and the SDK version the game was built with.</summary>
        public static PDInfo SystemInfo => *Api->getSystemInfo();

        /// <summary>
        /// Flushes the CPU instruction cache; only needed by code that generates machine code at run time.
        /// </summary>
        public static void ClearICache() => Api->clearICache();

        /// <summary>
        /// Looks up <paramref name="key"/> in the game's .strings file for the language (en.strings,
        /// jp.strings compiled into the pdx).
        /// </summary>
        /// <returns>The text, or null if the key or file doesn't exist.</returns>
        /// <param name="key">The key as written in the .strings file.</param>
        /// <param name="language">
        /// Which language's file to use; <see cref="Playdate.Language.System"/> (default) uses the device
        /// language and falls back to the other one.
        /// </param>
        public static string GetLocalizedText(string key, Language language = Language.System)
        {
            byte* text = Api->getLocalizedText(Utf8.Arg(key), language);
            string result = Utf8.ToString(text);
            if (text != null)
                Api->realloc(text, 0);   // the caller owns the returned C string
            return result;
        }

        /// <summary>
        /// The arguments the game was started with (from the Simulator command line, the device's run command
        /// or <see cref="RestartGame"/>), and the game's path.
        /// </summary>
        /// <param name="path">Receives the path of the running game's pdx.</param>
        /// <returns>The launch argument string, or null if there was none.</returns>
        public static string GetLaunchArgs(out string path)
        {
            byte* outPath;
            string args = Utf8.ToString(Api->getLaunchArgs(&outPath));
            path = Utf8.ToString(outPath);
            return args;
        }

        /// <summary>
        /// Restarts the game with a fresh runtime state; <paramref name="launchArgs"/> is available
        /// afterwards from <see cref="GetLaunchArgs"/>.
        /// </summary>
        /// <param name="launchArgs">Any text to hand to the restarted game (null for none).</param>
        public static void RestartGame(string launchArgs = null) => Api->restartGame(Utf8.Arg(launchArgs));

        /// <summary>
        /// Quits to the launcher; the game receives <see cref="SystemEvent.Terminate"/> first.
        /// </summary>
        public static void ExitToLauncher() => Api->exitToLauncher();

        private static readonly System.Collections.Generic.Queue<Action<string, string>> s_serverTime = new System.Collections.Generic.Queue<Action<string, string>>();

        /// <summary>
        /// Asks Panic's server for the current time, a clock the user can't change. The callback receives the
        /// time as text (seconds since 2000-01-01 UTC) or an error message. Needs Wi-Fi.
        /// </summary>
        /// <param name="callback">
        /// Receives (time, error): the time as decimal seconds since 2000-01-01 UTC and null, or null and an
        /// error message. Runs on the main thread.
        /// </param>
        public static void GetServerTime(Action<string, string> callback)
        {
            s_serverTime.Enqueue(callback);
            Api->getServerTime(&ServerTimeCallback);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void ServerTimeCallback(byte* time, byte* error)
        {
            if (s_serverTime.Count > 0)
                s_serverTime.Dequeue()?.Invoke(Utf8.ToString(time), Utf8.ToString(error));
        }

        private static Action<string> s_serialMessage;

        /// <summary>
        /// Receives messages sent to the device over USB with the <c>msg</c> serial command (in the
        /// Simulator: type <c>!msg text</c> in the console). Pass null to stop.
        /// </summary>
        /// <param name="callback">Receives each message text; null stops receiving.</param>
        public static void SetSerialMessageCallback(Action<string> callback)
        {
            s_serialMessage = callback;
            Api->setSerialMessageCallback(callback == null ? null : &SerialMessageCallback);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void SerialMessageCallback(byte* data) => s_serialMessage?.Invoke(Utf8.ToString(data));

        // ---- Menu ----

        /// <summary>
        /// Sets the 400x240 image shown next to the system menu while the game is paused; keep the important
        /// part in the left 200 pixels. Call it when <see cref="SystemEvent.Pause"/> arrives.
        /// </summary>
        /// <param name="xOffset">0-200: how far the image slides left as the menu opens.</param>
        /// <param name="bitmap">
        /// A 400x240 bitmap, or a null bitmap (<c>default</c>) to remove the image. The game keeps ownership;
        /// keep it alive while it is set.
        /// </param>
        public static void SetMenuImage(Bitmap bitmap, int xOffset = 0) => Api->setMenuImage(bitmap.Handle, xOffset);

        /// <summary>
        /// Adds an action item to the system menu (at most three custom items in total). Choosing it closes
        /// the menu and runs <paramref name="callback"/>.
        /// </summary>
        /// <param name="title">Text shown in the menu; keep it short (about 10 characters fit).</param>
        /// <param name="callback">
        /// Called with the item after the menu closes when the player chose it; may be null.
        /// </param>
        /// <returns>
        /// The item, or one whose <see cref="MenuItem.NativeHandle"/> is null if the menu is full (3 items).
        /// </returns>
        public static MenuItem AddMenuItem(string title, Action<MenuItem> callback)
        {
            var item = new MenuItem(callback);
            item.Handle = Api->addMenuItem(Utf8.Arg(title), &MenuItem.Trampoline, (void*)GCHandle.ToIntPtr(item.Self));
            return item;
        }

        /// <summary>
        /// Adds an on/off item to the system menu. <paramref name="callback"/> runs when the menu closes if
        /// the value changed; read <see cref="MenuItem.Checked"/>.
        /// </summary>
        /// <param name="title">Text shown in the menu; keep it short.</param>
        /// <param name="value">Initial state: true = checked.</param>
        /// <param name="callback">
        /// Called with the item after the menu closes if the player changed it; may be null.
        /// </param>
        /// <returns>
        /// The item, or one whose <see cref="MenuItem.NativeHandle"/> is null if the menu is full (3 items).
        /// </returns>
        public static MenuItem AddCheckmarkMenuItem(string title, bool value, Action<MenuItem> callback)
        {
            var item = new MenuItem(callback);
            item.Handle = Api->addCheckmarkMenuItem(Utf8.Arg(title), value ? 1 : 0, &MenuItem.Trampoline, (void*)GCHandle.ToIntPtr(item.Self));
            return item;
        }

        /// <summary>
        /// Adds an item that cycles through <paramref name="options"/> (keep them short).
        /// <paramref name="callback"/> runs when the menu closes; <see cref="MenuItem.Value"/> is the
        /// selected index.
        /// </summary>
        /// <param name="title">Text shown in the menu; keep it short.</param>
        /// <param name="options">
        /// The choices, at least one; keep them short (title and option share one line).
        /// </param>
        /// <param name="callback">
        /// Called with the item after the menu closes if the player changed it; may be null.
        /// </param>
        /// <returns>
        /// The item, or one whose <see cref="MenuItem.NativeHandle"/> is null if the menu is full (3 items).
        /// </returns>
        public static MenuItem AddOptionsMenuItem(string title, string[] options, Action<MenuItem> callback)
        {
            var item = new MenuItem(callback);
            byte** titles = (byte**)NativeMemory.Alloc((nuint)(sizeof(byte*) * options.Length));
            for (int i = 0; i < options.Length; i++)
                titles[i] = Utf8.Alloc(options[i]);
            item.Handle = Api->addOptionsMenuItem(Utf8.Arg(title), titles, options.Length, &MenuItem.Trampoline, (void*)GCHandle.ToIntPtr(item.Self));
            item.OwnedOptions = titles;
            item.OwnedOptionCount = options.Length;
            return item;
        }

        /// <summary>Removes all the game's menu items.</summary>
        public static void RemoveAllMenuItems()
        {
            Api->removeAllMenuItems();
            MenuItem.ReleaseAll();
        }
    }

    /// <summary>
    /// An item the game added to the system menu (C: PDMenuItem, Lua: playdate.menu.item). Keeps itself alive
    /// until removed.
    /// </summary>
    public sealed unsafe class MenuItem
    {
        private static System.Collections.Generic.List<MenuItem> s_items = new System.Collections.Generic.List<MenuItem>();

        internal PDMenuItem* Handle;
        internal GCHandle Self;
        internal byte** OwnedOptions;
        internal int OwnedOptionCount;
        private readonly Action<MenuItem> _callback;

        internal MenuItem(Action<MenuItem> callback)
        {
            _callback = callback;
            Self = GCHandle.Alloc(this);
            s_items.Add(this);
        }

        /// <summary>The native item, for the raw API.</summary>
        public PDMenuItem* NativeHandle => Handle;

        /// <summary>
        /// Checkmark items: 1 = checked, 0 = unchecked. Options items: the selected index, 0 to
        /// options.Length - 1.
        /// </summary>
        public int Value
        {
            get => PD.System->getMenuItemValue(Handle);
            set => PD.System->setMenuItemValue(Handle, value);
        }

        /// <summary>Checkmark state (Value != 0).</summary>
        public bool Checked
        {
            get => Value != 0;
            set => Value = value ? 1 : 0;
        }

        /// <summary>The text shown in the menu.</summary>
        public string Title
        {
            get => Utf8.ToString(PD.System->getMenuItemTitle(Handle));
            set => PD.System->setMenuItemTitle(Handle, Utf8.Arg(value));
        }

        /// <summary>Removes the item from the menu.</summary>
        public void Remove()
        {
            if (Handle == null)
                return;
            PD.System->removeMenuItem(Handle);
            Release();
        }

        private void Release()
        {
            Handle = null;
            if (Self.IsAllocated)
                Self.Free();
            for (int i = 0; i < OwnedOptionCount; i++)
                Utf8.Free(OwnedOptions[i]);
            NativeMemory.Free(OwnedOptions);
            OwnedOptions = null;
            OwnedOptionCount = 0;
            s_items.Remove(this);
        }

        internal static void ReleaseAll()
        {
            while (s_items.Count > 0)
                s_items[s_items.Count - 1].Release();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static void Trampoline(void* userdata)
        {
            var item = (MenuItem)GCHandle.FromIntPtr((IntPtr)userdata).Target;
            item._callback?.Invoke(item);
        }
    }
}
