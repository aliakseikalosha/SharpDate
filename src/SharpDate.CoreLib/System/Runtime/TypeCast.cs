// Casting helpers invoked by compiler-generated code for `is`, `as`, casts and array stores.
// Method names and signatures are a contract with ILC (see dotnet/runtime Runtime.Base TypeCast.cs, MIT).

using System.Runtime.CompilerServices;
using Internal.Runtime;

namespace System.Runtime
{
    internal static unsafe class TypeCast
    {
        public static object IsInstanceOfAny(MethodTable* pTargetType, object obj)
        {
            if (obj is null || obj.GetMethodTable() == pTargetType)
                return obj;
            return AreTypesAssignable(obj.GetMethodTable(), pTargetType) ? obj : null;
        }

        public static object IsInstanceOfInterface(MethodTable* pTargetType, object obj)
        {
            if (obj is null)
                return null;
            return ImplementsInterface(obj.GetMethodTable(), pTargetType) ? obj : null;
        }

        public static object IsInstanceOfClass(MethodTable* pTargetType, object obj)
        {
            if (obj is null)
                return null;
            MethodTable* mt = obj.GetMethodTable();
            if (mt == pTargetType)
                return obj;
            if (mt->IsArray || pTargetType->IsArray)
                return AreTypesAssignable(mt, pTargetType) ? obj : null;
            for (mt = mt->BaseType; mt != null; mt = mt->BaseType)
                if (mt == pTargetType)
                    return obj;
            return null;
        }

        public static bool IsInstanceOfException(MethodTable* pTargetType, object obj)
            => IsInstanceOfClass(pTargetType, obj) != null;

        public static object CheckCastAny(MethodTable* pTargetType, object obj)
        {
            if (obj is null || obj.GetMethodTable() == pTargetType)
                return obj;
            if (!AreTypesAssignable(obj.GetMethodTable(), pTargetType))
                ThrowHelpers.ThrowInvalidCastException();
            return obj;
        }

        public static object CheckCastInterface(MethodTable* pTargetType, object obj)
        {
            if (obj is null)
                return null;
            if (!ImplementsInterface(obj.GetMethodTable(), pTargetType))
                ThrowHelpers.ThrowInvalidCastException();
            return obj;
        }

        public static object CheckCastClass(MethodTable* pTargetType, object obj)
        {
            if (obj is null || obj.GetMethodTable() == pTargetType)
                return obj;
            return CheckCastClassSpecial(pTargetType, obj);
        }

        private static object CheckCastClassSpecial(MethodTable* pTargetType, object obj)
        {
            if (IsInstanceOfClass(pTargetType, obj) is null)
                ThrowHelpers.ThrowInvalidCastException();
            return obj;
        }

        public static ref object LdelemaRef(object[] array, nint index, MethodTable* elementType)
        {
            if ((nuint)index >= (nuint)(uint)array.Length)
                ThrowHelpers.ThrowIndexOutOfRangeException();
            ref object element = ref Unsafe.As<ArrayElement[]>(array)[index].Value;
            MethodTable* arrayElementType = array.GetMethodTable()->RelatedParameterType;
            if (elementType != arrayElementType)
                ThrowHelpers.ThrowArrayTypeMismatchException();
            return ref element;
        }

        public static void StelemRef(object[] array, nint index, object obj)
        {
            if ((nuint)index >= (nuint)(uint)array.Length)
                ThrowHelpers.ThrowIndexOutOfRangeException();
            ref object element = ref Unsafe.As<ArrayElement[]>(array)[index].Value;
            if (obj is null)
            {
                element = null;
                return;
            }
            MethodTable* elementType = array.GetMethodTable()->RelatedParameterType;
            if (elementType != obj.GetMethodTable() && elementType != MethodTable.Of<object>()
                && !AreTypesAssignable(obj.GetMethodTable(), elementType))
                ThrowHelpers.ThrowArrayTypeMismatchException();
            element = obj;
        }

        public static void CheckArrayStore(object array, object obj)
        {
            if (obj is null)
                return;
            MethodTable* elementType = array.GetMethodTable()->RelatedParameterType;
            if (!AreTypesAssignable(obj.GetMethodTable(), elementType))
                ThrowHelpers.ThrowArrayTypeMismatchException();
        }

        private struct ArrayElement
        {
            public object Value;
        }

        internal static bool IsDerived(MethodTable* pDerivedType, MethodTable* pBaseType)
        {
            for (MethodTable* mt = pDerivedType; mt != null; mt = mt->BaseType)
                if (mt == pBaseType)
                    return true;
            return false;
        }

