using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System
{
    public unsafe class Object
    {
        // The layout of object is a contract with the compiler: the first pointer-sized
        // field of every object is its MethodTable.
        private MethodTable* m_pEEType;

        public Object() { }

        ~Object() { }

        public virtual bool Equals(object obj) => RuntimeHelpers.ObjectReferenceEquals(this, obj);

        // The SharpDate GC never moves objects, so the address is a stable hash.
        public virtual int GetHashCode() => RuntimeHelpers.GetHashCode(this);

        public virtual string ToString() => "System.Object";

        public static bool ReferenceEquals(object objA, object objB) => RuntimeHelpers.ObjectReferenceEquals(objA, objB);

        public static bool Equals(object objA, object objB)
        {
            if (RuntimeHelpers.ObjectReferenceEquals(objA, objB))
                return true;
            if (objA is null || objB is null)
                return false;
            return objA.Equals(objB);
        }

        public Type GetType() => Type.GetTypeFromMethodTable(m_pEEType);

        protected object MemberwiseClone()
        {
            object clone = m_pEEType->HasComponentSize
                ? RuntimeImports.RhNewArray(m_pEEType, Unsafe.As<RawArrayData>(this).Length)
                : RuntimeImports.RhNewObject(m_pEEType);
            nuint size = RuntimeHelpers.GetObjectDataSize(this);
            Buffer.Memmove(ref clone.GetRawData(), ref GetRawData(), size);
            return clone;
        }

        internal MethodTable* GetMethodTable() => m_pEEType;

        internal ref byte GetRawData() => ref Unsafe.As<RawData>(this).Data;

        internal uint GetRawObjectDataSize()
            => m_pEEType->BaseSize - (uint)sizeof(ObjHeader) - (uint)sizeof(MethodTable*);
    }
}

namespace System.Runtime.CompilerServices
{
    // Used by the compiler (unboxing thunks) to address the first field of an object.
    [StructLayout(LayoutKind.Sequential)]
    internal class RawData
    {
        public byte Data;
    }
}
