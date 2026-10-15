# Native interop and the base library

The raw Playdate C API, strings for native calls, function pointers, native memory — and what the
C# base library (SharpDate.CoreLib) contains. Full member lists: [Native.md](../api/Native.md) (every
C function pointer with its C signature), [Utf8.md](../api/Utf8.md).

## The raw C API

The wrappers cover the whole API, but anything can also be called directly, exactly as in C.
`PD.Api` is the `PlaydateAPI*` and `PD.System`, `PD.Graphics`, `PD.Sprite`, `PD.Sound`, `PD.File`,
`PD.Display`, `PD.Lua`, `PD.Json`, `PD.Scoreboards`, `PD.Network` are its members. The structs in
`Playdate.Native` (generated from the SDK headers by `tools/generate_bindings.py`) have one
`delegate* unmanaged[Cdecl]` field per C function, in the same order; hovering a field shows its C
declaration.

| C | C# |
|---|---|
| `pd->graphics->drawLine(0, 0, 10, 10, 1, kColorBlack)` | `PD.Graphics->drawLine(0, 0, 10, 10, 1, (nuint)SolidColor.Black)` |
| `LCDColor` | `nuint` (`LCDColor.Value`) |
| `const char*` argument | `byte*` (`Utf8.Arg(text)`) |
| `const char*` result | `byte*` (`Utf8.ToString(p)`) |
| enum argument | the matching C# enum (`SolidColor`, `Buttons`, ...) |
| callback | `delegate* unmanaged[Cdecl]<...>` = `&Method` of an `[UnmanagedCallersOnly]` method |
| `int` boolean | `int` (compare with 0) |

Variadic C functions (`logToConsole`, `error`, `formatString`) can't be called through these pointers
on every platform; use `Sys.Log` / `Sys.Error` and C# string formatting instead.

```csharp
public static unsafe class RawApi
{
    public static void Demo()
    {
        // Same as Graphics.DrawLine(...): the wrapper is a one-line call to this.
        PD.Graphics->drawLine(0, 0, 399, 239, 2, (nuint)SolidColor.Black);

        // Strings in: Utf8.Arg converts into a reusable native buffer (valid until the next call
        // that uses the same slot; use slots 0-3 for several strings in one call).
        Playdate.Native.LCDBitmap* image = PD.Graphics->loadBitmap(Utf8.Arg("images/player"), null);
        if (image == null)
            return;

        // Out parameters are pointers to locals.
        int width, height, rowBytes;
        byte* mask, data;
        PD.Graphics->getBitmapData(image, &width, &height, &rowBytes, &mask, &data);

        // A wrapper struct can wrap the raw pointer to use the idiomatic API.
        var bitmap = new Bitmap(image);
        bitmap.Draw(10, 10);
        bitmap.Dispose();                         // = PD.Graphics->freeBitmap(image)

        // Strings out.
        string language = Sys.Language.ToString();
        byte* err = PD.File->geterr();
        Sys.Log(language + " / " + (Utf8.ToString(err) ?? "no file error"));
    }
}
```

Refer to native types as `Playdate.Native.X` (or alias them) rather than importing the namespace:
`FilePlayer`, `SoundSource`, `AudioSample` and others exist both as opaque native structs and as
wrapper structs in `Playdate`.

## Callbacks: [UnmanagedCallersOnly] and function pointers

Native code can only call static methods marked `[UnmanagedCallersOnly(CallConvs = new[] {
typeof(CallConvCdecl) })]`, passed as `&Method`. Such a method can't be generic and can't be a lambda.
To reach an object from a callback, pass a `GCHandle` as the userdata pointer (this is what `Sprite`,
`MenuItem` and the network classes do internally):

```csharp
public sealed unsafe class Ticker
{
    private GCHandle _self;
    public int Count;

    public Ticker()
    {
        _self = GCHandle.Alloc(this);           // keeps this object alive while native code holds it
        PD.System->setButtonCallback(&OnButton, (void*)GCHandle.ToIntPtr(_self), 5);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int OnButton(Buttons button, int down, uint when, void* userdata)
    {
        var self = (Ticker)GCHandle.FromIntPtr((IntPtr)userdata).Target;
        if (down != 0)
            self.Count++;
        return 0;
    }

    public void Dispose()
    {
        PD.System->setButtonCallback(null, null, 0);
        _self.Free();                           // now the GC may collect this object
    }
}
```

## Utf8

| Member | Meaning |
|---|---|
| `Utf8.Arg(text, slot = 0)` | NUL-terminated UTF-8 in a reusable scratch buffer; slots 0-3 |
| `Utf8.Alloc(text)` / `Utf8.Free(p)` | a copy that lives until you free it (for strings the API keeps) |
| `Utf8.ToString(p)` / `ToString(p, length)` | C string → C# string |
| `Utf8.CodePointCount(span)` | the "length" the Playdate text functions expect |

