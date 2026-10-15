// Delegate layout and initialization methods are a contract with ILC
// (see dotnet/runtime src/coreclr/nativeaot/System.Private.CoreLib/src/System/Delegate.cs, MIT).

using System.Runtime.CompilerServices;

namespace System
{
    public abstract class Delegate
    {
        // Field names are looked up by the compiler.
        private object _firstParameter;
        private object _helperObject;
        private nint _extraFunctionPointerOrData;
        private IntPtr _functionPointer;

        // Element of the invocation list of multicast delegates (looked up by the compiler).
        private struct Wrapper
        {
            public Wrapper(Delegate value) => Value = value;
            public Delegate Value;
        }

        private protected const int MulticastThunk = 0;
        private protected const int ClosedStaticThunk = 1;
        private protected const int OpenStaticThunk = 2;
        private protected const int ClosedInstanceThunkOverGenericMethod = 3;
        private protected const int OpenInstanceThunk = 4;
        private protected const int ObjectArrayThunk = 5;

        // Overridden by the compiler for every delegate type.
        private protected virtual IntPtr GetThunk(int whichThunk) => IntPtr.Zero;

        private void InitializeClosedInstance(object firstParameter, IntPtr functionPointer)
        {
            if (firstParameter is null)
                ThrowHelpers.ThrowArgumentException();
            _functionPointer = functionPointer;
            _firstParameter = firstParameter;
        }

        private void InitializeClosedInstanceSlow(object firstParameter, IntPtr functionPointer)
        {
            if (firstParameter is null)
                ThrowHelpers.ThrowArgumentException();
            if (!FunctionPointerOps.IsGenericMethodPointer(functionPointer))
            {
                _functionPointer = functionPointer;
                _firstParameter = firstParameter;
            }
            else
            {
                _firstParameter = this;
                _functionPointer = GetThunk(ClosedInstanceThunkOverGenericMethod);
                _extraFunctionPointerOrData = functionPointer;
                _helperObject = firstParameter;
            }
        }

        private void InitializeClosedInstanceWithoutNullCheck(object firstParameter, IntPtr functionPointer)
        {
            if (!FunctionPointerOps.IsGenericMethodPointer(functionPointer))
            {
                _functionPointer = functionPointer;
                _firstParameter = firstParameter;
            }
            else
            {
                _firstParameter = this;
                _functionPointer = GetThunk(ClosedInstanceThunkOverGenericMethod);
                _extraFunctionPointerOrData = functionPointer;
                _helperObject = firstParameter;
            }
        }

        private void InitializeClosedStaticThunk(object firstParameter, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            _extraFunctionPointerOrData = functionPointer;
            _helperObject = firstParameter;
            _functionPointer = functionPointerThunk;
            _firstParameter = this;
        }

        private void InitializeOpenStaticThunk(object _, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            _firstParameter = this;
            _functionPointer = functionPointerThunk;
            _extraFunctionPointerOrData = functionPointer;
        }

        private IntPtr GetActualTargetFunctionPointer(object thisObject)
        {
            Environment.FailFast("Open instance delegates are not supported by SharpDate.");
            return IntPtr.Zero;
        }

        public object Target
        {
            get
            {
                if (_helperObject is Wrapper[] list)
                    return list[(int)_extraFunctionPointerOrData - 1].Value.Target;
                if (ReferenceEquals(_firstParameter, this))
                    return _helperObject; // static thunk: the closed-over first argument (or null)
                return _firstParameter;
            }
        }

        public static Delegate Combine(Delegate a, Delegate b)
        {
            if (a is null)
                return b;
            if (b is null)
                return a;
            return a.CombineImpl(b);
        }

        public static Delegate Combine(params Delegate[] delegates)
        {
            Delegate result = null;
            for (int i = 0; i < delegates.Length; i++)
                result = Combine(result, delegates[i]);
            return result;
        }

        public static Delegate Remove(Delegate source, Delegate value)
        {
            if (source is null)
                return null;
            if (value is null)
                return source;
            return source.RemoveImpl(value);
        }

        public static Delegate RemoveAll(Delegate source, Delegate value)
        {
            Delegate newDelegate;
            do
            {
                newDelegate = source;
                source = Remove(source, value);
            }
            while (!ReferenceEquals(newDelegate, source));
            return newDelegate;
        }

        private int InvocationCount => _helperObject is Wrapper[] ? (int)_extraFunctionPointerOrData : 1;

        private Delegate GetInvocationItem(int index)
            => _helperObject is Wrapper[] list ? list[index].Value : this;

        public Delegate[] GetInvocationList()
        {
            int count = InvocationCount;
            Delegate[] result = new Delegate[count];
            for (int i = 0; i < count; i++)
                result[i] = GetInvocationItem(i);
            return result;
        }

        protected virtual Delegate CombineImpl(Delegate d)
        {
            int countA = InvocationCount;
            int countB = d.InvocationCount;
            Wrapper[] list = new Wrapper[countA + countB];
            for (int i = 0; i < countA; i++)
                list[i] = new Wrapper(GetInvocationItem(i));
            for (int i = 0; i < countB; i++)
                list[countA + i] = new Wrapper(d.GetInvocationItem(i));
            return NewMulticastDelegate(list, countA + countB);
        }

