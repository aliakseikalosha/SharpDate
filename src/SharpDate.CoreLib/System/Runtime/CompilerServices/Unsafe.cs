// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// Bodies of [Intrinsic] methods here are replaced by the compiler (ILC provides IL for them);
// the C# bodies only exist to make the library compile.

namespace System.Runtime.CompilerServices
{
    public static unsafe class Unsafe
    {
        [Intrinsic]
        public static void* AsPointer<T>(ref readonly T value) => throw null;

        [Intrinsic]
        public static int SizeOf<T>() => throw null;

        [Intrinsic]
        public static T As<T>(object o) where T : class => throw null;

        [Intrinsic]
        public static ref TTo As<TFrom, TTo>(ref TFrom source) => throw null;

        [Intrinsic]
        public static ref T AsRef<T>(void* source) => ref *(T*)source;

        [Intrinsic]
        public static ref T AsRef<T>(scoped ref readonly T source) => throw null;

        [Intrinsic]
        public static ref T Add<T>(ref T source, int elementOffset) => ref AddByteOffset(ref source, (nint)elementOffset * (nint)SizeOf<T>());

        [Intrinsic]
        public static ref T Add<T>(ref T source, nint elementOffset) => ref AddByteOffset(ref source, elementOffset * (nint)SizeOf<T>());

        [Intrinsic]
        public static ref T Add<T>(ref T source, nuint elementOffset) => ref AddByteOffset(ref source, (nint)elementOffset * (nint)SizeOf<T>());

        [Intrinsic]
        public static void* Add<T>(void* source, int elementOffset) => (byte*)source + (elementOffset * (nint)SizeOf<T>());

        [Intrinsic]
        public static ref T Subtract<T>(ref T source, int elementOffset) => ref SubtractByteOffset(ref source, (nint)elementOffset * (nint)SizeOf<T>());

        [Intrinsic]
        public static ref T AddByteOffset<T>(ref T source, nint byteOffset) => throw null;

        [Intrinsic]
        public static ref T AddByteOffset<T>(ref T source, nuint byteOffset) => ref AddByteOffset(ref source, (nint)byteOffset);

        [Intrinsic]
        public static ref T SubtractByteOffset<T>(ref T source, nint byteOffset) => ref AddByteOffset(ref source, -byteOffset);

        [Intrinsic]
        public static nint ByteOffset<T>(ref readonly T origin, ref readonly T target) => throw null;

        [Intrinsic]
        public static bool AreSame<T>(ref readonly T left, ref readonly T right) => throw null;

        [Intrinsic]
        public static bool IsAddressLessThan<T>(ref readonly T left, ref readonly T right) => throw null;

        [Intrinsic]
        public static bool IsAddressGreaterThan<T>(ref readonly T left, ref readonly T right) => throw null;

        [Intrinsic]
        public static T Read<T>(void* source) => *(T*)source;

        [Intrinsic]
        public static void Write<T>(void* destination, T value) => *(T*)destination = value;

        [Intrinsic]
        public static T ReadUnaligned<T>(void* source) => throw null;

        [Intrinsic]
        public static T ReadUnaligned<T>(ref readonly byte source) => throw null;

        [Intrinsic]
        public static void WriteUnaligned<T>(void* destination, T value) => throw null;

        [Intrinsic]
        public static void WriteUnaligned<T>(ref byte destination, T value) => throw null;

        [Intrinsic]
        public static void CopyBlock(void* destination, void* source, uint byteCount) => throw null;

        [Intrinsic]
        public static void CopyBlock(ref byte destination, ref readonly byte source, uint byteCount) => throw null;

        [Intrinsic]
        public static void CopyBlockUnaligned(void* destination, void* source, uint byteCount) => throw null;

        [Intrinsic]
        public static void InitBlock(void* startAddress, byte value, uint byteCount) => throw null;

        [Intrinsic]
        public static void InitBlockUnaligned(void* startAddress, byte value, uint byteCount) => throw null;

        [Intrinsic]
        public static void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount) => throw null;

        [Intrinsic]
        public static ref T NullRef<T>() => ref AsRef<T>(null);

        [Intrinsic]
        public static bool IsNullRef<T>(ref readonly T source) => AsPointer(in source) == null;

        [Intrinsic]
        public static void SkipInit<T>(out T value) => throw null;

        [Intrinsic]
        public static ref T Unbox<T>(object box) where T : struct => throw null;

        [Intrinsic]
        public static TTo BitCast<TFrom, TTo>(TFrom source) where TFrom : struct where TTo : struct => ReadUnaligned<TTo>(ref As<TFrom, byte>(ref source));
    }
}
