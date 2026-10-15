# Lua interop

`Lua`, `LuaFunction`, `LuaType`. Full member list: [Lua.md](../api/Lua.md). Working examples:
`examples/Exposure` (one function), `examples/Array` (a class that behaves like a table),
`examples/Particles` (a C# system driven from Lua), `examples/Lib3D` (a whole engine exposed to Lua).

A game can keep its main loop in `Source/main.lua` and implement the expensive parts in C#, exactly
like a C extension of a Lua game. The game class registers functions in `InitializeLua` and tells
SharpDate not to install its own update loop:

| Override | Value |
|---|---|
| `UsesUpdateCallback` | `false`: Lua's `playdate.update` runs the frames |
| `InitializeLua()` | register functions and classes here (kEventInitLua) |

## Functions

A Lua-callable function is a static `[UnmanagedCallersOnly]` method that takes the Lua state pointer
(`void*`, unused by the API) and returns how many values it pushed. Inside it, read arguments by
position (1-based) and push results:

| Read | Push |
|---|---|
| `ArgCount`, `GetArgType(pos)`, `ArgIsNil(pos)` | `PushNil()` |
| `GetArgBool`, `GetArgInt`, `GetArgFloat`, `GetArgString`, `GetArgBytes` | `PushBool`, `PushInt`, `PushFloat`, `PushString`, `PushBytes` |
| `GetBitmap(pos)` (a `playdate.graphics.image`), `GetSprite(pos)` | `PushBitmap`, `PushSprite` |
| `GetArgObject(pos, class)`, `GetArgManagedObject<T>(pos, class)` | `PushObject`, `PushManagedObject` |
| | `PushFunction(&f)` |

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public sealed unsafe class MathGame : Game
{
    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new MathGame());

    protected override bool UsesUpdateCallback => false;    // main.lua has playdate.update

    protected override void InitializeLua()
    {
        // Lua: local d = mathx.distance(x1, y1, x2, y2)
        if (!Lua.AddFunction(&Distance, "mathx.distance", out string error))
            Sys.Log("addFunction failed: " + error);
        // Lua: local min, max = mathx.range({ ... })  -- here: mathx.range(a, b, c, ...)
        Lua.AddFunction(&Range, "mathx.range", out _);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Distance(void* L)
    {
        float dx = Lua.GetArgFloat(3) - Lua.GetArgFloat(1);
        float dy = Lua.GetArgFloat(4) - Lua.GetArgFloat(2);
        Lua.PushFloat(MathF.Sqrt(dx * dx + dy * dy));
        return 1;                                   // one return value
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Range(void* L)
    {
        int count = Lua.ArgCount;
        if (count == 0)
        {
            Lua.PushNil();
            return 1;
        }
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 1; i <= count; i++)            // arguments are 1-based
        {
            if (Lua.GetArgType(i) != LuaType.Float && Lua.GetArgType(i) != LuaType.Int)
                continue;
            float v = Lua.GetArgFloat(i);
            min = MathF.Min(min, v);
            max = MathF.Max(max, v);
        }
        Lua.PushFloat(min);
        Lua.PushFloat(max);
        return 2;                                   // two return values
    }
}
```

The matching `main.lua`:

<!-- no-compile -->
```lua
import "CoreLibs/graphics"

function playdate.update()
    local d = mathx.distance(0, 0, 3, 4)       -- 5.0
    local lo, hi = mathx.range(4, 8, 15, 16)
    playdate.graphics.clear()
    playdate.graphics.drawText(d .. " " .. lo .. ".." .. hi, 10, 10)
end
```

A name with dots (`"mathx.distance"`) creates the tables along the way; existing ones such as
`"playdate.graphics.image.popcount"` gain a new function.

## Classes and objects

`Lua.RegisterClass(name, methods, out error)` creates a Lua class (a metatable). Methods named
`__gc`, `__index`, `__newindex`, `__len`, `__tostring` ... are metamethods; `new` is a constructor by
convention. A C# object becomes a Lua object with `PushManagedObject(obj, className)`; SharpDate keeps
it alive with a GC handle until `ReleaseManagedObject` runs in `__gc` (when Lua collects the object).
`GetArgManagedObject<T>(pos, className)` gets it back (null if the argument isn't of that class).

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// A counter object usable from Lua:  local c = counter.new(10); c:add(5); print(c:value())
public sealed class Counter
{
    public int Value;
}

public sealed unsafe class CounterGame : Game
{
    private const string ClassName = "counter";

    [ModuleInitializer]
    internal static void Register() => PD.Run(() => new CounterGame());

    protected override bool UsesUpdateCallback => false;

    protected override void InitializeLua()
    {
        var methods = new LuaFunction[]
        {
            new LuaFunction("new", &New),
            new LuaFunction("add", &Add),
            new LuaFunction("value", &Value),
            new LuaFunction("__gc", &Collect),
        };
        if (!Lua.RegisterClass(ClassName, methods, out string error))
            Sys.Log("registerClass failed: " + error);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int New(void* L)
    {
        var counter = new Counter { Value = Lua.ArgIsNil(1) ? 0 : Lua.GetArgInt(1) };
        Lua.PushManagedObject(counter, ClassName);   // kept alive until __gc
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Add(void* L)
    {
        // Method call c:add(5): argument 1 is the object itself, 2 is the amount.
        Counter counter = Lua.GetArgManagedObject<Counter>(1, ClassName);
        if (counter != null)
            counter.Value += Lua.GetArgInt(2);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Value(void* L)
    {
        Counter counter = Lua.GetArgManagedObject<Counter>(1, ClassName);
        Lua.PushInt(counter?.Value ?? 0);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Collect(void* L)
    {
        Lua.ReleaseManagedObject(1, ClassName);      // Lua collected the object: let C# collect it too
        return 0;
    }
}
```

For classes with an `__index` metamethod (table-like access), call `Lua.IndexMetatable()` first: if it
returns true, the key was a method in the metatable and is already on the stack (return 1). See
`examples/Array`.

`Lua.RegisterClass(name, methods, values, out error)` also adds constants (`LuaVal` entries ending with
a zeroed entry), and `isStatic: true` creates a plain table of functions instead of a class.

## Calling Lua from C#

Push the arguments, then call a global function by name (dots allowed). Calling into Lua is slow;
do it rarely.

```csharp
// Calls the Lua function game.onScore(points, "bonus")
Lua.PushInt(500);
Lua.PushString("bonus");
if (!Lua.CallFunction("game.onScore", 2, out string error))
    Sys.Log("game.onScore failed: " + error);
```

## Other members

| Member | Meaning |
|---|---|
| `Lua.Stop()` / `Lua.Start()` | pause / resume the Lua run loop |
| `Lua.GetArgType(pos, out className)` | the class name of an object argument |
| `Lua.GetArgObject(pos, class, out LuaUDObject*)` | the userdata wrapper, for `RetainObject`/`ReleaseObject` and user values |
| `Lua.RetainObject` / `ReleaseObject` | keep a Lua object alive while C# holds it |
| `Lua.SetUserValue(obj, slot)` / `GetUserValue(obj, slot)` | store Lua values inside an object (slots from `PushObject`'s `nValues`) |
| `Lua.PushObject(ptr, class)` / `GetArgObject(pos, class)` | the same with raw native pointers |
