using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System.Runtime
{
    // Exposes a managed method under a fixed native symbol name.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class RuntimeExportAttribute : Attribute
    {
        public string EntryPoint;
        public RuntimeExportAttribute(string entry) { EntryPoint = entry; }
    }

    // Binds an extern method directly to a native symbol (no marshalling, no GC transition).
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class RuntimeImportAttribute : Attribute
    {
        public string DllName { get; }
        public string EntryPoint { get; }
        public RuntimeImportAttribute(string entry) { EntryPoint = entry; }
        public RuntimeImportAttribute(string dllName, string entry) { EntryPoint = entry; DllName = dllName; }
    }

    // Entry points of the native half of the SharpDate runtime (src/SharpDate.Runtime/sdrt.c).
    internal static unsafe class RuntimeImports
    {
        private const string RuntimeLibrary = "*";

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpNewFast")]
        internal static extern object RhNewObject(MethodTable* pEEType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpNewArrayFast")]
        internal static extern object RhNewArray(MethodTable* pEEType, int length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_handle_alloc")]
        internal static extern IntPtr RhHandleAlloc(object value, GCHandleType type);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_handle_free")]
        internal static extern void RhHandleFree(IntPtr handle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_handle_get")]
        internal static extern object RhHandleGet(IntPtr handle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_handle_set")]
        internal static extern void RhHandleSet(IntPtr handle, object value);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_alloc")]
        internal static extern void* sd_alloc(nuint size);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_realloc")]
        internal static extern void* sd_realloc(void* ptr, nuint size);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_free")]
        internal static extern void sd_free(void* ptr);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_gc_collect")]
        internal static extern void sd_gc_collect();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_gc_total_bytes")]
        internal static extern nuint sd_gc_total_bytes();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_gc_collection_count")]
        internal static extern int sd_gc_collection_count();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_gc_add_root")]
        internal static extern void sd_gc_add_root(object obj);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_fail_fast")]
        internal static extern void sd_fail_fast(char* message, int length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_log")]
        internal static extern void sd_log(byte* utf8, int length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_ticks_ms")]
        internal static extern uint sd_ticks_ms();

        // Arithmetic helpers that must not depend on the managed operators they implement.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_idiv")]
        internal static extern int sd_idiv(int a, int b);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_imod")]
        internal static extern int sd_imod(int a, int b);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_uidiv")]
        internal static extern uint sd_uidiv(uint a, uint b);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_uimod")]
        internal static extern uint sd_uimod(uint a, uint b);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_ldiv")]
        internal static extern long sd_ldiv(long a, long b);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_lmod")]
        internal static extern long sd_lmod(long a, long b);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_uldiv")]
        internal static extern ulong sd_uldiv(ulong a, ulong b);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_ulmod")]
        internal static extern ulong sd_ulmod(ulong a, ulong b);

        // Double-precision helpers (implemented in C so device builds use soft-float).
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_format_double")]
        internal static extern int sd_format_double(double value, int decimals, byte* buffer, int bufferLength);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_format_float")]
        internal static extern int sd_format_float(float value, int decimals, byte* buffer, int bufferLength);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_parse_float")]
        internal static extern float sd_parse_float(byte* text, int length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "sd_double_to_float")]
        internal static extern float sd_double_to_float(double value);
    }
}
