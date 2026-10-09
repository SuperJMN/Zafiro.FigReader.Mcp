using System.Text.Json.Nodes;
using Zafiro.FigReader.Core.Extraction;
using Zafiro.FigReader.Core.Model;
using Zafiro.FigReader.Core.Resolution;
using Xunit;

namespace Zafiro.FigReader.Core.Tests;

public class SyntheticFigIntegrationTests
{
    [Fact]
    public void Loads_archive_and_builds_a_navigable_document()
    {
        using var fixture = new SyntheticFigFile();

        var doc = FigmaDocument.Build(FigFile.Load(fixture.Path));

        Assert.True(doc.File.IsArchive);
        Assert.Equal("Synthetic document", doc.Root?.Name);
        Assert.Equal("Fixture page", Assert.Single(doc.Pages).Name);
        Assert.All(doc.AllNodes, node =>
            Assert.All(node.Children, child => Assert.Same(node, child.Parent)));
    }

    [Fact]
    public void Extracts_simplified_tree_and_styles()
    {
        using var fixture = new SyntheticFigFile();
        var service = new FigmaService();
        var doc = service.Load(fixture.Path);

        var tree = service.NodeTree(doc, nodeId: null, depth: 1).AsArray();
        var styles = service.Styles(doc);

        Assert.Equal("Fixture page", Assert.Single(tree)!["name"]!.GetValue<string>());
        Assert.Contains(styles["colors"]!.AsArray(),
            color => color!["color"]!.GetValue<string>() == "#00FF00");
    }

    [Fact]
    public void Search_finds_text_overrides_and_reports_page_context()
    {
        using var fixture = new SyntheticFigFile();
        var service = new FigmaService();
        var doc = service.Load(fixture.Path);

        var results = service.Search(doc, "First label", type: null, limit: 10, nodeId: SyntheticFigFile.PageId);

        var match = Assert.IsType<JsonObject>(Assert.Single(results));
        Assert.Equal(SyntheticFigFile.InstanceId, match["id"]!.GetValue<string>());
        Assert.Equal("Fixture page", match["pageName"]!.GetValue<string>());
        Assert.Equal("Synthetic document > Fixture page > Example instance", match["path"]!.GetValue<string>());
        Assert.Equal("First label", match["matchedText"]!.GetValue<string>());
    }

    [Fact]
    public void Resolves_nested_labels_component_swaps_and_visibility()
    {
        using var fixture = new SyntheticFigFile();
        var doc = FigmaDocument.Build(FigFile.Load(fixture.Path));
        var instance = Assert.IsType<FigmaNode>(doc.FindById(SyntheticFigFile.InstanceId));

        var resolved = new InstanceResolver(doc).Resolve(instance);

        Assert.NotNull(resolved);
        Assert.Collection(resolved.Children,
            first =>
            {
                Assert.True(first.Visible);
                Assert.Equal("First label", first.Children.Single(child => child.Type == "TEXT").Text);
                Assert.Equal("Square icon",
                    first.Children.Single(child => child.Type == "INSTANCE").SwapComponentName);
            },
            second =>
            {
                Assert.False(second.Visible);
                Assert.Equal("Second label", second.Children.Single(child => child.Type == "TEXT").Text);
            });
    }

    [Fact]
    public void Decodes_embedded_vector_commands_into_svg_path()
    {
        using var fixture = new SyntheticFigFile();
        var service = new FigmaService();
        var doc = service.Load(fixture.Path);

        var vector = service.Vector(doc, SyntheticFigFile.VectorId, "fillGeometry");

        var path = Assert.Single(vector["paths"]!.AsArray());
        Assert.Equal("F1", path!["fillRule"]!.GetValue<string>());
        Assert.Equal("F1 M1,2 C3,4 5,6 7,8 Q9,10 11,12 Z", path["path"]!.GetValue<string>());
    }
}
