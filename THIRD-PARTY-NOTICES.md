# Third-party notices

## .NET runtime (MIT)

SharpDate's minimal CoreLib (`src/SharpDate.CoreLib`) and native runtime (`src/SharpDate.Runtime`)
implement data contracts defined by the .NET NativeAOT compiler (MethodTable layout, ReadyToRun
module headers, GC descriptors, interface dispatch cells, delegate and static-constructor helpers).
Several files follow the structure of the corresponding sources in dotnet/runtime
(`src/coreclr/nativeaot/Runtime.Base`, `src/coreclr/nativeaot/Common`, `src/coreclr/gc/gcdesc.h`).

The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Playdate SDK

The C API bindings in `src/SharpDate/Native/PlaydateApi.g.cs` are generated from the Playdate SDK
headers. The Playdate SDK is © Panic Inc. and subject to its own license (`SDK_LICENSE.md` in the
SDK). The SDK itself is not included here. See [LICENSING.md](LICENSING.md) for how this repository
treats material derived from the headers.

The examples, and `src/SharpDate.Runtime/link_map.ld`, are ports or adaptations of code from the SDK's
`C_API/Examples` and `C_API/buildsupport`, which Panic licenses as follows:

```
BSD Zero Clause License

Copyright (C) Panic Inc. help@play.date

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE
OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE SOFTWARE OR
THE USE OR PERFORMANCE OF THIS SOFTWARE.
```
