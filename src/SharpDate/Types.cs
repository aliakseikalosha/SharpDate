// SPDX-License-Identifier: 0BSD
// This file restates interface facts from the Playdate SDK headers (names, values, layouts);
// those remain Panic's. SharpDate's own contributions to it are 0BSD. See LICENSING.md.

using System;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    // Enums and plain data structs of the Playdate C API. Values and layouts match pd_api*.h.

    /// <summary>
    /// A system event sent to the game (C: PDSystemEvent). Received in <see cref="Game.OnEvent"/>;
    /// <see cref="Init"/> and <see cref="InitLua"/> are handled by SharpDate and call <c>Initialize</c> /
    /// <c>InitializeLua</c> instead.
    /// </summary>
    public enum SystemEvent
    {
        /// <summary>
        /// The game is starting. The API is ready; SharpDate creates your <see cref="Game"/> here.
        /// </summary>
        Init,
        /// <summary>Lua is ready (only for games with a main.lua). Register Lua functions now.</summary>
        InitLua,
        /// <summary>The device is being locked.</summary>
        Lock,
        /// <summary>The device was unlocked.</summary>
        Unlock,
        /// <summary>The system menu opened. A good moment to call <see cref="Sys.SetMenuImage"/>.</summary>
        Pause,
        /// <summary>The system menu closed.</summary>
        Resume,
        /// <summary>
        /// The game is about to quit (back to the launcher or the device shutting down). Save state now.
        /// </summary>
        Terminate,
        /// <summary>
        /// Simulator only: a key was pressed on the computer's keyboard; <c>arg</c> is the key code.
        /// </summary>
        KeyPressed,
        /// <summary>Simulator only: a key was released; <c>arg</c> is the key code.</summary>
        KeyReleased,
        /// <summary>The battery is low.</summary>
        LowPower,
        /// <summary>Mirroring (Playdate Mirror app) started.</summary>
        MirrorStarted,
        /// <summary>Mirroring ended.</summary>
        MirrorEnded,
    }

    /// <summary>
    /// The Playdate's buttons as bit flags (C: PDButtons, Lua: playdate.kButtonLeft ...). Combine with | and
    /// test with <c>(buttons &amp; Buttons.A) != 0</c>, or use <see cref="Sys.IsPressed"/>.
    /// </summary>
    [Flags]
    public enum Buttons
    {
        /// <summary>No button.</summary>
        None = 0,
        /// <summary>D-pad left.</summary>
        Left = 1 << 0,
        /// <summary>D-pad right.</summary>
        Right = 1 << 1,
        /// <summary>D-pad up.</summary>
        Up = 1 << 2,
        /// <summary>D-pad down.</summary>
        Down = 1 << 3,
        /// <summary>The B button (left of A).</summary>
        B = 1 << 4,
        /// <summary>The A button.</summary>
        A = 1 << 5,
    }

    /// <summary>
    /// System language (C: PDLanguage). <see cref="System"/> is only meaningful for
    /// <see cref="Sys.GetLocalizedText"/>, meaning "the language the device is set to".
    /// </summary>
    public enum Language
    {
        /// <summary>English.</summary>
        English = 0,
        /// <summary>Japanese.</summary>
        Japanese,
        /// <summary>The device's current language (lookups only).</summary>
        System,
    }

    /// <summary>
    /// Immediate answer to a permission request (network, microphone). <see cref="Ask"/> means the user is
    /// being asked; the final answer arrives in the callback.
    /// </summary>
    public enum AccessReply
    {
        /// <summary>The user is being asked; wait for the callback.</summary>
        Ask,
        /// <summary>Access was denied earlier (the callback still runs with false).</summary>
        Deny,
        /// <summary>Access was granted earlier (the callback still runs with true).</summary>
        Allow,
    }

    /// <summary>
    /// Optional sensors to switch on with <see cref="Sys.SetPeripheralsEnabled"/> (C: PDPeripherals).
    /// </summary>
    [Flags]
    public enum Peripherals
    {
        /// <summary>Everything off (the default).</summary>
        None = 0,
        /// <summary>The accelerometer. Readings are available from the next frame on.</summary>
        Accelerometer = 1 << 0,
        /// <summary>All peripherals.</summary>
        All = 0xFFFF,
    }

    /// <summary>Power flags from <see cref="Sys.PowerStatus"/> (C: PDPowerStatus).</summary>
    [Flags]
    public enum PowerStatus
    {
        /// <summary>Running on battery.</summary>
        None = 0,
        /// <summary>The battery is charging.</summary>
        Charging = 1 << 0,
        /// <summary>Powered over USB.</summary>
        Usb = 1 << 1,
        /// <summary>Powered through the screw terminals (the charging dock).</summary>
        Screws = 1 << 2,
    }

    /// <summary>
    /// A calendar date and time (C: struct PDDateTime). Convert with <see cref="Sys.ConvertEpochToDateTime"/>
    /// and <see cref="Sys.ConvertDateTimeToEpoch"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PDDateTime
    {
        /// <summary>Full year, e.g. 2026.</summary>
        public ushort Year;
        /// <summary>Month, 1-12.</summary>
        public byte Month;   // 1-12
        /// <summary>Day of the month, 1-31.</summary>
        public byte Day;     // 1-31
        /// <summary>Day of the week, 1 = Monday ... 7 = Sunday.</summary>
        public byte Weekday; // 1=monday-7=sunday
        /// <summary>Hour, 0-23.</summary>
        public byte Hour;    // 0-23
        /// <summary>Minute, 0-59.</summary>
        public byte Minute;
        /// <summary>Second, 0-59.</summary>
        public byte Second;
    }

    /// <summary>System information from <see cref="Sys.SystemInfo"/> (C: struct PDInfo).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PDInfo
    {
        /// <summary>Runtime version as a number, e.g. 30102 for 3.1.2.</summary>
        public uint OSVersion;
        private uint _language;         // PDLanguage: 1 byte + padding on the device, 4 bytes in the Simulator
        /// <summary>
        /// The pdxversion from the game's pdxinfo: the SDK version the game was built with.
        /// </summary>
        public uint PdxVersion;

        /// <summary>The system language.</summary>
        public Language Language => (Language)(byte)_language;
    }

    /// <summary>
    /// How to open a file (C: FileOptions). <see cref="Read"/> searches the game's pdx (read-only),
    /// <see cref="ReadData"/> the game's data folder; combine both to look in the data folder first.
    /// <see cref="Write"/> and <see cref="Append"/> always use the data folder.
    /// </summary>
    [Flags]
    public enum FileOptions
    {
        /// <summary>Read from the pdx.</summary>
        Read = 1 << 0,
        /// <summary>Read from the data folder (Data/&lt;bundleID&gt;).</summary>
        ReadData = 1 << 1,
        /// <summary>Create or truncate a file in the data folder.</summary>
        Write = 1 << 2,
        /// <summary>Create a file in the data folder or append to it.</summary>
        Append = 2 << 2,
    }

    /// <summary>Origin for <see cref="PDFile.Seek"/> (C: SEEK_SET, SEEK_CUR, SEEK_END).</summary>
    public enum SeekOrigin
    {
        /// <summary>From the start of the file.</summary>
        Begin = 0,
        /// <summary>From the current position.</summary>
        Current = 1,
        /// <summary>From the end of the file.</summary>
        End = 2,
    }

    /// <summary>
    /// File information from <see cref="FileSystem.Stat"/> (C: FileStat). The time is the last modification
    /// time.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FileStat
    {
        /// <summary>Non-zero for folders; see <see cref="IsDirectory"/>.</summary>
        public int IsDir;
        /// <summary>Size in bytes.</summary>
        public uint Size;
        /// <summary>Modification year.</summary>
        public int Year;
        /// <summary>Modification month, 1-12.</summary>
        public int Month;
        /// <summary>Modification day, 1-31.</summary>
        public int Day;
        /// <summary>Modification hour, 0-23.</summary>
        public int Hour;
        /// <summary>Modification minute.</summary>
        public int Minute;
        /// <summary>Modification second.</summary>
        public int Second;

        /// <summary>True if the path is a folder.</summary>
        public bool IsDirectory => IsDir != 0;
    }

    // ---- Graphics ----

    /// <summary>
    /// An integer rectangle given by its edges (C: LCDRect). <see cref="Right"/> and <see cref="Bottom"/> are
    /// exclusive. Used for clip rects and dirty rects; sprites use the float <see cref="PDRect"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LCDRect
    {
        /// <summary>Left edge (inclusive).</summary>
        public int Left;
        /// <summary>Right edge (exclusive).</summary>
        public int Right;  // not inclusive
        /// <summary>Top edge (inclusive).</summary>
        public int Top;
        /// <summary>Bottom edge (exclusive).</summary>
        public int Bottom; // not inclusive

        /// <summary>
        /// Creates a rectangle from its edges. Note the argument order: left, top, right, bottom.
        /// </summary>
        /// <param name="left">Left edge (included).</param>
        /// <param name="top">Top edge (included).</param>
        /// <param name="right">Right edge (excluded), at least <paramref name="left"/>.</param>
        /// <param name="bottom">Bottom edge (excluded), at least <paramref name="top"/>.</param>
        public LCDRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>Creates a rectangle from a position and a size.</summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width, 0 or more.</param>
        /// <param name="height">Height, 0 or more.</param>
        public static LCDRect Make(int x, int y, int width, int height) => new LCDRect(x, y, x + width, y + height);

        /// <summary>Returns the rectangle moved by (dx, dy).</summary>
        /// <param name="dx">Horizontal offset.</param>
        /// <param name="dy">Vertical offset.</param>
        public LCDRect Translate(int dx, int dy) => new LCDRect(Left + dx, Top + dy, Right + dx, Bottom + dy);

        /// <summary>Right - Left.</summary>
        public int Width => Right - Left;
        /// <summary>Bottom - Top.</summary>
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// How bitmaps (and therefore text) are drawn (C: LCDBitmapDrawMode, Lua:
    /// playdate.graphics.kDrawMode...). Set with <see cref="Graphics.SetDrawMode"/> or
    /// <see cref="Sprite.DrawMode"/>.
    /// </summary>
    public enum DrawMode
    {
        /// <summary>Draw the image as is (default).</summary>
        Copy,
        /// <summary>White pixels are not drawn.</summary>
        WhiteTransparent,
        /// <summary>Black pixels are not drawn.</summary>
        BlackTransparent,
        /// <summary>Every opaque pixel is drawn white.</summary>
        FillWhite,
        /// <summary>Every opaque pixel is drawn black.</summary>
        FillBlack,
        /// <summary>Image XOR destination.</summary>
        XOR,
        /// <summary>Inverted XOR.</summary>
        NXOR,
        /// <summary>Black and white are swapped.</summary>
        Inverted,
    }

    /// <summary>
    /// Mirroring applied when drawing a bitmap (C: LCDBitmapFlip, Lua: playdate.graphics.kImageFlipped...).
    /// </summary>
    public enum BitmapFlip
    {
        /// <summary>Not mirrored.</summary>
        Unflipped,
        /// <summary>Mirrored horizontally.</summary>
        FlippedX,
        /// <summary>Mirrored vertically.</summary>
        FlippedY,
        /// <summary>Mirrored both ways (rotated 180 degrees).</summary>
        FlippedXY,
    }

    /// <summary>
    /// The four solid colors (C: LCDSolidColor, Lua: playdate.graphics.kColorBlack ...). Converts implicitly
    /// to <see cref="LCDColor"/>.
    /// </summary>
    public enum SolidColor
    {
        /// <summary>Black.</summary>
        Black,
        /// <summary>White.</summary>
        White,
        /// <summary>Transparent (nothing is drawn).</summary>
        Clear,
        /// <summary>Inverts what is underneath.</summary>
        XOR,
    }

    /// <summary>Line end style for <see cref="Graphics.SetLineCapStyle"/> (C: LCDLineCapStyle).</summary>
    public enum LineCapStyle
    {
        /// <summary>Flat end at the end point (default).</summary>
        Butt,
        /// <summary>Flat end extended by half the line width.</summary>
        Square,
        /// <summary>Rounded end.</summary>
        Round,
    }

    /// <summary>
    /// Text encodings accepted by the C text functions (C: PDStringEncoding). The wrappers choose it for you.
    /// </summary>
    public enum StringEncoding
    {
        /// <summary>7-bit ASCII.</summary>
        ASCII,
        /// <summary>UTF-8.</summary>
        UTF8,
        /// <summary>UTF-16 little endian (C# strings).</summary>
        UTF16LE,
    }

    /// <summary>Which areas of a self-intersecting polygon are filled (C: LCDPolygonFillRule).</summary>
    public enum PolygonFillRule
    {
        /// <summary>Non-zero winding rule (default).</summary>
        NonZero,
        /// <summary>Even-odd rule: overlapping areas alternate between filled and empty.</summary>
        EvenOdd,
    }

    /// <summary>Line breaking for <see cref="Graphics.DrawTextInRect"/> (C: PDTextWrappingMode).</summary>
    public enum TextWrappingMode
    {
        /// <summary>No wrapping; text past the edge is cut off.</summary>
        Clip,
        /// <summary>Break at any character.</summary>
        Character,
        /// <summary>Break between words.</summary>
        Word,
    }

    /// <summary>
    /// Horizontal alignment for <see cref="Graphics.DrawTextInRect"/> (C: PDTextAlignment).
    /// </summary>
    public enum TextAlignment
    {
        /// <summary>Left aligned.</summary>
        Left,
        /// <summary>Centered.</summary>
        Center,
        /// <summary>Right aligned.</summary>
        Right,
    }

    // ---- Sprites ----

    /// <summary>
    /// How a moving sprite reacts when it hits another one (C: SpriteCollisionResponseType, Lua:
    /// playdate.graphics.sprite.kCollisionType...). Returned from <see cref="Sprite.CollisionResponse"/>.
    /// </summary>
    public enum CollisionResponseType
    {
        /// <summary>Stop moving toward the other sprite but slide along it (platforms, walls).</summary>
        Slide,
        /// <summary>Stop at the point of contact (an arrow hitting a wall). The default.</summary>
        Freeze,
        /// <summary>Pass through; the collision is only reported (collecting a coin).</summary>
        Overlap,
        /// <summary>Bounce off (a ball in Pong).</summary>
        Bounce,
    }

    /// <summary>
    /// A float rectangle given by position and size (C: PDRect). Used for sprite bounds and collide rects.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PDRect
    {
        /// <summary>Left.</summary>
        public float X;
        /// <summary>Top.</summary>
        public float Y;
        /// <summary>Width.</summary>
        public float Width;
        /// <summary>Height.</summary>
        public float Height;

        /// <summary>Creates a rectangle.</summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width, 0 or more.</param>
        /// <param name="height">Height, 0 or more.</param>
        public PDRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>"(x, y, width, height)".</summary>
        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    /// <summary>A point in a collision result (C: CollisionPoint).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CollisionPoint
    {
        /// <summary>X coordinate.</summary>
        public float X;
        /// <summary>Y coordinate.</summary>
        public float Y;
    }

    /// <summary>A collision normal; each component is usually -1, 0 or 1 (C: CollisionVector).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CollisionVector
    {
        /// <summary>X component; -1 = hit from the right, 1 = hit from the left.</summary>
        public int X;
        /// <summary>Y component; -1 = landed on top of the other sprite, 1 = hit it from below.</summary>
        public int Y;
    }

    /// <summary>
    /// One collision reported by <see cref="Sprite.MoveWithCollisions"/> or
    /// <see cref="Sprite.CheckCollisions"/> (C: SpriteCollisionInfo). Read entries through a
    /// <see cref="CollisionList"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SpriteCollisionInfo
    {
        /// <summary>The sprite being moved. See <see cref="SpriteObject"/>.</summary>
        public LCDSprite* Sprite;       // The sprite being moved
        /// <summary>The sprite it collided with. See <see cref="OtherSprite"/>.</summary>
        public LCDSprite* Other;        // The sprite colliding with the sprite being moved
        // C: SpriteCollisionResponseType responseType; uint8_t overlaps;
        // The device's ARM toolchain uses 1-byte enums (-fshort-enums is the arm-none-eabi default),
        // the Simulator 4-byte ones, so both fields share one pointer-sized slot: on ARM32 it holds
        // responseType in byte 0 and overlaps in byte 1; on 64-bit, responseType in bytes 0-3 and
        // overlaps in byte 4. Either way Ti lands at the C offset (12 or 24).
        private nuint _responseTypeAndOverlaps;
        /// <summary>How far along the movement the collision happened, 0..1.</summary>
        public float Ti;                // How far along the movement to the goal the collision occurred (0..1)
        /// <summary>
        /// Difference between the requested and the actual position when the collision happened.
        /// </summary>
        public CollisionPoint Move;     // Difference between the original coordinates and the actual ones
        /// <summary>
        /// The collision normal, e.g. Y = -1 when the sprite landed on top of the other one.
        /// </summary>
        public CollisionVector Normal;  // Collision normal; usually -1, 0, or 1 in x and y
        /// <summary>Where the sprite started touching the other sprite.</summary>
        public CollisionPoint Touch;    // Coordinates where the sprite started touching other
        /// <summary>The rectangle the moving sprite occupied at the touch.</summary>
        public PDRect SpriteRect;       // Rectangle the sprite occupied when the touch happened
        /// <summary>The rectangle the other sprite occupied at the touch.</summary>
        public PDRect OtherRect;        // Rectangle the other sprite occupied when the touch happened

        /// <summary>The collision response that was applied.</summary>
        public CollisionResponseType ResponseType => (CollisionResponseType)(byte)_responseTypeAndOverlaps;

        /// <summary>
        /// True if the sprite was already overlapping the other one when the move started; false if it moved
        /// (or tunneled) into it.
        /// </summary>
        /// <summary>True if the sprite was overlapping other when the collision started.</summary>
        public bool Overlaps => (byte)((ulong)_responseTypeAndOverlaps >> (IntPtr.Size == 4 ? 8 : 32)) != 0;

        /// <summary>The managed sprite being moved.</summary>
        public Playdate.Sprite SpriteObject => Playdate.Sprite.FromHandle(Sprite);

        /// <summary>The managed sprite that was hit (null if it wasn't created through SharpDate).</summary>
        public Playdate.Sprite OtherSprite => Playdate.Sprite.FromHandle(Other);
    }

    /// <summary>
    /// A sprite crossed by a line, from <see cref="Sprites.QuerySpriteInfoAlongLine"/> (C: SpriteQueryInfo).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SpriteQueryInfo
    {
        /// <summary>The intersected sprite. See <see cref="SpriteObject"/>.</summary>
        public LCDSprite* Sprite;
        /// <summary>Where the line enters the sprite's collide rect, 0..1 along the line.</summary>
        public float Ti1;               // entry point (0..1 along the segment)
        /// <summary>Where the line exits, 0..1 along the line.</summary>
        public float Ti2;               // exit point
        /// <summary>Coordinates of the entry point.</summary>
        public CollisionPoint EntryPoint;
        /// <summary>Coordinates of the exit point.</summary>
        public CollisionPoint ExitPoint;

        /// <summary>The managed sprite intersected by the segment.</summary>
        public Playdate.Sprite SpriteObject => Playdate.Sprite.FromHandle(Sprite);
    }

    // ---- Sound ----

    /// <summary>Format of raw audio data (C: SoundFormat). Odd values are stereo.</summary>
    public enum SoundFormat
    {
        /// <summary>8-bit mono.</summary>
        Mono8 = 0,
        /// <summary>8-bit stereo.</summary>
        Stereo8 = 1,
        /// <summary>16-bit mono.</summary>
        Mono16 = 2,
        /// <summary>16-bit stereo.</summary>
        Stereo16 = 3,
        /// <summary>
        /// IMA ADPCM mono (compressed; can't play backwards or be used by a synth until decompressed).
        /// </summary>
        MonoADPCM = 4,
        /// <summary>IMA ADPCM stereo.</summary>
        StereoADPCM = 5,
    }

    /// <summary>LFO wave shapes (C: LFOType).</summary>
    public enum LFOType
    {
        /// <summary>Square wave.</summary>
        Square,
        /// <summary>Triangle wave.</summary>
        Triangle,
        /// <summary>Sine wave.</summary>
        Sine,
        /// <summary>A new random value each cycle.</summary>
        SampleAndHold,
        /// <summary>Rising sawtooth.</summary>
        SawtoothUp,
        /// <summary>Falling sawtooth.</summary>
        SawtoothDown,
        /// <summary>Steps through the half-step offsets given to <see cref="LFO.SetArpeggiation"/>.</summary>
        Arpeggiator,
        /// <summary>Values come from <see cref="LFO.SetFunction"/>.</summary>
        Function,
    }

    /// <summary>Synth waveforms (C: SoundWaveform, Lua: playdate.sound.kWave...).</summary>
    public enum SoundWaveform
    {
        /// <summary>Square wave; parameter 1 is the pulse width.</summary>
        Square,
        /// <summary>Triangle wave.</summary>
        Triangle,
        /// <summary>Sine wave.</summary>
        Sine,
        /// <summary>White noise.</summary>
        Noise,
        /// <summary>Sawtooth wave.</summary>
        Sawtooth,
        /// <summary>Teenage Engineering Pocket Operator "phase" synth (2 parameters).</summary>
        POPhase,
        /// <summary>Pocket Operator "digital" synth (2 parameters).</summary>
        PODigital,
        /// <summary>Pocket Operator "vosim" voice synth (2 parameters).</summary>
        POVosim,
    }

    /// <summary>Filter types for <see cref="SoundEffect.TwoPoleFilter"/> (C: TwoPoleFilterType).</summary>
    public enum TwoPoleFilterType
    {
        /// <summary>Low-pass.</summary>
        LowPass,
        /// <summary>High-pass.</summary>
        HighPass,
        /// <summary>Band-pass.</summary>
        BandPass,
        /// <summary>Notch (band-stop).</summary>
        Notch,
        /// <summary>Peaking EQ.</summary>
        PEQ,
        /// <summary>Low shelf.</summary>
        LowShelf,
        /// <summary>High shelf.</summary>
        HighShelf,
    }

    /// <summary>Microphone choice for <see cref="Sound.SetMicCallback"/> (C: enum MicSource).</summary>
    public enum MicSource
    {
        /// <summary>The headset mic if one is plugged in, otherwise the internal one.</summary>
        Autodetect = 0,
        /// <summary>Always the device's microphone.</summary>
        Internal = 1,
        /// <summary>Always the headset input.</summary>
        Headset = 2,
    }

    // ---- JSON ----

    /// <summary>
    /// JSON value kinds (C: json_value_type). Booleans are two types, <see cref="True"/> and
    /// <see cref="False"/>.
    /// </summary>
    public enum JsonValueType
    {
        /// <summary>null</summary>
        Null,
        /// <summary>true</summary>
        True,
        /// <summary>false</summary>
        False,
        /// <summary>
        /// A whole number. Note that whole floats in the text (e.g. 2.0) may be decoded as integers.
        /// </summary>
        Integer,
        /// <summary>A number with a fraction.</summary>
        Float,
        /// <summary>A string.</summary>
        String,
        /// <summary>An array (a sublist).</summary>
        Array,
        /// <summary>An object (a sublist).</summary>
        Table,
    }

    /// <summary>
    /// The payload of a <see cref="JsonValue"/> (C: the json_value union). Which field is valid depends on
    /// the type.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct JsonValueData
    {
        /// <summary>For <see cref="JsonValueType.Integer"/>.</summary>
        [FieldOffset(0)] public int IntValue;
        /// <summary>For <see cref="JsonValueType.Float"/>.</summary>
        [FieldOffset(0)] public float FloatValue;
        /// <summary>For <see cref="JsonValueType.String"/>: UTF-8, valid only during the callback.</summary>
        [FieldOffset(0)] public byte* StringValue;
        /// <summary>For arrays: the pointer returned by <see cref="JsonHandler.DidDecodeSublist"/>.</summary>
        [FieldOffset(0)] public void* ArrayValue;
        /// <summary>For tables: the pointer returned by <see cref="JsonHandler.DidDecodeSublist"/>.</summary>
        [FieldOffset(0)] public void* TableValue;
    }

    /// <summary>
    /// A value handed to <see cref="JsonHandler"/> callbacks (C: json_value). Valid only during the callback;
    /// copy what you need (the accessors below convert like the C json_intValue / json_floatValue ...
    /// helpers).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonValue
    {
        /// <summary>The <see cref="JsonValueType"/> as stored by the C decoder.</summary>
        public byte RawType;
        /// <summary>The raw payload.</summary>
        public JsonValueData Data;

        /// <summary>The kind of value.</summary>
        public JsonValueType Type => (JsonValueType)RawType;

        /// <summary>Boxes the value: null, bool, int, float or string (null for arrays and tables).</summary>
        public object ToObject() => Type switch
        {
            JsonValueType.Null => null,
            JsonValueType.True => true,
            JsonValueType.False => false,
            JsonValueType.Integer => Data.IntValue,
            JsonValueType.Float => Data.FloatValue,
            JsonValueType.String => Utf8.ToString(Data.StringValue),
            _ => null,
        };

        /// <summary>
        /// The value as an int: integers as is, floats truncated, numeric strings parsed, true = 1, otherwise
        /// 0.
        /// </summary>
        public int IntValue => Type switch
        {
            JsonValueType.Integer => Data.IntValue,
            JsonValueType.Float => (int)Data.FloatValue,
            JsonValueType.String => int.TryParse(Utf8.ToString(Data.StringValue), out int v) ? v : 0,
            JsonValueType.True => 1,
            _ => 0,
        };

        /// <summary>The value as a float: numbers as is, true = 1, otherwise 0.</summary>
        public float FloatValue => Type switch
        {
            JsonValueType.Integer => Data.IntValue,
            JsonValueType.Float => Data.FloatValue,
            JsonValueType.True => 1,
            _ => 0,
        };

        /// <summary>The value as a bool: non-empty strings and non-zero numbers are true.</summary>
        public bool BoolValue => Type == JsonValueType.String ? (Data.StringValue != null && Data.StringValue[0] != 0) : IntValue != 0;

        /// <summary>The string, or null if the value isn't a string.</summary>
        public string StringValue => Type == JsonValueType.String ? Utf8.ToString(Data.StringValue) : null;

        /// <summary>A short text form ("null", "true", the number, the string, "array" or "table").</summary>
        public override string ToString() => Type switch
        {
            JsonValueType.Null => "null",
            JsonValueType.True => "true",
            JsonValueType.False => "false",
            JsonValueType.Integer => Data.IntValue.ToString(),
            JsonValueType.Float => Data.FloatValue.ToString(),
            JsonValueType.String => Utf8.ToString(Data.StringValue),
            JsonValueType.Array => "array",
            _ => "table",
        };
    }

    /// <summary>
    /// The raw C json_decoder struct. Use <see cref="JsonHandler"/> instead; this is for direct use of
    /// <c>PD.Json</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonDecoder
    {
        /// <summary>Called on a parse error with the message and line number. Required.</summary>
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, int, void> decodeError;
        /// <summary>Called before an object or array is decoded.</summary>
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, JsonValueType, void> willDecodeSublist;
        /// <summary>Return 1 to decode the value for this key, 0 to skip it.</summary>
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, int> shouldDecodeTableValueForKey;
        /// <summary>Called with each decoded object member.</summary>
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, JsonValue, void> didDecodeTableValue;
        /// <summary>Return 1 to decode the array element at this 1-based position, 0 to skip it.</summary>
        public delegate* unmanaged[Cdecl]<JsonDecoder*, int, int> shouldDecodeArrayValueAtIndex;
        /// <summary>Called with each decoded array element (1-based position).</summary>
        public delegate* unmanaged[Cdecl]<JsonDecoder*, int, JsonValue, void> didDecodeArrayValue;
        /// <summary>
        /// Called after an object or array; the return value becomes its value one level up.
        /// </summary>
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, JsonValueType, void*> didDecodeSublist;
        /// <summary>Free for the client's use.</summary>
        public void* userdata;
        /// <summary>
        /// Set to 1 in willDecodeSublist to receive the sublist as a raw string instead of decoding it.
        /// </summary>
        public int returnString; // when set, the decoder skips parsing and returns the current subtree as a string
        /// <summary>The current path in the document, e.g. "web-app.servlet[1]".</summary>
        public byte* path;       // updated during parsing, reflects current position in tree
    }

    /// <summary>
    /// The raw C json_reader: a function that fills a buffer and returns the byte count (0 at the end).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonReader
    {
        /// <summary>
        /// Fills the buffer and returns the number of bytes written, or 0 at the end of the data.
        /// </summary>
        public delegate* unmanaged[Cdecl]<void*, byte*, int, int> read;
        /// <summary>Passed to <see cref="read"/>.</summary>
        public void* userdata;
    }

    /// <summary>
    /// The raw C json_encoder struct, filled in by <c>PD.Json->initEncoder</c>. Use <see cref="JsonWriter"/>
    /// instead.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonEncoder
    {
        /// <summary>The output function given to initEncoder.</summary>
        public delegate* unmanaged[Cdecl]<void*, byte*, int, void> writeStringFunc;
        /// <summary>Passed to the output function.</summary>
        public void* userdata;
        /// <summary>Encoder state (pretty flag, nesting); managed by the encoder.</summary>
        public int flags; // bitfields: pretty:1, startedTable:1, startedArray:1, depth:29
        /// <summary>Writes [.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> startArray;
        /// <summary>Starts the next array element (writes a comma when needed).</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> addArrayMember;
        /// <summary>Writes ].</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> endArray;
        /// <summary>Writes {.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> startTable;
        /// <summary>Writes a member name (and the separators); write its value next.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, byte*, int, void> addTableMember;
        /// <summary>Writes }.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> endTable;
        /// <summary>Writes null.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> writeNull;
        /// <summary>Writes false.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> writeFalse;
        /// <summary>Writes true.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> writeTrue;
        /// <summary>Writes an integer.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, int, void> writeInt;
        /// <summary>
        /// Writes a number given as a double. Calling it from managed code needs double-precision
        /// instructions, which crash on the device; see <see cref="JsonWriter.WriteFloat"/>.
        /// </summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, double, void> writeDouble;
        /// <summary>Writes a quoted, escaped string.</summary>
        public delegate* unmanaged[Cdecl]<JsonEncoder*, byte*, int, void> writeString;
    }

    // ---- Lua ----

    /// <summary>Type of a constant in a <see cref="LuaVal"/> table (C: the lua_val type enum).</summary>
    public enum LuaValueType
    {
        /// <summary><see cref="LuaValData.intval"/> is set.</summary>
        Int,
        /// <summary><see cref="LuaValData.floatval"/> is set.</summary>
        Float,
        /// <summary><see cref="LuaValData.strval"/> is set.</summary>
        Str,
    }

    /// <summary>
    /// Type of a Lua value on the stack, from <see cref="Lua.GetArgType(int)"/> (C: enum LuaType).
    /// </summary>
    public enum LuaType
    {
        /// <summary>nil</summary>
        Nil,
        /// <summary>A boolean.</summary>
        Bool,
        /// <summary>An integer.</summary>
        Int,
        /// <summary>A float.</summary>
        Float,
        /// <summary>A string.</summary>
        String,
        /// <summary>A table.</summary>
        Table,
        /// <summary>A function.</summary>
        Function,
        /// <summary>A coroutine.</summary>
        Thread,
        /// <summary>
        /// A userdata object (e.g. pushed with <see cref="Lua.PushObject"/>); see the className overload.
        /// </summary>
        Object,
    }

    /// <summary>
    /// One entry of a class's method table (C: lua_reg). Built for you by
    /// <see cref="Lua.RegisterClass(string, ReadOnlySpan{LuaFunction}, out string, bool)"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LuaReg
    {
        /// <summary>Method name (UTF-8).</summary>
        public byte* name;
        /// <summary>The function.</summary>
        public delegate* unmanaged[Cdecl]<void*, int> func;
    }

    /// <summary>The value of a <see cref="LuaVal"/> constant.</summary>
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct LuaValData
    {
        /// <summary>Integer value.</summary>
        [FieldOffset(0)] public uint intval;
        /// <summary>Float value.</summary>
        [FieldOffset(0)] public float floatval;
        /// <summary>String value (UTF-8, must stay valid).</summary>
        [FieldOffset(0)] public byte* strval;
    }

    /// <summary>
    /// A named constant added to a Lua class (C: lua_val). Pass an array ending with a zeroed entry to
    /// <see cref="Lua.RegisterClass(string, ReadOnlySpan{LuaFunction}, LuaVal*, out string, bool)"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LuaVal
    {
        /// <summary>Constant name (UTF-8, must stay valid).</summary>
        public byte* name;
        /// <summary>Which field of <see cref="v"/> is set.</summary>
        public LuaValueType type;
        /// <summary>The value.</summary>
        public LuaValData v;
    }

    // ---- Scoreboards ----

    /// <summary>
    /// A raw scoreboard score (C: PDScore). The wrappers convert it to <see cref="Scoreboards.Score"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDScore
    {
        /// <summary>Rank on the board, 1 = best.</summary>
        public uint Rank;
        /// <summary>The score.</summary>
        public uint Value;
        /// <summary>Player name (UTF-8).</summary>
        public byte* Player;
        /// <summary>Board identifier (UTF-8).</summary>
        public byte* BoardID;
    }

    /// <summary>A score inside a <see cref="PDScoresList"/> (C: PDScore without the board).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDListScore
    {
        /// <summary>Rank on the board.</summary>
        public uint Rank;
        /// <summary>The score.</summary>
        public uint Value;
        /// <summary>Player name (UTF-8).</summary>
        public byte* Player;
    }

    /// <summary>A raw list of scores (C: PDScoresList).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDScoresList
    {
        /// <summary>Board identifier.</summary>
        public byte* BoardID;
        /// <summary>Number of entries in <see cref="Scores"/>.</summary>
        public uint Count;
        /// <summary>When the board was last updated (seconds since 2000-01-01).</summary>
        public uint LastUpdated;
        /// <summary>Non-zero if the player's own score is in the list.</summary>
        public int PlayerIncluded;
        /// <summary>Maximum number of entries requested.</summary>
        public uint Limit;
        /// <summary>The entries.</summary>
        public PDListScore* Scores;
    }

    /// <summary>A raw scoreboard description (C: PDBoard).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDBoard
    {
        /// <summary>Board identifier.</summary>
        public byte* BoardID;
        /// <summary>Display name.</summary>
        public byte* Name;
    }

    /// <summary>A raw list of scoreboards (C: PDBoardsList).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDBoardsList
    {
        /// <summary>Number of boards.</summary>
        public uint Count;
        /// <summary>When the list was last updated.</summary>
        public uint LastUpdated;
        /// <summary>The boards.</summary>
        public PDBoard* Boards;
    }

    // ---- Network ----

    /// <summary>
    /// Network result codes (C: PDNetErr). Zero is success; everything else is a negative error.
    /// </summary>
    public enum NetErr
    {
        /// <summary>Success.</summary>
        Ok = 0,
        /// <summary>No Wi-Fi hardware (or it isn't available).</summary>
        NoDevice = -1,
        /// <summary>The network is busy; try again.</summary>
        Busy = -2,
        /// <summary>Sending failed.</summary>
        WriteError = -3,
        /// <summary>The send buffer is full.</summary>
        WriteBusy = -4,
        /// <summary>Sending timed out.</summary>
        WriteTimeout = -5,
        /// <summary>Receiving failed.</summary>
        ReadError = -6,
        /// <summary>A read is already in progress.</summary>
        ReadBusy = -7,
        /// <summary>No data arrived within the read timeout.</summary>
        ReadTimeout = -8,
        /// <summary>The read buffer overflowed.</summary>
        ReadOverflow = -9,
        /// <summary>Malformed data frame.</summary>
        FrameError = -10,
        /// <summary>The server's response could not be parsed.</summary>
        BadResponse = -11,
        /// <summary>The server answered with an error.</summary>
        ErrorResponse = -12,
        /// <summary>The Wi-Fi module did not reset in time.</summary>
        ResetTimeout = -13,
        /// <summary>A buffer is too small for the data.</summary>
        BufferTooSmall = -14,
        /// <summary>Unexpected response from the Wi-Fi module.</summary>
        UnexpectedResponse = -15,
        /// <summary>Not connected to a Wi-Fi access point.</summary>
        NotConnectedToAP = -16,
        /// <summary>Not supported (e.g. in this Simulator).</summary>
        NotImplemented = -17,
        /// <summary>The connection was closed.</summary>
        ConnectionClosed = -18,
    }

    /// <summary>Wi-Fi state from <see cref="Network.Status"/> (C: WifiStatus).</summary>
    public enum WifiStatus
    {
        /// <summary>Not connected to an access point.</summary>
        NotConnected = 0,
        /// <summary>Connected to an access point.</summary>
        Connected,
        /// <summary>A connection was attempted and no configured access point was available.</summary>
        NotAvailable,
    }
}
