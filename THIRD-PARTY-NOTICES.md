# Third-Party Notices

Zafiro.FigReader.Mcp includes and depends on third-party software. Their licenses and required
copyright notices are reproduced below.

---

## Kiwi (evanw/kiwi)

The Kiwi binary decoder in `src/Zafiro.FigReader.Core/Kiwi/` (ByteBuffer, schema decoding and the
generic message decoder) is a C# port derived from Evan Wallace's Kiwi reference
implementation (https://github.com/evanw/kiwi).

Licensed under the MIT License:

```
Copyright (c) 2016-2023 Evan Wallace

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

---

## ZstdSharp.Port (oleg-st/ZstdSharp)

Used as a NuGet dependency to decompress Zstandard-compressed `.fig` payloads.
A managed C# port of Facebook's Zstandard. https://github.com/oleg-st/ZstdSharp

Licensed under the MIT License:

```
MIT License

Copyright (c) 2021 Oleg Stepanischev

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

ZstdSharp.Port is itself a port of Zstandard (Copyright (c) Meta Platforms, Inc. and
affiliates), which is dual-licensed under the BSD-3-Clause and GPL-2.0 licenses; the
BSD-3-Clause option applies here.

---

## Other dependencies

- **ModelContextProtocol** (modelcontextprotocol/csharp-sdk) — MIT License.
- **Microsoft.Extensions.Hosting / .Extensions.*** — MIT License, © Microsoft Corporation.

---

## Note on the `.fig` format

Zafiro.FigReader.Mcp parses Figma `.fig` files via a clean-room, reverse-engineered understanding of the
Kiwi-based container format for interoperability. "Figma" is a trademark of Figma, Inc.;
this project is not affiliated with, endorsed by, or sponsored by Figma, Inc.
