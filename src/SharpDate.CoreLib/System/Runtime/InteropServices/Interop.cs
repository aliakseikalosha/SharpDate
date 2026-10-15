using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    public enum LayoutKind
    {
        Sequential = 0,
        Explicit = 2,
        Auto = 3,
    }

    public enum CharSet
    {
        None = 1,
        Ansi = 2,
        Unicode = 3,
        Auto = 4,
    }

    public enum CallingConvention
    {
        Winapi = 1,
        Cdecl = 2,
        StdCall = 3,
        ThisCall = 4,
        FastCall = 5,
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class StructLayoutAttribute : Attribute
    {
        public StructLayoutAttribute(LayoutKind layoutKind) { Value = layoutKind; }
        public StructLayoutAttribute(short layoutKind) { Value = (LayoutKind)layoutKind; }
        public LayoutKind Value { get; }
        public int Pack;
        public int Size;
        public CharSet CharSet;
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class FieldOffsetAttribute : Attribute
    {
        public FieldOffsetAttribute(int offset) { Value = offset; }
        public int Value { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class DllImportAttribute : Attribute
    {
        public DllImportAttribute(string dllName) { Value = dllName; }
        public string Value { get; }
        public string EntryPoint;
        public CharSet CharSet;
        public bool SetLastError;
        public bool ExactSpelling;
        public CallingConvention CallingConvention;
        public bool BestFitMapping;
        public bool PreserveSig;
        public bool ThrowOnUnmappableChar;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class UnmanagedCallersOnlyAttribute : Attribute
    {
        public UnmanagedCallersOnlyAttribute() { }
        public Type[] CallConvs;
        public string EntryPoint;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class UnmanagedCallConvAttribute : Attribute
    {
        public UnmanagedCallConvAttribute() { }
        public Type[] CallConvs;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class SuppressGCTransitionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class InAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class OutAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class OptionalAttribute : Attribute { }

    public enum UnmanagedType
    {
        Bool = 0x2,
        I1 = 0x3,
        U1 = 0x4,
        I2 = 0x5,
        U2 = 0x6,
        I4 = 0x7,
        U4 = 0x8,
        I8 = 0x9,
        U8 = 0xa,
        R4 = 0xb,
        R8 = 0xc,
        LPStr = 0x14,
        LPWStr = 0x15,
        ByValArray = 0x1e,
        SysInt = 0x1f,
        SysUInt = 0x20,
        FunctionPtr = 0x26,
        LPArray = 0x2a,
        LPStruct = 0x2b,
        LPUTF8Str = 0x30,
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, Inherited = false)]
    public sealed class MarshalAsAttribute : Attribute
    {
        public MarshalAsAttribute(UnmanagedType unmanagedType) { Value = unmanagedType; }
        public MarshalAsAttribute(short unmanagedType) { Value = (UnmanagedType)unmanagedType; }
        public UnmanagedType Value { get; }
        public int SizeConst;
        public UnmanagedType ArraySubType;
    }

    public enum GCHandleType
    {
        Weak = 0,
        WeakTrackResurrection = 1,
        Normal = 2,
        Pinned = 3
    }

    /// <summary>
    /// A handle that keeps a managed object alive while native code (for example a Playdate
    /// callback userdata pointer) refers to it. Handles are roots for the SharpDate GC.
    /// </summary>
    public struct GCHandle
    {
        private IntPtr _handle;

        private GCHandle(IntPtr handle) { _handle = handle; }

        public static GCHandle Alloc(object value) => new GCHandle(RuntimeImports.RhHandleAlloc(value, GCHandleType.Normal));

        public static GCHandle Alloc(object value, GCHandleType type) => new GCHandle(RuntimeImports.RhHandleAlloc(value, type));

        public void Free()
        {
            RuntimeImports.RhHandleFree(_handle);
            _handle = IntPtr.Zero;
        }

        public object Target
        {
            get => RuntimeImports.RhHandleGet(_handle);
            set => RuntimeImports.RhHandleSet(_handle, value);
        }

        public bool IsAllocated => _handle != IntPtr.Zero;

        public unsafe IntPtr AddrOfPinnedObject()
        {
            object target = Target;
            if (target is null)
                return IntPtr.Zero;
            if (target is Array array)
                return (IntPtr)Unsafe.AsPointer(ref array.GetRawArrayData());
            if (target is string s)
                return (IntPtr)Unsafe.AsPointer(ref s.GetRawStringData());
            return (IntPtr)Unsafe.AsPointer(ref target.GetRawData());
        }

        public static explicit operator IntPtr(GCHandle value) => value._handle;
        public static explicit operator GCHandle(IntPtr value) => new GCHandle(value);
        public static IntPtr ToIntPtr(GCHandle value) => value._handle;
        public static GCHandle FromIntPtr(IntPtr value) => new GCHandle(value);
    }

    public static unsafe class MemoryMarshal
    {
        [Intrinsic]
        public static ref T GetArrayDataReference<T>(T[] array)
            => ref Unsafe.As<byte, T>(ref Unsafe.As<RawArrayData>(array).Data);

        public static ref T GetReference<T>(Span<T> span) => ref span._reference;

        public static ref T GetReference<T>(ReadOnlySpan<T> span) => ref span._reference;

        public static Span<T> CreateSpan<T>(ref T reference, int length) => new Span<T>(ref reference, length);

        public static ReadOnlySpan<T> CreateReadOnlySpan<T>(ref readonly T reference, int length)
            => new ReadOnlySpan<T>(ref Unsafe.AsRef(in reference), length);

        public static ReadOnlySpan<byte> CreateReadOnlySpanFromNullTerminated(byte* value)
        {
            if (value == null)
                return default;
            int length = 0;
            while (value[length] != 0)
                length++;
            return new ReadOnlySpan<byte>(value, length);
        }

        public static Span<byte> AsBytes<T>(Span<T> span) where T : unmanaged
            => new Span<byte>(ref Unsafe.As<T, byte>(ref span._reference), span.Length * sizeof(T));

        public static ReadOnlySpan<byte> AsBytes<T>(ReadOnlySpan<T> span) where T : unmanaged
            => new ReadOnlySpan<byte>(ref Unsafe.As<T, byte>(ref span._reference), span.Length * sizeof(T));

        public static Span<TTo> Cast<TFrom, TTo>(Span<TFrom> span) where TFrom : unmanaged where TTo : unmanaged
            => new Span<TTo>(ref Unsafe.As<TFrom, TTo>(ref span._reference), span.Length * sizeof(TFrom) / sizeof(TTo));

        public static ReadOnlySpan<TTo> Cast<TFrom, TTo>(ReadOnlySpan<TFrom> span) where TFrom : unmanaged where TTo : unmanaged
            => new ReadOnlySpan<TTo>(ref Unsafe.As<TFrom, TTo>(ref span._reference), span.Length * sizeof(TFrom) / sizeof(TTo));

        public static T Read<T>(ReadOnlySpan<byte> source) where T : unmanaged
            => Unsafe.ReadUnaligned<T>(ref source._reference);

        public static void Write<T>(Span<byte> destination, in T value) where T : unmanaged
            => Unsafe.WriteUnaligned(ref destination._reference, value);
    }

    /// <summary>Native heap allocations (backed by the Playdate allocator on device).</summary>
    public static unsafe class NativeMemory
    {
        public static void* Alloc(nuint byteCount) => RuntimeImports.sd_alloc(byteCount);

        public static void* AllocZeroed(nuint byteCount)
        {
            void* p = RuntimeImports.sd_alloc(byteCount);
            Unsafe.InitBlockUnaligned(p, 0, (uint)byteCount);
            return p;
        }

        public static void* Realloc(void* ptr, nuint byteCount) => RuntimeImports.sd_realloc(ptr, byteCount);

        public static void Free(void* ptr)
        {
            if (ptr != null)
                RuntimeImports.sd_free(ptr);
        }

        public static void Clear(void* ptr, nuint byteCount) => Unsafe.InitBlockUnaligned(ptr, 0, (uint)byteCount);

        public static void Copy(void* source, void* destination, nuint byteCount)
            => Buffer.Memmove(ref *(byte*)destination, ref *(byte*)source, byteCount);
    }

    public static unsafe class Marshal
    {
        public static IntPtr AllocHGlobal(int cb) => (IntPtr)NativeMemory.Alloc((nuint)cb);
        public static IntPtr AllocHGlobal(nint cb) => (IntPtr)NativeMemory.Alloc((nuint)cb);
        public static void FreeHGlobal(IntPtr hglobal) => NativeMemory.Free((void*)hglobal);
        public static IntPtr AllocCoTaskMem(int cb) => (IntPtr)NativeMemory.Alloc((nuint)cb);
        public static void FreeCoTaskMem(IntPtr ptr) => NativeMemory.Free((void*)ptr);

        public static int SizeOf<T>() where T : unmanaged => sizeof(T);

        public static string PtrToStringUTF8(IntPtr ptr) => ptr == IntPtr.Zero ? null : new string((sbyte*)ptr);

        public static string PtrToStringUTF8(IntPtr ptr, int byteLen)
            => ptr == IntPtr.Zero ? null : Text.Encoding.UTF8.GetString((byte*)ptr, byteLen);

        public static string PtrToStringAnsi(IntPtr ptr) => PtrToStringUTF8(ptr);

        /// <summary>Allocates a NUL-terminated UTF-8 copy of <paramref name="s"/>; free with <see cref="FreeCoTaskMem"/>.</summary>
        public static IntPtr StringToCoTaskMemUTF8(string s)
        {
            if (s is null)
                return IntPtr.Zero;
            int len = Text.Encoding.UTF8.GetByteCount(s);
            byte* p = (byte*)NativeMemory.Alloc((nuint)len + 1);
            Text.Encoding.UTF8.GetBytes(s, new Span<byte>(p, len));
            p[len] = 0;
            return (IntPtr)p;
        }

        public static IntPtr StringToHGlobalAnsi(string s) => StringToCoTaskMemUTF8(s);

        public static void Copy(byte[] source, int startIndex, IntPtr destination, int length)
        {
            fixed (byte* p = &source[startIndex])
                Buffer.Memmove(ref *(byte*)destination, ref *p, (nuint)length);
        }

        public static void Copy(IntPtr source, byte[] destination, int startIndex, int length)
        {
            fixed (byte* p = &destination[startIndex])
                Buffer.Memmove(ref *p, ref *(byte*)source, (nuint)length);
        }

        public static IntPtr GetFunctionPointerForDelegate<TDelegate>(TDelegate d) where TDelegate : class
        {
            Environment.FailFast("Marshal.GetFunctionPointerForDelegate is not supported; use [UnmanagedCallersOnly] methods.");
            return IntPtr.Zero;
        }
    }
}

namespace System.Runtime.InteropServices
{
    // Required by the compiler's type system; SharpDate never implements dynamic casting.
    public unsafe interface IDynamicInterfaceCastable
    {
        bool IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented);
        RuntimeTypeHandle GetInterfaceImplementation(RuntimeTypeHandle interfaceType);

        internal static IntPtr GetDynamicInterfaceImplementation(object instance, Internal.Runtime.MethodTable* interfaceType, ushort slot)
        {
            Environment.FailFast("IDynamicInterfaceCastable is not supported.");
            return default;
        }
    }
}
