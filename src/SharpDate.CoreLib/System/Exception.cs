using System.Diagnostics.CodeAnalysis;

namespace System
{
    // SharpDate's runtime does not unwind: a thrown exception reports its message through the
    // Playdate error API and stops the game (like pd->system->error). `try/finally` and `using`
    // still work for the non-exceptional path.
    public class Exception
    {
        private string _message;
        private Exception _innerException;

        public Exception() { }
        public Exception(string message) { _message = message; }
        public Exception(string message, Exception innerException) { _message = message; _innerException = innerException; }

        public virtual string Message => _message ?? DefaultMessage;
        public Exception InnerException => _innerException;
        public virtual string StackTrace => null;
        public string Source { get; set; }
        public int HResult { get; set; }

        internal virtual string DefaultMessage => "Exception of type 'System.Exception' was thrown.";

        public override string ToString() => Message;
    }

    public class SystemException : Exception
    {
        public SystemException() { }
        public SystemException(string message) : base(message) { }
        public SystemException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ApplicationException : Exception
    {
        public ApplicationException() { }
        public ApplicationException(string message) : base(message) { }
        public ApplicationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ArgumentException : SystemException
    {
        private string _paramName;
        public ArgumentException() { }
        public ArgumentException(string message) : base(message) { }
        public ArgumentException(string message, Exception innerException) : base(message, innerException) { }
        public ArgumentException(string message, string paramName) : base(message) { _paramName = paramName; }
        public virtual string ParamName => _paramName;
        internal override string DefaultMessage => "Value does not fall within the expected range.";
    }

    public class ArgumentNullException : ArgumentException
    {
        public ArgumentNullException() { }
        public ArgumentNullException(string paramName) : base("Value cannot be null.", paramName) { }
        public ArgumentNullException(string paramName, string message) : base(message, paramName) { }

        public static void ThrowIfNull([NotNull] object argument, [Runtime.CompilerServices.CallerArgumentExpression(nameof(argument))] string paramName = null)
        {
            if (argument is null)
                throw new ArgumentNullException(paramName);
        }

        internal override string DefaultMessage => "Value cannot be null.";
    }

    public class ArgumentOutOfRangeException : ArgumentException
    {
        public ArgumentOutOfRangeException() { }
        public ArgumentOutOfRangeException(string paramName) : base("Specified argument was out of the range of valid values.", paramName) { }
        public ArgumentOutOfRangeException(string paramName, string message) : base(message, paramName) { }
        internal override string DefaultMessage => "Specified argument was out of the range of valid values.";
    }

    public class ArithmeticException : SystemException
    {
        public ArithmeticException() { }
        public ArithmeticException(string message) : base(message) { }
        internal override string DefaultMessage => "Overflow or underflow in the arithmetic operation.";
    }

    public class DivideByZeroException : ArithmeticException
    {
        public DivideByZeroException() { }
        public DivideByZeroException(string message) : base(message) { }
        internal override string DefaultMessage => "Attempted to divide by zero.";
    }

    public class OverflowException : ArithmeticException
    {
        public OverflowException() { }
        public OverflowException(string message) : base(message) { }
        internal override string DefaultMessage => "Arithmetic operation resulted in an overflow.";
    }

    public class FormatException : SystemException
    {
        public FormatException() { }
        public FormatException(string message) : base(message) { }
        internal override string DefaultMessage => "One of the identified items was in an invalid format.";
    }

    public class IndexOutOfRangeException : SystemException
    {
        public IndexOutOfRangeException() { }
        public IndexOutOfRangeException(string message) : base(message) { }
        internal override string DefaultMessage => "Index was outside the bounds of the array.";
    }

    public class InvalidCastException : SystemException
    {
        public InvalidCastException() { }
        public InvalidCastException(string message) : base(message) { }
        internal override string DefaultMessage => "Specified cast is not valid.";
    }

    public class InvalidOperationException : SystemException
    {
        public InvalidOperationException() { }
        public InvalidOperationException(string message) : base(message) { }
        public InvalidOperationException(string message, Exception innerException) : base(message, innerException) { }
        internal override string DefaultMessage => "Operation is not valid due to the current state of the object.";
    }

    public class NotImplementedException : SystemException
    {
        public NotImplementedException() { }
        public NotImplementedException(string message) : base(message) { }
        internal override string DefaultMessage => "The method or operation is not implemented.";
    }

    public class NotSupportedException : SystemException
    {
        public NotSupportedException() { }
        public NotSupportedException(string message) : base(message) { }
        internal override string DefaultMessage => "Specified method is not supported.";
    }

    public class PlatformNotSupportedException : NotSupportedException
    {
        public PlatformNotSupportedException() { }
        public PlatformNotSupportedException(string message) : base(message) { }
        internal override string DefaultMessage => "Operation is not supported on this platform.";
    }

    public class NullReferenceException : SystemException
    {
        public NullReferenceException() { }
        public NullReferenceException(string message) : base(message) { }
        internal override string DefaultMessage => "Object reference not set to an instance of an object.";
    }

    public class ArrayTypeMismatchException : SystemException
    {
        public ArrayTypeMismatchException() { }
        public ArrayTypeMismatchException(string message) : base(message) { }
        internal override string DefaultMessage => "Attempted to access an element as a type incompatible with the array.";
    }

    public class OutOfMemoryException : SystemException
    {
        public OutOfMemoryException() { }
        public OutOfMemoryException(string message) : base(message) { }
        internal override string DefaultMessage => "Insufficient memory to continue the execution of the program.";
    }

    public class ObjectDisposedException : InvalidOperationException
    {
        public ObjectDisposedException(string objectName) : base("Cannot access a disposed object: " + objectName) { }
        public ObjectDisposedException(string objectName, string message) : base(message) { }
    }

    public class TypeInitializationException : SystemException
    {
        public TypeInitializationException(string fullTypeName, Exception innerException) : base("The type initializer threw an exception.", innerException) { }
    }
}

namespace System.Collections.Generic
{
    public class KeyNotFoundException : SystemException
    {
        public KeyNotFoundException() : base("The given key was not present in the dictionary.") { }
        public KeyNotFoundException(string message) : base(message) { }
    }
}

namespace System.IO
{
    public class IOException : SystemException
    {
        public IOException() { }
        public IOException(string message) : base(message) { }
    }
}

namespace System
{
    internal static class ThrowHelpers
    {
        [DoesNotReturn] internal static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();
        [DoesNotReturn] internal static void ThrowArgumentOutOfRangeException() => throw new ArgumentOutOfRangeException();
        [DoesNotReturn] internal static void ThrowArgumentException() => throw new ArgumentException();
        [DoesNotReturn] internal static void ThrowArgumentNullException() => throw new ArgumentNullException();
        [DoesNotReturn] internal static void ThrowInvalidOperationException() => throw new InvalidOperationException();
        [DoesNotReturn] internal static void ThrowInvalidOperationException(string message) => throw new InvalidOperationException(message);
        [DoesNotReturn] internal static void ThrowArrayTypeMismatchException() => throw new ArrayTypeMismatchException();
        [DoesNotReturn] internal static void ThrowFormatException() => throw new FormatException();
        [DoesNotReturn] internal static void ThrowKeyNotFoundException() => throw new Collections.Generic.KeyNotFoundException();
        [DoesNotReturn] internal static void ThrowInvalidCastException() => throw new InvalidCastException();
        [DoesNotReturn] internal static void ThrowNullReferenceException() => throw new NullReferenceException();
        [DoesNotReturn] internal static void ThrowOverflowException() => throw new OverflowException();
        [DoesNotReturn] internal static void ThrowDivideByZeroException() => throw new DivideByZeroException();
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    using System;

