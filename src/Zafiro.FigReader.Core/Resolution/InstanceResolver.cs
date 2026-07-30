using Zafiro.FigReader.Core.Kiwi;
using Zafiro.FigReader.Core.Model;

namespace Zafiro.FigReader.Core.Resolution;

/// <summary>
/// Expands a Figma INSTANCE into the subtree it actually renders. Instances do not store their
/// children directly; they reference a SYMBOL and carry a set of overrides. This resolver walks the
/// referenced symbol's subtree and applies:
/// <list type="bullet">
/// <item><c>symbolData.symbolOverrides[]</c> keyed by <c>guidPath</c> (text, visibility, ...);</item>
/// <item><c>componentPropAssignments[]</c> providing text component-property values
///   (<c>value.textValue</c>) and instance-swaps (<c>value.guidValue</c>, e.g. an icon).</item>
/// </list>
/// Nested instances are resolved recursively, with the outer instance's deeper overrides rebased into
/// the nested symbol's coordinate space. Overrides from an outer instance win over the component's own.
/// </summary>
public sealed class InstanceResolver
{
    private const int MaxDepth = 40;

    private readonly FigmaDocument _doc;

    public InstanceResolver(FigmaDocument doc) => _doc = doc;

    /// <summary>
    /// Resolves the subtree an INSTANCE renders, expanding at most <paramref name="maxDepth"/> levels of
    /// resolved children (the default expands everything). Returns <see langword="null"/> for
    /// non-instances or when the referenced symbol cannot be found.
    /// </summary>
    public ResolvedNode? Resolve(FigmaNode instance, int maxDepth = int.MaxValue)
    {
        if (instance.Type != "INSTANCE")
        {
            return null;
        }

        var symbolId = SymbolIdOf(instance);
        if (symbolId is null)
        {
            return null;
        }

        return ExpandInstance(
            instance,
            symbolId,
            new Dictionary<string, KiwiObject>(StringComparer.Ordinal),
            ParseAssignments(instance.Raw.GetList("componentPropAssignments")),
            new HashSet<string>(StringComparer.Ordinal),
            0,
            maxDepth);
    }

    /// <summary>
    /// The number of resolved children an INSTANCE would have, computed cheaply (the referenced
    /// symbol's direct child count) without expanding the subtree.
    /// </summary>
    public int ResolvedChildCount(FigmaNode instance)
    {
        var symbolId = SymbolIdOf(instance);
        return symbolId is not null && _doc.FindById(symbolId) is { } symbol ? symbol.Children.Count : 0;
    }

    private ResolvedNode ExpandInstance(
        FigmaNode instance,
        string symbolId,
        IReadOnlyDictionary<string, KiwiObject> inheritedOverrides,
        IReadOnlyDictionary<string, KiwiObject> assignments,
        HashSet<string> visited,
        int depth,
        int remaining)
    {
        var root = new ResolvedNode
        {
            Source = instance,
            Type = instance.Type,
            Name = instance.Name,
            Visible = instance.Raw.GetBool("visible") != false,
        };

        var symbol = _doc.FindById(symbolId);
        if (symbol is null || remaining <= 0 || depth > MaxDepth || !visited.Add(symbolId))
        {
            return root;
        }

        // The instance's own path-keyed overrides only match its *default* symbol; when swapped
        // (symbolId differs) they no longer apply, so rely on the inherited (outer) overrides only.
        var overrides = symbolId == SymbolIdOf(instance)
            ? Merge(ParseOverrides(instance), inheritedOverrides)
            : new Dictionary<string, KiwiObject>(inheritedOverrides, StringComparer.Ordinal);

        foreach (var child in symbol.Children)
        {
            root.Children.Add(Build(child, overrides, assignments, visited, depth + 1, remaining - 1));
        }

        visited.Remove(symbolId);
        return root;
    }