## Native memory

`NativeMemory.Alloc` / `AllocZeroed` / `Realloc` / `Free` use the Playdate allocator (like
`pd->system->realloc`). `stackalloc`, pointers, `fixed`, `Span<T>` over native memory and
`Unsafe`/`MemoryMarshal` work as in .NET. Memory that the API returns and documents as "caller frees"
is freed with `PD.System->realloc(p, 0)`.

## Objects referenced only by native code

The garbage collector only sees references from C# code. An object reachable only through a native
pointer (userdata, a callback context) must be kept alive with a `GCHandle` or a static field — the
wrappers already do this for sprites, menu items, network connections, JSON handlers and Lua objects.
Objects never move, so a pointer into a pinned or `fixed` array stays valid while the array is alive.

## The base library (SharpDate.CoreLib)

Games compile against SharpDate's own `System.Private.CoreLib`, a subset of .NET written for the
Playdate. Available:

| Area | Types and members |
|---|---|
| Language support | primitives, `string`, arrays (incl. multidimensional), enums, structs, classes, interfaces (incl. default methods, variance), generics (except generic virtual methods), delegates and events, lambdas and closures, `yield` iterators, tuples, `Nullable<T>`, `Index`/`Range`, pattern matching, `using`/`IDisposable`, `params`, `ref`/`in`/`out`, `Span<T>`, `ReadOnlySpan<T>`, `stackalloc`, function pointers |
| `string` | `Concat`, `Format`, interpolation, `Join`, `Split`, `Substring`, `IndexOf`, `LastIndexOf`, `Contains`, `StartsWith`, `EndsWith`, `Replace`, `Trim*`, `PadLeft`/`PadRight`, `ToUpper`/`ToLower`, `Compare`, `IsNullOrEmpty`, `IsNullOrWhiteSpace`, `ToCharArray` |
| Text | `StringBuilder` (`Append`, `AppendLine`, `AppendFormat`, `Insert`, `Remove`, `Clear`), `Encoding.UTF8` (`GetBytes`, `GetString`, `GetByteCount`, `GetCharCount`) |
| Numbers | `ToString()` for all number types; `ToString(format)` for `int`, `uint` and `float` with `D`, `X`/`x`, `N`, `F` and `G` (plus a digit count, e.g. `"D2"`, `"F1"`) — also in interpolation holes like `{score:D6}`; `int.Parse`, `int.TryParse`, `float.Parse`; `Math`, `MathF`, `BitOperations`, `Random` (`Next`, `NextSingle`, `NextBytes`) |
| Collections | `List<T>`, `Dictionary<TKey, TValue>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`, `KeyValuePair`, `EqualityComparer<T>`, `Comparer<T>`, `IEnumerable<T>` and friends, `List.Sort` |
| Spans | `AsSpan`, `Slice`, `CopyTo`, `Fill`, `Clear`, `IndexOf`, `SequenceEqual`, `ToArray` |
| Runtime | `GC` (`Collect`, `CollectionCount`, `GetTotalMemory`, `KeepAlive`), `GCHandle`, `NativeMemory`, `Marshal`, `MemoryMarshal`, `Unsafe`, `RuntimeHelpers`, `Stopwatch`, `Debug.Assert`, `Console.WriteLine` (→ `Sys.Log`), `Interlocked`, `Volatile`, `lock` statements (single-threaded: no-ops) |
| Exceptions | the common exception types; `throw` reports the message and stops the game (no `catch`) |

Not available, with what to use instead:

| Missing | Use |
|---|---|
| LINQ | loops, `List<T>.Find/Exists/RemoveAll/Sort`, `foreach` |
| `catch` | validate inputs; return `bool`/error codes |
| reflection, `Type.Name`, `Enum.ToString()` names | switch statements, your own lookup tables |
| `async`/`await`, `Task`, threads, timers | the frame loop, counters, callbacks |
| `System.IO` (`File`, streams) | `FileSystem`, `PDFile` |
| `HttpClient`, sockets | `HttpConnection`, `TcpConnection` |
| `System.Numerics.Vector2/3`, `Matrix4x4` | your own structs, or `Point3D`/`Vector3D`/`Matrix3D` from `examples/Mini3D` |
| format strings on `byte`, `short`, `long`; culture-specific formatting | cast to `int` first; invariant formatting only |
| `double` on the device | `float`, `MathF` |
| generic virtual methods | non-generic virtual methods or generic classes |
| finalizers | `Dispose()` |
| `[ThreadStatic]` | static fields |

See [How it works](../how-it-works.md) for why.
