using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    public readonly ref struct Span<T>
    {
        internal readonly ref T _reference;
        private readonly int _length;

        public Span(T[] array)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            _reference = ref MemoryMarshal.GetArrayDataReference(array);
            _length = array.Length;
        }

        public Span(T[] array, int start, int length)
        {
            if (array is null)
            {
                if (start != 0 || length != 0)
                    ThrowHelpers.ThrowArgumentOutOfRangeException();
                this = default;
                return;
            }
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            _reference = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), start);
            _length = length;
        }

        public unsafe Span(void* pointer, int length)
        {
            if (length < 0)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            _reference = ref Unsafe.AsRef<T>(pointer);
            _length = length;
        }

        public Span(ref T reference)
        {
            _reference = ref reference;
            _length = 1;
        }

        internal Span(ref T reference, int length)
        {
            _reference = ref reference;
            _length = length;
        }

        public ref T this[int index]
        {
            [Intrinsic]
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelpers.ThrowIndexOutOfRangeException();
                return ref Unsafe.Add(ref _reference, (nint)(uint)index);
            }
        }

        public int Length => _length;
        public bool IsEmpty => _length == 0;
        public static Span<T> Empty => default;

        public ref T GetPinnableReference()
        {
            ref T ret = ref Unsafe.NullRef<T>();
            if (_length != 0)
                ret = ref _reference;
            return ref ret;
        }

        public void Clear()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                for (int i = 0; i < _length; i++)
                    Unsafe.Add(ref _reference, i) = default;
            }
            else
            {
                SpanHelpers.ClearWithoutReferences(ref Unsafe.As<T, byte>(ref _reference), (nuint)_length * (nuint)Unsafe.SizeOf<T>());
            }
        }

        public void Fill(T value)
        {
            for (int i = 0; i < _length; i++)
                Unsafe.Add(ref _reference, i) = value;
        }

        public void CopyTo(Span<T> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                ThrowHelpers.ThrowArgumentException();
            Buffer.Memmove(ref destination._reference, ref _reference, (nuint)_length);
        }

        public bool TryCopyTo(Span<T> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                return false;
            Buffer.Memmove(ref destination._reference, ref _reference, (nuint)_length);
            return true;
        }

        public Span<T> Slice(int start)
        {
            if ((uint)start > (uint)_length)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            return new Span<T>(ref Unsafe.Add(ref _reference, (nint)(uint)start), _length - start);
        }

        public Span<T> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            return new Span<T>(ref Unsafe.Add(ref _reference, (nint)(uint)start), length);
        }

        public T[] ToArray()
        {
            if (_length == 0)
                return Array.Empty<T>();
            T[] result = new T[_length];
            Buffer.Memmove(ref MemoryMarshal.GetArrayDataReference(result), ref _reference, (nuint)_length);
            return result;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public static implicit operator Span<T>(T[] array) => new Span<T>(array);

        public static implicit operator ReadOnlySpan<T>(Span<T> span) => new ReadOnlySpan<T>(ref span._reference, span._length);

        public override string ToString() => "System.Span";

        public ref struct Enumerator
        {
            private readonly Span<T> _span;
            private int _index;

            internal Enumerator(Span<T> span)
            {
                _span = span;
                _index = -1;
            }

            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _span.Length)
                {
                    _index = index;
                    return true;
                }
                return false;
            }

            public ref T Current => ref _span[_index];
        }
    }

    public readonly ref struct ReadOnlySpan<T>
    {
        internal readonly ref T _reference;
        private readonly int _length;

        public ReadOnlySpan(T[] array)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            _reference = ref MemoryMarshal.GetArrayDataReference(array);
            _length = array.Length;
        }

        public ReadOnlySpan(T[] array, int start, int length)
        {
            if (array is null)
            {
                if (start != 0 || length != 0)
                    ThrowHelpers.ThrowArgumentOutOfRangeException();
                this = default;
                return;
            }
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            _reference = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), start);
            _length = length;
        }

        public unsafe ReadOnlySpan(void* pointer, int length)
        {
            if (length < 0)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            _reference = ref Unsafe.AsRef<T>(pointer);
            _length = length;
        }

        public ReadOnlySpan(ref readonly T reference)
        {
            _reference = ref Unsafe.AsRef(in reference);
            _length = 1;
        }

        internal ReadOnlySpan(ref T reference, int length)
        {
            _reference = ref reference;
            _length = length;
        }

        public ref readonly T this[int index]
        {
            [Intrinsic]
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelpers.ThrowIndexOutOfRangeException();
                return ref Unsafe.Add(ref _reference, (nint)(uint)index);
            }
        }

        public int Length => _length;
        public bool IsEmpty => _length == 0;
        public static ReadOnlySpan<T> Empty => default;

        public ref readonly T GetPinnableReference()
        {
            ref T ret = ref Unsafe.NullRef<T>();
            if (_length != 0)
                ret = ref _reference;
            return ref ret;
        }

        public void CopyTo(Span<T> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                ThrowHelpers.ThrowArgumentException();
            Buffer.Memmove(ref destination._reference, ref _reference, (nuint)_length);
        }

        public bool TryCopyTo(Span<T> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                return false;
            Buffer.Memmove(ref destination._reference, ref _reference, (nuint)_length);
            return true;
        }

        public ReadOnlySpan<T> Slice(int start)
        {
            if ((uint)start > (uint)_length)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            return new ReadOnlySpan<T>(ref Unsafe.Add(ref _reference, (nint)(uint)start), _length - start);
        }

        public ReadOnlySpan<T> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            return new ReadOnlySpan<T>(ref Unsafe.Add(ref _reference, (nint)(uint)start), length);
        }

        public T[] ToArray()
        {
            if (_length == 0)
                return Array.Empty<T>();
            T[] result = new T[_length];
            Buffer.Memmove(ref MemoryMarshal.GetArrayDataReference(result), ref _reference, (nuint)_length);
            return result;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public static implicit operator ReadOnlySpan<T>(T[] array) => new ReadOnlySpan<T>(array);

        public override string ToString()
        {
            if (typeof(T) == typeof(char))
                return new string(new ReadOnlySpan<char>(ref Unsafe.As<T, char>(ref _reference), _length));
            return "System.ReadOnlySpan";
        }

        public ref struct Enumerator
        {
            private readonly ReadOnlySpan<T> _span;
            private int _index;

            internal Enumerator(ReadOnlySpan<T> span)
            {
                _span = span;
                _index = -1;
            }

            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _span.Length)
                {
                    _index = index;
                    return true;
                }
                return false;
            }

            public ref readonly T Current => ref _span[_index];
        }
    }

    public static class MemoryExtensions
    {
        public static ReadOnlySpan<char> AsSpan(this string text) => text is null ? default : new ReadOnlySpan<char>(ref text.GetRawStringData(), text.Length);
        public static ReadOnlySpan<char> AsSpan(this string text, int start) => text.AsSpan().Slice(start);
        public static ReadOnlySpan<char> AsSpan(this string text, int start, int length) => text.AsSpan().Slice(start, length);

        public static Span<T> AsSpan<T>(this T[] array) => new Span<T>(array);
        public static Span<T> AsSpan<T>(this T[] array, int start) => new Span<T>(array, start, array.Length - start);
        public static Span<T> AsSpan<T>(this T[] array, int start, int length) => new Span<T>(array, start, length);

        public static bool SequenceEqual(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> other)
            => span.Length == other.Length && SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(span), ref MemoryMarshal.GetReference(other), (nuint)span.Length);

        public static bool SequenceEqual(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
            => span.Length == other.Length && SpanHelpers.SequenceEqual(
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(span)),
                ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(other)), (nuint)span.Length * 2);

        public static bool SequenceEqual(this Span<byte> span, ReadOnlySpan<byte> other) => ((ReadOnlySpan<byte>)span).SequenceEqual(other);

        public static int IndexOf(this ReadOnlySpan<byte> span, byte value)
        {
            for (int i = 0; i < span.Length; i++)
                if (span[i] == value)
                    return i;
            return -1;
        }

        public static int IndexOf(this ReadOnlySpan<char> span, char value)
        {
            for (int i = 0; i < span.Length; i++)
                if (span[i] == value)
                    return i;
            return -1;
        }
    }
}
