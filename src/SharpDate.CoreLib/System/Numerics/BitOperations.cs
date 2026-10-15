namespace System.Numerics
{
    public static class BitOperations
    {
        public static int PopCount(uint value)
        {
            value -= (value >> 1) & 0x55555555u;
            value = (value & 0x33333333u) + ((value >> 2) & 0x33333333u);
            value = (value + (value >> 4)) & 0x0F0F0F0Fu;
            return (int)((value * 0x01010101u) >> 24);
        }

        public static int PopCount(ulong value) => PopCount((uint)value) + PopCount((uint)(value >> 32));

        public static int LeadingZeroCount(uint value)
        {
            if (value == 0)
                return 32;
            int n = 0;
            if ((value & 0xFFFF0000u) == 0) { n += 16; value <<= 16; }
            if ((value & 0xFF000000u) == 0) { n += 8; value <<= 8; }
            if ((value & 0xF0000000u) == 0) { n += 4; value <<= 4; }
            if ((value & 0xC0000000u) == 0) { n += 2; value <<= 2; }
            if ((value & 0x80000000u) == 0) { n += 1; }
            return n;
        }

        public static int TrailingZeroCount(uint value)
        {
            if (value == 0)
                return 32;
            int n = 0;
            while ((value & 1) == 0)
            {
                value >>= 1;
                n++;
            }
            return n;
        }

        public static int Log2(uint value) => 31 - LeadingZeroCount(value | 1);
        public static bool IsPow2(uint value) => value != 0 && (value & (value - 1)) == 0;
        public static bool IsPow2(int value) => value > 0 && (value & (value - 1)) == 0;
        public static uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));
        public static uint RotateRight(uint value, int offset) => (value >> offset) | (value << (32 - offset));

        public static uint RoundUpToPowerOf2(uint value)
        {
            --value;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }
}
