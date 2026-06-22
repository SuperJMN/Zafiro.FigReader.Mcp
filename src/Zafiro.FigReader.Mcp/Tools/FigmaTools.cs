using System.ComponentModel;
using System.Text.Json.Nodes;
using Zafiro.FigReader.Core.Extraction;
using ModelContextProtocol.Server;

namespace Zafiro.FigReader.Mcp.Tools;

/// <summary>
/// MCP tools for reading local Figma <c>.fig</c> files offline (no Figma API, no token).
/// Designed so an agent can inspect and faithfully reproduce a design: structure, layout,
/// colors, typography, text content and embedded images. All tools return JSON.
/// Tools other than <c>load_file</c> default to the most recently loaded file when <c>path</c> is omitted.
/// </summary>
[McpServerToolType]
public static class FigmaTools
{
    [McpServerTool(Name = "load_file")]
    [Description("Load and decode a local Figma .fig file and return a summary: file name, page list, " +
                 "total node count, node-type histogram and embedded blob count. Call this first. " +
                 "Decoding is cached, so subsequent tools are fast.")]
    public static string LoadFile(
        FigmaService service,
        [Description("Absolute path to the .fig file (e.g. /home/user/design.fig). '~' is expanded.")] string path)
    {
        var doc = service.Load(path);
        return Json(service.Summary(doc));
    }

    [McpServerTool(Name = "get_metadata")]
    [Description("Return document metadata (meta.json: file name, thumbnail size, background color, " +
                 "export date) plus high-level counts.")]
    public static string GetMetadata(
        FigmaService service,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.Metadata(service.Resolve(path)));
    }

    [McpServerTool(Name = "get_node_tree")]
    [Description("Return a compact, token-efficient hierarchy of nodes (id, name, type, x/y/width/height, " +
                 "fills, strokes, auto-layout and text). Omit nodeId to start from the document pages. " +
                 "Use 'depth' to control recursion; deeper subtrees report only a childCount.")]
    public static string GetNodeTree(
        FigmaService service,
        [Description("Optional node id ('sessionID:localID') to use as the subtree root.")] string? nodeId = null,
        [Description("Recursion depth (default 2). 0 returns just the node(s) with child counts.")] int depth = 2,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.NodeTree(service.Resolve(path), nodeId, depth));
    }

    [McpServerTool(Name = "get_node")]
    [Description("Return detailed properties for a single node: bounds, opacity, corner radius, fills, " +
                 "strokes, auto-layout, text style, parent id and direct children. Set raw=true for the " +
                 "full undecorated Kiwi object (verbose).")]
    public static string GetNode(
        FigmaService service,
        [Description("Node id in 'sessionID:localID' form (from get_node_tree or search_nodes).")] string nodeId,
        [Description("Return the full raw node instead of the simplified view (default false).")] bool raw = false,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.NodeDetail(service.Resolve(path), nodeId, raw));
    }

    [McpServerTool(Name = "get_text")]
    [Description("Extract all text content (characters + typography) under a subtree, or the whole " +
                 "document if nodeId is omitted. Useful for copying labels and content.")]
    public static string GetText(
        FigmaService service,
        [Description("Optional subtree root node id; omit for the whole document.")] string? nodeId = null,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.Text(service.Resolve(path), nodeId));
    }

    [McpServerTool(Name = "get_styles")]
    [Description("Summarize the design system actually used: distinct solid colors (with usage counts) " +
                 "and distinct text styles (font family/style/size).")]
    public static string GetStyles(
        FigmaService service,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.Styles(service.Resolve(path)));
    }

    [McpServerTool(Name = "list_images")]
    [Description("List embedded image and video blobs (their archive entry names/hashes).")]
    public static string ListImages(
        FigmaService service,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.ListImages(service.Resolve(path)));
    }

    [McpServerTool(Name = "export_image")]
    [Description("Extract an embedded image/video blob to a directory on disk and return the output path. " +
                 "Pass the entry name or hash from list_images.")]
    public static string ExportImage(
        FigmaService service,
        [Description("Blob entry name or hash (e.g. 'images/<hash>' or just '<hash>').")] string entry,
        [Description("Output directory; created if missing.")] string outputDirectory,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.ExportImage(service.Resolve(path), entry, outputDirectory));
    }

    [McpServerTool(Name = "search_nodes")]
    [Description("Find nodes by name or instance/text override content (case-insensitive substring), " +
                 "optionally by type and subtree. Returns id, name, type, bounds, page and path.")]
    public static string SearchNodes(
        FigmaService service,
        [Description("Name or text substring to match. Empty matches all (combine with type or nodeId).")] string query,
        [Description("Optional node type filter, e.g. FRAME, TEXT, INSTANCE, COMPONENT.")] string? type = null,
        [Description("Maximum results (default 50).")] int limit = 50,
        [Description("Optional subtree root node id, e.g. a page id like '207:23353'.")] string? nodeId = null,
        [Description("Optional .fig path; defaults to the last loaded file.")] string? path = null)
    {
        return Json(service.Search(service.Resolve(path), query, type, limit, nodeId));
    }

    private static string Json(JsonNode? node) => KiwiJson.Serialize(node);
}
