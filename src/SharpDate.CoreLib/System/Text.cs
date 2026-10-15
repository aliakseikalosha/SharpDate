using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text
{
    public sealed class StringBuilder
    {
        private char[] _chars;
        private int _length;

        public StringBuilder() : this(16) { }

        public StringBuilder(int capacity)
        {
            _chars = new char[capacity < 4 ? 4 : capacity];
        }

        public StringBuilder(string value) : this(value is null ? 16 : value.Length + 16)
        {
            Append(value);
        }

        public int Length
        {
            get => _length;
            set
            {
                if (value < 0)
                    ThrowHelpers.ThrowArgumentOutOfRangeException();
                EnsureCapacity(value);
                for (int i = _length; i < value; i++)
                    _chars[i] = '\0';
                _length = value;
            }
        }

        public int Capacity => _chars.Length;

        public char this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelpers.ThrowIndexOutOfRangeException();
                return _chars[index];
            }
            set
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelpers.ThrowIndexOutOfRangeException();
                _chars[index] = value;
            }
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= _chars.Length)
                return;
            int newCapacity = _chars.Length * 2;
            if (newCapacity < capacity)
                newCapacity = capacity;
            char[] newChars = new char[newCapacity];
            Array.Copy(_chars, newChars, _length);
            _chars = newChars;
        }

        public StringBuilder Append(char value)
        {
            if (_length == _chars.Length)
                EnsureCapacity(_length + 1);
            _chars[_length++] = value;
            return this;
        }

        public StringBuilder Append(char value, int repeatCount)
        {
            EnsureCapacity(_length + repeatCount);
            for (int i = 0; i < repeatCount; i++)
                _chars[_length++] = value;
            return this;
        }

        public StringBuilder Append(string value)
        {
            if (value is null)
                return this;
            return Append(value, 0, value.Length);
        }

        public StringBuilder Append(string value, int startIndex, int count)
        {
            if (value is null || count == 0)
                return this;
            EnsureCapacity(_length + count);
            for (int i = 0; i < count; i++)
                _chars[_length + i] = value[startIndex + i];
            _length += count;
            return this;
        }

        public StringBuilder Append(ReadOnlySpan<char> value)
        {
            EnsureCapacity(_length + value.Length);
            value.CopyTo(new Span<char>(_chars, _length, value.Length));
            _length += value.Length;
            return this;
        }

        public StringBuilder Append(char[] value) => value is null ? this : Append(new ReadOnlySpan<char>(value));
        public StringBuilder Append(int value) => Append(Number.Int32ToString(value));
        public StringBuilder Append(uint value) => Append(Number.UInt32ToString(value));
        public StringBuilder Append(long value) => Append(Number.Int64ToString(value));
        public StringBuilder Append(ulong value) => Append(Number.UInt64ToString(value));
        public StringBuilder Append(float value) => Append(Number.SingleToString(value, 4));
        public StringBuilder Append(double value) => Append(Number.DoubleToString(value));
        public StringBuilder Append(bool value) => Append(value ? "True" : "False");
        public StringBuilder Append(object value) => value is null ? this : Append(value.ToString());

        public StringBuilder AppendLine() => Append('\n');
        public StringBuilder AppendLine(string value) => Append(value).Append('\n');

        public StringBuilder AppendFormat(string format, object arg0) => Append(string.Format(format, arg0));
        public StringBuilder AppendFormat(string format, object arg0, object arg1) => Append(string.Format(format, arg0, arg1));
        public StringBuilder AppendFormat(string format, params object[] args) => Append(string.Format(format, args));

        public StringBuilder Insert(int index, string value)
        {
            if ((uint)index > (uint)_length)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            if (string.IsNullOrEmpty(value))
                return this;
            EnsureCapacity(_length + value.Length);
            Array.Copy(_chars, index, _chars, index + value.Length, _length - index);
            for (int i = 0; i < value.Length; i++)
                _chars[index + i] = value[i];
            _length += value.Length;
            return this;
        }

        public StringBuilder Remove(int startIndex, int length)
        {
            if ((uint)startIndex > (uint)_length || (uint)length > (uint)(_length - startIndex))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            Array.Copy(_chars, startIndex + length, _chars, startIndex, _length - startIndex - length);
            _length -= length;
            return this;
        }

        public StringBuilder Clear()
        {
            _length = 0;
            return this;
        }

        public override string ToString() => new string(_chars, 0, _length);

        public string ToString(int startIndex, int length) => new string(_chars, startIndex, length);
    }

    public abstract class Encoding
    {
        public static UTF8Encoding UTF8 { get; } = new UTF8Encoding();
    }

    /// <summary>UTF-8 conversion (Playdate APIs take UTF-8 strings).</summary>
    public sealed unsafe class UTF8Encoding : Encoding
    {
        public int GetByteCount(string s) => s is null ? 0 : GetByteCount((ReadOnlySpan<char>)s);

        public int GetByteCount(ReadOnlySpan<char> chars)
        {
            int count = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c < 0x80) count += 1;
                else if (c < 0x800) count += 2;
                else if (c >= 0xD800 && c <= 0xDBFF && i + 1 < chars.Length && chars[i + 1] >= 0xDC00 && chars[i + 1] <= 0xDFFF)
                {
                    count += 4;
                    i++;
                }
                else count += 3;
            }
            return count;
        }

        public int GetBytes(string s, Span<byte> bytes) => GetBytes((ReadOnlySpan<char>)s, bytes);

        public int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            int n = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                uint c = chars[i];
                if (c >= 0xD800 && c <= 0xDBFF && i + 1 < chars.Length && chars[i + 1] >= 0xDC00 && chars[i + 1] <= 0xDFFF)
                {
                    c = 0x10000 + ((c - 0xD800) << 10) + ((uint)chars[i + 1] - 0xDC00);
                    i++;
                }
                if (c < 0x80)
                {
                    bytes[n++] = (byte)c;
                }
                else if (c < 0x800)
                {
                    bytes[n++] = (byte)(0xC0 | (c >> 6));
                    bytes[n++] = (byte)(0x80 | (c & 0x3F));
                }
                else if (c < 0x10000)
                {
                    bytes[n++] = (byte)(0xE0 | (c >> 12));
                    bytes[n++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                    bytes[n++] = (byte)(0x80 | (c & 0x3F));
                }
                else
                {
                    bytes[n++] = (byte)(0xF0 | (c >> 18));
                    bytes[n++] = (byte)(0x80 | ((c >> 12) & 0x3F));
                    bytes[n++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                    bytes[n++] = (byte)(0x80 | (c & 0x3F));
                }
            }
            return n;
        }

        public byte[] GetBytes(string s)
        {
            byte[] result = new byte[GetByteCount(s)];
            GetBytes(s, result);
            return result;
        }

        public int GetCharCount(ReadOnlySpan<byte> bytes)
        {
            int count = 0;
            for (int i = 0; i < bytes.Length;)
            {
                byte b = bytes[i];
                int len = b < 0x80 ? 1 : b < 0xE0 ? 2 : b < 0xF0 ? 3 : 4;
                count += len == 4 ? 2 : 1;
                i += len;
            }
            return count;
        }

        public string GetString(byte[] bytes) => GetString(new ReadOnlySpan<byte>(bytes));

        public string GetString(byte[] bytes, int index, int count) => GetString(new ReadOnlySpan<byte>(bytes, index, count));

        public string GetString(byte* bytes, int byteCount) => GetString(new ReadOnlySpan<byte>(bytes, byteCount));

        public string GetString(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
                return string.Empty;
            string result = string.FastAllocateString(GetCharCount(bytes));
            ref char dst = ref result.GetRawStringData();
            int n = 0;
            for (int i = 0; i < bytes.Length;)
            {
                uint b = bytes[i];
                uint c;
                int len;
                if (b < 0x80) { c = b; len = 1; }
                else if (b < 0xE0) { c = b & 0x1F; len = 2; }
                else if (b < 0xF0) { c = b & 0x0F; len = 3; }
                else { c = b & 0x07; len = 4; }
                for (int k = 1; k < len; k++)
                    c = (c << 6) | (i + k < bytes.Length ? (uint)(bytes[i + k] & 0x3F) : 0);
                i += len;
                if (c >= 0x10000)
                {
                    c -= 0x10000;
                    Unsafe.Add(ref dst, n++) = (char)(0xD800 + (c >> 10));
                    Unsafe.Add(ref dst, n++) = (char)(0xDC00 + (c & 0x3FF));
                }
                else
                {
                    Unsafe.Add(ref dst, n++) = (char)c;
                }
            }
            return result;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>Builds interpolated strings ($"...") without boxing value-type arguments.</summary>
    [InterpolatedStringHandler]
    public ref struct DefaultInterpolatedStringHandler
    {
        private Text.StringBuilder _builder;

        public DefaultInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            _builder = new Text.StringBuilder(literalLength + formattedCount * 8);
        }

        public void AppendLiteral(string value) => _builder.Append(value);

        public void AppendFormatted(string value) => _builder.Append(value);
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder.Append(value);
        public void AppendFormatted(int value) => _builder.Append(Number.Int32ToString(value));
        public void AppendFormatted(int value, string format) => _builder.Append(Number.FormatInt32(value, format));
        public void AppendFormatted(uint value) => _builder.Append(Number.UInt32ToString(value));
        public void AppendFormatted(uint value, string format) => _builder.Append(Number.FormatUInt32(value, format));
        public void AppendFormatted(long value) => _builder.Append(Number.Int64ToString(value));
        public void AppendFormatted(float value) => _builder.Append(Number.SingleToString(value, 4));
        public void AppendFormatted(float value, string format) => _builder.Append(Number.FormatSingle(value, format));
        public void AppendFormatted(bool value) => _builder.Append(value ? "True" : "False");
        public void AppendFormatted(char value) => _builder.Append(value);

        public void AppendFormatted<T>(T value)
        {
            if (value is not null)
                _builder.Append(value.ToString());
        }

        public void AppendFormatted<T>(T value, string format)
        {
            if (value is null)
                return;
            _builder.Append(Number.FormatObject(value, format));
        }

        public void AppendFormatted<T>(T value, int alignment)
        {
            string s = value is null ? string.Empty : value.ToString();
            AppendAligned(s, alignment);
        }

        public void AppendFormatted<T>(T value, int alignment, string format)
        {
            string s = value is null ? string.Empty : Number.FormatObject(value, format);
            AppendAligned(s, alignment);
        }

        private void AppendAligned(string s, int alignment)
        {
            if (alignment > 0)
                _builder.Append(s.PadLeft(alignment));
            else
                _builder.Append(s.PadRight(-alignment));
        }

        public override string ToString() => _builder.ToString();

        public string ToStringAndClear()
        {
            string result = _builder.ToString();
            _builder = null;
            return result;
        }
    }
}
