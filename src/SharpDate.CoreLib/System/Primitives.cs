// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

namespace System
{
    public struct Void { }

    public struct Boolean : IEquatable<bool>
    {
        private readonly bool m_value;
        public static readonly string TrueString = "True";
        public static readonly string FalseString = "False";
        public override string ToString() => m_value ? "True" : "False";
        public bool Equals(bool other) => m_value == other;
        public override bool Equals(object obj) => obj is bool b && b == m_value;
        public override int GetHashCode() => m_value ? 1 : 0;
    }

    public struct Char : IEquatable<char>, IComparable<char>
    {
        private readonly char m_value;
        public const char MaxValue = (char)0xFFFF;
        public const char MinValue = (char)0x00;
        public override string ToString() => string.FromChar(m_value);
        public bool Equals(char other) => m_value == other;
        public override bool Equals(object obj) => obj is char c && c == m_value;
        public override int GetHashCode() => m_value | (m_value << 16);
        public int CompareTo(char other) => m_value - other;
        public static bool IsDigit(char c) => (uint)(c - '0') <= 9;
        public static bool IsLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';
        public static bool IsLetterOrDigit(char c) => IsLetter(c) || IsDigit(c);
        public static bool IsWhiteSpace(char c) => c == ' ' || (c >= '\t' && c <= '\r') || c == ' ' || c == '\u0085';
        public static bool IsUpper(char c) => (uint)(c - 'A') <= 'Z' - 'A';
        public static bool IsLower(char c) => (uint)(c - 'a') <= 'z' - 'a';
        public static char ToUpper(char c) => IsLower(c) ? (char)(c - 32) : c;
        public static char ToLower(char c) => IsUpper(c) ? (char)(c + 32) : c;
        public static bool IsHighSurrogate(char c) => (uint)(c - 0xD800) <= 0x3FF;
        public static bool IsLowSurrogate(char c) => (uint)(c - 0xDC00) <= 0x3FF;
        public static bool IsSurrogate(char c) => (uint)(c - 0xD800) <= 0x7FF;
        public static bool IsPunctuation(char c) => c < 128 && !IsLetterOrDigit(c) && !IsWhiteSpace(c) && c > 32 && c != 127;
        public static bool IsControl(char c) => c < 32 || (c >= 127 && c < 160);
    }

    public struct SByte : IEquatable<sbyte>, IComparable<sbyte>
    {
        private readonly sbyte m_value;
        public const sbyte MaxValue = 127;
        public const sbyte MinValue = -128;
        public override string ToString() => Number.Int32ToString(m_value);
        public bool Equals(sbyte other) => m_value == other;
        public override bool Equals(object obj) => obj is sbyte v && v == m_value;
        public override int GetHashCode() => m_value;
        public int CompareTo(sbyte other) => m_value - other;
    }

    public struct Byte : IEquatable<byte>, IComparable<byte>
    {
        private readonly byte m_value;
        public const byte MaxValue = 255;
        public const byte MinValue = 0;
        public override string ToString() => Number.UInt32ToString(m_value);
        public bool Equals(byte other) => m_value == other;
        public override bool Equals(object obj) => obj is byte v && v == m_value;
        public override int GetHashCode() => m_value;
        public int CompareTo(byte other) => m_value - other;
    }

    public struct Int16 : IEquatable<short>, IComparable<short>
    {
        private readonly short m_value;
        public const short MaxValue = 32767;
        public const short MinValue = -32768;
        public override string ToString() => Number.Int32ToString(m_value);
        public bool Equals(short other) => m_value == other;
        public override bool Equals(object obj) => obj is short v && v == m_value;
        public override int GetHashCode() => m_value;
        public int CompareTo(short other) => m_value - other;
    }

    public struct UInt16 : IEquatable<ushort>, IComparable<ushort>
    {
        private readonly ushort m_value;
        public const ushort MaxValue = 65535;
        public const ushort MinValue = 0;
        public override string ToString() => Number.UInt32ToString(m_value);
        public bool Equals(ushort other) => m_value == other;
        public override bool Equals(object obj) => obj is ushort v && v == m_value;
        public override int GetHashCode() => m_value;
        public int CompareTo(ushort other) => m_value - other;
    }

