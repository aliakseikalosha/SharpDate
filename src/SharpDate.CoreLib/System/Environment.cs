using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    public static unsafe class Environment
    {
        public static string NewLine => "\n";

        public static int TickCount => (int)RuntimeImports.sd_ticks_ms();

        public static long TickCount64 => RuntimeImports.sd_ticks_ms();

        public static int ProcessorCount => 1;

        public static bool Is64BitProcess => sizeof(nint) == 8;

        public static int CurrentManagedThreadId => 1;

        /// <summary>Reports <paramref name="message"/> through the Playdate error API and stops the game.</summary>
        [DoesNotReturn]
        public static void FailFast(string message)
        {
            if (message is null)
            {
                RuntimeImports.sd_fail_fast(null, 0);
            }
            else
            {
                fixed (char* p = message)
                    RuntimeImports.sd_fail_fast(p, message.Length);
            }
            while (true) { }
        }

        [DoesNotReturn]
        public static void FailFast(string message, Exception exception) => FailFast(message);
    }

    /// <summary>
    /// Controls the SharpDate garbage collector: a non-moving mark-and-sweep collector that runs when
    /// allocation pressure builds up or when <see cref="Collect()"/> is called. Objects referenced only
    /// from native memory (e.g. Playdate userdata pointers) must be kept alive with a GCHandle or a static.
    /// </summary>
    public static class GC
    {
        public static void Collect() => RuntimeImports.sd_gc_collect();
        public static void Collect(int generation) => RuntimeImports.sd_gc_collect();
        public static long GetTotalMemory(bool forceFullCollection)
        {
            if (forceFullCollection)
                RuntimeImports.sd_gc_collect();
            return (long)RuntimeImports.sd_gc_total_bytes();
        }
        public static int CollectionCount(int generation) => RuntimeImports.sd_gc_collection_count();
        public static int MaxGeneration => 0;
        public static void KeepAlive(object obj) { }
        public static void SuppressFinalize(object obj) { }
        public static void ReRegisterForFinalize(object obj) { }
        public static void WaitForPendingFinalizers() { }
        public static void AddMemoryPressure(long bytesAllocated) { }
        public static void RemoveMemoryPressure(long bytesAllocated) { }
    }

    /// <summary>Writes to the Playdate console (pd->system->logToConsole).</summary>
    public static unsafe class Console
    {
        private static Text.StringBuilder s_line;

        public static void WriteLine() => Flush(string.Empty);

        public static void WriteLine(string value)
        {
            if (s_line != null && s_line.Length > 0)
            {
                s_line.Append(value);
                value = s_line.ToString();
                s_line.Clear();
            }
            Flush(value ?? string.Empty);
        }

        public static void WriteLine(object value) => WriteLine(value?.ToString());
        public static void WriteLine(int value) => WriteLine(Number.Int32ToString(value));
        public static void WriteLine(uint value) => WriteLine(Number.UInt32ToString(value));
        public static void WriteLine(long value) => WriteLine(Number.Int64ToString(value));
        public static void WriteLine(float value) => WriteLine(Number.SingleToString(value, 4));
        public static void WriteLine(bool value) => WriteLine(value ? "True" : "False");
        public static void WriteLine(char value) => WriteLine(string.FromChar(value));
        public static void WriteLine(string format, object arg0) => WriteLine(string.Format(format, arg0));
        public static void WriteLine(string format, object arg0, object arg1) => WriteLine(string.Format(format, arg0, arg1));
        public static void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));

        public static void Write(string value)
        {
            s_line ??= new Text.StringBuilder();
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                {
                    s_line.Append(value, start, i - start);
                    Flush(s_line.ToString());
                    s_line.Clear();
                    start = i + 1;
                }
            }
            s_line.Append(value, start, value.Length - start);
        }

        public static void Write(object value) => Write(value?.ToString() ?? string.Empty);
        public static void Write(int value) => Write(Number.Int32ToString(value));
        public static void Write(float value) => Write(Number.SingleToString(value, 4));
        public static void Write(char value) => Write(string.FromChar(value));

        private static void Flush(string line)
        {
            int byteCount = Text.Encoding.UTF8.GetByteCount(line);
            byte* buffer = (byte*)Runtime.InteropServices.NativeMemory.Alloc((nuint)byteCount + 1);
            Text.Encoding.UTF8.GetBytes(line, new Span<byte>(buffer, byteCount));
            buffer[byteCount] = 0;
            RuntimeImports.sd_log(buffer, byteCount);
            Runtime.InteropServices.NativeMemory.Free(buffer);
        }
    }

    /// <summary>Pseudo-random numbers (xorshift128+; deterministic for a given seed).</summary>
    public class Random
    {
        private ulong _s0;
        private ulong _s1;

        public static Random Shared { get; } = new Random();

        public Random() : this((int)RuntimeImports.sd_ticks_ms() ^ 0x5DEECE6) { }

        public Random(int seed)
        {
            ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            _s0 = SplitMix(ref z);
            _s1 = SplitMix(ref z);
        }

        private static ulong SplitMix(ref ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            ulong r = z;
            r = (r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL;
            r = (r ^ (r >> 27)) * 0x94D049BB133111EBUL;
            return r ^ (r >> 31);
        }

        private uint NextUInt32()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return (uint)((_s1 + s0) >> 32);
        }

        public virtual int Next() => (int)(NextUInt32() >> 1);

        public virtual int Next(int maxValue)
        {
            if (maxValue < 0)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            return (int)(((ulong)NextUInt32() * (uint)maxValue) >> 32);
        }

        public virtual int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            return minValue + (int)(((ulong)NextUInt32() * (uint)(maxValue - minValue)) >> 32);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public virtual float NextSingle() => (NextUInt32() >> 8) * (1.0f / 16777216.0f);

        public virtual double NextDouble() => NextSingle();

        public virtual void NextBytes(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)NextUInt32();
        }
    }
}

