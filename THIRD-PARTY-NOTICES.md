# Third-party notices

## .NET runtime (MIT)

SharpDate's minimal CoreLib (`src/SharpDate.CoreLib`) and native runtime (`src/SharpDate.Runtime`)
implement data contracts defined by the .NET NativeAOT compiler (MethodTable layout, ReadyToRun
module headers, GC descriptors, interface dispatch cells, delegate and static-constructor helpers).
Several files follow the structure of the corresponding sources in dotnet/runtime
(`src/coreclr/nativeaot/Runtime.Base`, `src/coreclr/nativeaot/Common`, `src/coreclr/gc/gcdesc.h`).

Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License:
https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

## Playdate SDK

The C API bindings in `src/SharpDate/Native/PlaydateApi.g.cs` are generated from the Playdate SDK
headers, and the examples are ports of the SDK's C examples. The Playdate SDK is © Panic Inc. and
subject to its own license (`SDK_LICENSE.md` in the SDK). The SDK itself is not included here.