        protected virtual Delegate RemoveImpl(Delegate d)
        {
            int count = InvocationCount;
            int removeCount = d.InvocationCount;
            // Find the last occurrence of d's invocation list as a contiguous run.
            for (int start = count - removeCount; start >= 0; start--)
            {
                bool match = true;
                for (int j = 0; j < removeCount; j++)
                {
                    if (!GetInvocationItem(start + j).EqualsSingle(d.GetInvocationItem(j)))
                    {
                        match = false;
                        break;
                    }
                }
                if (!match)
                    continue;

                int remaining = count - removeCount;
                if (remaining == 0)
                    return null;
                if (remaining == 1)
                    return GetInvocationItem(start == 0 ? count - 1 : 0);
                Wrapper[] list = new Wrapper[remaining];
                int n = 0;
                for (int i = 0; i < count; i++)
                    if (i < start || i >= start + removeCount)
                        list[n++] = new Wrapper(GetInvocationItem(i));
                return NewMulticastDelegate(list, remaining);
            }
            return this;
        }

        private Delegate NewMulticastDelegate(Wrapper[] invocationList, int invocationCount)
        {
            Delegate result = Unsafe.As<Delegate>(MemberwiseClone());
            result._firstParameter = result;
            result._functionPointer = GetThunk(MulticastThunk);
            result._helperObject = invocationList;
            result._extraFunctionPointerOrData = invocationCount;
            return result;
        }

        private bool EqualsSingle(Delegate other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null || GetMethodTableRaw() != other.GetMethodTableRaw())
                return false;
            bool thisIsThunk = ReferenceEquals(_firstParameter, this);
            bool otherIsThunk = ReferenceEquals(other._firstParameter, other);
            return ReferenceEquals(thisIsThunk ? _helperObject : _firstParameter, otherIsThunk ? other._helperObject : other._firstParameter)
                && (thisIsThunk ? _extraFunctionPointerOrData : (nint)_functionPointer)
                   == (otherIsThunk ? other._extraFunctionPointerOrData : (nint)other._functionPointer);
        }

        private unsafe IntPtr GetMethodTableRaw() => (IntPtr)GetMethodTable();

        public override bool Equals(object obj)
        {
            if (obj is not Delegate d || InvocationCount != d.InvocationCount)
                return false;
            for (int i = 0; i < InvocationCount; i++)
                if (!GetInvocationItem(i).EqualsSingle(d.GetInvocationItem(i)))
                    return false;
            return true;
        }

        public override int GetHashCode() => (int)(ReferenceEquals(_firstParameter, this) ? _extraFunctionPointerOrData : (nint)_functionPointer);

        public static bool operator ==(Delegate d1, Delegate d2) => ReferenceEquals(d1, null) ? ReferenceEquals(d2, null) : d1.Equals((object)d2);
        public static bool operator !=(Delegate d1, Delegate d2) => !(d1 == d2);
    }

    public abstract class MulticastDelegate : Delegate
    {
        public static bool operator ==(MulticastDelegate d1, MulticastDelegate d2) => (Delegate)d1 == (Delegate)d2;
        public static bool operator !=(MulticastDelegate d1, MulticastDelegate d2) => (Delegate)d1 != (Delegate)d2;
        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
    }

    internal static class FunctionPointerOps
    {
        // Generic method pointers that need an instantiation argument are tagged by the compiler.
        private const nint FatFunctionPointerOffset = 2;

        public static bool IsGenericMethodPointer(IntPtr functionPointer) => ((nint)functionPointer & FatFunctionPointerOffset) != 0;
    }

    public delegate void Action();
    public delegate void Action<in T>(T obj);
    public delegate void Action<in T1, in T2>(T1 arg1, T2 arg2);
    public delegate void Action<in T1, in T2, in T3>(T1 arg1, T2 arg2, T3 arg3);
    public delegate void Action<in T1, in T2, in T3, in T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate void Action<in T1, in T2, in T3, in T4, in T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

    public delegate TResult Func<out TResult>();
    public delegate TResult Func<in T, out TResult>(T arg);
    public delegate TResult Func<in T1, in T2, out TResult>(T1 arg1, T2 arg2);
    public delegate TResult Func<in T1, in T2, in T3, out TResult>(T1 arg1, T2 arg2, T3 arg3);
    public delegate TResult Func<in T1, in T2, in T3, in T4, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate TResult Func<in T1, in T2, in T3, in T4, in T5, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

    public delegate int Comparison<in T>(T x, T y);
    public delegate bool Predicate<in T>(T obj);
    public delegate TOutput Converter<in TInput, out TOutput>(TInput input);
    public delegate void EventHandler(object sender, EventArgs e);
    public delegate void EventHandler<TEventArgs>(object sender, TEventArgs e);

    public class EventArgs
    {
        public static readonly EventArgs Empty = new EventArgs();
    }
}
