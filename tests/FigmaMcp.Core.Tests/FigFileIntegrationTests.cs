using FigmaMcp.Core.Extraction;
using FigmaMcp.Core.Model;
using Xunit;

namespace FigmaMcp.Core.Tests;

/// <summary>
/// End-to-end tests against a real <c>.fig</c> file. They are opt-in: set the environment variable
/// <c>FIGMA_SAMPLE_FIG</c> to the path of a <c>.fig</c> file to enable them. When it is unset (the
/// default, e.g. in CI without a sample), the tests pass trivially so the build stays green.
/// </summary>
public class FigFileIntegrationTests
{
    private static string? SamplePath =>
        Environment.GetEnvironmentVariable("FIGMA_SAMPLE_FIG") is { Length: > 0 } p && File.Exists(p)
            ? p
            : null;

    [Fact]
    public void Loads_real_file_and_builds_a_navigable_document()
    {
        if (SamplePath is not { } path)
        {
            return; // sample not configured; skip
        }

        var doc = FigmaDocument.Build(FigFile.Load(path));

        Assert.True(doc.NodeCount > 0);
        Assert.NotNull(doc.Root);

        // Every linked child must resolve back to its parent.
        foreach (var node in doc.AllNodes)
        {
            foreach (var child in node.Children)
            {
                Assert.Same(node, child.Parent);
            }
        }
    }

    [Fact]
    public void Extracts_simplified_tree_and_styles()
    {
        if (SamplePath is not { } path)
        {
            return; // sample not configured; skip
        }

        var service = new FigmaService();
        var doc = service.Load(path);

        var tree = service.NodeTree(doc, nodeId: null, depth: 1);
        Assert.NotNull(tree);

        var styles = service.Styles(doc);
        Assert.NotNull(styles["colors"]);
    }
}
