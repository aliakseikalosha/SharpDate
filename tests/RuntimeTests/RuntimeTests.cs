// Runtime and CoreLib tests. Runs inside tests/MockHost (no Simulator needed) or the Simulator.
// Every check logs "FAIL ..." on failure; the run ends with a summary line.

using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RuntimeTests;

public sealed class TestGame : Game
{
    private static int s_passed;
    private static int s_failed;
    private int _frame;
    private static Node s_keep;
    private static GCHandle s_weak;

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new TestGame());

    public static void Check(bool condition, string name)
    {
        if (condition)
        {
            s_passed++;
        }
        else
        {
            s_failed++;
            Sys.Log("FAIL " + name);
        }
    }

    public static void Equal<T>(T actual, T expected, string name)
    {
        bool ok = actual is null ? expected is null : actual.Equals(expected);
        if (ok)
            s_passed++;
        else
        {
            s_failed++;
            Sys.Log($"FAIL {name}: expected <{expected}> got <{actual}>");
        }
    }

    protected override void Initialize()
    {
        Sys.Log("SharpDate runtime tests");
        Sys.Log("- ArithmeticTests");
        ArithmeticTests.Run();
        Sys.Log("- StringTests");
        StringTests.Run();
        Sys.Log("- CollectionTests");
        CollectionTests.Run();
        Sys.Log("- TypeSystemTests");
        TypeSystemTests.Run();
        Sys.Log("- DelegateTests");
        DelegateTests.Run();
        Sys.Log("- StaticTests");
        StaticTests.Run();
        Sys.Log("- SpanTests");
        SpanTests.Run();
        Sys.Log("- GcTests");
        GcTests.Run();

        // State checked again after several frames (and collections) in Update.
        for (int i = 0; i < 500; i++)
            s_keep = new Node { Value = i, Next = s_keep, Name = "node" + i };
        s_weak = GCHandle.Alloc(new Node { Value = -1 }, GCHandleType.Weak);
    }

    public override bool Update()
    {
        // Churn the heap every frame.
        for (int i = 0; i < 5000; i++)
            _ = new Node { Value = i, Name = i.ToString() };

        if (++_frame == 5)
        {
            GC.Collect();
            int count = 0, sum = 0;
            bool namesOk = true;
            for (Node n = s_keep; n != null; n = n.Next)
            {
                count++;
                sum += n.Value;
                namesOk &= n.Name == "node" + n.Value;
            }
            Equal(count, 500, "linked list survives collections");
            Equal(sum, 499 * 500 / 2, "linked list contents");
            Check(namesOk, "strings referenced from objects survive");
            Check(s_weak.Target == null, "weak handle cleared after its target died");
            Check(GC.CollectionCount(0) > 0, "collections happened");
            Sys.Log($"RESULT: {s_passed} passed, {s_failed} failed (gcs={GC.CollectionCount(0)}, live={GC.GetTotalMemory(false) / 1024} KB)");
        }
        return true;
    }
}

public sealed class Node
{
    public int Value;
    public string Name;
    public Node Next;
}

internal static class ArithmeticTests
{
    public static void Run()
    {
        int a = -7, b = 2;
        TestGame.Equal(a / b, -3, "int division truncates");
        TestGame.Equal(a % b, -1, "int remainder sign");
        uint ua = 4000000000u, ub = 7;
        TestGame.Equal(ua / ub, 571428571u, "uint division");
        long la = -9000000000000L, lb = 7000;
        TestGame.Equal(la / lb, -1285714285L, "long division");
        TestGame.Equal(la % lb, -5000L, "long remainder");
        ulong ula = 18000000000000000000UL;
        TestGame.Equal(ula / 3, 6000000000000000000UL, "ulong division");
        TestGame.Equal(ula % 7, 18000000000000000000UL % 7, "ulong remainder");
        long big = 123456789012345L;
        TestGame.Equal(big * 1000, 123456789012345000L, "long multiply");
        int shift = 37;
        TestGame.Equal(1L << shift, 137438953472L, "variable long shift");
        TestGame.Equal(-1024L >> (shift - 30), -8L, "variable arithmetic shift");
        TestGame.Equal(ulong.MaxValue >> (shift - 30), 144115188075855871UL, "variable logical shift");

        float f = 2.5f;
        TestGame.Equal((int)(f * 3), 7, "float to int");
        TestGame.Equal((float)big, 1.23456789e14f, "long to float");
        TestGame.Check(MathF.Abs(MathF.Sin(MathF.PI / 2) - 1) < 1e-6f, "MathF.Sin");
        TestGame.Check(MathF.Abs(MathF.Sqrt(2) - 1.4142135f) < 1e-6f, "MathF.Sqrt");
        TestGame.Equal(MathF.Floor(-1.5f), -2f, "MathF.Floor");
        TestGame.Equal(Math.Max(3, 9), 9, "Math.Max");
        TestGame.Equal(Math.Clamp(15, 0, 10), 10, "Math.Clamp");
        TestGame.Equal(System.Numerics.BitOperations.PopCount(0xF0F0u), 8, "PopCount");

        TestGame.Equal(int.Parse("-1234"), -1234, "int.Parse");
        TestGame.Check(!int.TryParse("12x", out _), "int.TryParse rejects garbage");
        TestGame.Equal(float.Parse("3.25"), 3.25f, "float.Parse");
    }
}

