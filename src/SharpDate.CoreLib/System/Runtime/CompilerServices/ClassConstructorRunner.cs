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

    // Array allocations via `newobj` (multidimensional and jagged arrays); names are a contract with ILC.
    internal static unsafe class ArrayHelpers
    {
        public static Array NewObjArray(Internal.Runtime.MethodTable* pEEType, int nDimensions, int* pDimensions)
        {
            int rank = pEEType->ArrayRank;
            if (rank < nDimensions)
            {
                // The (lowerBound, length) constructor: only zero lower bounds are supported.
                for (int i = 0; i < rank; i++)
                {
                    if (pDimensions[2 * i] != 0)
                        throw new PlatformNotSupportedException("Arrays with non-zero lower bounds are not supported.");
                    pDimensions[i] = pDimensions[2 * i + 1];
                }
            }
            return Array.NewMultiDimArray(pEEType, pDimensions, rank);
        }

        public static Array NewObjArrayRare(Internal.Runtime.MethodTable* pEEType, int nDimensions, int* pDimensions)
        {
            if (pEEType->IsSzArray)
            {
                Array ret = System.Runtime.CompilerServices.Unsafe.As<Array>(System.Runtime.RuntimeImports.RhNewArray(pEEType, pDimensions[0]));
                if (nDimensions > 1)
                {
                    // Jagged arrays have constructors for each possible depth.
                    Internal.Runtime.MethodTable* elementType = pEEType->RelatedParameterType;
                    Array[] arrayOfArrays = (Array[])ret;
                    for (int i = 0; i < arrayOfArrays.Length; i++)
                        arrayOfArrays[i] = NewObjArrayRare(elementType, nDimensions - 1, pDimensions + 1);
                }
                return ret;
            }

            if (nDimensions == 2 && pDimensions[0] != 0)
                throw new PlatformNotSupportedException("Arrays with non-zero lower bounds are not supported.");
            int length = pDimensions[nDimensions - 1];
            return Array.NewMultiDimArray(pEEType, &length, 1);
        }
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
