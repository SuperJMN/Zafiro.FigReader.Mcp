# Zafiro.FigReader.Mcp

An **offline** MCP server that reads local Figma `.fig` files so an AI agent can inspect and
reproduce a design — structure, layout, colors, typography, text and embedded images — **without
the Figma API, an account or a token**. Think [Framelink](https://www.framelink.ai), but it parses
the exported `.fig` binary directly instead of calling Figma's servers.

## Why

The Figma REST API is free but requires a token and an internet round-trip, and Framelink-style
tools stream large payloads. Zafiro.FigReader.Mcp works on a `.fig` you exported yourself
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

The message holds a flat list of `nodeChanges` (Figma's CRDT model); Zafiro.FigReader.Mcp rebuilds the node tree
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
dotnet tool install -g Zafiro.FigReader.Mcp
# ...then run:
figreadermcp

# ...or run without installing:
dnx Zafiro.FigReader.Mcp
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
      "args": ["Zafiro.FigReader.Mcp", "--yes"]
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
      "args": ["Zafiro.FigReader.Mcp", "--yes"]
    }
  }
}
```

If you installed the tool globally you can instead use `"command": "figreadermcp"` with no args.

## Build from source

```bash
dotnet build -c Release
dotnet src/Zafiro.FigReader.Mcp/bin/Release/net10.0/Zafiro.FigReader.Mcp.dll
```

(Avoid `dotnet run` for MCP: its first-run build output can corrupt the stdio protocol.)

## Tools

| Tool | Purpose |
| --- | --- |
| `load_file(path)` | Decode a `.fig` and return a summary (file name, pages, node-type histogram, blob count). Call first; result is cached. |
| `get_metadata(path?)` | `meta.json` + high-level counts. |
| `get_node_tree(nodeId?, depth?, path?)` | Compact hierarchy (id, name, type, bounds, fills, strokes, auto-layout, text). INSTANCE nodes are expanded to the symbol content they render, with overrides applied (effective text, per-node visibility, icon instance-swaps as `swappedTo`). |
| `get_node(nodeId, raw?, path?)` | Full detail for one node. For an INSTANCE, `resolvedChildren` lists the effective labels, icons (`swappedTo`) and visibility. Set `raw=true` for the unfiltered Kiwi object. |
| `get_text(nodeId?, path?)` | All text content + typography under a subtree. |
| `get_styles(path?)` | Colors actually used (with counts) and distinct text styles. |
| `get_vector(nodeId, geometry?, path?)` | Decode a node's vector geometry into SVG path(s) (with an `F0`/`F1` fill-rule prefix, ready for an SVG `d`/Avalonia `StreamGeometry`). Pass `geometry="strokeGeometry"` for the stroke outline. |
| `list_images(path?)` | Embedded image/video blob entries. |
| `export_image(entry, outputDirectory, path?)` | Extract a blob to disk. |
| `search_nodes(query, type?, limit?, nodeId?, path?)` | Find nodes by name, text, or instance overrides; optionally scoped to a subtree such as a page. |

Node ids use the form `sessionID:localID`. Tools other than `load_file` default to the most
recently loaded file when `path` is omitted.

## Instances

Figma stores an INSTANCE as a reference to a SYMBOL plus a set of overrides, so instances carry no
child nodes of their own. `get_node_tree`/`get_node` resolve that for you: they walk the referenced
symbol and apply `symbolOverrides` (text, visibility, …) and `componentPropAssignments` (text
component properties and instance-swaps — e.g. an icon). Nested instances are resolved recursively,
so an icon inside a tab inside a tab-menu is surfaced as the actual component it renders.

## Example

```jsonc
// Synthetic example of get_node_tree on a button symbol
{
  "id": "1:100", "name": "Example/Primary", "type": "SYMBOL",
  "width": 200, "height": 40, "fills": ["#0000FF"],
  "layout": { "mode": "HORIZONTAL", "gap": 8, "padding": [8,16,8,16],
              "primaryAlign": "CENTER", "counterAlign": "CENTER" },
  "children": [
    { "id": "1:101", "name": "Label", "type": "TEXT",
      "text": { "characters": "Example", "fontFamily": "Sans",
                "fontStyle": "Regular", "fontSize": 16, "color": "#FFFFFF" } }
  ]
}
```

## Layout

```
src/Zafiro.FigReader.Core      Kiwi decoder, .fig container reader, document model, extraction
src/Zafiro.FigReader.Mcp    MCP stdio server + tools
tests/Zafiro.FigReader.Core.Tests
```

## Limitations

- The Kiwi schema is reverse-engineered; if Figma changes the format, extraction may need updates
  (the embedded schema keeps basic decoding working across versions).
- Instance resolution covers text/visibility overrides and text/instance-swap component properties;
  exotic property kinds (variables, exposed nested props) are surfaced only partially.
- Large files take a few seconds to decode on first load (then cached in memory).

## License

MIT — see [`LICENSE`](LICENSE).

The Kiwi binary decoder is a port of [evanw/kiwi](https://github.com/evanw/kiwi) (MIT). Third-party
licenses and attributions are listed in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

"Figma" is a trademark of Figma, Inc. This project is not affiliated with, endorsed by, or
sponsored by Figma, Inc.