internal static class StringTests
{
    public static void Run()
    {
        TestGame.Equal(12345.ToString(), "12345", "int.ToString");
        TestGame.Equal((-42).ToString(), "-42", "negative int.ToString");
        TestGame.Equal(long.MinValue.ToString(), "-9223372036854775808", "long.MinValue.ToString");
        TestGame.Equal(ulong.MaxValue.ToString(), "18446744073709551615", "ulong.MaxValue.ToString");
        TestGame.Equal(3.25f.ToString(), "3.25", "float.ToString");
        TestGame.Equal((-0.5f).ToString(), "-0.5", "negative float.ToString");
        TestGame.Equal(1.0f.ToString("F2"), "1.00", "float F2");
        TestGame.Equal(255.ToString("X4"), "00FF", "int X4");
        TestGame.Equal(1234567.ToString("N0"), "1,234,567", "int N0");

        int score = 42;
        float time = 1.5f;
        TestGame.Equal($"score={score} time={time:F1} name={"pd"}", "score=42 time=1.5 name=pd", "interpolation");
        TestGame.Equal(string.Format("{0}-{1}", "a", 7), "a-7", "string.Format");
        TestGame.Equal("Hello" + ", " + "World", "Hello, World", "concat");
        TestGame.Equal("abcdef".Substring(2, 3), "cde", "Substring");
        TestGame.Equal("a,b,,c".Split(',').Length, 4, "Split");
        TestGame.Equal("hello world".Replace("o", "0"), "hell0 w0rld", "Replace");
        TestGame.Equal("  trim me ".Trim(), "trim me", "Trim");
        TestGame.Equal("MiXeD".ToUpper(), "MIXED", "ToUpper");
        TestGame.Equal("haystack".IndexOf("st"), 3, "IndexOf");
        TestGame.Check("abc" == new string(new[] { 'a', 'b', 'c' }), "string equality");
        TestGame.Check(string.CompareOrdinal("apple", "banana") < 0, "CompareOrdinal");
        TestGame.Equal(string.Join("|", "x", "y", "z"), "x|y|z", "Join");

        string unicode = "Žluťoučký kůň 🐎";
        byte[] utf8 = Encoding.UTF8.GetBytes(unicode);
        TestGame.Equal(utf8.Length, 24, "UTF-8 byte count");
        TestGame.Equal(Encoding.UTF8.GetString(utf8), unicode, "UTF-8 round trip");

        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
            sb.Append(i % 10);
        TestGame.Equal(sb.Length, 100, "StringBuilder length");
        TestGame.Equal(sb.ToString(95, 5), "56789", "StringBuilder content");

        string key = sb.Length > 50 ? "two" : "one";
        switch (key)
        {
            case "one": TestGame.Check(false, "string switch"); break;
            case "two": TestGame.Check(true, "string switch"); break;
            default: TestGame.Check(false, "string switch"); break;
        }
    }
}

internal static class CollectionTests
{
    private static IEnumerable<int> Squares(int n)
    {
        for (int i = 1; i <= n; i++)
            yield return i * i;
    }