    public struct Int32 : IEquatable<int>, IComparable<int>
    {
        private readonly int m_value;
        public const int MaxValue = 0x7fffffff;
        public const int MinValue = unchecked((int)0x80000000);
        public override string ToString() => Number.Int32ToString(m_value);
        public string ToString(string format) => Number.FormatInt32(m_value, format);
        public bool Equals(int other) => m_value == other;
        public override bool Equals(object obj) => obj is int v && v == m_value;
        public override int GetHashCode() => m_value;
        public int CompareTo(int other) => m_value < other ? -1 : (m_value > other ? 1 : 0);
        public static int Parse(string s) => Number.ParseInt32(s);
        public static bool TryParse(string s, out int result) => Number.TryParseInt32(s, out result);
    }

    public struct UInt32 : IEquatable<uint>, IComparable<uint>
    {
        private readonly uint m_value;
        public const uint MaxValue = 0xffffffff;
        public const uint MinValue = 0;
        public override string ToString() => Number.UInt32ToString(m_value);
        public string ToString(string format) => Number.FormatUInt32(m_value, format);
        public bool Equals(uint other) => m_value == other;
        public override bool Equals(object obj) => obj is uint v && v == m_value;
        public override int GetHashCode() => (int)m_value;
        public int CompareTo(uint other) => m_value < other ? -1 : (m_value > other ? 1 : 0);
    }

    public struct Int64 : IEquatable<long>, IComparable<long>
    {
        private readonly long m_value;
        public const long MaxValue = 0x7fffffffffffffffL;
        public const long MinValue = unchecked((long)0x8000000000000000L);
        public override string ToString() => Number.Int64ToString(m_value);
        public bool Equals(long other) => m_value == other;
        public override bool Equals(object obj) => obj is long v && v == m_value;
        public override int GetHashCode() => (int)m_value ^ (int)(m_value >> 32);
        public int CompareTo(long other) => m_value < other ? -1 : (m_value > other ? 1 : 0);
    }

    public struct UInt64 : IEquatable<ulong>, IComparable<ulong>
    {
        private readonly ulong m_value;
        public const ulong MaxValue = 0xffffffffffffffffUL;
        public const ulong MinValue = 0;
        public override string ToString() => Number.UInt64ToString(m_value);
        public bool Equals(ulong other) => m_value == other;
        public override bool Equals(object obj) => obj is ulong v && v == m_value;
        public override int GetHashCode() => (int)m_value ^ (int)(m_value >> 32);
        public int CompareTo(ulong other) => m_value < other ? -1 : (m_value > other ? 1 : 0);
    }

    public unsafe struct IntPtr : IEquatable<nint>
    {
        private readonly void* _value;
        public static readonly nint Zero;
        public static int Size => sizeof(nint);
        public static nint MaxValue => sizeof(nint) == 8 ? unchecked((nint)long.MaxValue) : int.MaxValue;
        public static nint MinValue => sizeof(nint) == 8 ? unchecked((nint)long.MinValue) : int.MinValue;
        public IntPtr(int value) { _value = (void*)value; }
        public IntPtr(long value) { _value = (void*)value; }
        public IntPtr(void* value) { _value = value; }
        public void* ToPointer() => _value;
        public int ToInt32() => (int)(nint)_value;
        public long ToInt64() => (nint)_value;
        public override string ToString() => sizeof(nint) == 8 ? Number.Int64ToString((nint)_value) : Number.Int32ToString((int)(nint)_value);
        public bool Equals(nint other) => (nint)_value == other;
        public override bool Equals(object obj) => obj is nint v && v == (nint)_value;
        public override int GetHashCode() => (int)(nint)_value;
        public static explicit operator IntPtr(int value) => new IntPtr(value);
        public static explicit operator IntPtr(long value) => new IntPtr(value);
        public static explicit operator IntPtr(void* value) => new IntPtr(value);
        public static explicit operator void*(IntPtr value) => value._value;
        public static explicit operator int(IntPtr value) => (int)(nint)value._value;
        public static explicit operator long(IntPtr value) => (nint)value._value;
        public static bool operator ==(IntPtr value1, IntPtr value2) => value1._value == value2._value;
        public static bool operator !=(IntPtr value1, IntPtr value2) => value1._value != value2._value;
    }

