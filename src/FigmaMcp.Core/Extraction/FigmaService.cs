using System.Text.Json;
using System.Text.Json.Nodes;
using FigmaMcp.Core.Kiwi;
using FigmaMcp.Core.Model;

namespace FigmaMcp.Core.Extraction;

/// <summary>
/// High-level facade over <see cref="FigFile"/> / <see cref="FigmaDocument"/> used by the MCP tools.
/// Decoded documents are cached by absolute path so the (potentially multi-second) decode happens once.
/// The most recently loaded document becomes the implicit target for tools that omit a path.
/// </summary>
public sealed class FigmaService
{
    private readonly Dictionary<string, FigmaDocument> _cache = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private string? _currentPath;

    public FigmaDocument Load(string path)
    {
        var full = Path.GetFullPath(ExpandHome(path));
        lock (_gate)
        {
            if (!_cache.TryGetValue(full, out var doc))
            {
                doc = FigmaDocument.Build(FigFile.Load(full));
                _cache[full] = doc;
            }

            _currentPath = full;
            return doc;
        }
    }

    public FigmaDocument Resolve(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            return Load(path);
        }

        lock (_gate)
        {
            if (_currentPath is not null && _cache.TryGetValue(_currentPath, out var doc))
            {
                return doc;
            }
        }