    private ResolvedNode Build(
        FigmaNode node,
        IReadOnlyDictionary<string, KiwiObject> overrides,
        IReadOnlyDictionary<string, KiwiObject> assignments,
        HashSet<string> visited,
        int depth,
        int remaining)
    {
        // Within a symbol, Figma keys each override by the target node's own guid; only nested
        // instances add segments (the instance chain). So a node is matched by its id, and frame
        // nesting does not extend the key.
        overrides.TryGetValue(node.Id, out var ov);

        var visible = ov?.GetBool("visible") ?? node.Raw.GetBool("visible") ?? true;
        var resolved = new ResolvedNode
        {
            Source = node,
            Type = node.Type,
            Name = node.Name,
            Visible = visible,
        };

        if (node.Type == "TEXT")
        {
            resolved.Text = ov?.GetObject("textData")?.GetString("characters")
                            ?? ResolveTextProperty(node, assignments)
                            ?? node.Raw.GetObject("textData")?.GetString("characters");
        }

        if (node.Type == "INSTANCE")
        {
            var nodeAssignments = Merge(
                ParseAssignments(node.Raw.GetList("componentPropAssignments")),
                ParseAssignments(ov?.GetList("componentPropAssignments")));

            var (swapId, swapName) = DetectSwap(nodeAssignments);
            resolved.SwapComponentId = swapId;
            resolved.SwapComponentName = swapName;

            if (remaining > 0)
            {
                var nested = Rebase(overrides, node.Id);
                var effectiveSymbol = swapId ?? SymbolIdOf(node);
                if (effectiveSymbol is not null && depth <= MaxDepth)
                {
                    var expanded = ExpandInstance(node, effectiveSymbol, nested, nodeAssignments, visited, depth + 1, remaining);
                    resolved.Children.AddRange(expanded.Children);
                }
            }
        }
        else if (remaining > 0)
        {
            foreach (var child in node.Children)
            {
                resolved.Children.Add(Build(child, overrides, assignments, visited, depth + 1, remaining - 1));
            }
        }

        return resolved;
    }

    private static string? SymbolIdOf(FigmaNode instance)
    {
        var guid = instance.Raw.GetObject("symbolData")?.GetObject("symbolID");
        return guid is null ? null : FigmaDocument.FormatGuid(guid);
    }

    private static Dictionary<string, KiwiObject> ParseOverrides(FigmaNode instance)
    {
        var map = new Dictionary<string, KiwiObject>(StringComparer.Ordinal);
        var overrides = instance.Raw.GetObject("symbolData")?.GetList("symbolOverrides");
        if (overrides is null)
        {
            return map;
        }

        foreach (var item in overrides)
        {
            if (item is not KiwiObject entry)
            {
                continue;
            }

            var guids = entry.GetObject("guidPath")?.GetList("guids");
            if (guids is null || guids.Count == 0)
            {
                continue;
            }

            var key = string.Join('/', guids.OfType<KiwiObject>().Select(FigmaDocument.FormatGuid));
            // A guidPath is unique per override; last one wins if duplicated.
            map[key] = entry;
        }

        return map;
    }

    private static Dictionary<string, KiwiObject> ParseAssignments(IReadOnlyList<object?>? assignments)
    {
        var map = new Dictionary<string, KiwiObject>(StringComparer.Ordinal);
        if (assignments is null)
        {
            return map;
        }

        foreach (var item in assignments)
        {
            if (item is not KiwiObject assignment)
            {
                continue;
            }

            var defId = assignment.GetObject("defID");
            var value = assignment.GetObject("value");
            if (defId is null || value is null)
            {
                continue;
            }

            map[FigmaDocument.FormatGuid(defId)] = value;
        }

        return map;
    }

    private static string? ResolveTextProperty(FigmaNode textNode, IReadOnlyDictionary<string, KiwiObject> assignments)
    {
        var refs = textNode.Raw.GetList("componentPropRefs");
        if (refs is null)
        {
            return null;
        }

        foreach (var item in refs)
        {
            if (item is not KiwiObject reference)
            {
                continue;
            }

            if (reference.GetString("componentPropNodeField") != "TEXT_DATA")
            {
                continue;
            }

            var defId = reference.GetObject("defID");
            if (defId is null)
            {
                continue;
            }

            if (assignments.TryGetValue(FigmaDocument.FormatGuid(defId), out var value))
            {
                return value.GetObject("textValue")?.GetString("characters");
            }
        }

        return null;
    }

    private (string? Id, string? Name) DetectSwap(IReadOnlyDictionary<string, KiwiObject> assignments)
    {
        foreach (var value in assignments.Values)
        {
            var guid = value.GetObject("guidValue");
            if (guid is null)
            {
                continue;
            }

            var id = FigmaDocument.FormatGuid(guid);
            var target = _doc.FindById(id);
            if (target is not null)
            {
                return (id, target.Name);
            }
        }

        return (null, null);
    }

    private static Dictionary<string, KiwiObject> Rebase(
        IReadOnlyDictionary<string, KiwiObject> overrides,
        string instanceId)
    {
        var prefix = instanceId + "/";
        var map = new Dictionary<string, KiwiObject>(StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                map[key[prefix.Length..]] = value;
            }
        }

        return map;
    }

    private static Dictionary<string, KiwiObject> Merge(
        IReadOnlyDictionary<string, KiwiObject> own,
        IReadOnlyDictionary<string, KiwiObject> winning)
    {
        var map = new Dictionary<string, KiwiObject>(own, StringComparer.Ordinal);
        foreach (var (key, value) in winning)
        {
            map[key] = value;
        }

        return map;
    }
}