    public static void Run()
    {
        var list = new List<int> { 5, 3, 9, 1 };
        list.Sort();
        TestGame.Equal(string.Join(",", list[0].ToString(), list[1].ToString(), list[2].ToString(), list[3].ToString()), "1,3,5,9", "List.Sort");
        list.RemoveAt(0);
        list.Insert(0, 42);
        TestGame.Equal(list[0], 42, "List.Insert");
        TestGame.Check(list.Contains(9), "List.Contains");
        TestGame.Equal(list.FindIndex(x => x > 5), 0, "List.FindIndex with lambda");

        var dict = new Dictionary<string, int>();
        for (int i = 0; i < 1000; i++)
            dict["k" + i] = i;
        for (int i = 0; i < 1000; i += 2)
            dict.Remove("k" + i);
        TestGame.Equal(dict.Count, 500, "Dictionary count after removals");
        TestGame.Check(dict.TryGetValue("k999", out int v) && v == 999, "Dictionary lookup");
        TestGame.Check(!dict.ContainsKey("k500"), "Dictionary removed key");
        dict["new"] = 1; // reuses a free entry
        int sum = 0;
        foreach (KeyValuePair<string, int> kv in dict)
            sum += kv.Value;
        TestGame.Equal(sum, 250000 + 1, "Dictionary enumeration");

        var intKeys = new Dictionary<int, string> { [3] = "three", [-1] = "minus one" };
        TestGame.Equal(intKeys[-1], "minus one", "Dictionary with int keys");

        var set = new HashSet<string> { "a", "b", "a" };
        TestGame.Equal(set.Count, 2, "HashSet dedup");

        var queue = new Queue<int>();
        for (int i = 0; i < 10; i++) queue.Enqueue(i);
        for (int i = 0; i < 5; i++) queue.Dequeue();
        for (int i = 10; i < 20; i++) queue.Enqueue(i);
        int qsum = 0;
        foreach (int q in queue) qsum += q; // yield-based enumerator through IEnumerable<T>
        TestGame.Equal(qsum, 5 + 6 + 7 + 8 + 9 + 145, "Queue wraparound + enumeration");

        var stack = new Stack<string>();
        stack.Push("a"); stack.Push("b");
        TestGame.Equal(stack.Pop(), "b", "Stack LIFO");

        int total = 0;
        foreach (int s in Squares(10))
            total += s;
        TestGame.Equal(total, 385, "yield return iterator");

        IEnumerable nonGeneric = new List<string> { "x", "y" };
        int n = 0;
        foreach (object o in nonGeneric)
            n += ((string)o).Length;
        TestGame.Equal(n, 2, "non-generic IEnumerable");

        int[] arr = { 4, 2, 8, 6 };
        Array.Sort(arr);
        TestGame.Equal(arr[3], 8, "Array.Sort");
        Array.Resize(ref arr, 6);
        TestGame.Equal(arr.Length, 6, "Array.Resize");
        TestGame.Equal(Array.IndexOf(arr, 6), 2, "Array.IndexOf");

        int[,] grid = new int[3, 4];
        grid[2, 3] = 7;
        TestGame.Equal(grid[2, 3], 7, "multidimensional array");
    }
}

internal interface IShape
{
    float Area();
    string Describe() => "shape with area " + Area(); // default interface method
}

internal sealed class Rect : IShape
{
    public float W, H;
    public float Area() => W * H;
}

internal sealed class Circle : IShape
{
    public float R;
    public float Area() => 3 * R * R;
    public string Describe() => "circle";
}

internal struct Counter : IShape
{
    public int Count;
    public float Area() => ++Count;
}

internal abstract class Animal
{
    public abstract string Sound();
    public virtual string Name => "animal";
    public T Echo<T>(T value) => value;
    // Note: generic *virtual* methods are not supported by the SharpDate runtime.
}

internal class Dog : Animal
{
    public override string Sound() => "woof";
    public override string Name => "dog";
}

internal sealed class Puppy : Dog
{
    public override string Sound() => "yip";
}

internal enum Color { Red, Green = 5, Blue }

[Flags]
internal enum Access { None = 0, Read = 1, Write = 2 }

internal struct Vec { public int X, Y; }