namespace System.Diagnostics
{
    public static class Debug
    {
        [Conditional("DEBUG")]
        public static void Assert([DoesNotReturnIf(false)] bool condition)
        {
            if (!condition)
                Environment.FailFast("Assertion failed.");
        }

        [Conditional("DEBUG")]
        public static void Assert([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
                Environment.FailFast(message);
        }

        [Conditional("DEBUG")]
        public static void Fail(string message) => Environment.FailFast(message);

        [Conditional("DEBUG")]
        public static void WriteLine(string message) => Console.WriteLine(message);

        [Conditional("DEBUG")]
        public static void WriteLine(object value) => Console.WriteLine(value);
    }

    public class Stopwatch
    {
        private uint _start;
        private uint _elapsed;
        private bool _running;

        public static long Frequency => 1000;
        public static bool IsHighResolution => false;

        public static Stopwatch StartNew()
        {
            var sw = new Stopwatch();
            sw.Start();
            return sw;
        }

        public static long GetTimestamp() => Runtime.RuntimeImports.sd_ticks_ms();

        public void Start()
        {
            if (!_running)
            {
                _start = Runtime.RuntimeImports.sd_ticks_ms();
                _running = true;
            }
        }

        public void Stop()
        {
            if (_running)
            {
                _elapsed += Runtime.RuntimeImports.sd_ticks_ms() - _start;
                _running = false;
            }
        }

        public void Reset()
        {
            _elapsed = 0;
            _running = false;
        }

        public void Restart()
        {
            Reset();
            Start();
        }

        public bool IsRunning => _running;

        public long ElapsedMilliseconds => _elapsed + (_running ? Runtime.RuntimeImports.sd_ticks_ms() - _start : 0);

        public long ElapsedTicks => ElapsedMilliseconds;
    }
}

namespace System.Threading
{
    // Managed code runs on a single thread on Playdate, so these are plain memory operations.
    public static class Interlocked
    {
        public static int Increment(ref int location) => ++location;
        public static long Increment(ref long location) => ++location;
        public static int Decrement(ref int location) => --location;
        public static long Decrement(ref long location) => --location;
        public static int Add(ref int location, int value) => location += value;
        public static long Add(ref long location, long value) => location += value;

        public static int Exchange(ref int location, int value)
        {
            int old = location;
            location = value;
            return old;
        }

        public static T Exchange<T>(ref T location, T value) where T : class
        {
            T old = location;
            location = value;
            return old;
        }

        public static int CompareExchange(ref int location, int value, int comparand)
        {
            int old = location;
            if (old == comparand)
                location = value;
            return old;
        }

        public static nint CompareExchange(ref nint location, nint value, nint comparand)
        {
            nint old = location;
            if (old == comparand)
                location = value;
            return old;
        }

        public static object CompareExchange(ref object location, object value, object comparand)
        {
            object old = location;
            if (ReferenceEquals(old, comparand))
                location = value;
            return old;
        }

        public static T CompareExchange<T>(ref T location, T value, T comparand) where T : class
        {
            T old = location;
            if (ReferenceEquals(old, comparand))
                location = value;
            return old;
        }

        public static void MemoryBarrier() { }
    }

    public static class Volatile
    {
        public static int Read(ref readonly int location) => location;
        public static bool Read(ref readonly bool location) => location;
        public static T Read<T>(ref readonly T location) where T : class => location;
        public static void Write(ref int location, int value) => location = value;
        public static void Write(ref bool location, bool value) => location = value;
        public static void Write<T>(ref T location, T value) where T : class => location = value;
    }

    public static class Monitor
    {
        public static void Enter(object obj) { }
        public static void Enter(object obj, ref bool lockTaken) => lockTaken = true;
        public static void Exit(object obj) { }
    }

    public sealed class Lock
    {
        public void Enter() { }
        public void Exit() { }
        public Scope EnterScope() => default;

        public ref struct Scope
        {
            public void Dispose() { }
        }
    }
}
