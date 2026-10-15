// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// Module initialization for the SharpDate runtime: sets up GC static bases and runs eager
// static constructors, following the ReadyToRun module-header contract emitted by ILC
// (see dotnet/runtime src/coreclr/nativeaot/Common/src/Internal/Runtime/CompilerHelpers/StartupCodeHelpers.cs, MIT).

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    internal static unsafe class StartupCodeHelpers
    {
        private const uint ReadyToRunSignature = 0x00525452; // 'RTR'

        private const int SectionGCStaticRegion = 201;
        private const int SectionTypeManagerIndirection = 204;
        private const int SectionEagerCctor = 205;
        private const int SectionDehydratedData = 207;
        private const int SectionModuleInitializerList = 213;

        private const int ModuleInfoHasEndPointer = 0x1;

        private const nint GCStaticUninitialized = 0x1;
        private const nint GCStaticHasPreInitializedData = 0x2;
        private const nint GCStaticMask = 0x3;

        [StructLayout(LayoutKind.Sequential)]
        private struct ReadyToRunHeader
        {
            public uint Signature;
            public ushort MajorVersion;
            public ushort MinorVersion;
            public uint Flags;
            public ushort NumberOfSections;
            public byte EntrySize;
            public byte EntryType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ModuleInfoRow
        {
            public int SectionId;
            public int Flags;
            public byte* Start;
            public byte* End;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TypeManagerSlot
        {
            public IntPtr TypeManager;
            public int ModuleIndex;
        }

        /// <summary>
        /// Called once by the native runtime (sdrt.c) before any other managed code runs.
        /// Must not touch statics or allocate anything that needs statics.
        /// </summary>
        [RuntimeExport("SharpDate_InitializeModules")]
        private static void InitializeModules(void** headers, int count)
        {
            for (int i = 0; i < count; i++)
            {
                ReadyToRunHeader* header = (ReadyToRunHeader*)headers[i];
                if (header == null || header->Signature != ReadyToRunSignature)
                    continue;

                int length;
                TypeManagerSlot* slot = (TypeManagerSlot*)GetSection(header, SectionTypeManagerIndirection, out length);
                if (slot != null)
                {
                    slot->TypeManager = (IntPtr)header;
                    slot->ModuleIndex = i;
                }

                if (GetSection(header, SectionDehydratedData, out length) != null)
                    RuntimeImports.sd_fail_fast(null, 0); // built with --dehydrate, which SharpDate does not support

                byte* statics = GetSection(header, SectionGCStaticRegion, out length);
                if (statics != null)
                    InitializeStatics(statics, length);
            }

            for (int i = 0; i < count; i++)
            {
                ReadyToRunHeader* header = (ReadyToRunHeader*)headers[i];
                if (header != null && header->Signature == ReadyToRunSignature)
                    RunInitializers(header, SectionEagerCctor);
            }

            for (int i = 0; i < count; i++)
            {
                ReadyToRunHeader* header = (ReadyToRunHeader*)headers[i];
                if (header != null && header->Signature == ReadyToRunSignature)
                    RunInitializers(header, SectionModuleInitializerList);
            }
        }

        private static byte* GetSection(ReadyToRunHeader* header, int sectionId, out int length)
        {
            byte* rows = (byte*)(header + 1);
            for (int i = 0; i < header->NumberOfSections; i++)
            {
                ModuleInfoRow* row = (ModuleInfoRow*)(rows + i * header->EntrySize);
                if (row->SectionId == sectionId)
                {
                    length = (row->Flags & ModuleInfoHasEndPointer) != 0 ? (int)(row->End - row->Start) : sizeof(void*);
                    return row->Start;
                }
            }
            length = 0;
            return null;
        }

        private static void* ReadRelPtr32(void* address) => (byte*)address + *(int*)address;

        private static void RunInitializers(ReadyToRunHeader* header, int sectionId)
        {
            byte* start = GetSection(header, sectionId, out int length);
            if (start == null)
                return;
            for (byte* p = start; p < start + length; p += sizeof(int))
            {
                var initializer = (delegate*<void>)ReadRelPtr32(p);
                initializer();
            }
        }

        private static void InitializeStatics(byte* region, int length)
        {
            for (byte* block = region; block < region + length; block += sizeof(int))
            {
                // Each entry points at a static-base slot. Uninitialized slots hold a relative pointer to the
                // MethodTable describing the statics object (tagged in the low bits); we allocate the object
                // and replace the slot with a real object reference.
                nint* pBlock = (nint*)ReadRelPtr32(block);
                nint blockAddr = (nint)ReadRelPtr32(pBlock);
                if ((blockAddr & GCStaticUninitialized) != GCStaticUninitialized)
                    continue;

                MethodTable* mt = (MethodTable*)(blockAddr & ~GCStaticMask);
                object obj = RuntimeImports.RhNewObject(mt);
                RuntimeImports.sd_gc_add_root(obj);

                if ((blockAddr & GCStaticHasPreInitializedData) == GCStaticHasPreInitializedData)
                {
                    void* preInitData = ReadRelPtr32((int*)pBlock + 1);
                    Buffer.Memmove(ref obj.GetRawData(), ref *(byte*)preInitData, obj.GetRawObjectDataSize());
                }

                *pBlock = Unsafe.As<object, nint>(ref obj);
            }
        }
    }
}
