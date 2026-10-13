using System.Text.Json.Nodes;
using Zafiro.FigReader.Core.Extraction;
using Zafiro.FigReader.Core.Kiwi;
using static Zafiro.FigReader.Core.Tests.SyntheticFigFile;

namespace Zafiro.FigReader.Core.Tests;

public class InstanceOverrideTests
{
    [Fact]
    public void Resolved_children_report_overridden_geometry_typography_and_fills()
    {
        using var fixture = new SyntheticFigFile([
            .. Roots(),
            .. ToyComponent(),
            Node(20, "INSTANCE", "Toy instance", 2, ("symbolData", Obj("SymbolData",
                ("symbolID", Id(10)), ("symbolOverrides", new[]
                {
                    Override([11],
                        ("textData", Obj("Text", ("characters", "Overridden"))),
                        ("size", Size(200, 40)), ("transform", Translation(30, 40)),
                        ("fontName", Obj("Font", ("family", "Monospace"), ("style", "Bold"))),
                        ("fontSize", 24f), ("fillPaints", new[] { Solid(0, 0, 1) }))
                }))))
        ]);

        var label = ResolvedChild(fixture, "1:20");

        Assert.Equal((30d, 40d, 200d, 40d), Bounds(label));
        var text = label["text"]!;
        Assert.Equal("Overridden", text["characters"]!.GetValue<string>());
        Assert.Equal("Monospace", text["fontFamily"]!.GetValue<string>());
        Assert.Equal("Bold", text["fontStyle"]!.GetValue<string>());
        Assert.Equal(24, text["fontSize"]!.GetValue<double>());
        Assert.Equal("#0000FF", text["color"]!.GetValue<string>());
        Assert.Equal("#0000FF", label["fills"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Resolved_children_use_derived_layout_with_explicit_overrides_taking_precedence()
    {
        using var fixture = new SyntheticFigFile([
            .. Roots(),
            .. ToyComponent(),
            Node(20, "INSTANCE", "Toy instance", 2,
                ("symbolData", Obj("SymbolData", ("symbolID", Id(10)), ("symbolOverrides", new[]
                {
                    Override([11], ("size", Size(300, 50)))
                }))),
                ("derivedSymbolData", new[]
                {
                    Obj("Derived", ("guidPath", Path(11)), ("size", Size(150, 30)), ("transform", Translation(5, 6)))
                }))
        ]);

        var label = ResolvedChild(fixture, "1:20");

        Assert.Equal((5d, 6d, 300d, 50d), Bounds(label));
        Assert.Equal("Sans", label["text"]!["fontFamily"]!.GetValue<string>());
    }

    [Fact]
    public void Outer_and_inner_overrides_of_the_same_node_are_merged_field_by_field()
    {
        using var fixture = new SyntheticFigFile([
            .. Roots(),
            .. ToyComponent(),
            Node(30, "SYMBOL", "Wrapper", 2),
            Node(31, "INSTANCE", "Inner instance", 30, ("symbolData", Obj("SymbolData",
                ("symbolID", Id(10)), ("symbolOverrides", new[]
                {
                    Override([11], ("textData", Obj("Text", ("characters", "Inner text"))))
                })))),
            Node(40, "INSTANCE", "Outer instance", 2, ("symbolData", Obj("SymbolData",
                ("symbolID", Id(30)), ("symbolOverrides", new[]
                {
                    Override([31, 11], ("fontSize", 30f))
                }))))
        ]);

        var label = ResolvedChild(fixture, "1:40", depth: 3)["children"]![0]!;

        Assert.Equal("Inner text", label["text"]!["characters"]!.GetValue<string>());
        Assert.Equal(30, label["text"]!["fontSize"]!.GetValue<double>());
    }

    private static KiwiObject[] ToyComponent() =>
    [
        Node(10, "SYMBOL", "Toy component", 2),
        Node(11, "TEXT", "Toy label", 10,
            ("size", Size(100, 20)), ("transform", Translation(2, 4)),
            ("fontName", Obj("Font", ("family", "Sans"), ("style", "Regular"))), ("fontSize", 12f),
            ("textData", Obj("Text", ("characters", "Base"))), ("fillPaints", new[] { Solid(1, 0, 0) }))
    ];

    private static JsonNode ResolvedChild(SyntheticFigFile fixture, string instanceId, int depth = 2)
    {
        var service = new FigmaService();
        var tree = service.NodeTree(service.Load(fixture.Path), instanceId, depth);
        return Assert.Single(tree["children"]!.AsArray())!;
    }

    private static (double X, double Y, double Width, double Height) Bounds(JsonNode node) =>
        (node["x"]!.GetValue<double>(), node["y"]!.GetValue<double>(),
            node["width"]!.GetValue<double>(), node["height"]!.GetValue<double>());

    private static KiwiObject Path(params int[] ids) => Obj("GuidPath", ("guids", ids.Select(Id).ToArray()));

    private static KiwiObject Size(float width, float height) => Obj("Vector", ("x", width), ("y", height));

    private static KiwiObject Translation(float x, float y) =>
        Obj("Matrix", ("m00", 1f), ("m01", 0f), ("m02", x), ("m10", 0f), ("m11", 1f), ("m12", y));

    private static KiwiObject Solid(float r, float g, float b) =>
        Obj("Paint", ("type", "SOLID"), ("color", Obj("Color", ("r", r), ("g", g), ("b", b), ("a", 1f))));
}