internal static class TypeSystemTests
{
    private static T Max<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) >= 0 ? a : b;

    public static void Run()
    {
        var shapes = new List<IShape> { new Rect { W = 2, H = 3 }, new Circle { R = 1 } };
        float area = 0;
        foreach (IShape s in shapes)
            area += s.Area();
        TestGame.Equal(area, 9f, "interface dispatch");
        TestGame.Equal(shapes[0].Describe(), "shape with area 6", "default interface method");
        TestGame.Equal(shapes[1].Describe(), "circle", "overridden default interface method");

        IShape boxed = new Counter();
        boxed.Area();
        boxed.Area();
        TestGame.Equal(boxed.Area(), 3f, "interface call on boxed struct");

        Animal pet = new Puppy();
        TestGame.Equal(pet.Sound(), "yip", "virtual override");
        TestGame.Equal(pet.Name, "dog", "inherited virtual property");
        TestGame.Equal(pet.Echo(5), 5, "generic method");

        object o = pet;
        TestGame.Check(o is Dog, "is base class");
        TestGame.Check(o is not Rect, "is unrelated class");
        TestGame.Check(o as IShape == null, "as interface (not implemented)");
        TestGame.Check(shapes[0] is Rect r && r.W == 2, "pattern matching");

        IEnumerable<string> strings = new List<string> { "a" };
        IEnumerable<object> objects = strings; // covariance
        TestGame.Check(objects is IEnumerable<object>, "generic covariance");

        object[] covariant = new string[] { "x" };
        TestGame.Check(covariant[0] is string, "array covariance");

        object boxedInt = 42;
        TestGame.Equal((int)boxedInt, 42, "unboxing");
        object boxedColor = Color.Blue;
        TestGame.Equal((Color)boxedColor, Color.Blue, "enum unboxing");
        int? maybe = 5;
        object boxedNullable = maybe;
        TestGame.Equal((int)boxedNullable, 5, "nullable boxing");
        int? none = null;
        TestGame.Check((object)none == null, "null nullable boxes to null");

        TestGame.Equal(Color.Blue.ToString(), "6", "enum ToString (numeric, no metadata)");
        TestGame.Check((Access.Read | Access.Write).HasFlag(Access.Write), "Enum.HasFlag");

        TestGame.Check(new Vec { X = 1, Y = 2 }.Equals(new Vec { X = 1, Y = 2 }), "ValueType.Equals");
        TestGame.Equal(Max(3, 8), 8, "generic constraint");
        TestGame.Equal(Max("pear", "apple"), "pear", "generic constraint (string)");

        TestGame.Check(typeof(Dog) == pet.GetType().BaseType, "typeof / GetType / BaseType");
        TestGame.Check(typeof(IShape).IsAssignableFrom(typeof(Rect)), "Type.IsAssignableFrom");

        (int, string) tuple = (1, "one");
        var (num, word) = tuple;
        TestGame.Equal(num + word.Length, 4, "tuples");
    }
}

internal static class DelegateTests
{
    private static int s_calls;
    private static void Increment() => s_calls++;

    public static void Run()
    {
        int captured = 10;
        Func<int, int> addCaptured = x => x + captured;
        captured = 20;
        TestGame.Equal(addCaptured(1), 21, "closure captures variable");

        Action multi = Increment;
        multi += Increment;
        multi += () => s_calls += 100;
        multi();
        TestGame.Equal(s_calls, 102, "multicast delegate");
        multi -= Increment;
        s_calls = 0;
        multi();
        TestGame.Equal(s_calls, 101, "delegate removal");

        Func<string> instance = "abc".ToUpper;
        TestGame.Equal(instance(), "ABC", "delegate to instance method");

        Comparison<int> desc = static (a, b) => b - a;
        var list = new List<int> { 1, 3, 2 };
        list.Sort(desc);
        TestGame.Equal(list[0], 3, "static lambda comparison");

        EventSource source = new EventSource();
        int received = 0;
        source.Changed += v => received += v;
        source.Changed += v => received += v * 10;
        source.Raise(2);
        TestGame.Equal(received, 22, "events");
    }

    private sealed class EventSource
    {
        public event Action<int> Changed;
        public void Raise(int v) => Changed?.Invoke(v);
    }
}

internal static class Lazy
{
    public static readonly List<string> Log = new List<string>();
}

internal static class First
{
    public static readonly int Value;

