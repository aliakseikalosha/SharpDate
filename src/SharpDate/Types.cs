using System;
using System.Runtime.InteropServices;
using Playdate.Native;

namespace Playdate
{
    // Enums and plain data structs of the Playdate C API. Values and layouts match pd_api*.h.

    public enum SystemEvent
    {
        Init,
        InitLua,
        Lock,
        Unlock,
        Pause,
        Resume,
        Terminate,
        KeyPressed,
        KeyReleased,
        LowPower,
        MirrorStarted,
        MirrorEnded,
    }

    [Flags]
    public enum Buttons
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Up = 1 << 2,
        Down = 1 << 3,
        B = 1 << 4,
        A = 1 << 5,
    }

    public enum Language
    {
        English = 0,
        Japanese,
        System,
    }

    public enum AccessReply
    {
        Ask,
        Deny,
        Allow,
    }

    [Flags]
    public enum Peripherals
    {
        None = 0,
        Accelerometer = 1 << 0,
        All = 0xFFFF,
    }

    [Flags]
    public enum PowerStatus
    {
        None = 0,
        Charging = 1 << 0,
        Usb = 1 << 1,
        Screws = 1 << 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDDateTime
    {
        public ushort Year;
        public byte Month;   // 1-12
        public byte Day;     // 1-31
        public byte Weekday; // 1=monday-7=sunday
        public byte Hour;    // 0-23
        public byte Minute;
        public byte Second;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDInfo
    {
        public uint OSVersion;
        private uint _language;         // PDLanguage: 1 byte + padding on the device, 4 bytes in the Simulator
        public uint PdxVersion;

        public Language Language => (Language)(byte)_language;
    }

    [Flags]
    public enum FileOptions
    {
        Read = 1 << 0,
        ReadData = 1 << 1,
        Write = 1 << 2,
        Append = 2 << 2,
    }

    public enum SeekOrigin
    {
        Begin = 0,
        Current = 1,
        End = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FileStat
    {
        public int IsDir;
        public uint Size;
        public int Year;
        public int Month;
        public int Day;
        public int Hour;
        public int Minute;
        public int Second;

        public bool IsDirectory => IsDir != 0;
    }

    // ---- Graphics ----

    [StructLayout(LayoutKind.Sequential)]
    public struct LCDRect
    {
        public int Left;
        public int Right;  // not inclusive
        public int Top;
        public int Bottom; // not inclusive

        public LCDRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public static LCDRect Make(int x, int y, int width, int height) => new LCDRect(x, y, x + width, y + height);

        public LCDRect Translate(int dx, int dy) => new LCDRect(Left + dx, Top + dy, Right + dx, Bottom + dy);

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    public enum DrawMode
    {
        Copy,
        WhiteTransparent,
        BlackTransparent,
        FillWhite,
        FillBlack,
        XOR,
        NXOR,
        Inverted,
    }

    public enum BitmapFlip
    {
        Unflipped,
        FlippedX,
        FlippedY,
        FlippedXY,
    }

    public enum SolidColor
    {
        Black,
        White,
        Clear,
        XOR,
    }

    public enum LineCapStyle
    {
        Butt,
        Square,
        Round,
    }

    public enum StringEncoding
    {
        ASCII,
        UTF8,
        UTF16LE,
    }

    public enum PolygonFillRule
    {
        NonZero,
        EvenOdd,
    }

    public enum TextWrappingMode
    {
        Clip,
        Character,
        Word,
    }

    public enum TextAlignment
    {
        Left,
        Center,
        Right,
    }

    // ---- Sprites ----

    public enum CollisionResponseType
    {
        Slide,
        Freeze,
        Overlap,
        Bounce,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public PDRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CollisionPoint
    {
        public float X;
        public float Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CollisionVector
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SpriteCollisionInfo
    {
        public LCDSprite* Sprite;       // The sprite being moved
        public LCDSprite* Other;        // The sprite colliding with the sprite being moved
        // C: SpriteCollisionResponseType responseType; uint8_t overlaps;
        // The device's ARM toolchain uses 1-byte enums (-fshort-enums is the arm-none-eabi default),
        // the Simulator 4-byte ones, so both fields share one pointer-sized slot: on ARM32 it holds
        // responseType in byte 0 and overlaps in byte 1; on 64-bit, responseType in bytes 0-3 and
        // overlaps in byte 4. Either way Ti lands at the C offset (12 or 24).
        private nuint _responseTypeAndOverlaps;
        public float Ti;                // How far along the movement to the goal the collision occurred (0..1)
        public CollisionPoint Move;     // Difference between the original coordinates and the actual ones
        public CollisionVector Normal;  // Collision normal; usually -1, 0, or 1 in x and y
        public CollisionPoint Touch;    // Coordinates where the sprite started touching other
        public PDRect SpriteRect;       // Rectangle the sprite occupied when the touch happened
        public PDRect OtherRect;        // Rectangle the other sprite occupied when the touch happened

        /// <summary>The collision response that was applied.</summary>
        public CollisionResponseType ResponseType => (CollisionResponseType)(byte)_responseTypeAndOverlaps;

        /// <summary>True if the sprite was overlapping other when the collision started.</summary>
        public bool Overlaps => (byte)((ulong)_responseTypeAndOverlaps >> (IntPtr.Size == 4 ? 8 : 32)) != 0;

        /// <summary>The managed sprite being moved.</summary>
        public Playdate.Sprite SpriteObject => Playdate.Sprite.FromHandle(Sprite);

        /// <summary>The managed sprite that was hit (null if it wasn't created through SharpDate).</summary>
        public Playdate.Sprite OtherSprite => Playdate.Sprite.FromHandle(Other);
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SpriteQueryInfo
    {
        public LCDSprite* Sprite;
        public float Ti1;               // entry point (0..1 along the segment)
        public float Ti2;               // exit point
        public CollisionPoint EntryPoint;
        public CollisionPoint ExitPoint;

        /// <summary>The managed sprite intersected by the segment.</summary>
        public Playdate.Sprite SpriteObject => Playdate.Sprite.FromHandle(Sprite);
    }

    // ---- Sound ----

    public enum SoundFormat
    {
        Mono8 = 0,
        Stereo8 = 1,
        Mono16 = 2,
        Stereo16 = 3,
        MonoADPCM = 4,
        StereoADPCM = 5,
    }

    public enum LFOType
    {
        Square,
        Triangle,
        Sine,
        SampleAndHold,
        SawtoothUp,
        SawtoothDown,
        Arpeggiator,
        Function,
    }

    public enum SoundWaveform
    {
        Square,
        Triangle,
        Sine,
        Noise,
        Sawtooth,
        POPhase,
        PODigital,
        POVosim,
    }

    public enum TwoPoleFilterType
    {
        LowPass,
        HighPass,
        BandPass,
        Notch,
        PEQ,
        LowShelf,
        HighShelf,
    }

    public enum MicSource
    {
        Autodetect = 0,
        Internal = 1,
        Headset = 2,
    }

    // ---- JSON ----

    public enum JsonValueType
    {
        Null,
        True,
        False,
        Integer,
        Float,
        String,
        Array,
        Table,
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct JsonValueData
    {
        [FieldOffset(0)] public int IntValue;
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public byte* StringValue;
        [FieldOffset(0)] public void* ArrayValue;
        [FieldOffset(0)] public void* TableValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonValue
    {
        public byte RawType;
        public JsonValueData Data;

        public JsonValueType Type => (JsonValueType)RawType;

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

        public int IntValue => Type switch
        {
            JsonValueType.Integer => Data.IntValue,
            JsonValueType.Float => (int)Data.FloatValue,
            JsonValueType.String => int.TryParse(Utf8.ToString(Data.StringValue), out int v) ? v : 0,
            JsonValueType.True => 1,
            _ => 0,
        };

        public float FloatValue => Type switch
        {
            JsonValueType.Integer => Data.IntValue,
            JsonValueType.Float => Data.FloatValue,
            JsonValueType.True => 1,
            _ => 0,
        };

        public bool BoolValue => Type == JsonValueType.String ? (Data.StringValue != null && Data.StringValue[0] != 0) : IntValue != 0;

        public string StringValue => Type == JsonValueType.String ? Utf8.ToString(Data.StringValue) : null;

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

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonDecoder
    {
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, int, void> decodeError;
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, JsonValueType, void> willDecodeSublist;
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, int> shouldDecodeTableValueForKey;
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, JsonValue, void> didDecodeTableValue;
        public delegate* unmanaged[Cdecl]<JsonDecoder*, int, int> shouldDecodeArrayValueAtIndex;
        public delegate* unmanaged[Cdecl]<JsonDecoder*, int, JsonValue, void> didDecodeArrayValue;
        public delegate* unmanaged[Cdecl]<JsonDecoder*, byte*, JsonValueType, void*> didDecodeSublist;
        public void* userdata;
        public int returnString; // when set, the decoder skips parsing and returns the current subtree as a string
        public byte* path;       // updated during parsing, reflects current position in tree
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonReader
    {
        public delegate* unmanaged[Cdecl]<void*, byte*, int, int> read;
        public void* userdata;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct JsonEncoder
    {
        public delegate* unmanaged[Cdecl]<void*, byte*, int, void> writeStringFunc;
        public void* userdata;
        public int flags; // bitfields: pretty:1, startedTable:1, startedArray:1, depth:29
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> startArray;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> addArrayMember;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> endArray;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> startTable;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, byte*, int, void> addTableMember;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> endTable;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> writeNull;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> writeFalse;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, void> writeTrue;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, int, void> writeInt;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, double, void> writeDouble;
        public delegate* unmanaged[Cdecl]<JsonEncoder*, byte*, int, void> writeString;
    }

    // ---- Lua ----

    public enum LuaValueType
    {
        Int,
        Float,
        Str,
    }

    public enum LuaType
    {
        Nil,
        Bool,
        Int,
        Float,
        String,
        Table,
        Function,
        Thread,
        Object,
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LuaReg
    {
        public byte* name;
        public delegate* unmanaged[Cdecl]<void*, int> func;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct LuaValData
    {
        [FieldOffset(0)] public uint intval;
        [FieldOffset(0)] public float floatval;
        [FieldOffset(0)] public byte* strval;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LuaVal
    {
        public byte* name;
        public LuaValueType type;
        public LuaValData v;
    }

    // ---- Scoreboards ----

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDScore
    {
        public uint Rank;
        public uint Value;
        public byte* Player;
        public byte* BoardID;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDListScore
    {
        public uint Rank;
        public uint Value;
        public byte* Player;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDScoresList
    {
        public byte* BoardID;
        public uint Count;
        public uint LastUpdated;
        public int PlayerIncluded;
        public uint Limit;
        public PDListScore* Scores;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDBoard
    {
        public byte* BoardID;
        public byte* Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PDBoardsList
    {
        public uint Count;
        public uint LastUpdated;
        public PDBoard* Boards;
    }

    // ---- Network ----

    public enum NetErr
    {
        Ok = 0,
        NoDevice = -1,
        Busy = -2,
        WriteError = -3,
        WriteBusy = -4,
        WriteTimeout = -5,
        ReadError = -6,
        ReadBusy = -7,
        ReadTimeout = -8,
        ReadOverflow = -9,
        FrameError = -10,
        BadResponse = -11,
        ErrorResponse = -12,
        ResetTimeout = -13,
        BufferTooSmall = -14,
        UnexpectedResponse = -15,
        NotConnectedToAP = -16,
        NotImplemented = -17,
        ConnectionClosed = -18,
    }

    public enum WifiStatus
    {
        NotConnected = 0,
        Connected,
        NotAvailable,
    }
}
