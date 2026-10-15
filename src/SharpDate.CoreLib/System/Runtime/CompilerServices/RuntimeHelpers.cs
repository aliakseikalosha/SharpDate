using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System.Runtime.CompilerServices
{
    public static unsafe class RuntimeHelpers
    {
        public static int OffsetToStringData => sizeof(IntPtr) + sizeof(int);

        [Intrinsic]
        public static bool IsReferenceOrContainsReferences<T>() => throw null;

        [Intrinsic]
        internal static bool IsBitwiseEquatable<T>() => throw null;

        [Intrinsic]
        public static void InitializeArray(Array array, RuntimeFieldHandle fldHandle) => throw null;

        [Intrinsic]
        public static ReadOnlySpan<T> CreateSpan<T>(RuntimeFieldHandle fldHandle) => throw null;

        [Intrinsic]
        public static bool IsKnownConstant(int t) => false;

        [Intrinsic]
        public static bool IsKnownConstant(char t) => false;

        [Intrinsic]
        public static bool IsKnownConstant(string t) => false;

        public static int GetHashCode(object o) => o is null ? 0 : (int)((nint)Unsafe.As<object, nint>(ref o) >> 3);

        public static new bool Equals(object o1, object o2) => ObjectReferenceEquals(o1, o2);

        internal static bool ObjectReferenceEquals(object o1, object o2)
            => Unsafe.As<object, nint>(ref o1) == Unsafe.As<object, nint>(ref o2);

        // Number of bytes after the MethodTable pointer (including array/string payload).
        internal static nuint GetObjectDataSize(object obj)
        {
            MethodTable* mt = obj.GetMethodTable();
            nuint size = mt->BaseSize - (nuint)(sizeof(ObjHeader) + sizeof(MethodTable*));
            if (mt->HasComponentSize)
                size += (nuint)Unsafe.As<RawArrayData>(obj).Length * mt->ComponentSize;
            return size;
        }

        public static void RunClassConstructor(RuntimeTypeHandle type) { }

        public static void EnsureSufficientExecutionStack() { }
    }
}
