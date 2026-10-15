// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// Layout of compiler-generated type descriptors (MethodTable / EEType).
// This mirrors the data contract emitted by the .NET NativeAOT compiler (ILC); see
// dotnet/runtime src/coreclr/nativeaot/Common/src/Internal/Runtime/MethodTable.cs (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Internal.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ObjHeader
    {
        private IntPtr _objHeaderContents;
    }

    internal static class EETypeFlags
    {
        public const uint EETypeKindMask = 0x00030000;
        public const uint HasDispatchMap = 0x00040000;
        public const uint IsDynamicType = 0x00080000;
        public const uint HasFinalizer = 0x00100000;
        public const uint HasSealedVTableEntries = 0x00400000;
        public const uint GenericVariance = 0x00800000;
        public const uint HasPointers = 0x01000000;
        public const uint IsGeneric = 0x02000000;
        public const uint ElementTypeMask = 0x7C000000;
        public const int ElementTypeShift = 26;
        public const uint HasComponentSize = 0x80000000;

        // Extended flags (low 16 bits when the type has no component size)
        public const uint ValueTypeFieldPaddingMask = 0x00E0;
        public const int ValueTypeFieldPaddingShift = 5;
        public const uint NullableValueOffsetMask = 0x0700;
        public const int NullableValueOffsetShift = 8;
        public const uint RequiresAlign8 = 0x1000;
    }

    internal enum EETypeKind : uint
    {
        CanonicalEEType = 0x00000000,
        FunctionPointerEEType = 0x00010000,
        ParameterizedEEType = 0x00020000,
        GenericTypeDefEEType = 0x00030000,
    }

    internal enum EETypeElementType
    {
        Unknown = 0x00,
        Void = 0x01,
        Boolean = 0x02,
        Char = 0x03,
        SByte = 0x04,
        Byte = 0x05,
        Int16 = 0x06,
        UInt16 = 0x07,
        Int32 = 0x08,
        UInt32 = 0x09,
        Int64 = 0x0A,
        UInt64 = 0x0B,
        IntPtr = 0x0C,
        UIntPtr = 0x0D,
        Single = 0x0E,
        Double = 0x0F,
        ValueType = 0x10,
        Nullable = 0x12,
        Class = 0x14,
        Interface = 0x15,
        SystemArray = 0x16,
        Array = 0x17,
        SzArray = 0x18,
        ByRef = 0x19,
        Pointer = 0x1A,
        FunctionPointer = 0x1B,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DispatchMap
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Entry
        {
            public ushort InterfaceIndex;
            public ushort InterfaceMethodSlot;
            public ushort ImplMethodSlot;
        }

        public ushort StandardEntryCount;
        public ushort DefaultEntryCount;
        public ushort StandardStaticEntryCount;
        public ushort DefaultStaticEntryCount;
        // Entry[StandardEntryCount + DefaultEntryCount] follows, then static entries.

        public Entry* Entries => (Entry*)((byte*)Unsafe.AsPointer(ref this) + 4 * sizeof(ushort));
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MethodTable
    {
        // Low 16 bits: component size (arrays/strings) or extended flags. High 16 bits: flags.
        private uint _uFlags;
        private uint _uBaseSize;
        private MethodTable* _relatedType;
        private ushort _usNumVtableSlots;
        private ushort _usNumInterfaces;
        private uint _uHashCode;
        // IntPtr vtable[_usNumVtableSlots] follows,
        // then MethodTable* interfaces[_usNumInterfaces],
        // then optional relative-pointer fields (see GetFieldOffset).

        [Intrinsic]
        internal static extern MethodTable* Of<T>();

        internal uint Flags => _uFlags;
        internal uint BaseSize => _uBaseSize;
        internal uint HashCode => _uHashCode;
        internal ushort NumVtableSlots => _usNumVtableSlots;
        internal ushort NumInterfaces => _usNumInterfaces;

        internal bool HasComponentSize => (int)_uFlags < 0;
        internal ushort ComponentSize => HasComponentSize ? (ushort)_uFlags : (ushort)0;
        internal ushort RawLowBits => (ushort)_uFlags;

        internal EETypeKind Kind => (EETypeKind)(_uFlags & EETypeFlags.EETypeKindMask);
        internal EETypeElementType ElementType => (EETypeElementType)((_uFlags & EETypeFlags.ElementTypeMask) >> EETypeFlags.ElementTypeShift);

        internal bool IsCanonical => Kind == EETypeKind.CanonicalEEType;
        internal bool IsParameterizedType => Kind == EETypeKind.ParameterizedEEType;
        internal bool IsGenericTypeDefinition => Kind == EETypeKind.GenericTypeDefEEType;
        internal bool IsFunctionPointer => Kind == EETypeKind.FunctionPointerEEType;

        internal bool IsArray
        {
            get
            {
                EETypeElementType et = ElementType;
                return et == EETypeElementType.Array || et == EETypeElementType.SzArray;
            }
        }

        internal bool IsSzArray => ElementType == EETypeElementType.SzArray;
        internal bool IsString => IsCanonical && ComponentSize == 2;
        internal bool IsInterface => ElementType == EETypeElementType.Interface;
        internal bool IsValueType => ElementType < EETypeElementType.Class;
        internal bool IsPrimitive => ElementType < EETypeElementType.ValueType;
        internal bool IsNullable => ElementType == EETypeElementType.Nullable;
        internal bool IsPointer => ElementType == EETypeElementType.Pointer;
        internal bool IsByRef => ElementType == EETypeElementType.ByRef;
        internal bool IsGeneric => (_uFlags & EETypeFlags.IsGeneric) != 0;
        internal bool HasGenericVariance => (_uFlags & EETypeFlags.GenericVariance) != 0;
        internal bool HasDispatchMap => (_uFlags & EETypeFlags.HasDispatchMap) != 0;
        internal bool IsFinalizable => (_uFlags & EETypeFlags.HasFinalizer) != 0;
        internal bool HasSealedVTableEntries => (_uFlags & EETypeFlags.HasSealedVTableEntries) != 0;
        internal bool IsDynamicType => (_uFlags & EETypeFlags.IsDynamicType) != 0;
        internal bool ContainsGCPointers => (_uFlags & EETypeFlags.HasPointers) != 0;
        internal bool RequiresAlign8 => !HasComponentSize && (_uFlags & EETypeFlags.RequiresAlign8) != 0;

        internal uint ValueTypeFieldPadding => (_uFlags & EETypeFlags.ValueTypeFieldPaddingMask) >> EETypeFlags.ValueTypeFieldPaddingShift;

        // Size of the unboxed value: BaseSize minus object header, MethodTable pointer and heap padding.
        internal uint ValueTypeSize => _uBaseSize - (uint)(sizeof(ObjHeader) + sizeof(MethodTable*)) - ValueTypeFieldPadding;

        internal byte NullableValueOffset
        {
            get
            {
                int log2 = (int)((_uFlags & EETypeFlags.NullableValueOffsetMask) >> EETypeFlags.NullableValueOffsetShift);
                return (byte)(1 << log2);
            }
        }

        // Base type for canonical types; element type for arrays and pointers.
        internal MethodTable* RelatedParameterType => _relatedType;

        internal MethodTable* NonArrayBaseType => _relatedType;

        internal MethodTable* BaseType
        {
            get
            {
                if (IsCanonical)
                    return _relatedType;
                if (IsArray)
                    return Of<Array>();
                return null;
            }
        }

        // Base size of a single-dimensional array: MethodTable*, length (padded to pointer size), one pointer of payload alignment.
        internal static int SzArrayBaseSize => 2 * sizeof(IntPtr) + (sizeof(IntPtr) == 8 ? 8 : 4);

        /// <summary>Array rank; multidimensional arrays store two ints (length, lower bound) per dimension.</summary>
        internal int ArrayRank
        {
            get
            {
                int boundsSize = (int)_uBaseSize - SzArrayBaseSize;
                return boundsSize > 0 ? boundsSize / (2 * sizeof(int)) : 1;
            }
        }

        internal IntPtr* VTable => (IntPtr*)((byte*)Unsafe.AsPointer(ref this) + sizeof(MethodTable));

        internal MethodTable** InterfaceMap => (MethodTable**)((byte*)VTable + sizeof(IntPtr) * _usNumVtableSlots);

        internal uint GetFieldOffset(int field)
        {
            // Field order (statically generated types always use 32-bit relative pointers):
            // 0 TypeManagerIndirection, 1 WritableData, 2 DispatchMap?, 3 Finalizer?,
            // 4 SealedVirtualSlots?, 6 GenericDefinition?, 7 GenericComposition?
            uint offset = (uint)(sizeof(MethodTable) + sizeof(IntPtr) * _usNumVtableSlots + sizeof(MethodTable*) * _usNumInterfaces);
            if (field == FieldTypeManager) return offset;
            offset += 4;
            if (field == FieldWritableData) return offset;
            offset += 4;
            if (field == FieldDispatchMap) return offset;
            if (HasDispatchMap) offset += 4;
            if (field == FieldFinalizer) return offset;
            if (IsFinalizable) offset += 4;
            if (field == FieldSealedVirtualSlots) return offset;
            if (HasSealedVTableEntries) offset += 4;
            if (field == FieldGenericDefinition) return offset;
            if (IsGeneric) offset += 4;
            return offset; // FieldGenericComposition
        }

        internal const int FieldTypeManager = 0;
        internal const int FieldWritableData = 1;
        internal const int FieldDispatchMap = 2;
        internal const int FieldFinalizer = 3;
        internal const int FieldSealedVirtualSlots = 4;
        internal const int FieldGenericDefinition = 6;
        internal const int FieldGenericComposition = 7;

        private void* ReadRelPtr(uint offset)
        {
            byte* p = (byte*)Unsafe.AsPointer(ref this) + offset;
            return p + *(int*)p;
        }

        /// <summary>Pointer-sized writable cell owned by the runtime (caches the type's RuntimeType object).</summary>
        internal IntPtr* WritableData => (IntPtr*)ReadRelPtr(GetFieldOffset(FieldWritableData));

        internal DispatchMap* DispatchMap => HasDispatchMap ? (DispatchMap*)ReadRelPtr(GetFieldOffset(FieldDispatchMap)) : null;

        internal MethodTable* GenericDefinition => (MethodTable*)ReadRelPtr(GetFieldOffset(FieldGenericDefinition));

        // For generic type definitions the low 16 bits hold the number of type parameters.
        internal uint GenericParameterCount => (ushort)_uFlags;

        internal uint GenericArity => GenericDefinition->GenericParameterCount;

        internal MethodTable* GetGenericArgument(int index)
        {
            uint offset = GetFieldOffset(FieldGenericComposition);
            int* list;
            if (GenericArity == 1)
            {
                list = (int*)((byte*)Unsafe.AsPointer(ref this) + offset);
            }
            else
            {
                list = (int*)ReadRelPtr(offset);
            }
            int* entry = list + index;
            return (MethodTable*)((byte*)entry + *entry);
        }

        // Variance info (one byte per type parameter) lives on the generic definition.
        internal byte* GenericVariance
        {
            get
            {
                if (!HasGenericVariance)
                    return null;
                if (IsGeneric)
                    return GenericDefinition->GenericVariance;
                return (byte*)ReadRelPtr(GetFieldOffset(FieldGenericComposition));
            }
        }

        // Nullable<T> has exactly one type argument, stored inline as a relative pointer.
        internal MethodTable* NullableType => (MethodTable*)ReadRelPtr(GetFieldOffset(FieldGenericComposition));

        internal IntPtr GetSealedVirtualSlot(ushort slotNumber)
        {
            int* table = (int*)ReadRelPtr(GetFieldOffset(FieldSealedVirtualSlots));
            int* entry = table + slotNumber;
            return (IntPtr)((byte*)entry + *entry);
        }
    }
}
