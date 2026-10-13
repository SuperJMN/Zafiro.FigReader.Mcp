using System.Text.Json.Nodes;
using Zafiro.FigReader.Core.Extraction;
using Zafiro.FigReader.Core.Kiwi;
using static Zafiro.FigReader.Core.Tests.SyntheticFigFile;

namespace Zafiro.FigReader.Core.Tests;

public class VariableResolutionTests
{
    private const string SwatchId = "1:30";

    [Fact]
    public void Bound_color_follows_the_alias_chain_in_node_tree_and_styles()
    {
        using var fixture = new SyntheticFigFile([
            .. Roots(),
            Swatch(Alias(Id(31))),
            Collection(40, null, ("Only mode", 100, "!")),
            Variable(31, 40, null, (100, AliasValue(Alias(Id(32))))),
            Variable(32, 40, null, (100, ColorValue(0, 1, 0)))
        ]);
        var service = new FigmaService();
        var doc = service.Load(fixture.Path);

        var node = service.NodeDetail(doc, SwatchId, raw: false);
        var tree = service.NodeTree(doc, SwatchId, depth: 0);
        var colors = service.Styles(doc)["colors"]!.AsArray();

        Assert.Equal("#00FF00", node["fills"]![0]!.GetValue<string>());
        Assert.Equal("#00FF00", tree["fills"]![0]!.GetValue<string>());
        Assert.Contains(colors, color => color!["color"]!.GetValue<string>() == "#00FF00");
        Assert.DoesNotContain(colors, color => color!["color"]!.GetValue<string>() == "#FF0000");
    }

    [Fact]
    public void Variables_are_read_in_the_first_mode_of_their_collection()
    {
        using var fixture = new SyntheticFigFile([
            .. Roots(),
            Swatch(Alias(Id(31))),
            Collection(40, null, ("Dark", 101, "\""), ("Light", 100, "!")),
            Variable(31, 40, null, (101, ColorValue(0, 0, 1)), (100, ColorValue(0, 1, 0)))
        ]);
        var service = new FigmaService();

        var node = service.NodeDetail(service.Load(fixture.Path), SwatchId, raw: false);

        Assert.Equal("#00FF00", node["fills"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Library_variables_are_found_by_asset_key()
    {
        using var fixture = new SyntheticFigFile([
            .. Roots(),
            Swatch(Obj("Reference", ("assetRef", Obj("AssetRef", ("key", "variable-key"), ("version", "1:1"))))),
            Collection(40, "collection-key", ("Light", 100, "!")),
            Variable(31, null, "variable-key", (100, ColorValue(0, 1, 0)))
        ]);
        var service = new FigmaService();

        var node = service.NodeDetail(service.Load(fixture.Path), SwatchId, raw: false);

        Assert.Equal("#00FF00", node["fills"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Unresolved_bindings_are_reported_instead_of_the_literal_color()
    {
        using var fixture = new SyntheticFigFile([.. Roots(), Swatch(Alias(Id(99)))]);
        var service = new FigmaService();
        var doc = service.Load(fixture.Path);

        var fill = service.NodeDetail(doc, SwatchId, raw: false)["fills"]![0]!;
        var color = Assert.Single(service.Styles(doc)["colors"]!.AsArray())!;

        Assert.Equal("#FF0000", fill["color"]!.GetValue<string>());
        Assert.Equal("1:99", fill["unresolvedVariable"]!.GetValue<string>());
        Assert.Equal(1, color["unresolvedVariableUsages"]!.GetValue<int>());
    }

    /// <summary>A rectangle whose stored literal is red but whose color is bound to <paramref name="reference"/>.</summary>
    private static KiwiObject Swatch(KiwiObject reference) =>
        Node(30, "RECTANGLE", "Swatch", 2, ("fillPaints", new[]
        {
            Obj("Paint", ("type", "SOLID"), ("color", Obj("Color", ("r", 1f), ("g", 0f), ("b", 0f), ("a", 1f))),
                ("colorVar", AliasValue(reference)))
        }));

    private static KiwiObject Collection(int id, string? key, params (string Name, int Id, string SortPosition)[] modes)
    {
        var fields = new List<(string, object)>
        {
            ("variableSetModes", modes.Select(mode => Obj("Mode",
                ("id", Id(mode.Id)), ("name", mode.Name), ("sortPosition", mode.SortPosition))).ToArray())
        };
        if (key is not null)
        {
            fields.Add(("key", key));
        }

        return Node(id, "VARIABLE_SET", "Collection", 2, fields.ToArray());
    }

    private static KiwiObject Variable(int id, int? collection, string? key, params (int Mode, KiwiObject Data)[] values)
    {
        var fields = new List<(string, object)>
        {
            ("variableResolvedType", "COLOR"),
            ("variableDataValues", Obj("VariableValues", ("entries", values.Select(value => Obj("VariableEntry",
                ("modeID", Id(value.Mode)), ("variableData", value.Data))).ToArray())))
        };
        fields.Add(("variableSetID", collection is { } set
            ? Obj("Reference", ("guid", Id(set)))
            : Obj("Reference", ("assetRef", Obj("AssetRef", ("key", "collection-key"), ("version", "1:1"))))));
        if (key is not null)
        {
            fields.Add(("key", key));
        }

        return Node(id, "VARIABLE", $"Variable {id}", 2, fields.ToArray());
    }

    private static KiwiObject Alias(KiwiObject guid) => Obj("Reference", ("guid", guid));

    private static KiwiObject AliasValue(KiwiObject reference) =>
        Data(Obj("VariableValue", ("alias", reference)), "ALIAS");

    private static KiwiObject ColorValue(float r, float g, float b) =>
        Data(Obj("VariableValue", ("colorValue", Obj("Color", ("r", r), ("g", g), ("b", b), ("a", 1f)))), "COLOR");

    private static KiwiObject Data(KiwiObject value, string dataType) =>
        Obj("VariableData", ("value", value), ("dataType", dataType), ("resolvedDataType", "COLOR"));
}
