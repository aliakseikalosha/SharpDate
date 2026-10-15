# Licensing

SharpDate is copyleft for the engine and free of obligations for your game. This page explains which
license applies to which file and what you have to do when you ship a game. It's a plain-language guide,
not legal advice; the license texts decide.

## Which license applies where

| Files | License |
|---|---|
| `src/SharpDate`, `src/SharpDate.CoreLib`, `src/SharpDate.Runtime`, `build`, `tools`, `tests`, repository configuration | [MPL-2.0](LICENSE), except as listed below |
| `docs` | MPL-2.0, except as listed below |
| `examples`, `templates` | [0BSD](examples/LICENSE) |
| Code examples in the documentation | 0BSD |
| `src/SharpDate/Native/PlaydateApi.g.cs`, `src/SharpDate/Types.cs`, `src/SharpDate.Runtime/sdrt_pd.h`, `docs/api/Native.md`, `docs/api/Types.md` | SharpDate's own contributions are 0BSD; see [Playdate SDK material](#playdate-sdk-material) |

Source files state their license in a header. Code derived from the .NET runtime keeps its MIT
terms; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Making a game with SharpDate

Your game's own code and assets are yours. You can license them however you want, keep them closed and
sell the game. The MPL's copyleft applies only to SharpDate's own files, not to the files you add, even
though everything ends up compiled into one binary (the MPL calls this a "Larger Work").

When you distribute a game, which contains SharpDate in compiled form:

1. **Say where SharpDate's source can be found.** A credit in the game, its readme or its store page
   works:

   > Made with SharpDate (MPL-2.0, https://github.com/aliakseikalosha/SharpDate), which includes code
   > derived from the .NET runtime (MIT, © .NET Foundation and Contributors).

   Include the MIT license text from [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) with the game too,
   for example as a text file in its Source folder.

2. **If you changed any MPL-licensed SharpDate file, share those changed files** under the MPL, and link
   to your version in the credit. Only the changed SharpDate files: not your game. Adding new files of
   your own to a build doesn't count as changing SharpDate's files.

Code copied from `examples`, `templates` or the documentation's code examples has no conditions.

## Contributing

Contributions are accepted under the license of the files they change. New engine files should carry the
MPL header:

```
This Source Code Form is subject to the terms of the Mozilla Public
License, v. 2.0. If a copy of the MPL was not distributed with this
file, You can obtain one at https://mozilla.org/MPL/2.0/.
```

## Playdate SDK material

The Playdate SDK is © Panic Inc. and is not included in this repository. Its license (`SDK_LICENSE.md` in
the SDK) doesn't allow the SDK to be made subject to a copyleft license. So the files that restate
declarations from the SDK's headers are deliberately outside the MPL: the bindings generated from the
headers, the enums and structs mirroring them, and the pages listing them. Their 0BSD grant covers only
what SharpDate added, such as descriptions and C# naming, and nothing in this repository licenses Panic's
material to you. You need your own acceptance of the SDK license to use the SDK and to build games.

"Playdate" is a trademark of Panic Inc. SharpDate is an independent project, not affiliated with or
endorsed by Panic.