    static First()
    {
        Value = Second.Value + 1;
        Lazy.Log.Add("First");
    }
}

internal static class Second
{
    public static readonly int Value = Compute();

    private static int Compute()
    {
        Lazy.Log.Add("Second");
        return 41;
    }
}

internal static class StaticTests
{
    private static readonly int[] s_table = { 1, 2, 3 };
    private static int s_counter;

    public static void Run()
    {
        TestGame.Equal(First.Value, 42, "static constructor ordering");
        TestGame.Equal(Lazy.Log.Count, 2, "static constructors ran once each");
        s_table[1] = 20; // preinitialized arrays are writable
        TestGame.Equal(s_table[1], 20, "write to preinitialized static array");
        s_counter++;
        TestGame.Equal(s_counter, 1, "mutable static");
    }
}

internal static unsafe class SpanTests
{
    public static void Run()
    {
        Span<int> stack = stackalloc int[16];
        for (int i = 0; i < stack.Length; i++)
            stack[i] = i * 2;
        int sum = 0;
        foreach (int v in stack.Slice(4, 4))
            sum += v;
        TestGame.Equal(sum, 8 + 10 + 12 + 14, "stackalloc span slice");

        byte[] bytes = { 1, 2, 3, 4 };
        fixed (byte* p = bytes)
            TestGame.Equal(*(uint*)p, 0x04030201u, "fixed array (little endian)");

        ReadOnlySpan<byte> utf8 = "hello"u8;
        TestGame.Equal(utf8.Length, 5, "UTF-8 literal");
        TestGame.Equal(utf8[4], (byte)'o', "UTF-8 literal content");

        char* chars = stackalloc char[3] { 'p', 'd', '!' };
        TestGame.Equal(new string(chars, 0, 3), "pd!", "string from char*");

        Span<int> heap = new int[8];
        heap.Fill(7);
        TestGame.Equal(heap.ToArray()[7], 7, "Span.Fill/ToArray");

        void* native = NativeMemory.Alloc(64);
        new Span<byte>(native, 64).Clear();
        TestGame.Equal(((byte*)native)[63], (byte)0, "NativeMemory");
        NativeMemory.Free(native);
    }
}

internal static class GcTests
{
    private sealed class Holder
    {
        public object[] Items;
        public Vec[] Structs;
        public Pair[] Pairs;
    }

    private struct Pair
    {
        public string Name;
        public int Number;
    }

    public static void Run()
    {
        // Locals must survive a collection triggered while they're only on the stack.
        var holder = new Holder { Items = new object[100], Structs = new Vec[10], Pairs = new Pair[50] };
        for (int i = 0; i < holder.Items.Length; i++)
            holder.Items[i] = "item" + i;
        for (int i = 0; i < holder.Pairs.Length; i++)
            holder.Pairs[i] = new Pair { Name = "pair" + i, Number = i };

        long before = GC.GetTotalMemory(false);
        for (int i = 0; i < 100000; i++)
            _ = new byte[64];
        GC.Collect();
        long after = GC.GetTotalMemory(false);

        bool ok = true;
        for (int i = 0; i < holder.Items.Length; i++)
            ok &= (string)holder.Items[i] == "item" + i;
        TestGame.Check(ok, "object[] survives while referenced from a local");

        ok = true;
        for (int i = 0; i < holder.Pairs.Length; i++)
            ok &= holder.Pairs[i].Name == "pair" + i && holder.Pairs[i].Number == i;
        TestGame.Check(ok, "struct array with references survives");
        TestGame.Check(after < before + 1024 * 1024, "garbage is reclaimed");

        // Interior pointer: a ref into an array keeps the array alive.
        ref Pair middle = ref GetMiddle();
        GC.Collect();
        for (int i = 0; i < 1000; i++)
            _ = new Pair[4];
        TestGame.Equal(middle.Name, "interior", "interior reference keeps array alive");

        var pinned = GCHandle.Alloc(new Node { Value = 99 });
        GC.Collect();
        TestGame.Equal(((Node)pinned.Target).Value, 99, "strong GCHandle");
        pinned.Free();
    }

    private static ref Pair GetMiddle()
    {
        var array = new Pair[9];
        array[4].Name = "interior";
        return ref array[4];
    }
}