        throw new InvalidOperationException("No Figma file loaded. Call 'load_file' first or pass a 'path'.");
    }

    public JsonObject Summary(FigmaDocument doc)
    {
        var typeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in doc.AllNodes)
        {
            var t = n.Type ?? "UNKNOWN";
            typeCounts[t] = typeCounts.GetValueOrDefault(t) + 1;
        }

        var typeJson = new JsonObject();
        foreach (var (t, c) in typeCounts.OrderByDescending(kv => kv.Value))
        {
            typeJson[t] = c;
        }

        var pages = new JsonArray();
        foreach (var page in doc.Pages)
        {
            pages.Add(new JsonObject
            {
                ["id"] = page.Id,
                ["name"] = page.Name,
                ["childCount"] = page.Children.Count,
            });
        }

        return new JsonObject
        {
            ["path"] = doc.File.Path,
            ["fileName"] = FileName(doc),
            ["isArchive"] = doc.File.IsArchive,
            ["nodeCount"] = doc.NodeCount,
            ["pageCount"] = doc.Pages.Count,
            ["blobCount"] = doc.File.BlobEntries.Count,
            ["nodeTypeCounts"] = typeJson,
            ["pages"] = pages,
        };
    }

    public JsonNode Metadata(FigmaDocument doc)
    {
        var result = new JsonObject
        {
            ["fileName"] = FileName(doc),
            ["nodeCount"] = doc.NodeCount,
            ["pageCount"] = doc.Pages.Count,
        };

        if (doc.File.MetaJson is { } meta)
        {
            try
            {
                result["meta"] = JsonNode.Parse(meta);
            }
            catch (JsonException)
            {
                result["meta"] = meta;
            }
        }

        return result;
    }

    public JsonNode NodeTree(FigmaDocument doc, string? nodeId, int depth)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            var node = doc.FindById(nodeId!)
                ?? throw new KeyNotFoundException($"Node '{nodeId}' not found.");
            return FigmaExtractor.Simplify(node, depth);
        }

        var roots = doc.Pages.Count > 0
            ? doc.Pages
            : (doc.Root is { } r ? new[] { r } : Array.Empty<FigmaNode>());

        var array = new JsonArray();
        foreach (var root in roots)
        {
            array.Add(FigmaExtractor.Simplify(root, depth));
        }

        return array;
    }

    public JsonNode NodeDetail(FigmaDocument doc, string nodeId, bool raw)
    {
        var node = doc.FindById(nodeId)
            ?? throw new KeyNotFoundException($"Node '{nodeId}' not found.");

        if (raw)
        {
            return KiwiJson.ToJson(node.Raw, new KiwiJsonOptions { MaxDepth = 20, MaxArrayLength = 200 })
                   ?? new JsonObject();
        }

        var json = FigmaExtractor.Simplify(node, depth: 0, includeChildren: false);
        if (node.ParentId is not null)
        {
            json["parentId"] = node.ParentId;
        }

        if (node.Children.Count > 0)
        {
            var children = new JsonArray();
            foreach (var child in node.Children)
            {
                children.Add(new JsonObject { ["id"] = child.Id, ["name"] = child.Name, ["type"] = child.Type });
            }

            json["children"] = children;
        }

        return json;
    }

    public JsonArray Text(FigmaDocument doc, string? nodeId)
    {
        var scope = ScopeNodes(doc, nodeId);
        var array = new JsonArray();
        foreach (var node in scope.Where(n => n.Type == "TEXT"))
        {
            var characters = node.Raw.GetObject("textData")?.GetString("characters");
            if (string.IsNullOrEmpty(characters))
            {
                continue;
            }

            var entry = new JsonObject
            {
                ["id"] = node.Id,
                ["name"] = node.Name,
                ["characters"] = characters,
            };

            if (FigmaExtractor.SimplifyText(node) is { } style)
            {
                style.Remove("characters");
                entry["style"] = style;
            }

            array.Add(entry);
        }

        return array;
    }

    public JsonObject Styles(FigmaDocument doc)
    {
        var colorCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var textStyles = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var node in doc.AllNodes)
        {
            CountColors(node.Raw.GetList("fillPaints"), colorCounts);
            CountColors(node.Raw.GetList("strokePaints"), colorCounts);

            if (node.Type == "TEXT" && FigmaExtractor.SimplifyText(node) is { } text)
            {
                var family = text["fontFamily"]?.GetValue<string>();
                var fstyle = text["fontStyle"]?.GetValue<string>();
                var size = text["fontSize"]?.GetValue<double>();
                var key = $"{family}|{fstyle}|{size}";
                if (!textStyles.ContainsKey(key))
                {
                    textStyles[key] = new JsonObject
                    {
                        ["fontFamily"] = family,
                        ["fontStyle"] = fstyle,
                        ["fontSize"] = size,
                    };
                }
            }
        }

        var colors = new JsonArray();
        foreach (var (hex, count) in colorCounts.OrderByDescending(kv => kv.Value))
        {
            colors.Add(new JsonObject { ["color"] = hex, ["usages"] = count });
        }

        var styles = new JsonArray();
        foreach (var style in textStyles.Values)
        {
            styles.Add(style);
        }

        return new JsonObject { ["colors"] = colors, ["textStyles"] = styles };
    }

    public JsonArray ListImages(FigmaDocument doc)
    {
        var array = new JsonArray();
        foreach (var entry in doc.File.BlobEntries)
        {
            array.Add(new JsonObject
            {
                ["entry"] = entry,
                ["kind"] = entry.StartsWith("videos/", StringComparison.Ordinal) ? "video" : "image",
            });
        }

        return array;
    }

    public JsonObject ExportImage(FigmaDocument doc, string entry, string outputDirectory)
    {
        var name = entry.Contains('/') ? entry : $"images/{entry}";
        var bytes = doc.File.ReadBlob(name)
            ?? throw new KeyNotFoundException($"Blob '{entry}' not found in archive.");

        var dir = Path.GetFullPath(ExpandHome(outputDirectory));
        Directory.CreateDirectory(dir);
        var fileName = name.Replace('/', '_');
        var outPath = Path.Combine(dir, fileName);
        File.WriteAllBytes(outPath, bytes);

        return new JsonObject { ["entry"] = name, ["bytes"] = bytes.Length, ["path"] = outPath };
    }

    public JsonArray Search(FigmaDocument doc, string query, string? type, int limit)
    {
        var array = new JsonArray();
        foreach (var node in doc.AllNodes)
        {
            if (type is not null && !string.Equals(node.Type, type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(query) ||
                (node.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                var json = new JsonObject { ["id"] = node.Id, ["name"] = node.Name, ["type"] = node.Type };
                FigmaExtractor.AddBounds(json, node);
                array.Add(json);
                if (array.Count >= limit)
                {
                    break;
                }
            }
        }

        return array;
    }

    private static IEnumerable<FigmaNode> ScopeNodes(FigmaDocument doc, string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return doc.AllNodes;
        }

        var root = doc.FindById(nodeId!)
            ?? throw new KeyNotFoundException($"Node '{nodeId}' not found.");
        return new[] { root }.Concat(root.Descendants());
    }

    private static void CountColors(IReadOnlyList<object?>? paints, Dictionary<string, int> counts)
    {
        if (paints is null)
        {
            return;
        }

        foreach (var item in paints)
        {
            if (item is KiwiObject paint &&
                paint.GetString("type") == "SOLID" &&
                paint.GetBool("visible") != false &&
                paint.GetObject("color") is { } color)
            {
                var hex = FigmaExtractor.ColorToHex(color, paint.GetNumber("opacity"));
                counts[hex] = counts.GetValueOrDefault(hex) + 1;
            }
        }
    }

    private static string FileName(FigmaDocument doc)
    {
        if (doc.File.MetaJson is { } meta)
        {
            try
            {
                var name = JsonNode.Parse(meta)?["file_name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch (JsonException)
            {
                // fall through
            }
        }

        return Path.GetFileNameWithoutExtension(doc.File.Path);
    }

    private static string ExpandHome(string path) =>
        path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;
}
