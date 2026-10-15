// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;

namespace Playdate
{
    /// <summary>
    /// UTF-8 helpers for passing strings to the Playdate C API.
    /// </summary>
    /// <remarks>
    /// Most wrapper methods accept <see cref="string"/> and convert it into one of a few reusable
    /// native scratch buffers (no allocation per call). Pointers returned by <see cref="Arg"/> are
    /// only valid until the next call that uses the same slot.
    /// </remarks>
    public static unsafe class Utf8
    {
        private const int Slots = 4;
        private static byte** s_buffers;
        private static int* s_capacity;

        /// <summary>Converts <paramref name="value"/> to NUL-terminated UTF-8 in scratch slot <paramref name="slot"/>.</summary>
        /// <param name="value">The string (null returns null).</param>
        /// <param name="slot">
        /// Scratch buffer 0-3; use different slots for strings passed in the same call.
        /// </param>
        public static byte* Arg(string value, int slot = 0)
        {
            if (value is null)
                return null;
            if (s_buffers == null)
            {
                s_buffers = (byte**)NativeMemory.AllocZeroed((nuint)(sizeof(byte*) * Slots));
                s_capacity = (int*)NativeMemory.AllocZeroed((nuint)(sizeof(int) * Slots));
            }
            int needed = System.Text.Encoding.UTF8.GetByteCount(value) + 1;
            if (s_capacity[slot] < needed)
            {
                int capacity = needed < 64 ? 64 : needed * 2;
                s_buffers[slot] = (byte*)NativeMemory.Realloc(s_buffers[slot], (nuint)capacity);
                s_capacity[slot] = capacity;
            }
            byte* buffer = s_buffers[slot];
            int n = System.Text.Encoding.UTF8.GetBytes(value, new Span<byte>(buffer, needed - 1));
            buffer[n] = 0;
            return buffer;
        }

        /// <summary>Allocates a NUL-terminated UTF-8 copy with the Playdate allocator. Free with <see cref="Free"/>.</summary>
        /// <param name="value">The string (null returns null).</param>
        public static byte* Alloc(string value)
        {
            if (value is null)
                return null;
            int len = System.Text.Encoding.UTF8.GetByteCount(value);
            byte* p = (byte*)NativeMemory.Alloc((nuint)len + 1);
            System.Text.Encoding.UTF8.GetBytes(value, new Span<byte>(p, len));
            p[len] = 0;
            return p;
        }

        /// <summary>Frees memory from <see cref="Alloc"/>.</summary>
        /// <param name="p">A pointer from <see cref="Alloc"/>, or null.</param>
        public static void Free(byte* p) => NativeMemory.Free(p);

        /// <summary>Converts a NUL-terminated UTF-8 C string to a managed string (null for a null pointer).</summary>
        /// <param name="p">A NUL-terminated UTF-8 string, or null.</param>
        public static string ToString(byte* p) => p == null ? null : new string((sbyte*)p);

        /// <summary>Converts <paramref name="length"/> UTF-8 bytes to a managed string.</summary>
        /// <param name="p">UTF-8 bytes, or null.</param>
        /// <param name="length">Number of bytes, 0 or more.</param>
        public static string ToString(byte* p, int length) => p == null ? null : System.Text.Encoding.UTF8.GetString(p, length);

        /// <summary>Number of code points in a UTF-16 string (what the Playdate text APIs expect as a length).</summary>
        /// <param name="text">UTF-16 text.</param>
        public static int CodePointCount(ReadOnlySpan<char> text)
        {
            int count = text.Length;
            for (int i = 0; i < text.Length; i++)
                if (char.IsHighSurrogate(text[i]))
                    count--;
            return count;
        }

        /// <summary>Number of code points in a UTF-8 byte sequence.</summary>
        /// <param name="utf8">UTF-8 text.</param>
        public static int CodePointCount(ReadOnlySpan<byte> utf8)
        {
            int count = 0;
            for (int i = 0; i < utf8.Length; i++)
                if ((utf8[i] & 0xC0) != 0x80)
                    count++;
            return count;
        }
    }
}
