// Port of the Playdate SDK's C_API/Examples/Array: a custom type that mimics a Lua table,
// exposing functions to Lua and passing data back and forth between C# and Lua.
//
// The C version stores a malloc'd struct in the Lua object. Here the Lua object holds a managed
// IntArray, kept alive by a GC handle until Lua's __gc releases it.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ArrayExample;

public sealed class IntArray
{
    public readonly int[] Data;

    public IntArray(int count) { Data = new int[count]; }
}

public sealed unsafe class ArrayGame : Game
{
    private const string ClassName = "array";

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new ArrayGame());

    protected override bool UsesUpdateCallback => false;

    protected override void InitializeLua()
    {
        var methods = new LuaFunction[]
        {
            new LuaFunction("new", &NewObject),
            new LuaFunction("__gc", &Gc),
            new LuaFunction("__index", &Index),
            new LuaFunction("__newindex", &NewIndex),
            new LuaFunction("__len", &Len),
            new LuaFunction("getMinimum", &GetMinimum),
            new LuaFunction("getMaximum", &GetMaximum),
            new LuaFunction("getAverage", &GetAverage),
        };

        if (!Lua.RegisterClass(ClassName, methods, out string error))
            Sys.Log($"registerClass failed, {error}");
    }

    private static IntArray Arg(int pos) => Lua.GetArgManagedObject<IntArray>(pos, ClassName);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NewObject(void* L)
    {
        int count = Lua.GetArgInt(1);
        if (count <= 0)
            return 0;
        Lua.PushManagedObject(new IntArray(count), ClassName);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Gc(void* L)
    {
        Lua.ReleaseManagedObject(1, ClassName);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Index(void* L)
    {
        // we first need to check if the requested member is defined in the
        // metatable, otherwise we can't call other functions in this class
        if (Lua.IndexMetatable())
            return 1;

        IntArray array = Arg(1);
        int idx = Lua.GetArgInt(2);

        if (array != null && idx > 0 && idx <= array.Data.Length)
        {
            Lua.PushInt(array.Data[idx - 1]);
            return 1;
        }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int NewIndex(void* L)
    {
        IntArray array = Arg(1);
        int idx = Lua.GetArgInt(2);

        if (Lua.GetArgType(3) != LuaType.Int)
        {
            Sys.Error("array only accepts integer types");
            return 0;
        }

        if (array != null && idx > 0 && idx <= array.Data.Length)
            array.Data[idx - 1] = Lua.GetArgInt(3);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Len(void* L)
    {
        IntArray array = Arg(1);
        if (array == null)
            return 0;
        Lua.PushInt(array.Data.Length);
        return 1;
    }

    // while we're here we can add some utility functions..

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int GetMinimum(void* L)
    {
        IntArray array = Arg(1);
        if (array == null)
            return 0;
        int[] data = array.Data;
        int min = data[0], pos = 0;
        for (int i = 1; i < data.Length; ++i)
        {
            if (data[i] < min)
            {
                min = data[i];
                pos = i;
            }
        }
        Lua.PushInt(min);
        Lua.PushInt(pos + 1);
        return 2;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int GetMaximum(void* L)
    {
        IntArray array = Arg(1);
        if (array == null)
            return 0;
        int[] data = array.Data;
        int max = data[0], pos = 0;
        for (int i = 1; i < data.Length; ++i)
        {
            if (data[i] > max)
            {
                max = data[i];
                pos = i;
            }
        }
        Lua.PushInt(max);
        Lua.PushInt(pos + 1);
        return 2;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int GetAverage(void* L)
    {
        IntArray array = Arg(1);
        if (array == null)
            return 0;
        long sum = 0;
        foreach (int v in array.Data)
            sum += v;
        Lua.PushInt((int)(sum / array.Data.Length));
        return 1;
    }
}
