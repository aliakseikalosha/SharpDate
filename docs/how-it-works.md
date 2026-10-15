# How SharpDate works

SharpDate compiles C# ahead of time with the .NET NativeAOT compiler (ILC) and links the result with
a small runtime of its own. There is no interpreter and no JIT at run time.

```
 MyGame.cs ──csc──▶ MyGame.dll ┐
 SharpDate.dll (Playdate API)  ├─ ILC ──▶ game.o (osx-x64/arm64, linux-x64, windows-x64) ─ clang/gcc, lld-link ▶ pdex.dylib/.so/.dll ┐
 System.Private.CoreLib.dll    ┘      └─▶ game.o (linux-arm: Thumb-2, hard-float ELF) ─ arm-none-eabi-gcc ▶ pdex.elf ───┼─ pdc ─▶ MyGame.pdx
                                         + src/SharpDate.Runtime (sdrt.c, sdrt_*.S)                                    ┘
```

## Components

| Path | Role |
|---|---|
| `src/SharpDate.CoreLib` | SharpDate's `System.Private.CoreLib`: the base library games compile against |
| `src/SharpDate.Runtime` | Native runtime: allocator, garbage collector, write barriers, interface dispatch stubs, arithmetic helpers, `eventHandler`; `link_map.ld` for the device |
| `src/SharpDate` | Playdate API: generated bindings (`Native/PlaydateApi.g.cs`) and wrappers |
| `build/SharpDate.Game.props/.targets` | MSBuild glue: runs ILC per target, links, fixes up, runs `pdc` |
| `tools/` | Bindings generator, deploy, Simulator capture, VFP fix-up |

## Desktop platforms

The Simulator library is the same code on every system, compiled by ILC for the host's OS and CPU:
a Mach-O `pdex.dylib` on macOS, an ELF `pdex.so` on Linux and a COFF `pdex.dll` on Windows. The
native runtime only uses freestanding C headers (it declares the few Playdate API members it needs
in `sdrt_pd.h`), so the Windows build needs nothing but clang and lld-link: the DLL has no entry
point, exports only `eventHandler`, and imports the handful of C library functions compiled code
calls (`memset`, `sinf`, ...) from `ucrtbase.dll`, which is part of Windows 10 and 11. The assembly
stubs (`sdrt_x64.S`) have System V and Windows x64 variants of the write barrier and the interface
dispatch stub.

## Why a custom CoreLib

The standard .NET runtime needs an operating system (threads, virtual memory, a full GC, files).
The Playdate gives a game 16 MB of RAM, no MMU and a C API. ILC can compile against any assembly
that plays the role of `System.Private.CoreLib` (`--systemmodule`), so SharpDate provides one that
only depends on the small native runtime. It implements:

- the types the C# compiler needs (primitives, `string`, arrays, `Span<T>`, delegates, `Nullable<T>`,
  attributes, `Index`/`Range`, tuples, interpolated-string handler),
- the helpers ILC calls by name: type casts (`TypeCast`), boxing (`RuntimeExports`), static
  constructors (`ClassConstructorRunner`), array creation, delegate construction, throw helpers,
  64-bit division (`Math.DivInt64` etc. — 32-bit ARM has no instruction for it),
- module startup (`StartupCodeHelpers`): walks the ReadyToRun header ILC emits to allocate the GC
  static bases and run eager static constructors and `[ModuleInitializer]`s,
- interface dispatch resolution (`InterfaceDispatch`): decodes ILC's dispatch cells and the types'
  dispatch maps, with a small cache,
- a useful subset of the BCL: collections, text, math, `Random`, `Console`, `GC`, `GCHandle`, `NativeMemory`.

The CoreLib's public API is a subset of .NET's, so code written against it also compiles against
regular .NET.

## The native runtime

`src/SharpDate.Runtime/sdrt.c` is compiled with clang for the Simulator and arm-none-eabi-gcc for
the device:

- **Entry**: it defines `eventHandler`. On `kEventInit` it initializes the managed modules, then
  forwards every event to `SharpDate_EventHandler` in `SharpDate.dll`, which creates the `Game`.
- **Allocation**: every object is a separate block from `pd->system->realloc`, with a small header.
  Objects never move.
- **Garbage collection**: mark and sweep. Roots are the GC static bases, strong `GCHandle`s, and the
  native stack, which is scanned conservatively (with interior pointers) between the current stack
  pointer and the outermost entry into managed code. Objects are traced precisely using the GC
  descriptors ILC emits before each MethodTable. Objects created by the compiler at build time
  ("frozen" objects such as preinitialized static arrays) are traced too. A collection runs when
  256 KB or the live size (whichever is larger) has been allocated since the previous one, when an
  allocation fails, or on `GC.Collect()`. Weak handles are cleared when their target dies.
- **Write barriers** are plain stores (the GC is not generational); they're written in assembly
  (`sdrt_thumb2.S`, `sdrt_x64.S`, `sdrt_arm64.S`) because the JIT assumes they don't touch most
  registers.
- **Interface dispatch**: the stub saves the argument registers, calls the managed resolver and
  jumps to the target.
- **Exceptions**: `throw` calls `RhpThrowEx`, which reports the exception's message through
  `pd->system->error`. There is no unwinding, so `catch` blocks never run.

## Device specifics

- ILC targets `linux-arm` (Thumb-2, hard-float ABI), which matches the Cortex‑M7's instruction set.
  The output uses ordinary absolute and PC-relative relocations, which `pdc` handles like a C game's.
- The device link uses `src/SharpDate.Runtime/link_map.ld`, the SDK's linker script extended with
  ILC's sections (`__managedcode`, `__unbox`, `__modules`) and a small veneer area.
- The Playdate's FPU is single precision (`fpv5-sp-d16`). Double arithmetic in managed code would
  be undefined instructions; device builds list such methods. The JIT also occasionally copies two
  float registers with one `vmov.f64`; `tools/fixup_vfp.py` replaces each with a branch to a veneer
  of two `vmov.f32`.
- Audio callbacks run in interrupt context on the device (another thread in the Simulator). The
  runtime refuses managed allocation there and never collects from it.

## Limitations

| Not available | Why / alternative |
|---|---|
| Catching exceptions | No unwinder; validate inputs instead, or return error codes |
| Generic virtual methods | Needs the NativeAOT type loader; use non-generic virtuals or generic classes |
| Reflection, `Type.Name` | No metadata is generated (`--reflectiondata:none`) |
| `async`/`await`, threads, `lock` semantics | Single-threaded platform; use the update loop or callbacks |
| LINQ, `System.IO` streams, `HttpClient`, most of the BCL | Not in SharpDate.CoreLib; use `List<T>`, `FileSystem`, `HttpConnection` |
| `double` on device | Single-precision FPU; use `float` and `MathF` |
| Finalizers | Never run; use `Dispose` |
| `[ThreadStatic]` | Not supported |

Contracts with the compiler (MethodTable layout, module headers, GC descriptors, dispatch cells,
delegate fields...) follow the .NET runtime sources (MIT); see `THIRD-PARTY-NOTICES.md`.
