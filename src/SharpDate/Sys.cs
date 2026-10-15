using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>Wraps <c>pd->system</c>: input, time, logging, menu, battery and device settings.</summary>
    public static unsafe class Sys
    {
        private static playdate_sys* Api => PD.System;

        // ---- Logging ----

        /// <summary>Writes a line to the console (pd->system->logToConsole).</summary>
        public static void Log(string message)
        {
            Variadic.LogToConsole(Utf8.Arg(message));
            if (DevCapture.Enabled)
                DevCapture.Log(message);
        }

        /// <summary>Writes a line to the console from a UTF-8 literal, e.g. <c>Sys.Log("hello"u8)</c>.</summary>
        public static void Log(ReadOnlySpan<byte> utf8Message)
        {
            byte* p = stackalloc byte[utf8Message.Length + 1];
            utf8Message.CopyTo(new Span<byte>(p, utf8Message.Length));
            p[utf8Message.Length] = 0;
            Variadic.LogToConsole(p);
        }

        /// <summary>Logs an error in red and pauses execution (pd->system->error).</summary>
        public static void Error(string message)
        {
            if (DevCapture.Enabled)
                DevCapture.Log("ERROR: " + message);
            Variadic.Error(Utf8.Arg(message));
        }

        /// <summary>Draws the current frame rate at (x, y).</summary>
        public static void DrawFPS(int x, int y) => Api->drawFPS(x, y);

        // ---- Time ----

        public static uint CurrentTimeMilliseconds => Api->getCurrentTimeMilliseconds();

        public static uint GetSecondsSinceEpoch(out uint milliseconds)
        {
            uint ms;
            uint s = Api->getSecondsSinceEpoch(&ms);
            milliseconds = ms;
            return s;
        }

        public static uint SecondsSinceEpoch => Api->getSecondsSinceEpoch(null);

        /// <summary>Seconds since <see cref="ResetElapsedTime"/> was last called (high resolution).</summary>
        public static float ElapsedTime => Api->getElapsedTime();
        public static void ResetElapsedTime() => Api->resetElapsedTime();

        public static int TimezoneOffset => Api->getTimezoneOffset();
        public static bool ShouldDisplay24HourTime => Api->shouldDisplay24HourTime() != 0;

        public static PDDateTime ConvertEpochToDateTime(uint epoch)
        {
            PDDateTime dt;
            Api->convertEpochToDateTime(epoch, &dt);
            return dt;
        }

        public static uint ConvertDateTimeToEpoch(PDDateTime dateTime) => Api->convertDateTimeToEpoch(&dateTime);

        public static void Delay(uint milliseconds) => Api->delay(milliseconds);

        // ---- Buttons ----

        /// <summary>Current button state plus buttons pushed and released since the last update.</summary>
        public static void GetButtonState(out Buttons current, out Buttons pushed, out Buttons released)
        {
            Buttons c, p, r;
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

        public static bool IsPressed(Buttons button) => (ButtonsCurrent & button) != 0;
        public static bool JustPressed(Buttons button) => (ButtonsPushed & button) != 0;
        public static bool JustReleased(Buttons button) => (ButtonsReleased & button) != 0;

        /// <summary>Handler for <see cref="SetButtonCallback"/>: (button, down, when) -> return non-zero to stop processing.</summary>
        public delegate int ButtonHandler(Buttons button, bool down, uint when);

        private static ButtonHandler s_buttonHandler;

        /// <summary>
        /// Receives button events as they happen instead of polling (pd->system->setButtonCallback).
        /// Pass null to remove the callback.
        /// </summary>
        public static void SetButtonCallback(ButtonHandler handler, int queueSize = 0)
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

        /// <summary>Crank angle in degrees (0 = up, increasing clockwise).</summary>
        public static float CrankAngle => Api->getCrankAngle();

        /// <summary>Change in crank angle since the last call, in degrees.</summary>
        public static float CrankChange => Api->getCrankChange();

        public static bool IsCrankDocked => Api->isCrankDocked() != 0;

        /// <summary>Disables the crank docking sounds; returns the previous setting.</summary>
        public static bool SetCrankSoundsDisabled(bool disabled) => Api->setCrankSoundsDisabled(disabled ? 1 : 0) != 0;

        public static void SetPeripheralsEnabled(Peripherals mask) => Api->setPeripheralsEnabled(mask);

        public static void GetAccelerometer(out float x, out float y, out float z)
        {
            float ax, ay, az;
            Api->getAccelerometer(&ax, &ay, &az);
            x = ax;
            y = ay;
            z = az;
        }

        // ---- Device ----

        public static Language Language => Api->getLanguage();
        public static bool Flipped => Api->getFlipped() != 0;
        public static bool ReduceFlashing => Api->getReduceFlashing() != 0;
        public static void SetAutoLockDisabled(bool disabled) => Api->setAutoLockDisabled(disabled ? 1 : 0);
        public static float BatteryPercentage => Api->getBatteryPercentage();
        public static float BatteryVoltage => Api->getBatteryVoltage();
        public static PowerStatus PowerStatus => Api->getPowerStatus();
        public static float Volume => Api->getVolume();
        public static PDInfo SystemInfo => *Api->getSystemInfo();

        /// <summary>Clears the CPU instruction cache (needed after writing code to memory).</summary>
        public static void ClearICache() => Api->clearICache();

        public static string GetLocalizedText(string key, Language language = Language.System)
            => Utf8.ToString(Api->getLocalizedText(Utf8.Arg(key), language));

        public static string GetLaunchArgs(out string path)
        {
            byte* outPath;
            string args = Utf8.ToString(Api->getLaunchArgs(&outPath));
            path = Utf8.ToString(outPath);
            return args;
        }

        public static void RestartGame(string launchArgs = null) => Api->restartGame(Utf8.Arg(launchArgs));

        public static void ExitToLauncher() => Api->exitToLauncher();

        // ---- Menu ----

        /// <summary>Sets the image shown behind the system menu (400x240, scrolled by xOffset).</summary>
        public static void SetMenuImage(Bitmap bitmap, int xOffset = 0) => Api->setMenuImage(bitmap.Handle, xOffset);

        /// <summary>Adds a menu item that invokes <paramref name="callback"/> when selected.</summary>
        public static MenuItem AddMenuItem(string title, Action<MenuItem> callback)
        {
            var item = new MenuItem(callback);
            item.Handle = Api->addMenuItem(Utf8.Arg(title), &MenuItem.Trampoline, (void*)GCHandle.ToIntPtr(item.Self));
            return item;
        }

        /// <summary>Adds a checkmark menu item; <see cref="MenuItem.Value"/> is 0 or 1.</summary>
        public static MenuItem AddCheckmarkMenuItem(string title, bool value, Action<MenuItem> callback)
        {
            var item = new MenuItem(callback);
            item.Handle = Api->addCheckmarkMenuItem(Utf8.Arg(title), value ? 1 : 0, &MenuItem.Trampoline, (void*)GCHandle.ToIntPtr(item.Self));
            return item;
        }

        /// <summary>Adds an options menu item; <see cref="MenuItem.Value"/> is the selected option index.</summary>
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

        public static void RemoveAllMenuItems()
        {
            Api->removeAllMenuItems();
            MenuItem.ReleaseAll();
        }
    }

    /// <summary>A system menu item created with <see cref="Sys.AddMenuItem"/> and friends.</summary>
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

        public PDMenuItem* NativeHandle => Handle;

        /// <summary>Checkmark state (0/1) or selected option index.</summary>
        public int Value
        {
            get => PD.System->getMenuItemValue(Handle);
            set => PD.System->setMenuItemValue(Handle, value);
        }

        public bool Checked
        {
            get => Value != 0;
            set => Value = value ? 1 : 0;
        }

        public string Title
        {
            get => Utf8.ToString(PD.System->getMenuItemTitle(Handle));
            set => PD.System->setMenuItemTitle(Handle, Utf8.Arg(value));
        }

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
