// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeTypeHandle : IEquatable<RuntimeTypeHandle>
    {
        private IntPtr _value;

        internal RuntimeTypeHandle(IntPtr value) { _value = value; }

        public IntPtr Value => _value;

        internal MethodTable* ToMethodTable() => (MethodTable*)_value;

        [Intrinsic]
        internal static IntPtr ToIntPtr(RuntimeTypeHandle handle) => handle._value;

        public bool Equals(RuntimeTypeHandle other) => _value == other._value;
        public override bool Equals(object obj) => obj is RuntimeTypeHandle h && h._value == _value;
        public override int GetHashCode() => (int)_value;
        public static bool operator ==(RuntimeTypeHandle left, RuntimeTypeHandle right) => left._value == right._value;
        public static bool operator !=(RuntimeTypeHandle left, RuntimeTypeHandle right) => left._value != right._value;
    }

    public struct RuntimeMethodHandle
    {
        private IntPtr _value;
        public IntPtr Value => _value;
    }

    public struct RuntimeFieldHandle
    {
        private IntPtr _value;
        public IntPtr Value => _value;
    }

    /// <summary>
    /// Minimal type identity. SharpDate does not ship reflection metadata, so names are not available;
    /// comparisons (typeof(A) == x.GetType()) and IsValueType/IsArray queries work.
    /// </summary>
    public abstract unsafe class Type
    {
        private protected Type() { }

        public abstract RuntimeTypeHandle TypeHandle { get; }

        [Intrinsic]
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle) => handle.Value == IntPtr.Zero ? null : GetTypeFromMethodTable(handle.ToMethodTable());

        // There is exactly one Type object per type (the compiler relies on this for `==`):
        // it is cached in the type's writable data cell and kept alive as a GC root.
        internal static Type GetTypeFromMethodTable(MethodTable* mt)
        {
            IntPtr* cell = mt->WritableData;
            if (*cell != IntPtr.Zero)
                return Unsafe.As<IntPtr, Type>(ref *cell);
            Type type = new RuntimeType(new RuntimeTypeHandle((IntPtr)mt));
            System.Runtime.RuntimeImports.sd_gc_add_root(type);
            *cell = Unsafe.As<Type, IntPtr>(ref type);
            return type;
        }

        private MethodTable* MT => TypeHandle.ToMethodTable();

        public bool IsValueType => MT->IsValueType;
        public bool IsArray => MT->IsArray;
        public bool IsInterface => MT->IsInterface;
        public bool IsPrimitive => MT->IsPrimitive;
        public bool IsClass => !MT->IsValueType && !MT->IsInterface;
        public bool IsGenericType => MT->IsGeneric;
        public bool IsEnum => MT->IsValueType && MT->BaseType == MethodTable.Of<Enum>();
        public Type BaseType => MT->BaseType == null ? null : GetTypeFromMethodTable(MT->BaseType);
        public Type GetElementType() => MT->IsArray || MT->IsPointer ? GetTypeFromMethodTable(MT->RelatedParameterType) : null;

        public bool IsAssignableFrom(Type c) => c is not null && System.Runtime.TypeCast.AreTypesAssignable(c.MT, MT);
        public bool IsInstanceOfType(object o) => o is not null && IsAssignableFrom(o.GetType());

        public string Name => string.Concat("Type#", Number.FormatUInt32(MT->HashCode, "X8"));
        public string FullName => Name;

        [Intrinsic]
        public static bool operator ==(Type left, Type right)
        {
            if (left is null)
                return right is null;
            if (right is null)
                return false;
            return left.TypeHandle.Value == right.TypeHandle.Value;
        }

        [Intrinsic]
        public static bool operator !=(Type left, Type right) => !(left == right);

        public override bool Equals(object o) => o is Type t && t.TypeHandle.Value == TypeHandle.Value;
        public override int GetHashCode() => (int)MT->HashCode;
        public override string ToString() => Name;
    }

    internal sealed class RuntimeType : Type
    {
        private readonly RuntimeTypeHandle _handle;

        internal RuntimeType(RuntimeTypeHandle handle) { _handle = handle; }

        public override RuntimeTypeHandle TypeHandle => _handle;
    }
}