        internal static bool ImplementsInterface(MethodTable* pObjType, MethodTable* pTargetType)
        {
            int count = pObjType->NumInterfaces;
            MethodTable** map = pObjType->InterfaceMap;
            for (int i = 0; i < count; i++)
                if (map[i] == pTargetType)
                    return true;

            if (pTargetType->HasGenericVariance)
            {
                for (int i = 0; i < count; i++)
                    if (AreGenericTypesCompatibleViaVariance(map[i], pTargetType))
                        return true;
            }
            return false;
        }

        // Used by interface dispatch: does an implemented interface satisfy a variant target interface?
        internal static bool ImplementsInterfaceVariant(MethodTable* pImplemented, MethodTable* pTarget, bool arrayCovariance)
        {
            if (AreGenericTypesCompatibleViaVariance(pImplemented, pTarget))
                return true;
            if (!arrayCovariance || !pImplemented->IsGeneric || !pTarget->IsGeneric || pImplemented->GenericDefinition != pTarget->GenericDefinition)
                return false;
            // Arrays treat IList<T>/IEnumerable<T>... as covariant in T for reference types.
            int arity = (int)pTarget->GenericArity;
            for (int i = 0; i < arity; i++)
            {
                MethodTable* s = pImplemented->GetGenericArgument(i);
                MethodTable* t = pTarget->GetGenericArgument(i);
                if (s != t && (s->IsValueType || t->IsValueType || !AreTypesAssignable(s, t)))
                    return false;
            }
            return true;
        }

