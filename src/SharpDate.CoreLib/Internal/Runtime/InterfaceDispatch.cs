// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// Interface method resolution for the SharpDate runtime. Called from the dispatch stub in
// sdrt_*.S with the receiver and the dispatch cell emitted by ILC.
// Algorithm follows dotnet/runtime Runtime.Base DispatchResolve.cs (MIT).

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime
{
    internal static unsafe class InterfaceDispatch
    {
        // Cell layout: [stub pointer][cache word] where the cache word holds a 32-bit relative pointer
        // to the interface MethodTable tagged in its low bits. Cells are grouped in runs that end with a
        // terminator cell whose stub pointer is null and whose second word holds the interface slot.
        private const int CachePointerIsIndirectedInterfaceRelativePointer = 0x2;
        private const int CachePointerIsInterfaceRelativePointer = 0x3;

        private const int CacheSize = 256; // direct-mapped (cell, type) -> target cache

        private struct CacheEntry
        {
            public IntPtr Cell;
            public MethodTable* Type;
            public IntPtr Target;
        }

        private static CacheEntry* s_cache;

        [RuntimeExport("SharpDate_ResolveInterfaceDispatch")]
        private static IntPtr Resolve(object obj, IntPtr cell)
        {
            if (obj is null)
                ThrowHelpers.ThrowNullReferenceException();

            MethodTable* type = obj.GetMethodTable();

            if (s_cache == null)
            {
                s_cache = (CacheEntry*)RuntimeImports.sd_alloc((nuint)(sizeof(CacheEntry) * CacheSize));
                SpanHelpers.ClearWithoutReferences(ref *(byte*)s_cache, (nuint)(sizeof(CacheEntry) * CacheSize));
            }

            uint hash = (uint)(((nuint)cell >> 3) ^ ((nuint)type >> 4)) & (CacheSize - 1);
            CacheEntry* entry = s_cache + hash;
            if (entry->Cell == cell && entry->Type == type)
                return entry->Target;

            GetCellInfo(cell, out MethodTable* interfaceType, out ushort slot);
            IntPtr target = FindInterfaceMethodImplementationTarget(type, interfaceType, slot);
            if (target == IntPtr.Zero)
                Environment.FailFast("SharpDate: interface method implementation not found.");

            entry->Cell = cell;
            entry->Type = type;
            entry->Target = target;
            return target;
        }

        private static void GetCellInfo(IntPtr cell, out MethodTable* interfaceType, out ushort slot)
        {
            byte* cacheWord = (byte*)cell + sizeof(IntPtr);
            int rel = *(int*)cacheWord;
            nint tagged = (nint)(cacheWord + rel);
            int flags = (int)(tagged & 0x3);
            MethodTable* mt = (MethodTable*)(tagged & ~(nint)0x3);
            if (flags == CachePointerIsIndirectedInterfaceRelativePointer)
                mt = *(MethodTable**)mt;
            else if (flags != CachePointerIsInterfaceRelativePointer)
                Environment.FailFast("SharpDate: unsupported interface dispatch cell.");
            interfaceType = mt;

            // Walk forward to the terminator cell of this run, which records the slot number.
            byte* p = (byte*)cell;
            while (*(IntPtr*)p != IntPtr.Zero)
                p += 2 * sizeof(IntPtr);
            slot = (ushort)*(nint*)(p + sizeof(IntPtr));
        }

        internal static IntPtr FindInterfaceMethodImplementationTarget(MethodTable* pTgtType, MethodTable* pItfType, ushort itfSlotNumber)
        {
            bool defaultImplementations = false;
            while (true)
            {
                MethodTable* pCur = pTgtType;
                while (pCur != null)
                {
                    if (FindImplSlotForCurrentType(pCur, pItfType, itfSlotNumber, defaultImplementations, out ushort implSlotNumber))
                    {
                        if (implSlotNumber < pCur->NumVtableSlots)
                            return pTgtType->VTable[implSlotNumber];
                        if (implSlotNumber >= 0xFFF0)
                            return IntPtr.Zero; // reabstraction / diamond
                        return pCur->GetSealedVirtualSlot((ushort)(implSlotNumber - pCur->NumVtableSlots));
                    }
                    pCur = pCur->IsArray ? MethodTable.Of<Array>() : pCur->NonArrayBaseType;
                }
                if (defaultImplementations)
                    return IntPtr.Zero;
                defaultImplementations = true;
            }
        }

        private static bool FindImplSlotForCurrentType(MethodTable* pTgtType, MethodTable* pItfType, ushort itfSlotNumber, bool defaultImplementations, out ushort implSlotNumber)
        {
            implSlotNumber = 0;
            if (!pItfType->IsInterface)
            {
                implSlotNumber = itfSlotNumber;
                return pTgtType == pItfType;
            }
            if (!pTgtType->HasDispatchMap)
                return false;
            return FindImplSlotInMap(pTgtType, pItfType, itfSlotNumber, defaultImplementations, variant: false, out implSlotNumber)
                || FindImplSlotInMap(pTgtType, pItfType, itfSlotNumber, defaultImplementations, variant: true, out implSlotNumber);
        }

        private static bool FindImplSlotInMap(MethodTable* pTgtType, MethodTable* pItfType, ushort itfSlotNumber, bool defaultImplementations, bool variant, out ushort implSlotNumber)
        {
            implSlotNumber = 0;
            bool arrayCovariance = pTgtType->IsArray;
            if (variant && !pItfType->HasGenericVariance && !(arrayCovariance && pItfType->IsGeneric))
                return false;

            DispatchMap* map = pTgtType->DispatchMap;
            DispatchMap.Entry* entries = map->Entries;
            int start = defaultImplementations ? map->StandardEntryCount : 0;
            int end = defaultImplementations ? map->StandardEntryCount + map->DefaultEntryCount : map->StandardEntryCount;
            for (int i = start; i < end; i++)
            {
                DispatchMap.Entry* e = entries + i;
                if (e->InterfaceMethodSlot != itfSlotNumber)
                    continue;
                MethodTable* entryType = pTgtType->InterfaceMap[e->InterfaceIndex];
                if (entryType == pItfType || (variant && TypeCast.ImplementsInterfaceVariant(entryType, pItfType, arrayCovariance)))
                {
                    implSlotNumber = e->ImplMethodSlot;
                    return true;
                }
            }
            return false;
        }
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    internal static unsafe class ExceptionReporting
    {
        // Called by RhpThrowEx: SharpDate does not unwind, so every throw ends the game with a message.
        [RuntimeExport("SharpDate_ReportUnhandledException")]
        private static void ReportUnhandledException(object exception)
        {
            string message = "Unhandled exception";
            if (exception is Exception ex)
            {
                string text = ex.Message;
                if (!string.IsNullOrEmpty(text))
                    message = string.Concat("Unhandled exception: ", text);
            }
            Environment.FailFast(message);
        }
    }
}
