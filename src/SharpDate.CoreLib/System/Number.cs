// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    // Number formatting and parsing (invariant culture only).
    internal static unsafe class Number
    {
        internal static string Int32ToString(int value)
        {
            if (value >= 0)
                return UInt32ToString((uint)value);
            char* buffer = stackalloc char[11];
            char* end = buffer + 11;
            char* p = WriteUInt32Digits(end, (uint)(-(long)value));
            *--p = '-';
            return new string(p, 0, (int)(end - p));
        }

        internal static string UInt32ToString(uint value)
        {
            if (value < 10)
                return SmallNumbers[value];
            char* buffer = stackalloc char[10];
            char* end = buffer + 10;
            char* p = WriteUInt32Digits(end, value);
            return new string(p, 0, (int)(end - p));
        }

        private static readonly string[] SmallNumbers = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        internal static string Int64ToString(long value)
        {
            if (value >= 0)
                return UInt64ToString((ulong)value);
            char* buffer = stackalloc char[20];
            char* end = buffer + 20;
            char* p = WriteUInt64Digits(end, (ulong)(-value));
            *--p = '-';
            return new string(p, 0, (int)(end - p));
        }

        internal static string UInt64ToString(ulong value)
        {
            if (value <= uint.MaxValue)
                return UInt32ToString((uint)value);
            char* buffer = stackalloc char[20];
            char* end = buffer + 20;
            char* p = WriteUInt64Digits(end, value);
            return new string(p, 0, (int)(end - p));
        }

        private static char* WriteUInt32Digits(char* end, uint value)
        {
            char* p = end;
            do
            {
                uint q = value / 10;
                *--p = (char)('0' + (value - q * 10));
                value = q;
            }
            while (value != 0);
            return p;
        }

        private static char* WriteUInt64Digits(char* end, ulong value)
        {
            char* p = end;
            // Peel off 9 digits at a time so most work happens in 32-bit arithmetic.
            while (value > uint.MaxValue)
            {
                ulong q = value / 1000000000;
                uint low = (uint)(value - q * 1000000000);
                for (int i = 0; i < 9; i++)
                {
                    uint lq = low / 10;
                    *--p = (char)('0' + (low - lq * 10));
                    low = lq;
                }
                value = q;
            }
            return WriteUInt32Digits(p, (uint)value);
        }

        // Supports D[n], X[n], x[n], N[n], F[n], G.
        internal static string FormatInt32(int value, string format)
        {
            if (string.IsNullOrEmpty(format) || format == "G" || format == "g")
                return Int32ToString(value);
            char kind = format[0];
            int precision = ParsePrecision(format, -1);
            if (kind == 'X' || kind == 'x')
                return FormatHex((uint)value, precision, kind == 'X');
            if (kind == 'D' || kind == 'd')
            {
                string digits = value < 0 ? UInt64ToString((ulong)(-(long)value)) : UInt32ToString((uint)value);
                if (precision > digits.Length)
                    digits = digits.PadLeft(precision, '0');
                return value < 0 ? string.Concat("-", digits) : digits;
            }
            if (kind == 'N' || kind == 'n' || kind == 'F' || kind == 'f')
                return FormatFixed(value < 0, value < 0 ? (ulong)(-(long)value) : (ulong)value, 0, precision < 0 ? 2 : precision, kind == 'N' || kind == 'n');
            return Int32ToString(value);
        }

        internal static string FormatUInt32(uint value, string format)
        {
            if (string.IsNullOrEmpty(format) || format == "G" || format == "g")
                return UInt32ToString(value);
            char kind = format[0];
            int precision = ParsePrecision(format, -1);
            if (kind == 'X' || kind == 'x')
                return FormatHex(value, precision, kind == 'X');
            if (kind == 'D' || kind == 'd')
            {
                string digits = UInt32ToString(value);
                return precision > digits.Length ? digits.PadLeft(precision, '0') : digits;
            }
            if (kind == 'N' || kind == 'n' || kind == 'F' || kind == 'f')
                return FormatFixed(false, value, 0, precision < 0 ? 2 : precision, kind == 'N' || kind == 'n');
            return UInt32ToString(value);
        }

        private static string FormatHex(uint value, int minDigits, bool upper)
        {
            char* buffer = stackalloc char[8];
            int n = 0;
            do
            {
                uint nibble = value & 0xF;
                buffer[7 - n] = (char)(nibble < 10 ? '0' + nibble : (upper ? 'A' : 'a') + nibble - 10);
                value >>= 4;
                n++;
            }
            while (value != 0);
            string s = new string(buffer, 8 - n, n);
            return minDigits > n ? s.PadLeft(minDigits, '0') : s;
        }

        private static int ParsePrecision(string format, int defaultValue)
        {
            if (format.Length < 2)
                return defaultValue;
            int result = 0;
            for (int i = 1; i < format.Length; i++)
            {
                char c = format[i];
                if (c < '0' || c > '9')
                    return defaultValue;
                result = result * 10 + (c - '0');
            }
            return result;
        }

        // integerPart.fraction with `decimals` digits; fraction is already scaled to `decimals` digits.
        private static string FormatFixed(bool negative, ulong integerPart, ulong fraction, int decimals, bool groupThousands)
        {
            var sb = new Text.StringBuilder(32);
            if (negative)
                sb.Append('-');
            string digits = UInt64ToString(integerPart);
            if (groupThousands)
            {
                int first = digits.Length % 3;
                if (first == 0)
                    first = 3;
                sb.Append(digits, 0, first);
                for (int i = first; i < digits.Length; i += 3)
                {
                    sb.Append(',');
                    sb.Append(digits, i, 3);
                }
            }
            else
            {
                sb.Append(digits);
            }
            if (decimals > 0)
            {
                sb.Append('.');
                string frac = UInt64ToString(fraction);
                for (int i = frac.Length; i < decimals; i++)
                    sb.Append('0');
                sb.Append(frac);
            }
            return sb.ToString();
        }

        // Formats with up to `maxDecimals` fractional digits, trimming trailing zeros (like "G" for typical game values).
        internal static string SingleToString(float value, int maxDecimals)
        {
            string special = SpecialFloat(value);
            if (special != null)
                return special;
            bool negative = value < 0;
            float abs = negative ? -value : value;
            if (abs >= 1e9f || (abs != 0 && abs < 1e-4f))
                return FormatScientific(value);
            ulong scale = Pow10(maxDecimals);
            SplitFloat(abs, maxDecimals, scale, out ulong integerPart, out ulong fraction);
            int decimals = maxDecimals;
            while (decimals > 0 && fraction % 10 == 0)
            {
                fraction /= 10;
                decimals--;
            }
            return FormatFixed(negative && (integerPart != 0 || fraction != 0), integerPart, fraction, decimals, false);
        }

        internal static string FormatSingle(float value, string format)
        {
            if (string.IsNullOrEmpty(format) || format[0] == 'G' || format[0] == 'g' || format[0] == 'R' || format[0] == 'r')
                return SingleToString(value, 6);
            string special = SpecialFloat(value);
            if (special != null)
                return special;
            char kind = format[0];
            int precision = ParsePrecision(format, 2);
            if (kind == 'F' || kind == 'f' || kind == 'N' || kind == 'n')
            {
                bool negative = value < 0;
                float abs = negative ? -value : value;
                SplitFloat(abs, precision, Pow10(precision), out ulong integerPart, out ulong fraction);
                return FormatFixed(negative && (integerPart != 0 || fraction != 0), integerPart, fraction, precision, kind == 'N' || kind == 'n');
            }
            if (kind == 'P' || kind == 'p')
                return string.Concat(FormatSingle(value * 100, "F" + Int32ToString(precision)), " %");
            if (kind == 'E' || kind == 'e')
                return FormatScientific(value);
            return SingleToString(value, 6);
        }

        private static string SpecialFloat(float value)
        {
            if (value != value)
                return "NaN";
            if (value == float.PositiveInfinity)
                return "∞";
            if (value == float.NegativeInfinity)
                return "-∞";
            return null;
        }

        private static ulong Pow10(int n)
        {
            ulong r = 1;
            for (int i = 0; i < n; i++)
                r *= 10;
            return r;
        }

        // Splits a non-negative float into integer and rounded fractional digits using only float math.
        private static void SplitFloat(float abs, int decimals, ulong scale, out ulong integerPart, out ulong fraction)
        {
            if (abs >= 18446744073709551615f)
            {
                integerPart = ulong.MaxValue;
                fraction = 0;
                return;
            }
            integerPart = abs >= 4294967296f ? FloatToUInt64(abs) : (uint)abs;
            float frac = abs - (abs >= 4294967296f ? 0 : (float)(uint)integerPart);
            // Round the fraction to `decimals` digits.
            float scaled = frac * (float)scale + 0.5f;
            fraction = (ulong)(uint)scaled;
            if (fraction >= scale)
            {
                fraction -= scale;
                integerPart++;
            }
        }

        private static ulong FloatToUInt64(float abs)
        {
            // Avoid float->long conversion (which goes through double on ARM): split into two 32-bit halves.
            float high = abs / 4294967296f;
            uint hi = (uint)high;
            float rest = abs - (float)hi * 4294967296f;
            uint lo = rest > 0 ? (uint)rest : 0;
            return ((ulong)hi << 32) | lo;
        }

        private static string FormatScientific(float value)
        {
            if (value == 0)
                return "0";
            bool negative = value < 0;
            float abs = negative ? -value : value;
            int exponent = 0;
            while (abs >= 10f) { abs /= 10f; exponent++; }
            while (abs < 1f) { abs *= 10f; exponent--; }
            string mantissa = SingleToString(negative ? -abs : abs, 5);
            return string.Concat(mantissa, exponent < 0 ? "E-" : "E+", Int32ToString(exponent < 0 ? -exponent : exponent).PadLeft(2, '0'));
        }

        internal static string DoubleToString(double value)
        {
            byte* buffer = stackalloc byte[64];
            int length = RuntimeImports.sd_format_double(value, 10, buffer, 64);
            return Text.Encoding.UTF8.GetString(buffer, length);
        }

        internal static int ParseInt32(string s)
        {
            if (!TryParseInt32(s, out int result))
                ThrowHelpers.ThrowFormatException();
            return result;
        }

        internal static bool TryParseInt32(string s, out int result)
        {
            result = 0;
            if (s is null)
                return false;
            int i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            bool negative = false;
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
            {
                negative = s[i] == '-';
                i++;
            }
            int start = i;
            long value = 0;
            while (i < s.Length && char.IsDigit(s[i]))
            {
                value = value * 10 + (s[i] - '0');
                if (value > (long)int.MaxValue + 1)
                    return false;
                i++;
            }
            if (i == start)
                return false;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i != s.Length)
                return false;
            if (negative)
                value = -value;
            if (value > int.MaxValue || value < int.MinValue)
                return false;
            result = (int)value;
            return true;
        }

        internal static float ParseSingle(string s)
        {
            if (s is null)
                ThrowHelpers.ThrowArgumentNullException();
            int len = Text.Encoding.UTF8.GetByteCount(s);
            byte* buffer = stackalloc byte[len + 1];
            Text.Encoding.UTF8.GetBytes(s, new Span<byte>(buffer, len));
            buffer[len] = 0;
            return RuntimeImports.sd_parse_float(buffer, len);
        }

        internal static string FormatObject(object value, string format)
        {
            if (value is null)
                return string.Empty;
            if (format != null)
            {
                if (value is int i) return FormatInt32(i, format);
                if (value is uint u) return FormatUInt32(u, format);
                if (value is float f) return FormatSingle(f, format);
                if (value is short sh) return FormatInt32(sh, format);
                if (value is ushort ush) return FormatUInt32(ush, format);
                if (value is byte b) return FormatUInt32(b, format);
                if (value is sbyte sb) return FormatInt32(sb, format);
            }
            return value.ToString();
        }
    }
}
