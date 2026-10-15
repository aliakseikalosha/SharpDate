// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using Internal.Runtime;

namespace System
{
    public abstract class ValueType
    {
        // Field-wise comparison of the raw bytes is correct for structs without references;
        // structs with references should override Equals/GetHashCode (as in .NET, for performance).
        public override unsafe bool Equals(object obj)
        {
            if (obj is null)
                return false;
            MethodTable* mt = this.GetMethodTable();
            if (obj.GetMethodTable() != mt)
                return false;
            return SpanHelpers.SequenceEqual(ref this.GetRawData(), ref obj.GetRawData(), mt->ValueTypeSize);
        }

        public override unsafe int GetHashCode()
        {
            MethodTable* mt = this.GetMethodTable();
            uint size = mt->ValueTypeSize;
            ref byte data = ref this.GetRawData();
            uint hash = 2166136261;
            for (uint i = 0; i < size; i++)
            {
                hash ^= Unsafe.Add(ref data, (nint)i);
                hash *= 16777619;
            }
            return (int)hash;
        }

        public override string ToString() => "System.ValueType";
    }

    public abstract class Enum : ValueType
    {
        public override unsafe string ToString()
        {
            MethodTable* mt = this.GetMethodTable();
            ref byte data = ref this.GetRawData();
            switch (mt->ElementType)
            {
                case EETypeElementType.SByte: return Number.Int32ToString(Unsafe.As<byte, sbyte>(ref data));
                case EETypeElementType.Byte: return Number.UInt32ToString(data);
                case EETypeElementType.Int16: return Number.Int32ToString(Unsafe.As<byte, short>(ref data));
                case EETypeElementType.UInt16: return Number.UInt32ToString(Unsafe.As<byte, ushort>(ref data));
                case EETypeElementType.Int32: return Number.Int32ToString(Unsafe.As<byte, int>(ref data));
                case EETypeElementType.UInt32: return Number.UInt32ToString(Unsafe.As<byte, uint>(ref data));
                case EETypeElementType.Int64: return Number.Int64ToString(Unsafe.As<byte, long>(ref data));
                case EETypeElementType.UInt64: return Number.UInt64ToString(Unsafe.As<byte, ulong>(ref data));
                default: return "Enum";
            }
        }

        public bool HasFlag(Enum flag) => (ToUInt64() & flag.ToUInt64()) == flag.ToUInt64();

        private unsafe ulong ToUInt64()
        {
            MethodTable* mt = this.GetMethodTable();
            ref byte data = ref this.GetRawData();
            switch (mt->ValueTypeSize)
            {
                case 1: return data;
                case 2: return Unsafe.As<byte, ushort>(ref data);
                case 4: return Unsafe.As<byte, uint>(ref data);
                default: return Unsafe.As<byte, ulong>(ref data);
            }
        }
    }

    public struct Nullable<T> where T : struct
    {
        // Layout is a contract with the runtime: hasValue flag followed by the value.
        private readonly bool hasValue;
        internal T value;

        public Nullable(T value)
        {
            this.value = value;
            hasValue = true;
        }

        public readonly bool HasValue => hasValue;

        public readonly T Value
        {
            get
            {
                if (!hasValue)
                    ThrowHelpers.ThrowInvalidOperationException("Nullable object must have a value.");
                return value;
            }
        }

        public readonly T GetValueOrDefault() => value;
        public readonly T GetValueOrDefault(T defaultValue) => hasValue ? value : defaultValue;

        public override bool Equals(object other)
        {
            if (!hasValue)
                return other is null;
            if (other is null)
                return false;
            return value.Equals(other);
        }

        public override int GetHashCode() => hasValue ? value.GetHashCode() : 0;
        public override string ToString() => hasValue ? value.ToString() : "";

        public static implicit operator T?(T value) => new T?(value);
        public static explicit operator T(T? value) => value!.Value;
    }

    public static class Nullable
    {
        public static T? GetUnderlyingType<T>() where T : struct => null;
    }

    public interface IDisposable
    {
        void Dispose();
    }

    public interface IEquatable<T>
    {
        bool Equals(T other);
    }

    public interface IComparable
    {
        int CompareTo(object obj);
    }

    public interface IComparable<in T>
    {
        int CompareTo(T other);
    }

    public interface IFormatProvider
    {
    }

    public interface IFormattable
    {
        string ToString(string format, IFormatProvider formatProvider);
    }

    public interface ICloneable
    {
        object Clone();
    }

    public readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            _value = fromEnd ? ~value : value;
        }

        private Index(int value, int _) { _value = value; }

        public static Index Start => new Index(0, 0);
        public static Index End => new Index(~0, 0);
        public static Index FromStart(int value) => new Index(value);
        public static Index FromEnd(int value) => new Index(value, true);
        public int Value => _value < 0 ? ~_value : _value;
        public bool IsFromEnd => _value < 0;
        public int GetOffset(int length) => _value < 0 ? length + _value + 1 : _value;
        public bool Equals(Index other) => _value == other._value;
        public override bool Equals(object value) => value is Index i && i._value == _value;
        public override int GetHashCode() => _value;
        public static implicit operator Index(int value) => FromStart(value);
    }

    public readonly struct Range : IEquatable<Range>
    {
        public Index Start { get; }
        public Index End { get; }

        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public static Range StartAt(Index start) => new Range(start, Index.End);
        public static Range EndAt(Index end) => new Range(Index.Start, end);
        public static Range All => new Range(Index.Start, Index.End);

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);
            if ((uint)end > (uint)length || (uint)start > (uint)end)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            return (start, end - start);
        }

        public bool Equals(Range other) => other.Start.Equals(Start) && other.End.Equals(End);
        public override bool Equals(object value) => value is Range r && Equals(r);
        public override int GetHashCode() => Start.GetHashCode() * 31 + End.GetHashCode();
    }

    public struct ValueTuple
    {
    }

    public struct ValueTuple<T1>
    {
        public T1 Item1;
        public ValueTuple(T1 item1) { Item1 = item1; }
    }

    public struct ValueTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;
        public ValueTuple(T1 item1, T2 item2) { Item1 = item1; Item2 = item2; }
    }

    public struct ValueTuple<T1, T2, T3>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public ValueTuple(T1 item1, T2 item2, T3 item3) { Item1 = item1; Item2 = item2; Item3 = item3; }
    }

    public struct ValueTuple<T1, T2, T3, T4>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4) { Item1 = item1; Item2 = item2; Item3 = item3; Item4 = item4; }
    }

    public struct ValueTuple<T1, T2, T3, T4, T5>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public T5 Item5;
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) { Item1 = item1; Item2 = item2; Item3 = item3; Item4 = item4; Item5 = item5; }
    }
}
