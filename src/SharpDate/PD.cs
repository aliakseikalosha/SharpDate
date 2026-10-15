using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    /// <summary>
    /// Access to the raw Playdate C API (<c>PlaydateAPI*</c>) and the game entry point.
    /// </summary>
    /// <remarks>
    /// Everything in the C API is reachable through <see cref="Api"/>, e.g.
    /// <c>PD.Api->graphics->clear((nuint)SolidColor.White)</c>. The static classes
    /// <see cref="Graphics"/>, <see cref="Sys"/>, <see cref="Sound"/>, ... wrap it in idiomatic C#.
    /// </remarks>
    public static unsafe class PD
    {
        public const int LCD_COLUMNS = 400;
        public const int LCD_ROWS = 240;
        public const int LCD_ROWSIZE = 52;
        public const int AUDIO_FRAMES_PER_CYCLE = 512;
        public const float NOTE_C4 = 60;

        /// <summary>The PlaydateAPI pointer passed to the game at startup.</summary>
        public static PlaydateAPI* Api { get; private set; }

        public static playdate_sys* System => Api->system;
        public static playdate_file* File => Api->file;
        public static playdate_graphics* Graphics => Api->graphics;
        public static playdate_sprite* Sprite => Api->sprite;
        public static playdate_display* Display => Api->display;
        public static playdate_sound* Sound => Api->sound;
        public static playdate_lua* Lua => Api->lua;
        public static playdate_json* Json => Api->json;
        public static playdate_scoreboards* Scoreboards => Api->scoreboards;
        public static playdate_network* Network => Api->network;

        private static Func<Game> s_factory;
        private static Func<SystemEvent, uint, int> s_eventHandler;
        private static Game s_game;

        /// <summary>The running game, once <see cref="SystemEvent.Init"/> has been handled.</summary>
        public static Game Game => s_game;

        /// <summary>
        /// Registers the game. Call from a <c>[ModuleInitializer]</c> method:
        /// <code>
        /// [ModuleInitializer] internal static void Main() => PD.Run(() => new MyGame());
        /// </code>
        /// The factory runs when the Playdate sends <see cref="SystemEvent.Init"/>.
        /// </summary>
        public static void Run(Func<Game> factory) => s_factory = factory;

        /// <summary>
        /// Registers a raw event handler, equivalent to the C <c>eventHandler</c> function,
        /// for games that do not use the <see cref="Playdate.Game"/> class.
        /// </summary>
        public static void RunEventHandler(Func<SystemEvent, uint, int> handler) => s_eventHandler = handler;

        // Called by the native runtime's eventHandler (sdrt.c).
        [UnmanagedCallersOnly(EntryPoint = "SharpDate_EventHandler")]
        private static int EventHandler(PlaydateAPI* pd, SystemEvent ev, uint arg)
        {
            Api = pd;

            if (s_eventHandler != null)
                return s_eventHandler(ev, arg);

            if (ev == SystemEvent.Init)
            {
                DevCapture.Initialize();
                if (DevCapture.Enabled)
                    DevCapture.Log("[sharpdate] init");
                if (s_factory == null)
                {
                    Sys.Error("SharpDate: no game registered. Call PD.Run(() => new MyGame()) from a [ModuleInitializer].");
                    return 0;
                }
                s_game = s_factory();
                s_game.InitializeInternal();
                return 0;
            }

            if (s_game == null)
                return 0;

            if (ev == SystemEvent.InitLua)
                s_game.InitializeLuaInternal();
            else
                s_game.OnEvent(ev, arg);
            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static int UpdateCallback(void* userdata)
        {
            int result = s_game.Update() ? 1 : 0;
            if (DevCapture.Enabled)
                DevCapture.AfterFrame();
            return result;
        }
    }

    /// <summary>
    /// Development aid for automated testing. When the game's data folder contains a file named
    /// <c>sharpdate_capture</c> holding a frame number N, the frame buffer after frame N is written to
    /// <c>sharpdate_capture.pbm</c>, and <see cref="Sys.Log(string)"/> output is mirrored to
    /// <c>sharpdate_log.txt</c>. Without the trigger file this does nothing.
    /// </summary>
    internal static unsafe class DevCapture
    {
        internal static bool Enabled;
        private static int s_captureFrame;
        private static int s_frame;

        internal static void Initialize()
        {
            string trigger = FileSystem.ReadAllText("sharpdate_capture");
            if (trigger == null)
                return;
            Enabled = true;
            if (!int.TryParse(trigger.Trim(), out s_captureFrame))
                s_captureFrame = 30;
            FileSystem.WriteAllText("sharpdate_log.txt", "");
        }

        internal static void Log(string message)
        {
            PDFile file = FileSystem.Open("sharpdate_log.txt", FileOptions.Append);
            if (file.IsNull)
                return;
            file.Write(message);
            file.Write("\n");
            file.Dispose();
        }

        internal static void AfterFrame()
        {
            if (++s_frame == 1)
                Log("[sharpdate] first frame");
            if (s_frame != s_captureFrame)
                return;
            // PBM (P4): 1 = black, rows padded to bytes. Playdate frames use 1 = white.
            byte[] header = System.Text.Encoding.UTF8.GetBytes("P4\n400 240\n");
            byte[] image = new byte[header.Length + 50 * 240];
            Array.Copy(header, image, header.Length);
            byte* frame = PD.Graphics->getFrame();
            for (int y = 0; y < 240; y++)
                for (int x = 0; x < 50; x++)
                    image[header.Length + y * 50 + x] = (byte)~frame[y * PD.LCD_ROWSIZE + x];
            FileSystem.WriteAllBytes("sharpdate_capture.pbm", image);
            Log("[sharpdate] captured frame " + s_frame);
        }
    }

    /// <summary>
    /// Base class for a Playdate game. Override <see cref="Update"/> (called every frame) and
    /// optionally <see cref="Initialize"/>, <see cref="InitializeLua"/> and <see cref="OnEvent"/>.
    /// </summary>
    public abstract unsafe class Game
    {
        /// <summary>
        /// When true (the default) the game installs a C update callback, like a pure-C game.
        /// Return false for games whose run loop is written in Lua (main.lua) and that only
        /// register functions with <see cref="Lua"/>.
        /// </summary>
        protected virtual bool UsesUpdateCallback => true;

        /// <summary>Called once at startup (kEventInit). The Playdate API is available.</summary>
        protected virtual void Initialize() { }

        /// <summary>Called at kEventInitLua for games that use Lua; register Lua functions here.</summary>
        protected virtual void InitializeLua() { }

        /// <summary>
        /// Called every frame. Return true to tell the system to update the display
        /// (equivalent to returning non-zero from a C update callback).
        /// </summary>
        public virtual bool Update() => false;

        /// <summary>Called for other system events (pause, resume, lock, terminate, key presses in the Simulator, ...).</summary>
        protected internal virtual void OnEvent(SystemEvent ev, uint arg) { }

        internal void InitializeInternal()
        {
            Initialize();
            // Note: if an update callback is set in the init handler, the system assumes the game is pure C.
            if (UsesUpdateCallback)
                PD.System->setUpdateCallback(&PD.UpdateCallback, null);
        }

        internal void InitializeLuaInternal() => InitializeLua();
    }
}
