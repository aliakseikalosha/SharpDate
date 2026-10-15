// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System
{
    public abstract unsafe partial class Array
    {
        // Layout is a contract with the compiler: [MethodTable*][int length (padded to pointer size)][elements...]
        private int _numComponents;

        public int Length => _numComponents;

        public int Rank => GetMethodTable()->IsSzArray ? 1 : GetMethodTable()->ArrayRank;

        public int GetLength(int dimension)
        {
            if (GetMethodTable()->IsSzArray)
            {
                if (dimension != 0)
                    ThrowHelpers.ThrowIndexOutOfRangeException();
                return Length;
            }
            if ((uint)dimension >= (uint)Rank)
                ThrowHelpers.ThrowIndexOutOfRangeException();
            return Unsafe.Add(ref Unsafe.As<byte, int>(ref GetRawArrayData()), dimension);
        }

        public int GetLowerBound(int dimension) => 0;
        public int GetUpperBound(int dimension) => GetLength(dimension) - 1;

        // Allocates a multidimensional array; lengths are written into the bounds area after the length field.
        internal static Array NewMultiDimArray(MethodTable* eeType, int* pLengths, int rank)
        {
            ulong totalLength = 1;
            for (int i = 0; i < rank; i++)
            {
                if (pLengths[i] < 0)
                    ThrowHelpers.ThrowOverflowException();
                totalLength *= (ulong)pLengths[i];
                if (totalLength > int.MaxValue)
                    ThrowHelpers.ThrowOverflowException();
            }

            Array ret = Unsafe.As<Array>(RuntimeImports.RhNewArray(eeType, (int)totalLength));
            ref int bounds = ref Unsafe.As<byte, int>(ref ret.GetRawArrayData());
            for (int i = 0; i < rank; i++)
                Unsafe.Add(ref bounds, i) = pLengths[i];
            return ret;
        }

        internal nuint ElementSize => GetMethodTable()->ComponentSize;

        internal ref byte GetRawArrayData() => ref Unsafe.As<RawArrayData>(this).Data;

        public static T[] Empty<T>() => EmptyArray<T>.Value;

        public static void Clear(Array array) => Clear(array, 0, array.Length);

        public static void Clear(Array array, int index, int length)
        {
            if (array is null)
                ThrowHelpers.ThrowArgumentNullException();
            if ((uint)index > (uint)array.Length || (uint)length > (uint)(array.Length - index))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            nuint elementSize = array.ElementSize;
            SpanHelpers.ClearWithoutReferences(ref Unsafe.AddByteOffset(ref array.GetRawArrayData(), (nint)((nuint)index * elementSize)), (nuint)length * elementSize);
        }

        public static void Copy(Array sourceArray, Array destinationArray, int length)
            => Copy(sourceArray, 0, destinationArray, 0, length);

        public static void Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)
        {
            if (sourceArray is null || destinationArray is null)
                ThrowHelpers.ThrowArgumentNullException();
            if (sourceArray.GetMethodTable() != destinationArray.GetMethodTable())
                ThrowHelpers.ThrowArrayTypeMismatchException();
            if ((uint)sourceIndex > (uint)sourceArray.Length || (uint)length > (uint)(sourceArray.Length - sourceIndex)
                || (uint)destinationIndex > (uint)destinationArray.Length || (uint)length > (uint)(destinationArray.Length - destinationIndex))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            nuint elementSize = sourceArray.ElementSize;
            Buffer.Memmove(
                ref Unsafe.AddByteOffset(ref destinationArray.GetRawArrayData(), (nint)((nuint)destinationIndex * elementSize)),
                ref Unsafe.AddByteOffset(ref sourceArray.GetRawArrayData(), (nint)((nuint)sourceIndex * elementSize)),
                (nuint)length * elementSize);
        }

        public static void Resize<T>(ref T[] array, int newSize)
        {
            T[] larray = array;
            if (larray is null)
            {
                array = new T[newSize];
                return;
            }
            if (larray.Length != newSize)
            {
                T[] newArray = new T[newSize];
                Copy(larray, 0, newArray, 0, larray.Length > newSize ? newSize : larray.Length);
                array = newArray;
            }
        }

        public static int IndexOf<T>(T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++)
                if (Collections.Generic.EqualityComparer<T>.Default.Equals(array[i], value))
                    return i;
            return -1;
        }

        public static void Fill<T>(T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++)
                array[i] = value;
        }

        public static void Reverse<T>(T[] array)
        {
            int i = 0, j = array.Length - 1;
            while (i < j)
            {
                T tmp = array[i];
                array[i] = array[j];
                array[j] = tmp;
                i++;
                j--;
            }
        }

        public static void Sort<T>(T[] array, Comparison<T> comparison) => Sort(array, 0, array.Length, comparison);

        public static void Sort<T>(T[] array, int index, int length, Comparison<T> comparison)
        {
            // Insertion sort for small ranges, otherwise heap sort: no recursion, no allocation.
            if (length < 2)
                return;
            if (length <= 16)
            {
                for (int i = index + 1; i < index + length; i++)
                {
                    T key = array[i];
                    int j = i - 1;
                    while (j >= index && comparison(array[j], key) > 0)
                    {
                        array[j + 1] = array[j];
                        j--;
                    }
                    array[j + 1] = key;
                }
                return;
            }
            for (int i = length / 2 - 1; i >= 0; i--)
                SiftDown(array, index, i, length, comparison);
            for (int end = length - 1; end > 0; end--)
            {
                T tmp = array[index];
                array[index] = array[index + end];
                array[index + end] = tmp;
                SiftDown(array, index, 0, end, comparison);
            }
        }

        private static void SiftDown<T>(T[] a, int lo, int i, int n, Comparison<T> cmp)
        {
            T d = a[lo + i];
            while (i < n / 2)
            {
                int child = 2 * i + 1;
                if (child + 1 < n && cmp(a[lo + child], a[lo + child + 1]) < 0)
                    child++;
                if (cmp(d, a[lo + child]) >= 0)
                    break;
                a[lo + i] = a[lo + child];
                i = child;
            }
            a[lo + i] = d;
        }

        public static void Sort<T>(T[] array) where T : IComparable<T>
            => Sort(array, 0, array.Length, static (a, b) => a.CompareTo(b));
    }

    // Generic base used by the compiler for T[] when it needs to implement generic interfaces on arrays.
    internal class Array<T> : Array
    {
    }

    internal static class EmptyArray<T>
    {
        internal static readonly T[] Value = new T[0];
    }
}

namespace System.Runtime.CompilerServices
{
    // Pointer-sized length slot (int length + padding on 64-bit) followed by the first element.
    // Using nint keeps a single CoreLib binary valid for both 32-bit (device) and 64-bit (Simulator) targets.
    [StructLayout(LayoutKind.Sequential)]
    internal class RawArrayData
    {
        public nint LengthAndPadding;
        public byte Data;

        public int Length => (int)LengthAndPadding;
    }
}
