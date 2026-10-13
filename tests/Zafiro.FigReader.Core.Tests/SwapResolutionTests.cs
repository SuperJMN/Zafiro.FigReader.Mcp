using Zafiro.FigReader.Core.Extraction;
using Zafiro.FigReader.Core.Kiwi;
using Zafiro.FigReader.Core.Model;
using Zafiro.FigReader.Core.Resolution;
using static Zafiro.FigReader.Core.Tests.SyntheticFigFile;

namespace Zafiro.FigReader.Core.Tests;

/// <summary>
/// A button renders an icon wrapper whose inner icon is bound to an instance-swap property. The wrapper can
/// also be swapped as a whole for an alternative wrapper with a label.
/// </summary>
public class SwapResolutionTests
{
    private const int IconProperty = 90;
    private const string ButtonInstanceId = "1:40";

    [Fact]
    public void Property_swap_replaces_the_bound_layer_and_keeps_its_container()
    {
        var wrapper = ResolveWrapper(wrapper: [("componentPropAssignments", new[] { SwapAssignment(IconProperty, 11) })]);

        Assert.Equal("Icon wrapper", wrapper.Name);
        Assert.Null(wrapper.SwapComponentName);
        var icon = Assert.Single(wrapper.Children);
        Assert.Equal("Inner icon", icon.Name);
        Assert.Equal("Check", icon.SwapComponentName);
    }

    [Fact]
    public void Outer_property_assignment_wins_over_the_inner_one()
    {
        var wrapper = ResolveWrapper(
            wrapper: [("componentPropAssignments", new[] { SwapAssignment(IconProperty, 11) })],
            overrides: [Override([31], ("componentPropAssignments", new[] { SwapAssignment(IconProperty, 12) }))]);

        Assert.Equal("Star", Assert.Single(wrapper.Children).SwapComponentName);
    }

    [Fact]
    public void Swap_override_expands_the_new_component_and_applies_outer_overrides_inside_it()
    {
        var wrapper = ResolveWrapper(overrides:
        [
            Override([31], ("overriddenSymbolID", Id(25)), ("visible", false)),
            Override([31, 26], ("textData", Obj("Text", ("characters", "Deep"))))
        ]);

        Assert.Equal("Alt wrapper", wrapper.SwapComponentName);
        Assert.Equal("1:25", wrapper.SwapComponentId);
        Assert.False(wrapper.Visible);
        Assert.Equal("Deep", Assert.Single(wrapper.Children).Text);
    }

    [Fact]
    public void Property_swap_wins_over_a_swap_override_on_the_same_layer()
    {
        var wrapper = ResolveWrapper(
            wrapper: [("componentPropAssignments", new[] { SwapAssignment(IconProperty, 11) })],
            overrides: [Override([31, 21], ("overriddenSymbolID", Id(12)))]);

        Assert.Equal("Check", Assert.Single(wrapper.Children).SwapComponentName);
    }

    [Fact]
    public void Swap_to_a_missing_component_keeps_the_original_and_reports_it()
    {
        using var fixture = Fixture(overrides: [Override([31], ("overriddenSymbolID", Id(99)))]);
        var service = new FigmaService();

        var tree = service.NodeTree(service.Load(fixture.Path), ButtonInstanceId, depth: 2);

        var wrapper = Assert.Single(tree["children"]!.AsArray())!;
        Assert.Equal("1:99", wrapper["unresolvedSwap"]!.GetValue<string>());
        Assert.Null(wrapper["swappedTo"]);
        Assert.Equal("Inner icon", Assert.Single(wrapper["children"]!.AsArray())!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Swap_to_the_same_component_is_not_a_swap()
    {
        var wrapper = ResolveWrapper(
            wrapper: [("symbolData", Obj("SymbolData", ("symbolID", Id(20)),
                ("symbolOverrides", new[] { Override([21], ("visible", false)) })))],
            overrides: [Override([31], ("overriddenSymbolID", Id(20)))]);

        Assert.Null(wrapper.SwapComponentName);
        Assert.False(Assert.Single(wrapper.Children).Visible);
    }

    private static ResolvedNode ResolveWrapper(
        (string Name, object Value)[]? wrapper = null,
        KiwiObject[]? overrides = null)
    {
        using var fixture = Fixture(wrapper, overrides);
        var doc = FigmaDocument.Build(FigFile.Load(fixture.Path));

        var button = new InstanceResolver(doc).Resolve(doc.FindById(ButtonInstanceId)!);

        return Assert.Single(button!.Children);
    }

    private static SyntheticFigFile Fixture(
        (string Name, object Value)[]? wrapper = null,
        KiwiObject[]? overrides = null)
    {
        var wrapperFields = new List<(string Name, object Value)>
        {
            ("symbolData", Obj("SymbolData", ("symbolID", Id(20))))
        };
        foreach (var field in wrapper ?? [])
        {
            wrapperFields.RemoveAll(existing => existing.Name == field.Name);
            wrapperFields.Add(field);
        }

        return new SyntheticFigFile([
            .. Roots(),
            Node(10, "SYMBOL", "Gear", 2),
            Node(11, "SYMBOL", "Check", 2),
            Node(12, "SYMBOL", "Star", 2),
            Node(20, "SYMBOL", "Wrapper", 2),
            Node(21, "INSTANCE", "Inner icon", 20, ("symbolData", Obj("SymbolData", ("symbolID", Id(10)))),
                ("componentPropRefs", new[] { SwapRef(IconProperty) })),
            Node(25, "SYMBOL", "Alt wrapper", 2),
            Node(26, "TEXT", "Alt label", 25, ("textData", Obj("Text", ("characters", "Alt")))),
            Node(30, "SYMBOL", "Button", 2),
            Node(31, "INSTANCE", "Icon wrapper", 30, wrapperFields.ToArray()),
            Node(40, "INSTANCE", "Button instance", 2, ("symbolData", Obj("SymbolData",
                ("symbolID", Id(30)), ("symbolOverrides", overrides ?? [])))),
        ]);
    }
}
