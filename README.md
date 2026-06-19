# FigmaMcp

An **offline** MCP server that reads local Figma `.fig` files so an AI agent can inspect and
reproduce a design — structure, layout, colors, typography, text and embedded images — **without
the Figma API, an account or a token**. Think [Framelink](https://www.framelink.ai), but it parses
the exported `.fig` binary directly instead of calling Figma's servers.

## Why

The Figma REST API is free but requires a token and an internet round-trip, and Framelink-style
tools stream large payloads. FigmaMcp works on a `.fig` you exported yourself
(*Figma → File → Save local copy…*), fully offline, and returns compact, token-efficient JSON.

## How it works

A `.fig` file is a ZIP containing `canvas.fig` (+ `meta.json`, `thumbnail.png`, `images/`, `videos/`).
`canvas.fig` is Evan Wallace's [Kiwi](https://github.com/evanw/kiwi) binary format:

```
"fig-kiwi" (8 bytes) | uint32 version | chunk0 | chunk1 | ...
chunk = uint32 length + payload
chunk0 = Kiwi binary schema (raw DEFLATE) — self-describing
chunk1.. = Kiwi message (raw DEFLATE or Zstandard) → root type "Message"
```

The message holds a flat list of `nodeChanges` (Figma's CRDT model); FigmaMcp rebuilds the node tree
from each node's `parentIndex` (parent GUID + fractional position). The Kiwi decoder is a clean-room
port of `evanw/kiwi`; Zstandard is handled by `ZstdSharp.Port`. No native dependencies.

## Build & test

```bash
dotnet build -c Release
dotnet test                                   # unit tests (no sample needed)
FIGMA_SAMPLE_FIG=/path/to/file.fig dotnet test # also runs the integration tests
```

## Install

Published as a .NET tool on NuGet (requires the .NET 10 SDK/runtime):

```bash
# Install globally...
dotnet tool install -g Zafiro.Figma.Mcp
# ...then run:
figmamcp

# ...or run without installing:
dnx Zafiro.Figma.Mcp
```

The server speaks MCP over **stdio** (logs go to stderr, so stdout stays clean for the protocol).

## MCP client configuration

### VS Code (`.vscode/mcp.json`)

```json
{
  "servers": {
    "figma": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Zafiro.Figma.Mcp", "--yes"]
    }
  }
}
```

### Claude Desktop (`claude_desktop_config.json`)

```json
{
  "mcpServers": {
    "figma": {
      "command": "dnx",
      "args": ["Zafiro.Figma.Mcp", "--yes"]
    }
  }
}
```

If you installed the tool globally you can instead use `"command": "figmamcp"` with no args.

## Build from source

```bash
dotnet build -c Release
dotnet src/FigmaMcp.Server/bin/Release/net10.0/Zafiro.Figma.Mcp.dll
```

(Avoid `dotnet run` for MCP: its first-run build output can corrupt the stdio protocol.)

## Tools

| Tool | Purpose |
| --- | --- |
| `load_file(path)` | Decode a `.fig` and return a summary (file name, pages, node-type histogram, blob count). Call first; result is cached. |
| `get_metadata(path?)` | `meta.json` + high-level counts. |
| `get_node_tree(nodeId?, depth?, path?)` | Compact hierarchy (id, name, type, bounds, fills, strokes, auto-layout, text). |
| `get_node(nodeId, raw?, path?)` | Full detail for one node (set `raw=true` for the unfiltered Kiwi object). |
| `get_text(nodeId?, path?)` | All text content + typography under a subtree. |
| `get_styles(path?)` | Colors actually used (with counts) and distinct text styles. |
| `list_images(path?)` | Embedded image/video blob entries. |
| `export_image(entry, outputDirectory, path?)` | Extract a blob to disk. |
| `search_nodes(query, type?, limit?, path?)` | Find nodes by name/type. |

Node ids use the form `sessionID:localID`. Tools other than `load_file` default to the most
recently loaded file when `path` is omitted.

## Example

```jsonc
// get_node_tree on a button symbol
{
  "id": "5:326", "name": "Button/Primary", "type": "SYMBOL",
  "width": 199, "height": 48, "fills": ["#2730E9"],
  "layout": { "mode": "HORIZONTAL", "gap": 16, "padding": [8,40,8,32],
              "primaryAlign": "CENTER", "counterAlign": "CENTER" },
  "children": [
    { "id": "5:322", "name": "Button Text", "type": "TEXT",
      "text": { "characters": "Button Text", "fontFamily": "SF Pro Display",
                "fontStyle": "Semibold", "fontSize": 18, "color": "#FFFFFF" } }
  ]
}
```

## Layout

```
src/FigmaMcp.Core      Kiwi decoder, .fig container reader, document model, extraction
src/FigmaMcp.Server    MCP stdio server + tools
tests/FigmaMcp.Core.Tests
```

## Limitations

- The Kiwi schema is reverse-engineered; if Figma changes the format, extraction may need updates
  (the embedded schema keeps basic decoding working across versions).
- Vector path geometry is referenced by blob index and not decoded into SVG (yet).
- Large files take a few seconds to decode on first load (then cached in memory).

## License

MIT — see [`LICENSE`](LICENSE).

The Kiwi binary decoder is a port of [evanw/kiwi](https://github.com/evanw/kiwi) (MIT). Third-party
licenses and attributions are listed in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

"Figma" is a trademark of Figma, Inc. This project is not affiliated with, endorsed by, or
sponsored by Figma, Inc.