    // Helpers the compiler calls for implicit runtime checks (names are a contract with ILC).
    internal static class ThrowHelpers
    {
        private static void ThrowOverflowException() => throw new OverflowException();
        private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();
        private static void ThrowNullReferenceException() => throw new NullReferenceException();
        private static void ThrowDivideByZeroException() => throw new DivideByZeroException();
        private static void ThrowArgumentOutOfRangeException() => throw new ArgumentOutOfRangeException();
        private static void ThrowArgumentException() => throw new ArgumentException();
        private static void ThrowPlatformNotSupportedException() => throw new PlatformNotSupportedException();
        private static void ThrowNotImplementedException() => throw new NotImplementedException();
        private static void ThrowTypeLoadException() => throw new NotSupportedException("Type load failure");
        private static void ThrowInvalidProgramException() => throw new NotSupportedException("Invalid program");
        private static void ThrowMissingMethodException() => throw new NotSupportedException("Missing method");
        private static void ThrowMissingFieldException() => throw new NotSupportedException("Missing field");
        private static void ThrowBodyRemoved() => throw new NotSupportedException("Method body removed by the compiler");
        private static void ThrowFeatureBodyRemoved() => throw new NotSupportedException("Feature not available in SharpDate");
        private static void ThrowInstanceBodyRemoved() => throw new NotSupportedException("Method body removed by the compiler");
        private static void ThrowUnavailableType() => throw new NotSupportedException("Type not available");
    }
}
