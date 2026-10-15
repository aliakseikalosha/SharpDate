using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    public static unsafe class Buffer
    {
        // The SharpDate GC is non-generational, so copying references needs no write barrier.
        internal static void Memmove<T>(ref T destination, ref T source, nuint elementCount)
            => SpanHelpers.Memmove(ref Unsafe.As<T, byte>(ref destination), ref Unsafe.As<T, byte>(ref source), elementCount * (nuint)Unsafe.SizeOf<T>());

        internal static void Memmove(ref byte destination, ref byte source, nuint byteCount)
            => SpanHelpers.Memmove(ref destination, ref source, byteCount);

        // Target of the compiler's bulk write barrier helper.
        private static void BulkMoveWithWriteBarrier(ref byte destination, ref byte source, nuint byteCount)
            => SpanHelpers.Memmove(ref destination, ref source, byteCount);

        public static void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            SpanHelpers.Memmove(ref *(byte*)destination, ref *(byte*)source, (nuint)sourceBytesToCopy);
        }

        public static void BlockCopy(Array src, int srcOffset, Array dst, int dstOffset, int count)
        {
            if ((uint)srcOffset + (uint)count > (uint)src.Length * src.ElementSize
                || (uint)dstOffset + (uint)count > (uint)dst.Length * dst.ElementSize)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            SpanHelpers.Memmove(
                ref Unsafe.AddByteOffset(ref dst.GetRawArrayData(), (nint)dstOffset),
                ref Unsafe.AddByteOffset(ref src.GetRawArrayData(), (nint)srcOffset),
                (nuint)count);
        }
    }

    internal static unsafe class SpanHelpers
    {
        // Targets of the compiler's MemCpy / MemSet / MemZero helpers; forwarded to the C library.
        [Intrinsic]
        internal static void Memmove(ref byte dest, ref byte src, nuint len)
        {
            if (len != 0)
                memmove(Unsafe.AsPointer(ref dest), Unsafe.AsPointer(ref src), len);
        }

        [Intrinsic]
        internal static void Fill(ref byte dest, byte value, nuint len)
        {
            if (len != 0)
                memset(Unsafe.AsPointer(ref dest), value, len);
        }

        [Intrinsic]
        internal static void ClearWithoutReferences(ref byte dest, nuint len)
        {
            if (len != 0)
                memset(Unsafe.AsPointer(ref dest), 0, len);
        }

        internal static bool SequenceEqual(ref byte first, ref byte second, nuint length)
        {
            if (length == 0 || Unsafe.AreSame(ref first, ref second))
                return true;
            return memcmp(Unsafe.AsPointer(ref first), Unsafe.AsPointer(ref second), length) == 0;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "memmove")]
        private static extern void* memmove(void* dest, void* src, nuint len);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "memset")]
        private static extern void* memset(void* dest, int value, nuint len);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "memcmp")]
        private static extern int memcmp(void* a, void* b, nuint len);
    }
}