    public unsafe struct UIntPtr : IEquatable<nuint>
    {
        private readonly void* _value;
        public static readonly nuint Zero;
        public static int Size => sizeof(nuint);
        public static nuint MaxValue => sizeof(nuint) == 8 ? unchecked((nuint)ulong.MaxValue) : uint.MaxValue;
        public static nuint MinValue => 0;
        public UIntPtr(uint value) { _value = (void*)value; }
        public UIntPtr(ulong value) { _value = (void*)value; }
        public UIntPtr(void* value) { _value = value; }
        public void* ToPointer() => _value;
        public uint ToUInt32() => (uint)(nuint)_value;
        public ulong ToUInt64() => (nuint)_value;
        public override string ToString() => sizeof(nuint) == 8 ? Number.UInt64ToString((nuint)_value) : Number.UInt32ToString((uint)(nuint)_value);
        public bool Equals(nuint other) => (nuint)_value == other;
        public override bool Equals(object obj) => obj is nuint v && v == (nuint)_value;
        public override int GetHashCode() => (int)(nuint)_value;
        public static explicit operator UIntPtr(uint value) => new UIntPtr(value);
        public static explicit operator UIntPtr(ulong value) => new UIntPtr(value);
        public static explicit operator UIntPtr(void* value) => new UIntPtr(value);
        public static explicit operator void*(UIntPtr value) => value._value;
        public static explicit operator uint(UIntPtr value) => (uint)(nuint)value._value;
        public static explicit operator ulong(UIntPtr value) => (nuint)value._value;
        public static bool operator ==(UIntPtr value1, UIntPtr value2) => value1._value == value2._value;
        public static bool operator !=(UIntPtr value1, UIntPtr value2) => value1._value != value2._value;
    }

    public struct Single : IEquatable<float>, IComparable<float>
    {
        private readonly float m_value;
        public const float MinValue = -3.40282346638528859e+38f;
        public const float MaxValue = 3.40282346638528859e+38f;
        public const float Epsilon = 1.4e-45f;
        public const float NaN = 0.0f / 0.0f;
        public const float PositiveInfinity = 1.0f / 0.0f;
        public const float NegativeInfinity = -1.0f / 0.0f;
        public static bool IsNaN(float f) => f != f;
        public static bool IsInfinity(float f) => f == PositiveInfinity || f == NegativeInfinity;
        public static bool IsFinite(float f) => !IsNaN(f) && !IsInfinity(f);
        public override string ToString() => Number.SingleToString(m_value, 4);
        public string ToString(string format) => Number.FormatSingle(m_value, format);
        public bool Equals(float other) => m_value == other || (IsNaN(m_value) && IsNaN(other));
        public override bool Equals(object obj) => obj is float v && Equals(v);
        public override unsafe int GetHashCode() { float v = m_value; return *(int*)&v; }
        public int CompareTo(float other) => m_value < other ? -1 : (m_value > other ? 1 : (m_value == other ? 0 : (IsNaN(m_value) ? (IsNaN(other) ? 0 : -1) : 1)));
        public static float Parse(string s) => Number.ParseSingle(s);
    }

    // Note: the Playdate CPU (Cortex-M7, fpv5-sp) has no double-precision FPU. Double arithmetic
    // works in the Simulator but traps on device; prefer float in game code.
    public struct Double : IEquatable<double>
    {
        private readonly double m_value;
        public const double MinValue = -1.7976931348623157E+308;
        public const double MaxValue = 1.7976931348623157E+308;
        public const double Epsilon = 4.9406564584124654E-324;
        public const double NaN = 0.0 / 0.0;
        public const double PositiveInfinity = 1.0 / 0.0;
        public const double NegativeInfinity = -1.0 / 0.0;
        public static bool IsNaN(double d) => d != d;
        public override string ToString() => Number.DoubleToString(m_value);
        public bool Equals(double other) => m_value == other || (IsNaN(m_value) && IsNaN(other));
        public override bool Equals(object obj) => obj is double v && Equals(v);
        public override unsafe int GetHashCode() { double v = m_value; long l = *(long*)&v; return (int)l ^ (int)(l >> 32); }
    }
}