        private static bool AreGenericTypesCompatibleViaVariance(MethodTable* pSourceType, MethodTable* pTargetType)
        {
            if (!pSourceType->IsGeneric || !pTargetType->IsGeneric)
                return false;
            if (pSourceType->GenericDefinition != pTargetType->GenericDefinition)
                return false;
            byte* variance = pTargetType->GenericVariance;
            int arity = (int)pTargetType->GenericArity;
            for (int i = 0; i < arity; i++)
            {
                MethodTable* s = pSourceType->GetGenericArgument(i);
                MethodTable* t = pTargetType->GetGenericArgument(i);
                if (s == t)
                    continue;
                byte v = variance != null ? variance[i] : (byte)0;
                if (v == 1) // covariant
                {
                    if (s->IsValueType || !AreTypesAssignable(s, t))
                        return false;
                }
                else if (v == 2) // contravariant
                {
                    if (t->IsValueType || !AreTypesAssignable(t, s))
                        return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public static bool AreTypesAssignable(MethodTable* pSourceType, MethodTable* pTargetType)
        {
            if (pSourceType == pTargetType)
                return true;

            if (pTargetType->IsInterface)
                return ImplementsInterface(pSourceType, pTargetType);

            if (pSourceType->IsArray)
            {
                if (pTargetType->IsArray)
                {
                    if (pSourceType->ElementType != pTargetType->ElementType)
                        return false;
                    MethodTable* se = pSourceType->RelatedParameterType;
                    MethodTable* te = pTargetType->RelatedParameterType;
                    if (se == te)
                        return true;
                    // Array covariance for reference element types.
                    if (!se->IsValueType && !te->IsValueType)
                        return AreTypesAssignable(se, te);
                    // int[] <-> uint[] style compatibility of same-size primitive arrays.
                    return se->IsPrimitive && te->IsPrimitive && NormalizedPrimitive(se) == NormalizedPrimitive(te);
                }
                return pTargetType == MethodTable.Of<Array>() || pTargetType == MethodTable.Of<object>();
            }

            if (pTargetType->IsNullable && pSourceType->IsValueType)
                return pTargetType->NullableType == pSourceType;

            if (pSourceType->IsPointer || pSourceType->IsFunctionPointer)
                return false;

            return IsDerived(pSourceType, pTargetType);
        }

        private static EETypeElementType NormalizedPrimitive(MethodTable* type)
        {
            EETypeElementType et = type->ElementType;
            switch (et)
            {
                case EETypeElementType.SByte: return EETypeElementType.Byte;
                case EETypeElementType.Int16: return EETypeElementType.UInt16;
                case EETypeElementType.Int32: return EETypeElementType.UInt32;
                case EETypeElementType.Int64: return EETypeElementType.UInt64;
                case EETypeElementType.IntPtr: return EETypeElementType.UIntPtr;
                default: return et;
            }
        }
    }

    // Boxing helpers invoked by compiler-generated code.
    internal static unsafe class RuntimeExports
    {
        public static object RhBox(MethodTable* pEEType, ref byte data)
        {
            ref byte dataAdjusted = ref data;
            if (pEEType->IsNullable)
            {
                if (data == 0)
                    return null;
                dataAdjusted = ref Unsafe.Add(ref data, pEEType->NullableValueOffset);
                pEEType = pEEType->NullableType;
            }

            object result = RuntimeImports.RhNewObject(pEEType);
            Buffer.Memmove(ref result.GetRawData(), ref dataAdjusted, pEEType->ValueTypeSize);
            return result;
        }

        [RuntimeExport("RhBoxAny")]
        public static object RhBoxAny(ref byte data, MethodTable* pEEType)
        {
            if (pEEType->IsValueType)
                return RhBox(pEEType, ref data);
            return Unsafe.As<byte, object>(ref data);
        }

        private static bool UnboxAnyTypeCompare(MethodTable* pEEType, MethodTable* ptrUnboxToEEType)
        {
            if (pEEType == ptrUnboxToEEType)
                return true;
            if (pEEType->ElementType == ptrUnboxToEEType->ElementType)
            {
                switch (ptrUnboxToEEType->ElementType)
                {
                    // Enums unbox to their underlying primitive and vice versa.
                    case EETypeElementType.Byte:
                    case EETypeElementType.SByte:
                    case EETypeElementType.Int16:
                    case EETypeElementType.UInt16:
                    case EETypeElementType.Int32:
                    case EETypeElementType.UInt32:
                    case EETypeElementType.Int64:
                    case EETypeElementType.UInt64:
                    case EETypeElementType.IntPtr:
                    case EETypeElementType.UIntPtr:
                        return true;
                }
            }
            return false;
        }

        public static ref byte RhUnbox2(MethodTable* pUnboxToEEType, object obj)
        {
            if (obj is null)
                ThrowHelpers.ThrowNullReferenceException();
            if (!UnboxAnyTypeCompare(obj.GetMethodTable(), pUnboxToEEType))
                ThrowHelpers.ThrowInvalidCastException();
            return ref obj.GetRawData();
        }

        public static void RhUnboxNullable(ref byte data, MethodTable* pUnboxToEEType, object obj)
        {
            if (obj != null && obj.GetMethodTable() != pUnboxToEEType->NullableType)
                ThrowHelpers.ThrowInvalidCastException();
            RhUnbox(obj, ref data, pUnboxToEEType);
        }

        public static void RhUnboxTypeTest(MethodTable* pType, MethodTable* pBoxType)
        {
            if (!UnboxAnyTypeCompare(pType, pBoxType))
                ThrowHelpers.ThrowInvalidCastException();
        }

        [RuntimeExport("RhUnboxAny")]
        public static void RhUnboxAny(object o, ref byte data, MethodTable* pUnboxToEEType)
        {
            if (pUnboxToEEType->IsValueType)
            {
                bool isValid = pUnboxToEEType->IsNullable
                    ? (o is null || o.GetMethodTable() == pUnboxToEEType->NullableType)
                    : (o != null && UnboxAnyTypeCompare(o.GetMethodTable(), pUnboxToEEType));
                if (!isValid)
                    ThrowHelpers.ThrowInvalidCastException();
                RhUnbox(o, ref data, pUnboxToEEType);
            }
            else
            {
                if (o != null && TypeCast.IsInstanceOfAny(pUnboxToEEType, o) is null)
                    ThrowHelpers.ThrowInvalidCastException();
                Unsafe.As<byte, object>(ref data) = o;
            }
        }

        [RuntimeExport("RhUnbox")]
        public static void RhUnbox(object obj, ref byte data, MethodTable* pUnboxToEEType)
        {
            if (obj is null)
            {
                // Only Nullable<T> can be unboxed from null: clear the whole value.
                SpanHelpers.ClearWithoutReferences(ref data, pUnboxToEEType->ValueTypeSize);
                return;
            }

            if (pUnboxToEEType != null && pUnboxToEEType->IsNullable)
            {
                // Set HasValue and copy the payload after it.
                Unsafe.As<byte, bool>(ref data) = true;
                data = ref Unsafe.Add(ref data, pUnboxToEEType->NullableValueOffset);
            }

            MethodTable* objType = obj.GetMethodTable();
            Buffer.Memmove(ref data, ref obj.GetRawData(), objType->ValueTypeSize);
        }
    }
}

namespace System.Runtime
{
    // Generic virtual methods (virtual or interface methods with their own type parameters) need the
    // NativeAOT type loader's dispatch tables, which SharpDate's minimal runtime does not include.
    internal static class TypeLoaderExports
    {
        public static IntPtr GVMLookupForSlot(object obj, RuntimeMethodHandle slot)
        {
            Environment.FailFast("SharpDate does not support generic virtual methods (virtual/abstract/interface methods with their own type parameters).");
            return IntPtr.Zero;
        }
    }
}
