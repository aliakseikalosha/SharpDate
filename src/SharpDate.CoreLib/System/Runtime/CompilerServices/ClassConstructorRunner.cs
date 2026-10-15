using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    // The compiler calls into this class to run static constructors lazily (names are a contract with ILC).
    // Playdate games run managed code on a single thread, so no locking is needed.
    internal static unsafe class ClassConstructorRunner
    {
        private static object CheckStaticClassConstructionReturnGCStaticBase(ref StaticClassConstructionContext context, object gcStaticBase)
        {
            CheckStaticClassConstruction(ref context);
            return gcStaticBase;
        }

        private static IntPtr CheckStaticClassConstructionReturnNonGCStaticBase(ref StaticClassConstructionContext context, IntPtr nonGcStaticBase)
        {
            CheckStaticClassConstruction(ref context);
            return nonGcStaticBase;
        }

        private static void CheckStaticClassConstruction(ref StaticClassConstructionContext context)
        {
            IntPtr cctor = context.cctorMethodAddress;
            if (cctor == IntPtr.Zero)
                return;
            // Mark as initialized before running so recursive accesses during the cctor see the type as ready.
            context.cctorMethodAddress = IntPtr.Zero;
            ((delegate*<void>)cctor)();
        }
    }

    // Holds the address of a type's static constructor; zero once it has run. Contract with the compiler.
    [StructLayout(LayoutKind.Sequential)]
    public struct StaticClassConstructionContext
    {
        public IntPtr cctorMethodAddress;
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    using System;

    internal static class LdTokenHelpers
    {
        private static RuntimeTypeHandle GetRuntimeTypeHandle(IntPtr pEEType) => new RuntimeTypeHandle(pEEType);

        private static Type GetRuntimeType(IntPtr pEEType) => Type.GetTypeFromHandle(new RuntimeTypeHandle(pEEType));

        private static RuntimeMethodHandle GetRuntimeMethodHandle(IntPtr pHandleSignature) => default;

        private static RuntimeFieldHandle GetRuntimeFieldHandle(IntPtr pHandleSignature) => default;
    }

    internal static class DelegateHelpers
    {
        private static object[] GetEmptyObjectArray() => Array.Empty<object>();
    }

    internal static class SynchronizedMethodHelpers
    {
        private static void MonitorEnter(object obj, ref bool lockTaken) => lockTaken = true;
        private static void MonitorExit(object obj, ref bool lockTaken) => lockTaken = false;
    }
}
