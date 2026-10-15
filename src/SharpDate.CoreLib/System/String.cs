using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public sealed unsafe class String : IEquatable<string>, IComparable<string>
    {
        // Layout is a contract with the compiler: [MethodTable*][int length][char data...][\0]
        private readonly int _stringLength;
        private char _firstChar;

        public static readonly string Empty = "";

        public int Length => _stringLength;

        [IndexerName("Chars")]
        public char this[int index]
        {
            [Intrinsic]
            get
            {
                if ((uint)index >= (uint)_stringLength)
                    ThrowHelpers.ThrowIndexOutOfRangeException();
                return Unsafe.Add(ref _firstChar, index);
            }
        }

        public ref readonly char GetPinnableReference() => ref _firstChar;

        internal ref char GetRawStringData() => ref _firstChar;

        // ---- Constructors. The compiler redirects `new string(...)` to the matching static Ctor method. ----

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(char* value);

        private static string Ctor(char* value)
        {
            if (value == null)
                return Empty;
            int length = 0;
            while (value[length] != 0)
                length++;
            return Ctor(value, 0, length);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(char* value, int startIndex, int length);

        private static string Ctor(char* value, int startIndex, int length)
        {
            if (length <= 0)
                return Empty;
            string result = FastAllocateString(length);
            Buffer.Memmove(ref Unsafe.As<char, byte>(ref result._firstChar), ref *(byte*)(value + startIndex), (nuint)length * 2);
            return result;
        }

        // Creates a string from a NUL-terminated UTF-8 buffer.
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(sbyte* value);

        private static string Ctor(sbyte* value)
        {
            if (value == null)
                return Empty;
            int length = 0;
            while (value[length] != 0)
                length++;
            return Text.Encoding.UTF8.GetString((byte*)value, length);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(sbyte* value, int startIndex, int length);

        private static string Ctor(sbyte* value, int startIndex, int length)
            => Text.Encoding.UTF8.GetString((byte*)value + startIndex, length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(char[] value);

        private static string Ctor(char[] value)
        {
            if (value == null || value.Length == 0)
                return Empty;
            fixed (char* p = value)
                return Ctor(p, 0, value.Length);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(char[] value, int startIndex, int length);

        private static string Ctor(char[] value, int startIndex, int length)
        {
            if ((uint)startIndex > (uint)value.Length || (uint)length > (uint)(value.Length - startIndex))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            fixed (char* p = value)
                return Ctor(p, startIndex, length);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(char c, int count);

        private static string Ctor(char c, int count)
        {
            if (count <= 0)
                return Empty;
            string result = FastAllocateString(count);
            for (int i = 0; i < count; i++)
                Unsafe.Add(ref result._firstChar, i) = c;
            return result;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern String(ReadOnlySpan<char> value);

        private static string Ctor(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return Empty;
            string result = FastAllocateString(value.Length);
            Buffer.Memmove(ref Unsafe.As<char, byte>(ref result._firstChar), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value)), (nuint)value.Length * 2);
            return result;
        }

        internal static string FastAllocateString(int length)
        {
            return Unsafe.As<string>(RuntimeImports.RhNewArray(MethodTable.Of<string>(), length));
        }

        internal static string FromChar(char c)
        {
            string s = FastAllocateString(1);
            s._firstChar = c;
            return s;
        }

        // ---- Queries ----

        public static bool IsNullOrEmpty(string value) => value is null || value._stringLength == 0;

        public static bool IsNullOrWhiteSpace(string value)
        {
            if (value is null)
                return true;
            for (int i = 0; i < value._stringLength; i++)
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            return true;
        }

        public bool Equals(string value)
        {
            if (RuntimeHelpers.ObjectReferenceEquals(this, value))
                return true;
            if (value is null || value._stringLength != _stringLength)
                return false;
            return SpanHelpers.SequenceEqual(ref Unsafe.As<char, byte>(ref _firstChar), ref Unsafe.As<char, byte>(ref value._firstChar), (nuint)_stringLength * 2);
        }

        public override bool Equals(object obj) => obj is string s && Equals(s);

        public static bool Equals(string a, string b)
        {
            if (RuntimeHelpers.ObjectReferenceEquals(a, b))
                return true;
            if (a is null || b is null)
                return false;
            return a.Equals(b);
        }

        public static bool operator ==(string a, string b) => Equals(a, b);
        public static bool operator !=(string a, string b) => !Equals(a, b);

        public override int GetHashCode()
        {
            // FNV-1a over UTF-16 code units
            uint hash = 2166136261;
            for (int i = 0; i < _stringLength; i++)
            {
                hash ^= Unsafe.Add(ref _firstChar, i);
                hash *= 16777619;
            }
            return (int)hash;
        }

        public int CompareTo(string other) => CompareOrdinal(this, other);

        public static int CompareOrdinal(string a, string b)
        {
            if (RuntimeHelpers.ObjectReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            int n = a._stringLength < b._stringLength ? a._stringLength : b._stringLength;
            for (int i = 0; i < n; i++)
            {
                int d = a[i] - b[i];
                if (d != 0)
                    return d;
            }
            return a._stringLength - b._stringLength;
        }

        public static int Compare(string a, string b) => CompareOrdinal(a, b);

        public override string ToString() => this;

        public int IndexOf(char value) => IndexOf(value, 0);

        public int IndexOf(char value, int startIndex)
        {
            for (int i = startIndex; i < _stringLength; i++)
                if (Unsafe.Add(ref _firstChar, i) == value)
                    return i;
            return -1;
        }

        public int IndexOf(string value) => IndexOf(value, 0);

        public int IndexOf(string value, int startIndex)
        {
            if (value is null)
                ThrowHelpers.ThrowArgumentNullException();
            int last = _stringLength - value._stringLength;
            for (int i = startIndex; i <= last; i++)
            {
                int j = 0;
                while (j < value._stringLength && Unsafe.Add(ref _firstChar, i + j) == value[j])
                    j++;
                if (j == value._stringLength)
                    return i;
            }
            return -1;
        }

        public int LastIndexOf(char value)
        {
            for (int i = _stringLength - 1; i >= 0; i--)
                if (Unsafe.Add(ref _firstChar, i) == value)
                    return i;
            return -1;
        }

        public bool Contains(char value) => IndexOf(value) >= 0;
        public bool Contains(string value) => IndexOf(value) >= 0;

        public bool StartsWith(string value)
        {
            if (value._stringLength > _stringLength)
                return false;
            for (int i = 0; i < value._stringLength; i++)
                if (Unsafe.Add(ref _firstChar, i) != value[i])
                    return false;
            return true;
        }

        public bool EndsWith(string value)
        {
            int offset = _stringLength - value._stringLength;
            if (offset < 0)
                return false;
            for (int i = 0; i < value._stringLength; i++)
                if (Unsafe.Add(ref _firstChar, offset + i) != value[i])
                    return false;
            return true;
        }

        public bool StartsWith(char value) => _stringLength > 0 && _firstChar == value;
        public bool EndsWith(char value) => _stringLength > 0 && Unsafe.Add(ref _firstChar, _stringLength - 1) == value;

        // ---- Producing new strings ----

        public string Substring(int startIndex) => Substring(startIndex, _stringLength - startIndex);

        public string Substring(int startIndex, int length)
        {
            if ((uint)startIndex > (uint)_stringLength || (uint)length > (uint)(_stringLength - startIndex))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            if (length == 0)
                return Empty;
            if (startIndex == 0 && length == _stringLength)
                return this;
            fixed (char* p = &_firstChar)
                return Ctor(p, startIndex, length);
        }

        public static implicit operator ReadOnlySpan<char>(string value)
            => value is null ? default : new ReadOnlySpan<char>(ref value._firstChar, value._stringLength);

        public char[] ToCharArray()
        {
            char[] result = new char[_stringLength];
            for (int i = 0; i < _stringLength; i++)
                result[i] = Unsafe.Add(ref _firstChar, i);
            return result;
        }

        public string ToUpper()
        {
            string result = FastAllocateString(_stringLength);
            for (int i = 0; i < _stringLength; i++)
                Unsafe.Add(ref result._firstChar, i) = char.ToUpper(Unsafe.Add(ref _firstChar, i));
            return result;
        }

        public string ToLower()
        {
            string result = FastAllocateString(_stringLength);
            for (int i = 0; i < _stringLength; i++)
                Unsafe.Add(ref result._firstChar, i) = char.ToLower(Unsafe.Add(ref _firstChar, i));
            return result;
        }

        public string ToUpperInvariant() => ToUpper();
        public string ToLowerInvariant() => ToLower();

        public string Trim()
        {
            int start = 0, end = _stringLength - 1;
            while (start <= end && char.IsWhiteSpace(this[start])) start++;
            while (end >= start && char.IsWhiteSpace(this[end])) end--;
            return Substring(start, end - start + 1);
        }

        public string TrimStart()
        {
            int start = 0;
            while (start < _stringLength && char.IsWhiteSpace(this[start])) start++;
            return Substring(start);
        }

        public string TrimEnd()
        {
            int end = _stringLength - 1;
            while (end >= 0 && char.IsWhiteSpace(this[end])) end--;
            return Substring(0, end + 1);
        }

        public string PadLeft(int totalWidth, char paddingChar = ' ')
        {
            int pad = totalWidth - _stringLength;
            if (pad <= 0)
                return this;
            return Concat(new string(paddingChar, pad), this);
        }

        public string PadRight(int totalWidth, char paddingChar = ' ')
        {
            int pad = totalWidth - _stringLength;
            if (pad <= 0)
                return this;
            return Concat(this, new string(paddingChar, pad));
        }

        public string Replace(char oldChar, char newChar)
        {
            string result = FastAllocateString(_stringLength);
            for (int i = 0; i < _stringLength; i++)
            {
                char c = Unsafe.Add(ref _firstChar, i);
                Unsafe.Add(ref result._firstChar, i) = c == oldChar ? newChar : c;
            }
            return result;
        }

        public string Replace(string oldValue, string newValue)
        {
            newValue ??= Empty;
            var sb = new Text.StringBuilder(_stringLength);
            int i = 0;
            while (true)
            {
                int idx = IndexOf(oldValue, i);
                if (idx < 0)
                    break;
                sb.Append(this, i, idx - i);
                sb.Append(newValue);
                i = idx + oldValue._stringLength;
            }
            sb.Append(this, i, _stringLength - i);
            return sb.ToString();
        }

        public string[] Split(char separator)
        {
            int count = 1;
            for (int i = 0; i < _stringLength; i++)
                if (Unsafe.Add(ref _firstChar, i) == separator)
                    count++;
            string[] parts = new string[count];
            int start = 0, n = 0;
            for (int i = 0; i <= _stringLength; i++)
            {
                if (i == _stringLength || Unsafe.Add(ref _firstChar, i) == separator)
                {
                    parts[n++] = Substring(start, i - start);
                    start = i + 1;
                }
            }
            return parts;
        }

        public static string Join(string separator, params string[] values)
        {
            var sb = new Text.StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(separator);
                sb.Append(values[i]);
            }
            return sb.ToString();
        }

        // ---- Concatenation (targets of the C# `+` operator) ----

        public static string Concat(string str0, string str1)
        {
            if (IsNullOrEmpty(str0))
                return str1 ?? Empty;
            if (IsNullOrEmpty(str1))
                return str0;
            string result = FastAllocateString(str0._stringLength + str1._stringLength);
            CopyInto(result, 0, str0);
            CopyInto(result, str0._stringLength, str1);
            return result;
        }

        public static string Concat(string str0, string str1, string str2)
            => Concat(Concat(str0, str1), str2);

        public static string Concat(string str0, string str1, string str2, string str3)
            => Concat(Concat(str0, str1), Concat(str2, str3));

        public static string Concat(params string[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i] is null ? 0 : values[i]._stringLength;
            if (total == 0)
                return Empty;
            string result = FastAllocateString(total);
            int pos = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] is null)
                    continue;
                CopyInto(result, pos, values[i]);
                pos += values[i]._stringLength;
            }
            return result;
        }

        public static string Concat(object arg0) => arg0?.ToString() ?? Empty;
        public static string Concat(object arg0, object arg1) => Concat(arg0?.ToString(), arg1?.ToString());
        public static string Concat(object arg0, object arg1, object arg2) => Concat(arg0?.ToString(), arg1?.ToString(), arg2?.ToString());

        public static string Concat(params object[] args)
        {
            string[] strings = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
                strings[i] = args[i]?.ToString();
            return Concat(strings);
        }

        public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1)
        {
            string result = FastAllocateString(str0.Length + str1.Length);
            Span<char> dst = new Span<char>(ref result._firstChar, result._stringLength);
            str0.CopyTo(dst);
            str1.CopyTo(dst.Slice(str0.Length));
            return result;
        }

        private static void CopyInto(string dest, int destPos, string src)
        {
            Buffer.Memmove(
                ref Unsafe.As<char, byte>(ref Unsafe.Add(ref dest._firstChar, destPos)),
                ref Unsafe.As<char, byte>(ref src._firstChar),
                (nuint)src._stringLength * 2);
        }

        // ---- Formatting: supports {index} and {index:format} placeholders ----

        public static string Format(string format, object arg0) => FormatCore(format, new object[] { arg0 });
        public static string Format(string format, object arg0, object arg1) => FormatCore(format, new object[] { arg0, arg1 });
        public static string Format(string format, object arg0, object arg1, object arg2) => FormatCore(format, new object[] { arg0, arg1, arg2 });
        public static string Format(string format, params object[] args) => FormatCore(format, args);

        private static string FormatCore(string format, object[] args)
        {
            var sb = new Text.StringBuilder(format.Length + 16);
            int i = 0;
            while (i < format.Length)
            {
                char c = format[i];
                if (c == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{')
                    {
                        sb.Append('{');
                        i += 2;
                        continue;
                    }
                    int close = format.IndexOf('}', i);
                    if (close < 0)
                        ThrowHelpers.ThrowFormatException();
                    int colon = format.IndexOf(':', i);
                    string itemFormat = null;
                    int indexEnd = close;
                    if (colon > i && colon < close)
                    {
                        itemFormat = format.Substring(colon + 1, close - colon - 1);
                        indexEnd = colon;
                    }
                    int index = Number.ParseInt32(format.Substring(i + 1, indexEnd - i - 1));
                    sb.Append(Number.FormatObject(args[index], itemFormat));
                    i = close + 1;
                }
                else if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
