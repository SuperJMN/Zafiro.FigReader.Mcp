using System.Text.Json.Nodes;
using Zafiro.FigReader.Core.Kiwi;
using Zafiro.FigReader.Core.Model;

namespace Zafiro.FigReader.Core.Extraction;

/// <summary>
/// Produces compact, token-efficient views of Figma nodes for an AI agent: simplified node trees,
/// per-node detail, bounds, colors, auto-layout and text style. Raw access remains available via
/// <see cref="KiwiJson"/> for callers that need everything.
/// </summary>
public static class FigmaExtractor
{
    /// <summary>Builds a simplified node, optionally recursing into children down to <paramref name="depth"/>.</summary>
    public static JsonObject Simplify(FigmaNode node, int depth, bool includeChildren = true)
    {
        var json = new JsonObject
        {
            ["id"] = node.Id,
            ["name"] = node.Name,
            ["type"] = node.Type,
        };

        AddBounds(json, node);

        if (node.Raw.GetBool("visible") == false)
        {
            json["visible"] = false;
        }

        var opacity = node.Raw.GetNumber("opacity");
        if (opacity is not null && opacity < 1)
        {
            json["opacity"] = Round(opacity.Value);
        }

        var cornerRadius = node.Raw.GetNumber("cornerRadius");
        if (cornerRadius is > 0)
        {
            json["cornerRadius"] = Round(cornerRadius.Value);
        }

        var fills = SimplifyPaints(node.Raw.GetList("fillPaints"));
        if (fills is not null)
        {
            json["fills"] = fills;
        }

        var strokes = SimplifyPaints(node.Raw.GetList("strokePaints"));
        if (strokes is not null)
        {
            json["strokes"] = strokes;
            var sw = node.Raw.GetNumber("strokeWeight");
            if (sw is not null)
            {
                json["strokeWeight"] = Round(sw.Value);
            }
        }

        var layout = SimplifyLayout(node);
        if (layout is not null)
        {
            json["layout"] = layout;
        }

        var text = SimplifyText(node);
        if (text is not null)
        {
            json["text"] = text;
        }

        if (includeChildren && node.Children.Count > 0)
        {
            if (depth <= 0)
            {
                json["childCount"] = node.Children.Count;
            }
            else
            {
                var children = new JsonArray();
                foreach (var child in node.Children)
                {
                    children.Add(Simplify(child, depth - 1, includeChildren));
                }

                json["children"] = children;
            }
        }

        return json;
    }

    public static void AddBounds(JsonObject json, FigmaNode node)
    {
        var size = node.Raw.GetObject("size");
        var transform = node.Raw.GetObject("transform");
        if (size is not null)
        {
            json["width"] = Round(size.GetNumber("x") ?? 0);
            json["height"] = Round(size.GetNumber("y") ?? 0);
        }

        if (transform is not null)
        {
            json["x"] = Round(transform.GetNumber("m02") ?? 0);
            json["y"] = Round(transform.GetNumber("m12") ?? 0);
        }
    }

    public static JsonArray? SimplifyPaints(IReadOnlyList<object?>? paints)
    {
        if (paints is null || paints.Count == 0)
        {
            return null;
        }

        var array = new JsonArray();
        foreach (var item in paints)
        {
            if (item is not KiwiObject paint)
            {
                continue;
            }

            if (paint.GetBool("visible") == false)
            {
                continue;
            }

            var type = paint.GetString("type");
            if (type == "SOLID" && paint.GetObject("color") is { } color)
            {
                var opacity = paint.GetNumber("opacity");
                array.Add(ColorToHex(color, opacity));
            }
            else
            {
                array.Add(type ?? "PAINT");
            }
        }

        return array.Count > 0 ? array : null;
    }

    private static JsonObject? SimplifyLayout(FigmaNode node)
    {
        var mode = node.Raw.GetString("stackMode");
        if (mode is null or "NONE")
        {
            return null;
        }

        var layout = new JsonObject { ["mode"] = mode };

        var gap = node.Raw.GetNumber("stackSpacing");
        if (gap is not null)
        {
            layout["gap"] = Round(gap.Value);
        }

        var top = node.Raw.GetNumber("stackVerticalPadding") ?? 0;
        var right = node.Raw.GetNumber("stackPaddingRight") ?? 0;
        var bottom = node.Raw.GetNumber("stackPaddingBottom") ?? 0;
        var left = node.Raw.GetNumber("stackHorizontalPadding") ?? 0;
        if (top != 0 || right != 0 || bottom != 0 || left != 0)
        {
            layout["padding"] = new JsonArray { Round(top), Round(right), Round(bottom), Round(left) };
        }

        AddString(layout, "primaryAlign", node.Raw.GetString("stackPrimaryAlignItems"));
        AddString(layout, "counterAlign", node.Raw.GetString("stackCounterAlignItems"));
        AddString(layout, "primarySizing", node.Raw.GetString("stackPrimarySizing"));
        AddString(layout, "counterSizing", node.Raw.GetString("stackCounterSizing"));

        return layout;
    }

    public static JsonObject? SimplifyText(FigmaNode node)
    {
        if (node.Type != "TEXT")
        {
            return null;
        }

        var text = new JsonObject
        {
            ["characters"] = node.Raw.GetObject("textData")?.GetString("characters"),
        };

        var font = node.Raw.GetObject("fontName");
        if (font is not null)
        {
            AddString(text, "fontFamily", font.GetString("family"));
            AddString(text, "fontStyle", font.GetString("style"));
        }

        var fontSize = node.Raw.GetNumber("fontSize");
        if (fontSize is not null)
        {
            text["fontSize"] = Round(fontSize.Value);
        }

        var lineHeight = node.Raw.GetObject("lineHeight");
        if (lineHeight is not null)
        {
            var value = lineHeight.GetNumber("value");
            var units = lineHeight.GetString("units");
            if (value is not null)
            {
                text["lineHeight"] = units switch
                {
                    "PIXELS" => $"{Round(value.Value)}px",
                    "PERCENT" => $"{Round(value.Value)}%",
                    _ => Round(value.Value),
                };
            }
        }

        var letterSpacing = node.Raw.GetObject("letterSpacing")?.GetNumber("value");
        if (letterSpacing is not null && letterSpacing != 0)
        {
            text["letterSpacing"] = Round(letterSpacing.Value);
        }

        AddString(text, "align", node.Raw.GetString("textAlignHorizontal"));

        var color = node.Raw.GetList("fillPaints");
        var colors = SimplifyPaints(color);
        if (colors is { Count: > 0 })
        {
            text["color"] = colors[0]!.DeepClone();
        }

        return text;
    }

    public static string ColorToHex(KiwiObject color, double? extraOpacity = null)
    {
        var r = Channel(color.GetNumber("r"));
        var g = Channel(color.GetNumber("g"));
        var b = Channel(color.GetNumber("b"));
        var a = (color.GetNumber("a") ?? 1) * (extraOpacity ?? 1);
        var alpha = Channel(a);

        return alpha == 255
            ? $"#{r:X2}{g:X2}{b:X2}"
            : $"#{r:X2}{g:X2}{b:X2}{alpha:X2}";
    }

    private static int Channel(double? v) => (int)Math.Round(Math.Clamp(v ?? 0, 0, 1) * 255);

    private static double Round(double v) => Math.Round(v, 2);

    private static void AddString(JsonObject json, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            json[key] = value;
        }
    }
}
