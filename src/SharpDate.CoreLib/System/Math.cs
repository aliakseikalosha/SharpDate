using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    /// <summary>
    /// Single-precision math. Prefer MathF in game code: the Playdate FPU is single precision only.
    /// </summary>
    public static class MathF
    {
        public const float PI = 3.14159265f;
        public const float E = 2.71828183f;
        public const float Tau = 6.28318531f;

        [Intrinsic] public static float Abs(float x) => x < 0 ? -x : x;
        [Intrinsic] public static float Sqrt(float x) => CLib.sqrtf(x);
        [Intrinsic] public static float Sin(float x) => CLib.sinf(x);
        [Intrinsic] public static float Cos(float x) => CLib.cosf(x);
        [Intrinsic] public static float Tan(float x) => CLib.tanf(x);
        [Intrinsic] public static float Asin(float x) => CLib.asinf(x);
        [Intrinsic] public static float Acos(float x) => CLib.acosf(x);
        [Intrinsic] public static float Atan(float x) => CLib.atanf(x);
        [Intrinsic] public static float Atan2(float y, float x) => CLib.atan2f(y, x);
        [Intrinsic] public static float Exp(float x) => CLib.expf(x);
        [Intrinsic] public static float Log(float x) => CLib.logf(x);
        [Intrinsic] public static float Log10(float x) => CLib.log10f(x);
        [Intrinsic] public static float Log2(float x) => CLib.log2f(x);
        [Intrinsic] public static float Pow(float x, float y) => CLib.powf(x, y);
        [Intrinsic] public static float Floor(float x) => CLib.floorf(x);
        [Intrinsic] public static float Ceiling(float x) => CLib.ceilf(x);
        [Intrinsic] public static float Round(float x) => CLib.roundf(x);
        [Intrinsic] public static float Truncate(float x) => CLib.truncf(x);
        [Intrinsic] public static float Max(float x, float y) => x > y ? x : y;
        [Intrinsic] public static float Min(float x, float y) => x < y ? x : y;
        public static float Sign(float x) => x > 0 ? 1 : (x < 0 ? -1 : 0);
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
        [Intrinsic] public static float FusedMultiplyAdd(float x, float y, float z) => x * y + z;
        public static float IEEERemainder(float x, float y) => x - y * Round(x / y);
        public static float Round(float x, int digits)
        {
            float scale = 1;
            for (int i = 0; i < digits; i++)
                scale *= 10;
            return Round(x * scale) / scale;
        }
    }

    public static class Math
    {
        public const double PI = 3.14159265358979323846;
        public const double E = 2.7182818284590452354;
        public const double Tau = 6.283185307179586476925;

        public static int Abs(int value)
        {
            if (value < 0)
            {
                if (value == int.MinValue)
                    ThrowHelpers.ThrowOverflowException();
                return -value;
            }
            return value;
        }

        public static long Abs(long value) => value < 0 ? -value : value;
        public static short Abs(short value) => (short)(value < 0 ? -value : value);
        public static sbyte Abs(sbyte value) => (sbyte)(value < 0 ? -value : value);
        [Intrinsic] public static float Abs(float value) => MathF.Abs(value);
        [Intrinsic] public static double Abs(double value) => value < 0 ? -value : value;

        public static int Max(int a, int b) => a > b ? a : b;
        public static uint Max(uint a, uint b) => a > b ? a : b;
        public static long Max(long a, long b) => a > b ? a : b;
        public static ulong Max(ulong a, ulong b) => a > b ? a : b;
        public static short Max(short a, short b) => a > b ? a : b;
        public static byte Max(byte a, byte b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static double Max(double a, double b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static uint Min(uint a, uint b) => a < b ? a : b;
        public static long Min(long a, long b) => a < b ? a : b;
        public static ulong Min(ulong a, ulong b) => a < b ? a : b;
        public static short Min(short a, short b) => a < b ? a : b;
        public static byte Min(byte a, byte b) => a < b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static double Min(double a, double b) => a < b ? a : b;

        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
        public static uint Clamp(uint value, uint min, uint max) => value < min ? min : (value > max ? max : value);
        public static long Clamp(long value, long min, long max) => value < min ? min : (value > max ? max : value);
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
        public static double Clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);

        public static int Sign(int value) => value > 0 ? 1 : (value < 0 ? -1 : 0);
        public static int Sign(long value) => value > 0 ? 1 : (value < 0 ? -1 : 0);
        public static int Sign(float value) => value > 0 ? 1 : (value < 0 ? -1 : 0);

        public static int DivRem(int a, int b, out int result)
        {
            int div = a / b;
            result = a - div * b;
            return div;
        }

        public static (int Quotient, int Remainder) DivRem(int left, int right)
        {
            int quotient = left / right;
            return (quotient, left - quotient * right);
        }

        public static long BigMul(int a, int b) => (long)a * b;

        // Double overloads are routed through the C library (soft-float on device).
        [Intrinsic] public static double Sqrt(double d) => CLib.sqrt(d);
        [Intrinsic] public static double Sin(double a) => CLib.sin(a);
        [Intrinsic] public static double Cos(double d) => CLib.cos(d);
        [Intrinsic] public static double Tan(double a) => CLib.tan(a);
        [Intrinsic] public static double Atan(double d) => CLib.atan(d);
        [Intrinsic] public static double Atan2(double y, double x) => CLib.atan2(y, x);
        [Intrinsic] public static double Pow(double x, double y) => CLib.pow(x, y);
        [Intrinsic] public static double Exp(double d) => CLib.exp(d);
        [Intrinsic] public static double Log(double d) => CLib.log(d);
        [Intrinsic] public static double Floor(double d) => CLib.floor(d);
        [Intrinsic] public static double Ceiling(double a) => CLib.ceil(a);
        [Intrinsic] public static double Round(double a) => CLib.round(a);
        [Intrinsic] public static double Truncate(double d) => CLib.trunc(d);

        // ---- Helpers called by compiler-generated code (names are a contract with ILC). ----
        // The ARM32 JIT emits calls to these for every non-constant division (it does not assume a
        // hardware divider), so they must not use the `/` and `%` operators themselves.

        private static int DivInt32(int dividend, int divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            if (divisor == -1 && dividend == int.MinValue) ThrowHelpers.ThrowOverflowException();
            return RuntimeImports.sd_idiv(dividend, divisor);
        }

        private static uint DivUInt32(uint dividend, uint divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            return RuntimeImports.sd_uidiv(dividend, divisor);
        }

        private static int ModInt32(int dividend, int divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            if (divisor == -1) return 0;
            return RuntimeImports.sd_imod(dividend, divisor);
        }

        private static uint ModUInt32(uint dividend, uint divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            return RuntimeImports.sd_uimod(dividend, divisor);
        }

        private static long DivInt64(long dividend, long divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            if (divisor == -1 && dividend == long.MinValue) ThrowHelpers.ThrowOverflowException();
            return RuntimeImports.sd_ldiv(dividend, divisor);
        }

        private static ulong DivUInt64(ulong dividend, ulong divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            return RuntimeImports.sd_uldiv(dividend, divisor);
        }

        private static long ModInt64(long dividend, long divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            if (divisor == -1) return 0;
            return RuntimeImports.sd_lmod(dividend, divisor);
        }

        private static ulong ModUInt64(ulong dividend, ulong divisor)
        {
            if (divisor == 0) ThrowHelpers.ThrowDivideByZeroException();
            return RuntimeImports.sd_ulmod(dividend, divisor);
        }

        private static long MultiplyChecked(long left, long right)
        {
            long result = left * right;
            if (left != 0 && (RuntimeImports.sd_ldiv(result, left) != right || (left == -1 && right == long.MinValue)))
                ThrowHelpers.ThrowOverflowException();
            return result;
        }

        private static ulong MultiplyChecked(ulong left, ulong right)
        {
            ulong result = left * right;
            if (left != 0 && RuntimeImports.sd_uldiv(result, left) != right)
                ThrowHelpers.ThrowOverflowException();
            return result;
        }

        private static int ConvertToInt32Checked(double value)
        {
            if (!(value > -2147483649.0 && value < 2147483648.0))
                ThrowHelpers.ThrowOverflowException();
            return (int)value;
        }

        private static uint ConvertToUInt32Checked(double value)
        {
            if (!(value > -1.0 && value < 4294967296.0))
                ThrowHelpers.ThrowOverflowException();
            return (uint)value;
        }

        private static long ConvertToInt64Checked(double value)
        {
            if (!(value >= -9223372036854775808.0 && value < 9223372036854775808.0))
                ThrowHelpers.ThrowOverflowException();
            return (long)value;
        }

        private static ulong ConvertToUInt64Checked(double value)
        {
            if (!(value > -1.0 && value < 18446744073709551616.0))
                ThrowHelpers.ThrowOverflowException();
            return (ulong)value;
        }
    }

    internal static class CLib
    {
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "sqrtf")] internal static extern float sqrtf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "sinf")] internal static extern float sinf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "cosf")] internal static extern float cosf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "tanf")] internal static extern float tanf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "asinf")] internal static extern float asinf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "acosf")] internal static extern float acosf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "atanf")] internal static extern float atanf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "atan2f")] internal static extern float atan2f(float y, float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "expf")] internal static extern float expf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "logf")] internal static extern float logf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "log10f")] internal static extern float log10f(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "log2f")] internal static extern float log2f(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "powf")] internal static extern float powf(float x, float y);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "floorf")] internal static extern float floorf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "ceilf")] internal static extern float ceilf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "roundf")] internal static extern float roundf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "truncf")] internal static extern float truncf(float x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "fmodf")] internal static extern float fmodf(float x, float y);

        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "sqrt")] internal static extern double sqrt(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "sin")] internal static extern double sin(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "cos")] internal static extern double cos(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "tan")] internal static extern double tan(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "atan")] internal static extern double atan(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "atan2")] internal static extern double atan2(double y, double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "pow")] internal static extern double pow(double x, double y);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "exp")] internal static extern double exp(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "log")] internal static extern double log(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "floor")] internal static extern double floor(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "ceil")] internal static extern double ceil(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "round")] internal static extern double round(double x);
        [MethodImpl(MethodImplOptions.InternalCall), RuntimeImport("*", "trunc")] internal static extern double trunc(double x);
    }
}
